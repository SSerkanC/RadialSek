using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class SettingsWindow : Window
    {
        public event EventHandler? SettingsSaved;
        private readonly MenuConfigService _configService;
        private readonly StartupLaunchService _startupLaunchService;
        private readonly SoundManager _soundManager;
        private readonly ObservableCollection<MenuPageConfig> _pages;
        private readonly ObservableCollection<ActivationShortcut> _shortcuts;
        private ActivationShortcut _targetingShortcut;
        private string? _capturingShortcutId;
        private ObservableCollection<MenuItemConfig> _activeItems;
        private MenuPageConfig _activePage;
        private int _activePageIndex;
        private Point _dragStartPoint;
        private MenuItemConfig? _draggedItem;
        private MenuFeatures _features;
        private string _categoryStripStyle = "GlassBeam";
        private string _categoryStripFont = "Segoe";
        private string _centerClockFont = "ProgramLabel";
        private double _categoryStripOpacity = 0.98;
        private double _categoryStripFontOpacity = 1.0;
        private string _categoryStripFontColor = DefaultCategoryStripFontColor;
        private double _innerGradientRingThicknessScale = 1.0;
        private double _outerGradientRingThicknessScale = 1.0;
        private double _menuBackdropBlurSizeScale = 1.0;
        private double _menuBackdropBlurStrengthScale = 1.0;
        private AudioSettings _audioSettings = new AudioSettings();
        private WeatherSettings _weatherSettings = new WeatherSettings();
        private MenuItemConfig? _editingItem;
        private string _selectedSectionKey = "general";
        private readonly Dictionary<string, FrameworkElement> _sectionMap = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Button> _navigationButtons = new List<Button>();
        private bool _isBinding;
        private readonly DispatcherTimer _previewTimer;
        private const string DefaultCategoryStripFontColor = "#FAFCFF";
        private static readonly SolidColorBrush InactiveTabHoverBrush = CreateFrozenBrush(Color.FromRgb(26, 31, 38));
        private static readonly SolidColorBrush DeleteTabHoverBrush = CreateFrozenBrush(Color.FromArgb(50, 255, 68, 68));
        private static readonly SolidColorBrush DeleteTabTextHoverBrush = CreateFrozenBrush(Color.FromRgb(255, 107, 107));
        private static readonly Color[] PreviewNeonOrbitPalette =
        {
            Color.FromRgb(57, 255, 110),
            Color.FromRgb(232, 34, 255),
            Color.FromRgb(0, 180, 255),
            Color.FromRgb(255, 174, 0),
            Color.FromRgb(147, 50, 255),
            Color.FromRgb(0, 216, 201),
            Color.FromRgb(255, 46, 123),
            Color.FromRgb(255, 222, 0)
        };

        public SettingsWindow(MenuConfigService configService)
        {
            InitializeComponent();
            _configService = configService;
            _startupLaunchService = new StartupLaunchService();
            _soundManager = SoundManager.Instance;
            _previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(70)
            };
            _previewTimer.Tick += (_, __) =>
            {
                _previewTimer.Stop();
                RenderPreview();
            };

            var config = _configService.LoadConfig();
            _features = config.Features?.Clone() ?? new MenuFeatures();
            _categoryStripStyle = CategoryStripStyleService.ResolveKey(config.CategoryStripStyle);
            _categoryStripFont = CategoryStripFontService.Resolve(config.CategoryStripFont).Key;
            _centerClockFont = CenterClockFontService.ResolveKey(config.CenterClockFont);
            _categoryStripOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripOpacity <= 0 ? 0.98 : config.CategoryStripOpacity));
            _categoryStripFontOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripFontOpacity <= 0 ? 1.0 : config.CategoryStripFontOpacity));
            _categoryStripFontColor = string.IsNullOrWhiteSpace(config.CategoryStripFontColor) ? DefaultCategoryStripFontColor : config.CategoryStripFontColor;
            _innerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.InnerGradientRingThicknessScale <= 0 ? 1.0 : config.InnerGradientRingThicknessScale));
            _outerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.OuterGradientRingThicknessScale <= 0 ? 1.0 : config.OuterGradientRingThicknessScale));
            _menuBackdropBlurSizeScale = Math.Max(0.6, Math.Min(2.5, config.MenuBackdropBlurSizeScale <= 0 ? 1.0 : config.MenuBackdropBlurSizeScale));
            _menuBackdropBlurStrengthScale = Math.Max(0.4, Math.Min(2.5, config.MenuBackdropBlurStrengthScale <= 0 ? 1.0 : config.MenuBackdropBlurStrengthScale));
            _audioSettings = config.Audio?.Clone() ?? new AudioSettings();
            _weatherSettings = config.Weather?.Clone() ?? new WeatherSettings();
            _soundManager.ApplySettings(_audioSettings);
            _pages = new ObservableCollection<MenuPageConfig>(
                (config.Pages ?? new List<MenuPageConfig>()).Select(page => new MenuPageConfig
                {
                    Title = page.Title,
                    Items = MenuItemCloneService.CloneMany(page.Items)
                }));
            if (_pages.Count == 0)
            {
                _pages.Add(new MenuPageConfig { Title = "1" });
            }
            _activePage = _pages[0];
            _activeItems = new ObservableCollection<MenuItemConfig>(_activePage.Items);
            _shortcuts = new ObservableCollection<ActivationShortcut>(
                (config.Shortcuts ?? new List<ActivationShortcut>()).Select(x => x.Clone()));
            if (_shortcuts.Count == 0)
            {
                _shortcuts.Add(new ActivationShortcut());
            }
            _targetingShortcut = config.TargetingShortcut?.Clone() ?? ActivationShortcut.CreateTargetingModeDefault();

            _isBinding = true;
            StyleComboBox.ItemsSource = MenuStyleService.GetOptions();
            StyleComboBox.SelectedValue = config.MenuStyle;
            TargetingModeComboBox.ItemsSource = TargetingModeStyleService.GetOptions();
            TargetingModeComboBox.SelectedValue = TargetingModeStyleService.ResolveKey(config.TargetingModeStyle);
            ThemeComboBox.ItemsSource = ThemePaletteService.GetPalettes();
            ThemeComboBox.SelectedValue = config.Theme;
            OpenAnimationComboBox.ItemsSource = MenuOpenAnimationService.GetOptions();
            OpenAnimationComboBox.SelectedValue = MenuOpenAnimationService.ResolveKey(config.OpenAnimationStyle);
            CategoryStripStyleComboBox.ItemsSource = CategoryStripStyleService.GetOptions();
            CategoryStripStyleComboBox.SelectedValue = _categoryStripStyle;
            CategoryStripFontComboBox.ItemsSource = CategoryStripFontService.GetOptions();
            CategoryStripFontComboBox.SelectedValue = _categoryStripFont;
            CenterClockFontComboBox.ItemsSource = CenterClockFontService.GetOptions();
            CenterClockFontComboBox.SelectedValue = _centerClockFont;
            WeatherManualPresetComboBox.ItemsSource = WeatherSettingsService.GetWeatherPresetOptions();
            WeatherManualPresetComboBox.SelectedValue = WeatherSettingsService.ResolveWeatherPresetKey(_weatherSettings.ManualPreset);
            WeatherDayNightModeComboBox.ItemsSource = WeatherSettingsService.GetDayNightModeOptions();
            WeatherDayNightModeComboBox.SelectedValue = WeatherSettingsService.ResolveDayNightModeKey(_weatherSettings.DayNightMode);
            CategoryStripOpacitySlider.Value = _categoryStripOpacity;
            CategoryStripFontOpacitySlider.Value = _categoryStripFontOpacity;
            InnerGradientRingThicknessSlider.Value = _innerGradientRingThicknessScale;
            OuterGradientRingThicknessSlider.Value = _outerGradientRingThicknessScale;
            MenuBackdropBlurSizeSlider.Value = _menuBackdropBlurSizeScale;
            MenuBackdropBlurStrengthSlider.Value = _menuBackdropBlurStrengthScale;
            WeatherSpeedSlider.Value = WeatherSettingsService.ClampSpeedScale(_weatherSettings.AnimationSpeedScale);
            WeatherIntensitySlider.Value = WeatherSettingsService.ClampIntensityScale(_weatherSettings.AnimationIntensityScale);

            StartWithWindowsCheckBox.IsChecked = _features.StartWithWindows;
            HoverLabelsCheckBox.IsChecked = _features.ShowHoverLabels;
            CategoryLabelsCheckBox.IsChecked = _features.ShowCategoryLabels;
            IconChromeCheckBox.IsChecked = _features.ShowIconChrome;
            GradientRingAnimationsCheckBox.IsChecked = _features.EnableGradientRingAnimations;
            MonochromeBackdropCheckBox.IsChecked = _features.EnableMonochromeBackdrop;
            MenuBackdropBlurCheckBox.IsChecked = _features.EnableMenuBackdropBlur;
            LightIdleModeCheckBox.IsChecked = _features.EnableLightIdleMode;
            LightIdleDelaySlider.Value = ClampLightIdleDelay(_features.LightIdleDelaySeconds);
            WeatherAnimationsEnabledCheckBox.IsChecked = _weatherSettings.EnableAnimations;
            WeatherUseLiveDataCheckBox.IsChecked = _weatherSettings.UseLiveData;
            SoundsEnabledCheckBox.IsChecked = _audioSettings.EnableSounds;
            SilentModeCheckBox.IsChecked = _audioSettings.SilentMode;
            MasterVolumeSlider.Value = ClampUnit(_audioSettings.MasterVolume, 0.72);
            UiVolumeSlider.Value = ClampUnit(_audioSettings.UiVolume, 0.86);
            HoverVolumeSlider.Value = ClampUnit(_audioSettings.HoverVolume, 0.78);
            NotificationVolumeSlider.Value = ClampUnit(_audioSettings.NotificationVolume, 0.82);

            OpenShortcutCaptureButton.Content = GetShortcut(ActivationShortcut.OpenMenuShortcutId).GetDisplayText();
            ToggleShortcutCaptureButton.Content = GetShortcut(ActivationShortcut.ToggleProgramShortcutId).GetDisplayText();
            TargetingShortcutCaptureButton.Content = _targetingShortcut.GetDisplayText();
            MenuTreeView.ItemsSource = _activeItems;
            TargetingModeComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            StyleComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            ThemeComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            OpenAnimationComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            CategoryStripStyleComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            CategoryStripFontComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            CenterClockFontComboBox.SelectionChanged += OnPreviewOptionSelectionChanged;
            StartWithWindowsCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            StartWithWindowsCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            HoverLabelsCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            HoverLabelsCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            CategoryLabelsCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            CategoryLabelsCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            IconChromeCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            IconChromeCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            GradientRingAnimationsCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            GradientRingAnimationsCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            MonochromeBackdropCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            MonochromeBackdropCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            MenuBackdropBlurCheckBox.Checked += OnFeatureToggleCheckBoxChanged;
            MenuBackdropBlurCheckBox.Unchecked += OnFeatureToggleCheckBoxChanged;
            CategoryStripOpacitySlider.ValueChanged += OnCategoryStripOpacitySliderValueChanged;
            CategoryStripFontOpacitySlider.ValueChanged += OnCategoryStripFontOpacitySliderValueChanged;
            InnerGradientRingThicknessSlider.ValueChanged += OnInnerGradientRingThicknessSliderValueChanged;
            OuterGradientRingThicknessSlider.ValueChanged += OnOuterGradientRingThicknessSliderValueChanged;
            MenuBackdropBlurSizeSlider.ValueChanged += OnMenuBackdropBlurSizeSliderValueChanged;
            MenuBackdropBlurStrengthSlider.ValueChanged += OnMenuBackdropBlurStrengthSliderValueChanged;
            PreviewCanvas.SizeChanged += (_, __) => RequestPreviewRender();
            UpdateLightIdleDelayValueText();
            UpdateLightIdleModeUi();
            UpdateCategoryStripValueTexts();
            UpdateWeatherValueTexts();
            UpdateWeatherControlsUi();
            UpdateStripFontColorButton();
            UpdateAudioValueTexts();
            UpdateAudioControlsUi();
            InitializeNavigation();
            _isBinding = false;

            if (_activeItems.Count > 0)
            {
                MenuTreeView.UpdateLayout();
                LoadSelectedItem(_activeItems[0]);
            }
            else
            {
                PrepareNewItem();
            }

            RebuildPageTabs();
            RequestPreviewRender();
            Loaded += (_, __) => _soundManager.Play(SoundCue.UiWindowOpen);
            Closed += (_, __) => _soundManager.Play(SoundCue.UiWindowClose);
        }

        private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ApplyEditorToCurrentItem(false);

            if (e.NewValue is MenuItemConfig item)
            {
                LoadSelectedItem(item);
            }
        }

        private void OnAddNewClicked(object sender, RoutedEventArgs e)
        {
            var item = new MenuItemConfig
            {
                Label = "Yeni Program",
                TargetPath = "",
                IsCategory = false
            };
            AddItemToCurrentLevel(item);
            StatusTextBlock.Text = "Yeni program ogesi olusturuldu.";
            PlayUiSound(SoundCue.UiClick);
        }

        private void OnAddCategoryClicked(object sender, RoutedEventArgs e)
        {
            var item = new MenuItemConfig
            {
                Label = "Yeni Kategori",
                TargetPath = "",
                IsCategory = true
            };
            AddItemToCurrentLevel(item);
            StatusTextBlock.Text = "Yeni kategori olusturuldu.";
            PlayUiSound(SoundCue.UiClick);
        }

        private void OnRemoveClicked(object sender, RoutedEventArgs e)
        {
            if (_editingItem is not MenuItemConfig item)
            {
                return;
            }

            var collection = FindParentCollection(item) ?? _activeItems;
            collection.Remove(item);
            MenuTreeView.Items.Refresh();
            if (_activeItems.Count == 0)
            {
                PrepareNewItem();
            }
            else
            {
                LoadSelectedItem(_activeItems[0]);
            }

            StatusTextBlock.Text = "Seçili öğe silindi.";
            RequestPreviewRender();
            PlayUiSound(SoundCue.Warning);
        }

        private void OnMoveUpClicked(object sender, RoutedEventArgs e)
        {
            MoveSelectedItem(-1);
        }

        private void OnMoveDownClicked(object sender, RoutedEventArgs e)
        {
            MoveSelectedItem(1);
        }

        private void MoveSelectedItem(int direction)
        {
            if (_editingItem is not MenuItemConfig item)
            {
                return;
            }

            var collection = FindParentCollection(item) ?? _activeItems;
            var oldIndex = collection.IndexOf(item);
            var newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= collection.Count)
            {
                return;
            }

            collection.RemoveAt(oldIndex);
            collection.Insert(newIndex, item);
            MenuTreeView.Items.Refresh();
            LoadSelectedItem(item);
            StatusTextBlock.Text = "Sıralama güncellendi.";
            RequestPreviewRender();
            PlayUiSound(SoundCue.UiSelect);
        }

        private void OnBrowseClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Dosya, Program veya Klasor Sec (Klasor secmek icin klasorun icine girip Ac'a basin)",
                Filter = "Tüm Dosyalar|*.*|Uygulamalar|*.exe|Kısayollar|*.lnk",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Klasor Secici"
            };

            if (dialog.ShowDialog(this) == true)
            {
                var path = dialog.FileName;
                if (path != null && (path.EndsWith("Klasor Secici") || path.EndsWith("Klasor Secici.txt")))
                {
                    path = System.IO.Path.GetDirectoryName(path);
                }
                
                if (string.IsNullOrWhiteSpace(path)) return;

                var isDir = System.IO.Directory.Exists(path);
                var label = isDir 
                    ? System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar))
                    : System.IO.Path.GetFileNameWithoutExtension(path);

                if (_editingItem == null)
                {
                    var newItem = new MenuItemConfig
                    {
                        TargetPath = path,
                        Label = label,
                        IsCategory = false
                    };
                    AddItemToCurrentLevel(newItem);
                }
                else
                {
                    _isBinding = true;
                    TargetPathTextBox.Text = path;
                    LabelTextBox.Text = label;
                    _editingItem.TargetPath = path;
                    _editingItem.Label = label;
                    _editingItem.IsCategory = false;
                    _isBinding = false;
                    RefreshItems();
                    RequestPreviewRender();
                }

                PlayUiSound(SoundCue.UiSelect);
            }
        }

        private void OnEditorTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isBinding || _editingItem == null) return;
            
            _editingItem.Label = LabelTextBox.Text;
            _editingItem.TargetPath = TargetPathTextBox.Text;
            RequestPreviewRender();
        }
        
        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (_editingItem != null)
            {
                RefreshItems();
            }
        }

        private bool ApplyEditorToCurrentItem(bool showErrors)
        {
            if (_editingItem != null)
            {
                _editingItem.Label = LabelTextBox.Text.Trim();
                _editingItem.TargetPath = TargetPathTextBox.Text.Trim();
            }
            return true;
        }

        private void RefreshItems()
        {
            MenuTreeView.Items.Refresh();
        }

        private void SyncActivePageItems()
        {
            _activePage.Items = _activeItems.Select(MenuItemCloneService.Clone).ToList();
        }

        private void PrepareNewItem()
        {
            _isBinding = true;
            _editingItem = null;
            LabelTextBox.Text = "";
            TargetPathTextBox.Text = "";
            RequestPreviewRender();
            _isBinding = false;
        }

        private void OnSaveSettingsClicked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void OnResetToDefaultsClicked(object sender, RoutedEventArgs e)
        {
            _isBinding = true;
            StyleComboBox.SelectedValue = "Style1";
            ThemeComboBox.SelectedValue = "Crimson";
            OpenAnimationComboBox.SelectedValue = "SoftRise";
            TargetingModeComboBox.SelectedValue = "LaserLine";
            CategoryStripStyleComboBox.SelectedValue = "GlassBeam";
            CategoryStripFontComboBox.SelectedValue = "Segoe";
            CenterClockFontComboBox.SelectedValue = "ProgramLabel";
            CategoryStripOpacitySlider.Value = 0.98;
            CategoryStripFontOpacitySlider.Value = 1.0;
            InnerGradientRingThicknessSlider.Value = 1.0;
            OuterGradientRingThicknessSlider.Value = 1.0;
            MenuBackdropBlurSizeSlider.Value = 1.0;
            MenuBackdropBlurStrengthSlider.Value = 1.0;

            _features = new MenuFeatures();
            StartWithWindowsCheckBox.IsChecked = _features.StartWithWindows;
            HoverLabelsCheckBox.IsChecked = _features.ShowHoverLabels;
            CategoryLabelsCheckBox.IsChecked = _features.ShowCategoryLabels;
            IconChromeCheckBox.IsChecked = _features.ShowIconChrome;
            GradientRingAnimationsCheckBox.IsChecked = _features.EnableGradientRingAnimations;
            MonochromeBackdropCheckBox.IsChecked = _features.EnableMonochromeBackdrop;
            MenuBackdropBlurCheckBox.IsChecked = _features.EnableMenuBackdropBlur;
            LightIdleModeCheckBox.IsChecked = _features.EnableLightIdleMode;
            LightIdleDelaySlider.Value = ClampLightIdleDelay(_features.LightIdleDelaySeconds);
            _categoryStripFontColor = DefaultCategoryStripFontColor;
            _audioSettings = new AudioSettings();
            _weatherSettings = new WeatherSettings();
            SoundsEnabledCheckBox.IsChecked = _audioSettings.EnableSounds;
            SilentModeCheckBox.IsChecked = _audioSettings.SilentMode;
            MasterVolumeSlider.Value = _audioSettings.MasterVolume;
            UiVolumeSlider.Value = _audioSettings.UiVolume;
            HoverVolumeSlider.Value = _audioSettings.HoverVolume;
            NotificationVolumeSlider.Value = _audioSettings.NotificationVolume;
            WeatherAnimationsEnabledCheckBox.IsChecked = _weatherSettings.EnableAnimations;
            WeatherUseLiveDataCheckBox.IsChecked = _weatherSettings.UseLiveData;
            WeatherManualPresetComboBox.SelectedValue = WeatherSettingsService.ResolveWeatherPresetKey(_weatherSettings.ManualPreset);
            WeatherDayNightModeComboBox.SelectedValue = WeatherSettingsService.ResolveDayNightModeKey(_weatherSettings.DayNightMode);
            WeatherSpeedSlider.Value = _weatherSettings.AnimationSpeedScale;
            WeatherIntensitySlider.Value = _weatherSettings.AnimationIntensityScale;
            _isBinding = false;

            ReplaceShortcut(ActivationShortcut.CreateOpenMenuDefault());
            ReplaceShortcut(ActivationShortcut.CreateToggleProgramDefault());
            _targetingShortcut = ActivationShortcut.CreateTargetingModeDefault();
            OpenShortcutCaptureButton.Content = GetShortcut(ActivationShortcut.OpenMenuShortcutId).GetDisplayText();
            ToggleShortcutCaptureButton.Content = GetShortcut(ActivationShortcut.ToggleProgramShortcutId).GetDisplayText();
            TargetingShortcutCaptureButton.Content = _targetingShortcut.GetDisplayText();

            UpdateLightIdleDelayValueText();
            UpdateLightIdleModeUi();
            UpdateCategoryStripValueTexts();
            UpdateStripFontColorButton();
            UpdateAudioValueTexts();
            UpdateAudioControlsUi();
            UpdateWeatherValueTexts();
            UpdateWeatherControlsUi();
            _soundManager.ApplySettings(_audioSettings);
            RequestPreviewRender();
            StatusTextBlock.Text = "Varsayilan ayarlar yuklendi. Kaydet ile kalici hale getirebilirsiniz.";
            PlayUiSound(SoundCue.Warning);
        }

        private void ApplyPanelValuesToModel()
        {
            _features.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _features.ShowHoverLabels = HoverLabelsCheckBox.IsChecked == true;
            _features.ShowCategoryLabels = CategoryLabelsCheckBox.IsChecked == true;
            _features.ShowIconChrome = IconChromeCheckBox.IsChecked == true;
            _features.EnableGradientRingAnimations = GradientRingAnimationsCheckBox.IsChecked == true;
            _features.EnableMonochromeBackdrop = MonochromeBackdropCheckBox.IsChecked == true;
            _features.EnableMenuBackdropBlur = MenuBackdropBlurCheckBox.IsChecked == true;
            _features.EnableLightIdleMode = LightIdleModeCheckBox.IsChecked == true;
            _features.LightIdleDelaySeconds = ClampLightIdleDelay((int)Math.Round(LightIdleDelaySlider.Value));

            var selectedOpenAnimation = OpenAnimationComboBox.SelectedValue as string ?? "SoftRise";
            _features.EnableOpenAnimation = !string.Equals(selectedOpenAnimation, "None", StringComparison.OrdinalIgnoreCase);

            _categoryStripStyle = CategoryStripStyleComboBox.SelectedValue as string ?? "GlassBeam";
            _categoryStripFont = CategoryStripFontComboBox.SelectedValue as string ?? "Segoe";
            _centerClockFont = CenterClockFontComboBox.SelectedValue as string ?? "ProgramLabel";
            _categoryStripOpacity = Math.Max(0.15, Math.Min(1.0, CategoryStripOpacitySlider.Value));
            _categoryStripFontOpacity = Math.Max(0.15, Math.Min(1.0, CategoryStripFontOpacitySlider.Value));
            _innerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, InnerGradientRingThicknessSlider.Value));
            _outerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, OuterGradientRingThicknessSlider.Value));
            _menuBackdropBlurSizeScale = Math.Max(0.6, Math.Min(2.5, MenuBackdropBlurSizeSlider.Value));
            _menuBackdropBlurStrengthScale = Math.Max(0.4, Math.Min(2.5, MenuBackdropBlurStrengthSlider.Value));
            _weatherSettings.EnableAnimations = WeatherAnimationsEnabledCheckBox.IsChecked == true;
            _weatherSettings.UseLiveData = WeatherUseLiveDataCheckBox.IsChecked == true;
            _weatherSettings.ManualPreset = WeatherSettingsService.ResolveWeatherPresetKey(WeatherManualPresetComboBox.SelectedValue as string);
            _weatherSettings.DayNightMode = WeatherSettingsService.ResolveDayNightModeKey(WeatherDayNightModeComboBox.SelectedValue as string);
            _weatherSettings.AnimationSpeedScale = WeatherSettingsService.ClampSpeedScale(WeatherSpeedSlider.Value);
            _weatherSettings.AnimationIntensityScale = WeatherSettingsService.ClampIntensityScale(WeatherIntensitySlider.Value);
            ApplyAudioPanelValuesToModel();
        }

        private void OnOpenShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.OpenMenuShortcutId;
            OpenShortcutCaptureButton.Content = "Kisayol bekleniyor...";
            Keyboard.Focus(this);
            PlayUiSound(SoundCue.UiSelect);
        }

        private void OnToggleShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.ToggleProgramShortcutId;
            ToggleShortcutCaptureButton.Content = "Kisayol bekleniyor...";
            Keyboard.Focus(this);
            PlayUiSound(SoundCue.UiSelect);
        }

        private void OnTargetingShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.TargetingModeShortcutId;
            TargetingShortcutCaptureButton.Content = "Kisayol bekleniyor...";
            Keyboard.Focus(this);
            PlayUiSound(SoundCue.UiSelect);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                _capturingShortcutId = null;
                e.Handled = true;
                Close();
                return;
            }

            if (_capturingShortcutId == null)
            {
                return;
            }

            if (_capturingShortcutId == ActivationShortcut.TargetingModeShortcutId)
            {
                var targetingShortcut = CreateTargetingShortcut(key);
                if (targetingShortcut != null)
                {
                    ApplyCapturedShortcut(targetingShortcut);
                    e.Handled = true;
                }

                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            var shortcut = new ActivationShortcut
            {
                Ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0,
                Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
                Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0,
                Win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0,
                Trigger = key.ToString(),
                ShortcutId = _capturingShortcutId
            };

            ApplyCapturedShortcut(shortcut);
            e.Handled = true;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_capturingShortcutId == null)
            {
                return;
            }

            var trigger = e.ChangedButton switch
            {
                MouseButton.Middle => "MiddleMouse",
                MouseButton.Right => "RightMouse",
                MouseButton.XButton1 => "XButton1",
                MouseButton.XButton2 => "XButton2",
                _ => null
            };

            if (trigger == null)
            {
                return;
            }

            var shortcut = new ActivationShortcut
            {
                Ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0,
                Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
                Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0,
                Win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0,
                Trigger = trigger,
                ShortcutId = _capturingShortcutId
            };

            ApplyCapturedShortcut(shortcut);
            e.Handled = true;
        }

        private void ApplyCapturedShortcut(ActivationShortcut shortcut)
        {
            if (shortcut.ShortcutId == ActivationShortcut.ToggleProgramShortcutId)
            {
                ReplaceShortcut(shortcut);
                ToggleShortcutCaptureButton.Content = shortcut.GetDisplayText();
            }
            else if (shortcut.ShortcutId == ActivationShortcut.TargetingModeShortcutId)
            {
                _targetingShortcut = shortcut.Clone();
                TargetingShortcutCaptureButton.Content = _targetingShortcut.GetDisplayText();
            }
            else
            {
                ReplaceShortcut(shortcut);
                OpenShortcutCaptureButton.Content = shortcut.GetDisplayText();
            }

            _capturingShortcutId = null;
            StatusTextBlock.Text = "Kisayol guncellendi.";
            PlayUiSound(SoundCue.ShortcutCaptured);
        }

        private ActivationShortcut? CreateTargetingShortcut(Key key)
        {
            var normalizedModifier = key switch
            {
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LeftShift or Key.RightShift => "Shift",
                Key.LWin or Key.RWin => "Win",
                _ => null
            };

            if (normalizedModifier != null)
            {
                return new ActivationShortcut
                {
                    ShortcutId = ActivationShortcut.TargetingModeShortcutId,
                    Trigger = normalizedModifier,
                    Ctrl = false,
                    Alt = false,
                    Shift = false,
                    Win = false
                };
            }

            if (key == Key.System)
            {
                return null;
            }

            return new ActivationShortcut
            {
                ShortcutId = ActivationShortcut.TargetingModeShortcutId,
                Ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0,
                Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
                Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0,
                Win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0,
                Trigger = key.ToString()
            };
        }

        private void OnLightIdleModeCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            UpdateLightIdleModeUi();
            if (!_isBinding && sender is CheckBox checkBox)
            {
                PlayUiSound(checkBox.IsChecked == true ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
            }
        }

        private void OnFeatureToggleCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_isBinding || sender is not CheckBox checkBox)
            {
                return;
            }

            PlayUiSound(checkBox.IsChecked == true ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
        }

        private void OnLightIdleDelaySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateLightIdleDelayValueText();
        }

        private void UpdateLightIdleDelayValueText()
        {
            LightIdleDelayValueTextBlock.Text = $"{ClampLightIdleDelay((int)Math.Round(LightIdleDelaySlider.Value))} sn";
        }

        private void UpdateLightIdleModeUi()
        {
            var isEnabled = LightIdleModeCheckBox.IsChecked == true;
            LightIdleDelaySlider.IsEnabled = isEnabled;
            LightIdleDelayValueTextBlock.Opacity = isEnabled ? 1.0 : 0.45;
            LightIdleModeDescriptionTextBlock.Opacity = isEnabled ? 1.0 : 0.65;
        }

        private void OnWeatherControlChanged(object sender, RoutedEventArgs e)
        {
            UpdateWeatherControlsUi();
            if (!_isBinding && sender is CheckBox checkBox)
            {
                PlayUiSound(checkBox.IsChecked == true ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
                RequestPreviewRender();
            }
        }

        private void OnWeatherSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateWeatherControlsUi();
            RequestPreviewRender();
            PlayUiSound(SoundCue.UiSelect);
        }

        private void OnWeatherSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateWeatherValueTexts();
            if (_isBinding)
            {
                return;
            }

            RequestPreviewRender();
        }

        private void UpdateWeatherValueTexts()
        {
            if (WeatherSpeedValueTextBlock != null)
            {
                WeatherSpeedValueTextBlock.Text = $"x{WeatherSpeedSlider.Value:0.0}";
            }

            if (WeatherIntensityValueTextBlock != null)
            {
                WeatherIntensityValueTextBlock.Text = $"x{WeatherIntensitySlider.Value:0.0}";
            }
        }

        private void UpdateWeatherControlsUi()
        {
            var animationsEnabled = WeatherAnimationsEnabledCheckBox.IsChecked == true;
            var useLiveData = WeatherUseLiveDataCheckBox.IsChecked == true;
            var manualMode = animationsEnabled && !useLiveData;

            WeatherUseLiveDataCheckBox.IsEnabled = animationsEnabled;
            WeatherManualPresetComboBox.IsEnabled = manualMode;
            WeatherDayNightModeComboBox.IsEnabled = animationsEnabled;
            WeatherSpeedSlider.IsEnabled = animationsEnabled;
            WeatherIntensitySlider.IsEnabled = animationsEnabled;

            var manualOpacity = manualMode ? 1.0 : 0.55;
            WeatherManualPresetComboBox.Opacity = manualOpacity;
            WeatherDayNightModeComboBox.Opacity = animationsEnabled ? 1.0 : 0.55;
            WeatherSpeedSlider.Opacity = animationsEnabled ? 1.0 : 0.55;
            WeatherIntensitySlider.Opacity = animationsEnabled ? 1.0 : 0.55;
        }

        private void OnSoundModeCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateAudioControlsUi();
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
            if (sender is CheckBox checkBox)
            {
                PlayUiSound(checkBox.IsChecked == true ? SoundCue.UiToggleOn : SoundCue.UiToggleOff);
            }
        }

        private void OnMasterVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateAudioValueTexts();
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
        }

        private void OnUiVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateAudioValueTexts();
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
        }

        private void OnHoverVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateAudioValueTexts();
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
        }

        private void OnNotificationVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isBinding)
            {
                return;
            }

            UpdateAudioValueTexts();
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
        }

        private void OnTestUiSoundClicked(object sender, RoutedEventArgs e)
        {
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
            PlayUiSound(SoundCue.UiClick);
        }

        private void OnTestNotificationSoundClicked(object sender, RoutedEventArgs e)
        {
            ApplyAudioPanelValuesToModel();
            _soundManager.ApplySettings(_audioSettings);
            PlayUiSound(SoundCue.Notification);
        }

        private void UpdateAudioValueTexts()
        {
            MasterVolumeValueTextBlock.Text = FormatVolumePercentage(MasterVolumeSlider.Value);
            UiVolumeValueTextBlock.Text = FormatVolumePercentage(UiVolumeSlider.Value);
            HoverVolumeValueTextBlock.Text = FormatVolumePercentage(HoverVolumeSlider.Value);
            NotificationVolumeValueTextBlock.Text = FormatVolumePercentage(NotificationVolumeSlider.Value);
        }

        private void UpdateAudioControlsUi()
        {
            var soundsEnabled = SoundsEnabledCheckBox.IsChecked == true;
            var silentMode = SilentModeCheckBox.IsChecked == true;
            var allowPlayback = soundsEnabled && !silentMode;
            MasterVolumeSlider.IsEnabled = soundsEnabled;
            UiVolumeSlider.IsEnabled = allowPlayback;
            HoverVolumeSlider.IsEnabled = allowPlayback;
            NotificationVolumeSlider.IsEnabled = allowPlayback;
            TestUiSoundButton.IsEnabled = allowPlayback;
            TestNotificationSoundButton.IsEnabled = allowPlayback;
            var contentOpacity = soundsEnabled ? 1.0 : 0.45;
            var activeOpacity = allowPlayback ? 1.0 : 0.55;
            MasterVolumeSlider.Opacity = contentOpacity;
            UiVolumeSlider.Opacity = activeOpacity;
            HoverVolumeSlider.Opacity = activeOpacity;
            NotificationVolumeSlider.Opacity = activeOpacity;
            AudioHintTextBlock.Opacity = allowPlayback ? 0.95 : 0.65;
        }

        private void ApplyAudioPanelValuesToModel()
        {
            _audioSettings.EnableSounds = SoundsEnabledCheckBox.IsChecked == true;
            _audioSettings.SilentMode = SilentModeCheckBox.IsChecked == true;
            _audioSettings.MasterVolume = ClampUnit(MasterVolumeSlider.Value, 0.72);
            _audioSettings.UiVolume = ClampUnit(UiVolumeSlider.Value, 0.86);
            _audioSettings.HoverVolume = ClampUnit(HoverVolumeSlider.Value, 0.78);
            _audioSettings.NotificationVolume = ClampUnit(NotificationVolumeSlider.Value, 0.82);
        }

        private static string FormatVolumePercentage(double value)
        {
            return $"{Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 100):0}%";
        }

        private static double ClampUnit(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static int ClampLightIdleDelay(int seconds)
        {
            return Math.Max(5, Math.Min(60, seconds <= 0 ? 20 : seconds));
        }

        private void OnCategoryStripOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            CategoryStripOpacityValueText.Text = $"{Math.Round(CategoryStripOpacitySlider.Value * 100):0}%";
            RequestPreviewRender();
        }

        private void OnCategoryStripFontOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            CategoryStripFontOpacityValueText.Text = $"{Math.Round(CategoryStripFontOpacitySlider.Value * 100):0}%";
            RequestPreviewRender();
        }

        private void OnInnerGradientRingThicknessSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            InnerGradientRingThicknessValueText.Text = FormatScalePercentage(InnerGradientRingThicknessSlider.Value);
            RequestPreviewRender();
        }

        private void OnOuterGradientRingThicknessSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            OuterGradientRingThicknessValueText.Text = FormatScalePercentage(OuterGradientRingThicknessSlider.Value);
            RequestPreviewRender();
        }

        private void OnMenuBackdropBlurSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MenuBackdropBlurSizeValueText.Text = FormatScalePercentage(MenuBackdropBlurSizeSlider.Value);
            RequestPreviewRender();
        }

        private void OnMenuBackdropBlurStrengthSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MenuBackdropBlurStrengthValueText.Text = FormatScalePercentage(MenuBackdropBlurStrengthSlider.Value);
            RequestPreviewRender();
        }

        private void UpdateCategoryStripValueTexts()
        {
            CategoryStripOpacityValueText.Text = $"{Math.Round(CategoryStripOpacitySlider.Value * 100):0}%";
            CategoryStripFontOpacityValueText.Text = $"{Math.Round(CategoryStripFontOpacitySlider.Value * 100):0}%";
            InnerGradientRingThicknessValueText.Text = FormatScalePercentage(InnerGradientRingThicknessSlider.Value);
            OuterGradientRingThicknessValueText.Text = FormatScalePercentage(OuterGradientRingThicknessSlider.Value);
            MenuBackdropBlurSizeValueText.Text = FormatScalePercentage(MenuBackdropBlurSizeSlider.Value);
            MenuBackdropBlurStrengthValueText.Text = FormatScalePercentage(MenuBackdropBlurStrengthSlider.Value);
        }

        private static string FormatScalePercentage(double value)
        {
            return $"{Math.Round(value * 100):0}%";
        }

        private void OnCategoryStripFontColorClicked(object sender, RoutedEventArgs e)
        {
            var hasCustomColor = !string.Equals(_categoryStripFontColor, DefaultCategoryStripFontColor, StringComparison.OrdinalIgnoreCase);
            var dialog = new ColorPickerWindow(_categoryStripFontColor, hasCustomColor)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _categoryStripFontColor = dialog.ResetToThemeRequested ? DefaultCategoryStripFontColor : dialog.SelectedHexColor;
            UpdateStripFontColorButton();
            RequestPreviewRender();
            PlayUiSound(SoundCue.UiSelect);
        }

        private void UpdateStripFontColorButton()
        {
            if (CategoryStripFontColorSwatch == null)
            {
                return;
            }

            CategoryStripFontColorSwatch.Background = new SolidColorBrush(ParseHexColor(_categoryStripFontColor));
        }

        private static Color ParseHexColor(string value)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                return color;
            }
            catch
            {
                return Color.FromRgb(250, 252, 255);
            }
        }

        private void InitializeNavigation()
        {
            _navigationButtons.Clear();
            _navigationButtons.Add(NavGeneralButton);
            _navigationButtons.Add(NavAudioButton);
            _navigationButtons.Add(NavControlsButton);
            _navigationButtons.Add(NavAppearanceButton);
            _navigationButtons.Add(NavRingsButton);
            _navigationButtons.Add(NavWeatherButton);
            _navigationButtons.Add(NavMenuButton);

            _sectionMap.Clear();
            _sectionMap["general"] = SectionGeneralPanel;
            _sectionMap["audio"] = SectionAudioPanel;
            _sectionMap["controls"] = SectionControlsPanel;
            _sectionMap["appearance"] = SectionAppearancePanel;
            _sectionMap["rings"] = SectionRingsPanel;
            _sectionMap["weather"] = SectionWeatherPanel;
            _sectionMap["menu"] = SectionMenuPanel;

            UpdateNavigationSelection();
            UpdateSectionVisibility();
        }

        private void OnNavigationButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string sectionKey)
            {
                return;
            }

            if (string.Equals(_selectedSectionKey, sectionKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedSectionKey = sectionKey;
            UpdateNavigationSelection();
            UpdateSectionVisibility();
            PlayUiSound(SoundCue.UiHover);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSectionVisibility();
        }

        private void OnPreviewOptionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isBinding)
            {
                return;
            }

            RequestPreviewRender();
            PlayUiSound(SoundCue.UiSelect);
        }

        private void UpdateNavigationSelection()
        {
            var accentBrush = (Brush)FindResource("AccentBrush");
            var panelBrush = (Brush)FindResource("PanelBrushAlt");
            var strokeBrush = (Brush)FindResource("StrokeBrush");
            var mutedBrush = (Brush)FindResource("MutedBrush");

            foreach (var button in _navigationButtons)
            {
                var isActive = string.Equals(button.Tag as string, _selectedSectionKey, StringComparison.OrdinalIgnoreCase);
                button.Background = isActive ? accentBrush : panelBrush;
                button.BorderBrush = isActive ? accentBrush : strokeBrush;
                button.Foreground = isActive ? Brushes.White : mutedBrush;
            }
        }

        private void UpdateSectionVisibility()
        {
            var query = NormalizeSearchText(SettingsSearchTextBox.Text);
            var hasQuery = !string.IsNullOrWhiteSpace(query);
            var visibleCount = 0;

            foreach (var section in _sectionMap)
            {
                var keywords = NormalizeSearchText(section.Value.Tag as string ?? string.Empty);
                var showSection = hasQuery
                    ? keywords.Contains(query, StringComparison.Ordinal)
                    : string.Equals(section.Key, _selectedSectionKey, StringComparison.OrdinalIgnoreCase);
                section.Value.Visibility = showSection ? Visibility.Visible : Visibility.Collapsed;
                if (showSection)
                {
                    visibleCount++;
                }
            }

            NoResultsTextBlock.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string NormalizeSearchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Trim()
                .ToLowerInvariant()
                .Replace('\u0131', 'i')
                .Replace('\u015F', 's')
                .Replace('\u011F', 'g')
                .Replace('\u00FC', 'u')
                .Replace('\u00F6', 'o')
                .Replace('\u00E7', 'c');
        }

        private void SaveSettings()
        {
            ApplyEditorToCurrentItem(false);
            ApplyPanelValuesToModel();

            var selectedTheme = ThemeComboBox.SelectedValue as string ?? "Crimson";
            var selectedStyle = StyleComboBox.SelectedValue as string ?? "Style1";
            var selectedOpenAnimation = OpenAnimationComboBox.SelectedValue as string ?? "SoftRise";
            var selectedTargetingMode = TargetingModeComboBox.SelectedValue as string ?? "LaserLine";
            SyncActivePageItems();
            var config = new MenuConfig
            {
                MenuStyle = selectedStyle,
                Theme = selectedTheme,
                OpenAnimationStyle = selectedOpenAnimation,
                TargetingModeStyle = selectedTargetingMode,
                CategoryStripStyle = _categoryStripStyle,
                CategoryStripFont = _categoryStripFont,
                CenterClockFont = _centerClockFont,
                CategoryStripOpacity = _categoryStripOpacity,
                CategoryStripFontOpacity = _categoryStripFontOpacity,
                CategoryStripFontColor = _categoryStripFontColor,
                InnerGradientRingThicknessScale = _innerGradientRingThicknessScale,
                OuterGradientRingThicknessScale = _outerGradientRingThicknessScale,
                MenuBackdropBlurSizeScale = _menuBackdropBlurSizeScale,
                MenuBackdropBlurStrengthScale = _menuBackdropBlurStrengthScale,
                TargetingShortcut = _targetingShortcut.Clone(),
                Features = _features.Clone(),
                Weather = _weatherSettings.Clone(),
                Shortcuts = _shortcuts.Select(x => x.Clone()).ToList(),
                Audio = _audioSettings.Clone()
            };
            config.Pages.Clear();

            foreach (var page in _pages)
            {
                var pageConfig = new MenuPageConfig
                {
                    Title = page.Title
                };

                foreach (var item in page.Items.Where(x =>
                             !string.IsNullOrWhiteSpace(x.Label) &&
                             (!string.IsNullOrWhiteSpace(x.TargetPath) || IsCategory(x))))
                {
                    pageConfig.Items.Add(MenuItemCloneService.Clone(item));
                }

                config.Pages.Add(pageConfig);
            }

            _configService.SaveConfig(config);
            _startupLaunchService.Apply(_features.StartWithWindows, System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            _soundManager.ApplySettings(_audioSettings);
            StatusTextBlock.Text = "Ayarlar kaydedildi. Yeni duzen bir sonraki acilista kullanilacak.";
            PlayUiSound(SoundCue.Success);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }

        private ActivationShortcut GetShortcut(string shortcutId)
        {
            var existing = _shortcuts.FirstOrDefault(x => x.ShortcutId == shortcutId);
            if (existing != null)
            {
                return existing.Clone();
            }

            return shortcutId == ActivationShortcut.ToggleProgramShortcutId
                ? ActivationShortcut.CreateToggleProgramDefault()
                : ActivationShortcut.CreateOpenMenuDefault();
        }

        private void ReplaceShortcut(ActivationShortcut shortcut)
        {
            for (var i = _shortcuts.Count - 1; i >= 0; i--)
            {
                if (_shortcuts[i].ShortcutId == shortcut.ShortcutId)
                {
                    _shortcuts.RemoveAt(i);
                }
            }

            _shortcuts.Add(shortcut.Clone());
        }

        private void OnEditChildrenClicked(object sender, RoutedEventArgs e)
        {
            if (_editingItem == null)
            {
                StatusTextBlock.Text = "Önce bir öğe seçin.";
                PlayUiSound(SoundCue.Warning);
                return;
            }

            ApplyEditorToCurrentItem(false);
            var childEditor = new ItemCollectionEditorWindow(_editingItem.Children, _editingItem.Label + " / Alt Menü")
            {
                Owner = this
            };

            if (childEditor.ShowDialog() == true)
            {
                _editingItem.Children = childEditor.ResultItems;
                _editingItem.IsCategory = true;
                RefreshItems();
                StatusTextBlock.Text = "Alt menü güncellendi.";
                RequestPreviewRender();
                PlayUiSound(SoundCue.UiSelect);
            }
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddItemToCurrentLevel(MenuItemConfig item)
        {
            var collection = _editingItem != null ? FindParentCollection(_editingItem) ?? _activeItems : _activeItems;
            collection.Add(item);
            RefreshItems();
            LoadSelectedItem(item);
            LabelTextBox.Focus();
            LabelTextBox.SelectAll();
            RequestPreviewRender();
        }

        private void SwitchPage(int pageIndex)
        {
            ApplyEditorToCurrentItem(false);
            SyncActivePageItems();
            _activePageIndex = pageIndex;
            _activePage = _pages[pageIndex];
            _activeItems = new ObservableCollection<MenuItemConfig>(
                MenuItemCloneService.CloneMany(_activePage.Items));
            MenuTreeView.ItemsSource = _activeItems;
            RebuildPageTabs();

            if (_activeItems.Count > 0)
            {
                LoadSelectedItem(_activeItems[0]);
            }
            else
            {
                PrepareNewItem();
                RefreshItems();
            }

            StatusTextBlock.Text = _activePage.Title + ". sayfa duzenleniyor.";
            RequestPreviewRender();
            PlayUiSound(SoundCue.UiHover);
        }

        private void RebuildPageTabs()
        {
            if (PageTabsPanel == null)
            {
                return;
            }

            PageTabsPanel.Children.Clear();

            var inputBrush = (SolidColorBrush)FindResource("InputBrush");
            var panelAltBrush = (SolidColorBrush)FindResource("PanelBrushAlt");
            var strokeBrush = (SolidColorBrush)FindResource("StrokeBrush");
            var mutedBrush = (SolidColorBrush)FindResource("MutedBrush");

            for (var i = 0; i < _pages.Count; i++)
            {
                var index = i;
                var isActive = i == _activePageIndex;

                var tabBorder = new Border
                {
                    Background = isActive ? panelAltBrush : Brushes.Transparent,
                    BorderBrush = isActive ? strokeBrush : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand,
                    AllowDrop = true
                };

                if (!isActive)
                {
                    tabBorder.MouseEnter += (_, __) => tabBorder.Background = InactiveTabHoverBrush;
                    tabBorder.MouseLeave += (_, __) => tabBorder.Background = Brushes.Transparent;
                }
                tabBorder.MouseLeftButtonDown += (_, __) => SwitchPage(index);
                tabBorder.DragOver += OnPageTabDragOver;
                tabBorder.Drop += (_, e) => OnPageTabDrop(index, e);

                var panel = new StackPanel { Orientation = Orientation.Horizontal };

                var titleText = new TextBlock
                {
                    Text = "Sayfa " + _pages[i].Title,
                    Foreground = isActive ? Brushes.White : mutedBrush,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                    Margin = new Thickness(14, 8, _pages.Count > 1 ? 6 : 14, 8),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(titleText);

                if (_pages.Count > 1)
                {
                    var closeBorder = new Border
                    {
                        Background = Brushes.Transparent,
                        CornerRadius = new CornerRadius(8),
                        Margin = new Thickness(0, 0, 6, 0),
                        Padding = new Thickness(6, 4, 6, 4),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        ToolTip = "Sayfay\u0131 Sil"
                    };

                    var closeText = new TextBlock
                    {
                        Text = "\u00D7",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = mutedBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    closeBorder.Child = closeText;

                    closeBorder.MouseEnter += (_, __) => 
                    {
                        closeBorder.Background = DeleteTabHoverBrush;
                        closeText.Foreground = DeleteTabTextHoverBrush;
                    };
                    closeBorder.MouseLeave += (_, __) => 
                    {
                        closeBorder.Background = Brushes.Transparent;
                        closeText.Foreground = mutedBrush;
                    };
                    closeBorder.MouseLeftButtonDown += (s, e) => 
                    {
                        e.Handled = true;
                        RemovePage(index);
                    };

                    panel.Children.Add(closeBorder);
                }

                tabBorder.Child = panel;
                PageTabsPanel.Children.Add(tabBorder);
            }

            var addBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = strokeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                ToolTip = "Yeni Sayfa Ekle"
            };

            addBorder.MouseEnter += (_, __) => addBorder.Background = InactiveTabHoverBrush;
            addBorder.MouseLeave += (_, __) => addBorder.Background = Brushes.Transparent;
            addBorder.MouseLeftButtonDown += (_, __) => AddNewPage();

            var addText = new TextBlock
            {
                Text = "+",
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                Foreground = mutedBrush,
                Margin = new Thickness(16, 4, 16, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            addBorder.Child = addText;

            PageTabsPanel.Children.Add(addBorder);
        }

        private void OnPageTabDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MenuItemConfig)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void OnPageTabDrop(int targetPageIndex, DragEventArgs e)
        {
            if (targetPageIndex < 0 || targetPageIndex >= _pages.Count)
            {
                return;
            }

            if (!e.Data.GetDataPresent(typeof(MenuItemConfig)))
            {
                return;
            }

            if (e.Data.GetData(typeof(MenuItemConfig)) is not MenuItemConfig dragged)
            {
                return;
            }

            if (targetPageIndex == _activePageIndex)
            {
                e.Handled = true;
                return;
            }

            var sourceCollection = FindParentCollection(dragged) ?? _activeItems;
            if (!sourceCollection.Remove(dragged))
            {
                return;
            }

            SyncActivePageItems();
            _pages[targetPageIndex].Items.Add(MenuItemCloneService.Clone(dragged));
            SwitchPage(targetPageIndex);

            if (_activeItems.Count > 0)
            {
                LoadSelectedItem(_activeItems[_activeItems.Count - 1]);
            }

            StatusTextBlock.Text = "Menu ogesi sayfalar arasinda tasindi.";
            RequestPreviewRender();
            PlayUiSound(SoundCue.MenuDrop);
            e.Handled = true;
        }

        private void AddNewPage()
        {
            ApplyEditorToCurrentItem(false);
            SyncActivePageItems();
            var newPage = new MenuPageConfig
            {
                Title = (_pages.Count + 1).ToString()
            };
            _pages.Add(newPage);
            SwitchPage(_pages.Count - 1);
            PlayUiSound(SoundCue.UiClick);
        }

        private void RemovePage(int pageIndex)
        {
            if (_pages.Count <= 1 || pageIndex < 0 || pageIndex >= _pages.Count)
            {
                return;
            }

            ApplyEditorToCurrentItem(false);
            SyncActivePageItems();
            _pages.RemoveAt(pageIndex);
            if (_activePageIndex >= _pages.Count)
            {
                _activePageIndex = _pages.Count - 1;
            }

            for (var i = 0; i < _pages.Count; i++)
            {
                _pages[i].Title = (i + 1).ToString();
            }

            SwitchPage(_activePageIndex);
            StatusTextBlock.Text = "Sayfa silindi.";
            PlayUiSound(SoundCue.Warning);
        }

        private void OnTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(MenuTreeView);
            _draggedItem = GetTreeItemFromSource(e.OriginalSource as DependencyObject);
        }

        private void OnTreeMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null)
            {
                return;
            }

            var position = e.GetPosition(MenuTreeView);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(MenuTreeView, new DataObject(typeof(MenuItemConfig), _draggedItem), DragDropEffects.Move);
        }

        private void OnTreeDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MenuItemConfig)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void OnTreeDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var targetNode = GetTreeItemFromSource(e.OriginalSource as DependencyObject);

                foreach (var path in files)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    
                    var isDirectory = Directory.Exists(path);
                    var item = new MenuItemConfig
                    {
                        Label = isDirectory
                            ? System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar))
                            : System.IO.Path.GetFileNameWithoutExtension(path),
                        TargetPath = path,
                        IsCategory = false
                    };
                    
                    if (targetNode != null && IsCategory(targetNode))
                    {
                        targetNode.Children.Add(item);
                    }
                    else
                    {
                        _activeItems.Add(item);
                    }
                }
                
                RefreshItems();
                RequestPreviewRender();
                e.Handled = true;
                PlayUiSound(SoundCue.MenuDrop);
                return;
            }

            if (!e.Data.GetDataPresent(typeof(MenuItemConfig)))
            {
                return;
            }

            var dragged = (MenuItemConfig)e.Data.GetData(typeof(MenuItemConfig));
            var target = GetTreeItemFromSource(e.OriginalSource as DependencyObject);
            if (dragged == null || ReferenceEquals(dragged, target) || IsDescendantOf(dragged, target))
            {
                return;
            }

            var sourceCollection = FindParentCollection(dragged) ?? _activeItems;
            sourceCollection.Remove(dragged);

            if (target == null)
            {
                _activeItems.Add(dragged);
            }
            else if (IsCategory(target))
            {
                target.Children.Add(dragged);
            }
            else
            {
                var targetCollection = FindParentCollection(target) ?? _activeItems;
                var targetIndex = targetCollection.IndexOf(target);
                var targetContainer = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
                if (targetContainer != null)
                {
                    var relative = e.GetPosition(targetContainer);
                    if (relative.Y > targetContainer.ActualHeight / 2.0)
                    {
                        targetIndex++;
                    }
                }

                targetCollection.Insert(Math.Min(targetIndex, targetCollection.Count), dragged);
            }

            RefreshItems();
            LoadSelectedItem(dragged);
            StatusTextBlock.Text = "Menu ogesi tasindi.";
            PlayUiSound(SoundCue.MenuDrop);
        }

        private static bool IsCategory(MenuItemConfig item)
        {
            return item.IsCategory || item.Children.Count > 0;
        }

        private static bool IsDescendantOf(MenuItemConfig source, MenuItemConfig? potentialParent)
        {
            if (potentialParent == null)
            {
                return false;
            }

            foreach (var child in source.Children)
            {
                if (ReferenceEquals(child, potentialParent) || IsDescendantOf(child, potentialParent))
                {
                    return true;
                }
            }

            return false;
        }

        private MenuItemConfig? GetTreeItemFromSource(DependencyObject? source)
        {
            var container = FindVisualParent<TreeViewItem>(source);
            return container?.DataContext as MenuItemConfig;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T target)
                {
                    return target;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private IList<MenuItemConfig>? FindParentCollection(MenuItemConfig item)
        {
            if (_activeItems.Contains(item))
            {
                return _activeItems;
            }

            return FindParentCollectionRecursive(_activeItems, item);
        }

        private IList<MenuItemConfig>? FindParentCollectionRecursive(IEnumerable<MenuItemConfig> source, MenuItemConfig target)
        {
            foreach (var item in source)
            {
                if (item.Children.Contains(target))
                {
                    return item.Children;
                }

                var nested = FindParentCollectionRecursive(item.Children, target);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void LoadSelectedItem(MenuItemConfig item)
        {
            _isBinding = true;
            _editingItem = item;
            LabelTextBox.Text = item.Label;
            TargetPathTextBox.Text = item.TargetPath;
            StatusTextBlock.Text = "Bir öğe seçildi. Yaptığınız değişiklikler anında kaydedilir.";
            RequestPreviewRender();
            _isBinding = false;
        }

        private void OnItemsDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void OnItemsDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var path in files)
            {
                var isDirectory = Directory.Exists(path);
                var item = new MenuItemConfig
                {
                    Label = isDirectory
                        ? System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar))
                        : System.IO.Path.GetFileNameWithoutExtension(path),
                    TargetPath = path,
                    IsCategory = false
                };
                _activeItems.Add(item);
            }

            RefreshItems();
            RequestPreviewRender();
            StatusTextBlock.Text = "Sürükle-bırak ile yeni öğeler eklendi.";
            PlayUiSound(SoundCue.MenuDrop);
        }

        private void RequestPreviewRender()
        {
            if (!IsLoaded)
            {
                return;
            }

            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void RenderPreview()
        {
            if (PreviewCanvas == null)
            {
                return;
            }

            PreviewCanvas.Children.Clear();
            if (_activeItems.Count == 0)
            {
                return;
            }

            var width = PreviewCanvas.ActualWidth > 40 ? PreviewCanvas.ActualWidth : PreviewCanvas.Width;
            var height = PreviewCanvas.ActualHeight > 40 ? PreviewCanvas.ActualHeight : PreviewCanvas.Height;
            var centerX = width / 2;
            var centerY = height / 2;
            var style = StyleComboBox.SelectedValue as string ?? "Style1";
            var theme = ThemePaletteService.Resolve(ThemeComboBox.SelectedValue as string ?? "Crimson");

            var outerRadius = style == "Style4" ? 114 : style == "Style5" ? 96 : 106;
            var innerRadius = style == "Style3" ? 78 : style == "Style2" ? 66 : 58;
            var totalSweep = style == "Style6" ? 240.0 : 360.0;
            var startAngle = style == "Style6" ? -210.0 : -90.0;
            var gap = style == "Style2" || style == "Style5" ? 0.0 : 3.0;

            if (style == "Style7")
            {
                RenderNeonOrbitPreview(centerX, centerY);
                return;
            }

            if (MenuBackdropBlurCheckBox.IsChecked == true)
            {
                var blurSizeScale = Math.Max(0.6, Math.Min(2.5, MenuBackdropBlurSizeSlider.Value));
                var blurStrengthScale = Math.Max(0.4, Math.Min(2.5, MenuBackdropBlurStrengthSlider.Value));
                var blurDiameter = Math.Max(120.0, outerRadius * 4.0 * blurSizeScale);
                var blurFill = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.5, 0.5),
                    Center = new Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5
                };
                blurFill.GradientStops.Add(new GradientStop(Color.FromArgb(ToByte(96.0 * blurStrengthScale), theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B), 0.0));
                blurFill.GradientStops.Add(new GradientStop(Color.FromArgb(ToByte(56.0 * blurStrengthScale), theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B), 0.48));
                blurFill.GradientStops.Add(new GradientStop(Color.FromArgb(ToByte(18.0 * blurStrengthScale), theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B), 0.78));
                blurFill.GradientStops.Add(new GradientStop(Color.FromArgb(0, theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B), 1.0));

                var blurHalo = new Ellipse
                {
                    Width = blurDiameter,
                    Height = blurDiameter,
                    Fill = blurFill,
                    Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = Math.Max(20.0, outerRadius * 0.26 * blurStrengthScale),
                        RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
                    }
                };
                Canvas.SetLeft(blurHalo, centerX - blurHalo.Width / 2);
                Canvas.SetTop(blurHalo, centerY - blurHalo.Height / 2);
                PreviewCanvas.Children.Add(blurHalo);
            }

            var backdrop = new Ellipse
            {
                Width = outerRadius * 2 + 16,
                Height = outerRadius * 2 + 16,
                Fill = new SolidColorBrush(Color.FromArgb(20, theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B))
            };
            Canvas.SetLeft(backdrop, centerX - backdrop.Width / 2);
            Canvas.SetTop(backdrop, centerY - backdrop.Height / 2);
            PreviewCanvas.Children.Add(backdrop);

            for (var i = 0; i < _activeItems.Count; i++)
            {
                var segmentStart = startAngle + (totalSweep / _activeItems.Count) * i;
                var segmentEnd = segmentStart + (totalSweep / _activeItems.Count) - gap;
                var selected = ReferenceEquals(_activeItems[i], _editingItem);

                var path = new System.Windows.Shapes.Path
                {
                    Data = CreateSegment(centerX, centerY, innerRadius, outerRadius, segmentStart, segmentEnd),
                    Fill = new SolidColorBrush(selected ? theme.SegmentActiveColor : theme.SegmentColor),
                    Stroke = new SolidColorBrush(theme.SegmentStrokeColor),
                    StrokeThickness = style == "Style5" ? 0.8 : 1.2
                };
                PreviewCanvas.Children.Add(path);

                var mid = (segmentStart + segmentEnd) / 2.0;
                var dotRadius = (innerRadius + outerRadius) / 2.0;
                var dot = new Ellipse
                {
                    Width = selected ? 14 : 10,
                    Height = selected ? 14 : 10,
                    Fill = new SolidColorBrush(selected ? theme.IconActiveBorderColor : Colors.White)
                };
                Canvas.SetLeft(dot, centerX + Math.Cos(mid * Math.PI / 180.0) * dotRadius - dot.Width / 2);
                Canvas.SetTop(dot, centerY + Math.Sin(mid * Math.PI / 180.0) * dotRadius - dot.Height / 2);
                PreviewCanvas.Children.Add(dot);
            }

            var center = new Ellipse
            {
                Width = innerRadius * 2 - 8,
                Height = innerRadius * 2 - 8,
                Fill = new SolidColorBrush(theme.CenterColor),
                Stroke = new SolidColorBrush(theme.CenterBorderColor),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(center, centerX - center.Width / 2);
            Canvas.SetTop(center, centerY - center.Height / 2);
            PreviewCanvas.Children.Add(center);

            var title = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                Foreground = new SolidColorBrush(theme.TitleColor),
                FontFamily = CenterClockFontService.CreateFontFamily(CenterClockFontComboBox.SelectedValue as string ?? _centerClockFont),
                FontWeight = FontWeights.SemiBold,
                FontSize = 28,
                Width = innerRadius * 1.5,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(title, centerX - title.Width / 2);
            Canvas.SetTop(title, centerY - 18);
            PreviewCanvas.Children.Add(title);
        }

        private void RenderNeonOrbitPreview(double centerX, double centerY)
        {
            var itemCount = Math.Max(1, _activeItems.Count);
            var orbitRadius = 92.0;
            var nodeRadius = 29.0;

            for (var i = 0; i < itemCount; i++)
            {
                var angle = -90.0 + (360.0 / itemCount) * i;
                var point = PointOnCircle(centerX, centerY, orbitRadius, angle);
                var color = PreviewNeonOrbitPalette[i % PreviewNeonOrbitPalette.Length];
                var selected = i < _activeItems.Count && ReferenceEquals(_activeItems[i], _editingItem);

                var glow = new Ellipse
                {
                    Width = nodeRadius * 2 + 14,
                    Height = nodeRadius * 2 + 14,
                    Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)42 : (byte)18, color.R, color.G, color.B))
                };
                Canvas.SetLeft(glow, point.X - glow.Width / 2);
                Canvas.SetTop(glow, point.Y - glow.Height / 2);
                PreviewCanvas.Children.Add(glow);

                var ring = new Ellipse
                {
                    Width = nodeRadius * 2,
                    Height = nodeRadius * 2,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = selected ? 2.6 : 2.1
                };
                Canvas.SetLeft(ring, point.X - ring.Width / 2);
                Canvas.SetTop(ring, point.Y - ring.Height / 2);
                PreviewCanvas.Children.Add(ring);

                var iconDot = new Ellipse
                {
                    Width = selected ? 10 : 8,
                    Height = selected ? 10 : 8,
                    Fill = new SolidColorBrush(color)
                };
                Canvas.SetLeft(iconDot, point.X - iconDot.Width / 2);
                Canvas.SetTop(iconDot, point.Y - iconDot.Height / 2);
                PreviewCanvas.Children.Add(iconDot);
            }

            var center = new Ellipse
            {
                Width = 64,
                Height = 64,
                Fill = new SolidColorBrush(Color.FromRgb(48, 52, 60)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 230, 232, 236)),
                StrokeThickness = 1.6
            };
            Canvas.SetLeft(center, centerX - center.Width / 2);
            Canvas.SetTop(center, centerY - center.Height / 2);
            PreviewCanvas.Children.Add(center);

            var closeText = new TextBlock
            {
                Text = "X",
                Foreground = new SolidColorBrush(Color.FromArgb(232, 245, 247, 251)),
                FontSize = 22,
                FontWeight = FontWeights.Light,
                Width = 40,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(closeText, centerX - closeText.Width / 2);
            Canvas.SetTop(closeText, centerY - 16);
            PreviewCanvas.Children.Add(closeText);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }

        private static Geometry CreateSegment(double centerX, double centerY, double innerRadius, double outerRadius, double startAngle, double endAngle)
        {
            var startOuter = PointOnCircle(centerX, centerY, outerRadius, startAngle);
            var endOuter = PointOnCircle(centerX, centerY, outerRadius, endAngle);
            var startInner = PointOnCircle(centerX, centerY, innerRadius, endAngle);
            var endInner = PointOnCircle(centerX, centerY, innerRadius, startAngle);
            var largeArc = Math.Abs(endAngle - startAngle) > 180;

            var figure = new PathFigure { StartPoint = startOuter, IsClosed = true };
            figure.Segments.Add(new ArcSegment(endOuter, new Size(outerRadius, outerRadius), 0, largeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(startInner, true));
            figure.Segments.Add(new ArcSegment(endInner, new Size(innerRadius, innerRadius), 0, largeArc, SweepDirection.Counterclockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Point PointOnCircle(double centerX, double centerY, double radius, double angleDegrees)
        {
            var angle = angleDegrees * Math.PI / 180.0;
            return new Point(centerX + Math.Cos(angle) * radius, centerY + Math.Sin(angle) * radius);
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private void PlayUiSound(SoundCue cue)
        {
            _soundManager.Play(cue);
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _capturingShortcutId == null)
            {
                DragMove();
            }
        }

        private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}

