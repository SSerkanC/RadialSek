using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class ColorPickerWindow : Window
    {
        private static readonly Color[] PresetColors =
        {
            Color.FromRgb(49, 117, 184),
            Color.FromRgb(46, 204, 113),
            Color.FromRgb(241, 196, 15),
            Color.FromRgb(231, 76, 60),
            Color.FromRgb(155, 89, 182),
            Color.FromRgb(26, 188, 156),
            Color.FromRgb(230, 126, 34),
            Color.FromRgb(236, 72, 153),
            Color.FromRgb(96, 165, 250),
            Color.FromRgb(163, 230, 53),
            Color.FromRgb(244, 182, 26),
            Color.FromRgb(120, 132, 255)
        };

        private bool _isUpdating;
        private readonly SoundManager _soundManager = SoundManager.Instance;

        public string SelectedHexColor { get; private set; }
        public bool ResetToThemeRequested { get; private set; }

        public ColorPickerWindow(string initialHexColor, bool hasFixedColor)
        {
            InitializeComponent();
            SelectedHexColor = string.IsNullOrWhiteSpace(initialHexColor) ? "#3175B8" : initialHexColor;
            ResetToThemeButton.IsEnabled = hasFixedColor;
            ResetToThemeButton.Opacity = hasFixedColor ? 1.0 : 0.45;
            ApplyPresets();
            LoadInitialColor();
            Loaded += (_, __) => _soundManager.Play(SoundCue.UiWindowOpen);
        }

        private void ApplyPresets()
        {
            var buttons = new[]
            {
                PresetButton0, PresetButton1, PresetButton2, PresetButton3, PresetButton4,
                PresetButton5, PresetButton6, PresetButton7, PresetButton8, PresetButton9,
                PresetButton10, PresetButton11
            };

            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].Background = new SolidColorBrush(PresetColors[i]);
                buttons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                buttons[i].BorderThickness = new Thickness(1);
                buttons[i].Tag = PresetColors[i];
            }
        }

        private void LoadInitialColor()
        {
            var color = ParseHexColor(SelectedHexColor);
            _isUpdating = true;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            HexInputTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _isUpdating = false;
            UpdatePreview(color);
        }

        private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating)
            {
                return;
            }

            var color = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
            UpdatePreview(color);
        }

        private void OnHexInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating)
            {
                return;
            }

            if (!TryParseHexColor(HexInputTextBox.Text, out var color))
            {
                return;
            }

            _isUpdating = true;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            _isUpdating = false;
            UpdatePreview(color);
        }

        private void OnPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not Color color)
            {
                return;
            }

            _isUpdating = true;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            HexInputTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _isUpdating = false;
            UpdatePreview(color);
            _soundManager.Play(SoundCue.UiSelect);
        }

        private void UpdatePreview(Color color)
        {
            SelectedHexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            PreviewSwatch.Fill = new SolidColorBrush(color);
            HexValueText.Text = SelectedHexColor;
            RedValueText.Text = color.R.ToString();
            GreenValueText.Text = color.G.ToString();
            BlueValueText.Text = color.B.ToString();

            if (!string.Equals(HexInputTextBox.Text, SelectedHexColor, StringComparison.OrdinalIgnoreCase))
            {
                _isUpdating = true;
                HexInputTextBox.Text = SelectedHexColor;
                _isUpdating = false;
            }
        }

        private static Color ParseHexColor(string hex)
        {
            return TryParseHexColor(hex, out var color)
                ? color
                : Color.FromRgb(49, 117, 184);
        }

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.FromRgb(49, 117, 184);

            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var normalized = hex.Trim();
            if (!normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = "#" + normalized;
            }

            try
            {
                var converted = System.Windows.Media.ColorConverter.ConvertFromString(normalized);
                if (converted is Color parsedColor)
                {
                    if (parsedColor.A != 255)
                    {
                        parsedColor = Color.FromRgb(parsedColor.R, parsedColor.G, parsedColor.B);
                    }

                    color = parsedColor;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            _soundManager.Play(SoundCue.Success);
            DialogResult = true;
        }

        private void OnResetToThemeClicked(object sender, RoutedEventArgs e)
        {
            ResetToThemeRequested = true;
            _soundManager.Play(SoundCue.UiSelect);
            DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            _soundManager.Play(SoundCue.UiWindowClose);
            DialogResult = false;
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            _soundManager.Play(SoundCue.UiWindowClose);
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
