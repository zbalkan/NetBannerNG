using System;
using Microsoft.Win32;

namespace NetBannerNG
{
    public class RegistrySetting : IDisposable
    {
        public int Classification => (int)netBannerKey.GetValue("Classification");
        public int CaveatsEnabled => netBannerKey.GetValue("CaveatsEnabled") != null ? (int)netBannerKey.GetValue("CaveatsEnabled") : 0;
        public string Caveats => CaveatsEnabled != 1 ? string.Empty : netBannerKey.GetValue("Caveats").ToString();
        public int? FpCon => netBannerKey.GetValue("FpCon") == null ? null : (int?)netBannerKey.GetValue("FpCon");
        public int? InfoCon => netBannerKey.GetValue("InfoCon") == null ? null : (int?)netBannerKey.GetValue("InfoCon");
        public int CustomSettings => netBannerKey.GetValue("CustomSettings") == null ? 0 : (int)netBannerKey.GetValue("CustomSettings");
        public int CustomBackgroundColor => CustomSettings != 1 ? 0 : (int)netBannerKey.GetValue("CustomBackgroundColor");
        public int CustomForeColor => CustomSettings != 1 ? 0 : (int)netBannerKey.GetValue("CustomForeColor");
        public string CustomDisplayText => (string)netBannerKey.GetValue("CustomDisplayText");

        private bool disposedValue;
        private readonly RegistryKey netBannerKey;

        public RegistrySetting()
        {
            netBannerKey = Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\NetBanner");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    netBannerKey.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~RegistrySetting()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}