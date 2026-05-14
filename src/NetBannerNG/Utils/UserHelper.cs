using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
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