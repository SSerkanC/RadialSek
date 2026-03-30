using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class CategorySymbolPickerWindow : Window
    {
        private string _selectedKey;
        private readonly SoundManager _soundManager = SoundManager.Instance;

        public string SelectedSymbolKey => _selectedKey;

        public CategorySymbolPickerWindow(string currentKey)
        {
            InitializeComponent();
            _selectedKey = CategorySymbolService.NormalizeKey(currentKey);
            BuildSymbolGrid();
            UpdatePreview();
            Loaded += (_, __) => _soundManager.Play(SoundCue.UiWindowOpen);
        }

        private void BuildSymbolGrid()
        {
            SymbolGrid.Children.Clear();
            foreach (var option in CategorySymbolService.GetOptions())
            {
                var card = new Border
                {
                    Width = 176,
                    MinHeight = 228,
                    Margin = new Thickness(0, 0, 12, 12),
                    Padding = new Thickness(14),
                    CornerRadius = new CornerRadius(16),
                    Background = new SolidColorBrush(Color.FromRgb(21, 26, 33)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(43, 51, 64)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = option.Key
                };

                var iconHost = new Border
                {
                    Width = 88,
                    Height = 88,
                    CornerRadius = new CornerRadius(22),
                    Background = new SolidColorBrush(Color.FromRgb(14, 18, 24)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new Grid
                    {
                        Children =
                        {
                            CategorySymbolService.CreateSymbolVisual(option.Key, 48, new SolidColorBrush(Color.FromRgb(236, 191, 84)))
                        }
                    }
                };

                var stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                stack.Children.Add(iconHost);
                stack.Children.Add(new TextBlock
                {
                    Text = option.DisplayName,
                    Margin = new Thickness(0, 10, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 146
                });
                stack.Children.Add(new TextBlock
                {
                    Text = option.Description,
                    Margin = new Thickness(0, 4, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(155, 167, 186)),
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 146
                });

                card.Child = stack;
                card.MouseLeftButtonUp += (_, __) =>
                {
                    _selectedKey = option.Key;
                    UpdatePreview();
                    RefreshSelectionVisuals();
                    _soundManager.Play(SoundCue.UiSelect);
                };

                SymbolGrid.Children.Add(card);
            }

            RefreshSelectionVisuals();
        }

        private void RefreshSelectionVisuals()
        {
            foreach (var child in SymbolGrid.Children)
            {
                if (child is not Border card)
                {
                    continue;
                }

                var isSelected = string.Equals(card.Tag as string, _selectedKey, StringComparison.OrdinalIgnoreCase);
                card.Background = new SolidColorBrush(isSelected ? Color.FromRgb(33, 42, 54) : Color.FromRgb(21, 26, 33));
                card.BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(79, 139, 255) : Color.FromRgb(43, 51, 64));
            }
        }

        private void UpdatePreview()
        {
            var option = CategorySymbolService.GetOptions().FirstOrDefault(x => string.Equals(x.Key, _selectedKey, StringComparison.OrdinalIgnoreCase))
                         ?? CategorySymbolService.GetOptions()[0];

            PreviewIconHost.Children.Clear();
            PreviewIconHost.Children.Add(CategorySymbolService.CreateSymbolVisual(option.Key, 44, new SolidColorBrush(Color.FromRgb(244, 182, 26))));
            PreviewTitleText.Text = option.DisplayName;
            PreviewDescriptionText.Text = option.Description;
            ResetButton.IsEnabled = !string.Equals(_selectedKey, "DefaultGrid", StringComparison.OrdinalIgnoreCase);
            ResetButton.Opacity = ResetButton.IsEnabled ? 1.0 : 0.5;
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            _soundManager.Play(SoundCue.Success);
            DialogResult = true;
        }

        private void OnResetClicked(object sender, RoutedEventArgs e)
        {
            _selectedKey = "DefaultGrid";
            UpdatePreview();
            RefreshSelectionVisuals();
            _soundManager.Play(SoundCue.UiSelect);
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
