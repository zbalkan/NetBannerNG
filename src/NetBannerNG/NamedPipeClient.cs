using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using H.Formatters;
using H.Pipes;
using H.Pipes.Args;
using NetBannerNG.Common.NamedPipes;
using NetBannerNG.Utils;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.Wrap;

namespace NetBannerNG
{
    /// <summary>
    ///     Cross-process control channel for the desktop UI.
    ///
    ///     Why this pipe exists:
    ///     - the Windows Service runs in Session 0 and cannot directly drive a user-session WPF window,
    ///     - the UI process runs in the interactive user session and owns border rendering,
    ///     - the service receives client diagnostics and sends bootstrap state (for example admin status).
    ///
    ///     Named pipes provide local, low-latency IPC between those two processes without opening network ports.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    public class NamedPipeClient : IAsyncDisposable
    {
        private const int MaxMessageTextLength = 4096;

        private readonly SingleConnectionPipeClient<PipeMessage> _client;
        private readonly AsyncPolicyWrap _resiliencePolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly TaskCompletionSource<bool> _bootstrapReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _serverTrustSync = new();
        private PipeStream? _verifiedStream;
        private PipeStream? _rejectedStream;
        private static readonly ThreadLocal<Random> ThreadRandom = new(() => new Random(Guid.NewGuid().GetHashCode()));

        public NamedPipeClient(string pipeName, int timeout = 10000)
        {
            _client = new SingleConnectionPipeClient<PipeMessage>(pipeName, formatter: new MessagePackFormatter())
            {
                // The service never needs to act as this user; identification is enough for it to
                // learn who connected, and it denies a squatting server a usable impersonation token.
                CreatePipeStreamFunc = (name, serverName) => new NamedPipeClientStream(
                    serverName,
                    name,
                    PipeDirection.InOut,
                    PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Identification)
            };

            _client.MessageReceived += OnMessageReceived!;
            _client.Connected += OnConnected!;
            _client.Disconnected += OnDisconnected!;
            _client.ExceptionOccurred += OnExceptionOccurred!;

            _timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromMilliseconds(timeout));

#pragma warning disable CA5394 // Do not use insecure randomness
            var retryPolicy = Policy
                .Handle<OperationCanceledException>()
                .Or<InvalidOperationException>()
                .Or<IOException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100 + (ThreadRandom.Value?.Next(25, 150) ?? 100)),
                    onRetry: (exception, delay, attempt, _) => DebugTrace($"Retry attempt={attempt} delay_ms={(int)delay.TotalMilliseconds} reason={exception.GetType().Name}"));
#pragma warning restore CA5394 // Do not use insecure randomness

            var breakerPolicy = Policy
                .Handle<OperationCanceledException>()
                .Or<InvalidOperationException>()
                .Or<IOException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(5),
                    onBreak: (exception, breakDelay) => DebugTrace($"CircuitOpen duration_ms={(int)breakDelay.TotalMilliseconds} reason={exception.GetType().Name}"),
                    onReset: () => DebugTrace("CircuitReset"));

            _resiliencePolicy = Policy.WrapAsync(retryPolicy, breakerPolicy);
        }

        public async Task<bool> InitializeAsync()
        {
            bool result;
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                await ExecuteWithResilience(_client.ConnectAsync).ConfigureAwait(false);

                result = IsServerTrusted();
                if (!result)
                {
                    DebugTrace("InitializeFailed reason=UntrustedServer");
                    await _client.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                DebugTrace($"InitializeFailed reason={ex.GetType().Name} message={ex.Message}");
                await _client.DisposeAsync().ConfigureAwait(false);
                result = false;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return result;
        }

        internal async Task SendLog(string message)
        {
            if (_client is not { IsConnected: true })
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message.Length > MaxMessageTextLength)
            {
                message = message.Substring(0, MaxMessageTextLength);
            }

            try
            {
                await ExecuteWithResilience(async cancellationToken => {
                    var outboundMessage = new PipeMessage
                    {
                        Action = ActionType.SendLog,
                        Text = message,
                    };
                    outboundMessage.Checksum = PipeMessageChecksum.Compute(outboundMessage);
                    await _client.WriteAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
                    DebugTrace($"OutboundSent action={outboundMessage.Action} text_len={outboundMessage.Text?.Length ?? 0} checksum_len={outboundMessage.Checksum?.Length ?? 0}");
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Task cancelled after timeout.");
            }
            catch (InvalidOperationException ex)
            {
                DebugTrace($"OutboundSendSkipped reason=ClientNotConnected message={ex.Message}");
            }
            catch (IOException ex)
            {
                DebugTrace($"OutboundSendFailed reason=IoException message={ex.Message}");
            }
            catch (BrokenCircuitException ex)
            {
                DebugTrace($"OutboundSendSkipped reason=CircuitOpen message={ex.Message}");
            }
        }

        internal async Task<bool> ReportReadyAsync()
        {
            if (_client is not { IsConnected: true })
            {
                return false;
            }

            try
            {
                await _timeoutPolicy.ExecuteAsync(_ => _bootstrapReceived.Task, CancellationToken.None).ConfigureAwait(false);
                await ExecuteWithResilience(async cancellationToken => {
                    var readyMessage = new PipeMessage
                    {
                        Action = ActionType.Ready,
                        Text = "1",
                    };
                    readyMessage.Checksum = PipeMessageChecksum.Compute(readyMessage);
                    await _client.WriteAsync(readyMessage, cancellationToken).ConfigureAwait(false);
                    DebugTrace("ReadinessSent protocol=1");
                }).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (BrokenCircuitException)
            {
                return false;
            }
            catch (TimeoutRejectedException)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _client.MessageReceived -= OnMessageReceived!;
            _client.Connected -= OnConnected!;
            _client.Disconnected -= OnDisconnected!;
            _client.ExceptionOccurred -= OnExceptionOccurred!;

            await _client.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void OnConnected(object o, ConnectionEventArgs<PipeMessage> args) =>
            DebugTrace("Connected");

        private void OnDisconnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
            DebugTrace("Disconnected");
            _bootstrapReceived.TrySetCanceled();
            Application.Current?.Dispatcher?.BeginInvoke(new Action(App.ShutDownGracefully));
        }

        private void OnMessageReceived(object sender, ConnectionMessageEventArgs<PipeMessage> args)
        {
            if (args.Message == null)
            {
                return;
            }

            // H.Pipes starts reading before it raises Connected, so the first message can arrive
            // before InitializeAsync has checked the endpoint. Verify here as well.
            if (!IsServerTrusted())
            {
                DebugTrace($"InboundDroppedUntrustedServer action={args.Message.Action}");
                return;
            }

            if (!PipeMessageChecksum.IsValid(args.Message))
            {
                DebugTrace($"InboundInvalidChecksum action={args.Message.Action} text_len={args.Message.Text?.Length ?? 0}");
                return;
            }

            switch (args.Message.Action)
            {
                case ActionType.IsAdmin:
                    {
                        if (!bool.TryParse(args.Message.Text, out var isAdmin))
                        {
                            DebugTrace($"InboundIsAdminInvalid value={args.Message.Text}");
                            return;
                        }

                        AdminHelper.IsAdmin = isAdmin;
                        _bootstrapReceived.TrySetResult(true);
                        DebugTrace($"InboundIsAdmin value={isAdmin}");
                        break;
                    }
                case ActionType.Unknown:
                    {
                        break;
                    }
                default:
                    {
                        DebugTrace($"InboundUnhandled action={args.Message.Action}");
                        break;
                    }
            }
        }

        // Pipe names are predictable, so another local user can create the expected pipe before
        // the service does. Trust the endpoint only if the kernel reports the installed watchdog
        // service (session 0, expected image path) as the server process. Cached per stream.
        private bool IsServerTrusted()
        {
            var stream = _client.Connection?.PipeStream;
            if (stream == null)
            {
                return false;
            }

            lock (_serverTrustSync)
            {
                if (ReferenceEquals(stream, _verifiedStream))
                {
                    return true;
                }

                if (ReferenceEquals(stream, _rejectedStream))
                {
                    return false;
                }

                if (VerifyServer(stream))
                {
                    _verifiedStream = stream;
                    return true;
                }

                _rejectedStream = stream;
                DebugTrace("ServerVerificationFailed");
                return false;
            }
        }

#pragma warning disable IDE0022 // Use expression body for method
        private static bool VerifyServer(PipeStream stream)
        {
#if DEBUG
            // Debug builds host the watchdog interactively (not as a session-0 service).
            return stream.IsConnected;
#else
            return PipeEndpointIdentity.IsServerInstalledWatchdog(stream);
#endif
        }
#pragma warning restore IDE0022 // Use expression body for method

        private void OnExceptionOccurred(object o, ExceptionEventArgs args) => DebugTrace($"PipeException {args.Exception.Message}");

        [Conditional("DEBUG")]
        private static void DebugTrace(string message) => Debug.WriteLine($"[PipeClient] {message}");

        private async Task ExecuteWithResilience(Func<CancellationToken, Task> action) =>
            await _resiliencePolicy.ExecuteAsync(
                async cancellationToken =>
                    await _timeoutPolicy.ExecuteAsync(action, cancellationToken).ConfigureAwait(false),
                CancellationToken.None).ConfigureAwait(false);
    }
}