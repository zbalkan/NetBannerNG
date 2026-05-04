namespace NetBannerNG.LittleForker
{
    public static class ProcessSupervisorExtensions
    {
        public static Task WhenStateIs(
            this ProcessSupervisor processSupervisor,
            ProcessSupervisor.State processState,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(processSupervisor);

            var taskCompletionSource = new TaskCompletionSource<int>();
            cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());

            void Handler(ProcessSupervisor.State state)
            {
                if (processState == state)
                {
                    taskCompletionSource.SetResult(0);
                    processSupervisor.StateChanged -= Handler;
                }
            }

            processSupervisor.StateChanged += Handler;

            return taskCompletionSource.Task;
        }

        public static Task WhenOutputStartsWith(
            this ProcessSupervisor processSupervisor,
            string startsWith,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(processSupervisor);
            if (string.IsNullOrEmpty(startsWith))
                throw new ArgumentException($"'{nameof(startsWith)}' cannot be null or empty.", nameof(startsWith));

            var taskCompletionSource = new TaskCompletionSource<int>();
            cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());

            void Handler(string data)
            {
                if (data?.StartsWith(startsWith, StringComparison.InvariantCulture) == true)
                {
                    taskCompletionSource.SetResult(0);
                    processSupervisor.OutputDataReceived -= Handler;
                }
            }

            processSupervisor.OutputDataReceived += Handler;
            return taskCompletionSource.Task;
        }
    }
}