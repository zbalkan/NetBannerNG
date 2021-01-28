using Microsoft.Win32;
using System.Collections.Generic;
using System.Drawing;

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

        public static bool IsCaveatsEnabled()
        {
            return NetBannerKey.GetValue("CaveatsEnabled") != null;
        }

        public static ClassificationMark GetClassification()
        {
            var classifications = new List<ClassificationMark>
            {
                new ClassificationMark(){ ClassificationName ="UNCLASSIFIED", BackgroundColor = Color.Green, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="SECRET", BackgroundColor = Color.Blue, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="TOP SECRET", BackgroundColor = Color.Red, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="SCI", BackgroundColor = Color.Red, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO UNCLASSIFIED", BackgroundColor = Color.Green, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO RESTRICTED", BackgroundColor = Color.Blue, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO CONFIDENTIAL", BackgroundColor = Color.Blue, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO SECRET", BackgroundColor = Color.Red, ForeColor = Color.White },
                new ClassificationMark(){ ClassificationName ="NATO TOP SECRET", BackgroundColor = Color.Red, ForeColor = Color.White }
            };

            var index = int.Parse(NetBannerKey.GetValue("Classification").ToString()) - 1;
            return classifications[index];
        }

        public static string GetCaveat()
        {
            return NetBannerKey.GetValue("Caveats").ToString();
        }

        public static string GetFpCon()
        {
            var result = int.Parse(NetBannerKey.GetValue("FpCon").ToString());
            string fpCon;
            switch (result)
            {
                default:
                case 1:
                    fpCon = "ALPHA";
                    break;
                case 2:
                    fpCon = "BETA";
                    break;
                case 3:
                    fpCon = "CHARLIE";
                    break;
                case 4:
                    fpCon = "DELTA";
                    break;
            }
            return fpCon;
        }

        public static int GetInfoCon()
        {
            return int.Parse(NetBannerKey.GetValue("InfoCon").ToString());
        }

        public static bool IsCustomSettingEnabled()
        {
            return int.Parse(NetBannerKey.GetValue("CustomSettings").ToString()) == 1;
        }

        public static CustomSettings GetCustomSettings()
        {
            return new CustomSettings
            {
                CustomBackgroundColor = (CustomBackgroundColor)int.Parse(NetBannerKey.GetValue("CustomBackgroundColor").ToString()),
                CustomForeColor = (CustomForeColor)int.Parse(NetBannerKey.GetValue("CustomForeColor").ToString()),
                CustomDisplayText = NetBannerKey.GetValue("CustomDisplayText").ToString()
            };
        }
    }
}
