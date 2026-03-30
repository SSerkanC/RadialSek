using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using ShapePath = System.Windows.Shapes.Path;
using WpfPoint = System.Windows.Point;
using RadialSek.Models;
using RadialSek.Services;
using Image = System.Windows.Controls.Image;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow : Window
    {
        public event EventHandler? MenuDismissed;

        private enum HoverHitShape
        {
            RingSector,
            Circle
        }

        private enum WeatherVisualPreset
        {
            ClearSkyDay,
            ClearSkyNight,
            MostlyClear,
            PartlyCloudy,
            Overcast,
            Fog,
            Drizzle,
            FreezingDrizzle,
            Rain,
            FreezingRain,
            Snow,
            SnowGrains,
            RainShowers,
            SnowShowers,
            Thunderstorm,
            ThunderstormHail
        }

        private sealed class SegmentInteraction
        {
            public SegmentInteraction(ShapePath segment, Border iconHost, MenuItemConfig item, double startAngle, double endAngle, int itemIndex)
            {
                Segment = segment;
                IconHost = iconHost;
                Item = item;
                StartAngle = startAngle;
                EndAngle = endAngle;
                ItemIndex = itemIndex;
            }

            public ShapePath Segment { get; }
            public Border IconHost { get; }
            public MenuItemConfig Item { get; }
            public double StartAngle { get; }
            public double EndAngle { get; }
            public int ItemIndex { get; }
        }

        private sealed class HoverInteraction
        {
            public HoverInteraction(
                ShapePath segment,
                Border iconHost,
                MenuItemConfig? item,
                List<MenuItemConfig> ownerItems,
                int itemIndex,
                int depth,
                double startAngle,
                double endAngle,
                IReadOnlyList<string>? breadcrumbPath,
                SolidColorBrush segmentBrush,
                System.Windows.Media.Color accentColor,
                bool isGhostSlot)
            {
                Segment = segment;
                IconHost = iconHost;
                Item = item;
                OwnerItems = ownerItems;
                ItemIndex = itemIndex;
                Depth = depth;
                StartAngle = startAngle;
                EndAngle = endAngle;
                BreadcrumbPath = breadcrumbPath ?? Array.Empty<string>();
                SegmentBrush = segmentBrush;
                AccentColor = accentColor;
                IsGhostSlot = isGhostSlot;
                HitShape = HoverHitShape.RingSector;
                HitCenter = new WpfPoint();
            }

            public ShapePath Segment { get; }
            public Border IconHost { get; }
            public MenuItemConfig? Item { get; }
            public List<MenuItemConfig> OwnerItems { get; }
            public int ItemIndex { get; }
            public int Depth { get; }
            public double StartAngle { get; }
            public double EndAngle { get; }
            public IReadOnlyList<string> BreadcrumbPath { get; }
            public SolidColorBrush SegmentBrush { get; }
            public System.Windows.Media.Color AccentColor { get; }
            public bool IsGhostSlot { get; }
            public HoverHitShape HitShape { get; set; }
            public double InnerRadius { get; set; }
            public double OuterRadius { get; set; }
            public WpfPoint HitCenter { get; set; }
            public double HitRadius { get; set; }
        }

        private sealed class DragItemInteraction
        {
            public DragItemInteraction(
                MenuItemConfig? item,
                List<MenuItemConfig> ownerItems,
                int itemIndex,
                int depth,
                double startAngle,
                double endAngle,
                IReadOnlyList<string>? breadcrumbPath = null,
                bool isGhostSlot = false,
                int? insertIndex = null)
            {
                Item = item;
                OwnerItems = ownerItems;
                ItemIndex = itemIndex;
                Depth = depth;
                StartAngle = startAngle;
                EndAngle = endAngle;
                BreadcrumbPath = breadcrumbPath ?? Array.Empty<string>();
                IsGhostSlot = isGhostSlot;
                InsertIndex = insertIndex ?? (itemIndex + 1);
            }

            public MenuItemConfig? Item { get; }
            public List<MenuItemConfig> OwnerItems { get; }
            public int ItemIndex { get; }
            public int Depth { get; }
            public double StartAngle { get; }
            public double EndAngle { get; }
            public IReadOnlyList<string> BreadcrumbPath { get; }
            public bool IsGhostSlot { get; }
            public int InsertIndex { get; }
        }

        private sealed class DragPayload
        {
            public DragPayload(DragItemInteraction source)
            {
                Source = source;
            }

            public DragItemInteraction Source { get; }
        }

        private sealed class LayoutProfile
        {
            public double InnerRadius { get; set; }
            public double OuterRadius { get; set; }
            public double IconRadius { get; set; }
            public double IconSize { get; set; }
            public double SegmentGap { get; set; }
            public double StartAngle { get; set; }
            public double SweepAngle { get; set; }
            public double CenterSize { get; set; }
            public double CenterBorder { get; set; }
            public double CenterBlur { get; set; }
            public double ShadowBlur { get; set; }
            public double ShadowPad { get; set; }
            public byte ShadowAlpha { get; set; }
            public double SegmentStrokeThickness { get; set; }
            public double SegmentBlur { get; set; }
            public double SegmentShadowOpacity { get; set; }
            public double TitleFontSize { get; set; }
            public double SubtitleFontSize { get; set; }
            public double HoverIconSize { get; set; }
            public double HoverOpacity { get; set; }
            public bool ShowHoverLabel { get; set; }
            public bool ShowIconChrome { get; set; }
            public bool UseCompactChevron { get; set; }
            public double SubmenuInset { get; set; }
            public double SubmenuInnerRadius { get; set; }
            public double SubmenuOuterRadius { get; set; }
            public double SubmenuIconRadius { get; set; }
            public double SubmenuIconSize { get; set; }
            public double SubmenuStrokeThickness { get; set; }
            public double SubmenuHoverOpacity { get; set; }
            public double SubmenuExpandFactor { get; set; }
            public double SubmenuMaxSweep { get; set; }
            public double SubmenuMinPerItemSweep { get; set; }
        }

        private readonly LauncherService _launcherService;
        private readonly MenuConfig _config;
        private readonly MenuConfigService _configService;
        private readonly SoundManager _soundManager;
        private readonly MenuFeatures _features;
        private readonly bool _enableGradientRingAnimations;
        private readonly ThemePalette _theme;
        private readonly string _menuStyle;
        private readonly string _openAnimationStyle;
        private readonly string _targetingModeStyle;
        private readonly string _categoryStripStyle;
        private readonly string _categoryStripFont;
        private readonly System.Windows.Media.FontFamily _centerClockFontFamily;
        private readonly double _categoryStripOpacity;
        private readonly double _categoryStripFontOpacity;
        private readonly System.Windows.Media.Color _categoryStripFontColor;
        private readonly double _innerGradientRingThicknessScale;
        private readonly double _outerGradientRingThicknessScale;
        private readonly double _menuBackdropBlurSizeScale;
        private readonly double _menuBackdropBlurStrengthScale;
        private readonly ActivationShortcut _targetingShortcut;
        private readonly OpenMeteoWeatherService _weatherService;
        private readonly WeatherSettings _weatherSettings;
        private readonly Random _weatherRandom = new Random();
        private readonly List<List<MenuItemConfig>> _pages = new List<List<MenuItemConfig>>();
        private readonly List<ShapePath> _segments = new List<ShapePath>();
        private readonly Dictionary<int, List<UIElement>> _submenuLayers = new Dictionary<int, List<UIElement>>();
        private readonly Dictionary<ShapePath, SolidColorBrush> _segmentBrushes = new Dictionary<ShapePath, SolidColorBrush>();
        private readonly Dictionary<ShapePath, SegmentInteraction> _segmentInteractions = new Dictionary<ShapePath, SegmentInteraction>();
        private readonly Dictionary<Border, SegmentInteraction> _iconInteractions = new Dictionary<Border, SegmentInteraction>();
        private readonly Dictionary<DependencyObject, DragItemInteraction> _dragInteractions = new Dictionary<DependencyObject, DragItemInteraction>();
        private readonly Dictionary<DependencyObject, HoverInteraction> _hoverInteractions = new Dictionary<DependencyObject, HoverInteraction>();
        private readonly List<HoverInteraction> _hoverInteractionEntries = new List<HoverInteraction>();
        private readonly Dictionary<TextBlock, int> _centerTextAnimationVersions = new Dictionary<TextBlock, int>();
        private readonly HashSet<FrameworkElement> _weatherVisualElements = new HashSet<FrameworkElement>();
        private static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, LinkedListNode<string>> IconCacheNodes = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> IconCacheUsage = new LinkedList<string>();
        private static readonly Dictionary<string, ImageSource> ExtensionIconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PathSpecificIconExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe",
            ".lnk",
            ".ico",
            ".url",
            ".bat",
            ".cmd",
            ".com",
            ".scr"
        };
        private static readonly ImageSource FallbackIconImage = CreateFallbackIconImage();
        private static readonly ImageSource? WeatherCloudSpriteSource = TryLoadWeatherCloudSprite();
        private static readonly System.Windows.Media.Color[] NeonOrbitPalette =
        {
            System.Windows.Media.Color.FromRgb(57, 255, 110),
            System.Windows.Media.Color.FromRgb(232, 34, 255),
            System.Windows.Media.Color.FromRgb(0, 180, 255),
            System.Windows.Media.Color.FromRgb(255, 174, 0),
            System.Windows.Media.Color.FromRgb(147, 50, 255),
            System.Windows.Media.Color.FromRgb(0, 216, 201),
            System.Windows.Media.Color.FromRgb(255, 46, 123),
            System.Windows.Media.Color.FromRgb(255, 222, 0)
        };
        private readonly DispatcherTimer _dragHoverOpenTimer;
        private readonly DispatcherTimer _modifierPollTimer;
        private readonly DispatcherTimer _persistChangesTimer;
        private readonly DispatcherTimer _submenuCloseGraceTimer;
        private readonly DispatcherTimer _openAnimationFinalizeTimer;
        private readonly DispatcherTimer _prepareForDisplayDelayTimer;
        private readonly DispatcherTimer _monochromeBackdropCaptureTimer;
        private readonly DispatcherTimer _monochromeRingSnapshotTimer;
        private readonly DispatcherTimer _centerClockTimer;
        private static readonly SolidColorBrush BackdropHitTestBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
        private static readonly System.Windows.Media.FontFamily CenterTitleDefaultFontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold");
        private static readonly DoubleCollection MainGhostDashArray = CreateFrozenDoubleCollection(4, 4);
        private static readonly DoubleCollection SubmenuGhostDashArray = CreateFrozenDoubleCollection(3, 3);
        private static readonly DoubleCollection EditModeRingDashArray = CreateFrozenDoubleCollection(1.6, 3.2, 8.8, 3.2);
        private static readonly Dictionary<int, SolidColorBrush> FrozenBrushCache = new Dictionary<int, SolidColorBrush>();
        private static readonly Dictionary<(int StartColor, int EndColor, int StartX, int StartY, int EndX, int EndY), LinearGradientBrush> FrozenTwoStopGradientBrushCache = new Dictionary<(int, int, int, int, int, int), LinearGradientBrush>();
        private static readonly Dictionary<(int Color1, int Offset1, int Color2, int Offset2, int Color3, int Offset3, int StartX, int StartY, int EndX, int EndY), LinearGradientBrush> FrozenThreeStopGradientBrushCache = new Dictionary<(int, int, int, int, int, int, int, int, int, int), LinearGradientBrush>();
        private static readonly Dictionary<int, BlurEffect> BlurEffectCache = new Dictionary<int, BlurEffect>();
        private static readonly Dictionary<(int Blur, int Opacity, int Color, int Depth), DropShadowEffect> ShadowEffectCache = new Dictionary<(int Blur, int Opacity, int Color, int Depth), DropShadowEffect>();
        private HwndSource? _hwndSource;
        private ShapePath? _activeSegment;
        private Border? _activeIconHost;
        private ShapePath? _activeAccentPath;
        private Ellipse? _hoverOrbitRing;
        private ShapePath? _hoverOrbitPointer;
        private PathGeometry? _hoverOrbitPointerGeometry;
        private PathFigure? _hoverOrbitPointerFigure;
        private LineSegment? _hoverOrbitPointerLeftSegment;
        private QuadraticBezierSegment? _hoverOrbitPointerRearSegment;
        private SegmentInteraction? _activeInteraction;
        private HoverInteraction? _activeSubmenuInteraction;
        private HoverInteraction? _dragPreviewInteraction;
        private ShapePath? _externalFileDropGhostSlice;
        private Border? _externalFileDropGhostBadge;
        private ScaleTransform? _externalFileDropGhostBadgeScale;
        private HoverInteraction? _externalFileDropGhostTarget;
        private int _externalFileDropGhostInsertIndex = -1;
        private Border? _itemContextMenu;
        private Ellipse? _editModeRing;
        private Ellipse? _editModeFocusHaze;
        private Border? _categoryNameStrip;
        private Ellipse? _categoryBridgeDot;
        private readonly List<UIElement> _altGuideVisuals = new List<UIElement>();
        private DragItemInteraction? _contextMenuTarget;
        private DragItemInteraction? _selectedInteraction;
        private double _centerX;
        private double _centerY;
        private TextBlock? _centerBreadcrumb;
        private TextBlock? _centerTitle;
        private TextBlock? _centerSubtitle;
        private double _centerTitleBaseFontSize;
        private bool _isCenterClockTypographyActive;
        private DateTime? _shutdownCountdownTargetUtc;
        private Grid? _utilityDockButton;
        private Border? _utilityDockButtonCore;
        private FrameworkElement? _weatherCloudVisual;
        private FrameworkElement? _weatherCloudRearVisual;
        private Border? _centerPanel;
        private Ellipse? _centerAccentRing;
        private System.Windows.Media.Brush? _centerAccentRingBaseStroke;
        private double _centerAccentRingBaseOpacity;
        private double _centerAccentRingBaseStrokeThickness;
        private Ellipse? _centerAccentRingSolidOverlay;
        private Ellipse? _outerAccentRing;
        private System.Windows.Media.Brush? _outerAccentRingBaseStroke;
        private double _outerAccentRingBaseOpacity;
        private double _outerAccentRingBaseStrokeThickness;
        private Ellipse? _outerAccentRingSolidOverlay;
        private int _currentPageIndex;
        private bool _isPaging;
        private bool _isEditModeEnabled;
        private bool _isAltGuideActive;
        private bool _isOdakKaskadiAnimationActive;
        private bool _isTargetingCursorHidden;
        private bool _wasAltPressed;
        private bool _wasShiftPressed;
        private MenuItemConfig? _selectedItem;
        private DragItemInteraction? _pendingDragInteraction;
        private DependencyObject? _pendingDragSource;
        private DragItemInteraction? _dragHoverCategoryTarget;
        private MenuItemConfig? _dragHoverOpenedCategory;
        private int _dragHoverOpenedFromDepth;
        private WpfPoint _dragStartPoint;
        private bool _isWindowClickThroughEnabled;
        private bool _hasPendingConfigSave;
        private bool _suppressNextDismissSound;
        private bool _isDismissing;
        private DateTime _ignoreBackdropMouseUntilUtc;
        private bool _isMonochromeSnapshotTransitionRunning;
        private bool _hasPendingMonochromeSnapshotRequest;
        private Ellipse? _monochromeSnapshotCenterRing;
        private Ellipse? _monochromeSnapshotOuterRing;
        private double _monochromeSnapshotCenterRestoreOpacity;
        private double _monochromeSnapshotOuterRestoreOpacity;
        private readonly HashSet<UIElement> _odakKaskadiAnimatedSegments = new HashSet<UIElement>();
        private readonly Dictionary<Border, WpfPoint> _odakKaskadiIconFinalPositions = new Dictionary<Border, WpfPoint>();
        private bool _hoverOrbitHasState;
        private double _hoverOrbitLastOuterRadius = double.NaN;
        private double _hoverOrbitLastAngle = double.NaN;
        private System.Windows.Media.Color _hoverOrbitLastAccentColor = Colors.Transparent;
        private DateTime _hoverOrbitLastUpdateUtc = DateTime.MinValue;
        private int _weatherFetchVersion;
        private bool _hasWeatherVisualSignature;
        private int _weatherVisualSignature;
        private double _activeWeatherSpeedScale = 1.0;
        private double _activeWeatherIntensityScale = 1.0;

        private const string InternalDragFormat = "RadialSek.MainItemDrag";
        private static readonly TimeSpan DragHoverOpenDelay = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan PersistChangesDelay = TimeSpan.FromMilliseconds(320);
        private static readonly TimeSpan SubmenuCloseGraceDelay = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan BackdropClickGraceDelay = TimeSpan.FromMilliseconds(220);
        private static readonly TimeSpan MonochromeBackdropCaptureDelay = TimeSpan.FromMilliseconds(260);
        private static readonly TimeSpan MonochromeRingFadeOutDuration = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan MonochromeRingFadeOutCaptureBuffer = TimeSpan.FromMilliseconds(24);
        private static readonly TimeSpan MonochromeRingFadeInDuration = TimeSpan.FromMilliseconds(160);
        private static readonly TimeSpan MonochromeBackdropFadeDuration = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MonochromeBackdropStartDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan HoverOrbitGuideTransitionDuration = TimeSpan.FromMilliseconds(140);
        private static readonly TimeSpan HoverOrbitMinUpdateInterval = TimeSpan.FromMilliseconds(14);
        private static readonly TimeSpan WeatherCloudSlideDuration = TimeSpan.FromMilliseconds(6800);
        private static readonly TimeSpan WeatherCloudFadeInDuration = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan WeatherLoopDuration = TimeSpan.FromMilliseconds(3000);
        private static readonly TimeSpan WeatherCycleDuration = TimeSpan.FromMilliseconds(9200);
        private static readonly int WeatherRenderTier = RenderCapability.Tier >> 16;
        private static readonly CubicEase WeatherCloudMovementEase = FreezeIfPossible(new CubicEase { EasingMode = EasingMode.EaseInOut });
        private static readonly SineEase WeatherCloudFadeInEase = FreezeIfPossible(new SineEase { EasingMode = EasingMode.EaseOut });
        private static readonly SineEase WeatherLabelFadeInEase = FreezeIfPossible(new SineEase { EasingMode = EasingMode.EaseOut });
        private static readonly SineEase WeatherLabelFadeOutEase = FreezeIfPossible(new SineEase { EasingMode = EasingMode.EaseIn });
        private const double HoverOrbitPointerScale = 1.5;
        private const double HoverOrbitAngleSnap = 0.5;
        private const double SubmenuHoverScale = 1.08;
        private const double WeatherCloudWidthScale = 1.06;
        private const double WeatherCloudHeightScale = 0.55;
        private const double WeatherCloudRearScale = 0.93;
        private const double WeatherCloudStartOffsetXScale = 0.35;
        private const double WeatherCloudStartOffsetYScale = 0.28;
        private const double WeatherCloudEndOffsetXScale = -0.43;
        private const double WeatherCloudEndOffsetYScale = 0.31;
        private const double WeatherCloudRearStartOffsetXScale = -0.46;
        private const double WeatherCloudRearStartOffsetYScale = -0.16;
        private const double WeatherCloudRearEndOffsetXScale = 0.34;
        private const double WeatherCloudRearEndOffsetYScale = -0.10;
        private const int IconCacheLimit = 160;
        private const int WeatherCloudZIndex = 210;
        private const int WeatherCloudRearZIndex = -14;
        private const int WeatherPrecipitationZIndex = 206;
        private const int WeatherFogZIndex = 198;
        private const int WeatherCelestialZIndex = 202;
        private const int WeatherLightningZIndex = 240;
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int HtClient = 1;
        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;
        private const int OdakKaskadiCenterAccentHoldMs = 1000;
        private const int OdakKaskadiCenterAccentPeakMs = 1500;
        private const int OdakKaskadiCenterAccentReturnMs = 3500;
        private const int OdakKaskadiFinalizeBufferMs = 80;
        private const double CenterClockFontScale = 2.0;
        private const int RingAmbientAnimationFps = 30;
        private const int RingSequenceAnimationFps = 40;
        private const int OverlayContextMenuZIndex = 1200;
        private const string UtilityDockTrayTag = "UtilityDockTray";
        private const string UtilityShutdownTrayTag = "UtilityShutdownTray";

        static RadialOverlayWindow()
        {
            if (BackdropHitTestBrush.CanFreeze)
            {
                BackdropHitTestBrush.Freeze();
            }
        }

        public RadialOverlayWindow(double centerX, double centerY, MenuConfig config, LauncherService launcherService, MenuConfigService configService)
        {
            InitializeComponent();
            _config = config;
            _launcherService = launcherService;
            _configService = configService;
            _soundManager = SoundManager.Instance;
            _soundManager.ApplyConfig(config);
            _features = config.Features ?? new MenuFeatures();
            _enableGradientRingAnimations = _features.EnableGradientRingAnimations;
            _menuStyle = string.IsNullOrWhiteSpace(config.MenuStyle) ? "Style1" : config.MenuStyle;
            _openAnimationStyle = MenuOpenAnimationService.ResolveKey(config.OpenAnimationStyle);
            _targetingModeStyle = TargetingModeStyleService.ResolveKey(config.TargetingModeStyle);
            _categoryStripStyle = CategoryStripStyleService.ResolveKey(config.CategoryStripStyle);
            _categoryStripFont = CategoryStripFontService.Resolve(config.CategoryStripFont).Key;
            _centerClockFontFamily = CenterClockFontService.CreateFontFamily(config.CenterClockFont);
            _categoryStripOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripOpacity <= 0 ? 0.98 : config.CategoryStripOpacity));
            _categoryStripFontOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripFontOpacity <= 0 ? 1.0 : config.CategoryStripFontOpacity));
            _categoryStripFontColor = ParseConfiguredColor(config.CategoryStripFontColor, System.Windows.Media.Color.FromRgb(250, 252, 255));
            _innerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.InnerGradientRingThicknessScale <= 0 ? 1.0 : config.InnerGradientRingThicknessScale));
            _outerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.OuterGradientRingThicknessScale <= 0 ? 1.0 : config.OuterGradientRingThicknessScale));
            _menuBackdropBlurSizeScale = Math.Max(0.6, Math.Min(2.5, config.MenuBackdropBlurSizeScale <= 0 ? 1.0 : config.MenuBackdropBlurSizeScale));
            _menuBackdropBlurStrengthScale = Math.Max(0.4, Math.Min(2.5, config.MenuBackdropBlurStrengthScale <= 0 ? 1.0 : config.MenuBackdropBlurStrengthScale));
            _targetingShortcut = config.TargetingShortcut?.Clone() ?? ActivationShortcut.CreateTargetingModeDefault();
            _weatherService = OpenMeteoWeatherService.Instance;
            _weatherSettings = config.Weather?.Clone() ?? new WeatherSettings();
            _theme = ThemePaletteService.Resolve(config.Theme);
            _dragHoverOpenTimer = new DispatcherTimer { Interval = DragHoverOpenDelay };
            _dragHoverOpenTimer.Tick += OnDragHoverOpenTimerTick;
            _modifierPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _modifierPollTimer.Tick += (_, __) => PollModifierState();
            _persistChangesTimer = new DispatcherTimer { Interval = PersistChangesDelay };
            _persistChangesTimer.Tick += OnPersistChangesTimerTick;
            _submenuCloseGraceTimer = new DispatcherTimer { Interval = SubmenuCloseGraceDelay };
            _submenuCloseGraceTimer.Tick += OnSubmenuCloseGraceTimerTick;
            _openAnimationFinalizeTimer = new DispatcherTimer();
            _openAnimationFinalizeTimer.Tick += OnOpenAnimationFinalizeTimerTick;
            _prepareForDisplayDelayTimer = new DispatcherTimer();
            _prepareForDisplayDelayTimer.Tick += OnPrepareForDisplayDelayTimerTick;
            _monochromeBackdropCaptureTimer = new DispatcherTimer();
            _monochromeBackdropCaptureTimer.Tick += OnMonochromeBackdropCaptureTimerTick;
            _monochromeRingSnapshotTimer = new DispatcherTimer();
            _monochromeRingSnapshotTimer.Tick += OnMonochromeRingSnapshotTimerTick;
            _centerClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _centerClockTimer.Tick += OnCenterClockTimerTick;
            SourceInitialized += OnSourceInitialized;
            Deactivated += OnWindowDeactivated;
            NormalizeConfigPages(config);
            Closed += (_, __) =>
            {
                _dragHoverOpenTimer.Stop();
                _modifierPollTimer.Stop();
                _persistChangesTimer.Stop();
                _submenuCloseGraceTimer.Stop();
                _openAnimationFinalizeTimer.Stop();
                _prepareForDisplayDelayTimer.Stop();
                _monochromeBackdropCaptureTimer.Stop();
                _monochromeRingSnapshotTimer.Stop();
                _centerClockTimer.Stop();
                StopWeatherVisuals();
                SetTargetingCursorHidden(false);
                ForceReleaseInputState("Overlay kapatilirken fail-safe");
                if (_hwndSource != null)
                {
                    SetWindowClickThrough(false);
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }
            };
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
            _hwndSource?.AddHook(WndProc);
            SetWindowClickThrough(false);
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            if (!IsVisible || _isDismissing)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    if (!IsVisible || _isDismissing)
                    {
                        return;
                    }

                    Dismiss();
                }));
        }

        private void ForceReleaseInputState(string logContext)
        {
            try
            {
                if (Mouse.Captured != null)
                {
                    Mouse.Capture(null);
                }
            }
            catch (Exception ex)
            {
                LogUiException(logContext + " (mouse capture birakilirken hata)", ex);
            }

            try
            {
                Keyboard.ClearFocus();
            }
            catch (Exception ex)
            {
                LogUiException(logContext + " (focus temizlenirken hata)", ex);
            }

            try
            {
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                LogUiException(logContext + " (cursor sifirlanirken hata)", ex);
            }

            try
            {
                SetWindowClickThrough(false);
            }
            catch (Exception ex)
            {
                LogUiException(logContext + " (click-through kapatilirken hata)", ex);
            }
        }

        public void ReopenAt(double centerX, double centerY)
        {
            var wasVisible = IsVisible;
            ResetTransientStateForReuse();
            BuildMenu(centerX, centerY, _config);
            _ignoreBackdropMouseUntilUtc = DateTime.UtcNow + BackdropClickGraceDelay;
            BeginAnimation(OpacityProperty, null);
            RootCanvas.BeginAnimation(OpacityProperty, null);
            Opacity = 0.0;
            RootCanvas.Opacity = 0.0;

            if (!IsVisible)
            {
                Show();
            }

            if (wasVisible && _features.EnableMonochromeBackdrop)
            {
                _prepareForDisplayDelayTimer.Stop();
                _prepareForDisplayDelayTimer.Interval = MonochromeBackdropCaptureDelay;
                _prepareForDisplayDelayTimer.Start();
                return;
            }

            FinalizePrepareForDisplay();
        }

        public void Dismiss()
        {
            if (_isDismissing)
            {
                return;
            }

            _isDismissing = true;
            var shouldPlayDismissSound = !_suppressNextDismissSound;
            _suppressNextDismissSound = false;
            var wasVisible = IsVisible;

            try
            {
                try
                {
                    ResetTransientStateForReuse();
                }
                catch (Exception ex)
                {
                    LogUiException("Dismiss sirasinda gecici durum sifirlanirken hata", ex);
                }

                ForceReleaseInputState("Dismiss fail-safe");

                try
                {
                    if (IsVisible)
                    {
                        Hide();
                    }
                }
                catch (Exception ex)
                {
                    LogUiException("Dismiss sirasinda pencere gizlenirken hata", ex);
                }

                ForceReleaseInputState("Dismiss sonrasi fail-safe");

                if (shouldPlayDismissSound && wasVisible)
                {
                    PlayUiSound(SoundCue.MenuClose);
                }
            }
            finally
            {
                _isDismissing = false;
                MenuDismissed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RequestLaunchAndDismiss(string targetPath, int launchDelayMs = 0)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            void LaunchAction()
            {
                Mouse.Capture(null);
                Keyboard.ClearFocus();
                _suppressNextDismissSound = true;
                Dismiss();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _launcherService.Launch(targetPath);
                    }
                    catch (Exception ex)
                    {
                        LogUiException("Program baslatilirken hata", ex);
                        PlayUiSound(SoundCue.Error);
                    }
                }), DispatcherPriority.Background);
            }

            if (launchDelayMs <= 0)
            {
                LaunchAction();
                return;
            }

            var delayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Clamp(launchDelayMs, 20, 220))
            };
            delayTimer.Tick += (_, __) =>
            {
                delayTimer.Stop();
                LaunchAction();
            };
            delayTimer.Start();
        }

        public void FlushPendingChanges()
        {
            if (!_hasPendingConfigSave)
            {
                return;
            }

            _persistChangesTimer.Stop();
            _hasPendingConfigSave = false;
            _configService.SaveConfig(_config);
        }

        public void DisposeWindow()
        {
            FlushPendingChanges();
            _persistChangesTimer.Stop();
            Close();
        }

        public static void ReleaseIdleResources()
        {
            lock (IconCache)
            {
                IconCache.Clear();
                IconCacheNodes.Clear();
                IconCacheUsage.Clear();
                ExtensionIconCache.Clear();
            }

            FrozenBrushCache.Clear();
            FrozenTwoStopGradientBrushCache.Clear();
            FrozenThreeStopGradientBrushCache.Clear();
            BlurEffectCache.Clear();
            ShadowEffectCache.Clear();
        }

        private void PrepareForDisplay()
        {
            UpdateMonochromeBackdropLayer();
            UpdateBackdropInteractivity();
            Opacity = 1.0;
            RootCanvas.Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            Focus();
            Keyboard.Focus(BackdropRoot);
            SyncModifierStateSnapshot();
            if (_wasAltPressed)
            {
                BeginAltGuideMode();
            }
            _modifierPollTimer.Start();
            _centerClockTimer.Start();
            RefreshIdleCenterTitleIfNeeded();
            UpdateEditModeClickThroughState();

            if (!string.Equals(_openAnimationStyle, "None", StringComparison.OrdinalIgnoreCase))
            {
                PlayOpenAnimation();
            }

            StartWeatherVisuals();
            PlayUiSound(SoundCue.MenuOpen);
        }

        private void FinalizePrepareForDisplay()
        {
            PrepareForDisplay();
            Activate();
            Focus();
            Keyboard.Focus(BackdropRoot);
        }

        private void OnPrepareForDisplayDelayTimerTick(object? sender, EventArgs e)
        {
            _prepareForDisplayDelayTimer.Stop();
            if (!IsVisible)
            {
                return;
            }

            FinalizePrepareForDisplay();
        }

        private void ResetTransientStateForReuse()
        {
            _modifierPollTimer.Stop();
            _dragHoverOpenTimer.Stop();
            _openAnimationFinalizeTimer.Stop();
            _prepareForDisplayDelayTimer.Stop();
            _monochromeBackdropCaptureTimer.Stop();
            _monochromeRingSnapshotTimer.Stop();
            _centerClockTimer.Stop();
            StopWeatherVisuals();
            _isMonochromeSnapshotTransitionRunning = false;
            _hasPendingMonochromeSnapshotRequest = false;
            _monochromeSnapshotCenterRing = null;
            _monochromeSnapshotOuterRing = null;
            _monochromeSnapshotCenterRestoreOpacity = 0.0;
            _monochromeSnapshotOuterRestoreOpacity = 0.0;
            EnsureOdakKaskadiAnimationSettled();
            ClearPendingDragState(clearPreview: true);
            ClearExternalFileDropGhostSlice();
            SetWindowClickThrough(false);
            _dragHoverCategoryTarget = null;
            _dragHoverOpenedCategory = null;
            _dragHoverOpenedFromDepth = 0;
            _wasAltPressed = false;
            _wasShiftPressed = false;
            _isAltGuideActive = false;
            SetTargetingCursorHidden(false);
            HideAltGuideLine();
            HideItemContextMenu();
            ClearPendingDragCategoryOpen(resetSubmenu: false);
            ResetEditModeForReuse();
            ClearMonochromeBackdropLayer();
            HideHoverOrbitGuide();
            UpdateBackdropInteractivity();
        }

        private void SyncModifierStateSnapshot()
        {
            _wasShiftPressed = (GetAsyncKeyState(VkShift) & 0x8000) != 0;
            _wasAltPressed = IsTargetingShortcutPressed();
        }

        private void ResetEditModeForReuse()
        {
            _isEditModeEnabled = false;
            if (_editModeFocusHaze != null)
            {
                RootCanvas.Children.Remove(_editModeFocusHaze);
                _editModeFocusHaze = null;
            }

            if (_editModeRing != null)
            {
                RootCanvas.Children.Remove(_editModeRing);
                _editModeRing = null;
            }
        }

        private void ScheduleConfigSave()
        {
            _hasPendingConfigSave = true;
            _persistChangesTimer.Stop();
            _persistChangesTimer.Start();
        }

        private void StartWeatherCloudAnimation(
            double frontOpacity = 0.96,
            double rearOpacity = 0.76,
            double cloudScale = 1.0)
        {
            if (RootCanvas == null || !IsVisible)
            {
                return;
            }

            StopWeatherCloudAnimation();

            var profile = GetLayoutProfile();
            var centerSize = _centerPanel != null
                ? Math.Min(_centerPanel.Width, _centerPanel.Height)
                : profile.CenterSize;
            if (centerSize <= 0)
            {
                return;
            }

            var cloudWidth = Math.Clamp(centerSize * WeatherCloudWidthScale * Math.Max(0.55, Math.Min(1.8, cloudScale)), 120, 280);
            var cloudHeight = ResolveWeatherCloudHeight(cloudWidth, centerSize);
            var lightweightCloud = ShouldUseLightweightCloudEffects();
            var cloud = CreateWeatherCloudVisual(cloudWidth, cloudHeight, lightweightCloud);
            _weatherCloudVisual = cloud;
            BeginWeatherCloudTrack(
                cloud,
                startOffsetX: centerSize * WeatherCloudStartOffsetXScale,
                startOffsetY: centerSize * WeatherCloudStartOffsetYScale,
                endOffsetX: centerSize * WeatherCloudEndOffsetXScale,
                endOffsetY: centerSize * WeatherCloudEndOffsetYScale,
                peakOpacity: Math.Max(0.2, Math.Min(1.0, frontOpacity)),
                zIndex: WeatherCloudZIndex);

            var rearCloudWidth = cloudWidth * WeatherCloudRearScale;
            var rearCloudHeight = ResolveWeatherCloudHeight(rearCloudWidth, centerSize);
            var rearCloud = CreateWeatherCloudVisual(rearCloudWidth, rearCloudHeight, lightweightCloud);
            _weatherCloudRearVisual = rearCloud;
            BeginWeatherCloudTrack(
                rearCloud,
                startOffsetX: centerSize * WeatherCloudRearStartOffsetXScale,
                startOffsetY: centerSize * WeatherCloudRearStartOffsetYScale,
                endOffsetX: centerSize * WeatherCloudRearEndOffsetXScale,
                endOffsetY: centerSize * WeatherCloudRearEndOffsetYScale,
                peakOpacity: Math.Max(0.16, Math.Min(0.96, rearOpacity)),
                zIndex: WeatherCloudRearZIndex);
        }

        private void BeginWeatherCloudTrack(
            FrameworkElement cloud,
            double startOffsetX,
            double startOffsetY,
            double endOffsetX,
            double endOffsetY,
            double peakOpacity,
            int zIndex)
        {
            var movementDuration = ScaleWeatherDuration(WeatherCloudSlideDuration.TotalMilliseconds);
            var fadeInDuration = ScaleWeatherDuration(WeatherCloudFadeInDuration.TotalMilliseconds);
            var holdOpacity = Math.Max(0.0, Math.Min(1.0, peakOpacity));
            Canvas.SetLeft(cloud, _centerX + startOffsetX - (cloud.Width / 2.0));
            Canvas.SetTop(cloud, _centerY + startOffsetY - (cloud.Height / 2.0));

            var translate = new TranslateTransform();
            cloud.RenderTransform = translate;
            cloud.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            RegisterWeatherVisual(cloud, zIndex);

            var moveX = endOffsetX - startOffsetX;
            var moveY = endOffsetY - startOffsetY;

            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = 0.0,
                To = moveX,
                Duration = movementDuration,
                EasingFunction = WeatherCloudMovementEase,
                FillBehavior = FillBehavior.HoldEnd,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 0.0,
                To = moveY,
                Duration = movementDuration,
                EasingFunction = WeatherCloudMovementEase,
                FillBehavior = FillBehavior.HoldEnd,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });

            cloud.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = holdOpacity,
                Duration = fadeInDuration,
                EasingFunction = WeatherCloudFadeInEase,
                FillBehavior = FillBehavior.HoldEnd
            });
        }

        private void StopWeatherCloudAnimation()
        {
            RemoveWeatherCloudVisual(_weatherCloudVisual);
            RemoveWeatherCloudVisual(_weatherCloudRearVisual);
        }

        private void RemoveWeatherCloudVisual(FrameworkElement? cloudVisual)
        {
            if (cloudVisual == null)
            {
                return;
            }

            cloudVisual.BeginAnimation(OpacityProperty, null);
            StopVisualTreeAnimations(cloudVisual);
            if (cloudVisual.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
            }

            if (cloudVisual.Parent is Panel parent)
            {
                parent.Children.Remove(cloudVisual);
            }

            _weatherVisualElements.Remove(cloudVisual);

            if (ReferenceEquals(_weatherCloudVisual, cloudVisual))
            {
                _weatherCloudVisual = null;
            }

            if (ReferenceEquals(_weatherCloudRearVisual, cloudVisual))
            {
                _weatherCloudRearVisual = null;
            }
        }

        private FrameworkElement CreateWeatherCloudVisual(double width, double height, bool lightweight = false)
        {
            if (WeatherCloudSpriteSource != null)
            {
                var cloudImage = new Grid
                {
                    Width = width,
                    Height = height,
                    Opacity = 0.0,
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };

                var image = new Image
                {
                    Source = WeatherCloudSpriteSource,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = lightweight
                        ? null
                        : CreateOptimizedBlurEffect(Math.Max(1.2, height * 0.03), blurScale: 0.84, minRadius: 0.8)
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.LowQuality);
                cloudImage.Children.Add(image);
                cloudImage.Effect = lightweight
                    ? null
                    : CreateOptimizedShadowEffect(10, 0.18, System.Windows.Media.Color.FromArgb(255, 96, 102, 114));
                return cloudImage;
            }

            return CreateFallbackWeatherCloudVisual(width, height, lightweight);
        }

        private FrameworkElement CreateFallbackWeatherCloudVisual(double width, double height, bool lightweight = false)
        {
            var cloud = new Canvas
            {
                Width = width,
                Height = height,
                Opacity = 0.0,
                IsHitTestVisible = false,
                CacheMode = new BitmapCache()
            };

            AddCloudPuff(cloud, width * 0.00, height * 0.34, width * 0.38, height * 0.42, System.Windows.Media.Color.FromArgb(224, 255, 255, 255), !lightweight);
            AddCloudPuff(cloud, width * 0.18, height * 0.20, width * 0.37, height * 0.46, System.Windows.Media.Color.FromArgb(236, 255, 255, 255), !lightweight);
            AddCloudPuff(cloud, width * 0.40, height * 0.16, width * 0.34, height * 0.43, System.Windows.Media.Color.FromArgb(228, 250, 252, 255), !lightweight);
            AddCloudPuff(cloud, width * 0.58, height * 0.27, width * 0.34, height * 0.38, System.Windows.Media.Color.FromArgb(215, 245, 248, 252), !lightweight);
            AddCloudPuff(cloud, width * 0.28, height * 0.42, width * 0.48, height * 0.34, System.Windows.Media.Color.FromArgb(204, 236, 241, 248), !lightweight);
            AddCloudPuff(cloud, width * 0.08, height * 0.47, width * 0.32, height * 0.30, System.Windows.Media.Color.FromArgb(176, 224, 230, 238), !lightweight);

            var underShadow = new Ellipse
            {
                Width = width * 0.62,
                Height = height * 0.20,
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(84, 40, 43, 50)),
                Effect = lightweight
                    ? null
                    : CreateOptimizedBlurEffect(Math.Max(4.2, height * 0.11), blurScale: 0.90, minRadius: 2.6),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(underShadow, width * 0.18);
            Canvas.SetTop(underShadow, height * 0.66);
            cloud.Children.Add(underShadow);

            cloud.Effect = lightweight
                ? null
                : CreateOptimizedBlurEffect(Math.Max(4.5, height * 0.09), blurScale: 0.88, minRadius: 2.8);
            return cloud;
        }

        private double ResolveWeatherCloudHeight(double cloudWidth, double centerSize)
        {
            if (WeatherCloudSpriteSource is BitmapSource bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                var ratio = (double)bitmap.PixelHeight / bitmap.PixelWidth;
                return Math.Clamp(cloudWidth * ratio, 64, 150);
            }

            return Math.Clamp(centerSize * WeatherCloudHeightScale, 78, 128);
        }

        private static ImageSource? TryLoadWeatherCloudSprite()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("pack://application:,,,/weather_img/cloud_1.png", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void AddCloudPuff(Canvas host, double left, double top, double width, double height, System.Windows.Media.Color color, bool withShadow)
        {
            var puff = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = GetCachedBrush(color),
                Effect = withShadow
                    ? CreateOptimizedShadowEffect(8, 0.16, System.Windows.Media.Color.FromArgb(255, 140, 148, 158))
                    : null,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(puff, left);
            Canvas.SetTop(puff, top);
            host.Children.Add(puff);
        }

        private void OnPersistChangesTimerTick(object? sender, EventArgs e)
        {
            FlushPendingChanges();
        }

        private void OnCenterClockTimerTick(object? sender, EventArgs e)
        {
            if (!IsVisible)
            {
                return;
            }

            RefreshIdleCenterTitleIfNeeded();
        }

        private void SetCenterTitleText(string text, bool useIdleClockTypography)
        {
            if (_centerTitle == null)
            {
                return;
            }

            ApplyCenterTitleTypography(useIdleClockTypography);
            var isShutdownCountdownActive = _shutdownCountdownTargetUtc.HasValue && useIdleClockTypography;
            _centerTitle.Foreground = isShutdownCountdownActive
                ? GetCachedBrush(ResolveShutdownCountdownTitleColor())
                : GetCachedBrush(_theme.TitleColor);
            AnimateCenterText(_centerTitle, text);
        }

        private void ApplyCenterTitleTypography(bool useIdleClockTypography)
        {
            if (_centerTitle == null)
            {
                return;
            }

            var baseFontSize = _centerTitleBaseFontSize > 0 ? _centerTitleBaseFontSize : _centerTitle.FontSize;
            var targetFontSize = useIdleClockTypography
                ? baseFontSize * CenterClockFontScale
                : baseFontSize;
            var targetFontFamily = useIdleClockTypography
                ? _centerClockFontFamily
                : CenterTitleDefaultFontFamily;

            _isCenterClockTypographyActive = useIdleClockTypography;
            _centerTitle.Tag = targetFontSize;
            _centerTitle.FontFamily = targetFontFamily;
            _centerTitle.FontSize = targetFontSize;
        }

        private string GetDefaultCenterTitleText()
        {
            if (_isEditModeEnabled)
            {
                return "Sürükleme Modu";
            }

            if (IsNeonOrbitStyle())
            {
                return "X";
            }

            if (TryGetShutdownCountdownDisplayText(out var countdownText))
            {
                return countdownText;
            }

            return DateTime.Now.ToString("HH:mm");
        }

        private bool ShouldShowIdleCenterClock()
        {
            return !_isEditModeEnabled &&
                   !IsNeonOrbitStyle() &&
                   _activeSegment == null &&
                   _activeIconHost == null &&
                   _activeInteraction == null &&
                   _activeSubmenuInteraction == null;
        }

        private void RefreshIdleCenterTitleIfNeeded()
        {
            if (!ShouldShowIdleCenterClock())
            {
                return;
            }

            if (TryGetShutdownCountdownDisplayText(out var countdownText))
            {
                SetCenterTitleText(countdownText, useIdleClockTypography: true);
                return;
            }

            SetCenterTitleText(DateTime.Now.ToString("HH:mm"), useIdleClockTypography: true);
        }

        private bool TryGetShutdownCountdownDisplayText(out string text)
        {
            text = string.Empty;
            if (!_shutdownCountdownTargetUtc.HasValue)
            {
                return false;
            }

            var remaining = _shutdownCountdownTargetUtc.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _shutdownCountdownTargetUtc = null;
                return false;
            }

            if (remaining.TotalHours >= 1.0)
            {
                text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1:00}:{2:00}",
                    (int)remaining.TotalHours,
                    remaining.Minutes,
                    remaining.Seconds);
            }
            else
            {
                text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}",
                    remaining.Minutes,
                    remaining.Seconds);
            }

            return true;
        }

        private void NormalizeConfigPages(MenuConfig config)
        {
            if (config.Pages != null && config.Pages.Count > 0)
            {
                foreach (var page in config.Pages)
                {
                    page.Items ??= new List<MenuItemConfig>();
                    _pages.Add(page.Items);
                }
                return;
            }

            config.Pages = new List<MenuPageConfig>
            {
                new MenuPageConfig
                {
                    Title = "1",
                    Items = config.Items ?? new List<MenuItemConfig>()
                }
            };

            if (config.Page2Items != null && config.Page2Items.Count > 0)
            {
                config.Pages.Add(new MenuPageConfig
                {
                    Title = "2",
                    Items = config.Page2Items
                });
            }

            foreach (var page in config.Pages)
            {
                _pages.Add(page.Items);
            }
        }

        private void BuildMenu(double centerX, double centerY, MenuConfig config)
        {
            _centerX = centerX;
            _centerY = centerY;
            StopWeatherVisuals();
            _openAnimationFinalizeTimer.Stop();
            _isOdakKaskadiAnimationActive = false;
            _odakKaskadiAnimatedSegments.Clear();
            _odakKaskadiIconFinalPositions.Clear();
            ClearCenterAccentRingSolidOverlay();
            ClearOuterAccentRingSolidOverlay();
            _centerAccentRing = null;
            _centerAccentRingBaseStroke = null;
            _centerAccentRingBaseOpacity = 0.0;
            _centerAccentRingBaseStrokeThickness = 0.0;
            _centerAccentRingSolidOverlay = null;
            _outerAccentRing = null;
            _outerAccentRingBaseStroke = null;
            _outerAccentRingBaseOpacity = 0.0;
            _outerAccentRingBaseStrokeThickness = 0.0;
            _outerAccentRingSolidOverlay = null;
            RootCanvas.Children.Clear();
            _externalFileDropGhostSlice = null;
            _externalFileDropGhostBadge = null;
            _externalFileDropGhostBadgeScale = null;
            _externalFileDropGhostTarget = null;
            _externalFileDropGhostInsertIndex = -1;
            _dragHoverOpenedCategory = null;
            _dragHoverOpenedFromDepth = 0;
            _segments.Clear();
            _submenuLayers.Clear();
            _segmentBrushes.Clear();
            _segmentInteractions.Clear();
            _iconInteractions.Clear();
            _dragInteractions.Clear();
            _hoverInteractions.Clear();
            _hoverInteractionEntries.Clear();
            _editModeRing = null;
            _editModeFocusHaze = null;
            _altGuideVisuals.Clear();
            ClearPendingDragCategoryOpen(resetSubmenu: false);
            HideItemContextMenu();
            _activeSegment = null;
            _activeIconHost = null;
            _activeAccentPath = null;
            _activeInteraction = null;
            _activeSubmenuInteraction = null;
            _selectedItem = null;
            _selectedInteraction = null;
            _utilityDockButton = null;
            _utilityDockButtonCore = null;

            var items = _pages[Math.Max(0, Math.Min(_currentPageIndex, _pages.Count - 1))];

            var profile = GetLayoutProfile();
            profile.ShowHoverLabel = _features.ShowHoverLabels;
            if (_features.ShowIconChrome)
            {
                profile.ShowIconChrome = true;
            }

            if (_features.EnableMenuBackdropBlur)
            {
                var menuBackdropBlurHalo = CreateMenuBackdropBlurHalo(profile.OuterRadius);
                Canvas.SetLeft(menuBackdropBlurHalo, centerX - menuBackdropBlurHalo.Width / 2);
                Canvas.SetTop(menuBackdropBlurHalo, centerY - menuBackdropBlurHalo.Height / 2);
                RootCanvas.Children.Add(menuBackdropBlurHalo);
            }

            var ringShadow = new Ellipse
            {
                Width = profile.OuterRadius * 2 + profile.ShadowPad,
                Height = profile.OuterRadius * 2 + profile.ShadowPad,
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(profile.ShadowAlpha, _theme.ShadowColor.R, _theme.ShadowColor.G, _theme.ShadowColor.B)),
                Effect = CreateOptimizedBlurEffect(profile.ShadowBlur),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ringShadow, centerX - ringShadow.Width / 2);
            Canvas.SetTop(ringShadow, centerY - ringShadow.Height / 2);
            RootCanvas.Children.Add(ringShadow);

            var outerAccentRing = CreateOuterAccentRing(profile.OuterRadius);
            _outerAccentRing = outerAccentRing;
            _outerAccentRingBaseStroke = outerAccentRing.Stroke;
            _outerAccentRingBaseOpacity = outerAccentRing.Opacity;
            _outerAccentRingBaseStrokeThickness = outerAccentRing.StrokeThickness;
            Canvas.SetLeft(outerAccentRing, centerX - outerAccentRing.Width / 2);
            Canvas.SetTop(outerAccentRing, centerY - outerAccentRing.Height / 2);
            RootCanvas.Children.Add(outerAccentRing);

            _centerPanel = new Border
            {
                Width = profile.CenterSize,
                Height = profile.CenterSize,
                CornerRadius = new CornerRadius(profile.CenterSize / 2),
                Background = GetCachedBrush(_theme.CenterColor),
                BorderBrush = GetCachedBrush(_theme.CenterBorderColor),
                BorderThickness = new Thickness(profile.CenterBorder),
                Effect = CreateOptimizedShadowEffect(profile.CenterBlur, 0.22, _theme.ShadowColor)
            };
            Canvas.SetLeft(_centerPanel, centerX - _centerPanel.Width / 2);
            Canvas.SetTop(_centerPanel, centerY - _centerPanel.Height / 2);

            var centerAccentRing = CreateCenterAccentRing(profile.CenterSize);
            _centerAccentRing = centerAccentRing;
            _centerAccentRingBaseStroke = centerAccentRing.Stroke;
            _centerAccentRingBaseOpacity = centerAccentRing.Opacity;
            _centerAccentRingBaseStrokeThickness = centerAccentRing.StrokeThickness;
            Canvas.SetLeft(centerAccentRing, centerX - centerAccentRing.Width / 2);
            Canvas.SetTop(centerAccentRing, centerY - centerAccentRing.Height / 2);
            var centerStack = new Grid
            {
                Width = profile.CenterSize,
                Height = profile.CenterSize,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _centerBreadcrumb = new TextBlock
            {
                Text = "",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(185, _theme.SubtitleColor.R, _theme.SubtitleColor.G, _theme.SubtitleColor.B)),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = Math.Max(10, profile.SubtitleFontSize - 1),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(16, 10, 16, 0),
                Opacity = 0.9
            };
            _centerBreadcrumb.Tag = _centerBreadcrumb.FontSize;

            _centerTitle = new TextBlock
            {
                Text = GetDefaultCenterTitleText(),
                Foreground = GetCachedBrush(_theme.TitleColor),
                FontFamily = CenterTitleDefaultFontFamily,
                FontSize = profile.TitleFontSize,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0)
            };
            _centerTitleBaseFontSize = profile.TitleFontSize;
            _centerTitle.Tag = _centerTitleBaseFontSize;
            ApplyCenterTitleTypography(ShouldShowIdleCenterClock());

            _centerSubtitle = new TextBlock
            {
                Text = string.Empty,
                Foreground = GetCachedBrush(_theme.SubtitleColor),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = profile.SubtitleFontSize,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = Math.Max(13.5, profile.SubtitleFontSize * 1.34),
                Margin = new Thickness(18, 6, 18, 0)
            };
            _centerSubtitle.Tag = _centerSubtitle.FontSize;
            _centerSubtitle.Visibility = Visibility.Collapsed;
            _utilityDockButton = CreateUtilityDockButton(profile);
            _utilityDockButton.HorizontalAlignment = HorizontalAlignment.Center;
            _utilityDockButton.VerticalAlignment = VerticalAlignment.Bottom;
            _utilityDockButton.Margin = new Thickness(0, 0, 0, Math.Max(8, profile.CenterSize * 0.06));

            centerStack.Children.Add(_centerBreadcrumb);
            centerStack.Children.Add(_utilityDockButton);
            centerStack.Children.Add(_centerTitle);
            Panel.SetZIndex(_centerTitle, 3);
            Panel.SetZIndex(_utilityDockButton, 2);
            _centerPanel.Child = centerStack;
            RootCanvas.Children.Add(centerAccentRing);
            RootCanvas.Children.Add(_centerPanel);
            ApplyCenterTextLayout();
            EnsureEditModeVisuals(profile);

            if (items.Count == 0)
            {
                var ghostSweep = Math.Min(profile.SweepAngle, 220.0);
                var ghostStartAngle = profile.StartAngle - (ghostSweep / 2.0);
                var ghostEndAngle = profile.StartAngle + (ghostSweep / 2.0) - profile.SegmentGap;
                var ghostMiddleAngle = (ghostStartAngle + ghostEndAngle) / 2.0;
                var ghostColor = System.Windows.Media.Color.FromRgb(255, 214, 102);

                var ghostSegment = new ShapePath
                {
                    Data = CreateRingSegmentGeometry(centerX, centerY, profile.InnerRadius, profile.OuterRadius, ghostStartAngle, ghostEndAngle),
                    Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(72, ghostColor.R, ghostColor.G, ghostColor.B)),
                    Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(200, ghostColor.R, ghostColor.G, ghostColor.B)),
                    StrokeThickness = Math.Max(1.2, profile.SegmentStrokeThickness),
                    Effect = CreateOptimizedShadowEffect(8, 0.12, ghostColor)
                };
                ghostSegment.StrokeDashArray = MainGhostDashArray;

                var ghostBrush = (SolidColorBrush)ghostSegment.Fill;
                var ghostHost = CreateGhostSlotHost(profile.IconSize);
                var ghostTarget = new DragItemInteraction(
                    item: null,
                    ownerItems: items,
                    itemIndex: -1,
                    depth: 0,
                    startAngle: ghostStartAngle,
                    endAngle: ghostEndAngle,
                    breadcrumbPath: Array.Empty<string>(),
                    isGhostSlot: true,
                    insertIndex: 0);

                _dragInteractions[ghostSegment] = ghostTarget;
                _dragInteractions[ghostHost] = ghostTarget;
                RegisterHoverInteraction(
                    ghostSegment,
                    ghostHost,
                    item: null,
                    ownerItems: items,
                    itemIndex: -1,
                    depth: 0,
                    startAngle: ghostStartAngle,
                    endAngle: ghostEndAngle,
                    breadcrumbPath: Array.Empty<string>(),
                    segmentBrush: ghostBrush,
                    accentColor: ghostColor,
                    isGhostSlot: true,
                    innerRadius: profile.InnerRadius,
                    outerRadius: profile.OuterRadius);

                var ghostRadius = profile.IconRadius;
                var ghostX = centerX + Math.Cos(ToRadians(ghostMiddleAngle)) * ghostRadius - ghostHost.Width / 2.0;
                var ghostY = centerY + Math.Sin(ToRadians(ghostMiddleAngle)) * ghostRadius - ghostHost.Height / 2.0;
                Canvas.SetLeft(ghostHost, ghostX);
                Canvas.SetTop(ghostHost, ghostY);

                RootCanvas.Children.Add(ghostSegment);
                RootCanvas.Children.Add(ghostHost);
                _segments.Add(ghostSegment);
                Dispatcher.BeginInvoke(new Action(UpdateSelectionUnderCursor));
                return;
            }

            var itemCount = items.Count;
            var perItemSweep = profile.SweepAngle / itemCount;
            var singleItemSweep = Math.Min(profile.SweepAngle, 300.0);

            for (var i = 0; i < items.Count; i++)
            {
                var startAngle = itemCount == 1
                    ? profile.StartAngle - (singleItemSweep / 2.0)
                    : profile.StartAngle + perItemSweep * i;
                var endAngle = itemCount == 1
                    ? profile.StartAngle + (singleItemSweep / 2.0) - profile.SegmentGap
                    : startAngle + perItemSweep - profile.SegmentGap;
                var middleAngle = (startAngle + endAngle) / 2.0;
                var segmentColor = GetNeonOrbitColor(i);

                var segment = new ShapePath
                {
                    Data = IsNeonOrbitStyle()
                        ? CreateOrbitNodeGeometry(centerX, centerY, profile.IconRadius, middleAngle, profile.OuterRadius)
                        : CreateRingSegmentGeometry(centerX, centerY, profile.InnerRadius, profile.OuterRadius, startAngle, endAngle),
                    Fill = new SolidColorBrush(IsNeonOrbitStyle()
                        ? System.Windows.Media.Color.FromArgb(0, segmentColor.R, segmentColor.G, segmentColor.B)
                        : GetItemBaseSegmentColor(items[i])),
                    Stroke = GetCachedBrush(IsNeonOrbitStyle() ? segmentColor : GetItemStrokeColor(items[i])),
                    StrokeThickness = profile.SegmentStrokeThickness,
                    Tag = items[i],
                    Effect = IsNeonOrbitStyle()
                        ? CreateOptimizedShadowEffect(8, 0.18, segmentColor)
                        : CreateOptimizedShadowEffect(profile.SegmentBlur, profile.SegmentShadowOpacity, Colors.Black)
                };
                _segmentBrushes[segment] = (SolidColorBrush)segment.Fill;

                var iconHost = CreateIconHost(items[i], profile.IconSize, startAngle, endAngle);
                var itemRadius = GetItemRadius(items[i], profile);
                var iconX = centerX + Math.Cos(ToRadians(middleAngle)) * itemRadius - iconHost.Width / 2.0;
                var iconY = centerY + Math.Sin(ToRadians(middleAngle)) * itemRadius - iconHost.Height / 2.0;
                Canvas.SetLeft(iconHost, iconX);
                Canvas.SetTop(iconHost, iconY);

                AttachInteractions(segment, iconHost, items[i], startAngle, endAngle, i);
                RootCanvas.Children.Add(segment);
                RootCanvas.Children.Add(iconHost);
                _segments.Add(segment);
            }

            Dispatcher.BeginInvoke(new Action(UpdateSelectionUnderCursor));
        }

        private Border CreateIconHost(MenuItemConfig item, double size, double startAngle, double endAngle)
        {
            var profile = GetLayoutProfile();
            var label = CreateItemLabel(item.Label, size, isSubmenu: false);

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(CreateMenuItemVisual(item, size, isSubmenu: false, startAngle, endAngle));

            var hostWidth = size;
            var hostHeight = size;

            return new Border
            {
                Width = hostWidth,
                Height = hostHeight,
                Tag = Tuple.Create(item, label),
                Child = stack,
                Background = CreateHostBackground(item, isActive: false),
                BorderBrush = CreateHostBorderBrush(item, isActive: false),
                BorderThickness = new Thickness(IsNeonOrbitStyle() ? 0 : profile.ShowIconChrome && _theme.IconBorderColor.A > 0 ? 1 : 0),
                CornerRadius = new CornerRadius(999),
                RenderTransformOrigin = new WpfPoint(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Effect = CreateOptimizedShadowEffect(
                    IsNeonOrbitStyle() ? 0 : profile.ShowIconChrome ? 18 : 0,
                    profile.ShowIconChrome ? 0.35 : 0,
                    _theme.ShadowColor)
            };
        }

        private void AttachInteractions(ShapePath segment, Border iconHost, MenuItemConfig item, double startAngle, double endAngle, int itemIndex)
        {
            var profile = GetLayoutProfile();
            var middleAngle = (startAngle + endAngle) / 2.0;
            var interaction = new SegmentInteraction(segment, iconHost, item, startAngle, endAngle, itemIndex);
            _segmentInteractions[segment] = interaction;
            _iconInteractions[iconHost] = interaction;
            RegisterDragInteraction(segment, iconHost, item, _pages[_currentPageIndex], itemIndex, depth: 0, startAngle, endAngle);
            RegisterHoverInteraction(
                segment,
                iconHost,
                item,
                _pages[_currentPageIndex],
                itemIndex,
                depth: 0,
                startAngle,
                endAngle,
                Array.Empty<string>(),
                _segmentBrushes[segment],
                IsNeonOrbitStyle() ? ((SolidColorBrush)segment.Stroke).Color : GetItemBaseSegmentColor(item),
                isGhostSlot: false,
                innerRadius: profile.InnerRadius,
                outerRadius: profile.OuterRadius,
                hitCenter: IsNeonOrbitStyle() ? PointOnCircle(_centerX, _centerY, profile.IconRadius, middleAngle) : (WpfPoint?)null,
                hitRadius: IsNeonOrbitStyle() ? profile.OuterRadius : 0);

            void Select()
            {
                try
                {
                    if (_itemContextMenu != null)
                    {
                        return;
                    }

                    HideItemContextMenu();
                    SelectItem(segment, iconHost, item, startAngle, endAngle);
                }
                catch (Exception ex)
                {
                    LogUiException("Ana dilim hover seçimi sırasında hata", ex);
                }
            }

            void Launch()
            {
                if (IsEditModifierActive())
                {
                    return;
                }

                if (IsCategoryItem(item))
                {
                    Select();
                    return;
                }

                PlayLaunchFeedback(iconHost);
                RequestLaunchAndDismiss(item.TargetPath, launchDelayMs: 58);
            }

            segment.MouseEnter += (_, __) => Select();
            segment.MouseMove += (_, __) => Select();
            iconHost.MouseEnter += (_, __) => Select();
            iconHost.MouseMove += (_, __) => Select();
            segment.PreviewMouseLeftButtonDown += (_, e) => BeginPotentialDrag(e, segment);
            iconHost.PreviewMouseLeftButtonDown += (_, e) => BeginPotentialDrag(e, iconHost);
            segment.PreviewMouseLeftButtonUp += (_, __) => ClearPendingDragState();
            iconHost.PreviewMouseLeftButtonUp += (_, __) => ClearPendingDragState();
            segment.MouseRightButtonUp += (_, e) => ShowItemContextMenuForSource(segment, e);
            iconHost.MouseRightButtonUp += (_, e) => ShowItemContextMenuForSource(iconHost, e);
            segment.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                Launch();
            };
            iconHost.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                Launch();
            };
        }

        private void AnimateIconHostState(Border iconHost, double targetScale, double targetOpacity, bool instant = false)
        {
            if (iconHost.RenderTransform is not ScaleTransform scaleTransform)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                iconHost.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                iconHost.RenderTransform = scaleTransform;
            }

            var clampedScale = Math.Clamp(targetScale, 0.82, 1.22);
            var clampedOpacity = Math.Clamp(targetOpacity, 0.12, 1.0);
            if (instant)
            {
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
                iconHost.BeginAnimation(OpacityProperty, null);
                scaleTransform.ScaleX = clampedScale;
                scaleTransform.ScaleY = clampedScale;
                iconHost.Opacity = clampedOpacity;
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var scaleAnimation = new DoubleAnimation
            {
                To = clampedScale,
                Duration = TimeSpan.FromMilliseconds(130),
                EasingFunction = ease
            };
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);

            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = clampedOpacity,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = ease
            });
        }

        private void PlayLaunchFeedback(Border iconHost)
        {
            PlayUiSound(SoundCue.MenuLaunch);

            if (iconHost.RenderTransform is not ScaleTransform scaleTransform)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                iconHost.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                iconHost.RenderTransform = scaleTransform;
            }

            var pulseEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var pulseDuration = TimeSpan.FromMilliseconds(78);
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = 0.94,
                Duration = pulseDuration,
                AutoReverse = true,
                EasingFunction = pulseEase
            });
            scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = 0.94,
                Duration = pulseDuration,
                AutoReverse = true,
                EasingFunction = pulseEase
            });
            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = Math.Max(0.84, iconHost.Opacity * 0.92),
                Duration = pulseDuration,
                AutoReverse = true,
                EasingFunction = pulseEase
            });
        }

        private void RegisterDragInteraction(
            ShapePath segment,
            Border iconHost,
            MenuItemConfig item,
            List<MenuItemConfig> ownerItems,
            int itemIndex,
            int depth,
            double startAngle,
            double endAngle,
            IReadOnlyList<string>? breadcrumbPath = null)
        {
            var dragInteraction = new DragItemInteraction(item, ownerItems, itemIndex, depth, startAngle, endAngle, breadcrumbPath);
            _dragInteractions[segment] = dragInteraction;
            _dragInteractions[iconHost] = dragInteraction;
        }

        private void RegisterHoverInteraction(
            ShapePath segment,
            Border iconHost,
            MenuItemConfig? item,
            List<MenuItemConfig> ownerItems,
            int itemIndex,
            int depth,
            double startAngle,
            double endAngle,
            IReadOnlyList<string>? breadcrumbPath,
            SolidColorBrush segmentBrush,
            System.Windows.Media.Color accentColor,
            bool isGhostSlot,
            double innerRadius,
            double outerRadius,
            WpfPoint? hitCenter = null,
            double hitRadius = 0)
        {
            var hoverInteraction = new HoverInteraction(
                segment,
                iconHost,
                item,
                ownerItems,
                itemIndex,
                depth,
                startAngle,
                endAngle,
                breadcrumbPath,
                segmentBrush,
                accentColor,
                isGhostSlot);
            if (hitCenter.HasValue && hitRadius > 0)
            {
                hoverInteraction.HitShape = HoverHitShape.Circle;
                hoverInteraction.HitCenter = hitCenter.Value;
                hoverInteraction.HitRadius = hitRadius;
            }
            else
            {
                hoverInteraction.HitShape = HoverHitShape.RingSector;
                hoverInteraction.InnerRadius = innerRadius;
                hoverInteraction.OuterRadius = outerRadius;
            }
            _hoverInteractions[segment] = hoverInteraction;
            _hoverInteractions[iconHost] = hoverInteraction;
            _hoverInteractionEntries.Add(hoverInteraction);
        }

        private static bool IsCategoryItem(MenuItemConfig item)
        {
            return item.IsCategory || item.Children.Count > 0;
        }

        private void BeginPotentialDrag(MouseButtonEventArgs e, DependencyObject source)
        {
            if (!IsEditModifierActive())
            {
                ClearPendingDragState();
                return;
            }

            if (!_dragInteractions.TryGetValue(source, out var interaction))
            {
                ClearPendingDragState();
                return;
            }

            _pendingDragInteraction = interaction;
            _pendingDragSource = source;
            _dragStartPoint = e.GetPosition(RootCanvas);
            if (BackdropRoot != null)
            {
                Mouse.Capture(BackdropRoot, CaptureMode.SubTree);
            }

            SetWindowClickThrough(false);
        }

        private void ClearPendingDragState(bool clearPreview = false)
        {
            _pendingDragInteraction = null;
            _pendingDragSource = null;
            if (Mouse.Captured != null)
            {
                Mouse.Capture(null);
            }

            if (clearPreview)
            {
                ClearDragDropPreview();
            }

            UpdateEditModeClickThroughState();
        }

        private void UpdateSelectionUnderCursor()
        {
            try
            {
                if (_isAltGuideActive)
                {
                    UpdateSelectionAlongGuide();
                    return;
                }

                var position = GetCursorPositionOnRootCanvas();
                var interaction = FindHoverInteractionAtPoint(position);
                if (interaction == null)
                {
                    if (HasVisibleSubmenu())
                    {
                        BeginSubmenuCloseGrace();
                        return;
                    }

                    ClearCurrentSelection();
                    return;
                }

                CancelSubmenuCloseGrace();

                if (interaction.Depth == 0)
                {
                    if (interaction.Item != null)
                    {
                        SelectItem(
                            interaction.Segment,
                            interaction.IconHost,
                            interaction.Item,
                            interaction.StartAngle,
                            interaction.EndAngle);
                    }
                    else if (interaction.IsGhostSlot)
                    {
                        ApplyGhostSlotSelection(interaction);
                    }
                    return;
                }

                if (interaction.IsGhostSlot)
                {
                    ApplyGhostSlotSelection(interaction);
                    return;
                }

                ApplySubmenuSelection(interaction);
            }
            catch (Exception ex)
            {
                LogUiException("Mouse seçim güncellemesi sırasında hata", ex);
            }
        }

        private bool HasVisibleSubmenu()
        {
            return _submenuLayers.Count > 0;
        }

        private void BeginSubmenuCloseGrace()
        {
            if (_itemContextMenu != null || _isPaging || !HasVisibleSubmenu())
            {
                return;
            }

            if (!_submenuCloseGraceTimer.IsEnabled)
            {
                _submenuCloseGraceTimer.Start();
            }
        }

        private void CancelSubmenuCloseGrace()
        {
            if (_submenuCloseGraceTimer.IsEnabled)
            {
                _submenuCloseGraceTimer.Stop();
            }
        }

        private void OnSubmenuCloseGraceTimerTick(object? sender, EventArgs e)
        {
            _submenuCloseGraceTimer.Stop();

            if (_itemContextMenu != null || _isPaging || !HasVisibleSubmenu())
            {
                return;
            }

            var interaction = FindHoverInteractionAtPoint(GetCursorPositionOnRootCanvas());
            if (interaction != null)
            {
                return;
            }

            ClearCurrentSelection();
        }

        private void UpdateSelectionAlongGuide()
        {
            try
            {
                var position = GetCursorPositionOnRootCanvas();
                var dx = position.X - _centerX;
                var dy = position.Y - _centerY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance < 6)
                {
                    return;
                }

                var bestInteraction = FindHoverInteractionAlongGuide(position);
                if (bestInteraction == null)
                {
                    return;
                }

                if (bestInteraction.Depth == 0 && bestInteraction.Item != null)
                {
                    SelectItem(
                        bestInteraction.Segment,
                        bestInteraction.IconHost,
                        bestInteraction.Item,
                        bestInteraction.StartAngle,
                        bestInteraction.EndAngle);
                    return;
                }

                ApplySubmenuSelection(bestInteraction);
            }
            catch (Exception ex)
            {
                LogUiException("Hedefleme modu seçim güncellemesi sırasında hata", ex);
            }
        }

        private HoverInteraction? FindHoverInteractionAtPoint(WpfPoint position)
        {
            var dx = position.X - _centerX;
            var dy = position.Y - _centerY;
            var radius = Math.Sqrt((dx * dx) + (dy * dy));
            var angle = NormalizeAngle(ToDegrees(Math.Atan2(dy, dx)));
            HoverInteraction? bestInteraction = null;
            var bestDepth = int.MinValue;
            var bestScore = double.MaxValue;

            foreach (var interaction in _hoverInteractionEntries)
            {
                if (!TryGetPointHitScore(interaction, position, radius, angle, out var score))
                {
                    continue;
                }

                if (bestInteraction == null ||
                    interaction.Depth > bestDepth ||
                    (interaction.Depth == bestDepth && score < bestScore))
                {
                    bestInteraction = interaction;
                    bestDepth = interaction.Depth;
                    bestScore = score;
                }
            }

            return bestInteraction;
        }

        private HoverInteraction? FindCategoryHoverInteractionAtPoint(WpfPoint position)
        {
            var dx = position.X - _centerX;
            var dy = position.Y - _centerY;
            var radius = Math.Sqrt((dx * dx) + (dy * dy));
            var angle = NormalizeAngle(ToDegrees(Math.Atan2(dy, dx)));
            HoverInteraction? bestInteraction = null;
            var bestDepth = int.MinValue;
            var bestScore = double.MaxValue;

            foreach (var interaction in _hoverInteractionEntries)
            {
                if (interaction.IsGhostSlot || interaction.Item == null || !IsCategoryItem(interaction.Item))
                {
                    continue;
                }

                if (!TryGetPointHitScore(interaction, position, radius, angle, out var score))
                {
                    continue;
                }

                if (bestInteraction == null ||
                    interaction.Depth > bestDepth ||
                    (interaction.Depth == bestDepth && score < bestScore))
                {
                    bestInteraction = interaction;
                    bestDepth = interaction.Depth;
                    bestScore = score;
                }
            }

            return bestInteraction;
        }

        private void ApplyGhostSlotSelection(HoverInteraction interaction)
        {
            CancelSubmenuCloseGrace();

            if (_activeSubmenuInteraction != null)
            {
                ClearSubmenuSelection();
            }

            interaction.Segment.Opacity = 1.0;
            interaction.IconHost.Opacity = 1.0;
            _selectedInteraction = new DragItemInteraction(
                item: null,
                interaction.OwnerItems,
                interaction.ItemIndex,
                interaction.Depth,
                interaction.StartAngle,
                interaction.EndAngle,
                interaction.BreadcrumbPath,
                isGhostSlot: true,
                insertIndex: interaction.OwnerItems.Count);

            if (_centerTitle != null)
            {
                SetCenterTitleText(interaction.Depth == 0 ? "İlk Öğeyi Ekle" : "Kategoriye Ekle", useIdleClockTypography: false);
            }

            UpdateCenterBreadcrumb(interaction.BreadcrumbPath);

            if (_centerSubtitle != null)
            {
                AnimateCenterText(
                    _centerSubtitle,
                    interaction.Depth == 0
                        ? "Buraya bırakarak menüyü doldurun"
                        : "Buraya bırakarak kategori içine taşıyın");
            }

            HideCategoryNameStrip();
            HideHoverOrbitGuide();
        }

        private HoverInteraction? FindHoverInteractionAlongGuide(WpfPoint target)
        {
            var dx = target.X - _centerX;
            var dy = target.Y - _centerY;
            var maxDistance = Math.Sqrt((dx * dx) + (dy * dy));
            if (maxDistance < 6)
            {
                return null;
            }

            var angle = NormalizeAngle(ToDegrees(Math.Atan2(dy, dx)));
            HoverInteraction? bestInteraction = null;
            var bestDepth = int.MinValue;
            var bestDistance = double.MaxValue;

            foreach (var interaction in _hoverInteractionEntries)
            {
                if (interaction.IsGhostSlot || !TryGetGuideHitDistance(interaction, angle, maxDistance, out var hitDistance))
                {
                    continue;
                }

                if (bestInteraction == null ||
                    interaction.Depth > bestDepth ||
                    (interaction.Depth == bestDepth && hitDistance < bestDistance))
                {
                    bestInteraction = interaction;
                    bestDepth = interaction.Depth;
                    bestDistance = hitDistance;
                }
            }

            return bestInteraction;
        }

        private bool TryGetPointHitScore(
            HoverInteraction interaction,
            WpfPoint position,
            double radius,
            double angle,
            out double score)
        {
            const double radiusTolerance = 2.5;
            const double angleTolerance = 1.1;

            if (interaction.HitShape == HoverHitShape.Circle)
            {
                var dx = position.X - interaction.HitCenter.X;
                var dy = position.Y - interaction.HitCenter.Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > interaction.HitRadius + radiusTolerance)
                {
                    score = 0;
                    return false;
                }

                score = distance;
                return true;
            }

            if (radius < interaction.InnerRadius - radiusTolerance ||
                radius > interaction.OuterRadius + radiusTolerance ||
                !IsAngleWithinSweep(angle, interaction.StartAngle, interaction.EndAngle, angleTolerance))
            {
                score = 0;
                return false;
            }

            score = Math.Abs(radius - ((interaction.InnerRadius + interaction.OuterRadius) / 2.0));
            return true;
        }

        private bool TryGetGuideHitDistance(
            HoverInteraction interaction,
            double angle,
            double maxDistance,
            out double hitDistance)
        {
            const double tolerance = 2.5;

            if (interaction.HitShape == HoverHitShape.Circle)
            {
                var unitX = Math.Cos(ToRadians(angle));
                var unitY = Math.Sin(ToRadians(angle));
                var centerX = interaction.HitCenter.X - _centerX;
                var centerY = interaction.HitCenter.Y - _centerY;
                var projection = (centerX * unitX) + (centerY * unitY);
                var centerDistanceSquared = (centerX * centerX) + (centerY * centerY);
                var perpendicularSquared = centerDistanceSquared - (projection * projection);
                var radius = interaction.HitRadius + tolerance;
                if (perpendicularSquared > radius * radius)
                {
                    hitDistance = 0;
                    return false;
                }

                var offset = Math.Sqrt(Math.Max(0, (radius * radius) - perpendicularSquared));
                var first = projection - offset;
                var second = projection + offset;
                if (second < 0)
                {
                    hitDistance = 0;
                    return false;
                }

                hitDistance = first >= 0 ? first : second;
                return hitDistance <= maxDistance;
            }

            if (!IsAngleWithinSweep(angle, interaction.StartAngle, interaction.EndAngle, 0.8))
            {
                hitDistance = 0;
                return false;
            }

            hitDistance = Math.Max(0, interaction.InnerRadius - tolerance);
            return hitDistance <= maxDistance;
        }

        private static bool IsAngleWithinSweep(double angle, double startAngle, double endAngle, double tolerance = 0)
        {
            var normalizedAngle = NormalizeAngle(angle);
            var normalizedStart = NormalizeAngle(startAngle - tolerance);
            var normalizedEnd = NormalizeAngle(endAngle + tolerance);
            if (normalizedStart <= normalizedEnd)
            {
                return normalizedAngle >= normalizedStart && normalizedAngle <= normalizedEnd;
            }

            return normalizedAngle >= normalizedStart || normalizedAngle <= normalizedEnd;
        }

        private static double NormalizeAngle(double angle)
        {
            var normalized = angle % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }

        private static double ToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        private void SelectItem(ShapePath segment, Border iconHost, MenuItemConfig item, double startAngle, double endAngle)
        {
            CancelSubmenuCloseGrace();

            if (_isAltGuideActive && _activeSubmenuInteraction != null && _activeSubmenuInteraction.Depth > 0)
            {
                if (IsCategoryItem(item))
                {
                    return;
                }
            }

            if (ReferenceEquals(_selectedItem, item) &&
                _selectedInteraction != null &&
                !_selectedInteraction.IsGhostSlot &&
                _selectedInteraction.Item != null &&
                ReferenceEquals(_selectedInteraction.Item, item) &&
                _selectedInteraction.Depth == 0)
            {
                if (_hoverOrbitRing == null || _hoverOrbitRing.Visibility != Visibility.Visible)
                {
                    RefreshHoverOrbitGuide();
                }
                return;
            }

            ClearCurrentSelection(resetCenterText: false);
            _selectedItem = item;
            var profile = GetLayoutProfile();
            AnimateSegmentHighlight(segment, true);
            var hoverScale = profile.HoverIconSize / Math.Max(1, profile.IconSize);
            AnimateIconHostState(iconHost, hoverScale, profile.HoverOpacity);
            if (iconHost.Tag is Tuple<MenuItemConfig, TextBlock> tuple)
            {
                tuple.Item2.Visibility = profile.ShowHoverLabel && !IsCategoryItem(tuple.Item1)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                iconHost.Background = CreateHostBackground(tuple.Item1, isActive: true);
                iconHost.BorderBrush = CreateHostBorderBrush(tuple.Item1, isActive: true);
            }
            var isCategory = IsCategoryItem(item);

            if (_centerTitle != null)
            {
                if (!_isEditModeEnabled)
                {
                    SetCenterTitleText(isCategory ? string.Empty : item.Label, useIdleClockTypography: false);
                }
            }
            UpdateCenterBreadcrumb(Array.Empty<string>());
            if (_centerSubtitle != null)
            {
                if (!_isEditModeEnabled)
                {
                    AnimateCenterText(_centerSubtitle, isCategory
                        ? "Alt menü açıldı\nBir öğe seçin"
                        : "Tıklayarak aç\nESC ile kapat");
                }
            }

            if (isCategory)
            {
                ClearCenterTextForCategoryMode();
            }

            if (isCategory)
            {
                ShowCategoryHoverBadge(
                    title: item.Label,
                    subtitle: string.Empty,
                    breadcrumb: string.Empty,
                    symbolKey: item.CategorySymbolKey,
                    accentColor: GetItemActiveSegmentColor(item),
                    showSymbol: true);
            }
            else
            {
                HideCategoryNameStrip();
            }

            _activeSegment = segment;
            _activeIconHost = iconHost;
            _activeInteraction = _segmentInteractions.TryGetValue(segment, out var interaction)
                ? interaction
                : null;
            _selectedInteraction = _activeInteraction != null
                ? new DragItemInteraction(item, _pages[_currentPageIndex], _activeInteraction.ItemIndex, 0, startAngle, endAngle)
                : null;
            PlayUiSound(SoundCue.UiHover);

            if (isCategory)
            {
                ApplyCategoryHoverShape(segment, startAngle, endAngle);
            }
            else
            {
                ShowHoverAccent(startAngle, endAngle);
            }

            if (isCategory)
            {
                ShowSubmenu(
                    item.Children,
                    startAngle,
                    endAngle,
                    new[] { item.Label },
                    inheritedColor: GetSubmenuInheritedColor(item, GetItemBaseSegmentColor(item)));
            }
            else
            {
                HideSubmenu();
            }

            UpdateAltGuideVisual();
            RefreshHoverOrbitGuide();
        }

        private void ClearCurrentSelection(bool resetCenterText = true)
        {
            CancelSubmenuCloseGrace();

            var profile = GetLayoutProfile();
            ClearSubmenuSelection();
            HideSubmenu();

            if (_activeSegment != null)
            {
                AnimateSegmentHighlight(_activeSegment, false);
                RestoreSegmentShape();
            }

            if (_activeIconHost?.Tag is Tuple<MenuItemConfig, TextBlock> tuple)
            {
                AnimateIconHostState(_activeIconHost, 1.0, 1.0);
                _activeIconHost.Background = CreateHostBackground(tuple.Item1, isActive: false);
                _activeIconHost.BorderBrush = CreateHostBorderBrush(tuple.Item1, isActive: false);
                _activeIconHost.BorderThickness = new Thickness(profile.ShowIconChrome && _theme.IconBorderColor.A > 0 ? 1 : 0);
                tuple.Item2.Visibility = Visibility.Collapsed;
            }

            if (resetCenterText && _centerTitle != null)
            {
                SetCenterTitleText(GetDefaultCenterTitleText(), useIdleClockTypography: ShouldShowIdleCenterClock());
            }
            if (resetCenterText)
            {
                UpdateCenterBreadcrumb(Array.Empty<string>());
            }
            if (resetCenterText && _centerSubtitle != null)
            {
                AnimateCenterText(_centerSubtitle, _isEditModeEnabled
                    ? string.Empty
                    : IsNeonOrbitStyle() ? string.Empty : "Bir dilimin üstüne gelin\nve tıklayın");
            }

            HideCategoryNameStrip();

            _activeSegment = null;
            _activeIconHost = null;
            _activeInteraction = null;
            _selectedItem = null;
            if (resetCenterText)
            {
                HideHoverOrbitGuide();
            }
        }

        private void ApplySubmenuSelection(HoverInteraction interaction)
        {
            CancelSubmenuCloseGrace();

            if (interaction.IsGhostSlot || interaction.Item == null)
            {
                return;
            }

            if (_isAltGuideActive &&
                _activeSubmenuInteraction != null &&
                _activeSubmenuInteraction.Depth > interaction.Depth)
            {
                return;
            }

            if (_activeSubmenuInteraction != null &&
                ReferenceEquals(_activeSubmenuInteraction.Item, interaction.Item) &&
                ReferenceEquals(_activeSubmenuInteraction.OwnerItems, interaction.OwnerItems) &&
                _activeSubmenuInteraction.Depth == interaction.Depth)
            {
                ReapplySubmenuSelectionVisual(interaction);
                if (_hoverOrbitRing == null || _hoverOrbitRing.Visibility != Visibility.Visible)
                {
                    RefreshHoverOrbitGuide();
                }
                return;
            }

            ClearSubmenuSelection();

            AnimateSubmenuSegment(interaction.SegmentBrush, interaction.Item, true, interaction.AccentColor);
            if (IsNeonOrbitStyle())
            {
                interaction.SegmentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                {
                    To = System.Windows.Media.Color.FromArgb(52, interaction.AccentColor.R, interaction.AccentColor.G, interaction.AccentColor.B),
                    Duration = TimeSpan.FromMilliseconds(120),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            interaction.IconHost.Background = CreateHostBackground(interaction.Item, isActive: true);
            interaction.IconHost.BorderBrush = CreateHostBorderBrush(interaction.Item, isActive: true);
            AnimateIconHostState(interaction.IconHost, SubmenuHoverScale, GetLayoutProfile().SubmenuHoverOpacity);
            _activeSubmenuInteraction = interaction;
            _selectedItem = interaction.Item;
            _selectedInteraction = new DragItemInteraction(
                interaction.Item,
                interaction.OwnerItems,
                interaction.ItemIndex,
                interaction.Depth,
                interaction.StartAngle,
                interaction.EndAngle,
                interaction.BreadcrumbPath);
            PlayUiSound(SoundCue.UiHover);

            var submenuBreadcrumb = interaction.BreadcrumbPath.Count == 0
                ? string.Empty
                : string.Join(" > ", interaction.BreadcrumbPath);
            var submenuTitle = interaction.Item.Label;
            var submenuSubtitle = IsCategoryItem(interaction.Item)
                ? "Bu öğe de alt menü taşıyor"
                : "Tıklayarak aç";

            ShowCategoryHoverBadge(
                title: submenuTitle,
                subtitle: submenuSubtitle,
                breadcrumb: submenuBreadcrumb,
                symbolKey: interaction.Item.CategorySymbolKey,
                accentColor: interaction.AccentColor,
                showSymbol: false);

            ClearCenterTextForCategoryMode();

            if (IsCategoryItem(interaction.Item))
            {
                var nextPath = new List<string>(interaction.BreadcrumbPath)
                {
                    interaction.Item.Label
                };
                ShowSubmenu(
                    interaction.Item.Children,
                    interaction.StartAngle,
                    interaction.EndAngle,
                    nextPath,
                    interaction.Depth + 1,
                    inheritedColor: GetSubmenuInheritedColor(interaction.Item, interaction.AccentColor));
            }

            RefreshHoverOrbitGuide();
        }

        private void ReapplySubmenuSelectionVisual(HoverInteraction interaction)
        {
            if (interaction.Item == null)
            {
                return;
            }

            AnimateSubmenuSegment(interaction.SegmentBrush, interaction.Item, true, interaction.AccentColor);
            if (IsNeonOrbitStyle())
            {
                interaction.SegmentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                {
                    To = System.Windows.Media.Color.FromArgb(52, interaction.AccentColor.R, interaction.AccentColor.G, interaction.AccentColor.B),
                    Duration = TimeSpan.FromMilliseconds(90),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            interaction.IconHost.Background = CreateHostBackground(interaction.Item, isActive: true);
            interaction.IconHost.BorderBrush = CreateHostBorderBrush(interaction.Item, isActive: true);
            AnimateIconHostState(interaction.IconHost, SubmenuHoverScale, GetLayoutProfile().SubmenuHoverOpacity);
        }

        private void ClearSubmenuSelection()
        {
            if (_activeSubmenuInteraction == null || _activeSubmenuInteraction.Item == null)
            {
                return;
            }

            AnimateSubmenuSegment(_activeSubmenuInteraction.SegmentBrush, _activeSubmenuInteraction.Item, false, _activeSubmenuInteraction.AccentColor);
            if (IsNeonOrbitStyle())
            {
                _activeSubmenuInteraction.SegmentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                {
                    To = System.Windows.Media.Color.FromArgb(0, _activeSubmenuInteraction.AccentColor.R, _activeSubmenuInteraction.AccentColor.G, _activeSubmenuInteraction.AccentColor.B),
                    Duration = TimeSpan.FromMilliseconds(140),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            _activeSubmenuInteraction.IconHost.Background = CreateHostBackground(_activeSubmenuInteraction.Item, isActive: false);
            _activeSubmenuInteraction.IconHost.BorderBrush = CreateHostBorderBrush(_activeSubmenuInteraction.Item, isActive: false);
            AnimateIconHostState(_activeSubmenuInteraction.IconHost, 1.0, 1.0);
            _activeSubmenuInteraction = null;
            if (_activeInteraction != null && _activeInteraction.Item != null && IsCategoryItem(_activeInteraction.Item))
            {
                ShowCategoryHoverBadge(
                    title: _activeInteraction.Item.Label,
                    subtitle: string.Empty,
                    breadcrumb: string.Empty,
                    symbolKey: _activeInteraction.Item.CategorySymbolKey,
                    accentColor: GetItemActiveSegmentColor(_activeInteraction.Item),
                    showSymbol: true);
            }
            else
            {
                HideCategoryNameStrip();
            }
        }

        private bool IsGuideHoldingSubmenuItem(MenuItemConfig item, List<MenuItemConfig> ownerItems, int depth)
        {
            if (!_isAltGuideActive || _activeSubmenuInteraction == null || _activeSubmenuInteraction.Item == null)
            {
                return false;
            }

            return ReferenceEquals(_activeSubmenuInteraction.Item, item) &&
                   ReferenceEquals(_activeSubmenuInteraction.OwnerItems, ownerItems) &&
                   _activeSubmenuInteraction.Depth == depth;
        }

        private void RestoreSegmentShape()
        {
            if (_activeSegment == null || _activeInteraction == null)
            {
                RemoveHoverAccent();
                return;
            }

            var profile = GetLayoutProfile();
            _activeSegment.Data = IsNeonOrbitStyle()
                ? CreateOrbitNodeGeometry(_centerX, _centerY, profile.IconRadius, (_activeInteraction.StartAngle + _activeInteraction.EndAngle) / 2.0, profile.OuterRadius)
                : CreateRingSegmentGeometry(
                    _centerX,
                    _centerY,
                    profile.InnerRadius,
                    profile.OuterRadius,
                    _activeInteraction.StartAngle,
                    _activeInteraction.EndAngle);
            RemoveHoverAccent();
        }

        private void ApplyCategoryHoverShape(ShapePath segment, double startAngle, double endAngle)
        {
            var profile = GetLayoutProfile();
            if (IsNeonOrbitStyle())
            {
                segment.Data = CreateOrbitNodeGeometry(_centerX, _centerY, profile.IconRadius, (startAngle + endAngle) / 2.0, profile.OuterRadius + 8);
            }
            else
            {
                var expandedOuterRadius = profile.OuterRadius + Math.Max(10, profile.OuterRadius * 0.07);
                var submenuStartLimit = profile.SubmenuInnerRadius - Math.Max(2.0, profile.SegmentStrokeThickness + 1.5);
                if (submenuStartLimit > profile.OuterRadius)
                {
                    expandedOuterRadius = Math.Min(expandedOuterRadius, submenuStartLimit);
                }

                expandedOuterRadius = Math.Max(profile.OuterRadius, expandedOuterRadius);
                segment.Data = CreateRingSegmentGeometry(
                    _centerX,
                    _centerY,
                    profile.InnerRadius,
                    expandedOuterRadius,
                    startAngle,
                    endAngle);
            }
            RemoveHoverAccent();
        }

        private void ShowHoverAccent(double startAngle, double endAngle)
        {
            RemoveHoverAccent();

            var profile = GetLayoutProfile();
            var accent = new ShapePath
            {
                Data = IsNeonOrbitStyle()
                    ? CreateOrbitNodeGeometry(_centerX, _centerY, profile.IconRadius, (startAngle + endAngle) / 2.0, profile.OuterRadius + 10)
                    : CreateRingSegmentGeometry(
                        _centerX,
                        _centerY,
                        profile.OuterRadius + 7,
                        profile.OuterRadius + 7 + Math.Max(5, profile.OuterRadius * 0.03),
                        startAngle + 0.8,
                        endAngle - 0.8),
                Fill = new SolidColorBrush(IsNeonOrbitStyle()
                    ? Colors.Transparent
                    : System.Windows.Media.Color.FromArgb(245, 255, 207, 64)),
                Stroke = IsNeonOrbitStyle() && _activeSegment?.Stroke is SolidColorBrush activeStroke
                    ? GetCachedBrush(activeStroke.Color)
                    : null,
                StrokeThickness = IsNeonOrbitStyle() ? 2.2 : 0,
                Opacity = 0.0,
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(
                    IsNeonOrbitStyle() ? 18 : 14,
                    IsNeonOrbitStyle() ? 0.85 : 0.65,
                    IsNeonOrbitStyle() && _activeSegment?.Stroke is SolidColorBrush neonStroke
                        ? neonStroke.Color
                        : System.Windows.Media.Color.FromArgb(255, 255, 199, 48))
            };

            RootCanvas.Children.Add(accent);
            _activeAccentPath = accent;

            var fadeIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(90),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            accent.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void RemoveHoverAccent()
        {
            if (_activeAccentPath == null)
            {
                return;
            }

            RootCanvas.Children.Remove(_activeAccentPath);
            _activeAccentPath = null;
        }
        private void RefreshHoverOrbitGuide()
        {
            try
            {
                if (_selectedInteraction == null ||
                    _selectedInteraction.Item == null ||
                    _selectedInteraction.IsGhostSlot)
                {
                    HideHoverOrbitGuide();
                    return;
                }

                var profile = GetLayoutProfile();
                var selectedItem = _selectedInteraction.Item;
                var selectedDepth = _selectedInteraction.Depth;

                if (selectedDepth > 0)
                {
                    var targetOuterRadius = _activeSubmenuInteraction?.OuterRadius ?? GetSubmenuLayerOuterRadius(profile, selectedDepth);
                    if (IsCategoryItem(selectedItem) && _submenuLayers.ContainsKey(selectedDepth + 1))
                    {
                        targetOuterRadius = GetSubmenuLayerOuterRadius(profile, selectedDepth + 1);
                    }

                    var targetAngle = _activeSubmenuInteraction != null
                        ? GetAngleMidpoint(_activeSubmenuInteraction.StartAngle, _activeSubmenuInteraction.EndAngle)
                        : GetAngleMidpoint(_selectedInteraction.StartAngle, _selectedInteraction.EndAngle);
                    var targetColor = _activeSubmenuInteraction?.AccentColor ?? GetItemActiveSegmentColor(selectedItem);

                    ShowHoverOrbitGuide(
                        targetOuterRadius,
                        targetAngle,
                        targetColor);
                    return;
                }

                if (_activeInteraction?.Item == null)
                {
                    HideHoverOrbitGuide();
                    return;
                }

                var rootItem = _activeInteraction.Item;
                var rootOuterRadius = GetRootHoverOrbitRadius(rootItem, profile);
                if (IsCategoryItem(rootItem) && _submenuLayers.ContainsKey(1))
                {
                    rootOuterRadius = GetSubmenuLayerOuterRadius(profile, 1);
                }

                ShowHoverOrbitGuide(
                    rootOuterRadius,
                    GetAngleMidpoint(_activeInteraction.StartAngle, _activeInteraction.EndAngle),
                    GetItemActiveSegmentColor(rootItem));
            }
            catch (Exception ex)
            {
                LogUiException("Hover orbit göstergesi güncellenirken hata", ex);
                HideHoverOrbitGuide();
            }
        }
        private void ShowHoverOrbitGuide(double outerRadius, double angleDegrees, System.Windows.Media.Color accentColor)
        {
            if (RootCanvas == null || outerRadius <= 0 || double.IsNaN(outerRadius) || double.IsInfinity(outerRadius))
            {
                HideHoverOrbitGuide();
                return;
            }

            var snappedAngle = Math.Round(NormalizeAngle(angleDegrees) / HoverOrbitAngleSnap) * HoverOrbitAngleSnap;
            var nowUtc = DateTime.UtcNow;
            if (_hoverOrbitHasState &&
                _hoverOrbitRing != null &&
                _hoverOrbitRing.Visibility == Visibility.Visible)
            {
                var angleDelta = GetSmallestAngleDelta(snappedAngle, _hoverOrbitLastAngle);
                var radiusDelta = Math.Abs(outerRadius - _hoverOrbitLastOuterRadius);
                var sameAccent = _hoverOrbitLastAccentColor == accentColor;
                if (sameAccent && radiusDelta < 0.06 && angleDelta < 0.06)
                {
                    return;
                }

                if (sameAccent &&
                    (nowUtc - _hoverOrbitLastUpdateUtc) < HoverOrbitMinUpdateInterval &&
                    radiusDelta < 1.2 &&
                    angleDelta < 1.2)
                {
                    return;
                }
            }

            angleDegrees = snappedAngle;
            EnsureHoverOrbitGuideVisuals();

            var displayRadius = outerRadius + Math.Max(4.0, outerRadius * 0.02);
            var diameter = displayRadius * 2.0;
            var left = _centerX - displayRadius;
            var top = _centerY - displayRadius;
            var strokeThickness = Math.Clamp(displayRadius * 0.012, 2.0, 4.4);
            var ringStrokeColor = System.Windows.Media.Color.FromArgb(
                IsNeonOrbitStyle() ? (byte)190 : (byte)150,
                accentColor.R,
                accentColor.G,
                accentColor.B);
            var ringGlowColor = BlendColors(accentColor, Colors.White, IsNeonOrbitStyle() ? 0.18 : 0.28);
            var ringWasVisible = _hoverOrbitRing != null && _hoverOrbitRing.Visibility == Visibility.Visible;
            var animateTransition = !ringWasVisible || !_hoverOrbitHasState || Math.Abs(outerRadius - _hoverOrbitLastOuterRadius) > 0.9;

            if (_hoverOrbitRing != null)
            {
                _hoverOrbitRing.Visibility = Visibility.Visible;
                _hoverOrbitRing.Stroke = GetCachedBrush(ringStrokeColor);
                _hoverOrbitRing.StrokeThickness = strokeThickness;
                var ringEffect = CreateOptimizedShadowEffect(
                    IsNeonOrbitStyle() ? 16 : 12,
                    IsNeonOrbitStyle() ? 0.20 : 0.15,
                    ringGlowColor);
                if (!ReferenceEquals(_hoverOrbitRing.Effect, ringEffect))
                {
                    _hoverOrbitRing.Effect = ringEffect;
                }
                if (double.IsNaN(Canvas.GetLeft(_hoverOrbitRing)))
                {
                    Canvas.SetLeft(_hoverOrbitRing, left);
                }
                if (double.IsNaN(Canvas.GetTop(_hoverOrbitRing)))
                {
                    Canvas.SetTop(_hoverOrbitRing, top);
                }
                if (animateTransition)
                {
                    var currentLeft = CoerceFiniteDouble(Canvas.GetLeft(_hoverOrbitRing), left);
                    var currentTop = CoerceFiniteDouble(Canvas.GetTop(_hoverOrbitRing), top);
                    _hoverOrbitRing.BeginAnimation(Ellipse.WidthProperty, new DoubleAnimation
                    {
                        To = diameter,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    _hoverOrbitRing.BeginAnimation(Ellipse.HeightProperty, new DoubleAnimation
                    {
                        To = diameter,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    _hoverOrbitRing.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation
                    {
                        From = currentLeft,
                        To = left,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    _hoverOrbitRing.BeginAnimation(Canvas.TopProperty, new DoubleAnimation
                    {
                        From = currentTop,
                        To = top,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    _hoverOrbitRing.BeginAnimation(OpacityProperty, new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
                else
                {
                    _hoverOrbitRing.BeginAnimation(Ellipse.WidthProperty, null);
                    _hoverOrbitRing.BeginAnimation(Ellipse.HeightProperty, null);
                    _hoverOrbitRing.BeginAnimation(Canvas.LeftProperty, null);
                    _hoverOrbitRing.BeginAnimation(Canvas.TopProperty, null);
                    _hoverOrbitRing.BeginAnimation(OpacityProperty, null);
                    _hoverOrbitRing.Width = diameter;
                    _hoverOrbitRing.Height = diameter;
                    Canvas.SetLeft(_hoverOrbitRing, left);
                    Canvas.SetTop(_hoverOrbitRing, top);
                    _hoverOrbitRing.Opacity = 1.0;
                }
            }

            var angleRadians = ToRadians(angleDegrees);
            var directionX = Math.Cos(angleRadians);
            var directionY = Math.Sin(angleRadians);
            var perpendicularX = -directionY;
            var perpendicularY = directionX;
            var triangleLength = Math.Clamp(displayRadius * 0.055, 11.0, 22.0) * HoverOrbitPointerScale;
            var triangleWidth = Math.Clamp(displayRadius * 0.05, 10.0, 18.0) * HoverOrbitPointerScale;
            var tipRadius = Math.Max(4.0, displayRadius - (triangleLength * 0.34));
            var baseCenterRadius = displayRadius + (triangleLength * 0.72);
            var tip = PointOnCircle(_centerX, _centerY, tipRadius, angleDegrees);
            var baseCenter = PointOnCircle(_centerX, _centerY, baseCenterRadius, angleDegrees);
            var leftPoint = new WpfPoint(
                baseCenter.X + (perpendicularX * triangleWidth * 0.5),
                baseCenter.Y + (perpendicularY * triangleWidth * 0.5));
            var rightPoint = new WpfPoint(
                baseCenter.X - (perpendicularX * triangleWidth * 0.5),
                baseCenter.Y - (perpendicularY * triangleWidth * 0.5));
            var rearCurveControl = new WpfPoint(
                baseCenter.X + (directionX * triangleWidth * 0.58),
                baseCenter.Y + (directionY * triangleWidth * 0.58));

            if (_hoverOrbitPointer != null)
            {
                _hoverOrbitPointer.Visibility = Visibility.Visible;
                _hoverOrbitPointer.Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(245, accentColor.R, accentColor.G, accentColor.B));
                _hoverOrbitPointer.Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(245, 255, 255, 255));
                _hoverOrbitPointer.StrokeThickness = 0.9;
                var pointerEffect = CreateOptimizedShadowEffect(
                    IsNeonOrbitStyle() ? 10 : 7,
                    IsNeonOrbitStyle() ? 0.16 : 0.10,
                    ringGlowColor);
                if (!ReferenceEquals(_hoverOrbitPointer.Effect, pointerEffect))
                {
                    _hoverOrbitPointer.Effect = pointerEffect;
                }

                UpdateHoverOrbitPointerGeometry(tip, leftPoint, rightPoint, rearCurveControl);
                if (animateTransition)
                {
                    _hoverOrbitPointer.BeginAnimation(OpacityProperty, new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = HoverOrbitGuideTransitionDuration,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
                else
                {
                    _hoverOrbitPointer.BeginAnimation(OpacityProperty, null);
                    _hoverOrbitPointer.Opacity = 1.0;
                }
            }

            _hoverOrbitHasState = true;
            _hoverOrbitLastOuterRadius = outerRadius;
            _hoverOrbitLastAngle = angleDegrees;
            _hoverOrbitLastAccentColor = accentColor;
            _hoverOrbitLastUpdateUtc = nowUtc;
        }

        private void HideHoverOrbitGuide()
        {
            if (_hoverOrbitRing == null && _hoverOrbitPointer == null)
            {
                return;
            }

            if (_hoverOrbitRing != null)
            {
                _hoverOrbitRing.BeginAnimation(Ellipse.WidthProperty, null);
                _hoverOrbitRing.BeginAnimation(Ellipse.HeightProperty, null);
                _hoverOrbitRing.BeginAnimation(Canvas.LeftProperty, null);
                _hoverOrbitRing.BeginAnimation(Canvas.TopProperty, null);
                _hoverOrbitRing.BeginAnimation(OpacityProperty, null);
                _hoverOrbitRing.Opacity = 0.0;
                _hoverOrbitRing.Visibility = Visibility.Collapsed;
            }

            if (_hoverOrbitPointer != null)
            {
                _hoverOrbitPointer.BeginAnimation(OpacityProperty, null);
                _hoverOrbitPointer.Opacity = 0.0;
                _hoverOrbitPointer.Visibility = Visibility.Collapsed;
            }

            _hoverOrbitHasState = false;
            _hoverOrbitLastOuterRadius = double.NaN;
            _hoverOrbitLastAngle = double.NaN;
            _hoverOrbitLastAccentColor = Colors.Transparent;
            _hoverOrbitLastUpdateUtc = DateTime.MinValue;
        }

        private static double CoerceFiniteDouble(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? fallback
                : value;
        }

        private void EnsureHoverOrbitGuideVisuals()
        {
            if (RootCanvas == null)
            {
                return;
            }

            if (_hoverOrbitRing == null)
            {
                _hoverOrbitRing = new Ellipse
                {
                    Width = 0,
                    Height = 0,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255)),
                    StrokeThickness = 0,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    CacheMode = new BitmapCache(),
                    Opacity = 0.0,
                    Visibility = Visibility.Collapsed,
                    Effect = CreateOptimizedShadowEffect(12, 0.16, _theme.CenterBorderColor)
                };
                Canvas.SetLeft(_hoverOrbitRing, _centerX);
                Canvas.SetTop(_hoverOrbitRing, _centerY);
                Panel.SetZIndex(_hoverOrbitRing, 190);
            }

            if (_hoverOrbitPointer == null)
            {
                _hoverOrbitPointerFigure = new PathFigure
                {
                    StartPoint = new WpfPoint(_centerX, _centerY),
                    IsClosed = true,
                    IsFilled = true
                };
                _hoverOrbitPointerLeftSegment = new LineSegment(new WpfPoint(_centerX, _centerY), true);
                _hoverOrbitPointerRearSegment = new QuadraticBezierSegment(
                    new WpfPoint(_centerX, _centerY),
                    new WpfPoint(_centerX, _centerY),
                    true);
                _hoverOrbitPointerFigure.Segments.Add(_hoverOrbitPointerLeftSegment);
                _hoverOrbitPointerFigure.Segments.Add(_hoverOrbitPointerRearSegment);
                _hoverOrbitPointerGeometry = new PathGeometry();
                _hoverOrbitPointerGeometry.Figures.Add(_hoverOrbitPointerFigure);

                _hoverOrbitPointer = new ShapePath
                {
                    Data = _hoverOrbitPointerGeometry,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255)),
                    StrokeThickness = 0,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    CacheMode = new BitmapCache(),
                    Opacity = 0.0,
                    Visibility = Visibility.Collapsed
                };
                Panel.SetZIndex(_hoverOrbitPointer, 192);
            }

            if (!RootCanvas.Children.Contains(_hoverOrbitRing))
            {
                RootCanvas.Children.Add(_hoverOrbitRing);
            }

            if (!RootCanvas.Children.Contains(_hoverOrbitPointer))
            {
                RootCanvas.Children.Add(_hoverOrbitPointer);
            }
        }

        private double GetRootHoverOrbitRadius(MenuItemConfig item, LayoutProfile profile)
        {
            var radius = profile.OuterRadius;
            if (IsCategoryItem(item))
            {
                radius += IsNeonOrbitStyle()
                    ? 8
                    : Math.Max(10, profile.OuterRadius * 0.07);
            }

            return radius;
        }

        private double GetSubmenuLayerOuterRadius(LayoutProfile profile, int depth)
        {
            var normalizedDepth = Math.Max(1, depth);
            var layerGap = IsNeonOrbitStyle()
                ? Math.Max(34, profile.SubmenuIconSize + 18)
                : Math.Max(26, profile.SubmenuOuterRadius - profile.SubmenuInnerRadius + 8);

            return profile.SubmenuOuterRadius + ((normalizedDepth - 1) * layerGap);
        }

        private void UpdateHoverOrbitPointerGeometry(
            WpfPoint tip,
            WpfPoint leftPoint,
            WpfPoint rightPoint,
            WpfPoint rearCurveControl)
        {
            if (_hoverOrbitPointerFigure == null ||
                _hoverOrbitPointerLeftSegment == null ||
                _hoverOrbitPointerRearSegment == null)
            {
                return;
            }

            _hoverOrbitPointerFigure.StartPoint = tip;
            _hoverOrbitPointerLeftSegment.Point = leftPoint;
            _hoverOrbitPointerRearSegment.Point1 = rearCurveControl;
            _hoverOrbitPointerRearSegment.Point2 = rightPoint;
        }

        private static double GetSmallestAngleDelta(double firstAngle, double secondAngle)
        {
            var delta = Math.Abs(NormalizeAngle(firstAngle) - NormalizeAngle(secondAngle));
            return delta > 180.0 ? 360.0 - delta : delta;
        }

        private static double GetAngleMidpoint(double startAngle, double endAngle)
        {
            var normalizedStart = NormalizeAngle(startAngle);
            var normalizedEnd = NormalizeAngle(endAngle);
            var sweep = normalizedEnd - normalizedStart;
            if (sweep < 0)
            {
                sweep += 360.0;
            }

            return NormalizeAngle(normalizedStart + (sweep / 2.0));
        }

        private void ShowSubmenu(
            List<MenuItemConfig> items,
            double parentStartAngle,
            double parentEndAngle,
            IReadOnlyList<string>? breadcrumbPath = null,
            int depth = 1,
            DragItemInteraction? ghostSlot = null,
            System.Windows.Media.Color? inheritedColor = null)
        {
            HideSubmenu(depth);
            var slotCount = items.Count + (ghostSlot != null ? 1 : 0);
            if (slotCount == 0)
            {
                return;
            }

            var profile = GetLayoutProfile();
            var parentMidAngle = (parentStartAngle + parentEndAngle) / 2.0;
            var parentSpan = Math.Abs(parentEndAngle - parentStartAngle);
            var desiredSpan = Math.Max(parentSpan * profile.SubmenuExpandFactor, Math.Min(profile.SubmenuMaxSweep, profile.SubmenuMinPerItemSweep * slotCount));
            var totalSweep = IsNeonOrbitStyle()
                ? Math.Min(profile.SubmenuMaxSweep, Math.Max(parentSpan * 1.45, profile.SubmenuMinPerItemSweep * slotCount))
                : Math.Min(profile.SubmenuMaxSweep, desiredSpan);
            if (slotCount == 1)
            {
                var minimumSingleSweep = IsNeonOrbitStyle() ? 42.0 : 64.0;
                totalSweep = Math.Max(totalSweep, minimumSingleSweep);
            }

            var startBase = parentMidAngle - totalSweep / 2.0;
            var step = totalSweep / slotCount;
            var safeInset = Math.Min(profile.SubmenuInset, Math.Max(0.0, (step * 0.45) - 0.2));
            var layerElements = new List<UIElement>();
            var layerGap = IsNeonOrbitStyle()
                ? Math.Max(34, profile.SubmenuIconSize + 18)
                : Math.Max(26, profile.SubmenuOuterRadius - profile.SubmenuInnerRadius + 8);
            var layerInnerRadius = profile.SubmenuInnerRadius + ((depth - 1) * layerGap);
            var layerOuterRadius = profile.SubmenuOuterRadius + ((depth - 1) * layerGap);
            var layerIconRadius = profile.SubmenuIconRadius + ((depth - 1) * layerGap);
            var layerNodeRadius = IsNeonOrbitStyle()
                ? Math.Max(18, (profile.SubmenuIconSize * 0.64) + ((depth - 1) * 2))
                : layerOuterRadius;

            for (var i = 0; i < slotCount; i++)
            {
                var startAngle = startBase + step * i + safeInset;
                var endAngle = startBase + step * (i + 1) - safeInset;
                if (endAngle <= startAngle)
                {
                    var fallbackMiddleAngle = startBase + (step * (i + 0.5));
                    var halfSweep = Math.Max(0.6, step * 0.35);
                    startAngle = fallbackMiddleAngle - halfSweep;
                    endAngle = fallbackMiddleAngle + halfSweep;
                }

                var middleAngle = (startAngle + endAngle) / 2.0;
                var isGhostSlot = ghostSlot != null && i == slotCount - 1;
                var item = !isGhostSlot ? items[i] : null;
                var submenuColor = IsNeonOrbitStyle()
                    ? inheritedColor ?? GetNeonOrbitColor(i + depth)
                    : isGhostSlot
                        ? inheritedColor ?? _theme.SegmentColor
                        : GetItemBaseSegmentColor(item!, inheritedColor);

                var segment = new ShapePath
                {
                    Data = IsNeonOrbitStyle()
                        ? CreateOrbitNodeGeometry(_centerX, _centerY, layerIconRadius, middleAngle, layerNodeRadius)
                        : CreateRingSegmentGeometry(_centerX, _centerY, layerInnerRadius, layerOuterRadius, startAngle, endAngle),
                    Fill = isGhostSlot
                        ? GetCachedBrush(System.Windows.Media.Color.FromArgb(86, 255, 214, 102))
                        : new SolidColorBrush(IsNeonOrbitStyle()
                            ? System.Windows.Media.Color.FromArgb(0, submenuColor.R, submenuColor.G, submenuColor.B)
                            : GetItemBaseSegmentColor(item!, inheritedColor)),
                    Stroke = GetCachedBrush(IsNeonOrbitStyle() ? submenuColor : GetItemStrokeColor(item!, inheritedColor)),
                    StrokeThickness = profile.SubmenuStrokeThickness,
                    Tag = item,
                    Effect = IsNeonOrbitStyle()
                        ? CreateOptimizedShadowEffect(10, 0.22, submenuColor)
                        : null
                };
                if (isGhostSlot)
                {
                    segment.StrokeDashArray = SubmenuGhostDashArray;
                    segment.Opacity = 0.9;
                }
                var segmentBrush = (SolidColorBrush)segment.Fill;

                var iconHost = isGhostSlot
                    ? CreateGhostSlotHost(profile.SubmenuIconSize)
                    : CreateSubmenuIconHost(item!, profile.SubmenuIconSize, startAngle, endAngle);
                if (isGhostSlot)
                {
                    var ghostTarget = new DragItemInteraction(
                        item: null,
                        ownerItems: ghostSlot!.OwnerItems,
                        itemIndex: -1,
                        depth: depth,
                        startAngle: startAngle,
                        endAngle: endAngle,
                        breadcrumbPath: ghostSlot.BreadcrumbPath,
                        isGhostSlot: true,
                        insertIndex: ghostSlot.InsertIndex);
                    _dragInteractions[segment] = ghostTarget;
                    _dragInteractions[iconHost] = ghostTarget;
                    RegisterHoverInteraction(
                        segment,
                        iconHost,
                        item: null,
                        ghostSlot.OwnerItems,
                        itemIndex: -1,
                        depth,
                        startAngle,
                        endAngle,
                        ghostSlot.BreadcrumbPath,
                        segmentBrush,
                        submenuColor,
                        isGhostSlot: true,
                        innerRadius: layerInnerRadius,
                        outerRadius: layerOuterRadius,
                        hitCenter: IsNeonOrbitStyle() ? PointOnCircle(_centerX, _centerY, layerIconRadius, middleAngle) : (WpfPoint?)null,
                        hitRadius: IsNeonOrbitStyle() ? layerNodeRadius : 0);
                }
                else
                {
                    RegisterDragInteraction(segment, iconHost, item!, items, i, depth, startAngle, endAngle, breadcrumbPath);
                    RegisterHoverInteraction(
                        segment,
                        iconHost,
                        item!,
                        items,
                        i,
                        depth,
                        startAngle,
                        endAngle,
                        breadcrumbPath,
                        segmentBrush,
                        submenuColor,
                        isGhostSlot: false,
                        innerRadius: layerInnerRadius,
                        outerRadius: layerOuterRadius,
                        hitCenter: IsNeonOrbitStyle() ? PointOnCircle(_centerX, _centerY, layerIconRadius, middleAngle) : (WpfPoint?)null,
                        hitRadius: IsNeonOrbitStyle() ? layerNodeRadius : 0);
                }
                var itemRadius = layerIconRadius;
                var iconX = _centerX + Math.Cos(ToRadians(middleAngle)) * itemRadius - iconHost.Width / 2.0;
                var iconY = _centerY + Math.Sin(ToRadians(middleAngle)) * itemRadius - iconHost.Height / 2.0;
                Canvas.SetLeft(iconHost, iconX);
                Canvas.SetTop(iconHost, iconY);

                void SelectSub()
                {
                    try
                    {
                        var currentItem = item;
                        if (_itemContextMenu != null)
                        {
                            return;
                        }

                        HideItemContextMenu();
                        if (isGhostSlot)
                        {
                            segment.Opacity = 1.0;
                            iconHost.Opacity = 1.0;
                            if (_centerTitle != null)
                            {
                                SetCenterTitleText("Kategoriye Ekle", useIdleClockTypography: false);
                            }
                            UpdateCenterBreadcrumb(ghostSlot?.BreadcrumbPath ?? Array.Empty<string>());
                            if (_centerSubtitle != null)
                            {
                                AnimateCenterText(_centerSubtitle, "Buraya b\u0131rakarak kategori i\u00E7ine ta\u015F\u0131y\u0131n");
                            }
                            return;
                        }

                        if (currentItem == null)
                        {
                            return;
                        }

                        if (_hoverInteractions.TryGetValue(segment, out var registeredInteraction))
                        {
                            ApplySubmenuSelection(registeredInteraction);
                            UpdateAltGuideVisual();
                            return;
                        }

                        AnimateSubmenuSegment(segmentBrush, currentItem, true, submenuColor);
                        if (IsNeonOrbitStyle())
                        {
                            segmentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                            {
                                To = System.Windows.Media.Color.FromArgb(52, submenuColor.R, submenuColor.G, submenuColor.B),
                                Duration = TimeSpan.FromMilliseconds(120),
                                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                            });
                        }
                        iconHost.Background = CreateHostBackground(currentItem, isActive: true);
                        iconHost.BorderBrush = CreateHostBorderBrush(currentItem, isActive: true);
                        AnimateIconHostState(iconHost, SubmenuHoverScale, profile.SubmenuHoverOpacity);
                        _selectedItem = currentItem;
                        _selectedInteraction = new DragItemInteraction(currentItem, items, i, depth, startAngle, endAngle, breadcrumbPath);
                        if (_centerTitle != null)
                        {
                            SetCenterTitleText(currentItem.Label, useIdleClockTypography: false);
                        }
                        UpdateCenterBreadcrumb(breadcrumbPath ?? Array.Empty<string>());
                        if (_centerSubtitle != null)
                        {
                            AnimateCenterText(_centerSubtitle, IsCategoryItem(currentItem)
                                ? "Bu \u00F6\u011Fe de alt men\u00FC ta\u015F\u0131yor"
                                : "T\u0131klayarak a\u00E7");
                        }

                        if (IsCategoryItem(currentItem))
                        {
                            var nextPath = new List<string>(breadcrumbPath ?? Array.Empty<string>())
                            {
                                currentItem.Label
                            };
                            ShowSubmenu(
                                currentItem.Children,
                                startAngle,
                                endAngle,
                                nextPath,
                                depth + 1,
                                inheritedColor: GetSubmenuInheritedColor(currentItem, submenuColor));
                        }

                        UpdateAltGuideVisual();
                    }
                    catch (Exception ex)
                    {
                        LogUiException("Alt menü hover seçimi sırasında hata", ex);
                    }
                }

                void ResetSub()
                {
                    if (isGhostSlot)
                    {
                        segment.Opacity = 0.9;
                        iconHost.Opacity = 0.92;
                        return;
                    }

                    if (IsGuideHoldingSubmenuItem(item!, items, depth))
                    {
                        ReapplySubmenuSelectionVisual(new HoverInteraction(
                            segment,
                            iconHost,
                            item,
                            items,
                            i,
                            depth,
                            startAngle,
                            endAngle,
                            breadcrumbPath,
                            segmentBrush,
                            submenuColor,
                            isGhostSlot: false));
                        return;
                    }

                    AnimateSubmenuSegment(segmentBrush, item!, false, submenuColor);
                    if (IsNeonOrbitStyle())
                    {
                        segmentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                        {
                            To = System.Windows.Media.Color.FromArgb(0, submenuColor.R, submenuColor.G, submenuColor.B),
                            Duration = TimeSpan.FromMilliseconds(140),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                        });
                    }
                    iconHost.Background = CreateHostBackground(item!, isActive: false);
                    iconHost.BorderBrush = CreateHostBorderBrush(item!, isActive: false);
                    AnimateIconHostState(iconHost, 1.0, 1.0);
                }

                void LaunchSub()
                {
                    var currentItem = item;
                    if (isGhostSlot)
                    {
                        return;
                    }

                    if (IsEditModifierActive())
                    {
                        return;
                    }

                    if (currentItem == null || IsCategoryItem(currentItem))
                    {
                        return;
                    }

                    PlayLaunchFeedback(iconHost);
                    RequestLaunchAndDismiss(currentItem.TargetPath, launchDelayMs: 58);
                }

                segment.MouseEnter += (_, __) => SelectSub();
                segment.MouseMove += (_, __) => SelectSub();
                iconHost.MouseEnter += (_, __) => SelectSub();
                iconHost.MouseMove += (_, __) => SelectSub();
                segment.PreviewMouseLeftButtonDown += (_, e) => BeginPotentialDrag(e, segment);
                iconHost.PreviewMouseLeftButtonDown += (_, e) => BeginPotentialDrag(e, iconHost);
                segment.PreviewMouseLeftButtonUp += (_, __) => ClearPendingDragState();
                iconHost.PreviewMouseLeftButtonUp += (_, __) => ClearPendingDragState();
                if (!isGhostSlot)
                {
                    segment.MouseRightButtonUp += (_, e) => ShowItemContextMenuForSource(segment, e);
                    iconHost.MouseRightButtonUp += (_, e) => ShowItemContextMenuForSource(iconHost, e);
                }
                segment.MouseLeave += (_, __) => ResetSub();
                iconHost.MouseLeave += (_, __) => ResetSub();
                segment.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    LaunchSub();
                };
                iconHost.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    LaunchSub();
                };

                RootCanvas.Children.Add(segment);
                RootCanvas.Children.Add(iconHost);
                layerElements.Add(segment);
                layerElements.Add(iconHost);
            }

            _submenuLayers[depth] = layerElements;

            Dispatcher.BeginInvoke(new Action(UpdateSelectionUnderCursor));
        }

        private void UpdateCenterBreadcrumb(IReadOnlyList<string> path)
        {
            if (_centerBreadcrumb == null)
            {
                return;
            }

            var breadcrumbText = path.Count == 0
                ? string.Empty
                : string.Join(" > ", path);
            AnimateCenterText(_centerBreadcrumb, breadcrumbText);
        }

        private Border CreateSubmenuIconHost(MenuItemConfig item, double size, double startAngle, double endAngle)
        {
            var profile = GetLayoutProfile();
            var hoverLabel = CreateItemLabel(item.Label, size, isSubmenu: true);
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(CreateMenuItemVisual(item, size, isSubmenu: true, startAngle, endAngle));

            var hostWidth = size;
            var hostHeight = size;

            return new Border
            {
                Width = hostWidth,
                Height = hostHeight,
                Tag = Tuple.Create(item, hoverLabel),
                Child = stack,
                Background = CreateHostBackground(item, isActive: false),
                BorderBrush = CreateHostBorderBrush(item, isActive: false),
                BorderThickness = new Thickness(IsNeonOrbitStyle() ? 0 : profile.ShowIconChrome && _theme.IconBorderColor.A > 0 ? 1 : 0),
                CornerRadius = new CornerRadius(999),
                RenderTransformOrigin = new WpfPoint(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Effect = CreateOptimizedShadowEffect(
                    IsNeonOrbitStyle() ? 0 : profile.ShowIconChrome ? 12 : 0,
                    profile.ShowIconChrome ? 0.28 : 0,
                    _theme.ShadowColor)
            };
        }

        private Border CreateGhostSlotHost(double size)
        {
            var label = new TextBlock
            {
                Text = "Birak",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(235, 255, 228, 150)),
                FontSize = Math.Clamp(size * 0.22, 8.5, 10.5),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var plus = new TextBlock
            {
                Text = "+",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(250, 255, 217, 118)),
                FontSize = Math.Clamp(size * 0.50, 15, 20),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -2, 0, -1)
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(plus);
            stack.Children.Add(label);

            return new Border
            {
                Width = size,
                Height = size,
                Background = GetCachedBrush(System.Windows.Media.Color.FromArgb(36, 255, 223, 132)),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(190, 255, 217, 118)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Opacity = 0.92,
                Child = stack
            };
        }

        private FrameworkElement CreateMenuItemVisual(MenuItemConfig item, double size, bool isSubmenu, double startAngle, double endAngle)
        {
            if (IsCategoryItem(item))
            {
                return CreateCategoryVisual(item, size, isSubmenu);
            }

            var target = item.TargetPath ?? "";
            bool isDirectory = !string.IsNullOrWhiteSpace(target) && 
                              (Directory.Exists(target) || Directory.Exists(Environment.ExpandEnvironmentVariables(target)));

            if (isDirectory)
            {
                return CreateFolderVisual(size, isSubmenu);
            }

            var iconVisualSize = isSubmenu
                ? Math.Clamp(size * 0.94, 34, 52)
                : Math.Clamp(size * 0.96, 38, 64);

            return new Image
            {
                Width = iconVisualSize,
                Height = iconVisualSize,
                Source = LoadIcon(item.TargetPath ?? string.Empty),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private FrameworkElement CreateCategoryVisual(MenuItemConfig item, double size, bool isSubmenu)
        {
            var iconSize = isSubmenu ? Math.Clamp(size * 0.68, 24, 34) : Math.Clamp(size * 0.78, 34, 48);
            var padding = isSubmenu ? 3.0 : 4.0;
            var stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(230, 246, 230, 190));
            var visual = CategorySymbolService.CreateSymbolVisual(item.CategorySymbolKey, iconSize, stroke);
            visual.Margin = new Thickness(padding);
            return new Grid
            {
                Width = iconSize + (padding * 2),
                Height = iconSize + (padding * 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    visual
                }
            };
        }

        private FrameworkElement CreateFolderVisual(double size, bool isSubmenu)
        {
            var iconSize = isSubmenu ? Math.Clamp(size * 0.52, 20, 26) : Math.Clamp(size * 0.60, 26, 36);
            var stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(230, 246, 230, 190));
            var fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(55, 255, 214, 84));
            var accent = GetCachedBrush(System.Windows.Media.Color.FromArgb(165, 255, 190, 64));
            var canvas = new Canvas
            {
                Width = iconSize,
                Height = iconSize,
                Background = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var body = new Border
            {
                Width = iconSize * 0.92,
                Height = iconSize * 0.62,
                Background = fill,
                BorderBrush = stroke,
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(4)
            };
            Canvas.SetLeft(body, iconSize * 0.04);
            Canvas.SetTop(body, iconSize * 0.28);

            var tab = new Border
            {
                Width = iconSize * 0.34,
                Height = iconSize * 0.18,
                Background = accent,
                BorderBrush = stroke,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(3, 3, 0, 0)
            };
            Canvas.SetLeft(tab, iconSize * 0.12);
            Canvas.SetTop(tab, iconSize * 0.12);

            var innerLine = new Border
            {
                Width = iconSize * 0.42,
                Height = 1.6,
                Background = stroke,
                Opacity = 0.7,
                CornerRadius = new CornerRadius(1)
            };
            Canvas.SetLeft(innerLine, iconSize * 0.24);
            Canvas.SetTop(innerLine, iconSize * 0.5);

            canvas.Children.Add(body);
            canvas.Children.Add(tab);
            canvas.Children.Add(innerLine);

            return canvas;
        }

        private TextBlock CreateItemLabel(string text, double size, bool isSubmenu)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = GetCachedBrush(Colors.White),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = isSubmenu ? Math.Clamp(size * 0.18, 7.5, 9.5) : Math.Clamp(size * 0.19, 8.5, 11),
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                LineHeight = isSubmenu ? 11.8 : 12.4,
                MaxWidth = Math.Max(46, size * 1.15),
                Margin = new Thickness(-8, isSubmenu ? 3 : 5, -8, 0),
                Effect = CreateOptimizedShadowEffect(4, 0.85, Colors.Black, 1)
            };
        }

        private System.Windows.Media.Brush CreateHostBackground(MenuItemConfig item, bool isActive)
        {
            if (IsNeonOrbitStyle())
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            if (IsCategoryItem(item))
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            var baseColor = isActive ? _theme.IconActiveColor : _theme.IconColor;
            if (baseColor.A <= 0)
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            var topBlend = BlendColors(baseColor, Colors.White, isActive ? 0.18 : 0.08);
            var bottomBlend = BlendColors(baseColor, _theme.CenterColor, isActive ? 0.20 : 0.30);
            var topAlpha = (byte)Math.Clamp(baseColor.A + (isActive ? 20 : 10), 0, 255);
            var bottomAlpha = (byte)Math.Clamp(baseColor.A + (isActive ? 8 : 0), 0, 255);
            return GetCachedLinearGradientBrush(
                System.Windows.Media.Color.FromArgb(topAlpha, topBlend.R, topBlend.G, topBlend.B),
                System.Windows.Media.Color.FromArgb(bottomAlpha, bottomBlend.R, bottomBlend.G, bottomBlend.B),
                new WpfPoint(0.12, 0.04),
                new WpfPoint(0.88, 0.96));
        }

        private System.Windows.Media.Brush CreateHostBorderBrush(MenuItemConfig item, bool isActive)
        {
            if (IsNeonOrbitStyle())
            {
                return System.Windows.Media.Brushes.Transparent;
            }

            if (!IsCategoryItem(item))
            {
                var borderBase = isActive ? _theme.IconActiveBorderColor : _theme.IconBorderColor;
                if (borderBase.A <= 0)
                {
                    return System.Windows.Media.Brushes.Transparent;
                }

                var borderBlend = BlendColors(
                    borderBase,
                    Colors.White,
                    isActive ? 0.26 : 0.12);
                return GetCachedBrush(System.Windows.Media.Color.FromArgb(borderBase.A, borderBlend.R, borderBlend.G, borderBlend.B));
            }

            return System.Windows.Media.Brushes.Transparent;
        }

        private static bool IsLightColor(System.Windows.Media.Color color)
        {
            var luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            return luminance >= 150;
        }

        private static double GetItemRadius(MenuItemConfig item, LayoutProfile profile)
        {
            return profile.IconRadius;
        }

        private static double GetSubmenuItemRadius(MenuItemConfig item, LayoutProfile profile)
        {
            return profile.SubmenuIconRadius;
        }

        private void HideSubmenu()
        {
            HideSubmenu(1);
        }

        private void HideSubmenu(int fromDepth)
        {
            if (fromDepth <= 1)
            {
                CancelSubmenuCloseGrace();
            }

            if (_activeSubmenuInteraction != null && _activeSubmenuInteraction.Depth >= fromDepth)
            {
                _activeSubmenuInteraction = null;
            }

            if (_selectedInteraction != null && _selectedInteraction.Depth >= fromDepth)
            {
                _selectedInteraction = _activeInteraction != null
                    ? new DragItemInteraction(
                        _activeInteraction.Item,
                        _pages[_currentPageIndex],
                        _activeInteraction.ItemIndex,
                        0,
                        _activeInteraction.StartAngle,
                        _activeInteraction.EndAngle)
                    : null;
            }

            foreach (var depth in _submenuLayers.Keys.Where(x => x >= fromDepth).ToList())
            {
                foreach (var element in _submenuLayers[depth])
                {
                    UnregisterInteractionElement(element);
                    RootCanvas.Children.Remove(element);
                }
                _submenuLayers.Remove(depth);
            }
        }

        private void UnregisterInteractionElement(UIElement element)
        {
            _dragInteractions.Remove(element);
            if (_hoverInteractions.TryGetValue(element, out var hoverInteraction))
            {
                _hoverInteractions.Remove(element);
                _hoverInteractionEntries.Remove(hoverInteraction);
            }
        }

        private void AnimateSubmenuSegment(SolidColorBrush brush, MenuItemConfig item, bool active)
        {
            var target = active
                ? GetItemActiveSegmentColor(item)
                : GetItemBaseSegmentColor(item);
            if (brush.Color == target)
            {
                return;
            }

            var animation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(active ? 108 : 142),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void AnimateSubmenuSegment(SolidColorBrush brush, MenuItemConfig item, bool active, System.Windows.Media.Color? inheritedColor)
        {
            var target = active
                ? GetItemActiveSegmentColor(item, inheritedColor)
                : GetItemBaseSegmentColor(item, inheritedColor);
            if (brush.Color == target)
            {
                return;
            }

            var animation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(active ? 108 : 142),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private LayoutProfile GetLayoutProfile()
        {
            if (string.Equals(_menuStyle, "Style2", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 126, OuterRadius = 220, IconRadius = 173, IconSize = 44, SegmentGap = 0,
                    StartAngle = -90, SweepAngle = 360, CenterSize = 178, CenterBorder = 1.5, CenterBlur = 18,
                    ShadowBlur = 10, ShadowPad = 10, ShadowAlpha = 18, SegmentStrokeThickness = 1, SegmentBlur = 4, SegmentShadowOpacity = 0.08,
                    TitleFontSize = 20, SubtitleFontSize = 13, HoverIconSize = 56, HoverOpacity = 1, ShowHoverLabel = false, ShowIconChrome = false,
                    UseCompactChevron = true, SubmenuInset = 0, SubmenuInnerRadius = 228, SubmenuOuterRadius = 300, SubmenuIconRadius = 264,
                    SubmenuIconSize = 40, SubmenuStrokeThickness = 1, SubmenuHoverOpacity = 1, SubmenuExpandFactor = 3.0, SubmenuMaxSweep = 230, SubmenuMinPerItemSweep = 34
                };
            }

            if (string.Equals(_menuStyle, "Style3", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 150, OuterRadius = 208, IconRadius = 179, IconSize = 36, SegmentGap = 1,
                    StartAngle = -90, SweepAngle = 360, CenterSize = 164, CenterBorder = 1, CenterBlur = 12,
                    ShadowBlur = 8, ShadowPad = 6, ShadowAlpha = 14, SegmentStrokeThickness = 1.2, SegmentBlur = 2, SegmentShadowOpacity = 0.04,
                    TitleFontSize = 19, SubtitleFontSize = 12, HoverIconSize = 46, HoverOpacity = 1, ShowHoverLabel = false, ShowIconChrome = false,
                    UseCompactChevron = true, SubmenuInset = 0.6, SubmenuInnerRadius = 220, SubmenuOuterRadius = 264, SubmenuIconRadius = 242,
                    SubmenuIconSize = 34, SubmenuStrokeThickness = 1, SubmenuHoverOpacity = 1, SubmenuExpandFactor = 3.2, SubmenuMaxSweep = 240, SubmenuMinPerItemSweep = 32
                };
            }

            if (string.Equals(_menuStyle, "Style4", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 104, OuterRadius = 236, IconRadius = 176, IconSize = 52, SegmentGap = 4,
                    StartAngle = -90, SweepAngle = 360, CenterSize = 214, CenterBorder = 2.5, CenterBlur = 28,
                    ShadowBlur = 20, ShadowPad = 26, ShadowAlpha = 34, SegmentStrokeThickness = 2.2, SegmentBlur = 12, SegmentShadowOpacity = 0.18,
                    TitleFontSize = 24, SubtitleFontSize = 15, HoverIconSize = 88, HoverOpacity = 0.8, ShowHoverLabel = true, ShowIconChrome = true,
                    UseCompactChevron = false, SubmenuInset = 2, SubmenuInnerRadius = 248, SubmenuOuterRadius = 346, SubmenuIconRadius = 296,
                    SubmenuIconSize = 50, SubmenuStrokeThickness = 1.8, SubmenuHoverOpacity = 0.9, SubmenuExpandFactor = 2.8, SubmenuMaxSweep = 220, SubmenuMinPerItemSweep = 34
                };
            }

            if (string.Equals(_menuStyle, "Style5", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 154, OuterRadius = 202, IconRadius = 178, IconSize = 34, SegmentGap = 0,
                    StartAngle = -90, SweepAngle = 360, CenterSize = 150, CenterBorder = 1, CenterBlur = 8,
                    ShadowBlur = 4, ShadowPad = 4, ShadowAlpha = 10, SegmentStrokeThickness = 0.8, SegmentBlur = 0, SegmentShadowOpacity = 0,
                    TitleFontSize = 18, SubtitleFontSize = 11, HoverIconSize = 40, HoverOpacity = 1, ShowHoverLabel = false, ShowIconChrome = false,
                    UseCompactChevron = true, SubmenuInset = 0, SubmenuInnerRadius = 214, SubmenuOuterRadius = 252, SubmenuIconRadius = 234,
                    SubmenuIconSize = 30, SubmenuStrokeThickness = 0.8, SubmenuHoverOpacity = 1, SubmenuExpandFactor = 3.4, SubmenuMaxSweep = 240, SubmenuMinPerItemSweep = 30
                };
            }

            if (string.Equals(_menuStyle, "Style6", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 142, OuterRadius = 206, IconRadius = 176, IconSize = 40, SegmentGap = 2,
                    StartAngle = -210, SweepAngle = 240, CenterSize = 150, CenterBorder = 1.2, CenterBlur = 10,
                    ShadowBlur = 10, ShadowPad = 8, ShadowAlpha = 18, SegmentStrokeThickness = 1.2, SegmentBlur = 2, SegmentShadowOpacity = 0.06,
                    TitleFontSize = 18, SubtitleFontSize = 11, HoverIconSize = 48, HoverOpacity = 1, ShowHoverLabel = false, ShowIconChrome = false,
                    UseCompactChevron = true, SubmenuInset = 0.8, SubmenuInnerRadius = 218, SubmenuOuterRadius = 272, SubmenuIconRadius = 244,
                    SubmenuIconSize = 34, SubmenuStrokeThickness = 1, SubmenuHoverOpacity = 1, SubmenuExpandFactor = 3.0, SubmenuMaxSweep = 190, SubmenuMinPerItemSweep = 28
                };
            }

            if (string.Equals(_menuStyle, "Style7", StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutProfile
                {
                    InnerRadius = 108, OuterRadius = 40, IconRadius = 168, IconSize = 46, SegmentGap = 0,
                    StartAngle = -90, SweepAngle = 360, CenterSize = 88, CenterBorder = 1.8, CenterBlur = 12,
                    ShadowBlur = 0, ShadowPad = 0, ShadowAlpha = 0, SegmentStrokeThickness = 2.2, SegmentBlur = 0, SegmentShadowOpacity = 0,
                    TitleFontSize = 22, SubtitleFontSize = 11, HoverIconSize = 54, HoverOpacity = 1, ShowHoverLabel = false, ShowIconChrome = false,
                    UseCompactChevron = true, SubmenuInset = 0, SubmenuInnerRadius = 216, SubmenuOuterRadius = 28, SubmenuIconRadius = 246,
                    SubmenuIconSize = 34, SubmenuStrokeThickness = 1.8, SubmenuHoverOpacity = 1, SubmenuExpandFactor = 1.9, SubmenuMaxSweep = 132, SubmenuMinPerItemSweep = 18
                };
            }

            return new LayoutProfile
            {
                InnerRadius = 114, OuterRadius = 224, IconRadius = 168, IconSize = 48, SegmentGap = 2,
                StartAngle = -90, SweepAngle = 360, CenterSize = 194, CenterBorder = 1.6, CenterBlur = 22,
                ShadowBlur = 14, ShadowPad = 16, ShadowAlpha = 24, SegmentStrokeThickness = 1.6, SegmentBlur = 7, SegmentShadowOpacity = 0.11,
                TitleFontSize = 22, SubtitleFontSize = 13, HoverIconSize = 62, HoverOpacity = 0.92, ShowHoverLabel = true, ShowIconChrome = true,
                UseCompactChevron = false, SubmenuInset = 1.2, SubmenuInnerRadius = 240, SubmenuOuterRadius = 322, SubmenuIconRadius = 281,
                SubmenuIconSize = 44, SubmenuStrokeThickness = 1.3, SubmenuHoverOpacity = 0.92, SubmenuExpandFactor = 3.0, SubmenuMaxSweep = 220, SubmenuMinPerItemSweep = 32
            };
        }

        private System.Windows.Media.Color GetSegmentStrokeColor()
        {
            if (string.Equals(_menuStyle, "Style3", StringComparison.OrdinalIgnoreCase))
            {
                return System.Windows.Media.Color.FromArgb(170, _theme.SegmentStrokeColor.R, _theme.SegmentStrokeColor.G, _theme.SegmentStrokeColor.B);
            }

            if (string.Equals(_menuStyle, "Style5", StringComparison.OrdinalIgnoreCase))
            {
                return System.Windows.Media.Color.FromArgb(120, _theme.SegmentStrokeColor.R, _theme.SegmentStrokeColor.G, _theme.SegmentStrokeColor.B);
            }

            if (string.Equals(_menuStyle, "Style7", StringComparison.OrdinalIgnoreCase))
            {
                return Colors.White;
            }

            if (string.Equals(_menuStyle, "Style2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_menuStyle, "Style6", StringComparison.OrdinalIgnoreCase))
            {
                return System.Windows.Media.Color.FromArgb(92, _theme.SegmentStrokeColor.R, _theme.SegmentStrokeColor.G, _theme.SegmentStrokeColor.B);
            }

            return _theme.SegmentStrokeColor;
        }

        private System.Windows.Media.Color GetSubmenuStrokeColor()
        {
            var baseColor = GetSegmentStrokeColor();
            return System.Windows.Media.Color.FromArgb((byte)Math.Min(190, baseColor.A + 30), baseColor.R, baseColor.G, baseColor.B);
        }

        private System.Windows.Media.Color? GetSubmenuInheritedColor(MenuItemConfig item, System.Windows.Media.Color? fallbackColor = null)
        {
            if (TryGetFixedColor(item, out var customColor))
            {
                return customColor;
            }

            return fallbackColor;
        }

        private System.Windows.Media.Color GetItemBaseSegmentColor(MenuItemConfig item)
        {
            if (!TryGetFixedColor(item, out var customColor))
            {
                return _theme.SegmentColor;
            }

            return System.Windows.Media.Color.FromArgb(_theme.SegmentColor.A, customColor.R, customColor.G, customColor.B);
        }

        private System.Windows.Media.Color GetItemBaseSegmentColor(MenuItemConfig item, System.Windows.Media.Color? inheritedColor)
        {
            if (TryGetFixedColor(item, out var customColor))
            {
                return System.Windows.Media.Color.FromArgb(_theme.SegmentColor.A, customColor.R, customColor.G, customColor.B);
            }

            if (inheritedColor.HasValue)
            {
                var inherited = inheritedColor.Value;
                return System.Windows.Media.Color.FromArgb(_theme.SegmentColor.A, inherited.R, inherited.G, inherited.B);
            }

            return _theme.SegmentColor;
        }

        private System.Windows.Media.Color GetItemActiveSegmentColor(MenuItemConfig item)
        {
            if (!TryGetFixedColor(item, out var customColor))
            {
                return _theme.SegmentActiveColor;
            }

            return BlendColors(
                System.Windows.Media.Color.FromArgb(_theme.SegmentActiveColor.A, customColor.R, customColor.G, customColor.B),
                Colors.White,
                0.18);
        }

        private System.Windows.Media.Color GetItemActiveSegmentColor(MenuItemConfig item, System.Windows.Media.Color? inheritedColor)
        {
            if (TryGetFixedColor(item, out var customColor))
            {
                return BlendColors(
                    System.Windows.Media.Color.FromArgb(_theme.SegmentActiveColor.A, customColor.R, customColor.G, customColor.B),
                    Colors.White,
                    0.18);
            }

            if (inheritedColor.HasValue)
            {
                var inherited = inheritedColor.Value;
                return BlendColors(
                    System.Windows.Media.Color.FromArgb(_theme.SegmentActiveColor.A, inherited.R, inherited.G, inherited.B),
                    Colors.White,
                    0.18);
            }

            return _theme.SegmentActiveColor;
        }

        private System.Windows.Media.Color GetItemStrokeColor(MenuItemConfig item)
        {
            if (!TryGetFixedColor(item, out var customColor))
            {
                return GetSegmentStrokeColor();
            }

            return BlendColors(
                System.Windows.Media.Color.FromArgb(220, customColor.R, customColor.G, customColor.B),
                Colors.White,
                0.12);
        }

        private System.Windows.Media.Color GetItemStrokeColor(MenuItemConfig item, System.Windows.Media.Color? inheritedColor)
        {
            if (TryGetFixedColor(item, out var customColor))
            {
                return BlendColors(
                    System.Windows.Media.Color.FromArgb(220, customColor.R, customColor.G, customColor.B),
                    Colors.White,
                    0.12);
            }

            if (inheritedColor.HasValue)
            {
                var inherited = inheritedColor.Value;
                return BlendColors(
                    System.Windows.Media.Color.FromArgb(220, inherited.R, inherited.G, inherited.B),
                    Colors.White,
                    0.12);
            }

            return GetSegmentStrokeColor();
        }

        private static bool TryGetFixedColor(MenuItemConfig item, out System.Windows.Media.Color color)
        {
            color = default;
            if (item == null || string.IsNullOrWhiteSpace(item.FixedColor))
            {
                return false;
            }

            try
            {
                var converted = System.Windows.Media.ColorConverter.ConvertFromString(item.FixedColor);
                if (converted is System.Windows.Media.Color parsed)
                {
                    color = parsed;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static System.Windows.Media.Color ParseConfiguredColor(string? value, System.Windows.Media.Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            try
            {
                var converted = System.Windows.Media.ColorConverter.ConvertFromString(value);
                if (converted is System.Windows.Media.Color parsed)
                {
                    return System.Windows.Media.Color.FromRgb(parsed.R, parsed.G, parsed.B);
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static T FreezeIfPossible<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze)
            {
                freezable.Freeze();
            }

            return freezable;
        }

        private static int ToColorCacheKey(System.Windows.Media.Color color)
        {
            return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        private static int ToGradientPointCacheKey(double value)
        {
            return (int)Math.Round(value * 1000.0);
        }

        private static int ToGradientOffsetCacheKey(double value)
        {
            return (int)Math.Round(value * 1000.0);
        }

        private static SolidColorBrush GetCachedBrush(System.Windows.Media.Color color)
        {
            var key = ToColorCacheKey(color);
            if (FrozenBrushCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var brush = FreezeIfPossible(new SolidColorBrush(color));
            FrozenBrushCache[key] = brush;
            return brush;
        }

        private static LinearGradientBrush GetCachedLinearGradientBrush(
            System.Windows.Media.Color startColor,
            System.Windows.Media.Color endColor,
            WpfPoint startPoint,
            WpfPoint endPoint)
        {
            var key = (
                StartColor: ToColorCacheKey(startColor),
                EndColor: ToColorCacheKey(endColor),
                StartX: ToGradientPointCacheKey(startPoint.X),
                StartY: ToGradientPointCacheKey(startPoint.Y),
                EndX: ToGradientPointCacheKey(endPoint.X),
                EndY: ToGradientPointCacheKey(endPoint.Y));

            if (FrozenTwoStopGradientBrushCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var brush = FreezeIfPossible(new LinearGradientBrush(startColor, endColor, startPoint, endPoint));
            FrozenTwoStopGradientBrushCache[key] = brush;
            return brush;
        }

        private static LinearGradientBrush GetCachedLinearGradientBrush(
            WpfPoint startPoint,
            WpfPoint endPoint,
            (System.Windows.Media.Color Color, double Offset) first,
            (System.Windows.Media.Color Color, double Offset) second,
            (System.Windows.Media.Color Color, double Offset) third)
        {
            var key = (
                Color1: ToColorCacheKey(first.Color),
                Offset1: ToGradientOffsetCacheKey(first.Offset),
                Color2: ToColorCacheKey(second.Color),
                Offset2: ToGradientOffsetCacheKey(second.Offset),
                Color3: ToColorCacheKey(third.Color),
                Offset3: ToGradientOffsetCacheKey(third.Offset),
                StartX: ToGradientPointCacheKey(startPoint.X),
                StartY: ToGradientPointCacheKey(startPoint.Y),
                EndX: ToGradientPointCacheKey(endPoint.X),
                EndY: ToGradientPointCacheKey(endPoint.Y));

            if (FrozenThreeStopGradientBrushCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = startPoint,
                EndPoint = endPoint
            };
            brush.GradientStops.Add(new GradientStop(first.Color, first.Offset));
            brush.GradientStops.Add(new GradientStop(second.Color, second.Offset));
            brush.GradientStops.Add(new GradientStop(third.Color, third.Offset));
            brush = FreezeIfPossible(brush);
            FrozenThreeStopGradientBrushCache[key] = brush;
            return brush;
        }

        private static DoubleCollection CreateFrozenDoubleCollection(params double[] values)
        {
            return FreezeIfPossible(new DoubleCollection(values));
        }

        private static Effect? CreateOptimizedBlurEffect(double radius, double blurScale = 0.58, double minRadius = 3.0)
        {
            var effectiveRadius = radius * blurScale;
            if (effectiveRadius < minRadius)
            {
                return null;
            }

            var blurKey = (int)Math.Round(effectiveRadius * 100.0);
            if (BlurEffectCache.TryGetValue(blurKey, out var cached))
            {
                return cached;
            }

            var effect = FreezeIfPossible(new BlurEffect
            {
                Radius = effectiveRadius,
                RenderingBias = RenderingBias.Performance
            });
            BlurEffectCache[blurKey] = effect;
            return effect;
        }

        private static Effect? CreateOptimizedShadowEffect(
            double blurRadius,
            double opacity,
            System.Windows.Media.Color color,
            double shadowDepth = 0,
            double blurScale = 0.58,
            double opacityScale = 0.72,
            double minBlur = 3.0,
            double minOpacity = 0.08)
        {
            var effectiveBlur = blurRadius * blurScale;
            var effectiveOpacity = opacity * opacityScale;
            if (effectiveBlur < minBlur || effectiveOpacity < minOpacity)
            {
                return null;
            }

            var cacheKey = (
                Blur: (int)Math.Round(effectiveBlur * 100.0),
                Opacity: (int)Math.Round(effectiveOpacity * 1000.0),
                Color: ToColorCacheKey(color),
                Depth: (int)Math.Round(shadowDepth * 100.0));

            if (ShadowEffectCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var effect = FreezeIfPossible(new DropShadowEffect
            {
                BlurRadius = effectiveBlur,
                ShadowDepth = shadowDepth,
                Opacity = effectiveOpacity,
                Color = color,
                RenderingBias = RenderingBias.Performance
            });
            ShadowEffectCache[cacheKey] = effect;
            return effect;
        }

        private static System.Windows.Media.Color BlendColors(System.Windows.Media.Color baseColor, System.Windows.Media.Color blendColor, double amount)
        {
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            byte Mix(byte left, byte right) => (byte)Math.Round((left * (1.0 - amount)) + (right * amount));
            return System.Windows.Media.Color.FromArgb(
                baseColor.A,
                Mix(baseColor.R, blendColor.R),
                Mix(baseColor.G, blendColor.G),
                Mix(baseColor.B, blendColor.B));
        }

        private System.Windows.Media.Color ResolveShutdownCountdownTitleColor()
        {
            var center = _theme.CenterColor;
            var candidates = new[]
            {
                System.Windows.Media.Color.FromRgb(12, 16, 22),
                System.Windows.Media.Color.FromRgb(24, 36, 58),
                System.Windows.Media.Color.FromRgb(236, 247, 255),
                System.Windows.Media.Color.FromRgb(92, 228, 255),
                System.Windows.Media.Color.FromRgb(80, 246, 196)
            };

            var best = candidates[0];
            var bestContrast = 0.0;
            for (var i = 0; i < candidates.Length; i++)
            {
                var contrast = CalculateContrastRatio(center, candidates[i]);
                if (contrast > bestContrast)
                {
                    bestContrast = contrast;
                    best = candidates[i];
                }
            }

            return best;
        }

        private static double CalculateContrastRatio(System.Windows.Media.Color background, System.Windows.Media.Color foreground)
        {
            var bgL = RelativeLuminance(background);
            var fgL = RelativeLuminance(foreground);
            var max = Math.Max(bgL, fgL);
            var min = Math.Min(bgL, fgL);
            return (max + 0.05) / (min + 0.05);
        }

        private static double RelativeLuminance(System.Windows.Media.Color color)
        {
            static double ToLinear(byte channel)
            {
                var normalized = channel / 255.0;
                return normalized <= 0.03928
                    ? normalized / 12.92
                    : Math.Pow((normalized + 0.055) / 1.055, 2.4);
            }

            var r = ToLinear(color.R);
            var g = ToLinear(color.G);
            var b = ToLinear(color.B);
            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }

        private WpfPoint GetElementCenter(FrameworkElement element)
        {
            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            return new WpfPoint(left + (element.Width / 2.0), top + (element.Height / 2.0));
        }

        private static System.Windows.Media.FontFamily ResolveCategoryStripFontFamily()
        {
            return CategoryStripFontService.CreateFontFamily("Segoe");
        }

        private static System.Windows.Media.FontFamily ResolveCategoryStripFontFamily(string fontKey)
        {
            return CategoryStripFontService.CreateFontFamily(fontKey);
        }

        private static bool ContainsTurkishCharacter(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var c in text)
            {
                if (c == 'ç' || c == 'Ç' ||
                    c == 'ğ' || c == 'Ğ' ||
                    c == 'ı' || c == 'İ' ||
                    c == 'ö' || c == 'Ö' ||
                    c == 'ş' || c == 'Ş' ||
                    c == 'ü' || c == 'Ü')
                {
                    return true;
                }
            }

            return false;
        }

        private static System.Windows.Media.FontFamily ResolveReadableCategoryStripFont(string configuredFontKey, string label)
        {
            if (ContainsTurkishCharacter(label))
            {
                return CategoryStripFontService.CreateFontFamily("Segoe");
            }

            return ResolveCategoryStripFontFamily(configuredFontKey);
        }

        private void ShowCategoryHoverBadge(
            string title,
            string subtitle,
            string breadcrumb,
            string? symbolKey,
            System.Windows.Media.Color accentColor,
            bool showSymbol)
        {
            if (string.IsNullOrWhiteSpace(title) &&
                string.IsNullOrWhiteSpace(subtitle) &&
                string.IsNullOrWhiteSpace(breadcrumb))
            {
                HideCategoryNameStrip();
                return;
            }

            HideCategoryNameStrip();

            var profile = GetLayoutProfile();
            var diameter = Math.Clamp(profile.CenterSize * 0.96, 104, 184);
            var baseColor = BlendColors(accentColor, _theme.CenterColor, 0.26);
            var topColor = BlendColors(baseColor, Colors.White, 0.14);
            var bottomColor = BlendColors(baseColor, Colors.Black, 0.18);
            var textColor = GetContrastingTextColor(baseColor);
            var textShadowColor = GetContrastingTextColor(textColor);
            var titleBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(245, textColor.R, textColor.G, textColor.B));
            var subtitleBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(218, textColor.R, textColor.G, textColor.B));
            var breadcrumbBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(195, textColor.R, textColor.G, textColor.B));
            var textShadow = CreateOptimizedShadowEffect(4.5, 0.26, textShadowColor);

            var badge = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = new CornerRadius(diameter / 2.0),
                BorderThickness = new Thickness(1.4),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(176, textColor.R, textColor.G, textColor.B)),
                Background = GetCachedLinearGradientBrush(
                    System.Windows.Media.Color.FromArgb(244, topColor.R, topColor.G, topColor.B),
                    System.Windows.Media.Color.FromArgb(248, bottomColor.R, bottomColor.G, bottomColor.B),
                    new WpfPoint(0.18, 0.06),
                    new WpfPoint(0.82, 0.94)),
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(18, 0.26, accentColor),
                Opacity = Math.Max(0.84, _categoryStripOpacity)
            };

            var content = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(Math.Max(10, diameter * 0.1))
            };

            if (showSymbol)
            {
                var symbolSize = Math.Clamp(diameter * 0.34, 28, 58);
                var symbolVisual = CategorySymbolService.CreateSymbolVisual(symbolKey, symbolSize, titleBrush);
                symbolVisual.Margin = new Thickness(0, 0, 0, 6);
                content.Children.Add(symbolVisual);
            }
            else if (!string.IsNullOrWhiteSpace(breadcrumb))
            {
                content.Children.Add(new TextBlock
                {
                    Text = breadcrumb,
                    Foreground = breadcrumbBrush,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                    FontSize = Math.Clamp(diameter * 0.085, 10, 13),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = diameter * 0.72,
                    Margin = new Thickness(0, 0, 0, 3),
                    Effect = textShadow
                });
            }

            content.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = titleBrush,
                FontFamily = ResolveReadableCategoryStripFont(_categoryStripFont, title),
                FontSize = Math.Clamp(diameter * (showSymbol ? 0.112 : 0.13), 12, 20),
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = diameter * 0.74,
                Effect = textShadow
            });

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                content.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = subtitleBrush,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = Math.Clamp(diameter * 0.083, 10, 13.5),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = diameter * 0.72,
                    Margin = new Thickness(0, 4, 0, 0),
                    Effect = textShadow
                });
            }

            badge.Child = content;
            _categoryNameStrip = badge;
            _categoryBridgeDot = null;

            Canvas.SetLeft(_categoryNameStrip, _centerX - (diameter / 2.0));
            Canvas.SetTop(_categoryNameStrip, _centerY - (diameter / 2.0));
            Panel.SetZIndex(_categoryNameStrip, 300);
            RootCanvas.Children.Add(_categoryNameStrip);
        }

        private static System.Windows.Media.Color GetContrastingTextColor(System.Windows.Media.Color backgroundColor)
        {
            return GetRelativeLuminance(backgroundColor) >= 0.52
                ? System.Windows.Media.Color.FromRgb(14, 18, 24)
                : System.Windows.Media.Color.FromRgb(244, 248, 252);
        }

        private static double GetRelativeLuminance(System.Windows.Media.Color color)
        {
            static double ToLinear(byte channel)
            {
                var value = channel / 255.0;
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            var r = ToLinear(color.R);
            var g = ToLinear(color.G);
            var b = ToLinear(color.B);
            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }

        private void HideCategoryNameStrip()
        {
            if (_categoryNameStrip != null)
            {
                RootCanvas.Children.Remove(_categoryNameStrip);
                _categoryNameStrip = null;
            }

            if (_categoryBridgeDot != null)
            {
                RootCanvas.Children.Remove(_categoryBridgeDot);
                _categoryBridgeDot = null;
            }
        }

        private Ellipse CreateMenuBackdropBlurHalo(double outerRadius)
        {
            var sizeScale = Math.Max(0.6, Math.Min(2.5, _menuBackdropBlurSizeScale));
            var strengthScale = Math.Max(0.4, Math.Min(2.5, _menuBackdropBlurStrengthScale));
            var blurDiameter = Math.Max(120.0, outerRadius * 4.0 * sizeScale);
            var coreColor = BlendColors(_theme.ShadowColor, _theme.CenterColor, 0.28);
            var edgeColor = BlendColors(coreColor, Colors.Black, 0.24);
            byte GetAlpha(double alphaBase) => (byte)Math.Max(0, Math.Min(255, Math.Round(alphaBase * strengthScale)));

            var fill = new RadialGradientBrush
            {
                GradientOrigin = new WpfPoint(0.5, 0.5),
                Center = new WpfPoint(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            fill.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(GetAlpha(98), coreColor.R, coreColor.G, coreColor.B), 0.0));
            fill.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(GetAlpha(58), coreColor.R, coreColor.G, coreColor.B), 0.48));
            fill.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(GetAlpha(22), edgeColor.R, edgeColor.G, edgeColor.B), 0.80));
            fill.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, edgeColor.R, edgeColor.G, edgeColor.B), 1.0));
            if (fill.CanFreeze)
            {
                fill.Freeze();
            }

            var blurRadius = Math.Max(18.0, outerRadius * 0.30 * strengthScale);

            return new Ellipse
            {
                Width = blurDiameter,
                Height = blurDiameter,
                Fill = fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                CacheMode = new BitmapCache(),
                Effect = CreateOptimizedBlurEffect(blurRadius, blurScale: 0.90, minRadius: 10.0)
            };
        }

        private Ellipse CreateCenterAccentRing(double centerSize)
        {
            var ringMargin = Math.Max(11, centerSize * 0.07);
            var diameter = centerSize + (ringMargin * 2);
            var accent = BlendColors(_theme.CenterBorderColor, _theme.SegmentActiveColor, 0.62);
            var softAccent = BlendColors(_theme.CenterColor, _theme.TitleColor, 0.42);
            var coolAccent = BlendColors(_theme.SubtitleColor, Colors.White, 0.25);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0, 0.5),
                EndPoint = new WpfPoint(1, 0.5),
                RelativeTransform = new RotateTransform(0, 0.5, 0.5)
            };
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(26, accent.R, accent.G, accent.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(232, accent.R, accent.G, accent.B), 0.22));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(208, softAccent.R, softAccent.G, softAccent.B), 0.50));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(224, coolAccent.R, coolAccent.G, coolAccent.B), 0.76));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(22, accent.R, accent.G, accent.B), 1.0));

            var ring = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Stroke = gradient,
                StrokeThickness = Math.Max(4.4, centerSize * 0.032) * _innerGradientRingThicknessScale,
                Opacity = 0.92,
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(10, 0.18, accent)
            };

            if (gradient.RelativeTransform is RotateTransform rotateTransform)
            {
                if (_enableGradientRingAnimations)
                {
                    var rotateAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 360,
                        Duration = TimeSpan.FromSeconds(10),
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = null
                    };
                    ApplyDesiredFrameRate(rotateAnimation, RingAmbientAnimationFps);
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
                }
            }

            if (_enableGradientRingAnimations)
            {
                StartCenterAccentRingAmbientOpacityAnimation(ring);
            }
            else
            {
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = 0.92;
            }

            return ring;
        }

        private static void StartCenterAccentRingAmbientOpacityAnimation(Ellipse ring)
        {
            var animation = new DoubleAnimation
            {
                From = 0.62,
                To = 0.96,
                Duration = TimeSpan.FromMilliseconds(2200),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ApplyDesiredFrameRate(animation, RingAmbientAnimationFps);
            ring.BeginAnimation(OpacityProperty, animation);
        }

        private Ellipse CreateOuterAccentRing(double outerRadius)
        {
            var strokeThickness = Math.Max(11.2, outerRadius * 0.056) * _outerGradientRingThicknessScale;
            var diameter = (outerRadius * 2) + (strokeThickness * 2);
            var accent = BlendColors(_theme.SegmentActiveColor, _theme.TitleColor, 0.48);
            var softAccent = BlendColors(_theme.CenterBorderColor, _theme.SubtitleColor, 0.34);
            var glowAccent = BlendColors(_theme.SegmentColor, Colors.White, 0.18);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0, 0.5),
                EndPoint = new WpfPoint(1, 0.5),
                RelativeTransform = new RotateTransform(0, 0.5, 0.5)
            };
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(16, accent.R, accent.G, accent.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(178, accent.R, accent.G, accent.B), 0.18));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(224, softAccent.R, softAccent.G, softAccent.B), 0.46));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(196, glowAccent.R, glowAccent.G, glowAccent.B), 0.76));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(14, accent.R, accent.G, accent.B), 1.0));

            var ring = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Stroke = gradient,
                StrokeThickness = strokeThickness,
                Opacity = 0.94,
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(12, 0.14, accent)
            };

            if (gradient.RelativeTransform is RotateTransform rotateTransform)
            {
                if (_enableGradientRingAnimations)
                {
                    var rotateAnimation = new DoubleAnimation
                    {
                        From = 360,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(16),
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = null
                    };
                    ApplyDesiredFrameRate(rotateAnimation, RingAmbientAnimationFps);
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
                }
            }

            if (_enableGradientRingAnimations)
            {
                StartOuterAccentRingAmbientOpacityAnimation(ring);
            }
            else
            {
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = 0.94;
            }

            return ring;
        }

        private static void StartOuterAccentRingAmbientOpacityAnimation(Ellipse ring)
        {
            var animation = new DoubleAnimation
            {
                From = 0.72,
                To = 0.98,
                Duration = TimeSpan.FromMilliseconds(2800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ApplyDesiredFrameRate(animation, RingAmbientAnimationFps);
            ring.BeginAnimation(OpacityProperty, animation);
        }

        private Grid CreateUtilityDockButton(LayoutProfile profile)
        {
            var coreDiameter = Math.Clamp(profile.CenterSize * 0.14, 22.0, 30.0);
            var ringPadding = Math.Max(4.0, coreDiameter * 0.14);
            var ringStroke = Math.Max(1.6, coreDiameter * 0.075);
            var hostSize = coreDiameter + (ringPadding * 2.0);
            var ringColor = BlendColors(_theme.CenterBorderColor, _theme.SegmentActiveColor, 0.58);
            var ringGlowColor = BlendColors(ringColor, Colors.White, 0.24);
            var coreColor = BlendColors(_theme.CenterColor, Colors.White, 0.10);
            var coreHoverColor = BlendColors(coreColor, Colors.White, 0.16);
            var coreBorderColor = BlendColors(_theme.CenterBorderColor, Colors.White, 0.18);
            var coreBorderHoverColor = BlendColors(_theme.CenterBorderColor, Colors.White, 0.34);
            var normalRingBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(214, ringColor.R, ringColor.G, ringColor.B));
            var hoverRingBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(244, ringGlowColor.R, ringGlowColor.G, ringGlowColor.B));
            var normalCoreBrush = GetCachedBrush(coreColor);
            var hoverCoreBrush = GetCachedBrush(coreHoverColor);
            var normalCoreBorderBrush = GetCachedBrush(coreBorderColor);
            var hoverCoreBorderBrush = GetCachedBrush(coreBorderHoverColor);

            var host = new Grid
            {
                Width = hostSize,
                Height = hostSize,
                Cursor = Cursors.Hand,
                Background = BackdropHitTestBrush,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                RenderTransformOrigin = new WpfPoint(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.0, 1.0)
            };

            var ring = new Ellipse
            {
                Width = hostSize,
                Height = hostSize,
                Stroke = normalRingBrush,
                StrokeThickness = ringStroke,
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(10, ringColor.R, ringColor.G, ringColor.B)),
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(8, 0.16, ringColor)
            };

            var core = new Border
            {
                Width = coreDiameter,
                Height = coreDiameter,
                CornerRadius = new CornerRadius(coreDiameter / 2.0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = normalCoreBrush,
                BorderBrush = normalCoreBorderBrush,
                BorderThickness = new Thickness(Math.Max(1.2, profile.CenterBorder)),
                Effect = CreateOptimizedShadowEffect(Math.Max(8.0, profile.CenterBlur * 0.36), 0.18, _theme.ShadowColor),
                IsHitTestVisible = false
            };
            _utilityDockButtonCore = core;

            core.Child = new TextBlock
            {
                Text = "\uE713",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Clamp(coreDiameter * 0.40, 14.0, 18.0),
                Foreground = GetCachedBrush(BlendColors(_theme.TitleColor, Colors.White, 0.18)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };

            host.Children.Add(ring);
            host.Children.Add(core);

            host.MouseEnter += (_, __) =>
            {
                ring.Stroke = hoverRingBrush;
                core.Background = hoverCoreBrush;
                core.BorderBrush = hoverCoreBorderBrush;
                if (host.RenderTransform is ScaleTransform scale)
                {
                    scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                    {
                        To = 1.05,
                        Duration = TimeSpan.FromMilliseconds(140),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                    {
                        To = 1.05,
                        Duration = TimeSpan.FromMilliseconds(140),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
            };

            host.MouseLeave += (_, __) =>
            {
                ring.Stroke = normalRingBrush;
                core.Background = normalCoreBrush;
                core.BorderBrush = normalCoreBorderBrush;
                if (host.RenderTransform is ScaleTransform scale)
                {
                    scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(180),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(180),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                }
            };

            host.MouseLeftButtonUp += OnUtilityDockButtonMouseLeftButtonUp;

            return host;
        }

        private static void ApplyDesiredFrameRate(Timeline timeline, int fps)
        {
            if (timeline == null || fps <= 0)
            {
                return;
            }

            Timeline.SetDesiredFrameRate(timeline, fps);
        }

        private static Geometry CreateRingSegmentGeometry(
            double centerX,
            double centerY,
            double innerRadius,
            double outerRadius,
            double startAngle,
            double endAngle)
        {
            var startOuter = PointOnCircle(centerX, centerY, outerRadius, startAngle);
            var endOuter = PointOnCircle(centerX, centerY, outerRadius, endAngle);
            var startInner = PointOnCircle(centerX, centerY, innerRadius, endAngle);
            var endInner = PointOnCircle(centerX, centerY, innerRadius, startAngle);

            var largeArc = Math.Abs(endAngle - startAngle) > 180;

            var figure = new PathFigure { StartPoint = startOuter, IsClosed = true };
            figure.Segments.Add(new ArcSegment(endOuter, new System.Windows.Size(outerRadius, outerRadius), 0, largeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(startInner, true));
            figure.Segments.Add(new ArcSegment(endInner, new System.Windows.Size(innerRadius, innerRadius), 0, largeArc, SweepDirection.Counterclockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return FreezeIfPossible(geometry);
        }

        private bool IsNeonOrbitStyle()
        {
            return string.Equals(_menuStyle, "Style7", StringComparison.OrdinalIgnoreCase);
        }

        private static Geometry CreateOrbitNodeGeometry(double centerX, double centerY, double orbitRadius, double angle, double nodeRadius)
        {
            var point = PointOnCircle(centerX, centerY, orbitRadius, angle);
            return FreezeIfPossible(new EllipseGeometry(point, nodeRadius, nodeRadius));
        }

        private static System.Windows.Media.Color GetNeonOrbitColor(int itemIndex)
        {
            return NeonOrbitPalette[Math.Abs(itemIndex) % NeonOrbitPalette.Length];
        }

        private static WpfPoint PointOnCircle(double centerX, double centerY, double radius, double angleDegrees)
        {
            var angle = ToRadians(angleDegrees);
            return new WpfPoint(
                centerX + Math.Cos(angle) * radius,
                centerY + Math.Sin(angle) * radius);
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private void PlayOpenAnimation()
        {
            if (_openAnimationStyle == "MeteorDrop")
            {
                PlayMeteorDropOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "ArcCascade")
            {
                PlayArcCascadeOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "NovaBloom")
            {
                PlayNovaBloomOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "VelvetCurtain")
            {
                PlayVelvetCurtainOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "CenterUnfold")
            {
                PlayCenterUnfoldOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "OdakKaskadi")
            {
                PlayOdakKaskadiOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "SoftFade")
            {
                PlaySoftFadeOpenAnimation();
                return;
            }

            var fadeEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var settleEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 };

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(176),
                EasingFunction = fadeEase
            };
            BeginAnimation(OpacityProperty, opacityAnimation);

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.978, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.008, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)))
            {
                EasingFunction = settleEase
            });
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)))
            {
                EasingFunction = settleEase
            });
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);

            var translateAnimation = new DoubleAnimationUsingKeyFrames();
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(14, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)))
            {
                EasingFunction = settleEase
            });
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)))
            {
                EasingFunction = settleEase
            });
            RootTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnimation);

            if (_centerPanel != null)
            {
                _centerPanel.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                var centerTransforms = new TransformGroup();
                var centerScale = new ScaleTransform(0.94, 0.94);
                var centerTranslate = new TranslateTransform(0, 10);
                centerTransforms.Children.Add(centerScale);
                centerTransforms.Children.Add(centerTranslate);
                _centerPanel.RenderTransform = centerTransforms;

                var centerScaleAnim = new DoubleAnimationUsingKeyFrames();
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.015, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(188)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(286)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, centerScaleAnim);
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, centerScaleAnim);

                var centerTranslateAnim = new DoubleAnimationUsingKeyFrames();
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(12, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(-1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(188)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(286)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, centerTranslateAnim);
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.95, 1.015, 188, 286);
        }

        private void PlayVelvetCurtainOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.92, 1.0, 170, 220);

            var interactions = GetMainRingInteractions();

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var begin = TimeSpan.FromMilliseconds(95 + (i * 34));
                AnimateSegmentScaleEntrance(interaction.Segment, 0.18, 1.0, begin, 280, new CubicEase { EasingMode = EasingMode.EaseOut });
                AnimateIconScaleEntrance(interaction.IconHost, 0.7, 1.0, begin + TimeSpan.FromMilliseconds(78), 180, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            }
        }

        private void PlayNovaBloomOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.88, 1.03, 160, 240);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var inwardDistance = Math.Max(28, (profile.InnerRadius - (profile.CenterSize / 2.0)) + 20);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var radians = ToRadians((interaction.StartAngle + interaction.EndAngle) / 2.0);
                var offsetX = -Math.Cos(radians) * inwardDistance;
                var offsetY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(120 + (i * 14));

                AnimateSegmentEntrance(interaction.Segment, offsetX, offsetY, begin, 300, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });
                AnimateIconEntrance(interaction.IconHost, offsetX * 1.18, offsetY * 1.18, begin + TimeSpan.FromMilliseconds(28), 260, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 });
            }
        }

        private void PlayArcCascadeOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.97, 1.0, 120, 170);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var sweepOffset = Math.Max(26, profile.OuterRadius * 0.11);
            var inwardDistance = Math.Max(18, (profile.InnerRadius - (profile.CenterSize / 2.0)) * 0.48);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var radians = ToRadians((interaction.StartAngle + interaction.EndAngle) / 2.0);
                var tangentX = -Math.Sin(radians) * sweepOffset;
                var tangentY = Math.Cos(radians) * sweepOffset;
                var radialX = -Math.Cos(radians) * inwardDistance;
                var radialY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(105 + (i * 44));

                AnimateSegmentEntrance(interaction.Segment, tangentX + radialX, tangentY + radialY, begin, 240, new CubicEase { EasingMode = EasingMode.EaseOut });
                AnimateIconEntrance(interaction.IconHost, tangentX * 0.9 + radialX, tangentY * 0.9 + radialY, begin + TimeSpan.FromMilliseconds(42), 210, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            }
        }

        private void PlayMeteorDropOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.9, 1.02, 140, 220);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var dropDistance = Math.Max(80, profile.OuterRadius * 0.42);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var begin = TimeSpan.FromMilliseconds(85 + (i * 26));
                var sideDrift = ((i % 2 == 0) ? -1 : 1) * Math.Max(10, profile.OuterRadius * 0.04);

                AnimateSegmentEntrance(interaction.Segment, sideDrift, -dropDistance, begin, 300, new BounceEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Bounces = 2,
                    Bounciness = 1.7
                });

                AnimateIconEntrance(interaction.IconHost, sideDrift * 1.4, -(dropDistance + 24), begin + TimeSpan.FromMilliseconds(55), 260, new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.28
                });
            }
        }

        private void PlayCenterUnfoldOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;

            AnimateCenterPanelEntrance(0.0, 1.0, 0.94, 1.0, 150, 180);

            var settleEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var profile = GetLayoutProfile();
            var inwardDistance = Math.Max(26, (profile.InnerRadius - (profile.CenterSize / 2.0)) + 12);
            var interactions = GetMainRingInteractions();

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var middleAngle = (interaction.StartAngle + interaction.EndAngle) / 2.0;
                var radians = ToRadians(middleAngle);
                var offsetX = -Math.Cos(radians) * inwardDistance;
                var offsetY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(110 + (i * 28));

                AnimateSegmentEntrance(interaction.Segment, offsetX, offsetY, begin, 220, settleEase);
                AnimateIconEntrance(interaction.IconHost, offsetX * 1.08, offsetY * 1.08, begin + TimeSpan.FromMilliseconds(35), 200, settleEase);
            }
        }

        private void PlayOdakKaskadiOpenAnimation()
        {
            EnsureOdakKaskadiAnimationSettled();
            _odakKaskadiAnimatedSegments.Clear();
            _odakKaskadiIconFinalPositions.Clear();
            _isOdakKaskadiAnimationActive = true;

            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;

            var centerEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (_centerPanel != null)
            {
                _centerPanel.BeginAnimation(OpacityProperty, null);
                _centerPanel.Opacity = 0.0;
                _centerPanel.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                _centerPanel.RenderTransform = new ScaleTransform(0.0, 0.0);

                _centerPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(1000),
                    EasingFunction = centerEase
                });

                if (_centerPanel.RenderTransform is ScaleTransform centerScale)
                {
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        EasingFunction = centerEase
                    });
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        EasingFunction = centerEase
                    });
                }
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.0, 1.0, 1000, 1000);

            var profile = GetLayoutProfile();
            if (_enableGradientRingAnimations)
            {
                PlayOdakKaskadiCenterAccentRingSequence();
                PlayOdakKaskadiOuterAccentRingSequence(profile);
            }

            var interactions = GetMainRingInteractions();
            if (interactions.Count == 0)
            {
                _isOdakKaskadiAnimationActive = false;
                return;
            }

            var centerRadius = profile.CenterSize * 0.5;
            var inwardDistance = Math.Max(24.0, (profile.InnerRadius - centerRadius) + 14.0);
            const double cascadeStartMs = 0.0;
            const double cascadeWindowMs = 1000.0;
            var segmentDurationMs = interactions.Count <= 1 ? cascadeWindowMs : 360.0;
            var stepMs = interactions.Count <= 1
                ? 0.0
                : Math.Max(0.0, (cascadeWindowMs - segmentDurationMs) / (interactions.Count - 1));
            var segmentEase = new CubicEase { EasingMode = EasingMode.EaseOut };

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var angle = ToRadians((interaction.StartAngle + interaction.EndAngle) * 0.5);
                var offsetX = -Math.Cos(angle) * inwardDistance;
                var offsetY = -Math.Sin(angle) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(cascadeStartMs + (stepMs * i));

                _odakKaskadiAnimatedSegments.Add(interaction.Segment);
                _odakKaskadiIconFinalPositions[interaction.IconHost] = new WpfPoint(
                    Canvas.GetLeft(interaction.IconHost),
                    Canvas.GetTop(interaction.IconHost));

                AnimateSegmentEntrance(
                    interaction.Segment,
                    offsetX,
                    offsetY,
                    begin,
                    (int)Math.Round(segmentDurationMs),
                    segmentEase);
                AnimateIconEntrance(
                    interaction.IconHost,
                    offsetX * 1.06,
                    offsetY * 1.06,
                    begin,
                    (int)Math.Round(segmentDurationMs),
                    segmentEase);
            }

            var ringTailMs = _enableGradientRingAnimations
                ? OdakKaskadiCenterAccentReturnMs + OdakKaskadiFinalizeBufferMs
                : 0.0;
            var finalizeMs = Math.Max(cascadeStartMs + cascadeWindowMs + 60.0, ringTailMs);
            _openAnimationFinalizeTimer.Stop();
            _openAnimationFinalizeTimer.Interval = TimeSpan.FromMilliseconds(finalizeMs);
            _openAnimationFinalizeTimer.Start();
        }

        private void PlayOdakKaskadiCenterAccentRingSequence()
        {
            if (_centerAccentRing == null)
            {
                return;
            }

            var ring = _centerAccentRing;
            var gradientStroke = _centerAccentRingBaseStroke ?? ring.Stroke;
            _centerAccentRingBaseStroke ??= gradientStroke;
            if (_centerAccentRingBaseOpacity <= 0.0)
            {
                _centerAccentRingBaseOpacity = ring.Opacity > 0 ? ring.Opacity : 0.92;
            }

            if (_centerAccentRingBaseStrokeThickness <= 0.0)
            {
                _centerAccentRingBaseStrokeThickness = ring.StrokeThickness > 0.0 ? ring.StrokeThickness : Math.Max(2.2, ring.Width * 0.018);
            }

            var baseOpacity = _centerAccentRingBaseOpacity;
            var baseStrokeThickness = _centerAccentRingBaseStrokeThickness;
            var peakStrokeThickness = baseStrokeThickness * 4.0;
            var thicknessHoldMs = OdakKaskadiCenterAccentHoldMs;
            var peakReachMs = OdakKaskadiCenterAccentPeakMs;
            var returnToBaseMs = OdakKaskadiCenterAccentReturnMs;
            var solidOpacity = Math.Min(1.0, baseOpacity + 0.08);
            var solidColor = BlendColors(_theme.CenterBorderColor, _theme.SegmentActiveColor, 0.74);
            var solidStroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(236, solidColor.R, solidColor.G, solidColor.B));

            ClearCenterAccentRingSolidOverlay();
            ring.BeginAnimation(Shape.StrokeProperty, null);
            ring.BeginAnimation(OpacityProperty, null);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
            ring.Stroke = gradientStroke;
            ring.StrokeThickness = baseStrokeThickness;
            ring.Opacity = 0.0;

            var opacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(opacitySequence, RingSequenceAnimationFps);
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = null
            });

            var thicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(thicknessSequence, RingSequenceAnimationFps);
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            opacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_centerAccentRing, ring))
                {
                    return;
                }

                ClearCenterAccentRingSolidOverlay();
                ring.BeginAnimation(Shape.StrokeProperty, null);
                ring.Stroke = _centerAccentRingBaseStroke ?? gradientStroke;
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = _centerAccentRingBaseOpacity;
                ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
                ring.StrokeThickness = _centerAccentRingBaseStrokeThickness > 0.0 ? _centerAccentRingBaseStrokeThickness : baseStrokeThickness;
                StartCenterAccentRingAmbientOpacityAnimation(ring);
            };

            ring.BeginAnimation(OpacityProperty, opacitySequence);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, thicknessSequence);

            var solidOverlay = new Ellipse
            {
                Width = ring.Width,
                Height = ring.Height,
                Stroke = solidStroke,
                StrokeThickness = baseStrokeThickness,
                Opacity = 0.0,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            Canvas.SetLeft(solidOverlay, Canvas.GetLeft(ring));
            Canvas.SetTop(solidOverlay, Canvas.GetTop(ring));
            var ringIndex = RootCanvas.Children.IndexOf(ring);
            if (ringIndex >= 0)
            {
                RootCanvas.Children.Insert(ringIndex + 1, solidOverlay);
            }
            else
            {
                RootCanvas.Children.Add(solidOverlay);
            }

            _centerAccentRingSolidOverlay = solidOverlay;

            var solidOpacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidOpacitySequence, RingSequenceAnimationFps);
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs))));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            solidOpacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_centerAccentRingSolidOverlay, solidOverlay))
                {
                    return;
                }

                ClearCenterAccentRingSolidOverlay();
            };

            var solidThicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidThicknessSequence, RingSequenceAnimationFps);
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            solidOverlay.BeginAnimation(OpacityProperty, solidOpacitySequence);
            solidOverlay.BeginAnimation(Shape.StrokeThicknessProperty, solidThicknessSequence);
        }

        private void PlayOdakKaskadiOuterAccentRingSequence(LayoutProfile profile)
        {
            if (_outerAccentRing == null)
            {
                return;
            }

            var ring = _outerAccentRing;
            var gradientStroke = _outerAccentRingBaseStroke ?? ring.Stroke;
            _outerAccentRingBaseStroke ??= gradientStroke;
            if (_outerAccentRingBaseOpacity <= 0.0)
            {
                _outerAccentRingBaseOpacity = ring.Opacity > 0 ? ring.Opacity : 0.94;
            }

            if (_outerAccentRingBaseStrokeThickness <= 0.0)
            {
                _outerAccentRingBaseStrokeThickness = ring.StrokeThickness > 0.0 ? ring.StrokeThickness : Math.Max(3.2, ring.Width * 0.018);
            }

            var baseOpacity = _outerAccentRingBaseOpacity;
            var baseStrokeThickness = _outerAccentRingBaseStrokeThickness;
            var segmentBandThickness = Math.Max(0.0, profile.OuterRadius - profile.InnerRadius);
            var peakStrokeThickness = baseStrokeThickness + segmentBandThickness;
            var thicknessHoldMs = OdakKaskadiCenterAccentHoldMs;
            var peakReachMs = OdakKaskadiCenterAccentPeakMs;
            var returnToBaseMs = OdakKaskadiCenterAccentReturnMs;
            var solidOpacity = Math.Min(1.0, baseOpacity + 0.06);
            var solidColor = BlendColors(_theme.SegmentActiveColor, _theme.TitleColor, 0.58);
            var solidStroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(228, solidColor.R, solidColor.G, solidColor.B));

            ClearOuterAccentRingSolidOverlay();
            ring.BeginAnimation(Shape.StrokeProperty, null);
            ring.BeginAnimation(OpacityProperty, null);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
            ring.Stroke = gradientStroke;
            ring.StrokeThickness = baseStrokeThickness;
            ring.Opacity = 0.0;

            var opacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(opacitySequence, RingSequenceAnimationFps);
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = null
            });

            var thicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(thicknessSequence, RingSequenceAnimationFps);
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            opacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_outerAccentRing, ring))
                {
                    return;
                }

                ClearOuterAccentRingSolidOverlay();
                ring.BeginAnimation(Shape.StrokeProperty, null);
                ring.Stroke = _outerAccentRingBaseStroke ?? gradientStroke;
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = _outerAccentRingBaseOpacity;
                ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
                ring.StrokeThickness = _outerAccentRingBaseStrokeThickness > 0.0 ? _outerAccentRingBaseStrokeThickness : baseStrokeThickness;
                StartOuterAccentRingAmbientOpacityAnimation(ring);
            };

            ring.BeginAnimation(OpacityProperty, opacitySequence);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, thicknessSequence);

            var solidOverlay = new Ellipse
            {
                Width = ring.Width,
                Height = ring.Height,
                Stroke = solidStroke,
                StrokeThickness = baseStrokeThickness,
                Opacity = 0.0,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            Canvas.SetLeft(solidOverlay, Canvas.GetLeft(ring));
            Canvas.SetTop(solidOverlay, Canvas.GetTop(ring));
            var ringIndex = RootCanvas.Children.IndexOf(ring);
            if (ringIndex >= 0)
            {
                RootCanvas.Children.Insert(ringIndex + 1, solidOverlay);
            }
            else
            {
                RootCanvas.Children.Add(solidOverlay);
            }

            _outerAccentRingSolidOverlay = solidOverlay;

            var solidOpacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidOpacitySequence, RingSequenceAnimationFps);
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs))));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            solidOpacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_outerAccentRingSolidOverlay, solidOverlay))
                {
                    return;
                }

                ClearOuterAccentRingSolidOverlay();
            };

            var solidThicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidThicknessSequence, RingSequenceAnimationFps);
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            solidOverlay.BeginAnimation(OpacityProperty, solidOpacitySequence);
            solidOverlay.BeginAnimation(Shape.StrokeThicknessProperty, solidThicknessSequence);
        }

        private void OnOpenAnimationFinalizeTimerTick(object? sender, EventArgs e)
        {
            _openAnimationFinalizeTimer.Stop();
            EnsureOdakKaskadiAnimationSettled();
        }

        private void EnsureOdakKaskadiAnimationSettled()
        {
            if (!_isOdakKaskadiAnimationActive &&
                _odakKaskadiAnimatedSegments.Count == 0 &&
                _odakKaskadiIconFinalPositions.Count == 0 &&
                _centerAccentRing == null &&
                _centerAccentRingSolidOverlay == null &&
                _outerAccentRing == null &&
                _outerAccentRingSolidOverlay == null)
            {
                return;
            }

            _openAnimationFinalizeTimer.Stop();

            foreach (var segment in _odakKaskadiAnimatedSegments)
            {
                segment.BeginAnimation(OpacityProperty, null);
                segment.Opacity = 1.0;

                if (segment.RenderTransform is TranslateTransform translate)
                {
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                }

                segment.RenderTransform = Transform.Identity;
            }

            foreach (var iconPair in _odakKaskadiIconFinalPositions)
            {
                var iconHost = iconPair.Key;
                var finalPosition = iconPair.Value;
                iconHost.BeginAnimation(OpacityProperty, null);
                iconHost.BeginAnimation(Canvas.LeftProperty, null);
                iconHost.BeginAnimation(Canvas.TopProperty, null);
                iconHost.Opacity = 1.0;
                Canvas.SetLeft(iconHost, finalPosition.X);
                Canvas.SetTop(iconHost, finalPosition.Y);
            }

            ClearCenterAccentRingSolidOverlay();
            if (_centerAccentRing != null)
            {
                _centerAccentRing.BeginAnimation(Shape.StrokeProperty, null);
                _centerAccentRing.BeginAnimation(OpacityProperty, null);
                _centerAccentRing.BeginAnimation(Shape.StrokeThicknessProperty, null);
                if (_centerAccentRingBaseStroke != null)
                {
                    _centerAccentRing.Stroke = _centerAccentRingBaseStroke;
                }

                var baseOpacity = _centerAccentRingBaseOpacity > 0 ? _centerAccentRingBaseOpacity : 0.92;
                _centerAccentRing.Opacity = baseOpacity;
                if (_centerAccentRingBaseStrokeThickness > 0.0)
                {
                    _centerAccentRing.StrokeThickness = _centerAccentRingBaseStrokeThickness;
                }
                if (_enableGradientRingAnimations)
                {
                    StartCenterAccentRingAmbientOpacityAnimation(_centerAccentRing);
                }
            }

            ClearOuterAccentRingSolidOverlay();
            if (_outerAccentRing != null)
            {
                _outerAccentRing.BeginAnimation(Shape.StrokeProperty, null);
                _outerAccentRing.BeginAnimation(OpacityProperty, null);
                _outerAccentRing.BeginAnimation(Shape.StrokeThicknessProperty, null);
                if (_outerAccentRingBaseStroke != null)
                {
                    _outerAccentRing.Stroke = _outerAccentRingBaseStroke;
                }

                var baseOpacity = _outerAccentRingBaseOpacity > 0 ? _outerAccentRingBaseOpacity : 0.94;
                _outerAccentRing.Opacity = baseOpacity;
                if (_outerAccentRingBaseStrokeThickness > 0.0)
                {
                    _outerAccentRing.StrokeThickness = _outerAccentRingBaseStrokeThickness;
                }
                if (_enableGradientRingAnimations)
                {
                    StartOuterAccentRingAmbientOpacityAnimation(_outerAccentRing);
                }
            }

            _odakKaskadiAnimatedSegments.Clear();
            _odakKaskadiIconFinalPositions.Clear();
            _isOdakKaskadiAnimationActive = false;
        }

        private void ClearCenterAccentRingSolidOverlay()
        {
            if (_centerAccentRingSolidOverlay == null)
            {
                return;
            }

            var overlay = _centerAccentRingSolidOverlay;
            _centerAccentRingSolidOverlay = null;
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.BeginAnimation(Shape.StrokeThicknessProperty, null);
            RootCanvas.Children.Remove(overlay);
        }

        private void ClearOuterAccentRingSolidOverlay()
        {
            if (_outerAccentRingSolidOverlay == null)
            {
                return;
            }

            var overlay = _outerAccentRingSolidOverlay;
            _outerAccentRingSolidOverlay = null;
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.BeginAnimation(Shape.StrokeThicknessProperty, null);
            RootCanvas.Children.Remove(overlay);
        }

        private List<SegmentInteraction> GetMainRingInteractions()
        {
            return _segments
                .Where(segment => _segmentInteractions.ContainsKey(segment))
                .Select(segment => _segmentInteractions[segment])
                .OrderBy(interaction => interaction.ItemIndex)
                .ToList();
        }

        private void AnimateCenterPanelEntrance(double fromOpacity, double toOpacity, double fromScale, double peakScale, int fadeMs, int scaleMs)
        {
            if (_centerPanel == null)
            {
                AnimateUtilityDockEntrance(fromOpacity, toOpacity, fromScale, peakScale, fadeMs, scaleMs);
                return;
            }

            _centerPanel.Opacity = fromOpacity;
            _centerPanel.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            _centerPanel.RenderTransform = new ScaleTransform(fromScale, fromScale);

            _centerPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = fromOpacity,
                To = toOpacity,
                Duration = TimeSpan.FromMilliseconds(fadeMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (_centerPanel.RenderTransform is ScaleTransform centerScale)
            {
                var scaleAnimation = new DoubleAnimationUsingKeyFrames();
                scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(fromScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs * 0.72)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs)))
                {
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
                });
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
            }

            AnimateUtilityDockEntrance(fromOpacity, toOpacity, fromScale, peakScale, fadeMs, scaleMs);
        }

        private void AnimateUtilityDockEntrance(double fromOpacity, double toOpacity, double fromScale, double peakScale, int fadeMs, int scaleMs)
        {
            if (_utilityDockButton == null)
            {
                return;
            }

            _utilityDockButton.BeginAnimation(OpacityProperty, null);
            _utilityDockButton.Opacity = fromOpacity;
            _utilityDockButton.RenderTransformOrigin = new WpfPoint(0.5, 0.5);

            if (!(_utilityDockButton.RenderTransform is ScaleTransform buttonScale))
            {
                buttonScale = new ScaleTransform(fromScale, fromScale);
                _utilityDockButton.RenderTransform = buttonScale;
            }

            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            buttonScale.ScaleX = fromScale;
            buttonScale.ScaleY = fromScale;

            _utilityDockButton.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = fromOpacity,
                To = toOpacity,
                Duration = TimeSpan.FromMilliseconds(fadeMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(fromScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs * 0.72)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
            });
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private static void AnimateSegmentEntrance(UIElement element, double offsetX, double offsetY, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            element.Opacity = 0.0;
            element.RenderTransform = new TranslateTransform(offsetX, offsetY);

            element.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 40)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (element.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation
                {
                    From = offsetX,
                    To = 0.0,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = offsetY,
                    To = 0.0,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private static void AnimateIconEntrance(Border iconHost, double offsetX, double offsetY, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            iconHost.Opacity = 0.0;

            var originalLeft = Canvas.GetLeft(iconHost);
            var originalTop = Canvas.GetTop(iconHost);
            Canvas.SetLeft(iconHost, originalLeft + offsetX);
            Canvas.SetTop(iconHost, originalTop + offsetY);

            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 40)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            iconHost.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation
            {
                From = originalLeft + offsetX,
                To = originalLeft,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });
            iconHost.BeginAnimation(Canvas.TopProperty, new DoubleAnimation
            {
                From = originalTop + offsetY,
                To = originalTop,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });
        }

        private static void AnimateSegmentScaleEntrance(UIElement element, double fromScale, double toScale, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            element.Opacity = 0.0;
            element.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(fromScale, fromScale);

            element.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 60)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (element.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private static void AnimateIconScaleEntrance(Border iconHost, double fromScale, double toScale, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            iconHost.Opacity = 0.0;
            iconHost.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            iconHost.RenderTransform = new ScaleTransform(fromScale, fromScale);

            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 50)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (iconHost.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private void PlaySoftFadeOpenAnimation()
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(170),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            RootScaleTransform.ScaleX = 0.995;
            RootScaleTransform.ScaleY = 0.995;
            RootTranslateTransform.Y = 4.0;

            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                From = 0.995,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                From = 0.995,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            RootTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 4.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (_centerPanel != null)
            {
                _centerPanel.Opacity = 0.0;
                _centerPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.92, 1.0, 200, 200);
        }

        private void AnimateSegmentHighlight(ShapePath segment, bool active)
        {
            if (!_segmentBrushes.TryGetValue(segment, out var brush))
            {
                return;
            }

            var target = active
                ? IsNeonOrbitStyle() && segment.Stroke is SolidColorBrush activeStroke
                    ? System.Windows.Media.Color.FromArgb(54, activeStroke.Color.R, activeStroke.Color.G, activeStroke.Color.B)
                    : segment.Tag is MenuItemConfig activeItem
                        ? GetItemActiveSegmentColor(activeItem)
                        : _theme.SegmentActiveColor
                : IsNeonOrbitStyle() && segment.Stroke is SolidColorBrush baseStroke
                    ? System.Windows.Media.Color.FromArgb(0, baseStroke.Color.R, baseStroke.Color.G, baseStroke.Color.B)
                    : segment.Tag is MenuItemConfig baseItem
                        ? GetItemBaseSegmentColor(baseItem)
                        : _theme.SegmentColor;
            if (brush.Color == target)
            {
                return;
            }

            var animation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(active ? 112 : 154),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void ClearCenterTextForCategoryMode()
        {
            ClearCenterTextBlockInstant(_centerBreadcrumb);
            ClearCenterTextBlockInstant(_centerTitle);
            ClearCenterTextBlockInstant(_centerSubtitle);
            ApplyCenterTextLayout();
        }

        private void ClearCenterTextBlockInstant(TextBlock? textBlock)
        {
            if (textBlock == null)
            {
                return;
            }

            _centerTextAnimationVersions[textBlock] = GetNextCenterTextAnimationVersion(textBlock);
            textBlock.BeginAnimation(OpacityProperty, null);
            if (textBlock.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.Y = 0.0;
            }
            else
            {
                textBlock.RenderTransform = new TranslateTransform(0, 0);
            }

            textBlock.Text = string.Empty;
            textBlock.Opacity = 0.0;
        }

        private int GetNextCenterTextAnimationVersion(TextBlock textBlock)
        {
            return _centerTextAnimationVersions.TryGetValue(textBlock, out var current)
                ? current + 1
                : 1;
        }

        private void AnimateCenterText(TextBlock textBlock, string newText)
        {
            if (textBlock.Text == newText)
            {
                ApplyCenterTextLayout();
                return;
            }

            if (textBlock.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform(0, 0);
                textBlock.RenderTransform = translate;
            }

            var animationVersion = GetNextCenterTextAnimationVersion(textBlock);
            _centerTextAnimationVersions[textBlock] = animationVersion;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(86),
                EasingFunction = ease
            };
            fadeOut.Completed += (_, __) =>
            {
                if (!_centerTextAnimationVersions.TryGetValue(textBlock, out var currentVersion) || currentVersion != animationVersion)
                {
                    return;
                }

                textBlock.Text = newText;
                ApplyCenterTextLayout();
                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = 4.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(148),
                    EasingFunction = ease
                });
                var fadeIn = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(148),
                    EasingFunction = ease
                };
                textBlock.BeginAnimation(OpacityProperty, fadeIn);
            };
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                To = -2.5,
                Duration = TimeSpan.FromMilliseconds(86),
                EasingFunction = ease
            });
            textBlock.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ApplyCenterTextLayout()
        {
            if (_centerPanel == null)
            {
                return;
            }

            var panelSize = Math.Min(_centerPanel.Width, _centerPanel.Height);
            if (panelSize <= 0)
            {
                return;
            }

            var usableWidth = Math.Max(100, panelSize - 18);
            var hasBreadcrumb = !string.IsNullOrWhiteSpace(_centerBreadcrumb?.Text);
            var titleMaxHeight = _isCenterClockTypographyActive
                ? (hasBreadcrumb ? panelSize * 0.58 : panelSize * 0.7)
                : (hasBreadcrumb ? panelSize * 0.35 : panelSize * 0.42);
            var titleMinFontSize = _isCenterClockTypographyActive
                ? Math.Max(14.0, _centerTitleBaseFontSize * 1.55)
                : 11.0;
            FitCenterTextBlock(_centerBreadcrumb, usableWidth, hasBreadcrumb ? panelSize * 0.16 : 0, 9.0);
            FitCenterTextBlock(_centerTitle, usableWidth, titleMaxHeight, titleMinFontSize);
            FitCenterTextBlock(_centerSubtitle, usableWidth, hasBreadcrumb ? panelSize * 0.25 : panelSize * 0.28, 9.5);
        }

        private static void FitCenterTextBlock(TextBlock? textBlock, double maxWidth, double maxHeight, double minFontSize)
        {
            if (textBlock == null)
            {
                return;
            }

            var baseFontSize = textBlock.Tag is double taggedSize ? taggedSize : textBlock.FontSize;
            if (maxHeight <= 0)
            {
                textBlock.MaxWidth = maxWidth;
                textBlock.Width = maxWidth;
                textBlock.FontSize = baseFontSize;
                return;
            }

            textBlock.MaxWidth = maxWidth;
            textBlock.Width = double.NaN;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.TextTrimming = TextTrimming.None;

            var fontSize = baseFontSize;
            while (fontSize > minFontSize)
            {
                textBlock.FontSize = fontSize;
                textBlock.Measure(new System.Windows.Size(maxWidth, double.PositiveInfinity));
                if (textBlock.DesiredSize.Height <= maxHeight)
                {
                    return;
                }

                fontSize -= 0.5;
            }

            textBlock.FontSize = minFontSize;
            textBlock.Measure(new System.Windows.Size(maxWidth, double.PositiveInfinity));
        }

        private static ImageSource LoadIcon(string targetPath)
        {
            var resolvedPath = ResolveIconPath(targetPath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return FallbackIconImage;
            }

            if (TryGetCachedIcon(resolvedPath, out var cached))
            {
                return cached;
            }

            try
            {
                ImageSource? imageSource = null;
                if (Directory.Exists(resolvedPath))
                {
                    imageSource = LoadShellIcon(resolvedPath, isDirectory: true, useFileAttributes: false);
                }
                else if (File.Exists(resolvedPath))
                {
                    imageSource = LoadFileIcon(resolvedPath);
                }

                if (imageSource != null)
                {
                    CacheIcon(resolvedPath, imageSource);
                    return imageSource;
                }
            }
            catch
            {
            }

            CacheIcon(resolvedPath, FallbackIconImage);
            return FallbackIconImage;
        }

        private static ImageSource? LoadFileIcon(string resolvedPath)
        {
            var extension = System.IO.Path.GetExtension(resolvedPath);
            if (!string.IsNullOrWhiteSpace(extension) && !PathSpecificIconExtensions.Contains(extension))
            {
                if (TryGetCachedExtensionIcon(extension, out var cachedExtensionIcon))
                {
                    return cachedExtensionIcon;
                }

                var extensionIcon = LoadShellIcon(extension, isDirectory: false, useFileAttributes: true);
                if (extensionIcon != null)
                {
                    CacheExtensionIcon(extension, extensionIcon);
                    return extensionIcon;
                }
            }

            var shellIcon = LoadShellIcon(resolvedPath, isDirectory: false, useFileAttributes: false);
            if (shellIcon != null)
            {
                return shellIcon;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedPath);
            return ConvertIconToImageSource(icon);
        }

        private static bool TryGetCachedIcon(string resolvedPath, out ImageSource cached)
        {
            lock (IconCache)
            {
                if (!IconCache.TryGetValue(resolvedPath, out cached!))
                {
                    return false;
                }

                TouchIconCacheEntry(resolvedPath);
                return true;
            }
        }

        private static void CacheIcon(string resolvedPath, ImageSource imageSource)
        {
            lock (IconCache)
            {
                IconCache[resolvedPath] = imageSource;
                TouchIconCacheEntry(resolvedPath);

                while (IconCache.Count > IconCacheLimit && IconCacheUsage.First != null)
                {
                    var oldestKey = IconCacheUsage.First.Value;
                    IconCacheUsage.RemoveFirst();
                    IconCacheNodes.Remove(oldestKey);
                    IconCache.Remove(oldestKey);
                }
            }
        }

        private static void TouchIconCacheEntry(string resolvedPath)
        {
            if (IconCacheNodes.TryGetValue(resolvedPath, out var existingNode))
            {
                IconCacheUsage.Remove(existingNode);
                IconCacheUsage.AddLast(existingNode);
                return;
            }

            var node = new LinkedListNode<string>(resolvedPath);
            IconCacheNodes[resolvedPath] = node;
            IconCacheUsage.AddLast(node);
        }

        private static bool TryGetCachedExtensionIcon(string extension, out ImageSource cached)
        {
            lock (IconCache)
            {
                return ExtensionIconCache.TryGetValue(extension, out cached!);
            }
        }

        private static void CacheExtensionIcon(string extension, ImageSource imageSource)
        {
            lock (IconCache)
            {
                ExtensionIconCache[extension] = imageSource;
            }
        }

        private static string ResolveIconPath(string targetPath)
        {
            return string.IsNullOrWhiteSpace(targetPath)
                ? string.Empty
                : Environment.ExpandEnvironmentVariables(targetPath.Trim());
        }

        private static ImageSource? LoadShellIcon(string path, bool isDirectory, bool useFileAttributes)
        {
            var flags = ShgfiIcon | ShgfiLargeIcon;
            var attributes = 0u;
            if (useFileAttributes)
            {
                flags |= ShgfiUseFileAttributes;
                attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
            }

            var fileInfo = new ShFileInfo();
            var result = SHGetFileInfo(path, attributes, ref fileInfo, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
            if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CreateBitmapSource(fileInfo.hIcon);
            }
            finally
            {
                DestroyIcon(fileInfo.hIcon);
            }
        }

        private static ImageSource? ConvertIconToImageSource(System.Drawing.Icon? icon)
        {
            if (icon == null)
            {
                return null;
            }

            return CreateBitmapSource(icon.Handle);
        }

        private static ImageSource CreateBitmapSource(IntPtr iconHandle)
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            source.Freeze();
            return source;
        }

        private static ImageSource CreateFallbackIcon()
        {
            return FallbackIconImage;
        }

        private static ImageSource CreateFallbackIconImage()
        {
            var drawing = new GeometryDrawing(
                System.Windows.Media.Brushes.White,
                null,
                new RectangleGeometry(new Rect(12, 12, 40, 40), 8, 8));
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeNormal = 0x00000080;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref ShFileInfo psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                if (_itemContextMenu != null)
                {
                    HideItemContextMenu();
                    e.Handled = true;
                    return;
                }

                if (_isEditModeEnabled)
                {
                    ToggleEditMode();
                    e.Handled = true;
                    return;
                }

                Dismiss();
                e.Handled = true;
            }
        }

        private void PollModifierState()
        {
            var isShiftPressed = (GetAsyncKeyState(VkShift) & 0x8000) != 0;
            if (isShiftPressed && !_wasShiftPressed)
            {
                ToggleEditMode();
            }

            _wasShiftPressed = isShiftPressed;
            var isTargetingPressed = IsTargetingShortcutPressed();
            if (isTargetingPressed && !_wasAltPressed)
            {
                BeginAltGuideMode();
            }
            else if (!isTargetingPressed && _wasAltPressed)
            {
                CompleteAltGuideMode();
            }

            _wasAltPressed = isTargetingPressed;
            UpdateBackdropInteractivity();
            UpdateEditModeClickThroughState();
        }

        private void ToggleEditMode()
        {
            _isEditModeEnabled = !_isEditModeEnabled;
            PlayUiSound(_isEditModeEnabled ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
            ClearPendingDragState(clearPreview: true);
            HideItemContextMenu();
            if (_isEditModeEnabled)
            {
                HideAltGuideLine();
                _isAltGuideActive = false;
                SetTargetingCursorHidden(false);
            }
            UpdateBackdropInteractivity();
            UpdateEditModeClickThroughState();

            if (_isEditModeEnabled)
            {
                EnsureEditModeVisuals(GetLayoutProfile());
            }
            else
            {
                FadeOutEditModeVisuals();
            }

            UpdateMonochromeBackdropLayer(delayCaptureForVisualStability: !_isEditModeEnabled);

            if (_centerTitle != null)
            {
                SetCenterTitleText(GetDefaultCenterTitleText(), useIdleClockTypography: ShouldShowIdleCenterClock());
            }

            if (_centerSubtitle != null)
            {
                AnimateCenterText(
                    _centerSubtitle,
                    _isEditModeEnabled
                        ? string.Empty
                        : "Bir dilimin üstüne gelin\nve tıklayın");
            }
        }

        private bool IsEditModifierActive()
        {
            return _isEditModeEnabled;
        }

        private void EnsureEditModeVisuals(LayoutProfile profile)
        {
            if (!_isEditModeEnabled)
            {
                return;
            }

            var isNewHaze = false;
            if (_editModeFocusHaze == null)
            {
                isNewHaze = true;
                _editModeFocusHaze = new Ellipse
                {
                    Fill = CreateEditModeFocusHazeBrush(),
                    Opacity = 0,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    CacheMode = new BitmapCache(),
                    Effect = CreateOptimizedShadowEffect(
                        28,
                        0.2,
                        BlendColors(_theme.CenterBorderColor, Colors.White, 0.18))
                };
                Panel.SetZIndex(_editModeFocusHaze, -20);
                RootCanvas.Children.Add(_editModeFocusHaze);
            }

            var isNewRing = false;
            if (_editModeRing == null)
            {
                isNewRing = true;
                _editModeRing = new Ellipse
                {
                    Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(210, 255, 212, 104)),
                    Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(20, 255, 208, 92)),
                    Opacity = 0,
                    StrokeDashArray = EditModeRingDashArray,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false,
                    RenderTransformOrigin = new WpfPoint(0.5, 0.5),
                    RenderTransform = new RotateTransform(0),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    Effect = CreateOptimizedShadowEffect(18, 0.28, System.Windows.Media.Color.FromArgb(255, 255, 206, 92))
                };
                RootCanvas.Children.Add(_editModeRing);
            }

            var hazeRadius = Math.Max(
                profile.OuterRadius + Math.Max(76, profile.OuterRadius * 0.64),
                profile.CenterSize * 1.92);
            var hazeDiameter = hazeRadius * 2.0;
            _editModeFocusHaze.Width = hazeDiameter;
            _editModeFocusHaze.Height = hazeDiameter;
            Canvas.SetLeft(_editModeFocusHaze, _centerX - hazeRadius);
            Canvas.SetTop(_editModeFocusHaze, _centerY - hazeRadius);
            if (isNewHaze)
            {
                _editModeFocusHaze.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation
                    {
                        From = 0.14,
                        To = 0.3,
                        Duration = TimeSpan.FromMilliseconds(2200),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });
            }

            var centerRadius = profile.CenterSize / 2.0;
            var gap = Math.Max(10, profile.InnerRadius - centerRadius);
            var ringRadius = centerRadius + (gap * 0.52);
            var ringThickness = Math.Max(8, Math.Min(16, gap * 0.34));
            var ringDiameter = ringRadius * 2.0;

            _editModeRing.Width = ringDiameter;
            _editModeRing.Height = ringDiameter;
            _editModeRing.StrokeThickness = ringThickness;
            Canvas.SetLeft(_editModeRing, _centerX - ringRadius);
            Canvas.SetTop(_editModeRing, _centerY - ringRadius);
            if (isNewRing)
            {
                _editModeRing.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation
                    {
                        From = 0.62,
                        To = 0.95,
                        Duration = TimeSpan.FromMilliseconds(1800),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    });

                if (_editModeRing.RenderTransform is RotateTransform rotate)
                {
                    rotate.BeginAnimation(
                        RotateTransform.AngleProperty,
                        new DoubleAnimation
                        {
                            From = 0,
                            To = 360,
                            Duration = TimeSpan.FromSeconds(18),
                            RepeatBehavior = RepeatBehavior.Forever,
                            EasingFunction = null
                        });
                }
            }

            if (_centerTitle != null)
            {
                ApplyCenterTitleTypography(useIdleClockTypography: false);
                _centerTitle.Text = "Sürükleme Modu";
            }

            if (_centerSubtitle != null)
            {
                _centerSubtitle.Text = string.Empty;
            }

            ApplyCenterTextLayout();
        }

        private RadialGradientBrush CreateEditModeFocusHazeBrush()
        {
            var baseColor = BlendColors(_theme.CenterBorderColor, Colors.White, IsNeonOrbitStyle() ? 0.32 : 0.2);
            var outerColor = BlendColors(_theme.CenterBorderColor, Colors.Black, 0.22);
            var brush = new RadialGradientBrush
            {
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(58, baseColor.R, baseColor.G, baseColor.B), 0.0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(26, baseColor.R, baseColor.G, baseColor.B), 0.32));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(12, outerColor.R, outerColor.G, outerColor.B), 0.62));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, outerColor.R, outerColor.G, outerColor.B), 1.0));
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private void FadeOutEditModeVisuals()
        {
            var haze = _editModeFocusHaze;
            _editModeFocusHaze = null;
            if (haze != null)
            {
                haze.BeginAnimation(OpacityProperty, null);
                var hazeFade = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                hazeFade.Completed += (_, __) =>
                {
                    RootCanvas.Children.Remove(haze);
                };
                haze.BeginAnimation(OpacityProperty, hazeFade);
            }

            if (_editModeRing == null)
            {
                return;
            }

            var ring = _editModeRing;
            _editModeRing = null;
            var fade = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fade.Completed += (_, __) =>
            {
                RootCanvas.Children.Remove(ring);
            };
            ring.BeginAnimation(OpacityProperty, fade);
        }

        private void ShowItemContextMenuForSource(DependencyObject source, MouseButtonEventArgs e)
        {
            if (!_dragInteractions.TryGetValue(source, out var target) || target.Item == null || target.IsGhostSlot)
            {
                return;
            }

            e.Handled = true;
            ShowItemContextMenu(target, e.GetPosition(RootCanvas));
        }

        private void ShowItemContextMenu(DragItemInteraction target, WpfPoint position)
        {
            HideItemContextMenu();
            _contextMenuTarget = target;
            PlayUiSound(SoundCue.UiSelect);

            var title = new TextBlock
            {
                Text = target.Item?.Label ?? "Menu Ogesi",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(232, 244, 246, 250)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var renameRow = CreateContextMenuActionRow(
                "Yeniden Adlandir",
                "Ogenin gorunen adini degistir",
                System.Windows.Media.Color.FromArgb(238, 214, 234, 255),
                System.Windows.Media.Color.FromArgb(92, 126, 184, 255),
                () => RenameContextMenuTarget());

            var fixedColorRow = CreateContextMenuActionRow(
                "Sabit Renk",
                "Bu oge icin ozel dilim rengi sec",
                System.Windows.Media.Color.FromArgb(238, 255, 222, 160),
                System.Windows.Media.Color.FromArgb(92, 255, 192, 96),
                () => PickFixedColorForContextMenuTarget());

            var settingsRow = CreateContextMenuActionRow(
                "Ayarlar",
                "Ayar penceresini ac",
                System.Windows.Media.Color.FromArgb(238, 208, 232, 255),
                System.Windows.Media.Color.FromArgb(92, 132, 176, 255),
                () => OpenSettingsWindowFromOverlay());

            Border? symbolRow = null;
            if (target.Item != null && IsCategoryItem(target.Item))
            {
                symbolRow = CreateContextMenuActionRow(
                    "Sembol Seç",
                    "Kategori ikonunu değiştir",
                    System.Windows.Media.Color.FromArgb(238, 206, 236, 255),
                    System.Windows.Media.Color.FromArgb(92, 108, 166, 255),
                    () => PickCategorySymbolForContextMenuTarget());
            }

            var addCategoryHint = GetCategoryInsertHint(target);
            var addCategoryRow = CreateContextMenuActionRow(
                "Kategori Ekle",
                addCategoryHint,
                System.Windows.Media.Color.FromArgb(238, 198, 248, 216),
                System.Windows.Media.Color.FromArgb(92, 110, 198, 138),
                () => AddCategoryToContextMenuTarget());

            var deleteText = new TextBlock
            {
                Text = "Sil",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(238, 255, 225, 162)),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var deleteHint = new TextBlock
            {
                Text = "Bu ogeyi menuden kaldir",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(138, 208, 213, 224)),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };

            var deleteGrid = new Grid();
            deleteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            deleteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            deleteGrid.Children.Add(deleteText);
            Grid.SetColumn(deleteText, 0);
            deleteGrid.Children.Add(deleteHint);
            Grid.SetColumn(deleteHint, 1);

            var deleteNormalBackground = GetCachedBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255));
            var deleteNormalBorder = GetCachedBrush(System.Windows.Media.Color.FromArgb(42, 255, 255, 255));
            var deleteHoverBackground = GetCachedBrush(System.Windows.Media.Color.FromArgb(22, 255, 220, 126));
            var deleteHoverBorder = GetCachedBrush(System.Windows.Media.Color.FromArgb(92, 255, 220, 126));

            var deleteRow = new Border
            {
                Background = deleteNormalBackground,
                BorderBrush = deleteNormalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 9, 10, 9),
                Cursor = Cursors.Hand
            };
            deleteRow.Child = deleteGrid;

            deleteRow.MouseEnter += (_, __) =>
            {
                deleteRow.Background = deleteHoverBackground;
                deleteRow.BorderBrush = deleteHoverBorder;
            };
            deleteRow.MouseLeave += (_, __) =>
            {
                deleteRow.Background = deleteNormalBackground;
                deleteRow.BorderBrush = deleteNormalBorder;
            };
            deleteRow.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                DeleteContextMenuTarget();
            };

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(renameRow);
            if (symbolRow != null)
            {
                panel.Children.Add(symbolRow);
            }
            panel.Children.Add(addCategoryRow);
            panel.Children.Add(fixedColorRow);
            panel.Children.Add(settingsRow);
            panel.Children.Add(deleteRow);

            var menu = new Border
            {
                Width = 214,
                Background = GetCachedBrush(System.Windows.Media.Color.FromArgb(242, 22, 24, 30)),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Child = panel,
                Effect = CreateOptimizedShadowEffect(18, 0.28, System.Windows.Media.Color.FromArgb(255, 0, 0, 0))
            };

            menu.MouseLeftButtonDown += (_, e) => e.Handled = true;
            menu.MouseRightButtonDown += (_, e) => e.Handled = true;

            menu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = menu.DesiredSize;
            var left = Math.Max(18, Math.Min(position.X + 16, Math.Max(18, ActualWidth - desired.Width - 18)));
            var top = Math.Max(18, Math.Min(position.Y - 8, Math.Max(18, ActualHeight - desired.Height - 18)));
            Canvas.SetLeft(menu, left);
            Canvas.SetTop(menu, top);
            Panel.SetZIndex(menu, OverlayContextMenuZIndex);
            RootCanvas.Children.Add(menu);
            _itemContextMenu = menu;
        }

        private void HideItemContextMenu()
        {
            if (_itemContextMenu != null)
            {
                RootCanvas.Children.Remove(_itemContextMenu);
                _itemContextMenu = null;
            }

            _contextMenuTarget = null;
        }

        private void OnUtilityDockButtonMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_utilityDockButton == null)
            {
                return;
            }

            e.Handled = true;
            var menuTag = _itemContextMenu?.Tag as string;
            if (_itemContextMenu != null &&
                !string.IsNullOrWhiteSpace(menuTag) &&
                menuTag.StartsWith("Utility", StringComparison.Ordinal))
            {
                HideItemContextMenu();
                return;
            }

            ShowUtilityDockContextMenu();
        }

        private bool IsWithinUtilityDockButton(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, _utilityDockButton) ||
                    ReferenceEquals(current, _utilityDockButtonCore))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void ShowUtilityDockContextMenu()
        {
            HideItemContextMenu();
            PlayUiSound(SoundCue.UiSelect);

            var title = new TextBlock
            {
                Text = "Arac Kutusu",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(236, 244, 246, 252)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var shutdownTimerRow = CreateContextMenuActionRow(
                "Kapatma Zamanlayicisi",
                "30 dk / 1 saat / 2 saat / iptal",
                System.Windows.Media.Color.FromArgb(238, 255, 230, 170),
                System.Windows.Media.Color.FromArgb(96, 255, 186, 96),
                ShowShutdownTimerOptionsMenu);

            var stopwatchRow = CreateContextMenuActionRow(
                "Kronometre",
                "Yakinda: baslat / durdur / sifirla",
                System.Windows.Media.Color.FromArgb(238, 216, 233, 255),
                System.Windows.Media.Color.FromArgb(96, 138, 188, 255),
                () => ShowUtilityDockComingSoonHint("Kronometre yakinda"));

            var settingsRow = CreateContextMenuActionRow(
                "Ayarlar",
                "Ayar penceresini ac",
                System.Windows.Media.Color.FromArgb(238, 216, 246, 255),
                System.Windows.Media.Color.FromArgb(96, 124, 180, 255),
                () => OpenSettingsWindowFromOverlay());

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(shutdownTimerRow);
            panel.Children.Add(stopwatchRow);
            panel.Children.Add(settingsRow);

            var menu = new Border
            {
                Width = 244,
                Background = GetCachedBrush(System.Windows.Media.Color.FromArgb(242, 22, 24, 30)),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Child = panel,
                Effect = CreateOptimizedShadowEffect(18, 0.28, System.Windows.Media.Color.FromArgb(255, 0, 0, 0))
            };

            menu.MouseLeftButtonDown += (_, args) => args.Handled = true;
            menu.MouseRightButtonDown += (_, args) => args.Handled = true;

            menu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = menu.DesiredSize;
            var profile = GetLayoutProfile();
            var rightAnchor = _centerX + profile.OuterRadius;
            var left = rightAnchor + Math.Max(26.0, profile.IconSize * 0.62);
            var top = _centerY - (desired.Height / 2.0);
            left = Math.Max(18, Math.Min(left, Math.Max(18, ActualWidth - desired.Width - 18)));
            top = Math.Max(18, Math.Min(top, Math.Max(18, ActualHeight - desired.Height - 18)));
            Canvas.SetLeft(menu, left);
            Canvas.SetTop(menu, top);
            Panel.SetZIndex(menu, OverlayContextMenuZIndex);
            menu.Tag = UtilityDockTrayTag;
            RootCanvas.Children.Add(menu);
            _itemContextMenu = menu;
        }

        private void ShowShutdownTimerOptionsMenu()
        {
            HideItemContextMenu();
            PlayUiSound(SoundCue.UiSelect);

            var title = new TextBlock
            {
                Text = "Kapatma Zamanlayicisi",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(236, 244, 246, 252)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var option30Min = CreateContextMenuActionRow(
                "30 Dakika",
                "Bilgisayar 30 dk sonra kapanir",
                System.Windows.Media.Color.FromArgb(238, 255, 227, 166),
                System.Windows.Media.Color.FromArgb(96, 255, 186, 96),
                () => ScheduleSystemShutdown(TimeSpan.FromMinutes(30)));

            var option1Hour = CreateContextMenuActionRow(
                "1 Saat",
                "Bilgisayar 1 saat sonra kapanir",
                System.Windows.Media.Color.FromArgb(238, 255, 227, 166),
                System.Windows.Media.Color.FromArgb(96, 255, 186, 96),
                () => ScheduleSystemShutdown(TimeSpan.FromHours(1)));

            var option2Hour = CreateContextMenuActionRow(
                "2 Saat",
                "Bilgisayar 2 saat sonra kapanir",
                System.Windows.Media.Color.FromArgb(238, 255, 227, 166),
                System.Windows.Media.Color.FromArgb(96, 255, 186, 96),
                () => ScheduleSystemShutdown(TimeSpan.FromHours(2)));

            var cancelOption = CreateContextMenuActionRow(
                "Iptal",
                "Planlanan kapatmayi iptal et",
                System.Windows.Media.Color.FromArgb(238, 255, 206, 190),
                System.Windows.Media.Color.FromArgb(96, 255, 132, 110),
                CancelScheduledSystemShutdown);

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(option30Min);
            panel.Children.Add(option1Hour);
            panel.Children.Add(option2Hour);
            panel.Children.Add(cancelOption);

            var menu = new Border
            {
                Width = 252,
                Background = GetCachedBrush(System.Windows.Media.Color.FromArgb(242, 22, 24, 30)),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Child = panel,
                Effect = CreateOptimizedShadowEffect(18, 0.28, System.Windows.Media.Color.FromArgb(255, 0, 0, 0))
            };

            menu.MouseLeftButtonDown += (_, e) => e.Handled = true;
            menu.MouseRightButtonDown += (_, e) => e.Handled = true;
            menu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

            var desired = menu.DesiredSize;
            var profile = GetLayoutProfile();
            var rightAnchor = _centerX + profile.OuterRadius;
            var left = rightAnchor + Math.Max(26.0, profile.IconSize * 0.62);
            var top = _centerY - (desired.Height / 2.0);
            left = Math.Max(18, Math.Min(left, Math.Max(18, ActualWidth - desired.Width - 18)));
            top = Math.Max(18, Math.Min(top, Math.Max(18, ActualHeight - desired.Height - 18)));

            Canvas.SetLeft(menu, left);
            Canvas.SetTop(menu, top);
            Panel.SetZIndex(menu, OverlayContextMenuZIndex);
            menu.Tag = UtilityShutdownTrayTag;
            RootCanvas.Children.Add(menu);
            _itemContextMenu = menu;
        }

        private void ScheduleSystemShutdown(TimeSpan delay)
        {
            var seconds = (int)Math.Round(Math.Max(1.0, delay.TotalSeconds));
            if (!TryRunShutdownCommand("/s /t " + seconds.ToString(CultureInfo.InvariantCulture)))
            {
                ShowUtilityDockComingSoonHint("Kapatma ayarlanamadi");
                return;
            }

            _shutdownCountdownTargetUtc = DateTime.UtcNow.AddSeconds(seconds);
            HideItemContextMenu();
            if (_centerTitle != null)
            {
                SetCenterTitleText(GetDefaultCenterTitleText(), useIdleClockTypography: true);
            }
        }

        private void CancelScheduledSystemShutdown()
        {
            if (!TryRunShutdownCommand("/a"))
            {
                ShowUtilityDockComingSoonHint("Iptal komutu calistirilamadi");
                return;
            }

            _shutdownCountdownTargetUtc = null;
            HideItemContextMenu();
            if (_centerTitle != null)
            {
                SetCenterTitleText(GetDefaultCenterTitleText(), useIdleClockTypography: ShouldShowIdleCenterClock());
            }
        }

        private static bool TryRunShutdownCommand(string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return process != null;
            }
            catch
            {
                return false;
            }
        }

        private void ShowUtilityDockComingSoonHint(string text)
        {
            HideItemContextMenu();
            PlayUiSound(SoundCue.UiHover);
            if (_centerTitle != null)
            {
                AnimateCenterText(_centerTitle, text);
            }
        }

        private void ShowOverlaySettingsContextMenu(WpfPoint position)
        {
            HideItemContextMenu();
            PlayUiSound(SoundCue.UiSelect);

            var title = new TextBlock
            {
                Text = "Hizli Menu",
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(232, 244, 246, 250)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var settingsRow = CreateContextMenuActionRow(
                "Ayarlar",
                "Ayar penceresini ac",
                System.Windows.Media.Color.FromArgb(238, 214, 234, 255),
                System.Windows.Media.Color.FromArgb(92, 126, 184, 255),
                () => OpenSettingsWindowFromOverlay());

            var addCategoryRow = CreateContextMenuActionRow(
                "Kategori Ekle",
                "Ana halkaya yeni kategori ekle",
                System.Windows.Media.Color.FromArgb(238, 198, 248, 216),
                System.Windows.Media.Color.FromArgb(92, 110, 198, 138),
                () => AddCategoryToMainRing());

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(addCategoryRow);
            panel.Children.Add(settingsRow);

            var menu = new Border
            {
                Width = 214,
                Background = GetCachedBrush(System.Windows.Media.Color.FromArgb(242, 22, 24, 30)),
                BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                Child = panel,
                Effect = CreateOptimizedShadowEffect(18, 0.28, System.Windows.Media.Color.FromArgb(255, 0, 0, 0))
            };

            menu.MouseLeftButtonDown += (_, e) => e.Handled = true;
            menu.MouseRightButtonDown += (_, e) => e.Handled = true;

            menu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = menu.DesiredSize;
            var left = Math.Max(18, Math.Min(position.X + 16, Math.Max(18, ActualWidth - desired.Width - 18)));
            var top = Math.Max(18, Math.Min(position.Y - 8, Math.Max(18, ActualHeight - desired.Height - 18)));
            Canvas.SetLeft(menu, left);
            Canvas.SetTop(menu, top);
            Panel.SetZIndex(menu, OverlayContextMenuZIndex);
            RootCanvas.Children.Add(menu);
            _itemContextMenu = menu;
        }

        private void OpenSettingsWindowFromOverlay()
        {
            HideItemContextMenu();

            if (Application.Current == null)
            {
                return;
            }

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.OpenSettingsWindowFromOverlay();
                return;
            }

            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow fallbackMainWindow)
                {
                    fallbackMainWindow.OpenSettingsWindowFromOverlay();
                    return;
                }
            }
        }

        private Border CreateContextMenuActionRow(string title, string hint, System.Windows.Media.Color foregroundColor, System.Windows.Media.Color hoverBorderColor, Action action)
        {
            var titleText = new TextBlock
            {
                Text = title,
                Foreground = GetCachedBrush(foregroundColor),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var hintText = new TextBlock
            {
                Text = hint,
                Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(138, 208, 213, 224)),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(titleText);
            Grid.SetColumn(titleText, 0);
            grid.Children.Add(hintText);
            Grid.SetColumn(hintText, 1);

            var normalBackground = GetCachedBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255));
            var normalBorder = GetCachedBrush(System.Windows.Media.Color.FromArgb(42, 255, 255, 255));
            var hoverBackground = GetCachedBrush(System.Windows.Media.Color.FromArgb(18, hoverBorderColor.R, hoverBorderColor.G, hoverBorderColor.B));
            var hoverBorder = GetCachedBrush(System.Windows.Media.Color.FromArgb(92, hoverBorderColor.R, hoverBorderColor.G, hoverBorderColor.B));

            var row = new Border
            {
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 9, 10, 9),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 6),
                Child = grid
            };

            row.MouseEnter += (_, __) =>
            {
                row.Background = hoverBackground;
                row.BorderBrush = hoverBorder;
            };
            row.MouseLeave += (_, __) =>
            {
                row.Background = normalBackground;
                row.BorderBrush = normalBorder;
            };
            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                action();
            };

            return row;
        }

        private void DeleteContextMenuTarget()
        {
            if (_contextMenuTarget?.Item == null)
            {
                HideItemContextMenu();
                return;
            }

            if (_contextMenuTarget.ItemIndex < 0 || _contextMenuTarget.ItemIndex >= _contextMenuTarget.OwnerItems.Count)
            {
                HideItemContextMenu();
                return;
            }

            _contextMenuTarget.OwnerItems.RemoveAt(_contextMenuTarget.ItemIndex);
            HideItemContextMenu();
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.Warning);
        }

        private void AddCategoryToContextMenuTarget()
        {
            if (_contextMenuTarget == null)
            {
                HideItemContextMenu();
                return;
            }

            var targetList = ResolveCategoryInsertTargetList(_contextMenuTarget);
            if (targetList == null)
            {
                HideItemContextMenu();
                return;
            }

            AddCategoryToList(targetList);
        }

        private void AddCategoryToMainRing()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count)
            {
                HideItemContextMenu();
                return;
            }

            AddCategoryToList(_pages[_currentPageIndex]);
        }

        private void AddCategoryToList(List<MenuItemConfig> targetList)
        {
            targetList.Add(new MenuItemConfig
            {
                Label = "Yeni Kategori",
                TargetPath = string.Empty,
                IsCategory = true
            });

            HideItemContextMenu();
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.UiSelect);
        }

        private void RenameContextMenuTarget()
        {
            if (_contextMenuTarget?.Item == null)
            {
                HideItemContextMenu();
                return;
            }

            var currentItem = _contextMenuTarget.Item;
            var result = ShowSimpleTextInputDialog("Yeniden Adlandir", "Yeni ad", currentItem.Label);
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            currentItem.Label = result.Trim();
            HideItemContextMenu();
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.Success);
        }

        private void PickFixedColorForContextMenuTarget()
        {
            if (_contextMenuTarget?.Item == null)
            {
                HideItemContextMenu();
                return;
            }

            var hasFixedColor = TryGetFixedColor(_contextMenuTarget.Item, out var currentColor);
            var initialColorHex = hasFixedColor
                ? $"#{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}"
                : GetItemBaseSegmentColor(_contextMenuTarget.Item).ToString();

            var dialog = new ColorPickerWindow(initialColorHex, hasFixedColor)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _contextMenuTarget.Item.FixedColor = dialog.ResetToThemeRequested
                ? string.Empty
                : dialog.SelectedHexColor;
            HideItemContextMenu();
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.UiSelect);
        }

        private void PickCategorySymbolForContextMenuTarget()
        {
            if (_contextMenuTarget?.Item == null || !IsCategoryItem(_contextMenuTarget.Item))
            {
                HideItemContextMenu();
                return;
            }

            var dialog = new CategorySymbolPickerWindow(_contextMenuTarget.Item.CategorySymbolKey)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _contextMenuTarget.Item.CategorySymbolKey = dialog.SelectedSymbolKey;
            HideItemContextMenu();
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.UiSelect);
        }

        private List<MenuItemConfig>? ResolveCategoryInsertTargetList(DragItemInteraction target)
        {
            if (target.Item != null && IsCategoryItem(target.Item))
            {
                return target.Item.Children;
            }

            var ownerCategory = FindCategoryByChildrenOwner(target.OwnerItems);
            if (ownerCategory != null)
            {
                return ownerCategory.Children;
            }

            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count)
            {
                return null;
            }

            return _pages[_currentPageIndex];
        }

        private string GetCategoryInsertHint(DragItemInteraction target)
        {
            if (target.Item != null && IsCategoryItem(target.Item))
            {
                return "Bu kategorinin icine yeni kategori ekle";
            }

            var ownerCategory = FindCategoryByChildrenOwner(target.OwnerItems);
            return ownerCategory != null
                ? "Bu kategorinin icine yeni kategori ekle"
                : "Ana halkaya yeni kategori ekle";
        }

        private MenuItemConfig? FindCategoryByChildrenOwner(List<MenuItemConfig> ownerItems)
        {
            foreach (var pageItems in _pages)
            {
                var candidate = FindCategoryByChildrenOwnerRecursive(pageItems, ownerItems);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MenuItemConfig? FindCategoryByChildrenOwnerRecursive(IEnumerable<MenuItemConfig> items, List<MenuItemConfig> ownerItems)
        {
            foreach (var item in items)
            {
                if (ReferenceEquals(item.Children, ownerItems))
                {
                    return item;
                }

                var nested = FindCategoryByChildrenOwnerRecursive(item.Children, ownerItems);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private string? ShowSimpleTextInputDialog(string title, string label, string initialValue)
        {
            var textBox = new TextBox
            {
                Text = initialValue,
                MinWidth = 240,
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var confirmButton = new Button
            {
                Content = "Tamam",
                Width = 92,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelButton = new Button
            {
                Content = "İptal",
                Width = 92,
                Height = 34
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            buttonPanel.Children.Add(confirmButton);
            buttonPanel.Children.Add(cancelButton);

            var panel = new StackPanel
            {
                Margin = new Thickness(18)
            };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = GetCachedBrush(Colors.White),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(textBox);
            panel.Children.Add(buttonPanel);

            var dialog = new Window
            {
                Title = title,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 340,
                Height = 165,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = GetCachedBrush(System.Windows.Media.Color.FromRgb(20, 22, 28)),
                Content = panel,
                ShowInTaskbar = false
            };

            string? result = null;
            confirmButton.Click += (_, __) =>
            {
                result = textBox.Text;
                dialog.DialogResult = true;
            };
            cancelButton.Click += (_, __) => dialog.DialogResult = false;
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    result = textBox.Text;
                    dialog.DialogResult = true;
                }
            };

            dialog.ShowDialog();
            return result;
        }

        private void UpdateBackdropInteractivity()
        {
            if (BackdropRoot == null)
            {
                return;
            }

            // Drag & drop olaylarının stabil çalışması için overlay arka planı
            // edit modunda da hit-test alabilir durumda kalmalı.
            BackdropRoot.Background = BackdropHitTestBrush;
            if (IsEditModifierActive() && (_editModeRing == null || _editModeFocusHaze == null))
            {
                EnsureEditModeVisuals(GetLayoutProfile());
            }
        }

        private void UpdateEditModeClickThroughState()
        {
            if (!_isEditModeEnabled)
            {
                SetWindowClickThrough(false);
                return;
            }

            var hasDragCapture = ReferenceEquals(Mouse.Captured, BackdropRoot);
            if (_pendingDragInteraction != null || hasDragCapture)
            {
                SetWindowClickThrough(false);
                return;
            }

            SetWindowClickThrough(!IsInteractivePoint(GetCursorPositionOnRootCanvas()));
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

        private void UpdateMonochromeBackdropLayer(bool delayCaptureForVisualStability = false)
        {
            if (MonochromeBackdropImage == null)
            {
                return;
            }

            // Düzenleme/sürükleme modunda dışarıdan dosya/klasör sürükleyebilmek için
            // tam ekran gri katmanı geçici olarak kaldırıyoruz.
            if (!_features.EnableMonochromeBackdrop || _isEditModeEnabled)
            {
                _monochromeBackdropCaptureTimer.Stop();
                CancelMonochromeSnapshotTransition(restoreRingsImmediately: true);
                ClearMonochromeBackdropLayer();
                return;
            }

            if (delayCaptureForVisualStability)
            {
                _monochromeBackdropCaptureTimer.Stop();
                _monochromeBackdropCaptureTimer.Interval = MonochromeBackdropCaptureDelay;
                _monochromeBackdropCaptureTimer.Start();
                return;
            }

            _monochromeBackdropCaptureTimer.Stop();
            ApplyMonochromeBackdropSnapshot();
        }

        private void OnMonochromeBackdropCaptureTimerTick(object? sender, EventArgs e)
        {
            _monochromeBackdropCaptureTimer.Stop();
            if (MonochromeBackdropImage == null || !_features.EnableMonochromeBackdrop || _isEditModeEnabled)
            {
                return;
            }

            ApplyMonochromeBackdropSnapshot();
        }

        private void ApplyMonochromeBackdropSnapshot()
        {
            if (MonochromeBackdropImage == null)
            {
                return;
            }

            if (TryStartMonochromeSnapshotTransition())
            {
                return;
            }

            ApplyMonochromeBackdropSnapshotCore();
        }

        private bool TryStartMonochromeSnapshotTransition()
        {
            if (_isMonochromeSnapshotTransitionRunning)
            {
                _hasPendingMonochromeSnapshotRequest = true;
                return true;
            }

            var centerRing = IsMonochromeRingSnapshotFadeCandidate(_centerAccentRing)
                ? _centerAccentRing
                : null;
            var outerRing = IsMonochromeRingSnapshotFadeCandidate(_outerAccentRing)
                ? _outerAccentRing
                : null;
            if (centerRing == null && outerRing == null)
            {
                return false;
            }

            _isMonochromeSnapshotTransitionRunning = true;
            _hasPendingMonochromeSnapshotRequest = false;
            _monochromeSnapshotCenterRing = centerRing;
            _monochromeSnapshotOuterRing = outerRing;
            _monochromeSnapshotCenterRestoreOpacity = ResolveMonochromeRingRestoreOpacity(centerRing, _centerAccentRingBaseOpacity, 0.92);
            _monochromeSnapshotOuterRestoreOpacity = ResolveMonochromeRingRestoreOpacity(outerRing, _outerAccentRingBaseOpacity, 0.94);

            if (centerRing != null)
            {
                BeginMonochromeRingFadeOut(centerRing);
            }

            if (outerRing != null)
            {
                BeginMonochromeRingFadeOut(outerRing);
            }

            _monochromeRingSnapshotTimer.Stop();
            _monochromeRingSnapshotTimer.Interval = MonochromeRingFadeOutDuration + MonochromeRingFadeOutCaptureBuffer;
            _monochromeRingSnapshotTimer.Start();
            return true;
        }

        private bool IsMonochromeRingSnapshotFadeCandidate(Ellipse? ring)
        {
            if (ring == null || ring.Visibility != Visibility.Visible || ring.Opacity <= 0.001 || RootCanvas == null)
            {
                return false;
            }

            return RootCanvas.Children.Contains(ring);
        }

        private static double ResolveMonochromeRingRestoreOpacity(Ellipse? ring, double baseOpacity, double fallbackOpacity)
        {
            if (baseOpacity > 0.0)
            {
                return baseOpacity;
            }

            if (ring != null && ring.Opacity > 0.001)
            {
                return ring.Opacity;
            }

            return fallbackOpacity;
        }

        private void BeginMonochromeRingFadeOut(Ellipse ring)
        {
            var fromOpacity = Math.Max(0.0, ring.Opacity);
            var fadeOut = new DoubleAnimation
            {
                From = fromOpacity,
                To = 0.0,
                Duration = MonochromeRingFadeOutDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ApplyDesiredFrameRate(fadeOut, RingAmbientAnimationFps);
            ring.BeginAnimation(OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
        }

        private void OnMonochromeRingSnapshotTimerTick(object? sender, EventArgs e)
        {
            _monochromeRingSnapshotTimer.Stop();
            if (MonochromeBackdropImage != null && _features.EnableMonochromeBackdrop && !_isEditModeEnabled)
            {
                ApplyMonochromeBackdropSnapshotCore();
            }

            RestoreMonochromeSnapshotRing(_monochromeSnapshotCenterRing, _monochromeSnapshotCenterRestoreOpacity, isCenterRing: true);
            RestoreMonochromeSnapshotRing(_monochromeSnapshotOuterRing, _monochromeSnapshotOuterRestoreOpacity, isCenterRing: false);

            _monochromeSnapshotCenterRing = null;
            _monochromeSnapshotOuterRing = null;
            _monochromeSnapshotCenterRestoreOpacity = 0.0;
            _monochromeSnapshotOuterRestoreOpacity = 0.0;
            _isMonochromeSnapshotTransitionRunning = false;

            if (_hasPendingMonochromeSnapshotRequest)
            {
                _hasPendingMonochromeSnapshotRequest = false;
                if (MonochromeBackdropImage != null && _features.EnableMonochromeBackdrop && !_isEditModeEnabled)
                {
                    ApplyMonochromeBackdropSnapshot();
                }
            }
        }

        private void CancelMonochromeSnapshotTransition(bool restoreRingsImmediately)
        {
            _monochromeRingSnapshotTimer.Stop();
            if (restoreRingsImmediately)
            {
                RestoreMonochromeSnapshotRingImmediate(_monochromeSnapshotCenterRing, _monochromeSnapshotCenterRestoreOpacity, isCenterRing: true);
                RestoreMonochromeSnapshotRingImmediate(_monochromeSnapshotOuterRing, _monochromeSnapshotOuterRestoreOpacity, isCenterRing: false);
            }

            _isMonochromeSnapshotTransitionRunning = false;
            _hasPendingMonochromeSnapshotRequest = false;
            _monochromeSnapshotCenterRing = null;
            _monochromeSnapshotOuterRing = null;
            _monochromeSnapshotCenterRestoreOpacity = 0.0;
            _monochromeSnapshotOuterRestoreOpacity = 0.0;
        }

        private void RestoreMonochromeSnapshotRingImmediate(Ellipse? ring, double targetOpacity, bool isCenterRing)
        {
            if (ring == null || RootCanvas == null || !RootCanvas.Children.Contains(ring))
            {
                return;
            }

            var clampedOpacity = Math.Max(0.0, Math.Min(1.0, targetOpacity));
            ring.BeginAnimation(OpacityProperty, null);
            ring.Opacity = clampedOpacity;
            if (!_enableGradientRingAnimations || _isOdakKaskadiAnimationActive)
            {
                return;
            }

            if (isCenterRing)
            {
                if (!ReferenceEquals(_centerAccentRing, ring))
                {
                    return;
                }

                StartCenterAccentRingAmbientOpacityAnimation(ring);
            }
            else
            {
                if (!ReferenceEquals(_outerAccentRing, ring))
                {
                    return;
                }

                StartOuterAccentRingAmbientOpacityAnimation(ring);
            }
        }

        private void RestoreMonochromeSnapshotRing(Ellipse? ring, double targetOpacity, bool isCenterRing)
        {
            if (ring == null || RootCanvas == null || !RootCanvas.Children.Contains(ring))
            {
                return;
            }

            var clampedOpacity = Math.Max(0.0, Math.Min(1.0, targetOpacity));
            ring.BeginAnimation(OpacityProperty, null);
            ring.Opacity = 0.0;
            if (clampedOpacity <= 0.001)
            {
                return;
            }

            var fadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = clampedOpacity,
                Duration = MonochromeRingFadeInDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ApplyDesiredFrameRate(fadeIn, RingAmbientAnimationFps);
            if (_enableGradientRingAnimations)
            {
                fadeIn.Completed += (_, __) =>
                {
                    if (_isOdakKaskadiAnimationActive)
                    {
                        return;
                    }

                    if (isCenterRing)
                    {
                        if (!ReferenceEquals(_centerAccentRing, ring))
                        {
                            return;
                        }

                        StartCenterAccentRingAmbientOpacityAnimation(ring);
                    }
                    else
                    {
                        if (!ReferenceEquals(_outerAccentRing, ring))
                        {
                            return;
                        }

                        StartOuterAccentRingAmbientOpacityAnimation(ring);
                    }
                };
            }

            ring.BeginAnimation(OpacityProperty, fadeIn, HandoffBehavior.SnapshotAndReplace);
        }

        private void ApplyMonochromeBackdropSnapshotCore()
        {
            if (MonochromeBackdropImage == null)
            {
                return;
            }

            var snapshot = CaptureMonochromeBackdrop();
            if (snapshot == null)
            {
                ClearMonochromeBackdropLayer();
                return;
            }

            MonochromeBackdropImage.Source = snapshot;
            MonochromeBackdropImage.Visibility = Visibility.Visible;
            MonochromeBackdropImage.BeginAnimation(OpacityProperty, null);
            MonochromeBackdropImage.Opacity = 0.0;
            MonochromeBackdropImage.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = MonochromeBackdropStartDelay,
                Duration = MonochromeBackdropFadeDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void ClearMonochromeBackdropLayer()
        {
            if (MonochromeBackdropImage == null)
            {
                return;
            }

            MonochromeBackdropImage.BeginAnimation(OpacityProperty, null);
            MonochromeBackdropImage.Opacity = 1.0;
            MonochromeBackdropImage.Source = null;
            MonochromeBackdropImage.Visibility = Visibility.Collapsed;
        }

        private ImageSource? CaptureMonochromeBackdrop()
        {
            try
            {
                var windowHandle = _hwndSource?.Handle ?? IntPtr.Zero;
                if (windowHandle == IntPtr.Zero)
                {
                    return null;
                }

                if (!GetWindowRect(windowHandle, out var bounds))
                {
                    return null;
                }

                var width = Math.Max(1, bounds.Right - bounds.Left);
                var height = Math.Max(1, bounds.Bottom - bounds.Top);
                using var bitmap = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
                }

                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var colorSource = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    colorSource.Freeze();

                    var grayscale = new FormatConvertedBitmap();
                    grayscale.BeginInit();
                    grayscale.Source = colorSource;
                    grayscale.DestinationFormat = PixelFormats.Gray8;
                    grayscale.EndInit();
                    grayscale.Freeze();
                    return grayscale;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch
            {
                return null;
            }
        }

        private void OnBackdropMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DateTime.UtcNow < _ignoreBackdropMouseUntilUtc)
            {
                e.Handled = true;
                return;
            }

            var position = e.GetPosition(RootCanvas);
            var isBackgroundClick = e.OriginalSource == sender || !IsInteractivePoint(position);
            if (!isBackgroundClick)
            {
                return;
            }

            if (_itemContextMenu != null)
            {
                HideItemContextMenu();
                e.Handled = true;
                return;
            }

            if (IsEditModifierActive())
            {
                e.Handled = true;
                return;
            }

            Dismiss();
            e.Handled = true;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isOdakKaskadiAnimationActive)
            {
                EnsureOdakKaskadiAnimationSettled();
            }

            if (_itemContextMenu == null)
            {
                if (DateTime.UtcNow < _ignoreBackdropMouseUntilUtc || IsEditModifierActive())
                {
                    return;
                }

                var point = e.GetPosition(RootCanvas);
                if (!IsInteractivePoint(point))
                {
                    Dismiss();
                    e.Handled = true;
                }

                return;
            }

            var source = e.OriginalSource as DependencyObject;
            if (IsWithinItemContextMenu(source) || IsWithinUtilityDockButton(source))
            {
                return;
            }

            HideItemContextMenu();
            e.Handled = true;
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isAltGuideActive)
            {
                _isAltGuideActive = false;
                SetTargetingCursorHidden(false);
                HideAltGuideLine();
                e.Handled = true;
                return;
            }

            if (IsWithinItemContextMenu(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
                return;
            }

            var point = e.GetPosition(RootCanvas);
            if (IsWithinUtilityDockButton(e.OriginalSource as DependencyObject))
            {
                ShowUtilityDockContextMenu();
                e.Handled = true;
                return;
            }

            if (IsPointInsideElementBounds(_centerPanel, point) ||
                IsPointInsideElementBounds(_categoryNameStrip, point))
            {
                ShowOverlaySettingsContextMenu(point);
                e.Handled = true;
                return;
            }

            if (_itemContextMenu != null)
            {
                HideItemContextMenu();
                e.Handled = true;
            }
        }

        private bool IsWithinItemContextMenu(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, _itemContextMenu))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmNcHitTest && _isEditModeEnabled)
            {
                handled = true;
                return new IntPtr(HtClient);
            }

            return IntPtr.Zero;
        }

        private static WpfPoint GetPointFromLParam(IntPtr lParam)
        {
            var value = lParam.ToInt64();
            var x = unchecked((short)(value & 0xFFFF));
            var y = unchecked((short)((value >> 16) & 0xFFFF));
            return new WpfPoint(x, y);
        }

        private bool IsInteractivePoint(WpfPoint point)
        {
            if (FindHoverInteractionAtPoint(point) != null)
            {
                return true;
            }

            if (IsPointInsideElementBounds(_centerPanel, point) ||
                IsPointInsideElementBounds(_categoryNameStrip, point) ||
                IsPointInsideElementBounds(_itemContextMenu, point))
            {
                return true;
            }

            var hit = VisualTreeHelper.HitTest(RootCanvas, point);
            if (hit?.VisualHit == null)
            {
                return false;
            }

            DependencyObject? current = hit.VisualHit;
            while (current != null)
            {
                if (_dragInteractions.ContainsKey(current) ||
                    (current is ShapePath path && _segmentInteractions.ContainsKey(path)) ||
                    (current is Border border && _iconInteractions.ContainsKey(border)) ||
                    ReferenceEquals(current, _centerPanel) ||
                    ReferenceEquals(current, _utilityDockButton) ||
                    ReferenceEquals(current, _utilityDockButtonCore) ||
                    ReferenceEquals(current, _itemContextMenu))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsPointInsideElementBounds(FrameworkElement? element, WpfPoint point)
        {
            if (element == null || element.Visibility != Visibility.Visible)
            {
                return false;
            }

            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            if (double.IsNaN(left) || double.IsNaN(top))
            {
                return false;
            }

            return point.X >= left &&
                   point.X <= left + element.ActualWidth &&
                   point.Y >= top &&
                   point.Y <= top + element.ActualHeight;
        }

        private void ShowPageIndicator(int pageNumber)
        {
            var profile = GetLayoutProfile();
            var text = new TextBlock
            {
                Text = "Sayfa " + pageNumber.ToString(),
                FontSize = 21,
                FontWeight = FontWeights.Bold,
                Foreground = GetCachedBrush(Colors.White),
                IsHitTestVisible = false,
                Effect = CreateOptimizedShadowEffect(12, 0.5, Colors.Black, 1)
            };

            text.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            
            Canvas.SetLeft(text, _centerX - text.DesiredSize.Width / 2);
            Canvas.SetTop(text, _centerY + profile.OuterRadius + 44);
            
            RootCanvas.Children.Add(text);

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                BeginTime = TimeSpan.FromSeconds(0.8),
                Duration = TimeSpan.FromSeconds(1.4),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            fadeOut.Completed += (s, e) => 
            {
                RootCanvas.Children.Remove(text);
            };

            var translateAnim = new DoubleAnimation
            {
                From = 15,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = new TranslateTransform(0, 15);
            text.RenderTransform = transform;

            transform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            text.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_pages.Count < 2 || (Keyboard.Modifiers & ModifierKeys.Alt) == 0 || _isPaging)
            {
                return;
            }

            _isPaging = true;
            e.Handled = true;
            var dir = e.Delta > 0 ? 1 : -1;
            PlayUiSound(SoundCue.UiTabSwitch);

            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, __) =>
            {
                _currentPageIndex = dir > 0
                    ? (_currentPageIndex + 1) % _pages.Count
                    : (_currentPageIndex - 1 + _pages.Count) % _pages.Count;

                BuildMenu(_centerX, _centerY, new MenuConfig
                {
                    MenuStyle = _menuStyle,
                    Theme = _theme.Key,
                    OpenAnimationStyle = _openAnimationStyle,
                    Features = _features,
                    Pages = _pages.Select((items, index) => new MenuPageConfig
                    {
                        Title = (index + 1).ToString(),
                        Items = new List<MenuItemConfig>(items)
                    }).ToList()
                });
                
                ShowPageIndicator(_currentPageIndex + 1);

                var fadeIn = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(120),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeIn.Completed += (s2, e2) =>
                {
                    _isPaging = false;
                    UpdateSelectionUnderCursor();
                };

                RootCanvas.BeginAnimation(OpacityProperty, fadeIn);
            };

            RootCanvas.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                UpdateBackdropInteractivity();

                if (_itemContextMenu != null)
                {
                    return;
                }

                if (_isPaging)
                {
                    return;
                }

                if (_pendingDragInteraction != null &&
                    e.LeftButton == MouseButtonState.Pressed &&
                    IsEditModifierActive())
                {
                    var position = e.GetPosition(RootCanvas);
                    if (Math.Abs(position.X - _dragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(position.Y - _dragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance)
                    {
                        var sourceInteraction = _pendingDragInteraction;
                        if (sourceInteraction == null)
                        {
                            return;
                        }

                        var data = new DataObject();
                        data.SetData(InternalDragFormat, new DragPayload(sourceInteraction));
                        var dragSource = _pendingDragSource as DependencyObject ?? (DependencyObject?)BackdropRoot ?? this;
                        try
                        {
                            DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move);
                        }
                        finally
                        {
                            ClearPendingDragState();
                            ClearDragDropPreview();
                            ClearExternalFileDropGhostSlice();
                            ClearPendingDragCategoryOpen(resetSubmenu: true);
                            _dragHoverOpenedCategory = null;
                            _dragHoverOpenedFromDepth = 0;
                            HideSubmenu();
                            ClearCurrentSelection();
                            if (IsVisible)
                            {
                                BuildMenu(_centerX, _centerY, _config);
                            }
                        }

                        return;
                    }
                }
                else
                {
                    if (_pendingDragInteraction != null &&
                        (e.LeftButton != MouseButtonState.Pressed || !IsEditModifierActive()))
                    {
                        ClearPendingDragState();
                    }

                    ClearPendingDragCategoryOpen(resetSubmenu: true);
                }

                if (_isAltGuideActive)
                {
                    UpdateAltGuideVisual();
                }

                UpdateSelectionUnderCursor();
                UpdateEditModeClickThroughState();
            }
            catch (Exception ex)
            {
                LogUiException("Preview mouse move sırasında hata", ex);
            }
        }

        private void OnDragHoverOpenTimerTick(object? sender, EventArgs e)
        {
            _dragHoverOpenTimer.Stop();
            try
            {
                if (_dragHoverCategoryTarget == null || _dragHoverCategoryTarget.Item == null)
                {
                    return;
                }

                var category = _dragHoverCategoryTarget.Item;
                var depth = Math.Max(1, _dragHoverCategoryTarget.Depth + 1);
                if (ReferenceEquals(_dragHoverOpenedCategory, category) &&
                    _dragHoverOpenedFromDepth == depth &&
                    _submenuLayers.ContainsKey(depth))
                {
                    return;
                }

                var nextPath = new List<string>(_dragHoverCategoryTarget.BreadcrumbPath)
                {
                    category.Label
                };

                var ghostSlot = new DragItemInteraction(
                    item: null,
                    ownerItems: category.Children,
                    itemIndex: -1,
                    depth: _dragHoverCategoryTarget.Depth + 1,
                    startAngle: _dragHoverCategoryTarget.StartAngle,
                    endAngle: _dragHoverCategoryTarget.EndAngle,
                    breadcrumbPath: nextPath,
                    isGhostSlot: true,
                    insertIndex: category.Children.Count);

                ShowSubmenu(
                    category.Children,
                    _dragHoverCategoryTarget.StartAngle,
                    _dragHoverCategoryTarget.EndAngle,
                    nextPath,
                    depth,
                    ghostSlot,
                    inheritedColor: GetSubmenuInheritedColor(category, GetItemBaseSegmentColor(category)));
                _dragHoverOpenedCategory = category;
                _dragHoverOpenedFromDepth = depth;
            }
            catch (Exception ex)
            {
                LogUiException("Drag hover submenu acilirken hata", ex);
                ClearPendingDragCategoryOpen(resetSubmenu: false);
            }
        }

        private void BeginAltGuideMode()
        {
            if (_isEditModeEnabled || _itemContextMenu != null || _isPaging)
            {
                return;
            }

            _isAltGuideActive = true;
            SetTargetingCursorHidden(true);
            EnsureAltGuideLine();
            UpdateSelectionUnderCursor();
            UpdateAltGuideVisual();
        }

        private void CompleteAltGuideMode()
        {
            var shouldActivate = _isAltGuideActive && !_isEditModeEnabled && _itemContextMenu == null;
            _isAltGuideActive = false;
            SetTargetingCursorHidden(false);
            HideAltGuideLine();

            if (!shouldActivate)
            {
                return;
            }

            ActivateSelectedInteraction();
        }

        private void ActivateSelectedInteraction()
        {
            if (_selectedInteraction?.Item == null || _selectedInteraction.IsGhostSlot)
            {
                return;
            }

            if (IsCursorOverCenterPanel())
            {
                return;
            }

            var item = _selectedInteraction.Item;
            if (IsCategoryItem(item))
            {
                return;
            }

            if (_activeIconHost != null)
            {
                PlayLaunchFeedback(_activeIconHost);
            }
            RequestLaunchAndDismiss(item.TargetPath, launchDelayMs: 58);
        }

        private bool IsCursorOverCenterPanel()
        {
            if (_centerPanel == null)
            {
                return false;
            }

            var position = GetCursorPositionOnRootCanvas();
            var radius = Math.Min(_centerPanel.Width, _centerPanel.Height) / 2.0;
            var dx = position.X - _centerX;
            var dy = position.Y - _centerY;
            return (dx * dx) + (dy * dy) <= (radius * radius);
        }

        private WpfPoint GetCursorPositionOnRootCanvas()
        {
            if (RootCanvas == null)
            {
                return new WpfPoint();
            }

            var cursor = System.Windows.Forms.Cursor.Position;
            return RootCanvas.PointFromScreen(new WpfPoint(cursor.X, cursor.Y));
        }

        private void PlayUiSound(SoundCue cue)
        {
            _soundManager.Play(cue);
        }

        private static void LogUiException(string context, Exception exception)
        {
            try
            {
                var logPath = ApplicationStorageService.GetDiagnosticLogPath();
                var content =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + context + Environment.NewLine +
                    exception + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(logPath, content);
            }
            catch
            {
            }
        }

        private void EnsureAltGuideLine()
        {
            if (_altGuideVisuals.Count > 0)
            {
                foreach (var visual in _altGuideVisuals)
                {
                    if (!RootCanvas.Children.Contains(visual))
                    {
                        RootCanvas.Children.Add(visual);
                    }
                }
            }
        }

        private void UpdateAltGuideVisual()
        {
            if (!_isAltGuideActive)
            {
                return;
            }

            EnsureAltGuideLine();
            var position = GetCursorPositionOnRootCanvas();
            RebuildAltGuideVisuals(position);
        }

        private void HideAltGuideLine()
        {
            if (_altGuideVisuals.Count == 0)
            {
                return;
            }

            foreach (var visual in _altGuideVisuals)
            {
                RootCanvas.Children.Remove(visual);
            }

            _altGuideVisuals.Clear();
        }

        private void RebuildAltGuideVisuals(WpfPoint target)
        {
            HideAltGuideLine();

            var dx = target.X - _centerX;
            var dy = target.Y - _centerY;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance < 6)
            {
                return;
            }

            var angle = Math.Atan2(dy, dx);
            var accent = BlendColors(_theme.SegmentActiveColor, Colors.White, 0.18);
            var glow = BlendColors(_theme.CenterBorderColor, _theme.TitleColor, 0.35);
            var soft = BlendColors(_theme.SegmentColor, _theme.SubtitleColor, 0.28);

            switch (_targetingModeStyle)
            {
                case "DottedFlow":
                    BuildDottedFlowGuide(target, distance, angle, glow, accent);
                    break;
                case "GlowOrb":
                    BuildGlowOrbGuide(target, distance, angle, glow, accent);
                    break;
                case "LightCone":
                    BuildLightConeGuide(target, distance, angle, glow, accent, soft);
                    break;
                case "ParticleTrail":
                    BuildParticleTrailGuide(target, distance, angle, glow, accent);
                    break;
                case "TargetArrow":
                    BuildTargetArrowGuide(target, distance, angle, glow, accent);
                    break;
                default:
                    BuildLaserLineGuide(target, glow, accent);
                    break;
            }
        }

        private void BuildLaserLineGuide(WpfPoint target, System.Windows.Media.Color glow, System.Windows.Media.Color accent)
        {
            var stroke = GetCachedLinearGradientBrush(
                new WpfPoint(0, 0.5),
                new WpfPoint(1, 0.5),
                (System.Windows.Media.Color.FromArgb(220, glow.R, glow.G, glow.B), 0.0),
                (System.Windows.Media.Color.FromArgb(250, accent.R, accent.G, accent.B), 0.55),
                (System.Windows.Media.Color.FromArgb(255, 255, 255, 255), 1.0));

            var line = new Line
            {
                X1 = _centerX,
                Y1 = _centerY,
                X2 = target.X,
                Y2 = target.Y,
                IsHitTestVisible = false,
                Stroke = stroke,
                StrokeThickness = 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                SnapsToDevicePixels = true,
                Opacity = 0.96,
                Effect = CreateOptimizedShadowEffect(10, 0.18, accent)
            };

            AddAltGuideVisual(line);
        }

        private void BuildDottedFlowGuide(WpfPoint target, double distance, double angle, System.Windows.Media.Color glow, System.Windows.Media.Color accent)
        {
            var dotCount = Math.Max(6, (int)(distance / 26));
            for (var i = 0; i < dotCount; i++)
            {
                var t = (i + 1.0) / dotCount;
                var size = 3.5 + (t * 3.8);
                var point = new WpfPoint(
                    _centerX + Math.Cos(angle) * distance * t,
                    _centerY + Math.Sin(angle) * distance * t);
                var color = BlendColors(glow, accent, t);
                var dot = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb((byte)(120 + (t * 120)), color.R, color.G, color.B)),
                    IsHitTestVisible = false,
                    Effect = CreateOptimizedShadowEffect(8, 0.14, color)
                };
                Canvas.SetLeft(dot, point.X - size / 2);
                Canvas.SetTop(dot, point.Y - size / 2);
                AddAltGuideVisual(dot);
            }
        }

        private void BuildGlowOrbGuide(WpfPoint target, double distance, double angle, System.Windows.Media.Color glow, System.Windows.Media.Color accent)
        {
            BuildLaserLineGuide(target, glow, accent);

            var orbGlow = new Ellipse
            {
                Width = 28,
                Height = 28,
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(74, accent.R, accent.G, accent.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(orbGlow, target.X - 14);
            Canvas.SetTop(orbGlow, target.Y - 14);
            AddAltGuideVisual(orbGlow);

            var orb = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(250, 255, 255, 255)),
                Stroke = GetCachedBrush(accent),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(orb, target.X - 6);
            Canvas.SetTop(orb, target.Y - 6);
            AddAltGuideVisual(orb);
        }

        private void BuildLightConeGuide(WpfPoint target, double distance, double angle, System.Windows.Media.Color glow, System.Windows.Media.Color accent, System.Windows.Media.Color soft)
        {
            var width = Math.Max(10, Math.Min(26, distance * 0.08));
            var centerHalfWidth = width * 0.18;
            var targetHalfWidth = width;
            var normalX = -Math.Sin(angle);
            var normalY = Math.Cos(angle);
            var p1 = new WpfPoint(_centerX + normalX * centerHalfWidth, _centerY + normalY * centerHalfWidth);
            var p2 = new WpfPoint(_centerX - normalX * centerHalfWidth, _centerY - normalY * centerHalfWidth);
            var p3 = new WpfPoint(target.X - normalX * targetHalfWidth, target.Y - normalY * targetHalfWidth);
            var p4 = new WpfPoint(target.X + normalX * targetHalfWidth, target.Y + normalY * targetHalfWidth);
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var tipCurveControl = new WpfPoint(
                target.X + (directionX * Math.Max(6.0, targetHalfWidth * 0.9)),
                target.Y + (directionY * Math.Max(6.0, targetHalfWidth * 0.9)));

            var fill = GetCachedLinearGradientBrush(
                new WpfPoint(0, 0.5),
                new WpfPoint(1, 0.5),
                (System.Windows.Media.Color.FromArgb(24, glow.R, glow.G, glow.B), 0.0),
                (System.Windows.Media.Color.FromArgb(80, soft.R, soft.G, soft.B), 0.5),
                (System.Windows.Media.Color.FromArgb(120, accent.R, accent.G, accent.B), 1.0));

            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = p1,
                IsClosed = true,
                IsFilled = true
            };
            figure.Segments.Add(new LineSegment(p2, true));
            figure.Segments.Add(new LineSegment(p3, true));
            figure.Segments.Add(new QuadraticBezierSegment(tipCurveControl, p4, true));
            geometry.Figures.Add(figure);

            var cone = new ShapePath
            {
                Data = geometry,
                Fill = fill,
                Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(118, accent.R, accent.G, accent.B)),
                StrokeThickness = 1.2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            AddAltGuideVisual(cone);
        }

        private void BuildParticleTrailGuide(WpfPoint target, double distance, double angle, System.Windows.Media.Color glow, System.Windows.Media.Color accent)
        {
            var particleCount = Math.Max(10, (int)(distance / 18));
            for (var i = 0; i < particleCount; i++)
            {
                var t = (i + 1.0) / particleCount;
                var spread = Math.Sin((i + 1) * 0.9) * 6.0 * (1.0 - t);
                var px = _centerX + Math.Cos(angle) * distance * t + (-Math.Sin(angle) * spread);
                var py = _centerY + Math.Sin(angle) * distance * t + (Math.Cos(angle) * spread);
                var size = 2.0 + (t * 4.4);
                var color = BlendColors(glow, accent, t);
                var particle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb((byte)(90 + (t * 130)), color.R, color.G, color.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(particle, px - size / 2);
                Canvas.SetTop(particle, py - size / 2);
                AddAltGuideVisual(particle);
            }
        }

        private void BuildTargetArrowGuide(WpfPoint target, double distance, double angle, System.Windows.Media.Color glow, System.Windows.Media.Color accent)
        {
            BuildLaserLineGuide(target, glow, accent);

            var arrowLength = Math.Max(14, Math.Min(26, distance * 0.12));
            var arrowWidth = arrowLength * 0.56;
            var back = new WpfPoint(
                target.X - Math.Cos(angle) * arrowLength,
                target.Y - Math.Sin(angle) * arrowLength);
            var normalX = -Math.Sin(angle);
            var normalY = Math.Cos(angle);
            var left = new WpfPoint(back.X + normalX * arrowWidth * 0.5, back.Y + normalY * arrowWidth * 0.5);
            var right = new WpfPoint(back.X - normalX * arrowWidth * 0.5, back.Y - normalY * arrowWidth * 0.5);

            var arrow = new Polygon
            {
                Points = new PointCollection { target, left, right },
                Fill = GetCachedBrush(System.Windows.Media.Color.FromArgb(245, accent.R, accent.G, accent.B)),
                Stroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            };
            AddAltGuideVisual(arrow);
        }

        private void AddAltGuideVisual(UIElement visual)
        {
            _altGuideVisuals.Add(visual);
            RootCanvas.Children.Add(visual);
        }

        private void SetTargetingCursorHidden(bool hidden)
        {
            if (hidden)
            {
                if (_isTargetingCursorHidden)
                {
                    return;
                }

                Mouse.OverrideCursor = Cursors.None;
                _isTargetingCursorHidden = true;
                return;
            }

            if (!_isTargetingCursorHidden)
            {
                return;
            }

            Mouse.OverrideCursor = null;
            _isTargetingCursorHidden = false;
        }

        private bool IsTargetingShortcutPressed()
        {
            if (!AreTargetingModifiersPressed())
            {
                return false;
            }

            return IsTargetingTriggerPressed();
        }

        private bool AreTargetingModifiersPressed()
        {
            var trigger = _targetingShortcut.Trigger?.Trim().ToUpperInvariant() ?? string.Empty;
            var ctrl = IsVirtualKeyPressed(VkControl);
            var alt = IsVirtualKeyPressed(VkMenu);
            var shift = IsVirtualKeyPressed(VkShift);
            var win = IsVirtualKeyPressed(VkLWin) || IsVirtualKeyPressed(VkRWin);

            var expectedCtrl = trigger == "CTRL" ? ctrl : _targetingShortcut.Ctrl;
            var expectedAlt = trigger == "ALT" ? alt : _targetingShortcut.Alt;
            var expectedShift = trigger == "SHIFT" ? shift : _targetingShortcut.Shift;
            var expectedWin = trigger == "WIN" ? win : _targetingShortcut.Win;

            return ctrl == expectedCtrl &&
                   alt == expectedAlt &&
                   shift == expectedShift &&
                   win == expectedWin;
        }

        private bool IsTargetingTriggerPressed()
        {
            var trigger = _targetingShortcut.Trigger;
            if (string.IsNullOrWhiteSpace(trigger))
            {
                return false;
            }

            return trigger.ToUpperInvariant() switch
            {
                "CTRL" => IsVirtualKeyPressed(VkControl),
                "ALT" => IsVirtualKeyPressed(VkMenu),
                "SHIFT" => IsVirtualKeyPressed(VkShift),
                "WIN" => IsVirtualKeyPressed(VkLWin) || IsVirtualKeyPressed(VkRWin),
                _ => IsVirtualKeyPressed(ResolveVirtualKey(trigger))
            };
        }

        private static bool IsVirtualKeyPressed(int virtualKey)
        {
            return virtualKey > 0 && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static int ResolveVirtualKey(string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger))
            {
                return 0;
            }

            var normalized = trigger.Trim().ToUpperInvariant();
            if (normalized.Length == 1)
            {
                var ch = normalized[0];
                if (ch >= 'A' && ch <= 'Z')
                {
                    return ch;
                }

                if (ch >= '0' && ch <= '9')
                {
                    return ch;
                }
            }

            if (normalized.StartsWith("F", StringComparison.Ordinal) &&
                int.TryParse(normalized.Substring(1), out var functionIndex) &&
                functionIndex >= 1 && functionIndex <= 12)
            {
                return 0x6F + functionIndex;
            }

            if (normalized.StartsWith("D", StringComparison.Ordinal) &&
                normalized.Length == 2 &&
                normalized[1] >= '0' &&
                normalized[1] <= '9')
            {
                return normalized[1];
            }

            return 0;
        }

        private void SchedulePendingDragCategoryOpen(DragItemInteraction target)
        {
            if (target.Item == null || !IsCategoryItem(target.Item) || target.IsGhostSlot)
            {
                ClearPendingDragCategoryOpen(resetSubmenu: false);
                return;
            }

            var isSamePendingTarget = ReferenceEquals(_dragHoverCategoryTarget?.Item, target.Item) &&
                ReferenceEquals(_dragHoverCategoryTarget?.OwnerItems, target.OwnerItems) &&
                _dragHoverCategoryTarget?.ItemIndex == target.ItemIndex;
            var isAlreadyOpen = IsDragHoverCategorySubmenuOpen(target);

            if (isSamePendingTarget && (_dragHoverOpenTimer.IsEnabled || isAlreadyOpen))
            {
                return;
            }

            _dragHoverCategoryTarget = target;
            _dragHoverOpenTimer.Stop();
            if (isAlreadyOpen)
            {
                return;
            }

            _dragHoverOpenTimer.Start();
        }

        private void ClearPendingDragCategoryOpen(bool resetSubmenu)
        {
            _dragHoverOpenTimer.Stop();
            _dragHoverCategoryTarget = null;
            if (resetSubmenu)
            {
                if (_dragHoverOpenedFromDepth > 0)
                {
                    HideSubmenu(_dragHoverOpenedFromDepth);
                }

                _dragHoverOpenedCategory = null;
                _dragHoverOpenedFromDepth = 0;
            }
        }

        private bool ShouldKeepDragHoverSubmenuOpen(DragItemInteraction? target)
        {
            if (_dragHoverOpenedCategory == null || _dragHoverOpenedFromDepth <= 0 || target == null)
            {
                return false;
            }

            if (target.Item != null && ReferenceEquals(target.Item, _dragHoverOpenedCategory))
            {
                return true;
            }

            return ContainsOwnerList(_dragHoverOpenedCategory, target.OwnerItems);
        }

        private bool IsDragHoverCategorySubmenuOpen(DragItemInteraction target)
        {
            if (target.Item == null || _dragHoverOpenedCategory == null || _dragHoverOpenedFromDepth <= 0)
            {
                return false;
            }

            if (!ReferenceEquals(_dragHoverOpenedCategory, target.Item))
            {
                return false;
            }

            var expectedDepth = Math.Max(1, target.Depth + 1);
            return _dragHoverOpenedFromDepth == expectedDepth && _submenuLayers.ContainsKey(expectedDepth);
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            UpdateBackdropInteractivity();
            HideItemContextMenu();

            var isInternalDrag = e.Data.GetDataPresent(InternalDragFormat);
            var isFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
            if (!isInternalDrag && !isFileDrop)
            {
                ClearDragDropPreview();
                ClearExternalFileDropGhostSlice();
                ClearPendingDragCategoryOpen(resetSubmenu: true);
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (isInternalDrag && !IsEditModifierActive())
            {
                ClearDragDropPreview();
                ClearExternalFileDropGhostSlice();
                ClearPendingDragCategoryOpen(resetSubmenu: true);
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var pointerPosition = GetDragPointerPosition(e);
            var targetInteraction = FindDragInteraction(pointerPosition) ?? FindDragInteraction(GetCursorPositionOnRootCanvas());
            UpdateDragDropPreview(
                targetInteraction,
                allowPreview: isFileDrop || IsEditModifierActive(),
                showExternalGhostSlice: isFileDrop || isInternalDrag);

            DragItemInteraction? categoryHoverTarget = null;
            if (targetInteraction?.Item != null &&
                !targetInteraction.IsGhostSlot &&
                IsCategoryItem(targetInteraction.Item))
            {
                categoryHoverTarget = targetInteraction;
            }
            else
            {
                var categoryHover = FindCategoryHoverInteractionAtPoint(pointerPosition) ??
                    FindCategoryHoverInteractionAtPoint(GetCursorPositionOnRootCanvas());
                if (categoryHover?.Item != null)
                {
                    categoryHoverTarget = new DragItemInteraction(
                        categoryHover.Item,
                        categoryHover.OwnerItems,
                        categoryHover.ItemIndex,
                        categoryHover.Depth,
                        categoryHover.StartAngle,
                        categoryHover.EndAngle,
                        categoryHover.BreadcrumbPath,
                        isGhostSlot: false,
                        insertIndex: categoryHover.ItemIndex);
                }
            }

            if (categoryHoverTarget != null)
            {
                SchedulePendingDragCategoryOpen(categoryHoverTarget);
            }
            else
            {
                ClearPendingDragCategoryOpen(resetSubmenu: !ShouldKeepDragHoverSubmenuOpen(targetInteraction));
            }

            if (e.Data.GetDataPresent(InternalDragFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void OnPreviewDragLeave(object sender, DragEventArgs e)
        {
            ClearDragDropPreview();
            ClearExternalFileDropGhostSlice();
            ClearPendingDragCategoryOpen(resetSubmenu: true);
        }

        private void OnPreviewDrop(object sender, DragEventArgs e)
        {
            UpdateBackdropInteractivity();
            ClearPendingDragState();
            ClearDragDropPreview();
            ClearExternalFileDropGhostSlice();
            ClearPendingDragCategoryOpen(resetSubmenu: true);
            HideItemContextMenu();

            var isInternalDrag = e.Data.GetDataPresent(InternalDragFormat);
            if (isInternalDrag && !IsEditModifierActive())
            {
                return;
            }

            if (isInternalDrag)
            {
                var payload = e.Data.GetData(InternalDragFormat) as DragPayload;
                var pointerPosition = GetDragPointerPosition(e);
                var targetInteraction = FindDragInteraction(pointerPosition) ?? FindDragInteraction(GetCursorPositionOnRootCanvas());
                if (payload != null && targetInteraction != null)
                {
                    MoveOrSwapItems(payload.Source, targetInteraction);
                }
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var pointerPosition = GetDragPointerPosition(e);
                var targetInteraction = FindDragInteraction(pointerPosition) ?? FindDragInteraction(GetCursorPositionOnRootCanvas());
                var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddDroppedItems(filePaths, targetInteraction);
                e.Handled = true;
            }
        }

        private WpfPoint GetDragPointerPosition(DragEventArgs e)
        {
            var cursorPosition = GetCursorPositionOnRootCanvas();
            if (RootCanvas == null)
            {
                return cursorPosition;
            }

            var eventPosition = e.GetPosition(RootCanvas);
            if (double.IsNaN(eventPosition.X) || double.IsNaN(eventPosition.Y))
            {
                return cursorPosition;
            }

            var dx = eventPosition.X - cursorPosition.X;
            var dy = eventPosition.Y - cursorPosition.Y;
            return ((dx * dx) + (dy * dy)) > 196
                ? cursorPosition
                : eventPosition;
        }

        private DragItemInteraction? FindDragInteraction(WpfPoint position)
        {
            var hoverInteraction = FindHoverInteractionAtPoint(position);
            if (hoverInteraction == null)
            {
                return null;
            }

            var insertIndex = hoverInteraction.IsGhostSlot
                ? hoverInteraction.OwnerItems.Count
                : ResolveInsertIndexForHover(hoverInteraction, position);

            return new DragItemInteraction(
                hoverInteraction.Item,
                hoverInteraction.OwnerItems,
                hoverInteraction.ItemIndex,
                hoverInteraction.Depth,
                hoverInteraction.StartAngle,
                hoverInteraction.EndAngle,
                hoverInteraction.BreadcrumbPath,
                hoverInteraction.IsGhostSlot,
                insertIndex);
        }

        private int ResolveInsertIndexForHover(HoverInteraction interaction, WpfPoint pointerPosition)
        {
            var defaultInsertIndex = interaction.ItemIndex + 1;
            var dx = pointerPosition.X - _centerX;
            var dy = pointerPosition.Y - _centerY;
            if (interaction.ItemIndex < 0 ||
                interaction.HitShape != HoverHitShape.RingSector ||
                Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            {
                return Math.Max(0, Math.Min(defaultInsertIndex, interaction.OwnerItems.Count));
            }

            var pointerAngle = NormalizeAngle(ToDegrees(Math.Atan2(dy, dx)));
            if (interaction.Item != null && IsCategoryItem(interaction.Item))
            {
                var progress = GetAngleProgressWithinSweep(pointerAngle, interaction.StartAngle, interaction.EndAngle);
                var insertIndex = progress >= 0.74
                    ? interaction.ItemIndex + 1
                    : interaction.ItemIndex;
                return Math.Max(0, Math.Min(insertIndex, interaction.OwnerItems.Count));
            }

            var distanceToStart = GetSmallestAngleDelta(pointerAngle, interaction.StartAngle);
            var distanceToEnd = GetSmallestAngleDelta(pointerAngle, interaction.EndAngle);
            var preferBefore = distanceToStart <= distanceToEnd;
            var resolvedInsertIndex = preferBefore ? interaction.ItemIndex : interaction.ItemIndex + 1;
            return Math.Max(0, Math.Min(resolvedInsertIndex, interaction.OwnerItems.Count));
        }

        private static bool IsCategoryContainerDropTarget(DragItemInteraction target)
        {
            if (target.IsGhostSlot || target.Item == null || !IsCategoryItem(target.Item))
            {
                return false;
            }

            return target.InsertIndex <= target.ItemIndex;
        }

        private static double GetAngleProgressWithinSweep(double angle, double startAngle, double endAngle)
        {
            var normalizedStart = NormalizeAngle(startAngle);
            var normalizedEnd = NormalizeAngle(endAngle);
            var normalizedAngle = NormalizeAngle(angle);
            var sweep = normalizedEnd - normalizedStart;
            if (sweep < 0)
            {
                sweep += 360.0;
            }

            if (sweep <= 0.001)
            {
                return 0.5;
            }

            var offset = normalizedAngle - normalizedStart;
            if (offset < 0)
            {
                offset += 360.0;
            }

            return Math.Max(0.0, Math.Min(1.0, offset / sweep));
        }

        private void UpdateDragDropPreview(DragItemInteraction? target, bool allowPreview, bool showExternalGhostSlice)
        {
            if (!allowPreview || target == null)
            {
                ClearDragDropPreview();
                ClearExternalFileDropGhostSlice();
                return;
            }

            var previewInteraction = FindHoverInteractionForDragTarget(target);
            if (previewInteraction == null)
            {
                ClearDragDropPreview();
                ClearExternalFileDropGhostSlice();
                return;
            }

            if (_dragPreviewInteraction != null &&
                ReferenceEquals(_dragPreviewInteraction.Item, previewInteraction.Item) &&
                ReferenceEquals(_dragPreviewInteraction.OwnerItems, previewInteraction.OwnerItems) &&
                _dragPreviewInteraction.ItemIndex == previewInteraction.ItemIndex &&
                _dragPreviewInteraction.Depth == previewInteraction.Depth &&
                _dragPreviewInteraction.IsGhostSlot == previewInteraction.IsGhostSlot &&
                (!showExternalGhostSlice || _externalFileDropGhostInsertIndex == target.InsertIndex))
            {
                return;
            }

            _dragPreviewInteraction = previewInteraction;

            var profile = GetLayoutProfile();
            var previewAngle = GetAngleMidpoint(previewInteraction.StartAngle, previewInteraction.EndAngle);
            var previewRadius = previewInteraction.OuterRadius;
            var previewColor = previewInteraction.Item != null
                ? GetItemActiveSegmentColor(previewInteraction.Item)
                : previewInteraction.AccentColor;

            if (IsCategoryContainerDropTarget(target))
            {
                var nextDepth = previewInteraction.Depth <= 0
                    ? 1
                    : previewInteraction.Depth + 1;
                previewRadius = Math.Max(previewRadius, GetSubmenuLayerOuterRadius(profile, nextDepth));
            }
            else if (previewInteraction.Depth == 0 && previewInteraction.Item != null)
            {
                previewRadius = Math.Max(previewRadius, GetRootHoverOrbitRadius(previewInteraction.Item, profile));
            }

            ShowHoverOrbitGuide(previewRadius, previewAngle, previewColor);
            if (showExternalGhostSlice)
            {
                ShowExternalFileDropGhostSlice(target, previewInteraction);
            }
            else
            {
                ClearExternalFileDropGhostSlice();
            }
        }

        private void ClearDragDropPreview()
        {
            if (_dragPreviewInteraction == null)
            {
                return;
            }

            _dragPreviewInteraction = null;
            RefreshHoverOrbitGuide();
        }

        private void ShowExternalFileDropGhostSlice(DragItemInteraction target, HoverInteraction targetInteraction)
        {
            if (ReferenceEquals(_externalFileDropGhostTarget, targetInteraction) &&
                _externalFileDropGhostInsertIndex == target.InsertIndex &&
                _externalFileDropGhostSlice != null &&
                RootCanvas.Children.Contains(_externalFileDropGhostSlice) &&
                _externalFileDropGhostBadge != null &&
                RootCanvas.Children.Contains(_externalFileDropGhostBadge))
            {
                return;
            }

            if (targetInteraction.Segment.Data is not Geometry segmentGeometry)
            {
                ClearExternalFileDropGhostSlice();
                return;
            }

            Geometry geometry = segmentGeometry.CloneCurrentValue();
            var badgeCenter = GetElementCenter(targetInteraction.IconHost);
            var shouldCoverCategorySlice = IsCategoryContainerDropTarget(target);
            if (!shouldCoverCategorySlice &&
                TryBuildHalfSliceDropSlotGeometry(target, targetInteraction, out var slotGeometry, out var slotBadgeCenter))
            {
                geometry = slotGeometry;
                badgeCenter = slotBadgeCenter;
            }

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            var accent = targetInteraction.Item != null
                ? GetItemActiveSegmentColor(targetInteraction.Item)
                : targetInteraction.AccentColor;
            var fillColor = System.Windows.Media.Color.FromArgb(106, accent.R, accent.G, accent.B);
            var strokeColor = System.Windows.Media.Color.FromArgb(244, accent.R, accent.G, accent.B);
            var glowColor = BlendColors(accent, Colors.White, 0.22);

            if (_externalFileDropGhostSlice == null)
            {
                _externalFileDropGhostSlice = new ShapePath
                {
                    IsHitTestVisible = false,
                    StrokeDashArray = MainGhostDashArray,
                    StrokeThickness = Math.Max(1.9, GetLayoutProfile().SegmentStrokeThickness + 0.92),
                    Effect = CreateOptimizedShadowEffect(11, 0.26, glowColor),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    CacheMode = new BitmapCache()
                };
            }
            else
            {
                _externalFileDropGhostSlice.BeginAnimation(UIElement.OpacityProperty, null);
            }

            _externalFileDropGhostSlice.Data = geometry;
            _externalFileDropGhostSlice.Fill = GetCachedBrush(fillColor);
            _externalFileDropGhostSlice.Stroke = GetCachedBrush(strokeColor);
            _externalFileDropGhostSlice.Opacity = 0.97;
            Panel.SetZIndex(_externalFileDropGhostSlice, 270);

            if (!RootCanvas.Children.Contains(_externalFileDropGhostSlice))
            {
                RootCanvas.Children.Add(_externalFileDropGhostSlice);
            }

            var badgeSize = Math.Clamp(targetInteraction.IconHost.Width * 0.7, 22, 34);
            if (_externalFileDropGhostBadge == null)
            {
                var plusText = new TextBlock
                {
                    Text = "+",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                    FontSize = Math.Clamp(badgeSize * 0.72, 14, 24),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                _externalFileDropGhostBadgeScale = new ScaleTransform(1.0, 1.0);
                _externalFileDropGhostBadge = new Border
                {
                    Width = badgeSize,
                    Height = badgeSize,
                    CornerRadius = new CornerRadius(badgeSize / 2.0),
                    BorderThickness = new Thickness(1.4),
                    Child = plusText,
                    RenderTransformOrigin = new WpfPoint(0.5, 0.5),
                    RenderTransform = _externalFileDropGhostBadgeScale,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    CacheMode = new BitmapCache()
                };
            }
            else
            {
                _externalFileDropGhostBadge.BeginAnimation(UIElement.OpacityProperty, null);
                _externalFileDropGhostBadgeScale?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                _externalFileDropGhostBadgeScale?.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            }

            var badgeBackground = BlendColors(System.Windows.Media.Color.FromArgb(228, 14, 18, 24), accent, 0.2);
            var badgeBorder = BlendColors(accent, Colors.White, 0.2);
            _externalFileDropGhostBadge.Background = GetCachedBrush(badgeBackground);
            _externalFileDropGhostBadge.BorderBrush = GetCachedBrush(System.Windows.Media.Color.FromArgb(236, badgeBorder.R, badgeBorder.G, badgeBorder.B));
            _externalFileDropGhostBadge.Effect = CreateOptimizedShadowEffect(10, 0.22, glowColor);
            _externalFileDropGhostBadge.Opacity = 1.0;

            if (_externalFileDropGhostBadge.Child is TextBlock plus)
            {
                plus.Foreground = GetCachedBrush(System.Windows.Media.Color.FromArgb(246, 246, 250, 255));
            }

            Canvas.SetLeft(_externalFileDropGhostBadge, badgeCenter.X - (_externalFileDropGhostBadge.Width / 2.0));
            Canvas.SetTop(_externalFileDropGhostBadge, badgeCenter.Y - (_externalFileDropGhostBadge.Height / 2.0));
            Panel.SetZIndex(_externalFileDropGhostBadge, 272);
            if (!RootCanvas.Children.Contains(_externalFileDropGhostBadge))
            {
                RootCanvas.Children.Add(_externalFileDropGhostBadge);
            }

            var pulseEase = new SineEase { EasingMode = EasingMode.EaseInOut };
            _externalFileDropGhostBadge.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 0.76,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(640),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = pulseEase
            });
            if (_externalFileDropGhostBadgeScale != null)
            {
                var scalePulse = new DoubleAnimation
                {
                    From = 0.94,
                    To = 1.06,
                    Duration = TimeSpan.FromMilliseconds(640),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = pulseEase
                };
                _externalFileDropGhostBadgeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scalePulse);
                _externalFileDropGhostBadgeScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scalePulse);
            }

            _externalFileDropGhostTarget = targetInteraction;
            _externalFileDropGhostInsertIndex = target.InsertIndex;
        }

        private bool TryBuildHalfSliceDropSlotGeometry(
            DragItemInteraction target,
            HoverInteraction targetInteraction,
            out Geometry slotGeometry,
            out WpfPoint badgeCenter)
        {
            slotGeometry = null!;
            badgeCenter = new WpfPoint();
            if (targetInteraction.HitShape != HoverHitShape.RingSector)
            {
                return false;
            }

            var siblings = _hoverInteractionEntries
                .Where(interaction =>
                    !interaction.IsGhostSlot &&
                    interaction.HitShape == HoverHitShape.RingSector &&
                    interaction.Depth == target.Depth &&
                    ReferenceEquals(interaction.OwnerItems, target.OwnerItems))
                .OrderBy(interaction => interaction.ItemIndex)
                .ToList();

            if (siblings.Count == 0)
            {
                return false;
            }

            var insertIndex = Math.Max(0, Math.Min(target.InsertIndex, siblings.Count));
            var left = insertIndex > 0 ? siblings[insertIndex - 1] : null;
            var right = insertIndex < siblings.Count ? siblings[insertIndex] : null;

            var boundaryAngle = left != null && right != null
                ? NormalizeAngle(GetAngleMidpoint(left.EndAngle, right.StartAngle))
                : right != null
                    ? NormalizeAngle(right.StartAngle)
                    : left != null
                        ? NormalizeAngle(left.EndAngle)
                        : NormalizeAngle(GetAngleMidpoint(targetInteraction.StartAngle, targetInteraction.EndAngle));

            var leftSweep = left != null ? GetInteractionSweep(left) : 0;
            var rightSweep = right != null ? GetInteractionSweep(right) : 0;
            var baseSweep = left != null && right != null
                ? Math.Min(leftSweep, rightSweep)
                : Math.Max(leftSweep, rightSweep);
            if (baseSweep <= 0.01)
            {
                baseSweep = GetInteractionSweep(targetInteraction);
            }

            var slotSweep = Math.Clamp(baseSweep * 0.5, 7.5, 52.0);
            var startAngle = boundaryAngle - (slotSweep / 2.0);
            var endAngle = boundaryAngle + (slotSweep / 2.0);
            var innerRadius = Math.Max(8.0, targetInteraction.InnerRadius + 1.2);
            var outerRadius = Math.Max(innerRadius + 4.0, targetInteraction.OuterRadius - 1.2);

            slotGeometry = CreateRingSegmentGeometry(_centerX, _centerY, innerRadius, outerRadius, startAngle, endAngle);
            badgeCenter = PointOnCircle(_centerX, _centerY, (innerRadius + outerRadius) * 0.5, boundaryAngle);
            return true;
        }

        private static double GetInteractionSweep(HoverInteraction interaction)
        {
            var normalizedStart = NormalizeAngle(interaction.StartAngle);
            var normalizedEnd = NormalizeAngle(interaction.EndAngle);
            var sweep = normalizedEnd - normalizedStart;
            if (sweep < 0)
            {
                sweep += 360.0;
            }

            return sweep;
        }

        private void ClearExternalFileDropGhostSlice()
        {
            _externalFileDropGhostTarget = null;
            _externalFileDropGhostInsertIndex = -1;
            if (_externalFileDropGhostSlice == null)
            {
                if (_externalFileDropGhostBadge != null)
                {
                    RootCanvas.Children.Remove(_externalFileDropGhostBadge);
                }

                return;
            }

            RootCanvas.Children.Remove(_externalFileDropGhostSlice);
            if (_externalFileDropGhostBadge != null)
            {
                RootCanvas.Children.Remove(_externalFileDropGhostBadge);
            }
        }

        private HoverInteraction? FindHoverInteractionForDragTarget(DragItemInteraction target)
        {
            HoverInteraction? bestGhostMatch = null;
            var bestGhostAngleDelta = double.MaxValue;

            foreach (var interaction in _hoverInteractionEntries)
            {
                if (!ReferenceEquals(interaction.OwnerItems, target.OwnerItems) ||
                    interaction.Depth != target.Depth ||
                    interaction.IsGhostSlot != target.IsGhostSlot)
                {
                    continue;
                }

                if (!target.IsGhostSlot)
                {
                    if ((ReferenceEquals(interaction.Item, target.Item) && interaction.ItemIndex == target.ItemIndex) ||
                        interaction.ItemIndex == target.ItemIndex)
                    {
                        return interaction;
                    }

                    continue;
                }

                var interactionAngle = GetAngleMidpoint(interaction.StartAngle, interaction.EndAngle);
                var targetAngle = GetAngleMidpoint(target.StartAngle, target.EndAngle);
                var angleDelta = GetSmallestAngleDelta(interactionAngle, targetAngle);
                if (angleDelta < bestGhostAngleDelta)
                {
                    bestGhostAngleDelta = angleDelta;
                    bestGhostMatch = interaction;
                }
            }

            return bestGhostMatch;
        }

        private void MoveOrSwapItems(DragItemInteraction source, DragItemInteraction target)
        {
            if (!target.IsGhostSlot && ReferenceEquals(source.Item, target.Item))
            {
                return;
            }

            if (IsCategoryContainerDropTarget(target))
            {
                var categoryTargetItem = target.Item;
                if (categoryTargetItem == null)
                {
                    return;
                }

                MoveItemToInsertSlot(source, categoryTargetItem.Children, categoryTargetItem.Children.Count);
            }
            else
            {
                var insertIndex = target.InsertIndex;
                MoveItemToInsertSlot(source, target.OwnerItems, insertIndex);
            }

            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.MenuDrop);
        }

        private static void SwapItems(List<MenuItemConfig> items, int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= items.Count || targetIndex < 0 || targetIndex >= items.Count || sourceIndex == targetIndex)
            {
                return;
            }

            var temp = items[sourceIndex];
            items[sourceIndex] = items[targetIndex];
            items[targetIndex] = temp;
        }

        private static void MoveItemBetweenLists(DragItemInteraction source, DragItemInteraction target)
        {
            if (source.Item == null || source.ItemIndex < 0 || source.ItemIndex >= source.OwnerItems.Count)
            {
                return;
            }

            if (target.Item == null)
            {
                return;
            }

            if (ContainsOwnerList(source.Item, target.OwnerItems))
            {
                return;
            }

            source.OwnerItems.RemoveAt(source.ItemIndex);
            var insertIndex = Math.Max(0, Math.Min(target.ItemIndex + 1, target.OwnerItems.Count));
            target.OwnerItems.Insert(insertIndex, source.Item);
        }

        private static void MoveItemToInsertSlot(DragItemInteraction source, List<MenuItemConfig> targetItems, int insertIndex)
        {
            if (source.Item == null || source.ItemIndex < 0 || source.ItemIndex >= source.OwnerItems.Count)
            {
                return;
            }

            if (ContainsOwnerList(source.Item, targetItems))
            {
                return;
            }

            source.OwnerItems.RemoveAt(source.ItemIndex);
            if (ReferenceEquals(source.OwnerItems, targetItems) && insertIndex > source.ItemIndex)
            {
                insertIndex--;
            }

            insertIndex = Math.Max(0, Math.Min(insertIndex, targetItems.Count));
            targetItems.Insert(insertIndex, source.Item);
        }

        private static bool ContainsOwnerList(MenuItemConfig item, List<MenuItemConfig> candidateList)
        {
            if (ReferenceEquals(item.Children, candidateList))
            {
                return true;
            }

            foreach (var child in item.Children)
            {
                if (ContainsOwnerList(child, candidateList))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddDroppedItems(IEnumerable<string> filePaths, DragItemInteraction? target)
        {
            List<MenuItemConfig> targetItems;
            var insertIndex = 0;
            if (target == null)
            {
                targetItems = _pages[_currentPageIndex];
                insertIndex = targetItems.Count;
            }
            else if (target != null && IsCategoryContainerDropTarget(target))
            {
                var categoryTargetItem = target.Item;
                if (categoryTargetItem == null)
                {
                    return;
                }

                targetItems = categoryTargetItem.Children;
                insertIndex = targetItems.Count;
            }
            else
            {
                var resolvedTarget = target;
                if (resolvedTarget == null)
                {
                    return;
                }

                targetItems = resolvedTarget.OwnerItems;
                insertIndex = Math.Max(0, Math.Min(resolvedTarget.InsertIndex, targetItems.Count));
            }

            var newItems = new List<MenuItemConfig>();
            foreach (var path in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var isDirectory = Directory.Exists(path);
                newItems.Add(new MenuItemConfig
                {
                    Label = isDirectory
                        ? GetDroppedItemLabel(path)
                        : System.IO.Path.GetFileNameWithoutExtension(path),
                    TargetPath = path
                });
            }

            if (newItems.Count == 0)
            {
                return;
            }

            targetItems.InsertRange(insertIndex, newItems);
            PersistMenuChanges();
            BuildMenu(_centerX, _centerY, _config);
            PlayUiSound(SoundCue.MenuDrop);
        }

        private static string GetDroppedItemLabel(string path)
        {
            var trimmedPath = path.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
            var fileName = System.IO.Path.GetFileName(trimmedPath);
            return string.IsNullOrWhiteSpace(fileName) ? trimmedPath : fileName;
        }

        private void PersistMenuChanges()
        {
            var clonedPages = _pages.Select((items, index) => new MenuPageConfig
            {
                Title = _config.Pages.ElementAtOrDefault(index)?.Title ?? (index + 1).ToString(),
                Items = MenuItemCloneService.CloneMany(items)
            }).ToList();

            _config.Pages = clonedPages;
            _config.Items = clonedPages.FirstOrDefault()?.Items ?? new List<MenuItemConfig>();
            _config.Page2Items = clonedPages.Skip(1).FirstOrDefault()?.Items ?? new List<MenuItemConfig>();

            _pages.Clear();
            foreach (var page in clonedPages)
            {
                _pages.Add(page.Items);
            }

            ScheduleConfigSave();
        }
    }
}


