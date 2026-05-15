using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using NetBannerNG.Common;

namespace NetBannerNG.Utils
{
    public static class UserHelper
    {
        private static WindowsIdentity? _currentUser;
        internal static WindowsIdentity CurrentUser => _currentUser ??= ProcessHelper.Owner(Process.GetCurrentProcess());

        internal static string UserTempPath => Path.GetTempPath();
    }
}