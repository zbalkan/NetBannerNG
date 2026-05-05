using H.Formatters;
using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;
using Polly;
using Polly.Timeout;
using System.Diagnostics;
using System.Text;

namespace NetBannerNG.Service
{
    /// <summary>
    ///     Named pipe server based on H.Pipes library. Check the reference article for more.
    /// </summary>
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    internal class NamedPipeServer : IAsyncDisposable
    {
        private const string PipeName = "netbannerng-pipe";
        private static SingleConnectionPipeServer<PipeMessage>? _server;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly TaskScheduler _scheduler = TaskScheduler.Default;

        internal NamedPipeServer(int timeout = 10000)
        {
            _server = new SingleConnectionPipeServer<PipeMessage>(PipeName, new MessagePackFormatter());
            ConfigurePipeSecurity(_server);

            _server.ClientConnected += OnClientConnected!;
            _server.ClientDisconnected += OnClientDisconnected!;
            _server.MessageReceived += OnMessageReceived!;
            _server.ExceptionOccurred += OnExceptionOccurred!;

            _timeoutPolicy = Policy
                .TimeoutAsync(TimeSpan.FromMilliseconds(timeout), (context, timespan, task) =>
                {
                    _ = task.ContinueWith(t =>
                      {
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


        private static void ConfigurePipeSecurity(SingleConnectionPipeServer<PipeMessage> server) => server.SetPipeSecurity(PipeSecurityPolicy.CreateDefaultServerSecurity());

        public async ValueTask DisposeAsync()
        {
            await _server!.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        internal async Task InitializeAsync()
        {
            await _server?.StartAsync()!;
            Console.WriteLine($"Created pipe: {_server.PipeName}");
        }

        private void OnClientConnected(object o, ConnectionEventArgs<PipeMessage> args) => _ = OnClientConnectedAsync(args);

        private async Task OnClientConnectedAsync(ConnectionEventArgs<PipeMessage> args)
        {
            Program.Log.LogInformation(EventLogCatalog.PipeClientConnected, args.Connection.PipeName);
            Console.WriteLine($"Client {args.Connection.PipeName} is now connected!");
            DebugTrace($"ClientConnected pipe={args.Connection.PipeName}");
            Console.WriteLine("Checking child process owner.");
            var isAdmin = false;
            try
            {
                isAdmin = PrivilegeHelper.IsSessionOwnerAdmin;
            }
            catch (Exception ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionAdminQueryFailed, ex.GetMessageStack());
            }
            Console.WriteLine($"Child process owner has admin rights: {isAdmin}");

            try
            {
                var bootstrapMessage = new PipeMessage
                {
                    Action = ActionType.IsAdmin,
                    Text = $"{isAdmin}"
                };
                bootstrapMessage.Checksum = PipeMessageChecksum.Compute(bootstrapMessage);
                await args.Connection.WriteAsync(bootstrapMessage).ConfigureAwait(false);
                DebugTrace($"BootstrapSent action={bootstrapMessage.Action} checksum_len={bootstrapMessage.Checksum?.Length ?? 0} checksum={ByteArrayToString(bootstrapMessage.Checksum!)}");
            }
            catch (IOException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeBootstrapDisconnected, ex.GetMessageStack());
                DebugTrace($"BootstrapSendFailed error={ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeBootstrapClientNotConnected, ex.GetMessageStack());
                DebugTrace($"BootstrapSendSkipped reason=ClientNotConnected error={ex.Message}");
            }
            catch (Exception ex)
            {
                Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, ex.GetMessageStack());
            }
        }

        private void OnClientDisconnected(object o, ConnectionEventArgs<PipeMessage> args)
        {
            Program.Log.LogInformation(EventLogCatalog.PipeClientDisconnected, args.Connection.PipeName);
            Program.Log.LogInformation(EventLogCatalog.PipeAutoRestartDisabled);
            DebugTrace($"ClientDisconnected pipe={args.Connection.PipeName}");
        }

        private void OnExceptionOccurred(object o, ExceptionEventArgs args)
        {
            Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, args.Exception.GetMessageStack());
            Console.WriteLine($"Exception occurred in pipe: {args.Exception.GetMessageStack()}");
        }

        private void OnMessageReceived(object sender, ConnectionMessageEventArgs<PipeMessage> args)
        {
            if (args.Message == null)
            {
                return;
            }

            if (!PipeMessageValidator.IsValidInboundClientMessage(args.Message))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeInboundRejected);
                DebugTrace($"InboundRejected action={args.Message.Action} text_len={args.Message.Text?.Length ?? 0} checksum_len={args.Message.Checksum.Length} checksum={ByteArrayToString(args.Message.Checksum)}");
                return;
            }

            switch (args.Message)
            {
                case { Action: ActionType.SendLog }:
                    Program.Log.LogError(EventLogCatalog.PipeClientForwardedLog, args.Connection.PipeName, Environment.NewLine, args.Message.Text!);
                    DebugTrace($"InboundAccepted action={args.Message.Action} text_len={args.Message.Text?.Length ?? 0}");
                    break;

                default:
                    Program.Log.LogWarning(EventLogCatalog.PipeUnknownActionType, args.Message.Action);
                    break;
            }
        }

        [Conditional("DEBUG")]
        private static void DebugTrace(string message) => Debug.WriteLine($"[PipeServer] {message}");

        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            return hex.ToString();
        }
    }
}
