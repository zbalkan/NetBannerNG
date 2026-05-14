using System;
using System.Collections.Generic;
using System.Linq;
using NetBannerNG.Common;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    public static class FullscreenSuppressionEvaluator
    {
        public struct WindowSnapshot
        {
            public IntPtr Handle { get; set; }
            public MonitorRect Bounds { get; set; }
            public MonitorRect MonitorBounds { get; set; }
            public bool IsVisible { get; set; }

            public WindowSnapshot(IntPtr handle, MonitorRect bounds, MonitorRect monitorBounds, bool isVisible)
            {
                Handle = handle;
                Bounds = bounds;
                MonitorBounds = monitorBounds;
                IsVisible = isVisible;
            }
        }

        internal static Dictionary<string, bool> EvaluateByGroup(
            IReadOnlyList<Monitor> monitors,
            HashSet<IntPtr> ownWindowHandles,
            IEnumerable<WindowSnapshot> windows)
        {
            var boundsGroupToActualGroup = monitors.ToDictionary(
                monitor => MonitorIdentity.BuildGroupId(string.Empty, monitor.Bounds),
                MonitorIdentity.BuildGroupId,
                StringComparer.Ordinal);
            var groups = monitors.Select(MonitorIdentity.BuildGroupId).ToHashSet(StringComparer.Ordinal);
            return EvaluateByGroup(groups, boundsGroupToActualGroup, ownWindowHandles, windows);
        }

        public static Dictionary<string, bool> EvaluateByGroup(
            IEnumerable<string> monitorGroupIds,
            Dictionary<string, string> boundsGroupToActualGroup,
            HashSet<IntPtr> ownWindowHandles,
            IEnumerable<WindowSnapshot> windows)
        {
            var results = monitorGroupIds.ToDictionary(group => group, _ => false, StringComparer.Ordinal);

            var resolvedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var window in windows)
            {
                if (!window.IsVisible || ownWindowHandles.Contains(window.Handle))
                {
                    continue;
                }

                var boundsGroupId = MonitorIdentity.BuildGroupId(string.Empty, (System.Windows.Rect)window.MonitorBounds);
                if (!boundsGroupToActualGroup.TryGetValue(boundsGroupId, out var groupId) || resolvedGroups.Contains(groupId))
                {
                    continue;
                }

                if (IsFullscreen(window.Bounds, window.MonitorBounds))
                {
                    results[groupId] = true;
                    resolvedGroups.Add(groupId);
                }
            }

            return results;
        }

        public static bool IsFullscreen(MonitorRect windowBounds, MonitorRect monitorBounds, double tolerance = 2.0) =>
            Math.Abs(windowBounds.Left - monitorBounds.Left) <= tolerance
            && Math.Abs(windowBounds.Top - monitorBounds.Top) <= tolerance
            && Math.Abs(windowBounds.Right - monitorBounds.Right) <= tolerance
            && Math.Abs(windowBounds.Bottom - monitorBounds.Bottom) <= tolerance;
    }
}
