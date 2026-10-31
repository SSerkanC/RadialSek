using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class SideDockWindow
    {
        private const double WeatherCardWidth = 240.0;
        private const double WeatherCardMargin = 2.0;
        public const double WeatherCardTotalWidth = WeatherCardWidth + WeatherCardMargin;

        private readonly DispatcherTimer _weatherDelayTimer = new();
        private readonly DispatcherTimer _ribbonAutoDismissTimer = new();
        private readonly DispatcherTimer _hardwareMonitorTimer = new();
        private bool _isWeatherCardVisible;
        private bool _isShowingHardwareFace;
        private bool _isFlipping;
        private int _weatherFetchVersion;
        private readonly Random _weatherRnd = new();
        private readonly List<UIElement> _activeWeatherVisuals = new();
        private readonly List<UIElement> _activeRibbonVisuals = new();
        private readonly List<AnimationClock> _activeWeatherClocks = new();
        private bool _isWeatherVisualsPaused;
        private static ImageSource? _cachedCloudSprite;
        private static bool _cloudSpriteChecked;

        private static ulong _prevIdleTime;
        private static ulong _prevKernelTime;
        private static ulong _prevUserTime;
        private static double _lastCpuUsage;

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private void StartManagedWeatherAnimation(IAnimatable target, DependencyProperty dp, AnimationTimeline anim, int desiredFps = 30)
        {
            try
            {
                Timeline.SetDesiredFrameRate(anim, desiredFps);
                var clock = anim.CreateClock();
                target.ApplyAnimationClock(dp, clock);
                _activeWeatherClocks.Add(clock);
            }
            catch
            {
                if (target is UIElement ue)
                {
                    ue.BeginAnimation(dp, anim);
                }
                else if (target is Animatable an)
                {
                    an.BeginAnimation(dp, anim);
                }
            }
        }

        public void PauseSideDockWeatherVisuals()
        {
            if (_isWeatherVisualsPaused || _activeWeatherClocks.Count == 0) return;
            _isWeatherVisualsPaused = true;
            foreach (var clock in _activeWeatherClocks)
            {
                try { clock.Controller?.Pause(); } catch { }
            }
        }

        public void ResumeSideDockWeatherVisuals()
        {
            if (!_isWeatherVisualsPaused || _activeWeatherClocks.Count == 0) return;
            _isWeatherVisualsPaused = false;
            foreach (var clock in _activeWeatherClocks)
            {
                try { clock.Controller?.Resume(); } catch { }
            }
        }

        private void InitializeWeatherComponents()
        {
            // Kullanıcı isteği: Hava durumu paneli 1 saniye açık kaldığında devreye girer
            _weatherDelayTimer.Interval = TimeSpan.FromSeconds(1);
            _weatherDelayTimer.Tick += OnWeatherDelayTimerTick;

            // Kullanıcı isteği: Şeritler 2.5 saniye sonra alttan ve üstten silinerek kaybolur
            _ribbonAutoDismissTimer.Interval = TimeSpan.FromSeconds(2.5);
            _ribbonAutoDismissTimer.Tick += OnRibbonAutoDismissTimerTick;

            // Donanım monitörü (Kart 3D çevrildiğinde her 1.5 saniyede hafifçe günceller)
            _hardwareMonitorTimer.Interval = TimeSpan.FromSeconds(1.5);
            _hardwareMonitorTimer.Tick += OnHardwareMonitorTimerTick;

            try
            {
                if (GetSystemTimes(out var iFt, out var kFt, out var uFt))
                {
                    _prevIdleTime = FileTimeToUInt64(iFt);
                    _prevKernelTime = FileTimeToUInt64(kFt);
                    _prevUserTime = FileTimeToUInt64(uFt);
                }
            }
            catch { }
        }

        private void PrepareWeatherCardForExpand()
        {
            _weatherDelayTimer.Stop();
            _ribbonAutoDismissTimer.Stop();
            _hardwareMonitorTimer.Stop();
            _isShowingHardwareFace = false;
            _isFlipping = false;
            if (WeatherCardFlipTransform != null) WeatherCardFlipTransform.ScaleX = 1.0;
            if (WeatherFaceGrid != null) WeatherFaceGrid.Visibility = Visibility.Visible;
            if (HardwareFaceGrid != null) HardwareFaceGrid.Visibility = Visibility.Collapsed;
            if (WeatherCard != null) WeatherCard.ToolTip = "Hava Durumu / Sistem Kartı (Değiştirmek için tıklayın)";
            _isWeatherCardVisible = false;
            StopSideDockWeatherVisuals();
            StopWeatherRibbonsAnimation();

            if (WeatherCard != null)
            {
                WeatherCard.Visibility = Visibility.Collapsed;
                WeatherCard.Opacity = 0.0;
            }
            if (WeatherCardTransform != null)
            {
                WeatherCardTransform.Y = 60.0;
            }
            if (WeatherRibbonsUnderCanvas != null)
            {
                WeatherRibbonsUnderCanvas.Visibility = Visibility.Collapsed;
                WeatherRibbonsUnderCanvas.Opacity = 0.0;
            }
            if (WeatherRibbonsOverCanvas != null)
            {
                WeatherRibbonsOverCanvas.Visibility = Visibility.Collapsed;
                WeatherRibbonsOverCanvas.Opacity = 0.0;
            }

            var isEnabled = _config?.Tools?.SideMenu?.ShowWeatherCard ?? true;
            if (!isEnabled) return;

            // 1 saniye sonra hava durumu kartını ve şeritlerini getirecek sayaç
            _weatherDelayTimer.Start();
        }

        private void HideWeatherCardAndStopVisuals()
        {
            _weatherDelayTimer.Stop();
            _ribbonAutoDismissTimer.Stop();
            _hardwareMonitorTimer.Stop();
            _isShowingHardwareFace = false;
            _isFlipping = false;
            if (WeatherCardFlipTransform != null) WeatherCardFlipTransform.ScaleX = 1.0;
            if (WeatherFaceGrid != null) WeatherFaceGrid.Visibility = Visibility.Visible;
            if (HardwareFaceGrid != null) HardwareFaceGrid.Visibility = Visibility.Collapsed;
            _isWeatherCardVisible = false;
            StopSideDockWeatherVisuals();
            StopWeatherRibbonsAnimation();

            if (WeatherCard != null)
            {
                WeatherCard.Visibility = Visibility.Collapsed;
                WeatherCard.Opacity = 0.0;
            }
            if (WeatherCardTransform != null)
            {
                WeatherCardTransform.Y = 60.0;
            }
            if (WeatherRibbonsUnderCanvas != null)
            {
                WeatherRibbonsUnderCanvas.Visibility = Visibility.Collapsed;
                WeatherRibbonsUnderCanvas.Opacity = 0.0;
            }
            if (WeatherRibbonsOverCanvas != null)
            {
                WeatherRibbonsOverCanvas.Visibility = Visibility.Collapsed;
                WeatherRibbonsOverCanvas.Opacity = 0.0;
            }
        }

        private void OnWeatherDelayTimerTick(object? sender, EventArgs e)
        {
            _weatherDelayTimer.Stop();
            if (!_isExpanded || _isTransitioning) return;

            var isEnabled = _config?.Tools?.SideMenu?.ShowWeatherCard ?? true;
            if (!isEnabled) return;

            ShowWeatherCardWithAnimation();
        }

        private void ShowWeatherCardWithAnimation()
        {
            if (_isWeatherCardVisible) return;
            var isEnabled = _config?.Tools?.SideMenu?.ShowWeatherCard ?? true;
            if (!isEnabled) return;
            _isWeatherCardVisible = true;

            // Pencere genişliği ve konumu zaten ExpandMenu'de sabitlendi.
            // Left veya Width asla değiştirilmez; sıçrama ve titremeyi tamamen yok eder.
            var totalWidth = ActualWidth > 0 ? ActualWidth : GetTargetExpandedWidth();

            // 1. Şeritleri başlat (Photoshop tasarımı 3 şerit)
            StartWeatherRibbonsAnimation(totalWidth);

            // 2. Hava durumu kartını aşağıdan yukarıya pürüzsüzce süzülerek göster
            WeatherCard.Visibility = Visibility.Visible;

            var slideUp = new DoubleAnimation(50.0, 0.0, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            WeatherCardTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            WeatherCard.BeginAnimation(OpacityProperty, fadeIn);

            // Hava durumu görsel ve animasyonlarını başlat
            StartSideDockWeatherVisuals();
        }

        // ==========================================
        // 3'LÜ BAĞLANTI ŞERİTLERİ (PHOTOSHOP TASARIMI & ANİMASYONU)
        // ==========================================

        private void StartWeatherRibbonsAnimation(double totalWidth)
        {
            StopWeatherRibbonsAnimation();
            if (WeatherRibbonsUnderCanvas == null || WeatherRibbonsOverCanvas == null) return;

            WeatherRibbonsUnderCanvas.Width = totalWidth;
            WeatherRibbonsOverCanvas.Width = totalWidth;
            WeatherRibbonsUnderCanvas.Height = 380;
            WeatherRibbonsOverCanvas.Height = 380;

            WeatherRibbonsUnderCanvas.Visibility = Visibility.Visible;
            WeatherRibbonsOverCanvas.Visibility = Visibility.Visible;
            WeatherRibbonsUnderCanvas.Opacity = 0.0;
            WeatherRibbonsOverCanvas.Opacity = 0.0;

            var baseColor = GetActiveTabBaseColor();
            var waveColor = Color.FromRgb(32, 38, 50); // Antrasit / koyu UI dalga rengi

            // 1. Sol İnce Şerit (Dış hat, 6px) - Z-Index 8 (DrawerPanel arkasında)
            // Üstten alta doğru renk akışı (2600ms - akıcı ve sakin)
            var outerPath = CreateRibbonPath("M 332,32 L 192,172 L 192,380", 6.0);
            var outerBrush = CreateMovingGradientBrush(baseColor, waveColor, fromTopToBottom: true, durationMs: 2600);
            outerPath.Stroke = outerBrush;
            WeatherRibbonsUnderCanvas.Children.Add(outerPath);
            _activeRibbonVisuals.Add(outerPath);

            // 2. Orta Kalın Şerit (Ana hat, 16px) - Z-Index 12 (DrawerPanel ÜZERİNDE)
            // Alttan üste doğru renk akışı (2600ms)
            var middlePath = CreateRibbonPath("M 350,50 L 210,190 L 210,380", 16.0);
            var middleBrush = CreateMovingGradientBrush(baseColor, waveColor, fromTopToBottom: false, durationMs: 2600);
            middlePath.Stroke = middleBrush;
            WeatherRibbonsOverCanvas.Children.Add(middlePath);
            _activeRibbonVisuals.Add(middlePath);

            // 3. Sağ İnce Şerit (İç hat, 6px) - Z-Index 8 (DrawerPanel arkasında)
            // Üstten alta doğru renk akışı (2600ms)
            var innerPath = CreateRibbonPath("M 368,68 L 228,208 L 228,380", 6.0);
            var innerBrush = CreateMovingGradientBrush(baseColor, waveColor, fromTopToBottom: true, durationMs: 2600);
            innerPath.Stroke = innerBrush;
            WeatherRibbonsUnderCanvas.Children.Add(innerPath);
            _activeRibbonVisuals.Add(innerPath);

            // Şeritlerin yumuşak belirmesi (üstten ve alttan süzülerek görünür)
            var appearAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            WeatherRibbonsUnderCanvas.BeginAnimation(OpacityProperty, appearAnim);
            WeatherRibbonsOverCanvas.BeginAnimation(OpacityProperty, appearAnim);

            // 2.5 saniye sonra şeritlerin alttan ve üstten başlayarak silinmesi
            _ribbonAutoDismissTimer.Stop();
            _ribbonAutoDismissTimer.Start();
        }

        private static System.Windows.Shapes.Path CreateRibbonPath(string data, double strokeThickness)
        {
            return new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                StrokeThickness = strokeThickness,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeLineJoin = PenLineJoin.Miter,
                IsHitTestVisible = false
            };
        }

        private static LinearGradientBrush CreateMovingGradientBrush(Color baseColor, Color waveColor, bool fromTopToBottom, int durationMs = 2600)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = fromTopToBottom ? new Point(0, 0) : new Point(0, 1),
                EndPoint = fromTopToBottom ? new Point(0, 1) : new Point(0, 0)
            };

            var stop1 = new GradientStop(baseColor, -0.4);
            var stopWave = new GradientStop(waveColor, -0.2);
            var stop2 = new GradientStop(baseColor, 0.0);

            brush.GradientStops.Add(stop1);
            brush.GradientStops.Add(stopWave);
            brush.GradientStops.Add(stop2);

            var animDuration = TimeSpan.FromMilliseconds(durationMs);

            var stop1Anim = new DoubleAnimation(-0.4, 1.2, animDuration) { RepeatBehavior = RepeatBehavior.Forever };
            var stopWaveAnim = new DoubleAnimation(-0.2, 1.4, animDuration) { RepeatBehavior = RepeatBehavior.Forever };
            var stop2Anim = new DoubleAnimation(0.0, 1.6, animDuration) { RepeatBehavior = RepeatBehavior.Forever };

            Timeline.SetDesiredFrameRate(stop1Anim, 30);
            Timeline.SetDesiredFrameRate(stopWaveAnim, 30);
            Timeline.SetDesiredFrameRate(stop2Anim, 30);

            stop1.BeginAnimation(GradientStop.OffsetProperty, stop1Anim);
            stopWave.BeginAnimation(GradientStop.OffsetProperty, stopWaveAnim);
            stop2.BeginAnimation(GradientStop.OffsetProperty, stop2Anim);

            return brush;
        }

        private void OnRibbonAutoDismissTimerTick(object? sender, EventArgs e)
        {
            _ribbonAutoDismissTimer.Stop();
            if (WeatherRibbonsUnderCanvas?.Visibility != Visibility.Visible && WeatherRibbonsOverCanvas?.Visibility != Visibility.Visible) return;

            // 2.5 saniye sonra şeritlerin alttan ve üstten silinerek kaybolması
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, __) =>
            {
                StopWeatherRibbonsAnimation();
            };
            WeatherRibbonsUnderCanvas?.BeginAnimation(OpacityProperty, fadeOut);
            WeatherRibbonsOverCanvas?.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void StopWeatherRibbonsAnimation()
        {
            try
            {
                _ribbonAutoDismissTimer.Stop();

                if (WeatherRibbonsUnderCanvas != null)
                {
                    try { WeatherRibbonsUnderCanvas.BeginAnimation(OpacityProperty, null); } catch { }
                    WeatherRibbonsUnderCanvas.Visibility = Visibility.Collapsed;
                    WeatherRibbonsUnderCanvas.Children.Clear();
                }

                if (WeatherRibbonsOverCanvas != null)
                {
                    try { WeatherRibbonsOverCanvas.BeginAnimation(OpacityProperty, null); } catch { }
                    WeatherRibbonsOverCanvas.Visibility = Visibility.Collapsed;
                    WeatherRibbonsOverCanvas.Children.Clear();
                }

                foreach (var visual in _activeRibbonVisuals)
                {
                    if (visual is System.Windows.Shapes.Path p && p.Stroke is LinearGradientBrush lgb)
                    {
                        foreach (var s in lgb.GradientStops)
                        {
                            try { s.BeginAnimation(GradientStop.OffsetProperty, null); } catch { }
                        }
                    }
                }
                _activeRibbonVisuals.Clear();
            }
            catch { }
        }

        private Color GetActiveTabBaseColor()
        {
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu?.Tabs != null)
            {
                var active = sideMenu.Tabs.Find(t => t.Id == sideMenu.ActiveTabId);
                if (active != null && !string.IsNullOrWhiteSpace(active.Color))
                {
                    try
                    {
                        return (Color)ColorConverter.ConvertFromString(active.Color);
                    }
                    catch { }
                }
            }
            // Varsayılan canlı turuncu renk (#F06500)
            return Color.FromRgb(240, 101, 0);
        }

        // ==========================================
        // HAVA DURUMU GÖRSELLEŞTİRME & ANİMASYONLARI
        // ==========================================

        private void OnWeatherCardClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_isFlipping || WeatherCardFlipTransform == null) return;
            FlipWeatherCard();
        }

        private void FlipWeatherCard()
        {
            _isFlipping = true;

            var shrinkAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            shrinkAnim.Completed += (s, ev) =>
            {
                _isShowingHardwareFace = !_isShowingHardwareFace;

                if (_isShowingHardwareFace)
                {
                    if (WeatherFaceGrid != null) WeatherFaceGrid.Visibility = Visibility.Collapsed;
                    if (HardwareFaceGrid != null) HardwareFaceGrid.Visibility = Visibility.Visible;
                    if (WeatherCard != null) WeatherCard.ToolTip = "Sistem Nabzı (Hava durumuna dönmek için tıklayın)";
                    UpdateHardwareStats();
                    _hardwareMonitorTimer.Start();
                }
                else
                {
                    _hardwareMonitorTimer.Stop();
                    if (HardwareFaceGrid != null) HardwareFaceGrid.Visibility = Visibility.Collapsed;
                    if (WeatherFaceGrid != null) WeatherFaceGrid.Visibility = Visibility.Visible;
                    if (WeatherCard != null) WeatherCard.ToolTip = "Hava Durumu Bilgisi (Sistem nabzına geçmek için tıklayın)";
                    StartSideDockWeatherVisuals();
                }

                var expandAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                expandAnim.Completed += (s2, ev2) =>
                {
                    _isFlipping = false;
                };

                WeatherCardFlipTransform.BeginAnimation(ScaleTransform.ScaleXProperty, expandAnim);
            };

            WeatherCardFlipTransform.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkAnim);
        }

        private void OnHardwareMonitorTimerTick(object? sender, EventArgs e)
        {
            if (_isShowingHardwareFace && _isWeatherCardVisible && _isExpanded)
            {
                UpdateHardwareStats();
            }
            else
            {
                _hardwareMonitorTimer.Stop();
            }
        }

        private static ulong FileTimeToUInt64(in FILETIME ft)
        {
            return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        }

        private static double CalculateCpuUsage()
        {
            if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
                return _lastCpuUsage;

            ulong idle = FileTimeToUInt64(idleFt);
            ulong kernel = FileTimeToUInt64(kernelFt);
            ulong user = FileTimeToUInt64(userFt);

            if (_prevKernelTime != 0 || _prevUserTime != 0)
            {
                ulong totalDiff = (kernel - _prevKernelTime) + (user - _prevUserTime);
                ulong idleDiff = idle - _prevIdleTime;

                if (totalDiff > 0)
                {
                    double cpu = (1.0 - ((double)idleDiff / totalDiff)) * 100.0;
                    _lastCpuUsage = Math.Clamp(Math.Round(cpu, 0), 0.0, 100.0);
                }
            }

            _prevIdleTime = idle;
            _prevKernelTime = kernel;
            _prevUserTime = user;

            return _lastCpuUsage;
        }

        private void UpdateHardwareStats()
        {
            try
            {
                // 1. CPU
                double cpuPercent = CalculateCpuUsage();
                if (HwCpuText != null)
                {
                    HwCpuText.Text = $"%{cpuPercent:0}";
                }
                if (HwCpuBar != null)
                {
                    double targetWidth = Math.Clamp(cpuPercent / 100.0 * 212.0, 2.0, 212.0);
                    HwCpuBar.Width = targetWidth;
                }

                // 2. RAM
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                    uint memLoad = memStatus.dwMemoryLoad;

                    if (HwRamText != null)
                    {
                        HwRamText.Text = $"{usedGb:0.1} / {totalGb:0.0} GB (%{memLoad})";
                    }
                    if (HwRamBar != null)
                    {
                        double targetWidth = Math.Clamp(memLoad / 100.0 * 212.0, 2.0, 212.0);
                        HwRamBar.Width = targetWidth;
                    }
                }
            }
            catch
            {
            }
        }

        public async void StartSideDockWeatherVisuals()
        {
            if (WeatherCanvas == null || !_isWeatherCardVisible) return;

            var version = ++_weatherFetchVersion;
            var weatherConfig = _config.Weather ?? new WeatherSettings();

            WeatherSnapshot? snapshot;
            if (!weatherConfig.UseLiveData)
            {
                snapshot = CreateManualWeatherSnapshot(weatherConfig);
            }
            else
            {
                snapshot = OpenMeteoWeatherService.Instance.GetCachedSnapshot() ?? CreateFallbackWeatherSnapshot();
            }

            ApplySideDockWeatherSnapshot(snapshot, weatherConfig);

            if (weatherConfig.UseLiveData)
            {
                try
                {
                    var liveSnapshot = await OpenMeteoWeatherService.Instance.GetCurrentSnapshotAsync().ConfigureAwait(false);
                    if (liveSnapshot != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (version == _weatherFetchVersion && _isWeatherCardVisible)
                            {
                                ApplySideDockWeatherSnapshot(liveSnapshot, weatherConfig);
                            }
                        }, DispatcherPriority.Background);
                    }
                }
                catch { }
            }
        }

        public void StopSideDockWeatherVisuals()
        {
            _weatherFetchVersion++;
            ClearSideDockWeatherVisuals();
        }

        private void ClearSideDockWeatherVisuals()
        {
            try
            {
                _isWeatherVisualsPaused = false;
                foreach (var clock in _activeWeatherClocks)
                {
                    try { clock.Controller?.Stop(); } catch { }
                }
                _activeWeatherClocks.Clear();

                foreach (var visual in _activeWeatherVisuals)
                {
                    if (visual is FrameworkElement fe)
                    {
                        try { fe.ApplyAnimationClock(OpacityProperty, null); } catch { }
                        try { fe.ApplyAnimationClock(Canvas.LeftProperty, null); } catch { }
                        try { fe.ApplyAnimationClock(Canvas.TopProperty, null); } catch { }
                        try { fe.BeginAnimation(OpacityProperty, null); } catch { }
                        try { fe.BeginAnimation(Canvas.LeftProperty, null); } catch { }
                        try { fe.BeginAnimation(Canvas.TopProperty, null); } catch { }
                        if (fe.RenderTransform is TranslateTransform tt && !tt.IsFrozen)
                        {
                            try { tt.ApplyAnimationClock(TranslateTransform.XProperty, null); } catch { }
                            try { tt.ApplyAnimationClock(TranslateTransform.YProperty, null); } catch { }
                            try { tt.BeginAnimation(TranslateTransform.XProperty, null); } catch { }
                            try { tt.BeginAnimation(TranslateTransform.YProperty, null); } catch { }
                        }
                    }
                }
                _activeWeatherVisuals.Clear();
                WeatherCanvas?.Children.Clear();
            }
            catch { }
        }

        private void ApplySideDockWeatherSnapshot(WeatherSnapshot snapshot, WeatherSettings settings)
        {
            if (WeatherCanvas == null || !_isWeatherCardVisible) return;

            ClearSideDockWeatherVisuals();

            var isDay = ResolveDayNight(snapshot.IsDay, settings.DayNightMode);
            var preset = settings.UseLiveData
                ? MapWeatherCodeToPreset(snapshot.WeatherCode)
                : ResolveManualWeatherPreset(settings.ManualPreset, isDay);

            // Metinleri Güncelle (Canlı yazısı kaldırıldı, temiz ve estetik layout)
            WeatherIconText.Text = GetWeatherEmoji(preset, isDay);
            WeatherIconText.Foreground = ResolveWeatherIconBrush(preset, isDay);
            WeatherConditionText.Text = GetWeatherDisplayName(preset);
            WeatherTempText.Text = snapshot.TemperatureCelsius.HasValue
                ? $"{Math.Round(snapshot.TemperatureCelsius.Value):0}°"
                : "--°";
            WeatherCityText.Text = "İstanbul";

            // Detay bilgileri
            WeatherDetailText.Text = $"Bulut: %{Math.Round(snapshot.CloudCover):0}";

            WeatherSubDetailText.Text = snapshot.Precipitation > 0
                ? $"Yağış: {snapshot.Precipitation:0.0} mm"
                : (snapshot.TemperatureCelsius.HasValue ? $"Hissedilen: {Math.Round(snapshot.TemperatureCelsius.Value):0}°" : "Sakin Rüzgar");

            if (!settings.EnableAnimations) return;

            // 1. Gökyüzü Işığı & Gök Cisimleri (Güneş / Ay ve Yıldızlar)
            AddSideDockDayNightVisuals(isDay);

            // 2. Hava Koşuluna Özel Efektler
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    break;
                case WeatherVisualPreset.MostlyClear:
                    AddSideDockClouds(cloudCount: 1, baseOpacity: 0.45);
                    break;
                case WeatherVisualPreset.PartlyCloudy:
                    AddSideDockClouds(cloudCount: 2, baseOpacity: 0.65);
                    break;
                case WeatherVisualPreset.Overcast:
                    AddSideDockClouds(cloudCount: 3, baseOpacity: 0.85);
                    AddSideDockFog(dense: false);
                    break;
                case WeatherVisualPreset.Fog:
                    AddSideDockClouds(cloudCount: 2, baseOpacity: 0.70);
                    AddSideDockFog(dense: true);
                    break;
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.FreezingDrizzle:
                    AddSideDockClouds(cloudCount: 2, baseOpacity: 0.75);
                    AddSideDockRain(dropCount: 10, isHeavy: false);
                    break;
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.FreezingRain:
                    AddSideDockClouds(cloudCount: 3, baseOpacity: 0.85);
                    AddSideDockRain(dropCount: 18, isHeavy: false);
                    break;
                case WeatherVisualPreset.RainShowers:
                    AddSideDockClouds(cloudCount: 3, baseOpacity: 0.90);
                    AddSideDockRain(dropCount: 24, isHeavy: true);
                    break;
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.SnowShowers:
                    AddSideDockClouds(cloudCount: 2, baseOpacity: 0.80);
                    AddSideDockSnow(flakeCount: 14);
                    break;
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail:
                    AddSideDockClouds(cloudCount: 3, baseOpacity: 0.95);
                    AddSideDockRain(dropCount: 22, isHeavy: true);
                    AddSideDockLightning();
                    break;
            }
        }

        private static Brush ResolveWeatherIconBrush(WeatherVisualPreset preset, bool isDay)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.MostlyClear:
                    return GetCachedBrush(Color.FromRgb(255, 202, 40)); // Amber / Canlı Altın Sarısı
                case WeatherVisualPreset.PartlyCloudy:
                    return isDay
                        ? GetCachedBrush(Color.FromRgb(251, 191, 36)) // Canlı Güneş Sarısı
                        : GetCachedBrush(Color.FromRgb(186, 230, 253)); // Gece az bulutlu: Buz Mavisi
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail:
                    return GetCachedBrush(Color.FromRgb(250, 204, 21)); // Şimşek Sarısı
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.RainShowers:
                case WeatherVisualPreset.FreezingRain:
                case WeatherVisualPreset.FreezingDrizzle:
                    return GetCachedBrush(Color.FromRgb(56, 189, 248)); // Canlı Yağmur Mavisi
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.SnowShowers:
                    return GetCachedBrush(Color.FromRgb(224, 242, 254)); // Kar Beyazı / Buz Mavisi
                case WeatherVisualPreset.ClearSkyNight:
                    return GetCachedBrush(Color.FromRgb(186, 230, 253)); // Ay Işığı Açık Mavi
                case WeatherVisualPreset.Fog:
                case WeatherVisualPreset.Overcast:
                default:
                    return GetCachedBrush(Color.FromRgb(203, 213, 225)); // Parlak Gümüş / Sis Gri
            }
        }

        private void AddSideDockDayNightVisuals(bool isDay)
        {
            if (isDay)
            {
                // Güneş Halesi
                var sunGlow = new Ellipse
                {
                    Width = 46,
                    Height = 46,
                    Fill = GetCachedBrush(Color.FromArgb(40, 255, 185, 0)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(sunGlow, 175);
                Canvas.SetTop(sunGlow, 8);
                WeatherCanvas.Children.Add(sunGlow);
                _activeWeatherVisuals.Add(sunGlow);

                var glowAnim = new DoubleAnimation(0.25, 0.60, TimeSpan.FromSeconds(2.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                StartManagedWeatherAnimation(sunGlow, OpacityProperty, glowAnim);

                // Güneş Küresi
                var sun = new Ellipse
                {
                    Width = 26,
                    Height = 26,
                    Fill = new RadialGradientBrush(Color.FromRgb(255, 245, 160), Color.FromRgb(245, 175, 25)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(sun, 185);
                Canvas.SetTop(sun, 18);
                WeatherCanvas.Children.Add(sun);
                _activeWeatherVisuals.Add(sun);

                var floatTransform = new TranslateTransform();
                sun.RenderTransform = floatTransform;
                var sunAnim = new DoubleAnimation(-2.5, 2.5, TimeSpan.FromSeconds(3.5))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                StartManagedWeatherAnimation(floatTransform, TranslateTransform.YProperty, sunAnim);
            }
            else
            {
                // Ay Küresi
                var moon = new Ellipse
                {
                    Width = 22,
                    Height = 22,
                    Fill = new RadialGradientBrush(Color.FromRgb(240, 245, 255), Color.FromRgb(180, 195, 220)),
                    Opacity = 0.90,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(moon, 185);
                Canvas.SetTop(moon, 18);
                WeatherCanvas.Children.Add(moon);
                _activeWeatherVisuals.Add(moon);

                // Yıldızlar
                for (int i = 0; i < 6; i++)
                {
                    var size = 1.8 + (_weatherRnd.NextDouble() * 1.6);
                    var star = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = GetCachedBrush(Color.FromArgb(235, 240, 248, 255)),
                        Opacity = 0.2 + (_weatherRnd.NextDouble() * 0.6),
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(star, 135 + (_weatherRnd.NextDouble() * 85));
                    Canvas.SetTop(star, 8 + (_weatherRnd.NextDouble() * 42));
                    WeatherCanvas.Children.Add(star);
                    _activeWeatherVisuals.Add(star);

                    var twinkle = new DoubleAnimation(0.15, 0.95, TimeSpan.FromMilliseconds(1000 + _weatherRnd.Next(1500)))
                    {
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    StartManagedWeatherAnimation(star, OpacityProperty, twinkle);
                }
            }
        }

        private void AddSideDockClouds(int cloudCount, double baseOpacity)
        {
            var sprite = TryGetCloudSprite();

            for (int i = 0; i < cloudCount; i++)
            {
                var width = 75.0 + (_weatherRnd.NextDouble() * 32.0);
                var height = width * 0.58;

                FrameworkElement cloud;
                if (sprite != null)
                {
                    cloud = new Image
                    {
                        Source = sprite,
                        Width = width,
                        Height = height,
                        Stretch = Stretch.Uniform,
                        Opacity = Math.Min(0.95, baseOpacity + (i * 0.08)),
                        IsHitTestVisible = false
                    };
                }
                else
                {
                    cloud = CreateProceduralCloud(width, height, baseOpacity);
                }

                var startY = 8.0 + (i * 12.0) + (_weatherRnd.NextDouble() * 6.0);
                Canvas.SetTop(cloud, startY);

                var translate = new TranslateTransform();
                cloud.RenderTransform = translate;
                WeatherCanvas.Children.Add(cloud);
                _activeWeatherVisuals.Add(cloud);

                var durationSeconds = 12.0 + (i * 4.0) + (_weatherRnd.NextDouble() * 3.0);
                var driftAnim = new DoubleAnimation(-width, WeatherCardWidth + 10, TimeSpan.FromSeconds(durationSeconds))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var initialX = (i * 80.0) % (WeatherCardWidth + width) - width;
                translate.X = initialX;

                StartManagedWeatherAnimation(translate, TranslateTransform.XProperty, driftAnim);
            }
        }

        private static Border CreateProceduralCloud(double width, double height, double opacity)
        {
            return new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(height / 2.0),
                Background = GetCachedBrush(Color.FromArgb((byte)(opacity * 200), 235, 242, 252)),
                Opacity = opacity,
                IsHitTestVisible = false
            };
        }

        private void AddSideDockRain(int dropCount, bool isHeavy)
        {
            for (int i = 0; i < dropCount; i++)
            {
                var dropLength = isHeavy ? 10.0 : 7.0;
                var drop = new Border
                {
                    Width = 1.3,
                    Height = dropLength,
                    CornerRadius = new CornerRadius(0.65),
                    Background = GetCachedBrush(Color.FromArgb(170, 140, 215, 255)),
                    Opacity = 0.70,
                    IsHitTestVisible = false
                };

                var startX = _weatherRnd.NextDouble() * WeatherCardWidth;
                Canvas.SetLeft(drop, startX);

                var translate = new TranslateTransform();
                drop.RenderTransform = translate;
                WeatherCanvas.Children.Add(drop);
                _activeWeatherVisuals.Add(drop);

                var durationMs = isHeavy ? 450 + _weatherRnd.Next(250) : 650 + _weatherRnd.Next(350);
                var fallAnim = new DoubleAnimation(-15, 135, TimeSpan.FromMilliseconds(durationMs))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };

                StartManagedWeatherAnimation(translate, TranslateTransform.YProperty, fallAnim);
            }
        }

        private void AddSideDockSnow(int flakeCount)
        {
            for (int i = 0; i < flakeCount; i++)
            {
                var size = 2.4 + (_weatherRnd.NextDouble() * 2.8);
                var flake = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = GetCachedBrush(Color.FromArgb(220, 245, 250, 255)),
                    Opacity = 0.80,
                    IsHitTestVisible = false
                };

                var startX = _weatherRnd.NextDouble() * WeatherCardWidth;
                Canvas.SetLeft(flake, startX);

                var translate = new TranslateTransform();
                flake.RenderTransform = translate;
                WeatherCanvas.Children.Add(flake);
                _activeWeatherVisuals.Add(flake);

                var durationMs = 2400 + _weatherRnd.Next(1800);
                var fallAnim = new DoubleAnimation(-10, 130, TimeSpan.FromMilliseconds(durationMs))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                StartManagedWeatherAnimation(translate, TranslateTransform.YProperty, fallAnim);

                var swayAnim = new DoubleAnimation(-4, 4, TimeSpan.FromMilliseconds(1200 + _weatherRnd.Next(800)))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                StartManagedWeatherAnimation(translate, TranslateTransform.XProperty, swayAnim);
            }
        }

        private void AddSideDockLightning()
        {
            var flash = new Border
            {
                Width = WeatherCardWidth,
                Height = 125,
                CornerRadius = new CornerRadius(15, 0, 15, 0),
                Background = GetCachedBrush(Color.FromArgb(180, 225, 240, 255)),
                Opacity = 0.0,
                IsHitTestVisible = false
            };
            WeatherCanvas.Children.Add(flash);
            _activeWeatherVisuals.Add(flash);

            var flashAnim = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            flashAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            flashAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.5))));
            flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.55))));
            flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.68))));
            flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.65, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.74))));
            flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.90))));
            flashAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(6.0))));

            StartManagedWeatherAnimation(flash, OpacityProperty, flashAnim);
        }

        private void AddSideDockFog(bool dense)
        {
            for (int i = 0; i < (dense ? 3 : 2); i++)
            {
                var band = new Border
                {
                    Width = WeatherCardWidth * 0.9,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = GetCachedBrush(Color.FromArgb((byte)(dense ? 70 : 45), 230, 238, 248)),
                    Opacity = 0.55,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(band, 8);
                Canvas.SetTop(band, 30 + (i * 26));

                var translate = new TranslateTransform();
                band.RenderTransform = translate;
                WeatherCanvas.Children.Add(band);
                _activeWeatherVisuals.Add(band);

                var fogAnim = new DoubleAnimation(-12, 12, TimeSpan.FromSeconds(4.0 + (i * 1.5)))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                StartManagedWeatherAnimation(translate, TranslateTransform.XProperty, fogAnim);
            }
        }

        private static ImageSource? TryGetCloudSprite()
        {
            if (_cloudSpriteChecked) return _cachedCloudSprite;
            _cloudSpriteChecked = true;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri("pack://application:,,,/weather_img/cloud_1.png", UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                if (bmp.CanFreeze) bmp.Freeze();
                _cachedCloudSprite = bmp;
            }
            catch
            {
                _cachedCloudSprite = null;
            }
            return _cachedCloudSprite;
        }

        private static bool ResolveDayNight(bool snapshotIsDay, string dayNightMode)
        {
            switch (dayNightMode)
            {
                case WeatherSettingsService.DayNightDay:
                    return true;
                case WeatherSettingsService.DayNightNight:
                    return false;
                default:
                    return snapshotIsDay;
            }
        }

        private static WeatherVisualPreset ResolveManualWeatherPreset(string? manualKey, bool isDay)
        {
            var key = WeatherSettingsService.ResolveWeatherPresetKey(manualKey);
            switch (key)
            {
                case "ClearSky":
                    return isDay ? WeatherVisualPreset.ClearSkyDay : WeatherVisualPreset.ClearSkyNight;
                case "MostlyClear": return WeatherVisualPreset.MostlyClear;
                case "PartlyCloudy": return WeatherVisualPreset.PartlyCloudy;
                case "Overcast": return WeatherVisualPreset.Overcast;
                case "Fog": return WeatherVisualPreset.Fog;
                case "Drizzle": return WeatherVisualPreset.Drizzle;
                case "FreezingDrizzle": return WeatherVisualPreset.FreezingDrizzle;
                case "Rain": return WeatherVisualPreset.Rain;
                case "FreezingRain": return WeatherVisualPreset.FreezingRain;
                case "Snow": return WeatherVisualPreset.Snow;
                case "SnowGrains": return WeatherVisualPreset.SnowGrains;
                case "RainShowers": return WeatherVisualPreset.RainShowers;
                case "SnowShowers": return WeatherVisualPreset.SnowShowers;
                case "Thunderstorm": return WeatherVisualPreset.Thunderstorm;
                case "ThunderstormHail": return WeatherVisualPreset.ThunderstormHail;
                default: return WeatherVisualPreset.PartlyCloudy;
            }
        }

        private static WeatherSnapshot CreateManualWeatherSnapshot(WeatherSettings settings)
        {
            var isDay = ResolveDayNight(DateTime.Now.Hour >= 6 && DateTime.Now.Hour < 19, settings.DayNightMode);
            return new WeatherSnapshot
            {
                WeatherCode = 2,
                IsDay = isDay,
                TemperatureCelsius = isDay ? 22.0 : 16.0,
                CloudCover = 45.0,
                Precipitation = 0.0,
                RetrievedAtUtc = DateTime.UtcNow
            };
        }

        private static string GetWeatherEmoji(WeatherVisualPreset preset, bool isDay)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay: return "☀️";
                case WeatherVisualPreset.ClearSkyNight: return "🌙";
                case WeatherVisualPreset.MostlyClear: return isDay ? "🌤️" : "🌙";
                case WeatherVisualPreset.PartlyCloudy: return isDay ? "⛅" : "☁️";
                case WeatherVisualPreset.Overcast: return "☁️";
                case WeatherVisualPreset.Fog: return "🌫️";
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.FreezingDrizzle: return "🌦️";
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.FreezingRain: return "🌧️";
                case WeatherVisualPreset.RainShowers: return "⛈️";
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.SnowShowers: return "❄️";
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail: return "⚡";
                default: return isDay ? "⛅" : "☁️";
            }
        }

        private static WeatherVisualPreset MapWeatherCodeToPreset(int weatherCode)
        {
            switch (weatherCode)
            {
                case 0: return WeatherVisualPreset.ClearSkyDay;
                case 1: return WeatherVisualPreset.MostlyClear;
                case 2: return WeatherVisualPreset.PartlyCloudy;
                case 3: return WeatherVisualPreset.Overcast;
                case 45:
                case 48: return WeatherVisualPreset.Fog;
                case 51:
                case 53:
                case 55: return WeatherVisualPreset.Drizzle;
                case 56:
                case 57: return WeatherVisualPreset.FreezingDrizzle;
                case 61:
                case 63:
                case 65: return WeatherVisualPreset.Rain;
                case 66:
                case 67: return WeatherVisualPreset.FreezingRain;
                case 71:
                case 73:
                case 75: return WeatherVisualPreset.Snow;
                case 77: return WeatherVisualPreset.SnowGrains;
                case 80:
                case 81:
                case 82: return WeatherVisualPreset.RainShowers;
                case 85:
                case 86: return WeatherVisualPreset.SnowShowers;
                case 95: return WeatherVisualPreset.Thunderstorm;
                case 96:
                case 99: return WeatherVisualPreset.ThunderstormHail;
                default: return WeatherVisualPreset.PartlyCloudy;
            }
        }

        private static string GetWeatherDisplayName(WeatherVisualPreset preset)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    return "Açık Hava";
                case WeatherVisualPreset.MostlyClear:
                    return "Az Bulutlu";
                case WeatherVisualPreset.PartlyCloudy:
                    return "Parçalı Bulutlu";
                case WeatherVisualPreset.Overcast:
                    return "Çok Bulutlu";
                case WeatherVisualPreset.Fog:
                    return "Sisli";
                case WeatherVisualPreset.Drizzle:
                    return "Çisenti";
                case WeatherVisualPreset.FreezingDrizzle:
                    return "Donan Çisenti";
                case WeatherVisualPreset.Rain:
                    return "Yağmurlu";
                case WeatherVisualPreset.FreezingRain:
                    return "Donan Yağmur";
                case WeatherVisualPreset.Snow:
                    return "Karlı";
                case WeatherVisualPreset.SnowGrains:
                    return "İnce Kar";
                case WeatherVisualPreset.RainShowers:
                    return "Sağanak Yağmur";
                case WeatherVisualPreset.SnowShowers:
                    return "Kar Sağanağı";
                case WeatherVisualPreset.Thunderstorm:
                    return "Fırtına";
                case WeatherVisualPreset.ThunderstormHail:
                    return "Dolu Fırtınası";
                default:
                    return "Parçalı Bulutlu";
            }
        }

        private static WeatherSnapshot CreateFallbackWeatherSnapshot()
        {
            var now = DateTime.Now;
            var isDay = now.Hour >= 6 && now.Hour < 19;
            return new WeatherSnapshot
            {
                WeatherCode = isDay ? 1 : 0,
                IsDay = isDay,
                TemperatureCelsius = isDay ? 22.0 : 16.0,
                CloudCover = 25.0,
                Precipitation = 0.0,
                RetrievedAtUtc = DateTime.UtcNow
            };
        }
    }
}
