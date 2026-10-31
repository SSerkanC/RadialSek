using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class SideDockWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExToolWindow = 0x00000080;
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int HtClient = 1;
        private const double CollapsedWidth = 24.0;
        private const double NormalDrawerWidth = 270.0;
        private const double CompactDrawerWidth = 74.0;
        private const double TabsBarWidth = 28.0;
        private const string DragDropItemFormat = "RadialSek_SideDock_Item";

        private double GetCurrentDrawerWidth()
        {
            var isCompact = _config?.Tools?.SideMenu?.IsCompact ?? false;
            return isCompact ? CompactDrawerWidth : NormalDrawerWidth;
        }

        private double GetCurrentExpandedWidth()
        {
            if (_isWeatherCardVisible)
            {
                return GetCurrentDrawerWidth() + WeatherCardTotalWidth;
            }
            return GetCurrentDrawerWidth() + TabsBarWidth;
        }

        private double GetTargetExpandedWidth()
        {
            var showWeather = _config?.Tools?.SideMenu?.ShowWeatherCard ?? true;
            if (showWeather)
            {
                return GetCurrentDrawerWidth() + WeatherCardTotalWidth;
            }
            return GetCurrentDrawerWidth() + TabsBarWidth;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint ShgfiIcon = 0x00000100;
        private const uint ShgfiLargeIcon = 0x00000000;
        private const uint ShgfiSysiconindex = 0x00004000;
        private const uint IldNormal = 0x00000000;

        private static readonly Guid IidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        private const int ShilLarge = 0;      // 32x32
        private const int ShilSmall = 1;      // 16x16
        private const int ShilExtraLarge = 2; // 48x48
        private const int ShilJumbo = 4;      // 256x256

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoActivate = 0x0010;

        private readonly double _screenRight;
        private readonly double _screenTop;
        private readonly double _screenHeight;
        private readonly LauncherService _launcherService;
        private readonly MenuConfigService _configService;
        private MenuConfig _config;
        private bool _isExpanded;
        private bool _isTransitioning;
        private bool _isChildDialogOpen;
        private MenuItemConfig? _draggedItem;
        private MenuItemConfig? _draggedItemParentFolder;
        private Point _dragStartPoint;
        private Point _folderDragStartPoint;
        private Border? _activeContextMenu;
        private Popup? _activeContextMenuPopup;
        private Border? _firstItemTopIndicator;
        private readonly HashSet<MenuItemConfig> _expandedFolders = new();
        private readonly DispatcherTimer _autoCollapseTimer;
        private readonly DispatcherTimer _openDelayTimer = new();
        private DateTime? _cursorLeftMenuTime;
        private bool _isInternalDragging;
        private bool _internalDropHandled;
        private Point _lastKnownCursorScreenPos;
        private DateTime _lastInteractionTime = DateTime.UtcNow;

        // Ultra-low-memory cached brushes, icons & frozen effects
        private static readonly Dictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Windows.Media.Effects.DropShadowEffect SharedContextMenuShadow = new()
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Direction = 270,
            Color = Colors.Black,
            Opacity = 0.55
        };

        static SideDockWindow()
        {
            SharedContextMenuShadow.Freeze();
        }

        public SideDockWindow(double screenRight, double screenTop, double screenHeight, LauncherService launcherService, MenuConfigService configService, MenuConfig config)
        {
            InitializeComponent();
            _screenRight = screenRight;
            _screenTop = screenTop;
            _screenHeight = screenHeight;
            _launcherService = launcherService;
            _configService = configService;
            _config = config;

            _openDelayTimer.Interval = TimeSpan.FromMilliseconds(200);
            _openDelayTimer.Tick += OnOpenDelayTimerTick;

            _autoCollapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _autoCollapseTimer.Tick += (s, e) =>
            {
                if (!_isExpanded || _isChildDialogOpen || _isInternalDragging || _activeContextMenu != null || _activeContextMenuPopup != null)
                {
                    _cursorLeftMenuTime = null;
                    return;
                }

                if (GetCursorPos(out var pt))
                {
                    // Akıllı Uyku Modu: İmleç hareketini izle
                    if (Math.Abs(pt.X - _lastKnownCursorScreenPos.X) > 1 || Math.Abs(pt.Y - _lastKnownCursorScreenPos.Y) > 1)
                    {
                        _lastKnownCursorScreenPos = new Point(pt.X, pt.Y);
                        _lastInteractionTime = DateTime.UtcNow;
                        ResumeSideDockWeatherVisuals();
                    }
                    else if ((DateTime.UtcNow - _lastInteractionTime).TotalSeconds >= 6.0)
                    {
                        // 6 saniyedir menü üzerinde fare tamamen hareketsizse animasyonları dondur (%0 CPU)
                        PauseSideDockWeatherVisuals();
                    }
                }

                if (!IsCursorOverMenuElements())
                {
                    if (_cursorLeftMenuTime == null)
                    {
                        _cursorLeftMenuTime = DateTime.UtcNow;
                    }
                    else if ((DateTime.UtcNow - _cursorLeftMenuTime.Value).TotalMilliseconds >= 500)
                    {
                        _cursorLeftMenuTime = null;
                        _autoCollapseTimer.Stop();
                        CollapseMenu();
                    }
                }
                else
                {
                    _cursorLeftMenuTime = null;
                }
            };

            RepositionToScreenEdge();
            ApplySettings();
            RenderTabs();
            RenderItems();
            InitializeWeatherComponents();

            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var exStyle = GetWindowLong(hwnd, GwlExStyle);
                SetWindowLong(hwnd, GwlExStyle, exStyle | WsExToolWindow);
                SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmNcHitTest)
            {
                // lParam: LOWORD = X (ekran pikseli), HIWORD = Y (ekran pikseli)
                var x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
                var y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));

                try
                {
                    var localPt = PointFromScreen(new Point(x, y));

                    if (!_isExpanded)
                    {
                        // Menü kapalıyken: Sadece ekranın en sağ kenarındaki 24px tetikleyici alan fareyi algılar
                        if (localPt.X >= ActualWidth - CollapsedWidth && localPt.X <= ActualWidth &&
                            localPt.Y >= 0 && localPt.Y <= ActualHeight)
                        {
                            handled = true;
                            return new IntPtr(HtClient);
                        }

                        // Menünün solundaki tüm boş alanlar tamamen şeffaftır; tıklamalar ve hover doğrudan masaüstüne geçer!
                        handled = true;
                        return new IntPtr(HtTransparent);
                    }

                    if (IsLocalPointOverInteractiveMenu(localPt))
                    {
                        handled = true;
                        return new IntPtr(HtClient);
                    }

                    // Menü açıkken menü ögeleri dışındaki şeffaf boşluklarda tıklamalar doğrudan arkadaki masaüstüne geçer
                    handled = true;
                    return new IntPtr(HtTransparent);
                }
                catch
                {
                    return IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        public void RepositionToScreenEdge()
        {
            Top = _screenTop;
            Height = _screenHeight;

            var drawerWidth = GetCurrentDrawerWidth();
            var targetWidth = GetTargetExpandedWidth();
            Left = _screenRight - targetWidth;
            Width = targetWidth;
            DrawerPanel.Width = drawerWidth;
        }

        public void RefreshConfig(MenuConfig config)
        {
            _config = config;
            ApplySettings();
            RenderTabs();
            RenderItems();
        }

        private void ApplySettings()
        {
            EnsureTabsInitialized();

            var showLine = _config.Tools?.SideMenu?.ShowIndicatorLine ?? true;
            VisualLine.Visibility = showLine ? Visibility.Visible : Visibility.Collapsed;

            var isCompact = _config.Tools?.SideMenu?.IsCompact ?? false;
            SideDockTitleText.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
            TitleStack.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;

            if (isCompact)
            {
                DrawerInnerGrid.Margin = new Thickness(6, 14, 6, 12);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Center;
                SettingsButton.Width = 32;
                SettingsButton.Height = 32;
                EmptyStateTitle.Visibility = Visibility.Collapsed;
                EmptyStateHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                DrawerInnerGrid.Margin = new Thickness(12, 14, 12, 12);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Center;
                SettingsButton.Width = 28;
                SettingsButton.Height = 28;
                EmptyStateTitle.Visibility = Visibility.Visible;
                EmptyStateHint.Visibility = Visibility.Visible;
            }

            var drawerWidth = GetCurrentDrawerWidth();
            var targetWidth = GetTargetExpandedWidth();
            Left = _screenRight - targetWidth;
            Width = targetWidth;
            DrawerPanel.Width = drawerWidth;
        }

        private void OnTriggerZoneMouseEnter(object sender, MouseEventArgs e)
        {
            if (_isExpanded || _isTransitioning) return;
            // Kullanıcı isteği: 0.2 saniye trigger üzerinde tutulursa açılsın
            _openDelayTimer.Stop();
            _openDelayTimer.Start();
        }

        private void OnTriggerZoneMouseLeave(object sender, MouseEventArgs e)
        {
            _openDelayTimer.Stop();
        }

        private void OnOpenDelayTimerTick(object? sender, EventArgs e)
        {
            _openDelayTimer.Stop();
            if (_isExpanded || _isTransitioning) return;

            if (IsCursorOverTriggerZone())
            {
                ExpandMenu();
            }
        }

        private bool IsCursorOverTriggerZone()
        {
            if (!GetCursorPos(out var pt)) return false;
            try
            {
                var localPt = PointFromScreen(new Point(pt.X, pt.Y));
                return localPt.X >= ActualWidth - CollapsedWidth && localPt.X <= ActualWidth &&
                       localPt.Y >= 0 && localPt.Y <= ActualHeight;
            }
            catch
            {
                return false;
            }
        }

        private void OnTriggerZoneDragEnter(object sender, DragEventArgs e)
        {
            if (_isExpanded || _isTransitioning) return;
            _openDelayTimer.Stop();
            _openDelayTimer.Start();
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnTriggerZoneDragOver(object sender, DragEventArgs e)
        {
            if (!_isExpanded && !_isTransitioning)
            {
                if (!_openDelayTimer.IsEnabled)
                {
                    _openDelayTimer.Start();
                }
            }
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnTriggerZoneDragLeave(object sender, DragEventArgs e)
        {
            _openDelayTimer.Stop();
        }

        private void OnTriggerZoneDrop(object sender, DragEventArgs e)
        {
            _openDelayTimer.Stop();
            if (!_isExpanded)
            {
                ExpandMenu();
            }
            OnDrawerDrop(sender, e);
        }

        private void OnTriggerZonePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isExpanded || _isTransitioning) return;
            e.Handled = true;
            _openDelayTimer.Stop();
            ExpandMenu();
        }

        private void OnDrawerPanelMouseEnter(object sender, MouseEventArgs e)
        {
            _cursorLeftMenuTime = null;
        }

        private void OnDrawerPanelMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isExpanded) return;
            if (!IsCursorOverMenuElements())
            {
                _cursorLeftMenuTime ??= DateTime.UtcNow;
            }
        }

        private void OnWeatherCardMouseEnter(object sender, MouseEventArgs e)
        {
            _cursorLeftMenuTime = null;
        }

        private void OnWeatherCardMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isExpanded) return;
            if (!IsCursorOverMenuElements())
            {
                _cursorLeftMenuTime ??= DateTime.UtcNow;
            }
        }

        private bool IsCursorOverMenuElements()
        {
            if (!GetCursorPos(out var pt))
                return false;

            try
            {
                var localPt = PointFromScreen(new Point(pt.X, pt.Y));
                return IsLocalPointOverInteractiveMenu(localPt);
            }
            catch
            {
                return false;
            }
        }

        private bool IsLocalPointOverInteractiveMenu(Point localPt)
        {
            if (!_isExpanded)
            {
                return localPt.X >= ActualWidth - CollapsedWidth && localPt.X <= ActualWidth &&
                       localPt.Y >= 0 && localPt.Y <= ActualHeight;
            }

            // Pencere sınırlarının dışına çıkılmışsa
            if (localPt.X < 0 || localPt.X > ActualWidth || localPt.Y < 0 || localPt.Y > ActualHeight)
            {
                return false;
            }

            var drawerWidth = GetCurrentDrawerWidth();

            // 1. Ana Yan Menü Paneli (DrawerPanel): En sağdaki drawerWidth piksellik alan
            if (localPt.X >= ActualWidth - drawerWidth - 2)
            {
                return true;
            }

            // 2. Sekmeler Şeridi (TabsBar): DrawerPanel'in sol üstündeki sekme şeridi
            if (localPt.X >= ActualWidth - drawerWidth - TabsBarWidth - 6 && localPt.Y <= 450)
            {
                return true;
            }

            // 3. Hava Durumu Kartı (WeatherCard) & Şeritler Alanı: Sol alt ve bağlantı bölgesi
            if (_isWeatherCardVisible && WeatherCard != null && WeatherCard.Visibility == Visibility.Visible)
            {
                var weatherTop = ActualHeight - 145;
                if (localPt.X >= ActualWidth - drawerWidth - WeatherCardTotalWidth - 8 && localPt.Y >= weatherTop)
                {
                    return true;
                }

                // Şeritlerin geçtiği bölge (kartın üstünden yan menüye bağlanan şerit alanı)
                if (WeatherRibbonsOverCanvas != null && WeatherRibbonsOverCanvas.Visibility == Visibility.Visible)
                {
                    var ribbonsTop = ActualHeight - 380;
                    if (localPt.X >= ActualWidth - drawerWidth - WeatherCardTotalWidth - 8 && localPt.Y >= ribbonsTop)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnDrawerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ItemsScrollViewer != null)
            {
                ItemsScrollViewer.ScrollToVerticalOffset(ItemsScrollViewer.VerticalOffset - (e.Delta * 0.5));
                e.Handled = true;
            }
        }

        private void OnItemsScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.5));
                e.Handled = true;
            }
        }

        private void OnAddTabButtonMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border b)
            {
                b.Width = 24;
                b.Background = GetCachedBrush(Color.FromRgb(190, 196, 205));
            }
        }

        private void OnAddTabButtonMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border b)
            {
                b.Width = 20;
                b.Background = GetCachedBrush(Color.FromRgb(164, 171, 182));
            }
        }

        private void OnAddTabButtonClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu == null) return;

            EnsureTabsInitialized();
            var newIndex = sideMenu.Tabs.Count + 1;
            var newTab = new SideMenuTabConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"Sekme {newIndex}",
                Items = new List<MenuItemConfig>()
            };
            sideMenu.Tabs.Add(newTab);
            sideMenu.ActiveTabId = newTab.Id;
            sideMenu.Items = newTab.Items;
            SaveAndRefresh();
        }


        public void ExpandMenu()
        {
            if (_isExpanded || _isTransitioning) return;
            _openDelayTimer.Stop();
            _autoCollapseTimer?.Stop();
            _cursorLeftMenuTime = null;
            _isTransitioning = true;
            _isExpanded = true;

            EnsureTabsInitialized();
            RenderTabs();
            RenderItems();
            PrepareWeatherCardForExpand();

            var drawerWidth = GetCurrentDrawerWidth();
            var drawerSlideDistance = drawerWidth + TabsBarWidth;

            DrawerPanel.Width = drawerWidth;
            TriggerZone.Visibility = Visibility.Collapsed;
            ExpandedContainer.Visibility = Visibility.Visible;

            _lastInteractionTime = DateTime.UtcNow;
            ResumeSideDockWeatherVisuals();
            _autoCollapseTimer?.Start();

            var slideAnim = new DoubleAnimation(drawerSlideDistance, 0.0, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            slideAnim.Completed += (_, __) =>
            {
                _isTransitioning = false;
                try
                {
                    Activate();
                    Focus();
                }
                catch { }
            };

            DrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
        }

        public void CollapseMenu()
        {
            if (!_isExpanded) return;
            _openDelayTimer.Stop();
            _autoCollapseTimer?.Stop();
            _cursorLeftMenuTime = null;
            _isTransitioning = true;

            try
            {
                CloseActiveContextMenu();
            }
            catch { }

            try
            {
                HideWeatherCardAndStopVisuals();
            }
            catch { }

            var drawerWidth = GetCurrentDrawerWidth();
            var drawerSlideDistance = drawerWidth + TabsBarWidth;

            void FinishCollapse()
            {
                _isExpanded = false;
                _isTransitioning = false;
                ExpandedContainer.Visibility = Visibility.Collapsed;
                TriggerZone.Visibility = Visibility.Visible;
            }

            var slideAnim = new DoubleAnimation(0.0, drawerSlideDistance, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideAnim.Completed += (_, __) =>
            {
                FinishCollapse();
            };

            try
            {
                DrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
            catch
            {
                FinishCollapse();
            }
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            if (_isChildDialogOpen || _isInternalDragging) return;
            CloseActiveContextMenu();
            if (_isExpanded)
            {
                CollapseMenu();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_activeContextMenu != null)
                {
                    CloseActiveContextMenu();
                    e.Handled = true;
                    return;
                }
                if (_isExpanded)
                {
                    CollapseMenu();
                    e.Handled = true;
                }
            }
        }

        private void OnSettingsButtonClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_activeContextMenuPopup != null && _activeContextMenuPopup.IsOpen)
            {
                CloseActiveContextMenu();
                return;
            }

            ShowSettingsMenu();
        }

        private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeContextMenu != null)
            {
                var pos = e.GetPosition(_activeContextMenu);
                if (pos.X < 0 || pos.X > _activeContextMenu.ActualWidth || pos.Y < 0 || pos.Y > _activeContextMenu.ActualHeight)
                {
                    CloseActiveContextMenu();
                }
            }

            // Şeffaf alana tıklanırsa derhal menüyü kapat
            var winPos = e.GetPosition(this);
            if (_isExpanded && !IsLocalPointOverInteractiveMenu(winPos))
            {
                CollapseMenu();
                return;
            }

            // Fare tekerleği tıklaması (Middle Click) ile sonraki sekmeye geçiş
            if (e.MiddleButton == MouseButtonState.Pressed && _isExpanded && !_isTransitioning)
            {
                e.Handled = true;
                CycleToNextTab();
            }
        }

        private void CycleToNextTab()
        {
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu?.Tabs == null || sideMenu.Tabs.Count <= 1) return;

            var currentIndex = sideMenu.Tabs.FindIndex(t => t.Id == sideMenu.ActiveTabId);
            if (currentIndex < 0) currentIndex = 0;

            var nextIndex = (currentIndex + 1) % sideMenu.Tabs.Count;
            SwitchToTab(sideMenu.Tabs[nextIndex]);
        }

        private void OnButtonHoverEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = GetCachedBrush(Color.FromArgb(45, 255, 255, 255));
            }
        }

        private void OnButtonHoverLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = GetCachedBrush(Color.FromArgb(26, 255, 255, 255));
            }
        }

        private void OnAddAppButtonHoverEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = GetCachedBrush(Color.FromArgb(45, 255, 255, 255));
                border.BorderBrush = GetCachedBrush(Color.FromArgb(120, 255, 170, 0));
            }
        }

        private void OnAddAppButtonHoverLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = GetCachedBrush(Color.FromArgb(30, 255, 255, 255));
                border.BorderBrush = GetCachedBrush(Color.FromArgb(53, 255, 255, 255));
            }
        }

        private void OnAddAppButtonClicked(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Uygulama veya Kısayol Seçin",
                Filter = "Uygulamalar ve Kısayollar (*.exe;*.lnk;*.url)|*.exe;*.lnk;*.url|Tüm Dosyalar (*.*)|*.*"
            };

            _isChildDialogOpen = true;
            try
            {
                if (dialog.ShowDialog(this) == true)
                {
                    AddApplication(dialog.FileName);
                }
            }
            finally
            {
                _isChildDialogOpen = false;
            }
        }

        // ==========================================
        // SÜRÜKLE & BIRAK (DOSYA SİSTEMİNDEN VE İÇERİDEN)
        // ==========================================

        private void OnDrawerDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DragDropItemFormat))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void OnDrawerDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        AddApplication(file);
                    }
                }
                e.Handled = true;
                return;
            }

            // İçeriden sürüklenen bir öğe ana menünün boşluğuna bırakıldıysa (klasör dışına çıkarıldıysa)
            if (e.Data.GetDataPresent(DragDropItemFormat) && _draggedItem != null)
            {
                _internalDropHandled = true;
                if (_draggedItemParentFolder != null)
                {
                    MoveItemOutOfFolder(_draggedItem, _draggedItemParentFolder);
                }
                e.Handled = true;
            }
        }

        private void OnTopHeaderDragOver(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DragDropItemFormat) && _draggedItem != null) ||
                e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = e.Data.GetDataPresent(DragDropItemFormat) ? DragDropEffects.Move : DragDropEffects.Copy;
                e.Handled = true;
                ShowFirstItemTopIndicator(true);
            }
        }

        private void OnTopHeaderDragLeave(object sender, DragEventArgs e)
        {
            ShowFirstItemTopIndicator(false);
        }

        private void OnTopHeaderDrop(object sender, DragEventArgs e)
        {
            ShowFirstItemTopIndicator(false);
            if (_draggedItem != null)
            {
                _internalDropHandled = true;
                ReorderItemToTop(_draggedItem, _draggedItemParentFolder);
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _internalDropHandled = true;
                    foreach (var file in files)
                    {
                        InsertApplicationNearTop(file);
                    }
                    e.Handled = true;
                }
            }
        }

        private void ShowFirstItemTopIndicator(bool show)
        {
            if (_firstItemTopIndicator != null)
            {
                _firstItemTopIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ReorderItemToTop(MenuItemConfig draggedItem, MenuItemConfig? sourceFolder)
        {
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu == null) return;
            var rootItems = sideMenu.Items;
            if (rootItems == null) return;

            if (sourceFolder != null)
            {
                sourceFolder.Children.Remove(draggedItem);
                draggedItem.FixedColor = string.Empty;
            }
            else
            {
                rootItems.Remove(draggedItem);
            }

            rootItems.Insert(0, draggedItem);
            SaveAndRefresh();
        }

        private void InsertApplicationNearTop(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var cleanPath = path.Trim();
            var label = Path.GetFileNameWithoutExtension(cleanPath);
            if (string.IsNullOrWhiteSpace(label)) label = cleanPath;

            var item = new MenuItemConfig
            {
                Label = label,
                TargetPath = cleanPath
            };

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;
            rootItems.Insert(0, item);
            SaveAndRefresh();
        }

        public void AddApplication(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var cleanPath = path.Trim();

            _config.Tools ??= new ToolsSettings();
            _config.Tools.SideMenu ??= new SideMenuToolSettings();
            _config.Tools.SideMenu.Items ??= new List<MenuItemConfig>();

            var label = Path.GetFileNameWithoutExtension(cleanPath);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = cleanPath;
            }

            var item = new MenuItemConfig
            {
                Label = label,
                TargetPath = cleanPath
            };

            _config.Tools.SideMenu.Items.Add(item);
            SaveAndRefresh();
        }


        public void AddApplicationToFolder(string path, MenuItemConfig folder)
        {
            if (string.IsNullOrWhiteSpace(path) || folder == null) return;
            var cleanPath = path.Trim();
            var label = Path.GetFileNameWithoutExtension(cleanPath);
            if (string.IsNullOrWhiteSpace(label)) label = cleanPath;

            var item = new MenuItemConfig
            {
                Label = label,
                TargetPath = cleanPath
            };

            folder.Children ??= new List<MenuItemConfig>();
            folder.Children.Add(item);
            _expandedFolders.Add(folder);
            SaveAndRefresh();
        }


        private void SaveAndRefresh()
        {
            try
            {
                _configService.SaveConfig(_config);
            }
            catch { }
            SideDockService.Instance.RefreshConfig(_config);
        }

        // ==========================================
        // GÖRSEL LİSTE OLUŞTURMA & KLASÖR MANTIĞI
        // ==========================================

        private void RenderItems()
        {
            ItemsContainer.Children.Clear();
            _firstItemTopIndicator = null;
            var items = _config.Tools?.SideMenu?.Items;
            if (items == null || items.Count == 0)
            {
                EmptyStateBorder.Visibility = Visibility.Visible;
                return;
            }

            EmptyStateBorder.Visibility = Visibility.Collapsed;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.IsCategory)
                {
                    var folderElement = CreateFolderElement(item, parentFolder: null, nestingLevel: 0, isFirstItem: i == 0);
                    ItemsContainer.Children.Add(folderElement);
                }
                else
                {
                    var itemRow = CreateAppItemRow(item, parentFolder: null, isNested: false, isFirstItem: i == 0);
                    ItemsContainer.Children.Add(itemRow);
                }
            }
        }

        private static Border CreateInsertionIndicator(bool isCompact)
        {
            var indicator = new Border
            {
                Height = 2.0,
                Margin = isCompact ? new Thickness(2, 0, 2, 0) : new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ClipToBounds = false,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            var lineGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var line = new Border
            {
                Height = 1.5,
                CornerRadius = new CornerRadius(0.75),
                Background = GetCachedBrush(Color.FromRgb(255, 180, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(255, 180, 0),
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
            };
            lineGrid.Children.Add(line);

            var circle = new Border
            {
                Width = 4.5,
                Height = 4.5,
                CornerRadius = new CornerRadius(2.25),
                Background = GetCachedBrush(Color.FromRgb(255, 230, 120)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(-2, 0, 0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(255, 180, 0),
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 1.0
                }
            };
            lineGrid.Children.Add(circle);

            indicator.Child = lineGrid;
            return indicator;
        }

        private static Border CreateFolderCueOverlay(bool isCompact, string text = "Klasör")
        {
            var overlay = new Border
            {
                Background = GetCachedBrush(Color.FromArgb(235, 28, 24, 16)),
                BorderBrush = GetCachedBrush(Color.FromArgb(245, 255, 185, 0)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(isCompact ? 8 : 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            if (isCompact)
            {
                var cueStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var cueIcon = new TextBlock
                {
                    Text = "📁",
                    FontSize = 18,
                    Foreground = GetCachedBrush(Color.FromRgb(255, 210, 60)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var cueText = new TextBlock
                {
                    Text = text,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetCachedBrush(Color.FromRgb(255, 210, 60)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                cueStack.Children.Add(cueIcon);
                cueStack.Children.Add(cueText);
                overlay.Child = cueStack;
            }
            else
            {
                var cueStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var cueIcon = new TextBlock
                {
                    Text = "📁 ",
                    FontSize = 14,
                    Foreground = GetCachedBrush(Color.FromRgb(255, 210, 60)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var cueText = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetCachedBrush(Color.FromRgb(255, 210, 60)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                cueStack.Children.Add(cueIcon);
                cueStack.Children.Add(cueText);
                overlay.Child = cueStack;
            }

            return overlay;
        }

        private UIElement CreateFolderElement(MenuItemConfig folder, MenuItemConfig? parentFolder = null, int nestingLevel = 0, bool isFirstItem = false)
        {
            var isCompact = _config.Tools?.SideMenu?.IsCompact ?? false;
            var isFolderExpanded = _expandedFolders.Contains(folder);

            var folderContainer = new StackPanel
            {
                Margin = new Thickness(nestingLevel > 0 ? (isCompact ? 4 : 10) : 0, 2, 0, 3),
                AllowDrop = true
            };

            var headerWrapper = new Grid
            {
                HorizontalAlignment = isCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                Width = isCompact ? 56 : double.NaN,
                AllowDrop = true
            };

            var topIndicator = CreateInsertionIndicator(isCompact);
            topIndicator.VerticalAlignment = VerticalAlignment.Top;
            topIndicator.Margin = isCompact ? new Thickness(2, -2, 2, 0) : new Thickness(6, -2, 6, 0);
            Panel.SetZIndex(topIndicator, 20);
            if (isFirstItem)
            {
                _firstItemTopIndicator = topIndicator;
            }

            var bottomIndicator = CreateInsertionIndicator(isCompact);
            bottomIndicator.VerticalAlignment = VerticalAlignment.Bottom;
            bottomIndicator.Margin = isCompact ? new Thickness(2, 0, 2, -2) : new Thickness(6, 0, 6, -2);
            Panel.SetZIndex(bottomIndicator, 20);

            // Klasör başlık kartı
            var headerBorder = new Border
            {
                Height = isCompact ? 52 : 42,
                Width = isCompact ? 56 : double.NaN,
                HorizontalAlignment = isCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(isCompact ? 10 : 8),
                Background = ResolveRowBrush(folder.FixedColor, isFolder: true),
                BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(isCompact ? 3 : 8, isCompact ? 3 : 4, isCompact ? 3 : 8, isCompact ? 3 : 4),
                Cursor = Cursors.Hand,
                ToolTip = isCompact ? $"{folder.Label} ({folder.Children.Count})" : null
            };

            headerWrapper.Children.Add(headerBorder);
            headerWrapper.Children.Add(topIndicator);
            headerWrapper.Children.Add(bottomIndicator);

            var grid = new Grid();
            if (isCompact)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            }

            // Klasör İkonu
            var folderIcon = new TextBlock
            {
                Text = isFolderExpanded ? "📂" : "📁",
                FontSize = isCompact ? 30 : 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(folderIcon, 0);
            grid.Children.Add(folderIcon);

            // Kompakt Mod İçin Hover İsmi (Hover yapıldığında sembolün yerine bu görünür)
            TextBlock? compactNameBlock = null;
            if (isCompact)
            {
                compactNameBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(folder.Label) ? "Klasör" : folder.Label,
                    Foreground = Brushes.White,
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed,
                    MaxHeight = 44,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(compactNameBlock, 0);
                grid.Children.Add(compactNameBlock);
            }

            // Klasör Adı ve Öğe Sayısı (Kompakt modda gizlenir)
            if (!isCompact)
            {
                var titleStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 4, 0)
                };
                var labelBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(folder.Label) ? "Klasör" : folder.Label,
                    Foreground = GetCachedBrush(Color.FromRgb(245, 248, 252)),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 135
                };
                var countBlock = new TextBlock
                {
                    Text = $" ({folder.Children.Count})",
                    Foreground = GetCachedBrush(Color.FromRgb(160, 175, 190)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleStack.Children.Add(labelBlock);
                titleStack.Children.Add(countBlock);
                Grid.SetColumn(titleStack, 1);
                grid.Children.Add(titleStack);

                // Aç/Kapat Oku
                var arrowBlock = new TextBlock
                {
                    Text = isFolderExpanded ? "▼" : "▶",
                    Foreground = GetCachedBrush(Color.FromRgb(160, 175, 190)),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(arrowBlock, 2);
                grid.Children.Add(arrowBlock);
            }

            var folderCueOverlay = CreateFolderCueOverlay(isCompact, isCompact ? "Klasöre Kat" : "Klasöre Ekle");
            Grid.SetColumn(folderCueOverlay, 0);
            if (!isCompact)
            {
                Grid.SetColumnSpan(folderCueOverlay, 3);
            }
            grid.Children.Add(folderCueOverlay);

            headerBorder.Child = grid;

            // Klasör Alt Öğeler Paneli (İç içe alt klasörleri de recursive destekler)
            var subItemsPanel = new StackPanel
            {
                Margin = new Thickness(isCompact ? 4 : 12, 2, 0, 2),
                Visibility = isFolderExpanded ? Visibility.Visible : Visibility.Collapsed
            };

            foreach (var child in folder.Children)
            {
                if (child.IsCategory)
                {
                    var subFolderElement = CreateFolderElement(child, parentFolder: folder, nestingLevel: nestingLevel + 1);
                    subItemsPanel.Children.Add(subFolderElement);
                }
                else
                {
                    var childRow = CreateAppItemRow(child, parentFolder: folder, isNested: true);
                    subItemsPanel.Children.Add(childRow);
                }
            }

            // Klasör Sürükleme Başlangıcı & Tıklama
            headerBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _folderDragStartPoint = e.GetPosition(null);
            };

            headerBorder.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isTransitioning)
                {
                    var currentPoint = e.GetPosition(null);
                    if (Math.Abs(currentPoint.X - _folderDragStartPoint.X) > 6 || Math.Abs(currentPoint.Y - _folderDragStartPoint.Y) > 6)
                    {
                        _isInternalDragging = true;
                        _internalDropHandled = false;
                        _autoCollapseTimer?.Stop();
                        _draggedItem = folder;
                        _draggedItemParentFolder = parentFolder;
                        try
                        {
                            var data = new DataObject(DragDropItemFormat, folder);
                            DragDrop.DoDragDrop(headerBorder, data, DragDropEffects.Move);

                            // Menü dışına sürüklendiyse klasörü kaldır
                            if (!_internalDropHandled && IsCursorOutsideDrawer())
                            {
                                RemoveItemFromMenu(folder, parentFolder);
                            }
                        }
                        finally
                        {
                            _isInternalDragging = false;
                            _draggedItem = null;
                            _draggedItemParentFolder = null;

                            if (IsCursorOutsideDrawer())
                            {
                                _autoCollapseTimer?.Stop();
                                _autoCollapseTimer?.Start();
                            }
                        }
                    }
                }
            };

            headerBorder.PreviewMouseLeftButtonUp += (s, e) =>
            {
                var currentPoint = e.GetPosition(null);
                if (Math.Abs(currentPoint.X - _folderDragStartPoint.X) <= 6 && Math.Abs(currentPoint.Y - _folderDragStartPoint.Y) <= 6)
                {
                    if (_expandedFolders.Contains(folder))
                    {
                        _expandedFolders.Remove(folder);
                    }
                    else
                    {
                        _expandedFolders.Add(folder);
                    }
                    RenderItems();
                }
            };

            // Sağ Tıklama ile Bağlam Menüsü
            headerBorder.PreviewMouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowItemContextMenu(folder, isFolder: true, parentFolder, e.GetPosition(DrawerPanel));
            };

            // Klasör başlık hover efekti
            headerBorder.MouseEnter += (_, __) =>
            {
                headerBorder.Background = ResolveRowHoverBrush(folder.FixedColor);
                headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(90, 255, 255, 255));
                if (isCompact && compactNameBlock != null)
                {
                    folderIcon.Visibility = Visibility.Collapsed;
                    compactNameBlock.Visibility = Visibility.Visible;
                }
            };
            headerBorder.MouseLeave += (_, __) =>
            {
                headerBorder.Background = ResolveRowBrush(folder.FixedColor, isFolder: true);
                headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255));
                if (isCompact && compactNameBlock != null)
                {
                    compactNameBlock.Visibility = Visibility.Collapsed;
                    folderIcon.Visibility = Visibility.Visible;
                }
            };

            // Bir öğeyi bu klasörün üzerine sürükleyip bırakınca sıralama (üst/alt) veya klasöre ekleme (orta)
            int folderDropMode = 0; // 1: Before, 2: After, 3: IntoFolder

            headerWrapper.DragOver += (s, e) =>
            {
                if ((e.Data.GetDataPresent(DragDropItemFormat) && _draggedItem != null && _draggedItem != folder) ||
                    e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent(DragDropItemFormat) ? DragDropEffects.Move : DragDropEffects.Copy;
                    e.Handled = true;
                    var pos = e.GetPosition(headerWrapper);
                    double totalH = headerWrapper.ActualHeight > 0 ? headerWrapper.ActualHeight : (isCompact ? 52 : 42);
                    double edgeZone = Math.Max(12, Math.Min(15, totalH * 0.28));

                    if (pos.Y < edgeZone)
                    {
                        folderDropMode = 1;
                        topIndicator.Visibility = Visibility.Visible;
                        bottomIndicator.Visibility = Visibility.Collapsed;
                        folderCueOverlay.Visibility = Visibility.Collapsed;
                        headerBorder.BorderThickness = new Thickness(1);
                        headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255));
                    }
                    else if (pos.Y > totalH - edgeZone)
                    {
                        folderDropMode = 2;
                        topIndicator.Visibility = Visibility.Collapsed;
                        bottomIndicator.Visibility = Visibility.Visible;
                        folderCueOverlay.Visibility = Visibility.Collapsed;
                        headerBorder.BorderThickness = new Thickness(1);
                        headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255));
                    }
                    else
                    {
                        folderDropMode = 3;
                        topIndicator.Visibility = Visibility.Collapsed;
                        bottomIndicator.Visibility = Visibility.Collapsed;
                        folderCueOverlay.Visibility = Visibility.Visible;
                        headerBorder.BorderThickness = new Thickness(2);
                        headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(240, 255, 180, 0));
                    }
                }
            };
            headerWrapper.DragLeave += (s, e) =>
            {
                var curPos = e.GetPosition(headerWrapper);
                if (curPos.X < -2 || curPos.Y < -2 || curPos.X > headerWrapper.ActualWidth + 2 || curPos.Y > headerWrapper.ActualHeight + 2)
                {
                    folderDropMode = 0;
                    topIndicator.Visibility = Visibility.Collapsed;
                    bottomIndicator.Visibility = Visibility.Collapsed;
                    folderCueOverlay.Visibility = Visibility.Collapsed;
                    headerBorder.BorderThickness = new Thickness(1);
                    headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255));
                }
            };
            headerWrapper.Drop += (s, e) =>
            {
                int currentMode = folderDropMode;
                folderDropMode = 0;
                topIndicator.Visibility = Visibility.Collapsed;
                bottomIndicator.Visibility = Visibility.Collapsed;
                folderCueOverlay.Visibility = Visibility.Collapsed;
                headerBorder.BorderThickness = new Thickness(1);
                headerBorder.BorderBrush = GetCachedBrush(Color.FromArgb(40, 255, 255, 255));

                if (_draggedItem != null && _draggedItem != folder)
                {
                    _internalDropHandled = true;
                    if (currentMode == 1)
                    {
                        ReorderItemBefore(_draggedItem, folder, _draggedItemParentFolder, parentFolder);
                    }
                    else if (currentMode == 2)
                    {
                        ReorderItemAfter(_draggedItem, folder, _draggedItemParentFolder, parentFolder);
                    }
                    else
                    {
                        AddItemToFolder(_draggedItem, folder, _draggedItemParentFolder);
                    }
                    e.Handled = true;
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        _internalDropHandled = true;
                        if (currentMode == 1)
                        {
                            foreach (var file in files)
                            {
                                InsertApplicationNearItem(file, folder, parentFolder, insertBefore: true);
                            }
                        }
                        else if (currentMode == 2)
                        {
                            foreach (var file in files)
                            {
                                InsertApplicationNearItem(file, folder, parentFolder, insertBefore: false);
                            }
                        }
                        else
                        {
                            foreach (var file in files)
                            {
                                AddApplicationToFolder(file, folder);
                            }
                        }
                        e.Handled = true;
                    }
                }
            };

            folderContainer.Children.Add(headerWrapper);
            folderContainer.Children.Add(subItemsPanel);
            return folderContainer;
        }

        private UIElement CreateAppItemRow(MenuItemConfig item, MenuItemConfig? parentFolder, bool isNested, bool isFirstItem = false)
        {
            var isCompact = _config.Tools?.SideMenu?.IsCompact ?? false;
            var effectiveColor = !string.IsNullOrWhiteSpace(item.FixedColor)
                ? item.FixedColor
                : parentFolder?.FixedColor;

            var rowWrapper = new Grid
            {
                HorizontalAlignment = isCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                Width = isCompact ? 56 : double.NaN,
                Margin = new Thickness(0, isCompact ? 2 : 2, 0, isCompact ? 2 : 2),
                AllowDrop = true
            };

            var topIndicator = CreateInsertionIndicator(isCompact);
            topIndicator.VerticalAlignment = VerticalAlignment.Top;
            topIndicator.Margin = isCompact ? new Thickness(2, -2, 2, 0) : new Thickness(6, -2, 6, 0);
            Panel.SetZIndex(topIndicator, 20);
            if (isFirstItem)
            {
                _firstItemTopIndicator = topIndicator;
            }

            var bottomIndicator = CreateInsertionIndicator(isCompact);
            bottomIndicator.VerticalAlignment = VerticalAlignment.Bottom;
            bottomIndicator.Margin = isCompact ? new Thickness(2, 0, 2, -2) : new Thickness(6, 0, 6, -2);
            Panel.SetZIndex(bottomIndicator, 20);

            var rowBorder = new Border
            {
                Height = isCompact ? 52 : 40,
                Width = isCompact ? 56 : double.NaN,
                HorizontalAlignment = isCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(isCompact ? 10 : 8),
                Background = ResolveRowBrush(effectiveColor, isFolder: false),
                BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(isCompact ? 3 : 8, isCompact ? 3 : 4, isCompact ? 3 : 8, isCompact ? 3 : 4),
                Cursor = Cursors.Hand,
                ToolTip = isCompact ? item.Label : null
            };

            var grid = new Grid();
            if (isCompact)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Uygulama İkonu
            var iconImage = new Image
            {
                Width = isCompact ? 44 : 22,
                Height = isCompact ? 44 : 22,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);
            var icon = ResolveItemIcon(item.TargetPath);
            if (icon != null)
            {
                iconImage.Source = icon;
            }
            Grid.SetColumn(iconImage, 0);
            grid.Children.Add(iconImage);

            // Başlık
            TextBlock? labelBlock = null;
            if (!isCompact)
            {
                labelBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Label) ? "Uygulama" : item.Label,
                    Foreground = GetCachedBrush(Color.FromRgb(235, 240, 246)),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(8, 0, 4, 0)
                };
                Grid.SetColumn(labelBlock, 1);
                grid.Children.Add(labelBlock);
            }

            var folderCueOverlay = CreateFolderCueOverlay(isCompact, isCompact ? "Klasör" : "Klasör Oluştur");
            Grid.SetColumn(folderCueOverlay, 0);
            if (!isCompact)
            {
                Grid.SetColumnSpan(folderCueOverlay, 2);
            }
            grid.Children.Add(folderCueOverlay);

            rowBorder.Child = grid;

            // Hover efekti - rengi belirgin şekilde aydınlat ve çerçeve parlaklığı ver
            rowBorder.MouseEnter += (_, __) =>
            {
                rowBorder.Background = ResolveRowHoverBrush(effectiveColor);
                rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(90, 255, 255, 255));
                if (labelBlock != null)
                {
                    labelBlock.Foreground = Brushes.White;
                    labelBlock.FontWeight = FontWeights.Medium;
                }
            };
            rowBorder.MouseLeave += (_, __) =>
            {
                rowBorder.Background = ResolveRowBrush(effectiveColor, isFolder: false);
                rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255));
                if (labelBlock != null)
                {
                    labelBlock.Foreground = GetCachedBrush(Color.FromRgb(235, 240, 246));
                    labelBlock.FontWeight = FontWeights.Normal;
                }
            };

            // Sürükleme Başlangıcı (Drag & Drop)
            rowBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragStartPoint = e.GetPosition(null);
            };

            rowBorder.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isTransitioning)
                {
                    var currentPoint = e.GetPosition(null);
                    if (Math.Abs(currentPoint.X - _dragStartPoint.X) > 6 || Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 6)
                    {
                        _isInternalDragging = true;
                        _internalDropHandled = false;
                        _autoCollapseTimer?.Stop();
                        _draggedItem = item;
                        _draggedItemParentFolder = parentFolder;
                        try
                        {
                            var data = new DataObject(DragDropItemFormat, item);
                            DragDrop.DoDragDrop(rowBorder, data, DragDropEffects.Move);

                            // Menü dışına sürüklendiyse uygulamayı kaldır
                            if (!_internalDropHandled && IsCursorOutsideDrawer())
                            {
                                RemoveItemFromMenu(item, parentFolder);
                            }
                        }
                        finally
                        {
                            _isInternalDragging = false;
                            _draggedItem = null;
                            _draggedItemParentFolder = null;

                            // Bittiğinde fare dışarıdaysa otomatik kapanmayı tetikle
                            if (IsCursorOutsideDrawer())
                            {
                                _autoCollapseTimer?.Stop();
                                _autoCollapseTimer?.Start();
                            }
                        }
                    }
                }
            };

            // Sağ Tıklama ile Bağlam Menüsü
            rowBorder.PreviewMouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowItemContextMenu(item, isFolder: false, parentFolder, e.GetPosition(DrawerPanel));
            };

            // 3 Bölge: Üst (Sıralama), Alt (Sıralama), Orta (Klasör Oluşturma + Görsel İpucu)
            int dropMode = 0; // 1: Before, 2: After, 3: MergeFolder

            rowWrapper.DragOver += (s, e) =>
            {
                if ((e.Data.GetDataPresent(DragDropItemFormat) && _draggedItem != null && _draggedItem != item) ||
                    e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent(DragDropItemFormat) ? DragDropEffects.Move : DragDropEffects.Copy;
                    e.Handled = true;

                    var pos = e.GetPosition(rowWrapper);
                    double totalH = rowWrapper.ActualHeight > 0 ? rowWrapper.ActualHeight : (isCompact ? 52 : 40);
                    double edgeZone = Math.Max(12, Math.Min(15, totalH * 0.28));

                    if (pos.Y < edgeZone)
                    {
                        // ÜST BÖLGE: Öncesine yerleştir
                        dropMode = 1;
                        topIndicator.Visibility = Visibility.Visible;
                        bottomIndicator.Visibility = Visibility.Collapsed;
                        folderCueOverlay.Visibility = Visibility.Collapsed;
                        rowBorder.BorderThickness = new Thickness(1);
                        rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255));
                    }
                    else if (pos.Y > totalH - edgeZone)
                    {
                        // ALT BÖLGE: Sonrasına yerleştir
                        dropMode = 2;
                        topIndicator.Visibility = Visibility.Collapsed;
                        bottomIndicator.Visibility = Visibility.Visible;
                        folderCueOverlay.Visibility = Visibility.Collapsed;
                        rowBorder.BorderThickness = new Thickness(1);
                        rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255));
                    }
                    else
                    {
                        // ORTA BÖLGE: Klasör oluşturma + Görsel İpucu
                        dropMode = 3;
                        topIndicator.Visibility = Visibility.Collapsed;
                        bottomIndicator.Visibility = Visibility.Collapsed;
                        folderCueOverlay.Visibility = Visibility.Visible;
                        rowBorder.BorderThickness = new Thickness(2);
                        rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(240, 255, 180, 0));
                    }
                }
            };

            rowWrapper.DragLeave += (s, e) =>
            {
                var curPos = e.GetPosition(rowWrapper);
                if (curPos.X < -2 || curPos.Y < -2 || curPos.X > rowWrapper.ActualWidth + 2 || curPos.Y > rowWrapper.ActualHeight + 2)
                {
                    dropMode = 0;
                    topIndicator.Visibility = Visibility.Collapsed;
                    bottomIndicator.Visibility = Visibility.Collapsed;
                    folderCueOverlay.Visibility = Visibility.Collapsed;
                    rowBorder.BorderThickness = new Thickness(1);
                    rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255));
                }
            };

            rowWrapper.Drop += (s, e) =>
            {
                int currentMode = dropMode;
                dropMode = 0;
                topIndicator.Visibility = Visibility.Collapsed;
                bottomIndicator.Visibility = Visibility.Collapsed;
                folderCueOverlay.Visibility = Visibility.Collapsed;
                rowBorder.BorderThickness = new Thickness(1);
                rowBorder.BorderBrush = GetCachedBrush(Color.FromArgb(28, 255, 255, 255));

                if (_draggedItem != null && _draggedItem != item)
                {
                    _internalDropHandled = true;
                    if (currentMode == 1)
                    {
                        // Üstüne yerleştir (sıralamayı değiştir)
                        ReorderItemBefore(_draggedItem, item, _draggedItemParentFolder, parentFolder);
                    }
                    else if (currentMode == 2)
                    {
                        // Altına yerleştir (sıralamayı değiştir)
                        ReorderItemAfter(_draggedItem, item, _draggedItemParentFolder, parentFolder);
                    }
                    else if (currentMode == 3)
                    {
                        // Klasör oluştur veya içine kat
                        HandleItemDroppedOnItem(_draggedItem, item, _draggedItemParentFolder, parentFolder);
                    }
                    e.Handled = true;
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        _internalDropHandled = true;
                        if (currentMode == 1)
                        {
                            foreach (var file in files)
                            {
                                InsertApplicationNearItem(file, item, parentFolder, insertBefore: true);
                            }
                        }
                        else if (currentMode == 2)
                        {
                            foreach (var file in files)
                            {
                                InsertApplicationNearItem(file, item, parentFolder, insertBefore: false);
                            }
                        }
                        else
                        {
                            // Klasör yap ve dosyayı içine ekle
                            ConvertItemToFolder(item, parentFolder);
                            var createdFolder = (parentFolder != null ? parentFolder.Children : _config.Tools?.SideMenu?.Items)
                                ?.Find(f => f.IsCategory && f.Children.Contains(item));
                            if (createdFolder != null)
                            {
                                foreach (var file in files)
                                {
                                    AddApplicationToFolder(file, createdFolder);
                                }
                            }
                        }
                        e.Handled = true;
                    }
                }
            };

            // Tıklayınca uygulamayı başlat ve kapat
            rowBorder.PreviewMouseLeftButtonUp += (s, e) =>
            {
                // Sürükleme değilse başlat
                var currentPoint = e.GetPosition(null);
                if (Math.Abs(currentPoint.X - _dragStartPoint.X) <= 6 && Math.Abs(currentPoint.Y - _dragStartPoint.Y) <= 6)
                {
                    CollapseMenu();
                    try
                    {
                        var hwnd = new WindowInteropHelper(this).Handle;
                        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                        _launcherService.Launch(item.TargetPath, screen.WorkingArea);
                    }
                    catch { }
                }
            };

            rowWrapper.Children.Add(rowBorder);
            rowWrapper.Children.Add(topIndicator);
            rowWrapper.Children.Add(bottomIndicator);

            return rowWrapper;
        }

        // ==========================================
        // KLASÖR BİRLEŞTİRME & ÇIKARMA MANTIKLARI
        // ==========================================

        private void HandleItemDroppedOnItem(MenuItemConfig dragged, MenuItemConfig target, MenuItemConfig? sourceFolder, MenuItemConfig? targetFolder)
        {
            if (dragged == target) return;

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            // Sürüklenen öge bir klasörse ve hedef bir klasörse -> Alt Klasör Yap!
            if (dragged.IsCategory && target.IsCategory)
            {
                AddItemToFolder(dragged, target, sourceFolder);
                return;
            }

            // Sürüklenen bir klasör bir uygulamanın üzerine bırakıldıysa -> Uygulamayı klasöre kat
            if (dragged.IsCategory && !target.IsCategory)
            {
                AddItemToFolder(target, dragged, targetFolder);
                return;
            }

            // Sürüklenen uygulama bir klasörün üzerine bırakıldıysa -> Klasöre ekle
            if (!dragged.IsCategory && target.IsCategory)
            {
                AddItemToFolder(dragged, target, sourceFolder);
                return;
            }

            // Durum 1: İki bağımsız uygulama birbirinin üzerine sürüklendi -> Yeni Klasör Oluştur!
            if (sourceFolder == null && targetFolder == null)
            {
                var targetIndex = rootItems.IndexOf(target);
                rootItems.Remove(dragged);
                rootItems.Remove(target);

                var newFolder = new MenuItemConfig
                {
                    Label = "Yeni Klasör",
                    IsCategory = true,
                    Children = new List<MenuItemConfig> { target, dragged }
                };

                if (targetIndex >= 0 && targetIndex <= rootItems.Count)
                {
                    rootItems.Insert(targetIndex, newFolder);
                }
                else
                {
                    rootItems.Add(newFolder);
                }

                _expandedFolders.Add(newFolder);
                SaveAndRefresh();
                return;
            }

            // Durum 2: Bir klasörün içindeki bir uygulama, dışarıdaki (klasörde olmayan) bir uygulamanın üzerine sürüklendi!
            // -> Bu iki uygulamadan bağımsız YENİ bir klasör oluştur!
            if (sourceFolder != null && targetFolder == null)
            {
                dragged.FixedColor = string.Empty;
                sourceFolder.Children.Remove(dragged);
                CheckAndDissolveFolderIfSingleItem(sourceFolder, FindParentFolder(sourceFolder));

                var targetIndex = rootItems.IndexOf(target);
                rootItems.Remove(target);

                var newFolder = new MenuItemConfig
                {
                    Label = "Yeni Klasör",
                    IsCategory = true,
                    Children = new List<MenuItemConfig> { target, dragged }
                };

                if (targetIndex >= 0 && targetIndex <= rootItems.Count)
                {
                    rootItems.Insert(targetIndex, newFolder);
                }
                else
                {
                    rootItems.Add(newFolder);
                }

                _expandedFolders.Add(newFolder);
                SaveAndRefresh();
                return;
            }

            // Durum 3: Bir öğe, klasörün içindeki bir öğenin üzerine bırakıldıysa o klasöre ekle
            if (targetFolder != null)
            {
                AddItemToFolder(dragged, targetFolder, sourceFolder);
            }
        }

        private void AddItemToFolder(MenuItemConfig item, MenuItemConfig targetFolder, MenuItemConfig? sourceFolder)
        {
            if (item == targetFolder) return;
            if (item.IsCategory && IsDescendantFolder(item, targetFolder)) return;

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            // Önceki yerinden kaldır
            if (sourceFolder != null)
            {
                sourceFolder.Children.Remove(item);
                CheckAndDissolveFolderIfSingleItem(sourceFolder, FindParentFolder(sourceFolder));
            }
            else
            {
                rootItems.Remove(item);
            }

            // Hedef klasöre ekle (klasörün rengini dinamik miras alması için kendi rengini temizle)
            if (!targetFolder.Children.Contains(item))
            {
                item.FixedColor = string.Empty;
                targetFolder.Children.Add(item);
            }

            _expandedFolders.Add(targetFolder);
            SaveAndRefresh();
        }


        private static bool IsDescendantFolder(MenuItemConfig potentialParent, MenuItemConfig candidate)
        {
            if (potentialParent.Children == null) return false;
            foreach (var child in potentialParent.Children)
            {
                if (child == candidate) return true;
                if (child.IsCategory && IsDescendantFolder(child, candidate)) return true;
            }
            return false;
        }

        private void ReorderItemBefore(MenuItemConfig dragged, MenuItemConfig target, MenuItemConfig? sourceFolder, MenuItemConfig? targetFolder)
        {
            if (dragged == target) return;

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            var sourceList = sourceFolder != null ? sourceFolder.Children : rootItems;
            var targetList = targetFolder != null ? targetFolder.Children : rootItems;

            if (sourceList == null || targetList == null) return;

            if (ReferenceEquals(sourceList, targetList))
            {
                int oldIndex = sourceList.IndexOf(dragged);
                int targetIndex = sourceList.IndexOf(target);
                if (oldIndex < 0 || targetIndex < 0 || oldIndex == targetIndex) return;

                sourceList.RemoveAt(oldIndex);
                if (oldIndex < targetIndex)
                {
                    targetIndex--;
                }
                sourceList.Insert(targetIndex, dragged);
            }
            else
            {
                sourceList.Remove(dragged);
                if (sourceFolder != null)
                {
                    CheckAndDissolveFolderIfSingleItem(sourceFolder, FindParentFolder(sourceFolder));
                }

                int targetIndex = targetList.IndexOf(target);
                if (targetIndex >= 0 && targetIndex <= targetList.Count)
                {
                    targetList.Insert(targetIndex, dragged);
                }
                else
                {
                    targetList.Add(dragged);
                }

                // Klasörden ana menüye taşındıysa klasör rengini sıfırla
                if (targetFolder == null && sourceFolder != null)
                {
                    dragged.FixedColor = string.Empty;
                }
            }

            SaveAndRefresh();
        }

        private void ReorderItemAfter(MenuItemConfig dragged, MenuItemConfig target, MenuItemConfig? sourceFolder, MenuItemConfig? targetFolder)
        {
            if (dragged == target) return;

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            var sourceList = sourceFolder != null ? sourceFolder.Children : rootItems;
            var targetList = targetFolder != null ? targetFolder.Children : rootItems;

            if (sourceList == null || targetList == null) return;

            if (ReferenceEquals(sourceList, targetList))
            {
                int oldIndex = sourceList.IndexOf(dragged);
                int targetIndex = sourceList.IndexOf(target);
                if (oldIndex < 0 || targetIndex < 0 || oldIndex == targetIndex) return;

                sourceList.RemoveAt(oldIndex);
                if (oldIndex < targetIndex)
                {
                    sourceList.Insert(targetIndex, dragged);
                }
                else
                {
                    sourceList.Insert(targetIndex + 1, dragged);
                }
            }
            else
            {
                sourceList.Remove(dragged);
                if (sourceFolder != null)
                {
                    CheckAndDissolveFolderIfSingleItem(sourceFolder, FindParentFolder(sourceFolder));
                }

                int targetIndex = targetList.IndexOf(target);
                if (targetIndex >= 0 && targetIndex + 1 <= targetList.Count)
                {
                    targetList.Insert(targetIndex + 1, dragged);
                }
                else
                {
                    targetList.Add(dragged);
                }

                if (targetFolder == null && sourceFolder != null)
                {
                    dragged.FixedColor = string.Empty;
                }
            }

            SaveAndRefresh();
        }

        private void InsertApplicationNearItem(string path, MenuItemConfig target, MenuItemConfig? parentFolder, bool insertBefore)
        {
            if (string.IsNullOrWhiteSpace(path) || target == null) return;
            var cleanPath = path.Trim();
            var label = Path.GetFileNameWithoutExtension(cleanPath);
            if (string.IsNullOrWhiteSpace(label)) label = cleanPath;

            var item = new MenuItemConfig
            {
                Label = label,
                TargetPath = cleanPath
            };

            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;
            var list = parentFolder != null ? parentFolder.Children : rootItems;
            int targetIndex = list.IndexOf(target);
            if (targetIndex < 0)
            {
                list.Add(item);
            }
            else
            {
                int insertIndex = insertBefore ? targetIndex : targetIndex + 1;
                if (insertIndex >= 0 && insertIndex <= list.Count)
                {
                    list.Insert(insertIndex, item);
                }
                else
                {
                    list.Add(item);
                }
            }
            SaveAndRefresh();
        }

        private void ConvertItemToFolder(MenuItemConfig item, MenuItemConfig? parentFolder)
        {
            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;
            var list = parentFolder != null ? parentFolder.Children : rootItems;
            int idx = list.IndexOf(item);
            if (idx < 0) return;

            var newFolder = new MenuItemConfig
            {
                Label = (string.IsNullOrWhiteSpace(item.Label) ? "Uygulama" : item.Label) + " Klasörü",
                IsCategory = true,
                Children = new List<MenuItemConfig> { item }
            };
            list[idx] = newFolder;
            _expandedFolders.Add(newFolder);
            SaveAndRefresh();
        }

        private void RemoveItemFromMenu(MenuItemConfig item, MenuItemConfig? parentFolder)
        {
            parentFolder ??= FindParentFolder(item);
            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            if (parentFolder != null)
            {
                parentFolder.Children.Remove(item);
                CheckAndDissolveFolderIfSingleItem(parentFolder, FindParentFolder(parentFolder));
            }
            else
            {
                rootItems.Remove(item);
            }

            _expandedFolders.Remove(item);
            SaveAndRefresh();
        }


        private bool IsCursorOutsideDrawer()
        {
            if (GetCursorPos(out var pt))
            {
                try
                {
                    var source = PresentationSource.FromVisual(this);
                    if (source != null && source.CompositionTarget != null)
                    {
                        var wpfPos = PointFromScreen(new Point(pt.X, pt.Y));
                        return wpfPos.X < 0 || wpfPos.X > ActualWidth || wpfPos.Y < 0 || wpfPos.Y > ActualHeight;
                    }
                }
                catch { }

                return pt.X < Left;
            }
            return false;
        }

        private void MoveItemOutOfFolder(MenuItemConfig item, MenuItemConfig sourceFolder)
        {
            var parentOfSource = FindParentFolder(sourceFolder);
            var destinationList = parentOfSource != null ? parentOfSource.Children : _config.Tools?.SideMenu?.Items;
            if (destinationList == null) return;

            // Klasörden çıkarılan uygulamanın rengini sıfırla (klasör rengi yapışıp kalmasın)
            item.FixedColor = string.Empty;

            sourceFolder.Children.Remove(item);
            var folderIndex = destinationList.IndexOf(sourceFolder);
            if (folderIndex >= 0 && folderIndex < destinationList.Count)
            {
                destinationList.Insert(folderIndex + 1, item);
            }
            else
            {
                destinationList.Add(item);
            }

            CheckAndDissolveFolderIfSingleItem(sourceFolder, parentOfSource);
            SaveAndRefresh();
        }


        private void CheckAndDissolveFolderIfSingleItem(MenuItemConfig folder, MenuItemConfig? parentFolder = null)
        {
            var rootItems = _config.Tools?.SideMenu?.Items;
            if (rootItems == null) return;

            var containerList = parentFolder != null ? parentFolder.Children : rootItems;
            if (containerList == null) return;

            // Klasörde sadece 1 uygulama kaldıysa klasörü kapat ve o uygulamayı normal olarak dışarı al!
            if (folder.Children.Count == 1)
            {
                var remainingItem = folder.Children[0];
                if (!remainingItem.IsCategory)
                {
                    // Klasör rengini temizle
                    remainingItem.FixedColor = string.Empty;

                    var folderIndex = containerList.IndexOf(folder);
                    containerList.Remove(folder);
                    _expandedFolders.Remove(folder);

                    if (folderIndex >= 0 && folderIndex <= containerList.Count)
                    {
                        containerList.Insert(folderIndex, remainingItem);
                    }
                    else
                    {
                        containerList.Add(remainingItem);
                    }

                    if (parentFolder != null)
                    {
                        CheckAndDissolveFolderIfSingleItem(parentFolder, FindParentFolder(parentFolder));
                    }
                }
            }
            else if (folder.Children.Count == 0)
            {
                containerList.Remove(folder);
                _expandedFolders.Remove(folder);

                if (parentFolder != null)
                {
                    CheckAndDissolveFolderIfSingleItem(parentFolder, FindParentFolder(parentFolder));
                }
            }
        }

        private MenuItemConfig? FindParentFolder(MenuItemConfig target, List<MenuItemConfig>? items = null)
        {
            items ??= _config.Tools?.SideMenu?.Items;
            if (items == null) return null;

            foreach (var it in items)
            {
                if (it.IsCategory && it.Children != null)
                {
                    if (it.Children.Contains(target)) return it;
                    var sub = FindParentFolder(target, it.Children);
                    if (sub != null) return sub;
                }
            }
            return null;
        }

        // ==========================================
        // SAĞ TIK BAĞLAM MENÜSÜ & AYARLAR MENÜSÜ
        // ==========================================

        private void ShowItemContextMenu(MenuItemConfig item, bool isFolder, MenuItemConfig? parentFolder, Point clickPos)
        {
            CloseActiveContextMenu();

            var menuPanel = new StackPanel();
            const double menuWidth = 165.0;

            // Yeniden Adlandır
            var renameRow = CreateContextMenuRow("✏️  Yeniden Adlandır", () =>
            {
                CloseActiveContextMenu();
                PromptRename(item);
            });
            menuPanel.Children.Add(renameRow);

            // Renk Değiştir
            var colorRow = CreateContextMenuRow("🎨  Renk Değiştir", () =>
            {
                CloseActiveContextMenu();
                PromptColorChange(item);
            });
            menuPanel.Children.Add(colorRow);

            // Klasör Yap (Eğer uygulama ise)
            if (!isFolder)
            {
                var makeFolderRow = CreateContextMenuRow("📁  Klasör Yap", () =>
                {
                    CloseActiveContextMenu();
                    ConvertItemToFolder(item, parentFolder);
                });
                menuPanel.Children.Add(makeFolderRow);
            }

            // Klasörden Çıkar (eğer bir klasörün içindeyse)
            if (parentFolder != null)
            {
                var extractRow = CreateContextMenuRow("↗️  Klasörden Çıkar", () =>
                {
                    CloseActiveContextMenu();
                    MoveItemOutOfFolder(item, parentFolder);
                });
                menuPanel.Children.Add(extractRow);
            }

            // Kaldır
            var deleteRow = CreateContextMenuRow("🗑️  Kaldır", () =>
            {
                CloseActiveContextMenu();
                RemoveItemFromMenu(item, parentFolder);
            }, isDestructive: true);
            menuPanel.Children.Add(deleteRow);

            var contextBorder = new Border
            {
                Width = menuWidth,
                Background = GetCachedBrush(Color.FromArgb(250, 22, 26, 34)),
                BorderBrush = GetCachedBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(5),
                Child = menuPanel,
                Effect = SharedContextMenuShadow
            };

            OpenContextMenuPopup(contextBorder);
        }

        private void ShowSettingsMenu()
        {
            CloseActiveContextMenu();

            var menuPanel = new StackPanel();

            var isCompact = _config.Tools?.SideMenu?.IsCompact ?? false;
            var isIndicatorVisible = _config.Tools?.SideMenu?.ShowIndicatorLine ?? true;
            const double menuWidth = 150.0;

            var compactRow = CreateContextMenuRow(isCompact ? "✔  Kompakt Mod" : "    Kompakt Mod", () =>
            {
                CloseActiveContextMenu();
                ToggleCompactMode();
            });
            menuPanel.Children.Add(compactRow);

            var lineRow = CreateContextMenuRow(isIndicatorVisible ? "✔  Sarı Çizgi" : "    Sarı Çizgi", () =>
            {
                CloseActiveContextMenu();
                ToggleIndicatorLine();
            });
            menuPanel.Children.Add(lineRow);

            var isWeatherVisible = _config.Tools?.SideMenu?.ShowWeatherCard ?? true;
            var weatherRow = CreateContextMenuRow(isWeatherVisible ? "✔  Hava Durumu" : "    Hava Durumu", () =>
            {
                CloseActiveContextMenu();
                ToggleWeatherCard();
            });
            menuPanel.Children.Add(weatherRow);

            var contextBorder = new Border
            {
                Width = menuWidth,
                Background = GetCachedBrush(Color.FromArgb(250, 22, 26, 34)),
                BorderBrush = GetCachedBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(5),
                Child = menuPanel,
                Effect = SharedContextMenuShadow
            };

            OpenContextMenuPopup(contextBorder, SettingsButton, PlacementMode.Top);
        }

        private void ToggleIndicatorLine()
        {
            _config.Tools ??= new ToolsSettings();
            _config.Tools.SideMenu ??= new SideMenuToolSettings();
            _config.Tools.SideMenu.ShowIndicatorLine = !_config.Tools.SideMenu.ShowIndicatorLine;
            VisualLine.Visibility = _config.Tools.SideMenu.ShowIndicatorLine ? Visibility.Visible : Visibility.Collapsed;
            SaveAndRefresh();
        }


        private void ToggleCompactMode()
        {
            _config.Tools ??= new ToolsSettings();
            _config.Tools.SideMenu ??= new SideMenuToolSettings();
            _config.Tools.SideMenu.IsCompact = !_config.Tools.SideMenu.IsCompact;

            SaveAndRefresh();
        }

        private void ToggleWeatherCard()
        {
            _config.Tools ??= new ToolsSettings();
            _config.Tools.SideMenu ??= new SideMenuToolSettings();
            _config.Tools.SideMenu.ShowWeatherCard = !_config.Tools.SideMenu.ShowWeatherCard;
            if (!_config.Tools.SideMenu.ShowWeatherCard)
            {
                HideWeatherCardAndStopVisuals();
            }
            else if (_isExpanded)
            {
                PrepareWeatherCardForExpand();
            }
            SaveAndRefresh();
        }


        // ==========================================
        // DIŞ SEKME (TAB) YÖNETİMİ
        // ==========================================

        private SideMenuTabConfig EnsureTabsInitialized()
        {
            _config.Tools ??= new ToolsSettings();
            var sideMenu = _config.Tools.SideMenu ??= new SideMenuToolSettings();
            sideMenu.Tabs ??= new List<SideMenuTabConfig>();
            sideMenu.Items ??= new List<MenuItemConfig>();

            if (sideMenu.Tabs.Count == 0)
            {
                var defaultTab = new SideMenuTabConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Sekme 1",
                    Items = new List<MenuItemConfig>(sideMenu.Items)
                };
                sideMenu.Tabs.Add(defaultTab);
                sideMenu.ActiveTabId = defaultTab.Id;
            }

            var active = sideMenu.Tabs.Find(t => t.Id == sideMenu.ActiveTabId) ?? sideMenu.Tabs[0];
            sideMenu.ActiveTabId = active.Id;
            sideMenu.Items = active.Items;
            return active;
        }

        private void RenderTabs()
        {
            TabsContainer.Children.Clear();
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu == null) return;

            var activeTab = EnsureTabsInitialized();
            var tabs = sideMenu.Tabs;

            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var tabIndex = i + 1;
                var isActive = tab.Id == activeTab.Id;

                var isFirst = i == 0;
                var cornerRadius = isActive && isFirst
                    ? new CornerRadius(10, 0, 0, 4)
                    : (isActive ? new CornerRadius(8, 0, 0, 8) : new CornerRadius(6, 0, 0, 6));

                // Aktif sekme tam boyutta (28px) sola doğru uzanır.
                // Pasif sekmeler ise kullanıcının istediği gibi daha küçük (20px) ve sağa çekilmiş durur.
                var normalWidth = isActive ? 28.0 : 20.0;
                var normalHeight = isActive ? (isFirst ? 28.0 : 26.0) : 24.0;

                var tabBadge = new Border
                {
                    Width = normalWidth,
                    Height = normalHeight,
                    CornerRadius = cornerRadius,
                    Margin = new Thickness(0, 0, 0, 3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Cursor = Cursors.Hand,
                    ToolTip = tab.Name,
                    BorderThickness = new Thickness(1, 1, 0, 1)
                };

                Color tabColor = Colors.Transparent;
                bool hasCustomColor = !string.IsNullOrWhiteSpace(tab.Color);
                if (hasCustomColor)
                {
                    try { tabColor = (Color)ColorConverter.ConvertFromString(tab.Color); } catch { }
                }

                if (isActive)
                {
                    if (hasCustomColor)
                    {
                        tabBadge.Background = GetCachedBrush(Color.FromArgb(240, tabColor.R, tabColor.G, tabColor.B));
                        tabBadge.BorderBrush = GetCachedBrush(Color.FromArgb(255, Math.Min((byte)255, (byte)(tabColor.R + 50)), Math.Min((byte)255, (byte)(tabColor.G + 50)), Math.Min((byte)255, (byte)(tabColor.B + 50))));
                    }
                    else
                    {
                        // Görseldeki gibi canlı turuncu renk (#F06500)
                        tabBadge.Background = GetCachedBrush(Color.FromRgb(240, 101, 0));
                        tabBadge.BorderBrush = GetCachedBrush(Color.FromRgb(255, 140, 40));
                    }
                }
                else
                {
                    if (hasCustomColor)
                    {
                        tabBadge.Background = GetCachedBrush(Color.FromArgb(140, tabColor.R, tabColor.G, tabColor.B));
                        tabBadge.BorderBrush = GetCachedBrush(Color.FromArgb(100, tabColor.R, tabColor.G, tabColor.B));
                    }
                    else
                    {
                        // Pasif sekme: Koyu gövdeyle uyumlu füme/siyah
                        tabBadge.Background = GetCachedBrush(Color.FromRgb(26, 30, 38));
                        tabBadge.BorderBrush = GetCachedBrush(Color.FromArgb(45, 255, 255, 255));
                    }
                }

                var tabText = new TextBlock
                {
                    Text = tabIndex.ToString(),
                    FontSize = isActive ? 13 : 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tabBadge.Child = tabText;

                // Pasif sekmeye hoverlanınca sola doğru hafifçe uzasın (hover geri bildirimi)
                tabBadge.MouseEnter += (_, __) =>
                {
                    if (!isActive)
                    {
                        tabBadge.Width = 24;
                        tabBadge.Background = GetCachedBrush(Color.FromRgb(45, 52, 64));
                    }
                };
                tabBadge.MouseLeave += (_, __) =>
                {
                    if (!isActive)
                    {
                        tabBadge.Width = 20;
                        if (hasCustomColor)
                            tabBadge.Background = GetCachedBrush(Color.FromArgb(140, tabColor.R, tabColor.G, tabColor.B));
                        else
                            tabBadge.Background = GetCachedBrush(Color.FromRgb(26, 30, 38));
                    }
                };

                var currentTab = tab;
                tabBadge.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    SwitchToTab(currentTab);
                };

                tabBadge.PreviewMouseRightButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    ShowTabContextMenu(currentTab, tabIndex, e.GetPosition(RootGrid));
                };

                TabsContainer.Children.Add(tabBadge);
            }
        }

        private void SwitchToTab(SideMenuTabConfig tab)
        {
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu == null || tab == null) return;
            sideMenu.ActiveTabId = tab.Id;
            sideMenu.Items = tab.Items;
            SaveAndRefresh();
        }


        private void OpenContextMenuPopup(Border contextBorder, UIElement? placementTarget = null, PlacementMode placement = PlacementMode.MousePoint)
        {
            CloseActiveContextMenu();

            var popup = new Popup
            {
                AllowsTransparency = true,
                PlacementTarget = placementTarget,
                Placement = placementTarget != null ? placement : PlacementMode.MousePoint,
                StaysOpen = false,
                Child = contextBorder
            };

            popup.Closed += (_, __) =>
            {
                if (_activeContextMenuPopup == popup)
                {
                    _activeContextMenuPopup = null;
                }
            };

            _activeContextMenuPopup = popup;
            popup.IsOpen = true;
        }

        private void ShowTabContextMenu(SideMenuTabConfig tab, int tabIndex, Point clickPos)
        {
            CloseActiveContextMenu();

            const double menuWidth = 160.0;
            var menuPanel = new StackPanel();

            // 1. Sekme Başlık Bilgisi
            var headerRow = new Border
            {
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = GetCachedBrush(Color.FromArgb(35, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 3)
            };
            var titleText = new TextBlock
            {
                Text = $"📌 {tabIndex}. {tab.Name}",
                FontWeight = FontWeights.Bold,
                Foreground = GetCachedBrush(Color.FromRgb(240, 244, 248)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = tab.Name
            };
            headerRow.Child = titleText;
            menuPanel.Children.Add(headerRow);

            // 2. İsim Değiştir
            var renameRow = CreateContextMenuRow("✏️  İsim Değiştir", () =>
            {
                CloseActiveContextMenu();
                PromptRenameTab(tab);
            });
            menuPanel.Children.Add(renameRow);

            // 3. Renk Değiştir
            var colorRow = CreateContextMenuRow("🎨  Renk Değiştir", () =>
            {
                CloseActiveContextMenu();
                PromptColorChangeTab(tab);
            });
            menuPanel.Children.Add(colorRow);

            // 4. Sekmeyi Sil
            if (_config.Tools?.SideMenu?.Tabs.Count > 1)
            {
                var deleteRow = CreateContextMenuRow("🗑️  Sekmeyi Sil", () =>
                {
                    CloseActiveContextMenu();
                    DeleteTab(tab);
                }, isDestructive: true);
                menuPanel.Children.Add(deleteRow);
            }

            var contextBorder = new Border
            {
                Width = menuWidth,
                Background = GetCachedBrush(Color.FromArgb(250, 22, 26, 34)),
                BorderBrush = GetCachedBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(5),
                Child = menuPanel,
                Effect = SharedContextMenuShadow
            };

            OpenContextMenuPopup(contextBorder);
        }

        private void PromptRenameTab(SideMenuTabConfig tab)
        {
            _isChildDialogOpen = true;
            try
            {
                var input = PromptSimpleInput("Sekme Adını Değiştir", "Yeni sekme adını girin:", tab.Name);
                if (!string.IsNullOrWhiteSpace(input))
                {
                    tab.Name = input.Trim();
                    SaveAndRefresh();
                }
            }
            finally
            {
                _isChildDialogOpen = false;
            }
        }

        private void PromptColorChangeTab(SideMenuTabConfig tab)
        {
            _isChildDialogOpen = true;
            try
            {
                var initialColor = string.IsNullOrWhiteSpace(tab.Color) ? "#FFAA00" : tab.Color;
                var dialog = new ColorPickerWindow(initialColor, hasFixedColor: !string.IsNullOrWhiteSpace(tab.Color))
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    tab.Color = dialog.ResetToThemeRequested ? string.Empty : dialog.SelectedHexColor;
                    SaveAndRefresh();
                }
            }
            finally
            {
                _isChildDialogOpen = false;
            }
        }

        private void DeleteTab(SideMenuTabConfig tab)
        {
            var sideMenu = _config.Tools?.SideMenu;
            if (sideMenu == null || sideMenu.Tabs.Count <= 1) return;

            var index = sideMenu.Tabs.IndexOf(tab);
            sideMenu.Tabs.Remove(tab);

            var nextTab = sideMenu.Tabs[Math.Max(0, index - 1)];
            sideMenu.ActiveTabId = nextTab.Id;
            sideMenu.Items = nextTab.Items;
            SaveAndRefresh();
        }

        private void CloseActiveContextMenu()
        {
            if (_activeContextMenuPopup != null)
            {
                _activeContextMenuPopup.IsOpen = false;
                _activeContextMenuPopup = null;
            }
            if (_activeContextMenu != null)
            {
                RootGrid.Children.Remove(_activeContextMenu);
                _activeContextMenu = null;
            }
        }

        private Border CreateContextMenuRow(string text, Action onClick, bool isDestructive = false)
        {
            var row = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            var tbText = new TextBlock
            {
                Text = text,
                Foreground = isDestructive ? GetCachedBrush(Color.FromRgb(255, 120, 120)) : GetCachedBrush(Color.FromRgb(235, 240, 246)),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            row.Child = tbText;

            row.MouseEnter += (_, __) =>
            {
                row.Background = isDestructive
                    ? GetCachedBrush(Color.FromArgb(50, 255, 80, 80))
                    : GetCachedBrush(Color.FromArgb(30, 255, 255, 255));
            };
            row.MouseLeave += (_, __) =>
            {
                row.Background = Brushes.Transparent;
            };

            row.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            return row;
        }

        // ==========================================
        // DİYALOGLAR (YENİDEN ADLANDIR & RENK DEĞİŞTİR)
        // ==========================================

        private void PromptRename(MenuItemConfig item)
        {
            _isChildDialogOpen = true;
            try
            {
                var input = PromptSimpleInput("Yeniden Adlandır", "Yeni adı girin:", item.Label);
                if (!string.IsNullOrWhiteSpace(input))
                {
                    item.Label = input.Trim();
                    SaveAndRefresh();
                }
            }
            finally
            {
                _isChildDialogOpen = false;
            }
        }

        private void PromptColorChange(MenuItemConfig item)
        {
            _isChildDialogOpen = true;
            try
            {
                var initialColor = string.IsNullOrWhiteSpace(item.FixedColor) ? "#202532" : item.FixedColor;
                var dialog = new ColorPickerWindow(initialColor, hasFixedColor: !string.IsNullOrWhiteSpace(item.FixedColor))
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    var chosenColor = dialog.ResetToThemeRequested ? string.Empty : dialog.SelectedHexColor;
                    item.FixedColor = chosenColor;

                    // Eğer klasörse, içindeki tüm uygulamaların sabit renklerini sıfırla ki klasör rengini dinamik miras alsınlar
                    if (item.IsCategory && item.Children != null)
                    {
                        foreach (var child in item.Children)
                        {
                            child.FixedColor = string.Empty;
                        }
                    }

                    SaveAndRefresh();
                }
            }
            finally
            {
                _isChildDialogOpen = false;
            }
        }

        private string? PromptSimpleInput(string title, string hint, string initial)
        {
            var win = new Window
            {
                Title = title,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 360,
                Height = 175,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var tb = new TextBox
            {
                Text = initial,
                Height = 36,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Background = GetCachedBrush(Color.FromRgb(26, 29, 36)),
                Foreground = Brushes.White,
                BorderBrush = GetCachedBrush(Color.FromRgb(70, 80, 95)),
                BorderThickness = new Thickness(1),
                CaretBrush = Brushes.White,
                Margin = new Thickness(0, 8, 0, 14)
            };

            var btnOk = new Button
            {
                Content = "Kaydet",
                Width = 85,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = GetCachedBrush(Color.FromRgb(240, 165, 0)),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var btnCancel = new Button
            {
                Content = "İptal",
                Width = 85,
                Height = 32,
                Background = GetCachedBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            string? result = null;
            btnOk.Click += (_, __) =>
            {
                result = tb.Text;
                win.DialogResult = true;
                win.Close();
            };
            btnCancel.Click += (_, __) =>
            {
                win.DialogResult = false;
                win.Close();
            };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = hint, Foreground = GetCachedBrush(Color.FromRgb(170, 180, 195)), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(tb);
            panel.Children.Add(btnPanel);

            var border = new Border
            {
                Background = GetCachedBrush(Color.FromArgb(248, 20, 23, 30)),
                BorderBrush = GetCachedBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18),
                Child = panel,
                Effect = SharedContextMenuShadow
            };

            win.Content = border;
            win.Loaded += (_, __) =>
            {
                tb.Focus();
                tb.SelectAll();
            };

            win.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    result = tb.Text;
                    win.DialogResult = true;
                    win.Close();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    win.DialogResult = false;
                    win.Close();
                    e.Handled = true;
                }
            };

            win.ShowDialog();
            return result;
        }

        // ==========================================
        // YARDIMCILAR & BELLEK OPTİMİZASYONU
        // ==========================================

        private static SolidColorBrush ResolveRowBrush(string? hexColor, bool isFolder)
        {
            if (!string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hexColor);
                    return GetCachedBrush(Color.FromArgb(170, color.R, color.G, color.B));
                }
                catch { }
            }

            return isFolder
                ? GetCachedBrush(Color.FromArgb(24, 255, 255, 255))
                : GetCachedBrush(Color.FromArgb(12, 255, 255, 255));
        }

        private static SolidColorBrush ResolveRowHoverBrush(string? hexColor)
        {
            if (!string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hexColor);
                    // Rengi belirgin şekilde aydınlat: Her kanala +60 ekle veya %40 artır, opaklığı 240 yap
                    var r = (byte)Math.Min(255, Math.Max(color.R + 60, (int)(color.R * 1.4)));
                    var g = (byte)Math.Min(255, Math.Max(color.G + 60, (int)(color.G * 1.4)));
                    var b = (byte)Math.Min(255, Math.Max(color.B + 60, (int)(color.B * 1.4)));
                    return GetCachedBrush(Color.FromArgb(240, r, g, b));
                }
                catch { }
            }

            return GetCachedBrush(Color.FromArgb(42, 255, 255, 255));
        }

        private static SolidColorBrush GetCachedBrush(Color color)
        {
            var key = $"{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            lock (BrushCache)
            {
                if (BrushCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                BrushCache[key] = brush;
                return brush;
            }
        }

        private static bool IsDescendantOf(DependencyObject node, DependencyObject target)
        {
            var current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, target)) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static string ResolveShortcutTarget(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath)) return shortcutPath;
            var ext = Path.GetExtension(shortcutPath);

            // Windows .lnk Kısayolu: Target executable veya iconLocation'ı çöz
            if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell != null)
                        {
                            dynamic? shortcut = shell.CreateShortcut(shortcutPath);
                            if (shortcut != null)
                            {
                                string iconLocation = shortcut.IconLocation;
                                if (!string.IsNullOrWhiteSpace(iconLocation))
                                {
                                    var parts = iconLocation.Split(',');
                                    var rawIconPath = parts[0].Trim();
                                    var iconPath = Environment.ExpandEnvironmentVariables(rawIconPath);
                                    if (File.Exists(iconPath))
                                    {
                                        return iconPath;
                                    }

                                    var localCandidate = TryNormalizeUncPath(iconPath);
                                    if (localCandidate != null && File.Exists(localCandidate))
                                    {
                                        return localCandidate;
                                    }
                                }

                                string targetPath = shortcut.TargetPath;
                                if (!string.IsNullOrWhiteSpace(targetPath))
                                {
                                    var expandedTarget = Environment.ExpandEnvironmentVariables(targetPath.Trim());
                                    if (File.Exists(expandedTarget) || Directory.Exists(expandedTarget))
                                    {
                                        return expandedTarget;
                                    }

                                    var localCandidate = TryNormalizeUncPath(expandedTarget);
                                    if (localCandidate != null && (File.Exists(localCandidate) || Directory.Exists(localCandidate)))
                                    {
                                        return localCandidate;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                var candidateFromBytes = TryExtractTargetFromLnkBytes(shortcutPath);
                if (!string.IsNullOrWhiteSpace(candidateFromBytes))
                {
                    return candidateFromBytes;
                }
            }
            // İnternet kısayolu (.url): IconFile değerini oku
            else if (string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(shortcutPath))
                    {
                        var lines = File.ReadAllLines(shortcutPath);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                            {
                                var iconFile = Environment.ExpandEnvironmentVariables(line.Substring(9).Trim());
                                if (File.Exists(iconFile))
                                {
                                    return iconFile;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return shortcutPath;
        }

        private static string? TryNormalizeUncPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                var idx = path.IndexOf(@"\Users\", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var userSub = path.Substring(idx);
                    var candidate = @"C:" + userSub;
                    if (File.Exists(candidate) || Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private static string? TryExtractTargetFromLnkBytes(string lnkPath)
        {
            try
            {
                if (!File.Exists(lnkPath)) return null;
                var bytes = File.ReadAllBytes(lnkPath);

                var ascii = System.Text.Encoding.ASCII.GetString(bytes);
                var matches = System.Text.RegularExpressions.Regex.Matches(ascii, @"[A-Za-z]:\\[^<>""|?*\x00-\x1F\r\n]+?\.(exe|ico|bat|cmd)");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (File.Exists(m.Value)) return m.Value;
                }

                var unicode = System.Text.Encoding.Unicode.GetString(bytes);
                var uMatches = System.Text.RegularExpressions.Regex.Matches(unicode, @"[A-Za-z]:\\[^<>""|?*\x00-\x1F\r\n]+?\.(exe|ico|bat|cmd)");
                foreach (System.Text.RegularExpressions.Match m in uMatches)
                {
                    if (File.Exists(m.Value)) return m.Value;
                }
            }
            catch { }
            return null;
        }

        private static ImageSource? TryExtractCleanShellIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                return null;
            }

            try
            {
                var sfi = new SHFILEINFO();
                var himlSys = SHGetFileInfo(path, 0, ref sfi, (uint)Marshal.SizeOf(sfi), ShgfiSysiconindex | ShgfiLargeIcon);
                if (himlSys == IntPtr.Zero && sfi.iIcon < 0)
                {
                    return null;
                }

                IntPtr hIcon = IntPtr.Zero;

                // 1. En yüksek kalite için Jumbo (256x256) dene
                IntPtr himlJumbo = IntPtr.Zero;
                try
                {
                    var iid = IidImageList;
                    if (SHGetImageList(ShilJumbo, ref iid, out himlJumbo) == 0 && himlJumbo != IntPtr.Zero)
                    {
                        hIcon = ImageList_GetIcon(himlJumbo, sfi.iIcon, IldNormal);
                    }
                }
                catch { }
                finally
                {
                    if (himlJumbo != IntPtr.Zero)
                    {
                        try { Marshal.Release(himlJumbo); } catch { }
                    }
                }

                // 2. ExtraLarge (48x48) dene
                if (hIcon == IntPtr.Zero)
                {
                    IntPtr himlExtra = IntPtr.Zero;
                    try
                    {
                        var iid = IidImageList;
                        if (SHGetImageList(ShilExtraLarge, ref iid, out himlExtra) == 0 && himlExtra != IntPtr.Zero)
                        {
                            hIcon = ImageList_GetIcon(himlExtra, sfi.iIcon, IldNormal);
                        }
                    }
                    catch { }
                    finally
                    {
                        if (himlExtra != IntPtr.Zero)
                        {
                            try { Marshal.Release(himlExtra); } catch { }
                        }
                    }
                }

                // 3. Standart Large (32x32) himlSys üzerinden al
                if (hIcon == IntPtr.Zero && himlSys != IntPtr.Zero)
                {
                    try
                    {
                        hIcon = ImageList_GetIcon(himlSys, sfi.iIcon, IldNormal);
                    }
                    catch { }
                }

                if (hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(
                            hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        var cropped = TrimOrCropEmptyMargins(bmp);
                        if (cropped.CanFreeze && !cropped.IsFrozen)
                        {
                            cropped.Freeze();
                        }

                        return cropped;
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch { }

            return null;
        }

        private static void CacheIcon(string key, ImageSource icon)
        {
            lock (IconCache)
            {
                if (IconCache.Count >= 120)
                {
                    // LRU: En eski 20 ikonu temizle, bellek kullanımını daima 18-25 MB arasında kilitle
                    var keysToRemove = new List<string>(20);
                    foreach (var k in IconCache.Keys)
                    {
                        keysToRemove.Add(k);
                        if (keysToRemove.Count >= 20) break;
                    }
                    foreach (var k in keysToRemove)
                    {
                        IconCache.Remove(k);
                    }
                }
                IconCache[key] = icon;
            }
        }

        private static BitmapSource TrimOrCropEmptyMargins(BitmapSource source)
        {
            try
            {
                if (source.PixelWidth <= 0 || source.PixelHeight <= 0) return source;

                var formatted = new FormatConvertedBitmap();
                formatted.BeginInit();
                formatted.Source = source;
                formatted.DestinationFormat = PixelFormats.Bgra32;
                formatted.EndInit();

                int width = formatted.PixelWidth;
                int height = formatted.PixelHeight;
                int stride = width * 4;
                int totalBytes = height * stride;

                // ArrayPool kullanarak her ikon kırpmada heap üzerinde 256KB'lık GC çöpü oluşmasını engelle
                byte[] pixels = System.Buffers.ArrayPool<byte>.Shared.Rent(totalBytes);
                try
                {
                    formatted.CopyPixels(pixels, stride, 0);

                    int minX = width;
                    int minY = height;
                    int maxX = -1;
                    int maxY = -1;

                    for (int y = 0; y < height; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            byte alpha = pixels[rowOffset + (x * 4) + 3];
                            if (alpha > 15)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }

                    if (maxX < minX || maxY < minY) return source;

                    int contentWidth = maxX - minX + 1;
                    int contentHeight = maxY - minY + 1;

                    // Eger ikon zaten alanin buyuk kismini kapliyorsa ve ortaliysa degistirme
                    if (contentWidth >= width * 0.85 && contentHeight >= height * 0.85 &&
                        minX <= width * 0.10 && (width - 1 - maxX) <= width * 0.10 &&
                        minY <= height * 0.10 && (height - 1 - maxY) <= height * 0.10)
                    {
                        return source;
                    }

                    int pad = Math.Max(1, Math.Min(4, Math.Min(contentWidth, contentHeight) / 16));
                    int cropX = Math.Max(0, minX - pad);
                    int cropY = Math.Max(0, minY - pad);
                    int cropW = Math.Min(width - cropX, contentWidth + (pad * 2));
                    int cropH = Math.Min(height - cropY, contentHeight + (pad * 2));

                    var cropped = new CroppedBitmap(formatted, new Int32Rect(cropX, cropY, cropW, cropH));
                    if (cropped.CanFreeze && !cropped.IsFrozen)
                    {
                        cropped.Freeze();
                    }
                    return cropped;
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(pixels);
                }
            }
            catch
            {
                return source;
            }
        }

        private static ImageSource? ResolveItemIcon(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return null;
            var expanded = Environment.ExpandEnvironmentVariables(targetPath.Trim());

            lock (IconCache)
            {
                if (IconCache.TryGetValue(expanded, out var cached))
                {
                    return cached;
                }
            }

            // Kısayol (.lnk / .url) ise doğrudan hedefin kendisini veya özel ikon dosyasını çöz.
            var resolvedTarget = ResolveShortcutTarget(expanded);

            // 1. Önce System ImageList üzerinden (SHGFI_SYSICONINDEX) temiz, ok simgesi olmayan yüksek çözünürlüklü ikonu çek.
            var icon = TryExtractCleanShellIcon(resolvedTarget);
            if (icon == null && !string.Equals(resolvedTarget, expanded, StringComparison.OrdinalIgnoreCase))
            {
                icon = TryExtractCleanShellIcon(expanded);
            }

            if (icon != null)
            {
                CacheIcon(expanded, icon);
                return icon;
            }

            try
            {
                // 2. Fallback: Standart yöntemler (hedef veya orijinal yol üzerinden)
                string pathToQuery = (File.Exists(resolvedTarget) || Directory.Exists(resolvedTarget)) ? resolvedTarget : expanded;
                if (File.Exists(pathToQuery))
                {
                    using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(pathToQuery);
                    if (sysIcon != null)
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(
                            sysIcon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        var cropped = TrimOrCropEmptyMargins(bmp);
                        if (cropped.CanFreeze && !cropped.IsFrozen)
                        {
                            cropped.Freeze();
                        }

                        CacheIcon(expanded, cropped);
                        return cropped;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
