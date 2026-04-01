using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using RadialSek.Models;
using Forms = System.Windows.Forms;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingIcon = System.Drawing.Icon;
using RadialSek.Services;
using RadialSek.UI;

namespace RadialSek
{
    public partial class MainWindow : Window
    {
        private readonly MenuConfigService _configService;
        private readonly GlobalInputHook _inputHook;
        private readonly LauncherService _launcherService;
        private readonly Forms.NotifyIcon _notifyIcon;
        private DrawingIcon _notifyIconAsset;
        private readonly Forms.ToolStripMenuItem _openMenuMenuItem;
        private readonly Forms.ToolStripMenuItem _toggleProgramMenuItem;
        private readonly SoundManager _soundManager;
        private MenuConfig _currentConfig;
        private RadialOverlayWindow? _overlay;
        private SettingsWindow? _settingsWindow;
        private bool _isProgramEnabled = true;
        private bool _isStartupInitialized;
        private readonly DispatcherTimer _lightIdleTimer;
        private DateTime _lastMenuInteractionUtc;
        private bool _isOverlayTransitionInProgress;

        public MainWindow()
        {
            InitializeComponent();

            _configService = new MenuConfigService();
            _launcherService = new LauncherService();
            _inputHook = new GlobalInputHook();
            _currentConfig = _configService.LoadConfig();
            _soundManager = SoundManager.Instance;
            _notifyIconAsset = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
            _openMenuMenuItem = CreateOpenMenuMenuItem();
            _toggleProgramMenuItem = CreateToggleProgramMenuItem();
            _notifyIcon = CreateNotifyIcon();
            _lastMenuInteractionUtc = DateTime.UtcNow;
            _lightIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _lightIdleTimer.Tick += OnLightIdleTimerTick;
            _lightIdleTimer.Start();
            Dispatcher.BeginInvoke(new Action(CompleteDeferredStartupInitialization), DispatcherPriority.Background);

            Closed += OnClosed;
        }

        private void CompleteDeferredStartupInitialization()
        {
            if (_isStartupInitialized)
            {
                return;
            }

            _isStartupInitialized = true;
            _soundManager.ApplyConfig(_currentConfig);
            _inputHook.UpdateShortcuts(_currentConfig.Shortcuts);
            _inputHook.SetProgramEnabled(_isProgramEnabled);
            _inputHook.ActivationRequested += OnActivationRequested;
            _inputHook.Start();
            TryApplyCustomTrayIcon();
            Dispatcher.BeginInvoke(new Action(PrewarmOverlayWindow), DispatcherPriority.ApplicationIdle);
        }

        private void TryApplyCustomTrayIcon()
        {
            try
            {
                var customIcon = LoadNotifyIcon();
                var previousIcon = _notifyIconAsset;
                _notifyIconAsset = customIcon;
                _notifyIcon.Icon = _notifyIconAsset;
                previousIcon.Dispose();
            }
            catch
            {
            }
        }

        private void PrewarmOverlayWindow()
        {
            if (_overlay != null || !_isProgramEnabled)
            {
                return;
            }

            try
            {
                var cursor = Forms.Cursor.Position;
                _overlay = new RadialOverlayWindow(
                    cursor.X,
                    cursor.Y,
                    _currentConfig,
                    _launcherService,
                    _configService);
                _overlay.MenuDismissed += OnOverlayMenuDismissed;
            }
            catch
            {
            }
        }

        private void OnActivationRequested(object? sender, ActivationEventArgs e)
        {
            if (e.ShortcutId == ActivationShortcut.ToggleProgramShortcutId)
            {
                ToggleProgramEnabled();
                return;
            }

            if (!_isProgramEnabled)
            {
                return;
            }

            OpenMenuAt(e.ScreenX, e.ScreenY);
        }

        private Forms.NotifyIcon CreateNotifyIcon()
        {
            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add(_openMenuMenuItem);
            contextMenu.Items.Add("Ayarlar", null, (_, __) => ShowSettings());
            contextMenu.Items.Add(_toggleProgramMenuItem);
            contextMenu.Items.Add("Çıkış", null, (_, __) => ExitApplication());

            var notifyIcon = new Forms.NotifyIcon
            {
                Icon = _notifyIconAsset,
                Text = "Radial Sek",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            notifyIcon.DoubleClick += (_, __) => ShowSettings();
            return notifyIcon;
        }

        private Forms.ToolStripMenuItem CreateOpenMenuMenuItem()
        {
            var item = new Forms.ToolStripMenuItem("Radial Menüyü Aç");
            item.Click += (_, __) =>
            {
                var position = Forms.Cursor.Position;
                OpenMenuAt(position.X, position.Y);
            };
            return item;
        }

        private Forms.ToolStripMenuItem CreateToggleProgramMenuItem()
        {
            var item = new Forms.ToolStripMenuItem();
            item.Click += (_, __) => ToggleProgramEnabled();
            UpdateToggleMenuItemText(item);
            return item;
        }

        private void ShowSettings()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_overlay != null)
                {
                    _overlay.FlushPendingChanges();
                    if (_overlay.IsVisible)
                    {
                        _overlay.Dismiss();
                    }
                }

                if (_settingsWindow != null && _settingsWindow.IsVisible)
                {
                    if (_settingsWindow.WindowState == WindowState.Minimized)
                    {
                        _settingsWindow.WindowState = WindowState.Normal;
                    }

                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow = new SettingsWindow(_configService);
                _settingsWindow.SettingsSaved += (_, __) => ReloadConfig();
                _settingsWindow.Closed += (_, __) =>
                {
                    ReloadConfig();
                    _settingsWindow = null;
                };
                _settingsWindow.Show();
                _settingsWindow.Activate();
            }));
        }

        public void OpenSettingsWindowFromOverlay()
        {
            ShowSettings();
        }

        private void ExitApplication()
        {
            if (Dispatcher.CheckAccess())
            {
                Close();
                return;
            }

            Dispatcher.BeginInvoke(new Action(Close));
        }

        private void ReloadConfig()
        {
            if (_overlay != null)
            {
                _overlay.MenuDismissed -= OnOverlayMenuDismissed;
                _overlay.DisposeWindow();
                _overlay = null;
            }

            _currentConfig = _configService.LoadConfig();
            _soundManager.ApplyConfig(_currentConfig);
            _inputHook.UpdateShortcuts(_currentConfig.Shortcuts);
            _inputHook.SetProgramEnabled(_isProgramEnabled);
            MarkMenuInteraction();
        }

        private void ToggleProgramEnabled()
        {
            _isProgramEnabled = !_isProgramEnabled;
            _inputHook.SetProgramEnabled(_isProgramEnabled);
            _soundManager.Play(_isProgramEnabled ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
            _soundManager.Play(SoundCue.Notification);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isProgramEnabled && _overlay != null && _overlay.IsVisible)
                {
                    _overlay.Dismiss();
                }

                _notifyIcon.Text = _isProgramEnabled
                    ? "Radial Sek"
                    : "Radial Sek (Devre Dışı)";
                UpdateToggleMenuItemText(_toggleProgramMenuItem);
                _notifyIcon.ShowBalloonTip(
                    1200,
                    "Radial Sek",
                    _isProgramEnabled ? "Program aktif." : "Program devre dışı.",
                    Forms.ToolTipIcon.Info);
            }));
        }

        private void OpenMenuAt(double screenX, double screenY)
        {
            void OpenMenuCore()
            {
                _isOverlayTransitionInProgress = true;
                try
                {
                    MarkMenuInteraction();
                    if (_overlay != null)
                    {
                        _overlay.ReleaseInputFailSafe("OpenMenuReuse");
                        if (_overlay.IsVisible)
                        {
                            _overlay.Dismiss();
                            return;
                        }

                        _overlay.ReopenAt(screenX, screenY);
                        return;
                    }

                    _overlay = new RadialOverlayWindow(
                        screenX,
                        screenY,
                        _currentConfig,
                        _launcherService,
                        _configService);
                    _overlay.MenuDismissed += OnOverlayMenuDismissed;
                    _overlay.ReopenAt(screenX, screenY);
                }
                catch (Exception ex)
                {
                    WriteDiagnosticLog("Overlay açılırken hata (ilk deneme)", ex);
                    if (!TryRecoverOverlayAfterOpenFailure(screenX, screenY, ex))
                    {
                        _soundManager.Play(SoundCue.Error);
                        _notifyIcon.ShowBalloonTip(
                            2000,
                            "Radial Sek",
                            "Menü açılırken hata oluştu. Detaylar radial_sek_error.log dosyasına yazıldı.",
                            Forms.ToolTipIcon.Error);
                    }
                }
                finally
                {
                    _isOverlayTransitionInProgress = false;
                }
            }

            if (Dispatcher.CheckAccess())
            {
                OpenMenuCore();
            }
            else
            {
                Dispatcher.Invoke(OpenMenuCore);
            }
        }

        private bool TryRecoverOverlayAfterOpenFailure(double screenX, double screenY, Exception rootException)
        {
            try
            {
                if (_overlay != null)
                {
                    _overlay.MenuDismissed -= OnOverlayMenuDismissed;
                    _overlay.ReleaseInputFailSafe("OpenMenuRecover");
                    _overlay.DisposeWindow();
                    _overlay = null;
                }
            }
            catch (Exception cleanupEx)
            {
                WriteDiagnosticLog("Overlay recovery temizliği sırasında hata", cleanupEx);
            }

            try
            {
                _overlay = new RadialOverlayWindow(
                    screenX,
                    screenY,
                    _currentConfig,
                    _launcherService,
                    _configService);
                _overlay.MenuDismissed += OnOverlayMenuDismissed;
                _overlay.ReopenAt(screenX, screenY);
                WriteDiagnosticLog("Overlay açılış fallback ile toparlandı", rootException);
                return true;
            }
            catch (Exception retryEx)
            {
                WriteDiagnosticLog("Overlay açılırken hata (yeniden oluşturma denemesi)", retryEx);
                return false;
            }
        }

        private static void WriteDiagnosticLog(string message, Exception ex)
        {
            try
            {
                var logPath = ApplicationStorageService.GetDiagnosticLogPath();
                var content =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine +
                    ex + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(logPath, content);
            }
            catch
            {
            }
        }

        private void UpdateToggleMenuItemText(Forms.ToolStripMenuItem item)
        {
            item.Text = _isProgramEnabled
                ? "Programı Devre Dışı Bırak"
                : "Programı Aktif Et";
        }

        private void OnOverlayMenuDismissed(object? sender, EventArgs e)
        {
            MarkMenuInteraction();
        }

        private void MarkMenuInteraction()
        {
            _lastMenuInteractionUtc = DateTime.UtcNow;
        }

        private bool IsLightIdleModeEnabled()
        {
            return _currentConfig.Features?.EnableLightIdleMode == true;
        }

        private TimeSpan GetLightIdleDelay()
        {
            var seconds = _currentConfig.Features?.LightIdleDelaySeconds ?? 20;
            seconds = Math.Max(5, Math.Min(60, seconds <= 0 ? 20 : seconds));
            return TimeSpan.FromSeconds(seconds);
        }

        private void OnLightIdleTimerTick(object? sender, EventArgs e)
        {
            if (!IsLightIdleModeEnabled() ||
                _overlay == null ||
                _overlay.IsVisible ||
                _isOverlayTransitionInProgress ||
                _settingsWindow?.IsVisible == true)
            {
                return;
            }

            if (DateTime.UtcNow - _lastMenuInteractionUtc < GetLightIdleDelay())
            {
                return;
            }

            EnterLightIdleMode();
        }

        private void EnterLightIdleMode()
        {
            if (_overlay == null || _overlay.IsVisible || _isOverlayTransitionInProgress)
            {
                return;
            }

            _overlay.ReleaseInputFailSafe("LightIdleMode");
            _overlay.MenuDismissed -= OnOverlayMenuDismissed;
            _overlay.DisposeWindow();
            _overlay = null;
            RadialOverlayWindow.ReleaseIdleResources();
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            TrimProcessWorkingSet();
            _lastMenuInteractionUtc = DateTime.UtcNow;
        }

        private static void TrimProcessWorkingSet()
        {
            try
            {
                using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                EmptyWorkingSet(currentProcess.Handle);
            }
            catch
            {
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _lightIdleTimer.Stop();
            if (_overlay != null)
            {
                _overlay.MenuDismissed -= OnOverlayMenuDismissed;
            }
            _overlay?.DisposeWindow();
            _overlay = null;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIconAsset.Dispose();
            _inputHook.Dispose();
            Application.Current.Shutdown();
        }

        private static DrawingIcon LoadNotifyIcon()
        {
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sek_logo_final_2.ico");
            if (File.Exists(icoPath))
            {
                using var sourceIcon = new DrawingIcon(icoPath);
                return (DrawingIcon)sourceIcon.Clone();
            }

            var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sek_system_tray_logo.png");
            if (!File.Exists(pngPath))
            {
                return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
            }

            using var bitmap = new DrawingBitmap(pngPath);
            var handle = bitmap.GetHicon();
            var sourceIconFromHandle = DrawingIcon.FromHandle(handle);
            try
            {
                return (DrawingIcon)sourceIconFromHandle.Clone();
            }
            finally
            {
                sourceIconFromHandle.Dispose();
                DestroyIcon(handle);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
    }
}

