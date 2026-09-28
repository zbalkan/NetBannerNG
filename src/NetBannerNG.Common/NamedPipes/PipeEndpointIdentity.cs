using System.IO.Pipes;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common.NamedPipes
{
    /// <summary>
    ///     Resolves the process at the other end of a connected named pipe from the kernel,
    ///     without relying on client impersonation or on metadata exposed by the pipe library.
    /// </summary>
    public static class PipeEndpointIdentity
    {
        [CLSCompliant(false)]
        public static bool TryGetServerProcessId(PipeStream pipe, out uint processId)
        {
            processId = 0;
            return pipe is { IsConnected: true } && Kernel32.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out processId) && processId != 0;
        }

        [CLSCompliant(false)]
        public static bool TryGetClientProcessId(PipeStream pipe, out uint processId)
        {
            processId = 0;
            return pipe is { IsConnected: true } && Kernel32.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out processId) && processId != 0;
        }

        [CLSCompliant(false)]
        public static bool TryGetClientSessionId(PipeStream pipe, out uint sessionId)
        {
            sessionId = 0;
            return pipe is { IsConnected: true } && Kernel32.GetNamedPipeClientSessionId(pipe.SafePipeHandle, out sessionId);
        }

        /// <summary>
        ///     True when the server end of <paramref name="pipe"/> is the installed watchdog service.
        ///     Pipe names are predictable, so any local user can create a pipe with the expected name
        ///     before the service does; the client must not trust the endpoint by name alone. The
        ///     server must run in session 0 (only services and SYSTEM can create processes there) and
        ///     from the installed watchdog image. Both facts come from the kernel, not from the peer.
        /// </summary>
        public static bool IsServerInstalledWatchdog(PipeStream pipe)
        {
            if (pipe == null)
            {
                throw new ArgumentNullException(nameof(pipe));
            }

            if (!pipe.IsConnected
                || !Kernel32.GetNamedPipeServerSessionId(pipe.SafePipeHandle, out var serverSessionId)
                || serverSessionId != 0)
            {
                return false;
            }

            return TryGetServerProcessId(pipe, out var serverProcessId) && ProcessHelper.IsInstalledWatchdogImage(serverProcessId);
        }
    }
}
