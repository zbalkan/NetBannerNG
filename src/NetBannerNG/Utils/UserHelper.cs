using Microsoft.Win32;
using NetBannerNG.Common.Native;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace NetBannerNG.Utils
{
    public static class UserHelper
    {
        private static WindowsIdentity _currentUser;
        internal static WindowsIdentity CurrentUser => _currentUser ??= ProcessHelper.Owner(Process.GetCurrentProcess());

        internal static string UserProfilePath
        {
            get
            {
                var userSID = CurrentUser.User.ToString();
                try
                {
                    var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + userSID;

                    var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key == null)
                    {
                        //handle error
                        return null;
                    }

                    var path = key.GetValue("ProfileImagePath") as string;
                    return Directory.Exists(path) ? path : null;
                }
                catch
                {
                    //handle exception
                    return null;
                }
            }
        }

        internal static string UserTempPath => @$"{UserProfilePath}\AppData\Local\Temp\";
    }
}