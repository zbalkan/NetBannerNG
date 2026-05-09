using System;
using System.Diagnostics;
using System.IO;
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
        private static readonly ThreadLocal<Random> ThreadRandom = new(() => new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

        public NamedPipeClient(string pipeName, int timeout = 10000)
        {
            _client = new SingleConnectionPipeClient<PipeMessage>(pipeName, formatter: new MessagePackFormatter());

            _client.MessageReceived += OnMessageReceived!;
            _client.Disconnected += OnDisconnected!;
            _client.Connected += OnConnected!;
            _client.ExceptionOccurred += OnExceptionOccurred!;

            _timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromMilliseconds(timeout));

            var retryPolicy = Policy
                .Handle<OperationCanceledException>()
                .Or<InvalidOperationException>()
                .Or<IOException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100 + (ThreadRandom.Value?.Next(25, 150) ?? 100)),
                    onRetry: (exception, delay, attempt, _) => {
                        DebugTrace($"Retry attempt={attempt} delay_ms={(int)delay.TotalMilliseconds} reason={exception.GetType().Name}");
                    });

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
            try
            {
                await ExecuteWithResilience(async cancellationToken => await _client.ConnectAsync(cancellationToken));

                result = true;
            }
            catch (Exception ex)
            {
                DebugTrace($"InitializeFailed reason={ex.GetType().Name} message={ex.Message}");
                await _client.DisposeAsync();
                result = false;
            }

            return result;
        }

        internal async Task SendException(string message)
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
                    await _client.WriteAsync(outboundMessage, cancellationToken);
                    DebugTrace($"OutboundSent action={outboundMessage.Action} text_len={outboundMessage.Text?.Length ?? 0} checksum_len={outboundMessage.Checksum?.Length ?? 0}");
                });
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

        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void OnConnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
        }

        private void OnDisconnected(object o, ConnectionEventArgs<PipeMessage> args) => Application.Current.Dispatcher.Invoke(App.ShutDownGracefully);

        private void OnMessageReceived(object sender, ConnectionMessageEventArgs<PipeMessage> args)
        {
            if (args.Message == null)
            {
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
                        DebugTrace($"InboundIsAdmin value={args.Message.Text}");
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