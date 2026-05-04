using NetBannerNG.Common.Native;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static NetBannerNG.Common.Native.NativeMethods;

namespace NetBannerNG.Common.AppBar
{
    /// <summary>
    /// <see href="https://github.com/PhilipRieck/WpfAppBar"/>
    /// </summary>
    public static class AppBarFunctions
    {
        private static readonly Dictionary<Window, RegisterInfo> RegisteredWindowInfo = new();

        private delegate void ResizeDelegate(Window appbarWindow, Rect rect);

        public static void SetAppBar(Window appbarWindow, DockEdge edge, FrameworkElement? childElement = null, bool topMost = true)
        {
            if (appbarWindow is null)
            {
                throw new ArgumentNullException(nameof(appbarWindow));
            }

            Debug.WriteLine($"Started docking window {appbarWindow} to {Enum.GetName(typeof(DockEdge), edge)} (topMost = {topMost}).");
            if (childElement != null)
            {
                Debug.Indent();
                Debug.WriteLine($"Size will be arranged for child element '{childElement.Name}' of type {childElement.GetType()}.");
                Debug.Unindent();
            }

            var info = appbarWindow.GetRegisterInfo().DockWithChild(edge, childElement);
            var appBarData = new APPBARDATA().WithWindow(appbarWindow);

            if (edge == DockEdge.None)
            {
                // When docked, hiding on Peek and Show Desktop actions was blocked.
                // Restore normal desktop window manager attributes.
                appBarData.Unregister(info).NormalizeHideBehavior();
                RestoreWindow(appbarWindow);
                return;
            }

            // Skip redundant docking when this window is already registered on the same edge
            // with an existing docked rectangle. This avoids duplicate shell docking work during
            // startup window sequencing.
            if (info.IsRegistered && info.Edge == edge && info.DockedSize.HasValue)
            {
                return;
            }

            // Set desktop window manager attributes to prevent window
            // from being hidden when peeking at the desktop or when
            // the 'show desktop' button is pressed
            appBarData.Register(info).PreventHideOnPeek();

            appbarWindow.StyleForDocking(topMost);

            AbSetPos(info, appbarWindow, childElement);
        }

        // Why twice?
        private static void AbSetPos(RegisterInfo info, Window appbarWindow, FrameworkElement? childElement)
        {
            //if (info.IsHandled) return;

            var barData = new APPBARDATA().FromWindow(appbarWindow, info.Edge);

            Debug.WriteLine($"Attempting to get work area of window {appbarWindow}.");

            var dockedSize = barData
                // Transform window size from wpf units (1/96 ") to real pixels, for win32 usage
                // Even if the documentation says SystemParameters.PrimaryScreen{Width, Height} return values in "pixels", they return wpf units instead.
                .CalculateDockedSize(CalculateActualSize(appbarWindow, childElement), WorkAreaAsPixel(appbarWindow, GetActualWorkArea(info)))
                .SendNewPositionToShell()
                // transform back to wpf units, for wpf window resizing in DoResize.
                .AsWpfUnits(appbarWindow);

            info.DockedSize = dockedSize;

            //This is done async, because WPF will send a resize after a new appbar is added.
            //if we size right away, WPFs resize comes last and overrides us.
            _ = appbarWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new ResizeDelegate(DoResize), appbarWindow, dockedSize);

            Debug.WriteLine($"Resized window: {appbarWindow}");
            Debug.WriteLine($"{appbarWindow} new size: {dockedSize}");
        }

        private static Vector CalculateActualSize(FrameworkElement appbarWindow, FrameworkElement childElement) => childElement != null ?
                WPFUnitHelper.Transform(appbarWindow, WPFUnitHelper.TransformTarget.ToPixel,
                    new Vector(childElement.ActualWidth, childElement.ActualHeight))
                : WPFUnitHelper.Transform(appbarWindow, WPFUnitHelper.TransformTarget.ToPixel,
                    new Vector(appbarWindow.ActualWidth, appbarWindow.ActualHeight));

        private static void DoResize(Window appbarWindow, Rect rect)
        {
            appbarWindow.Width = rect.Width;
            appbarWindow.Height = rect.Height;
            appbarWindow.Top = rect.Top;
            appbarWindow.Left = rect.Left;
        }

        private static Rect GetActualWorkArea(RegisterInfo info)
        {
            var hWnd = info.Window.GetHandle();
            var cwa = Native.Monitor.GetMonitorWorkArea(hWnd);

            var wa = new Rect(new Point(cwa.Left, cwa.Top), new Point(cwa.Right, cwa.Bottom));

            if (info.DockedSize != null)
            {
                wa.Union(info.DockedSize.Value);
            }

            Debug.WriteLine($"Captured actual work area: {wa}");
            return wa;
        }

        private static void RestoreWindow(Window appbarWindow)
        {
            var info = appbarWindow.GetRegisterInfo();
            appbarWindow.RestoreStyle(info);
            info.DockedSize = null;
            var rect = new Rect(info.OriginalPosition.X, info.OriginalPosition.Y, info.OriginalSize.Width, info.OriginalSize.Height);
            _ = appbarWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new ResizeDelegate(DoResize), appbarWindow, rect);
        }

        private static APPBARDATA SendAppBarRemovalToShell(APPBARDATA abd)
        {
            _ = SHAppBarMessage((int)AbMsg.AbmRemove, ref abd);
            return abd;
        }

        private static APPBARDATA SendNewAppBarToShell(APPBARDATA abd)
        {
            _ = SHAppBarMessage((int)AbMsg.AbmNew, ref abd);
            return abd;
        }

        private static Rect WorkAreaAsPixel(FrameworkElement appBarWindow, Rect actualWorkArea)
        {
            var screenSizeInPixels = WPFUnitHelper.Transform(appBarWindow, WPFUnitHelper.TransformTarget.ToPixel, new Vector(actualWorkArea.Width, actualWorkArea.Height));
            var workTopLeftInPixels = WPFUnitHelper.Transform(appBarWindow, WPFUnitHelper.TransformTarget.ToPixel, new Point(actualWorkArea.Left, actualWorkArea.Top));
            return new Rect(workTopLeftInPixels, screenSizeInPixels);
        }

        internal class RegisterInfo
        {
            internal int CallbackId { get; set; }
            internal bool IsRegistered { get; set; }
            internal Window Window { get; set; }
            internal DockEdge Edge { get; set; }
            internal WindowStyle OriginalStyle { get; set; }
            internal Point OriginalPosition { get; set; }
            internal Size OriginalSize { get; set; }
            internal ResizeMode OriginalResizeMode { get; set; }
            internal bool OriginalTopmost { get; set; }
            internal FrameworkElement? ChildElement { get; set; }
            internal Rect? DockedSize { get; set; }
            internal bool IsHandled { get; set; }

            internal IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (msg != CallbackId || wParam.ToInt32() != (int)AbNotify.AbnPoschanged || IsHandled)
                {
                    return IntPtr.Zero;
                }

                AbSetPos(this, Window, ChildElement);
                handled = true; // This is set but never used
                IsHandled = true; // Added to reduce unnecessary duplicate callbacks
                Debug.WriteLine($"WndProc hook: {CallbackId} | Window: {Window} | Edge: {Enum.GetName(typeof(DockEdge), Edge)} | DockedSize: {DockedSize ?? new()}");
                return IntPtr.Zero;
            }
        }

        #region ExtensionMethods

        #region APPBARDATA Extensions

        private static Rect AsWpfUnits(this APPBARDATA barData, FrameworkElement appBarWindowForReference)
        {
            var location = WPFUnitHelper.Transform(appBarWindowForReference, WPFUnitHelper.TransformTarget.ToWpfUnit, new Point(barData.rc.Left, barData.rc.Top));
            var dimension = WPFUnitHelper.Transform(appBarWindowForReference, WPFUnitHelper.TransformTarget.ToWpfUnit, new Vector(barData.rc.Right - barData.rc.Left, barData.rc.Bottom - barData.rc.Top));
            return new Rect(location, new Size(dimension.X, dimension.Y));
        }

        private static APPBARDATA CalculateDockedSize(this APPBARDATA barData, Vector sizeInPixels, Rect workAreaInPixelsF)
        {
            switch (barData.uEdge)
            {
                case (int)DockEdge.Left:
                    barData.rc.Top = (int)workAreaInPixelsF.Top;
                    barData.rc.Bottom = (int)workAreaInPixelsF.Bottom;
                    barData.rc.Left = (int)workAreaInPixelsF.Left;
                    //Left might not always be zero so we need to accommodate for that.
                    //For example, if the Start Menu is docked LEFT, if we don't do the math, we'll end up with a negative size error
                    barData.rc.Right = barData.rc.Left + (int)Math.Round(sizeInPixels.X);
                    break;

                case (int)DockEdge.Right:
                    barData.rc.Top = (int)workAreaInPixelsF.Top;
                    barData.rc.Bottom = (int)workAreaInPixelsF.Bottom;
                    barData.rc.Right = (int)workAreaInPixelsF.Right;
                    barData.rc.Left = barData.rc.Right - (int)Math.Round(sizeInPixels.X);
                    break;

                case (int)DockEdge.Top:
                    barData.rc.Left = (int)workAreaInPixelsF.Left;
                    barData.rc.Right = (int)workAreaInPixelsF.Right;
                    barData.rc.Top = (int)workAreaInPixelsF.Top;
                    //Top might not always be zero so we need to accommodate for that.
                    //For example, if the Start Menu is docked TOP, if we don't do the math, we'll end up with a negative size error
                    barData.rc.Bottom = barData.rc.Top + (int)Math.Round(sizeInPixels.Y);
                    break;

                case (int)DockEdge.Bottom:
                    barData.rc.Left = (int)workAreaInPixelsF.Left;
                    barData.rc.Right = (int)workAreaInPixelsF.Right;
                    barData.rc.Bottom = (int)workAreaInPixelsF.Bottom;
                    barData.rc.Top = barData.rc.Bottom - (int)Math.Round(sizeInPixels.Y);
                    break;

                default:
                    // No other cases
                    break;
            }

            return barData;
        }

        private static APPBARDATA FromWindow(this APPBARDATA barData, Window appbarWindow, DockEdge edge)
        {
            barData.cbSize = Marshal.SizeOf(barData);
            barData.hWnd = appbarWindow.GetHandle();
            barData.uEdge = (int)edge;
            return barData;
        }

        private static void NormalizeHideBehavior(this APPBARDATA appBarData)
        {
            var renderPolicy = (int)DwmncRenderingPolicy.UseWindowStyle;
            _ = DwmSetWindowAttribute(appBarData.hWnd, (int)DWMWINDOWATTRIBUTE.DwmaExcludedFromPeek, ref renderPolicy, sizeof(int));
            _ = DwmSetWindowAttribute(appBarData.hWnd, (int)DWMWINDOWATTRIBUTE.DwmaDisallowPeek, ref renderPolicy, sizeof(int));
        }

        private static void PreventHideOnPeek(this APPBARDATA appBarData)
        {
            var renderPolicy = (int)DwmncRenderingPolicy.Enabled;
            _ = DwmSetWindowAttribute(appBarData.hWnd, (int)DWMWINDOWATTRIBUTE.DwmaExcludedFromPeek, ref renderPolicy, sizeof(int));
            _ = DwmSetWindowAttribute(appBarData.hWnd, (int)DWMWINDOWATTRIBUTE.DwmaDisallowPeek, ref renderPolicy, sizeof(int));
        }

        private static APPBARDATA Register(this APPBARDATA abd, RegisterInfo info)
        {
            if (info.IsRegistered)
            {
                return abd;
            }

            info.IsRegistered = true;
            info.CallbackId = RegisterWindowMessage($"AppBarMessage-{abd.hWnd.ToInt32()}");
            abd.uCallbackMessage = info.CallbackId;
            abd = SendNewAppBarToShell(abd);

            var source = HwndSource.FromHwnd(abd.hWnd);
            source?.AddHook(info.WndProc);

            return abd;
        }

        private static APPBARDATA SendNewPositionToShell(this APPBARDATA barData)
        {
            _ = SHAppBarMessage((int)AbMsg.AbmQuerypos, ref barData);
            _ = SHAppBarMessage((int)AbMsg.AbmSetpos, ref barData);
            return barData;
        }

        private static APPBARDATA Unregister(this APPBARDATA abd, RegisterInfo info)
        {
            if (!info.IsRegistered)
            {
                return abd;
            }

            abd = SendAppBarRemovalToShell(abd);
            info.IsRegistered = false;

            var source = HwndSource.FromHwnd(abd.hWnd);
            source?.RemoveHook(info.WndProc);

            return abd;
        }

        private static APPBARDATA WithWindow(this APPBARDATA appBarData, Window appbarWindow)
        {
            appBarData.cbSize = Marshal.SizeOf(appBarData);
            appBarData.hWnd = appbarWindow.GetHandle();
            return appBarData;
        }

        #endregion APPBARDATA Extensions

        #region Window Extensions

        public static IntPtr GetHandle(this Window window) => new WindowInteropHelper(window).Handle;

        private static RegisterInfo GetRegisterInfo(this Window appbarWindow)
        {
            RegisterInfo reg;
            if (RegisteredWindowInfo.TryGetValue(appbarWindow, out var value))
            {
                reg = value;
            }
            else
            {
                reg = new RegisterInfo
                {
                    CallbackId = 0,
                    Window = appbarWindow,
                    IsRegistered = false,
                    Edge = DockEdge.Top,
                    OriginalStyle = appbarWindow.WindowStyle,
                    OriginalPosition = new Point(appbarWindow.Left, appbarWindow.Top),
                    OriginalSize = new Size(appbarWindow.ActualWidth, appbarWindow.ActualHeight),
                    OriginalResizeMode = appbarWindow.ResizeMode,
                    OriginalTopmost = appbarWindow.Topmost,
                    DockedSize = null
                };
                RegisteredWindowInfo.Add(appbarWindow, reg);
            }
            return reg;
        }

        private static void RestoreStyle(this Window appbarWindow, RegisterInfo info)
        {
            appbarWindow.WindowStyle = info.OriginalStyle;
            appbarWindow.ResizeMode = info.OriginalResizeMode;
            appbarWindow.Topmost = info.OriginalTopmost;
        }

        private static void StyleForDocking(this Window appbarWindow, bool topMost)
        {
            appbarWindow.WindowStyle = WindowStyle.None;
            appbarWindow.ResizeMode = ResizeMode.NoResize;
            appbarWindow.Topmost = topMost;
        }

        #endregion Window Extensions

        private static RegisterInfo DockWithChild(this RegisterInfo info, DockEdge edge, FrameworkElement? childElement)
        {
            info.Edge = edge;
            info.ChildElement = childElement;
            return info;
        }

        #endregion ExtensionMethods
    }
}