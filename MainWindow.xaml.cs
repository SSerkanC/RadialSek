using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
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
        private bool _isInLightIdleMode;
        private volatile bool _isOverlayInputActive;

        public MainWindow()
        {
            InitializeComponent();

            _configService = new MenuConfigService();
            _launcherService = new LauncherService();
            _inputHook = new GlobalInputHook();
            _inputHook.IsMouseActivationPaused = () => _isOverlayInputActive && _overlay?.IsVisible == true;
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
            SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
            SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
            SystemEvents.SessionSwitch += OnSystemSessionSwitch;
            TryApplyCustomTrayIcon();
            SideDockService.Instance.Initialize(_launcherService, _configService, _currentConfig);
            Dispatcher.BeginInvoke(new Action(PrewarmOverlayWindow), DispatcherPriority.ApplicationIdle);
        }

        private void OnSystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    if (e.Mode == PowerModes.Suspend)
                    {
                        _inputHook.ResetState();
                        DisposeOverlayInstance("SystemSuspend");
                        return;
                    }

                    if (e.Mode == PowerModes.Resume)
                    {
                        DisposeOverlayInstance("SystemResume");
                        _inputHook.Restart();
                        MarkMenuInteraction();
                    }
                }));
        }

        private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    DisposeOverlayInstance("DisplaySettingsChanged");
                    SideDockService.Instance.Reposition();
                    _inputHook.ResetState();
                    MarkMenuInteraction();
                }));
        }

        private void OnSystemSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    DisposeOverlayInstance("SessionSwitch");
                    if (e.Reason == SessionSwitchReason.SessionUnlock ||
                        e.Reason == SessionSwitchReason.ConsoleConnect ||
                        e.Reason == SessionSwitchReason.RemoteConnect)
                    {
                        _inputHook.Restart();
                    }
                    else
                    {
                        _inputHook.ResetState();
                    }

                    MarkMenuInteraction();
                }));
        }

        private void DisposeOverlayInstance(string reason)
        {
            _isOverlayInputActive = false;
            if (_overlay == null)
            {
                return;
            }

            var instance = _overlay;
            _overlay = null;
            try
            {
                instance.MenuDismissed -= OnOverlayMenuDismissed;
                instance.ReleaseInputFailSafe(reason);
                instance.DisposeWindow();
            }
            catch (Exception ex)
            {
                WriteDiagnosticLog("Overlay temizlenirken hata (" + reason + ")", ex);
            }
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
            catch (Exception ex)
            {
                WriteDiagnosticLog("Özel tray ikonu uygulanırken hata oluştu, varsayılan ikon kullanılıyor.", ex);
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
            var shortcutId = e.ShortcutId;
            var screenX = e.ScreenX;
            var screenY = e.ScreenY;

            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (shortcutId == ActivationShortcut.ToggleProgramShortcutId)
                    {
                        ToggleProgramEnabled();
                        return;
                    }

                    if (!_isProgramEnabled)
                    {
                        return;
                    }

                    OpenMenuAt(screenX, screenY);
                }));
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
                _isOverlayInputActive = false;
            }

            _currentConfig = _configService.LoadConfig();
            _soundManager.ApplyConfig(_currentConfig);
            _inputHook.UpdateShortcuts(_currentConfig.Shortcuts);
            _inputHook.SetProgramEnabled(_isProgramEnabled);
            SideDockService.Instance.RefreshConfig(_currentConfig);
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
                        _isOverlayInputActive = true;
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
                    _isOverlayInputActive = true;
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
            DisposeOverlayInstance("OpenMenuRecover");

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
                _isOverlayInputActive = true;
                WriteDiagnosticLog("Overlay açılış fallback ile toparlandı", rootException);
                return true;
            }
            catch (Exception retryEx)
            {
                WriteDiagnosticLog("Overlay açılırken hata (yeniden oluşturma denemesi)", retryEx);
                DisposeOverlayInstance("OpenMenuRecoverFailed");
                return false;
            }
        }

        private static void WriteDiagnosticLog(string message, Exception ex)
        {
            try
            {
                var logPath = ApplicationStorageService.GetDiagnosticLogPath();
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

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
            _isOverlayInputActive = false;
            MarkMenuInteraction();
        }

        private void MarkMenuInteraction()
        {
            _lastMenuInteractionUtc = DateTime.UtcNow;
            _isInLightIdleMode = false;
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
            if (_overlay != null && !_overlay.IsVisible && _isOverlayInputActive)
            {
                _isOverlayInputActive = false;
            }

            if (!IsLightIdleModeEnabled() ||
                _isInLightIdleMode ||
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

            _isInLightIdleMode = true;
            _isOverlayInputActive = false;
            try
            {
                _overlay.TrimForLightIdle();
            }
            catch (Exception ex)
            {
                WriteDiagnosticLog("Hafif bekleme moduna geçilirken hata", ex);
                DisposeOverlayInstance("LightIdleModeFallback");
            }

            RadialOverlayWindow.ReleaseIdleResources();
            GC.Collect(1, GCCollectionMode.Optimized, blocking: false);
            _lastMenuInteractionUtc = DateTime.UtcNow;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _lightIdleTimer.Stop();
            SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSystemSessionSwitch;
            DisposeOverlayInstance("MainWindowClosed");
            SideDockService.Instance.CloseWindow();
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
