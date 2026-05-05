using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Utils
{
    public static class UserHelper
    {
        private static WindowsIdentity? _currentUser;
        internal static WindowsIdentity CurrentUser => _currentUser ??= ProcessHelper.Owner(Process.GetCurrentProcess());

        internal static string UserProfilePath
        {
            get
            {
                var userSid = CurrentUser.User?.ToString();
                if (!string.IsNullOrWhiteSpace(userSid))
                {
                    try
                    {
                        var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + userSid;
                        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                        var profilePath = key?.GetValue("ProfileImagePath") as string;
                        if (!string.IsNullOrWhiteSpace(profilePath) && Directory.Exists(profilePath))
                        {
                            return profilePath!;
                        }
                    }
                    catch
                    {
                    }
                }

                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localAppData) && Directory.Exists(localAppData))
                {
                    return Directory.GetParent(localAppData)?.FullName ?? localAppData;
                }

                return Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        internal static string UserTempPath => Path.GetTempPath();
    }
}