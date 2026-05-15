using System;
using System.Collections.Generic;
using System.Linq;
using NetBannerNG.Common;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    public static class FullscreenSuppressionEvaluator
    {
        internal struct WindowSnapshot : IEquatable<WindowSnapshot>
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

            public readonly override bool Equals(object? obj) => obj is WindowSnapshot snapshot && Equals(snapshot);
            public readonly bool Equals(WindowSnapshot other) => EqualityComparer<IntPtr>.Default.Equals(Handle, other.Handle) && Bounds.Equals(other.Bounds) && MonitorBounds.Equals(other.MonitorBounds) && IsVisible == other.IsVisible;

            public readonly override int GetHashCode() => HashCode.Combine(Handle, Bounds, MonitorBounds, IsVisible);

            public static bool operator ==(WindowSnapshot left, WindowSnapshot right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(WindowSnapshot left, WindowSnapshot right)
            {
                return !(left == right);
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

        internal static Dictionary<string, bool> EvaluateByGroup(
            IEnumerable<string> monitorGroupIds,
            Dictionary<string, string> boundsGroupToActualGroup,
            HashSet<IntPtr> ownWindowHandles,
            IEnumerable<WindowSnapshot> windows)
        {
            if (monitorGroupIds is null)
            {
                throw new ArgumentNullException(nameof(monitorGroupIds));
            }

            if (boundsGroupToActualGroup is null)
            {
                throw new ArgumentNullException(nameof(boundsGroupToActualGroup));
            }

            if (ownWindowHandles is null)
            {
                throw new ArgumentNullException(nameof(ownWindowHandles));
            }

            if (windows is null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

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