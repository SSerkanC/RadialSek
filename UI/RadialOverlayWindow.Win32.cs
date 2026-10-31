using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExLayered = 0x00080000;
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int HtClient = 1;

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);

        private const int VkEscape = 0x1B;
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkMButton = 0x04;
        private const int VkXButton1 = 0x05;
        private const int VkXButton2 = 0x06;
        private const int VkRButton = 0x02;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private IntPtr AttachHwndSourceIfNeeded()
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            if (_hwndSource == null && hwnd != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(hwnd) ?? (HwndSource?)PresentationSource.FromVisual(this);
                _hwndSource?.AddHook(WndProc);
            }

            return hwnd;
        }

        private (double ScaleX, double ScaleY) GetWindowDpiScale()
        {
            try
            {
                var source = _hwndSource ?? PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformToDevice;
                    if (matrix.M11 > 0.01 && matrix.M22 > 0.01)
                    {
                        return (matrix.M11, matrix.M22);
                    }
                }
            }
            catch
            {
            }

            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                if (dpi.DpiScaleX > 0.01 && dpi.DpiScaleY > 0.01)
                {
                    return (dpi.DpiScaleX, dpi.DpiScaleY);
                }
            }
            catch
            {
            }

            return (1.0, 1.0);
        }

        private void PositionWindowAcrossVirtualScreen(
            double screenPixelX,
            double screenPixelY,
            out WpfPoint localCenterPoint)
        {
            var resolvedScreenX = screenPixelX;
            var resolvedScreenY = screenPixelY;
            if (double.IsNaN(resolvedScreenX) || double.IsInfinity(resolvedScreenX) ||
                double.IsNaN(resolvedScreenY) || double.IsInfinity(resolvedScreenY))
            {
                if (GetCursorPos(out var p))
                {
                    resolvedScreenX = p.X;
                    resolvedScreenY = p.Y;
                }
                else
                {
                    var fallback = System.Windows.Forms.Cursor.Position;
                    resolvedScreenX = fallback.X;
                    resolvedScreenY = fallback.Y;
                }
            }

            var hwnd = AttachHwndSourceIfNeeded();
            if (WindowState != WindowState.Normal)
            {
                WindowState = WindowState.Normal;
            }

            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualWidth = Math.Max(320.0, SystemParameters.VirtualScreenWidth);
            var virtualHeight = Math.Max(240.0, SystemParameters.VirtualScreenHeight);

            Left = virtualLeft;
            Top = virtualTop;
            Width = virtualWidth;
            Height = virtualHeight;
            if (RootCanvas != null)
            {
                RootCanvas.Width = virtualWidth;
                RootCanvas.Height = virtualHeight;
            }

            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(
                    hwnd,
                    HwndTopMost,
                    0,
                    0,
                    0,
                    0,
                    SwpNoMove | SwpNoSize | SwpNoActivate);
            }

            var source = _hwndSource ?? (HwndSource?)PresentationSource.FromVisual(this);
            WpfPoint dipPoint;
            if (source?.CompositionTarget != null)
            {
                dipPoint = source.CompositionTarget.TransformFromDevice.Transform(
                    new WpfPoint(resolvedScreenX, resolvedScreenY));
            }
            else
            {
                var (dpiScaleX, dpiScaleY) = GetWindowDpiScale();
                dipPoint = new WpfPoint(resolvedScreenX / dpiScaleX, resolvedScreenY / dpiScaleY);
            }

            var localX = dipPoint.X - virtualLeft;
            var localY = dipPoint.Y - virtualTop;
            localX = Math.Max(0.0, Math.Min(virtualWidth, localX));
            localY = Math.Max(0.0, Math.Min(virtualHeight, localY));
            localCenterPoint = new WpfPoint(localX, localY);
        }

        private void SetWindowClickThrough(bool enabled)
        {
            if (_isWindowClickThroughEnabled == enabled)
            {
                return;
            }

            var windowHandle = _hwndSource?.Handle ?? IntPtr.Zero;
            if (windowHandle == IntPtr.Zero)
            {
                _isWindowClickThroughEnabled = false;
                return;
            }

            var style = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            var nextStyle = enabled
                ? style | WsExTransparent
                : style & ~WsExTransparent;
            if (nextStyle == style)
            {
                _isWindowClickThroughEnabled = enabled;
                return;
            }

            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(nextStyle));
            SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            _isWindowClickThroughEnabled = enabled;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmNcHitTest && _isEditModeEnabled)
            {
                var hasDragCapture = ReferenceEquals(Mouse.Captured, BackdropRoot);
                if (_pendingDragInteraction == null && !hasDragCapture)
                {
                    var screenPoint = GetPointFromLParam(lParam);
                    var localPoint = SafeScreenPointToRootCanvas(screenPoint);
                    if (!IsInteractivePoint(localPoint))
                    {
                        handled = true;
                        return new IntPtr(HtTransparent);
                    }
                }

                handled = true;
                return new IntPtr(HtClient);
            }

            return IntPtr.Zero;
        }

        private static WpfPoint GetPointFromLParam(IntPtr lParam)
        {
            var value = lParam.ToInt64();
            var x = unchecked((short)(value & 0xffff));
            var y = unchecked((short)((value >> 16) & 0xffff));
            return new WpfPoint(x, y);
        }

        private WpfPoint GetCursorPositionOnRootCanvas()
        {
            if (RootCanvas == null)
            {
                return new WpfPoint();
            }

            if (GetCursorPos(out var p))
            {
                return SafeScreenPointToRootCanvas(new WpfPoint(p.X, p.Y));
            }

            var cursor = System.Windows.Forms.Cursor.Position;
            return SafeScreenPointToRootCanvas(new WpfPoint(cursor.X, cursor.Y));
        }

        private WpfPoint SafeScreenPointToRootCanvas(WpfPoint screenPoint)
        {
            try
            {
                var source = _hwndSource ?? (HwndSource?)PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var dipPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
                    return new WpfPoint(dipPoint.X - Left, dipPoint.Y - Top);
                }

                if (RootCanvas != null && PresentationSource.FromVisual(RootCanvas) != null)
                {
                    return RootCanvas.PointFromScreen(screenPoint);
                }

                var (dpiScaleX, dpiScaleY) = GetWindowDpiScale();
                return new WpfPoint((screenPoint.X / dpiScaleX) - Left, (screenPoint.Y / dpiScaleY) - Top);
            }
            catch
            {
            }

            return new WpfPoint(_centerX, _centerY);
        }
    }
}
