using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class LauncherSettingsWindow : Window
    {
        private readonly List<MenuItemConfig> _items;
        private readonly MenuItemConfig? _editingItem;
        private readonly DispatcherTimer _previewTimer;
        private const string DefaultCategoryStripFontColor = "#FAFCFF";
        private string _categoryStripFontColor = DefaultCategoryStripFontColor;
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

        public string SelectedTargetingModeStyle => TargetingModeComboBox.SelectedValue as string ?? "LaserLine";
        public string SelectedMenuStyle => StyleComboBox.SelectedValue as string ?? "Style1";
        public string SelectedTheme => ThemeComboBox.SelectedValue as string ?? "Crimson";
        public string SelectedOpenAnimationStyle => OpenAnimationComboBox.SelectedValue as string ?? "SoftRise";
        public string SelectedCategoryStripStyle => CategoryStripStyleComboBox.SelectedValue as string ?? "GlassBeam";
        public string SelectedCategoryStripFont => CategoryStripFontComboBox.SelectedValue as string ?? "Segoe";
        public double SelectedCategoryStripOpacity => CategoryStripOpacitySlider.Value;
        public double SelectedCategoryStripFontOpacity => CategoryStripFontOpacitySlider.Value;
        public string SelectedCategoryStripFontColor => _categoryStripFontColor;
        public double SelectedInnerGradientRingThicknessScale => InnerGradientRingThicknessSlider.Value;
        public double SelectedOuterGradientRingThicknessScale => OuterGradientRingThicknessSlider.Value;

        public LauncherSettingsWindow(
            IEnumerable<MenuItemConfig> items,
            MenuItemConfig? editingItem,
            string menuStyle,
            string theme,
            string openAnimationStyle,
            string targetingModeStyle,
            string categoryStripStyle,
            string categoryStripFont,
            double categoryStripOpacity,
            double categoryStripFontOpacity,
            string categoryStripFontColor,
            double innerGradientRingThicknessScale,
            double outerGradientRingThicknessScale)
        {
            InitializeComponent();
            _items = MenuItemCloneService.CloneMany(items);
            _editingItem = editingItem != null ? MenuItemCloneService.Clone(editingItem) : null;
            _categoryStripFontColor = string.IsNullOrWhiteSpace(categoryStripFontColor) ? DefaultCategoryStripFontColor : categoryStripFontColor;
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _previewTimer.Tick += (_, __) =>
            {
                _previewTimer.Stop();
                RenderPreview(true);
            };

            TargetingModeComboBox.ItemsSource = TargetingModeStyleService.GetOptions();
            TargetingModeComboBox.SelectedValue = TargetingModeStyleService.ResolveKey(targetingModeStyle);
            StyleComboBox.ItemsSource = MenuStyleService.GetOptions();
            StyleComboBox.SelectedValue = menuStyle;
            ThemeComboBox.ItemsSource = ThemePaletteService.GetPalettes();
            ThemeComboBox.SelectedValue = theme;
            OpenAnimationComboBox.ItemsSource = MenuOpenAnimationService.GetOptions();
            OpenAnimationComboBox.SelectedValue = MenuOpenAnimationService.ResolveKey(openAnimationStyle);
            CategoryStripStyleComboBox.ItemsSource = CategoryStripStyleService.GetOptions();
            CategoryStripStyleComboBox.SelectedValue = CategoryStripStyleService.ResolveKey(categoryStripStyle);
            CategoryStripFontComboBox.ItemsSource = CategoryStripFontService.GetOptions();
            CategoryStripFontComboBox.SelectedValue = CategoryStripFontService.Resolve(categoryStripFont).Key;
            CategoryStripOpacitySlider.Value = Math.Max(0.15, Math.Min(1.0, categoryStripOpacity <= 0 ? 0.98 : categoryStripOpacity));
            CategoryStripOpacityValueText.Text = $"{Math.Round(CategoryStripOpacitySlider.Value * 100):0}%";
            CategoryStripFontOpacitySlider.Value = Math.Max(0.15, Math.Min(1.0, categoryStripFontOpacity <= 0 ? 1.0 : categoryStripFontOpacity));
            CategoryStripFontOpacityValueText.Text = $"{Math.Round(CategoryStripFontOpacitySlider.Value * 100):0}%";
            InnerGradientRingThicknessSlider.Value = Math.Max(0.4, Math.Min(2.5, innerGradientRingThicknessScale <= 0 ? 1.0 : innerGradientRingThicknessScale));
            InnerGradientRingThicknessValueText.Text = FormatScalePercentage(InnerGradientRingThicknessSlider.Value);
            OuterGradientRingThicknessSlider.Value = Math.Max(0.4, Math.Min(2.5, outerGradientRingThicknessScale <= 0 ? 1.0 : outerGradientRingThicknessScale));
            OuterGradientRingThicknessValueText.Text = FormatScalePercentage(OuterGradientRingThicknessSlider.Value);
            UpdateStripFontColorButton();

            TargetingModeComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            StyleComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            ThemeComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            OpenAnimationComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            CategoryStripStyleComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            CategoryStripFontComboBox.SelectionChanged += (_, __) => RequestPreviewRender();
            CategoryStripOpacitySlider.ValueChanged += (_, __) =>
            {
                if (CategoryStripOpacityValueText != null)
                {
                    CategoryStripOpacityValueText.Text = $"{Math.Round(CategoryStripOpacitySlider.Value * 100):0}%";
                }
                RequestPreviewRender();
            };
            CategoryStripFontOpacitySlider.ValueChanged += (_, __) =>
            {
                if (CategoryStripFontOpacityValueText != null)
                {
                    CategoryStripFontOpacityValueText.Text = $"{Math.Round(CategoryStripFontOpacitySlider.Value * 100):0}%";
                }
                RequestPreviewRender();
            };
            InnerGradientRingThicknessSlider.ValueChanged += (_, __) =>
            {
                if (InnerGradientRingThicknessValueText != null)
                {
                    InnerGradientRingThicknessValueText.Text = FormatScalePercentage(InnerGradientRingThicknessSlider.Value);
                }
                RequestPreviewRender();
            };
            OuterGradientRingThicknessSlider.ValueChanged += (_, __) =>
            {
                if (OuterGradientRingThicknessValueText != null)
                {
                    OuterGradientRingThicknessValueText.Text = FormatScalePercentage(OuterGradientRingThicknessSlider.Value);
                }
                RequestPreviewRender();
            };
            PreviewCanvas.SizeChanged += (_, __) => RequestPreviewRender();

            Loaded += (_, __) => RenderPreview(false);
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

        private void RenderPreview(bool animateCard)
        {
            if (PreviewCanvas == null)
            {
                return;
            }

            PreviewCanvas.Children.Clear();
            if (_items.Count == 0)
            {
                return;
            }

            if (animateCard)
            {
                AnimatePreviewCard();
            }

            var width = PreviewCanvas.ActualWidth > 40 ? PreviewCanvas.ActualWidth : 640;
            var height = PreviewCanvas.ActualHeight > 40 ? PreviewCanvas.ActualHeight : 300;
            var centerX = width / 2;
            var centerY = height / 2;
            var style = SelectedMenuStyle;
            var theme = ThemePaletteService.Resolve(SelectedTheme);

            var outerRadius = style == "Style4" ? 128 : style == "Style5" ? 108 : 120;
            var innerRadius = style == "Style3" ? 86 : style == "Style2" ? 74 : 64;
            var totalSweep = style == "Style6" ? 240.0 : 360.0;
            var startAngle = style == "Style6" ? -210.0 : -90.0;
            var gap = style == "Style2" || style == "Style5" ? 0.0 : 3.0;

            if (style == "Style7")
            {
                RenderNeonOrbitPreview(centerX, centerY, theme);
            }
            else
            {
                var backdrop = new Ellipse
                {
                    Width = outerRadius * 2 + 24,
                    Height = outerRadius * 2 + 24,
                    Fill = new SolidColorBrush(Color.FromArgb(18, theme.ShadowColor.R, theme.ShadowColor.G, theme.ShadowColor.B))
                };
                Canvas.SetLeft(backdrop, centerX - backdrop.Width / 2);
                Canvas.SetTop(backdrop, centerY - backdrop.Height / 2);
                PreviewCanvas.Children.Add(backdrop);

                var outerAccentRing = CreatePreviewOuterAccentRing(outerRadius, theme);
                Canvas.SetLeft(outerAccentRing, centerX - outerAccentRing.Width / 2);
                Canvas.SetTop(outerAccentRing, centerY - outerAccentRing.Height / 2);
                PreviewCanvas.Children.Add(outerAccentRing);

                for (var i = 0; i < _items.Count; i++)
                {
                    var segmentStart = startAngle + (totalSweep / _items.Count) * i;
                    var segmentEnd = segmentStart + (totalSweep / _items.Count) - gap;
                    var selected = _editingItem != null && string.Equals(_items[i].Label, _editingItem.Label, StringComparison.Ordinal);

                    var path = new System.Windows.Shapes.Path
                    {
                        Data = CreateSegment(centerX, centerY, innerRadius, outerRadius, segmentStart, segmentEnd),
                        Fill = new SolidColorBrush(selected ? theme.SegmentActiveColor : theme.SegmentColor),
                        Stroke = new SolidColorBrush(theme.SegmentStrokeColor),
                        StrokeThickness = style == "Style5" ? 0.8 : 1.2
                    };
                    PreviewCanvas.Children.Add(path);
                }

                var centerSize = innerRadius * 2 - 8;
                var center = new Ellipse
                {
                    Width = centerSize,
                    Height = centerSize,
                    Fill = new SolidColorBrush(theme.CenterColor),
                    Stroke = new SolidColorBrush(theme.CenterBorderColor),
                    StrokeThickness = 1.4
                };
                Canvas.SetLeft(center, centerX - center.Width / 2);
                Canvas.SetTop(center, centerY - center.Height / 2);
                PreviewCanvas.Children.Add(center);

                var centerAccentRing = CreatePreviewCenterAccentRing(centerSize, theme);
                Canvas.SetLeft(centerAccentRing, centerX - centerAccentRing.Width / 2);
                Canvas.SetTop(centerAccentRing, centerY - centerAccentRing.Height / 2);
                PreviewCanvas.Children.Add(centerAccentRing);
            }

            RenderTargetingStyleBadge(width);
            RenderCategoryStripPreview(width, height, theme);
        }

        private void AnimatePreviewCard()
        {
            PreviewHeroCard.Opacity = 0.86;
            PreviewHeroCard.RenderTransformOrigin = new Point(0.5, 0.5);
            PreviewHeroCard.RenderTransform = new ScaleTransform(0.985, 0.985);

            PreviewHeroCard.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.86,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (PreviewHeroCard.RenderTransform is ScaleTransform scale)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = 0.985,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = 0.985,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }
        }

        private void RenderTargetingStyleBadge(double width)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(42, 79, 139, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 116, 170, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(12, 6, 12, 6),
                Child = new TextBlock
                {
                    Text = "Hedefleme: " + GetStyleDisplayName(SelectedTargetingModeStyle),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                }
            };

            badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(badge, width - badge.DesiredSize.Width - 8);
            Canvas.SetTop(badge, 8);
            PreviewCanvas.Children.Add(badge);
        }

        private static string GetStyleDisplayName(string key)
        {
            foreach (var option in TargetingModeStyleService.GetOptions())
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option.DisplayName;
                }
            }

            return key;
        }

        private void RenderCategoryStripPreview(double width, double height, ThemePalette theme)
        {
            var accent = theme.SegmentActiveColor;
            var stripWidth = 280.0;
            var stripHeight = 42.0;
            var centerY = height - 54.0;
            var x = (width - stripWidth) / 2.0;
            var styleKey = SelectedCategoryStripStyle;

            Brush background = styleKey switch
            {
                "GradientBeam" => new LinearGradientBrush(
                    Color.FromArgb(234, 26, 32, 42),
                    Color.FromArgb(248, accent.R, accent.G, accent.B),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                "NeonRail" => new LinearGradientBrush(
                    Color.FromArgb(110, 255, 255, 255),
                    Color.FromArgb(248, accent.R, accent.G, accent.B),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                "AuroraRibbon" => new LinearGradientBrush(
                    Color.FromArgb(210, 140, accent.G, 255),
                    Color.FromArgb(238, accent.R, accent.G, accent.B),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                "CarbonPulse" => new LinearGradientBrush(
                    Color.FromArgb(242, 18, 20, 24),
                    Color.FromArgb(246, 66, 70, 78),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                "CrystalTag" => new LinearGradientBrush(
                    Color.FromArgb(186, 255, 255, 255),
                    Color.FromArgb(236, accent.R, accent.G, accent.B),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                "LiquidArc" => new LinearGradientBrush(
                    Color.FromArgb(220, accent.R, accent.G, accent.B),
                    Color.FromArgb(255, 255, 255, 255),
                    new Point(0, 0.5),
                    new Point(1, 0.5)),
                _ => new LinearGradientBrush(
                    Color.FromArgb(214, 210, 228, 255),
                    Color.FromArgb(244, accent.R, accent.G, accent.B),
                    new Point(0, 0.5),
                    new Point(1, 0.5))
            };

            var previewHost = new Grid
            {
                Width = stripWidth,
                Height = stripHeight
            };

            var strip = new Border
            {
                Width = stripWidth,
                Height = stripHeight,
                Background = background,
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(stripHeight / 2),
                Opacity = SelectedCategoryStripOpacity
            };

            var stripTextColor = new SolidColorBrush(ParseHexColor(_categoryStripFontColor));
            var stripText = new TextBlock
            {
                Text = CategoryStripFontService.Resolve(SelectedCategoryStripFont).DisplayName,
                Foreground = stripTextColor,
                FontFamily = CategoryStripFontService.CreateFontFamily(SelectedCategoryStripFont),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Opacity = SelectedCategoryStripFontOpacity
            };

            previewHost.Children.Add(strip);
            previewHost.Children.Add(stripText);

            Canvas.SetLeft(previewHost, x);
            Canvas.SetTop(previewHost, centerY);
            PreviewCanvas.Children.Add(previewHost);
        }

        private Ellipse CreatePreviewCenterAccentRing(double centerSize, ThemePalette theme)
        {
            var ringMargin = Math.Max(11, centerSize * 0.07);
            var diameter = centerSize + (ringMargin * 2);
            var accent = BlendColors(theme.CenterBorderColor, theme.SegmentActiveColor, 0.62);
            var softAccent = BlendColors(theme.CenterColor, theme.TitleColor, 0.42);
            var coolAccent = BlendColors(theme.SubtitleColor, Colors.White, 0.25);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(26, accent.R, accent.G, accent.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(232, accent.R, accent.G, accent.B), 0.22));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(208, softAccent.R, softAccent.G, softAccent.B), 0.50));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(224, coolAccent.R, coolAccent.G, coolAccent.B), 0.76));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(22, accent.R, accent.G, accent.B), 1.0));

            return new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Stroke = gradient,
                StrokeThickness = Math.Max(4.4, centerSize * 0.032) * SelectedInnerGradientRingThicknessScale,
                Opacity = 0.92,
                IsHitTestVisible = false
            };
        }

        private Ellipse CreatePreviewOuterAccentRing(double outerRadius, ThemePalette theme)
        {
            var strokeThickness = Math.Max(11.2, outerRadius * 0.056) * SelectedOuterGradientRingThicknessScale;
            var diameter = (outerRadius * 2) + (strokeThickness * 2);
            var accent = BlendColors(theme.SegmentActiveColor, theme.TitleColor, 0.48);
            var softAccent = BlendColors(theme.CenterBorderColor, theme.SubtitleColor, 0.34);
            var glowAccent = BlendColors(theme.SegmentColor, Colors.White, 0.18);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(16, accent.R, accent.G, accent.B), 0.0));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(178, accent.R, accent.G, accent.B), 0.18));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(224, softAccent.R, softAccent.G, softAccent.B), 0.46));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(196, glowAccent.R, glowAccent.G, glowAccent.B), 0.76));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(14, accent.R, accent.G, accent.B), 1.0));

            return new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Stroke = gradient,
                StrokeThickness = strokeThickness,
                Opacity = 0.94,
                IsHitTestVisible = false
            };
        }

        private static Color BlendColors(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            var inverse = 1.0 - amount;
            return Color.FromRgb(
                (byte)Math.Round((from.R * inverse) + (to.R * amount)),
                (byte)Math.Round((from.G * inverse) + (to.G * amount)),
                (byte)Math.Round((from.B * inverse) + (to.B * amount)));
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
        }

        private void UpdateStripFontColorButton()
        {
            if (CategoryStripFontColorSwatch == null)
            {
                return;
            }

            CategoryStripFontColorSwatch.Background = new SolidColorBrush(ParseHexColor(_categoryStripFontColor));
        }

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Color.FromRgb(250, 252, 255);
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is Color color)
                {
                    return Color.FromRgb(color.R, color.G, color.B);
                }
            }
            catch
            {
            }

            return Color.FromRgb(250, 252, 255);
        }

        private void RenderNeonOrbitPreview(double centerX, double centerY, ThemePalette theme)
        {
            var itemCount = Math.Max(1, _items.Count);
            var orbitRadius = 108.0;
            var nodeRadius = 32.0;
            var outerAccentRing = CreatePreviewOuterAccentRing(orbitRadius + nodeRadius, theme);
            Canvas.SetLeft(outerAccentRing, centerX - outerAccentRing.Width / 2);
            Canvas.SetTop(outerAccentRing, centerY - outerAccentRing.Height / 2);
            PreviewCanvas.Children.Add(outerAccentRing);

            for (var i = 0; i < itemCount; i++)
            {
                var angle = -90.0 + (360.0 / itemCount) * i;
                var point = PointOnCircle(centerX, centerY, orbitRadius, angle);
                var color = PreviewNeonOrbitPalette[i % PreviewNeonOrbitPalette.Length];

                var glow = new Ellipse
                {
                    Width = nodeRadius * 2 + 18,
                    Height = nodeRadius * 2 + 18,
                    Fill = new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B))
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
                    StrokeThickness = 2.2
                };
                Canvas.SetLeft(ring, point.X - ring.Width / 2);
                Canvas.SetTop(ring, point.Y - ring.Height / 2);
                PreviewCanvas.Children.Add(ring);
            }

            var center = new Ellipse
            {
                Width = 72,
                Height = 72,
                Fill = new SolidColorBrush(Color.FromRgb(48, 52, 60)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 230, 232, 236)),
                StrokeThickness = 1.6
            };
            Canvas.SetLeft(center, centerX - center.Width / 2);
            Canvas.SetTop(center, centerY - center.Height / 2);
            PreviewCanvas.Children.Add(center);

            var centerAccentRing = CreatePreviewCenterAccentRing(center.Width, theme);
            Canvas.SetLeft(centerAccentRing, centerX - centerAccentRing.Width / 2);
            Canvas.SetTop(centerAccentRing, centerY - centerAccentRing.Height / 2);
            PreviewCanvas.Children.Add(centerAccentRing);
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

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
