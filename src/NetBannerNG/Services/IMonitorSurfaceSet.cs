using System;
using System.Collections.Generic;
using NetBannerNG.Borders;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Services
{
    internal interface IMonitorSurfaceSet
    {
        Monitor Monitor { get; }
        string GroupId { get; }

        IEnumerable<BorderBase> CreateLaunchEntries();

        bool MatchesMonitor(Monitor monitor);

        bool HasMonitorLayoutChanged(Monitor monitor);

        void SyncMonitor(Monitor monitor);

        void ApplyPostDockVisualState();

        void SetSuppressed(bool isSuppressed);

        void Close();

        bool TryShowWindow(BorderBase window, out Exception? error);
    }
}