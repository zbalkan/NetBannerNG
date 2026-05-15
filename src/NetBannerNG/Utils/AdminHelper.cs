namespace NetBannerNG.Utils
{
    internal static class AdminHelper
    {
        private static volatile bool _isAdmin;

        /// <summary>
        ///     Set by the service over the named pipe after identity verification; defaults false until confirmed.
        /// </summary>
        internal static bool IsAdmin
        {
            get => _isAdmin;
            set => _isAdmin = value;
        }
    }
}
