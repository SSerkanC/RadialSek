using System;
using System.Windows;
using RadialSek.Models;

namespace RadialSek.UI
{
    public partial class FeatureToggleWindow : Window
    {
        private string? _capturingShortcutId;
        public MenuFeatures ResultFeatures { get; private set; }
        public ActivationShortcut ResultOpenShortcut { get; private set; }
        public ActivationShortcut ResultToggleShortcut { get; private set; }
        public ActivationShortcut ResultTargetingShortcut { get; private set; }

        public FeatureToggleWindow(
            MenuFeatures features,
            ActivationShortcut openShortcut,
            ActivationShortcut toggleShortcut,
            ActivationShortcut targetingShortcut)
        {
            InitializeComponent();
            ResultFeatures = features.Clone();
            ResultOpenShortcut = openShortcut.Clone();
            ResultToggleShortcut = toggleShortcut.Clone();
            ResultTargetingShortcut = targetingShortcut.Clone();

            OpenShortcutCaptureButton.Content = ResultOpenShortcut.GetDisplayText();
            ToggleShortcutCaptureButton.Content = ResultToggleShortcut.GetDisplayText();
            TargetingShortcutCaptureButton.Content = ResultTargetingShortcut.GetDisplayText();
            StartWithWindowsCheckBox.IsChecked = ResultFeatures.StartWithWindows;
            OpenAnimationCheckBox.IsChecked = ResultFeatures.EnableOpenAnimation;
            HoverLabelsCheckBox.IsChecked = ResultFeatures.ShowHoverLabels;
            CategoryLabelsCheckBox.IsChecked = ResultFeatures.ShowCategoryLabels;
            IconChromeCheckBox.IsChecked = ResultFeatures.ShowIconChrome;
            MonochromeBackdropCheckBox.IsChecked = ResultFeatures.EnableMonochromeBackdrop;
            LightIdleModeCheckBox.IsChecked = ResultFeatures.EnableLightIdleMode;
            LightIdleDelaySlider.Value = ClampLightIdleDelay(ResultFeatures.LightIdleDelaySeconds);
            UpdateLightIdleDelayValueText();
            UpdateLightIdleModeUi();
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            ResultFeatures.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            ResultFeatures.EnableOpenAnimation = OpenAnimationCheckBox.IsChecked == true;
            ResultFeatures.ShowHoverLabels = HoverLabelsCheckBox.IsChecked == true;
            ResultFeatures.ShowCategoryLabels = CategoryLabelsCheckBox.IsChecked == true;
            ResultFeatures.ShowIconChrome = IconChromeCheckBox.IsChecked == true;
            ResultFeatures.EnableMonochromeBackdrop = MonochromeBackdropCheckBox.IsChecked == true;
            ResultFeatures.EnableLightIdleMode = LightIdleModeCheckBox.IsChecked == true;
            ResultFeatures.LightIdleDelaySeconds = ClampLightIdleDelay((int)Math.Round(LightIdleDelaySlider.Value));
            DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnOpenShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.OpenMenuShortcutId;
            OpenShortcutCaptureButton.Content = "Kısayol bekleniyor...";
            System.Windows.Input.Keyboard.Focus(this);
        }

        private void OnToggleShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.ToggleProgramShortcutId;
            ToggleShortcutCaptureButton.Content = "Kısayol bekleniyor...";
            System.Windows.Input.Keyboard.Focus(this);
        }

        private void OnTargetingShortcutCaptureClicked(object sender, RoutedEventArgs e)
        {
            _capturingShortcutId = ActivationShortcut.TargetingModeShortcutId;
            TargetingShortcutCaptureButton.Content = "Kısayol bekleniyor...";
            System.Windows.Input.Keyboard.Focus(this);
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_capturingShortcutId == null)
            {
                return;
            }

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
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

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            {
                e.Handled = true;
                return;
            }

            var shortcut = new ActivationShortcut
            {
                Ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0,
                Alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0,
                Shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0,
                Win = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0,
                Trigger = key.ToString(),
                ShortcutId = _capturingShortcutId
            };

            ApplyCapturedShortcut(shortcut);
            e.Handled = true;
        }

        private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && _capturingShortcutId == null)
            {
                DragMove();
            }
        }

        private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_capturingShortcutId == null)
            {
                return;
            }

            var trigger = e.ChangedButton switch
            {
                System.Windows.Input.MouseButton.Middle => "MiddleMouse",
                System.Windows.Input.MouseButton.Right => "RightMouse",
                System.Windows.Input.MouseButton.XButton1 => "XButton1",
                System.Windows.Input.MouseButton.XButton2 => "XButton2",
                _ => null
            };

            if (trigger == null)
            {
                return;
            }

            var shortcut = new ActivationShortcut
            {
                Ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0,
                Alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0,
                Shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0,
                Win = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0,
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
                ResultToggleShortcut = shortcut;
                ToggleShortcutCaptureButton.Content = ResultToggleShortcut.GetDisplayText();
            }
            else if (shortcut.ShortcutId == ActivationShortcut.TargetingModeShortcutId)
            {
                ResultTargetingShortcut = shortcut;
                TargetingShortcutCaptureButton.Content = ResultTargetingShortcut.GetDisplayText();
            }
            else
            {
                ResultOpenShortcut = shortcut;
                OpenShortcutCaptureButton.Content = ResultOpenShortcut.GetDisplayText();
            }

            _capturingShortcutId = null;
        }

        private ActivationShortcut? CreateTargetingShortcut(System.Windows.Input.Key key)
        {
            var normalizedModifier = key switch
            {
                System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl => "Ctrl",
                System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt => "Alt",
                System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift => "Shift",
                System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin => "Win",
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

            if (key == System.Windows.Input.Key.System)
            {
                return null;
            }

            return new ActivationShortcut
            {
                ShortcutId = ActivationShortcut.TargetingModeShortcutId,
                Ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0,
                Alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0,
                Shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0,
                Win = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0,
                Trigger = key.ToString()
            };
        }

        private void OnLightIdleModeCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            UpdateLightIdleModeUi();
        }

        private void OnLightIdleDelaySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateLightIdleDelayValueText();
        }

        private void UpdateLightIdleDelayValueText()
        {
            if (LightIdleDelayValueTextBlock == null)
            {
                return;
            }

            LightIdleDelayValueTextBlock.Text = $"{ClampLightIdleDelay((int)Math.Round(LightIdleDelaySlider.Value))} sn";
        }

        private void UpdateLightIdleModeUi()
        {
            if (LightIdleDelaySlider == null ||
                LightIdleDelayValueTextBlock == null ||
                LightIdleModeDescriptionTextBlock == null)
            {
                return;
            }

            var isEnabled = LightIdleModeCheckBox.IsChecked == true;
            LightIdleDelaySlider.IsEnabled = isEnabled;
            LightIdleDelayValueTextBlock.Opacity = isEnabled ? 1.0 : 0.5;
            LightIdleModeDescriptionTextBlock.Opacity = isEnabled ? 1.0 : 0.65;
        }

        private static int ClampLightIdleDelay(int seconds)
        {
            return Math.Max(5, Math.Min(60, seconds <= 0 ? 20 : seconds));
        }
    }
}
