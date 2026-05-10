using System.Diagnostics;
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

namespace NetBannerNG.Service
{
    /// <summary>
    ///     Named pipe server based on H.Pipes library. Check the reference article for more.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    internal class NamedPipeServer : IAsyncDisposable
    {
        private readonly SingleConnectionPipeServer<PipeMessage> _server;
        private readonly uint _sessionId;

        private sealed class AuthorizedClientContext
        {
            public string PipeName { get; set; } = string.Empty;
            public string? UserName { get; set; }
            public object? Connection { get; set; }
        }

        private AuthorizedClientContext? _authorizedClient;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly TaskScheduler _scheduler = TaskScheduler.Default;

        internal NamedPipeServer(uint sessionId, int timeout = 10000)
        {
            _sessionId = sessionId;
            var pipeName = PipeNaming.ForSession(sessionId);
            _server = new SingleConnectionPipeServer<PipeMessage>(pipeName, new MessagePackFormatter());
            ConfigurePipeSecurity(_server);

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

        private static void ConfigurePipeSecurity(SingleConnectionPipeServer<PipeMessage> server)
        {
            _ = PrivilegeHelper.TryGetActiveUserSid(out var interactiveUserSid);
            server.SetPipeSecurity(PipeSecurityPolicy.CreateDefaultServerSecurity(interactiveUserSid));
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
            _authorizedClient = authorizedClient;

            Program.Log.LogInformation(EventLogCatalog.PipeClientAuthorizationAccepted, _sessionId, args.Connection.PipeName);
            Program.Log.LogInformation(EventLogCatalog.PipeClientConnected, args.Connection.PipeName);
            Debug.WriteLine($"Client {args.Connection.PipeName} is now connected!");
            Debug.WriteLine($"[PipeServer]  ClientConnected pipe={args.Connection.PipeName}");
            Debug.WriteLine("Checking child process owner.");
            var isAdmin = false;
            try
            {
                isAdmin = PrivilegeHelper.IsSessionOwnerAdmin;
            }
            catch (Exception ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionAdminQueryFailed, ex.GetMessageStack());
            }
            Debug.WriteLine($"Child process owner has admin rights: {isAdmin}");

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
        }

        private void OnClientDisconnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
            _authorizedClient = null;
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
            var authorizedClient = _authorizedClient;
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
                hex.AppendFormat("{0:x2}", b);
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

            if (!TryAuthorizeClientIdentity(connection, activeUserSid, out var connectionUserName))
            {
                return false;
            }

            authorizedClient = new AuthorizedClientContext
            {
                PipeName = connectedPipeName ?? string.Empty,
                UserName = connectionUserName,
                Connection = connection
            };
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

        internal static bool TryAuthorizeClientIdentity(object connection, SecurityIdentifier activeUserSid, out string? connectionUserName)
        {
            connectionUserName = null;
            var connectionType = connection.GetType();
            var userSidProperty = connectionType.GetProperty("UserSid", BindingFlags.Instance | BindingFlags.Public);
            if (userSidProperty?.GetValue(connection) is SecurityIdentifier sidValue)
            {
                return sidValue == activeUserSid;
            }

            if (userSidProperty?.GetValue(connection) is string sidText && !string.IsNullOrWhiteSpace(sidText))
            {
                return string.Equals(sidText, activeUserSid.Value, StringComparison.OrdinalIgnoreCase);
            }

            var userNameProperty = connectionType.GetProperty("UserName", BindingFlags.Instance | BindingFlags.Public)
                ?? connectionType.GetProperty("ImpersonationUserName", BindingFlags.Instance | BindingFlags.Public);
            connectionUserName = userNameProperty?.GetValue(connection) as string;
            if (!string.IsNullOrWhiteSpace(connectionUserName))
            {
                try
                {
                    var expectedAccount = activeUserSid.Translate(typeof(NTAccount)).Value;
                    return string.Equals(connectionUserName, expectedAccount, StringComparison.OrdinalIgnoreCase);
                }
                catch (IdentityNotMappedException)
                {
                    return false;
                }
            }

            return false;
        }

        internal static bool IsAuthorizedConnectionInstance(object? authorizedConnection, object? inboundConnection) => authorizedConnection != null && ReferenceEquals(authorizedConnection, inboundConnection);
    }
}
