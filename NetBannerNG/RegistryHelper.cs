using Microsoft.Win32;

namespace NetBannerNG
{
    public static class RegistryHelper
    {
        private static RegistryKey NetBannerKey;

        public static bool ConnectRegistry()
        {
            NetBannerKey = Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\NetBanner");
            return NetBannerKey != null;
        }

        public static void DisconnectRegistry()
        {
            NetBannerKey.Close();
        }

        public static int GetCaveatsEnabled()
        {
            return NetBannerKey.GetValue("CaveatsEnabled") != null ? (int)NetBannerKey.GetValue("CaveatsEnabled") : 0;
        }

        public static int GetClassification()
        {
            return (int)NetBannerKey.GetValue("Classification");
        }

        public static string GetCaveat()
        {
            return NetBannerKey.GetValue("Caveats").ToString();
        }

        public static int? GetFpCon()
        {
            return NetBannerKey.GetValue("FpCon") == null ? null : (int?)(int)NetBannerKey.GetValue("FpCon");
        }

        public static int? GetInfoCon()
        {
            return NetBannerKey.GetValue("InfoCon") == null ? null : (int?)(int)NetBannerKey.GetValue("InfoCon");
        }

        public static int GetCustomSettingsKey()
        {
            return NetBannerKey.GetValue("CustomSettings") == null ? 0 : (int)NetBannerKey.GetValue("CustomSettings");
        }

        public static int GetCustomForeColor()
        {
            return (int)NetBannerKey.GetValue("CustomForeColor");
        }

        public static int GetCustomBackgroundColor()
        {
            return (int)NetBannerKey.GetValue("CustomBackgroundColor");
        }

        public static string GetCustomDisplayText()
        {
            return (string)NetBannerKey.GetValue("CustomDisplayText");
        }
    }
}
