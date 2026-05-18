using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using H.Formatters;
using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;
using Polly;
using Polly.Timeout;

namespace NetBannerNG.Watchdog
{
    /// <summary>
    ///     Named pipe server based on H.Pipes library. Check the reference article for more.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    internal class NamedPipeServer : IAsyncDisposable
    {
#pragma warning disable CA1823 // Avoid unused private fields
        private const string IdentityFallbackEnvironmentVariable = "NETBANNERNG_PIPE_IDENTITY_FALLBACK";
#pragma warning restore CA1823 // Avoid unused private fields
        private static readonly bool EnableIdentityFallback = ResolveIdentityFallbackMode();
        private readonly SingleConnectionPipeServer<PipeMessage> _server;
        private readonly uint _sessionId;

        private sealed class AuthorizedClientContext
        {
            public string PipeName { get; set; } = string.Empty;
            public object? Connection { get; set; }
        }

        private AuthorizedClientContext? _authorizedClient;
        private readonly object _authorizedClientSync = new();
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly TaskScheduler _scheduler = TaskScheduler.Default;

        private NamedPipeServer(uint sessionId, SecurityIdentifier interactiveUserSid, int timeout)
        {
            _sessionId = sessionId;
            Program.Log.LogInformation(EventLogCatalog.PipeIdentityFallbackMode, EnableIdentityFallback);
            var pipeName = PipeNaming.ForSession(sessionId);
            _server = new SingleConnectionPipeServer<PipeMessage>(pipeName, new MessagePackFormatter());
            _server.SetPipeSecurity(PipeSecurityPolicy.CreateDefaultServerSecurity(interactiveUserSid));

            _server.ClientConnected += OnClientConnected!;
            _server.ClientDisconnected += OnClientDisconnected!;
            _server.MessageReceived += OnMessageReceived!;
            _server.ExceptionOccurred += OnExceptionOccurred!;

            _timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromMilliseconds(timeout), (context, timespan, task) => {
                    _ = task.ContinueWith(t => {
                        if (t.IsFaulted)
                        {
                            Program.Log.LogInformation(EventLogCatalog.PipeTimeoutCallback, context.PolicyKey, context.OperationKey, timespan.TotalSeconds, t.Exception!);
                        }
                        else
                        {
                            Program.Log.LogInformation(EventLogCatalog.PipeTimeoutTaskCompleted);
                        }
                    },
                      scheduler: _scheduler);

                    return Task.CompletedTask;
                });
        }

        internal static bool TryCreate(uint sessionId, out NamedPipeServer? server, int timeout = 10000)
        {
            if (!PrivilegeHelper.TryGetActiveUserSid(out var interactiveUserSid) || interactiveUserSid == null)
            {
                server = null;
                return false;
            }

            server = new NamedPipeServer(sessionId, interactiveUserSid, timeout);
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            await _server.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        internal async Task InitializeAsync()
        {
            await _server.StartAsync().ConfigureAwait(false);
            Debug.WriteLine($"Created pipe: {_server.PipeName}");
        }

        private void OnClientConnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
            var connectionTask = OnClientConnectedAsync(args);
            _ = connectionTask.ContinueWith(task => {
                Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, task.Exception?.GetMessageStack() ?? "Unknown async connection error");
            },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                _scheduler);
        }

        private async Task OnClientConnectedAsync(ConnectionEventArgs<PipeMessage> args)
        {
            if (!TryBuildAuthorizedClient(args.Connection.PipeName, args.Connection, out var authorizedClient))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeClientAuthorizationRejected, _sessionId, args.Connection.PipeName);
                ServiceHost.ReportDeniedClient();
                Debug.WriteLine($"[PipeServer] ClientRejected expected_session={_sessionId} pipe={args.Connection.PipeName}");
                return;
            }
            // Connection callbacks are raised on thread-pool threads; guard authoritative client binding.
            lock (_authorizedClientSync) { _authorizedClient = authorizedClient; }

            Program.Log.LogInformation(EventLogCatalog.PipeClientAuthorizationAccepted, _sessionId, args.Connection.PipeName);
            Program.Log.LogInformation(EventLogCatalog.PipeClientConnected, args.Connection.PipeName);
            Debug.WriteLine($"Client {args.Connection.PipeName} is now connected!");
            Debug.WriteLine($"[PipeServer]  ClientConnected pipe={args.Connection.PipeName}");
            Debug.WriteLine("Checking child process owner.");
            var isAdmin = false;
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                isAdmin = PrivilegeHelper.IsSessionOwnerAdmin;
            }
            catch (Exception ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionAdminQueryFailed, ex.GetMessageStack());
            }
#pragma warning restore CA1031 // Do not catch general exception types
            Debug.WriteLine($"Child process owner has admin rights: {isAdmin}");

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                var bootstrapMessage = new PipeMessage
                {
                    Action = ActionType.IsAdmin,
                    Text = $"{isAdmin}"
                };
                bootstrapMessage.Checksum = PipeMessageChecksum.Compute(bootstrapMessage);
                await _timeoutPolicy
                    .ExecuteAsync(
                        _ => args.Connection.WriteAsync(bootstrapMessage),
                        new Context($"{nameof(NamedPipeServer)}.{nameof(OnClientConnectedAsync)}.{nameof(args.Connection.WriteAsync)}"))
                    .ConfigureAwait(false);
                Debug.WriteLine($"[PipeServer]  BootstrapSent action={bootstrapMessage.Action} checksum_len={bootstrapMessage.Checksum?.Length ?? 0} checksum={ByteArrayToString(bootstrapMessage.Checksum!)}");
            }
            catch (IOException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeBootstrapDisconnected, ex.GetMessageStack());
                Debug.WriteLine($"[PipeServer]  BootstrapSendFailed error={ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeBootstrapClientNotConnected, ex.GetMessageStack());
                Debug.WriteLine($"[PipeServer]  BootstrapSendSkipped reason=ClientNotConnected error={ex.Message}");
            }
            catch (Exception ex)
            {
                Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, ex.GetMessageStack());
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private void OnClientDisconnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
            // Disconnect can race with inbound messages; clear the authorized reference atomically.
            lock (_authorizedClientSync) { _authorizedClient = null; }
            ServiceHost.ReportConnectionChurn();
            Program.Log.LogInformation(EventLogCatalog.PipeClientDisconnected, args.Connection.PipeName);
            Program.Log.LogInformation(EventLogCatalog.PipeAutoRestartDisabled);
            Debug.WriteLine($"[PipeServer]  ClientDisconnected pipe={args.Connection.PipeName}");
        }

        private void OnExceptionOccurred(object o, ExceptionEventArgs args)
        {
            Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, args.Exception.GetMessageStack());
            Debug.WriteLine($"Exception occurred in pipe: {args.Exception.GetMessageStack()}");
        }

        private void OnMessageReceived(object sender, ConnectionMessageEventArgs<PipeMessage> args)
        {
            AuthorizedClientContext? authorizedClient;
            // Snapshot under lock so revalidation checks operate on a consistent authorized-client view.
            lock (_authorizedClientSync) { authorizedClient = _authorizedClient; }
            if (authorizedClient == null)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundRejectedUnauthorizedSession, _sessionId, args.Connection.PipeName);
                return;
            }
            if (!string.Equals(authorizedClient.PipeName, args.Connection.PipeName, StringComparison.Ordinal))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundRejectedUnauthorizedSession, _sessionId, args.Connection.PipeName);
                return;
            }
            if (!IsAuthorizedConnectionInstance(authorizedClient.Connection, args.Connection))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundRejectedUnauthorizedSession, _sessionId, args.Connection.PipeName);
                ServiceHost.ReportDeniedInbound();
                return;
            }

            var activeSessionId = PrivilegeHelper.GetInteractiveSessionId();
            if (activeSessionId != _sessionId)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundSessionRevalidationFailed, _sessionId, activeSessionId, args.Connection.PipeName);
                ServiceHost.ReportDeniedInbound();
                return;
            }

            if (!PrivilegeHelper.TryGetActiveUserSid(out var activeUserSid) || activeUserSid == null || !TryAuthorizeClientIdentity(args.Connection, activeUserSid, args.Connection.PipeName, EnableIdentityFallback))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundIdentityRevalidationFailed, _sessionId, args.Connection.PipeName);
                ServiceHost.ReportDeniedInbound();
                return;
            }

            if (args.Message == null)
            {
                return;
            }

            if (!PipeMessageValidator.IsValidInboundClientMessage(args.Message))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundRejected);
                Debug.WriteLine($"[PipeServer]  InboundRejected action={args.Message.Action} text_len={args.Message.Text?.Length ?? 0} checksum_len={args.Message.Checksum.Length} checksum={ByteArrayToString(args.Message.Checksum)}");
                return;
            }

            switch (args.Message)
            {
                case { Action: ActionType.SendLog }:
                    var sanitizedText = PipeLogSanitizer.SanitizeForSingleLineLog(args.Message.Text);
                    Program.Log.LogError(EventLogCatalog.PipeClientForwardedLog, args.Connection.PipeName, Environment.NewLine, sanitizedText);
                    Debug.WriteLine($"[PipeServer]  InboundAccepted action={args.Message.Action} text_len={args.Message.Text?.Length ?? 0}");
                    break;

                default:
                    Program.Log.LogWarning(EventLogCatalog.PipeUnknownActionType, args.Message.Action);
                    break;
            }
        }

        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
            }

            return hex.ToString();
        }

        private bool TryBuildAuthorizedClient(string? connectedPipeName, object connection, out AuthorizedClientContext authorizedClient)
        {
            authorizedClient = null!;
            if (string.IsNullOrWhiteSpace(connectedPipeName))
            {
                return false;
            }
            var activeSessionId = PrivilegeHelper.GetInteractiveSessionId();
            if (!IsAuthorizedClientConnection(_sessionId, connectedPipeName, activeSessionId))
            {
                return false;
            }

            if (!PrivilegeHelper.TryGetActiveUserSid(out var activeUserSid) || activeUserSid == null)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeClientAuthorizationRejected, _sessionId, connectedPipeName!);
                return false;
            }

            if (!TryAuthorizeClientIdentity(connection, activeUserSid, connectedPipeName, EnableIdentityFallback))
            {
                return false;
            }

#pragma warning disable CA1508 // Avoid dead conditional code
            authorizedClient = new AuthorizedClientContext
            {
                PipeName = connectedPipeName ?? string.Empty,
                Connection = connection
            };
#pragma warning restore CA1508 // Avoid dead conditional code
            return true;
        }

        internal static bool IsAuthorizedClientConnection(uint expectedSessionId, string? connectedPipeName, uint activeSessionId)
        {
            var expectedPipeName = PipeNaming.ForSession(expectedSessionId);
            if (!string.Equals(expectedPipeName, connectedPipeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return activeSessionId == expectedSessionId;
        }

        internal static bool TryAuthorizeClientIdentity(object connection, SecurityIdentifier activeUserSid, string? pipeName, bool allowIdentityFallback = false)
        {
            var connectionType = connection.GetType();
            var userSidProperty = connectionType.GetProperty("UserSid", BindingFlags.Instance | BindingFlags.Public);
            if (userSidProperty?.GetValue(connection) is SecurityIdentifier sidValue)
            {
                if (sidValue == activeUserSid)
                {
                    return true;
                }

                return false;
            }

            if (userSidProperty?.GetValue(connection) is string sidText && !string.IsNullOrWhiteSpace(sidText))
            {
                if (string.Equals(sidText, activeUserSid.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }

            if (!allowIdentityFallback)
            {
                return false;
            }

            var userNameProperty = connectionType.GetProperty("UserName", BindingFlags.Instance | BindingFlags.Public)
                ?? connectionType.GetProperty("ImpersonationUserName", BindingFlags.Instance | BindingFlags.Public);
            if (userNameProperty?.GetValue(connection) is string userNameValue && !string.IsNullOrWhiteSpace(userNameValue))
            {
                var currentUserName = WindowsIdentity.GetCurrent().Name;
                return string.Equals(currentUserName, userNameValue, StringComparison.OrdinalIgnoreCase);
            }

            // In interactive/debug mode we can encounter transports that do not expose identity metadata.
            // When fallback is explicitly enabled, the pipe ACL and session-bound pipe name remain the
            // authoritative guardrails, so allow the connection to proceed.
            Program.Log.LogError(EventLogCatalog.PipeIdentityFallbackUsed, connectionType.FullName ?? connectionType.Name, pipeName ?? string.Empty, "Connection did not expose SID or username metadata.");
            Program.Log.LogWarning(EventLogCatalog.PipeClientAuthorizationRejected, 0,
                "SID/username not available on connection; allowing due to interactive fallback.");
            return true;
        }

        internal static bool TryAuthorizeClientIdentity(object connection, SecurityIdentifier activeUserSid, bool allowInteractiveUserNameFallback = false) =>
            TryAuthorizeClientIdentity(connection, activeUserSid, string.Empty, allowInteractiveUserNameFallback);

        internal static bool IsAuthorizedConnectionInstance(object? authorizedConnection, object? inboundConnection) => authorizedConnection != null && ReferenceEquals(authorizedConnection, inboundConnection);

        private static bool ResolveIdentityFallbackMode()
        {
#if DEBUG
            var value = Environment.GetEnvironmentVariable(IdentityFallbackEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return bool.TryParse(value, out var parsed) && parsed;
#else
    return false;
#endif
        }
    }
}