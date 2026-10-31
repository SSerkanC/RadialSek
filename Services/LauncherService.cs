using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RadialSek.Services
{
    public class LauncherService
    {
        private static readonly HashSet<string> KnownBrowserExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome.exe", "msedge.exe", "firefox.exe", "zen.exe", "brave.exe", "opera.exe", "vivaldi.exe", "arc.exe"
        };

        public void Launch(string targetPath, Rectangle? targetWorkArea = null)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(targetPath.Trim());
            var workingDir = ResolveWorkingDirectory(expandedPath);

            // Hedef ekran verilmişse, başlatma öncesindeki açık pencereleri ve ön plan penceresini kaydet
            HashSet<IntPtr>? existingWindows = null;
            IntPtr initialForeground = IntPtr.Zero;
            uint currentAppPid = (uint)Process.GetCurrentProcess().Id;

            if (targetWorkArea.HasValue)
            {
                existingWindows = GetVisibleTopLevelWindows();
                initialForeground = GetForegroundWindow();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = expandedPath,
                UseShellExecute = true,
                WorkingDirectory = workingDir
            };

            // Çoklu monitörde tarayıcılar için özel işlem:
            // Eğer bir tarayıcı zaten arka planda veya diğer ekranda açıksa,
            // yeni bir pencere açması için '--new-window' argümanı ekle.
            if (targetWorkArea.HasValue)
            {
                var resolvedExe = ResolveExecutablePath(expandedPath);
                var exeName = Path.GetFileName(resolvedExe);
                if (KnownBrowserExecutables.Contains(exeName))
                {
                    var procName = Path.GetFileNameWithoutExtension(exeName);
                    try
                    {
                        if (Process.GetProcessesByName(procName).Length > 0)
                        {
                            startInfo.Arguments = "--new-window";
                        }
                    }
                    catch { }
                }
            }

            Process? startedProcess = null;
            try
            {
                startedProcess = Process.Start(startInfo);
            }
            catch
            {
                try
                {
                    // Fallback: argümansız başlat
                    startedProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = expandedPath,
                        UseShellExecute = true,
                        WorkingDirectory = workingDir
                    });
                }
                catch { }
            }

            // Hedef ekrana yönlendirme izleyicisi (Yalnızca tıklama anında kısa süreli mikro görev çalışır)
            if (targetWorkArea.HasValue && existingWindows != null)
            {
                int startedPid = startedProcess?.Id ?? 0;
                var targetRect = targetWorkArea.Value;

                Task.Run(async () =>
                {
                    // En fazla 3 saniye boyunca (60 x 50ms) yeni pencereyi ara
                    for (int i = 0; i < 60; i++)
                    {
                        await Task.Delay(50).ConfigureAwait(false);

                        // 1. Başlatılan sürecin doğrudan MainWindowHandle'ı oluştu mu?
                        if (startedPid != 0)
                        {
                            try
                            {
                                using var proc = Process.GetProcessById(startedPid);
                                proc.Refresh();
                                var mainHwnd = proc.MainWindowHandle;
                                if (mainHwnd != IntPtr.Zero && IsWindow(mainHwnd) && IsWindowVisible(mainHwnd) && !existingWindows.Contains(mainHwnd))
                                {
                                    if (EnsureWindowOnMonitor(mainHwnd, targetRect))
                                    {
                                        return;
                                    }
                                }
                            }
                            catch { }
                        }

                        // 2. Ön plana gelen yeni pencere mi oldu?
                        var fg = GetForegroundWindow();
                        if (fg != IntPtr.Zero && fg != initialForeground && !existingWindows.Contains(fg))
                        {
                            GetWindowThreadProcessId(fg, out var fgPid);
                            if (fgPid != currentAppPid && IsWindowValidTopLevel(fg))
                            {
                                if (EnsureWindowOnMonitor(fg, targetRect))
                                {
                                    return;
                                }
                            }
                        }

                        // 3. Top-level pencereler arasında yeni oluşan var mı?
                        var newWnd = FindAnyNewTopLevelWindow(existingWindows, currentAppPid);
                        if (newWnd != IntPtr.Zero)
                        {
                            if (EnsureWindowOnMonitor(newWnd, targetRect))
                            {
                                return;
                            }
                        }
                    }
                });
            }
        }

        private static bool EnsureWindowOnMonitor(IntPtr hwnd, Rectangle targetRect)
        {
            try
            {
                if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;

                // Pencerenin şu anda hangi ekranda olduğunu tespit et
                var currentScreen = System.Windows.Forms.Screen.FromHandle(hwnd);
                var currentWork = currentScreen.WorkingArea;

                // Zaten hedeflenen ekrandaysa hiçbir şey yapma
                if (currentWork.X == targetRect.X && currentWork.Y == targetRect.Y)
                {
                    return true;
                }

                if (!GetWindowRect(hwnd, out var rect)) return false;
                int curW = rect.Right - rect.Left;
                int curH = rect.Bottom - rect.Top;
                if (curW <= 0 || curH <= 0) return false;

                bool isMaximized = IsZoomed(hwnd);
                if (isMaximized)
                {
                    // Maksimize edilmiş pencereleri diğer ekrana taşıma (Restore -> Move -> Re-maximize)
                    ShowWindow(hwnd, SW_RESTORE);
                    int newX = targetRect.X + Math.Max(0, (targetRect.Width - curW) / 2);
                    int newY = targetRect.Y + Math.Max(0, (targetRect.Height - curH) / 2);
                    SetWindowPos(hwnd, IntPtr.Zero, newX, newY, curW, curH, SWP_NOZORDER | SWP_NOACTIVATE);
                    ShowWindow(hwnd, SW_MAXIMIZE);
                }
                else
                {
                    // Normal pencere: Hedef ekranın sınırları içinde ortala
                    int newW = Math.Min(curW, targetRect.Width);
                    int newH = Math.Min(curH, targetRect.Height);
                    int newX = targetRect.X + Math.Max(0, (targetRect.Width - newW) / 2);
                    int newY = targetRect.Y + Math.Max(0, (targetRect.Height - newH) / 2);

                    SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newW, newH, SWP_NOZORDER | SWP_SHOWWINDOW);
                }

                SetForegroundWindow(hwnd);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static HashSet<IntPtr> GetVisibleTopLevelWindows()
        {
            var windows = new HashSet<IntPtr>();
            try
            {
                EnumWindows((hWnd, _) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        windows.Add(hWnd);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return windows;
        }

        private static IntPtr FindAnyNewTopLevelWindow(HashSet<IntPtr> existing, uint currentAppPid)
        {
            IntPtr found = IntPtr.Zero;
            try
            {
                EnumWindows((hWnd, _) =>
                {
                    if (!existing.Contains(hWnd) && IsWindowVisible(hWnd))
                    {
                        GetWindowThreadProcessId(hWnd, out var pid);
                        if (pid != currentAppPid && IsWindowValidTopLevel(hWnd))
                        {
                            found = hWnd;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return found;
        }

        private static bool IsWindowValidTopLevel(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd)) return false;

            long exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

            if (GetWindowRect(hWnd, out var r))
            {
                if (r.Right - r.Left <= 60 || r.Bottom - r.Top <= 60) return false;
            }

            return true;
        }

        private static string ResolveExecutablePath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return string.Empty;
                var ext = Path.GetExtension(path);
                if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)) return path;

                if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell != null)
                        {
                            dynamic? shortcut = shell.CreateShortcut(path);
                            if (shortcut != null)
                            {
                                string target = shortcut.TargetPath;
                                if (!string.IsNullOrWhiteSpace(target)) return target;
                            }
                        }
                    }
                }
            }
            catch { }
            return path;
        }

        private static string ResolveWorkingDirectory(string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    return Path.GetDirectoryName(targetPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                }

                if (Directory.Exists(targetPath))
                {
                    return targetPath;
                }
            }
            catch { }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        #region Win32 API

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        private static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex).ToInt64() : GetWindowLong32(hWnd, nIndex);
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion
    }
}
