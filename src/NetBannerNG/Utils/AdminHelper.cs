namespace NetBannerNG.Utils
{
    internal static class AdminHelper
    {
        /// <summary>
        ///     Until the Windows service clearly sends on named pipe that the active user is an admin, return false.
        /// </summary>
        internal static bool IsAdmin { get; set; }
    }
}