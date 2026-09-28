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
            public uint ProcessId { get; set; }

            public WindowSnapshot(IntPtr handle, MonitorRect bounds, MonitorRect monitorBounds, bool isVisible, uint processId = 0)
            {
                Handle = handle;
                Bounds = bounds;
                MonitorBounds = monitorBounds;
                IsVisible = isVisible;
                ProcessId = processId;
            }

            public readonly override bool Equals(object? obj) => obj is WindowSnapshot snapshot && Equals(snapshot);
            public readonly bool Equals(WindowSnapshot other) => EqualityComparer<IntPtr>.Default.Equals(Handle, other.Handle) && Bounds.Equals(other.Bounds) && MonitorBounds.Equals(other.MonitorBounds) && IsVisible == other.IsVisible && ProcessId == other.ProcessId;

            public readonly override int GetHashCode() => HashCode.Combine(Handle, Bounds, MonitorBounds, IsVisible, ProcessId);

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

            var suppressingWindows = FindSuppressingWindowsByGroup(boundsGroupToActualGroup, ownWindowHandles, windows);
            return monitorGroupIds.ToDictionary(group => group, suppressingWindows.ContainsKey, StringComparer.Ordinal);
        }

        // Returns, per monitor group, the fullscreen window that suppresses that group's bars.
        // Windows are expected in z-order (topmost first). A fullscreen window suppresses its
        // monitor only when every eligible window above it on the same monitor belongs to the
        // same process: companion windows of a fullscreen app (player controls, launcher popups)
        // keep suppression, while a fullscreen window left behind another application's window
        // does not. The banner is a marking control, so ambiguity resolves to showing it.
        internal static Dictionary<string, WindowSnapshot> FindSuppressingWindowsByGroup(
            Dictionary<string, string> boundsGroupToActualGroup,
            HashSet<IntPtr> ownWindowHandles,
            IEnumerable<WindowSnapshot> windows)
        {
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

            var results = new Dictionary<string, WindowSnapshot>(StringComparer.Ordinal);
            var resolvedGroups = new HashSet<string>(StringComparer.Ordinal);
            var processesAboveByGroup = new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);
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

                if (!processesAboveByGroup.TryGetValue(groupId, out var processesAbove))
                {
                    processesAbove = new HashSet<uint>();
                    processesAboveByGroup[groupId] = processesAbove;
                }

                if (IsFullscreen(window.Bounds, window.MonitorBounds))
                {
                    if (processesAbove.All(processId => processId == window.ProcessId))
                    {
                        results[groupId] = window;
                    }

                    resolvedGroups.Add(groupId);
                    continue;
                }

                processesAbove.Add(window.ProcessId);
            }

            return results;
        }

        // A window is fullscreen when its rectangle is equal to, or fully covers, the monitor rectangle
        // it runs on. The small tolerance absorbs sub-pixel rounding from DPI scaling and the invisible
        // resize border that GetWindowRect can include for maximized borderless apps.
        public static bool IsFullscreen(MonitorRect windowBounds, MonitorRect monitorBounds, double tolerance = 1.0) =>
            windowBounds.Left <= monitorBounds.Left + tolerance
            && windowBounds.Top <= monitorBounds.Top + tolerance
            && windowBounds.Right >= monitorBounds.Right - tolerance
            && windowBounds.Bottom >= monitorBounds.Bottom - tolerance;
    }
}