using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class ItemCollectionEditorWindow : Window
    {
        private readonly ObservableCollection<MenuItemConfig> _items;
        private readonly SoundManager _soundManager = SoundManager.Instance;
        private MenuItemConfig? _editingItem;
        private bool _isBinding;

        public List<MenuItemConfig> ResultItems => MenuItemCloneService.CloneMany(_items);

        public ItemCollectionEditorWindow(IEnumerable<MenuItemConfig> items, string title)
        {
            InitializeComponent();
            _items = new ObservableCollection<MenuItemConfig>(MenuItemCloneService.CloneMany(items));
            ItemsListBox.ItemsSource = _items;
            TitleTextBlock.Text = title;
            SubtitleTextBlock.Text = title + " içindeki öğeleri düzenleyin.";

            if (_items.Count > 0)
            {
                ItemsListBox.SelectedIndex = 0;
            }

            Loaded += (_, __) => _soundManager.Play(SoundCue.UiWindowOpen);
        }

        private void OnItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _isBinding = true;
            if (ItemsListBox.SelectedItem is MenuItemConfig item)
            {
                _editingItem = item;
                LabelTextBox.Text = item.Label;
                TargetPathTextBox.Text = item.TargetPath;
                StatusTextBlock.Text = "Bir öğe seçildi. Değişiklikler anında kaydedilir.";
            }
            else
            {
                _editingItem = null;
                LabelTextBox.Text = "";
                TargetPathTextBox.Text = "";
            }
            _isBinding = false;
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            var item = new MenuItemConfig { Label = "Yeni Öğe", IsCategory = false };
            _items.Add(item);
            ItemsListBox.SelectedItem = item;
            _soundManager.Play(SoundCue.UiClick);
        }

        private void OnRemoveClicked(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is not MenuItemConfig item)
            {
                return;
            }

            var index = ItemsListBox.SelectedIndex;
            _items.Remove(item);
            if (_items.Count > 0)
            {
                ItemsListBox.SelectedIndex = Math.Max(0, index - 1);
            }
            StatusTextBlock.Text = "Öğe silindi.";
            _soundManager.Play(SoundCue.Warning);
        }

        private void OnMoveUpClicked(object sender, RoutedEventArgs e)
        {
            MoveSelected(-1);
        }

        private void OnMoveDownClicked(object sender, RoutedEventArgs e)
        {
            MoveSelected(1);
        }

        private void MoveSelected(int direction)
        {
            if (ItemsListBox.SelectedItem is not MenuItemConfig item)
            {
                return;
            }

            var oldIndex = _items.IndexOf(item);
            var newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= _items.Count)
            {
                return;
            }

            _items.Move(oldIndex, newIndex);
            ItemsListBox.SelectedItem = item;
            StatusTextBlock.Text = "Sıralama güncellendi.";
            _soundManager.Play(SoundCue.UiSelect);
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
                    _items.Add(newItem);
                    ItemsListBox.SelectedItem = newItem;
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
                    ItemsListBox.Items.Refresh();
                }

                _soundManager.Play(SoundCue.UiSelect);
            }
        }

        private void OnEditorTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isBinding || _editingItem == null) return;
            
            _editingItem.Label = LabelTextBox.Text;
            _editingItem.TargetPath = TargetPathTextBox.Text;
        }
        
        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (_editingItem != null)
            {
                ItemsListBox.Items.Refresh();
            }
        }
        
        private void OnItemsDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnItemsDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
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
                    _items.Add(item);
                }
                
                ItemsListBox.Items.Refresh();
                e.Handled = true;
                _soundManager.Play(SoundCue.MenuDrop);
            }
        }

        private bool ApplyEditor(bool showErrors)
        {
            if (_editingItem != null)
            {
                _editingItem.Label = LabelTextBox.Text.Trim();
                _editingItem.TargetPath = TargetPathTextBox.Text.Trim();
            }
            return true;
        }

        private void OnEditChildrenClicked(object sender, RoutedEventArgs e)
        {
            if (_editingItem == null)
            {
                return;
            }

            ApplyEditor(false);
            var childEditor = new ItemCollectionEditorWindow(_editingItem.Children, _editingItem.Label + " / Alt Menü")
            {
                Owner = this
            };

            if (childEditor.ShowDialog() == true)
            {
                _editingItem.Children = childEditor.ResultItems;
                _editingItem.IsCategory = true;
                ItemsListBox.Items.Refresh();
                StatusTextBlock.Text = "Alt menü güncellendi.";
                _soundManager.Play(SoundCue.UiSelect);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            ApplyEditor(false);
            _soundManager.Play(SoundCue.Success);
            DialogResult = true;
            Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            _soundManager.Play(SoundCue.UiWindowClose);
            Close();
        }
    }
}
