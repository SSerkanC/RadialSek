using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RadialSek.Models;
using RadialSek.Services;
using ShapePath = System.Windows.Shapes.Path;
using WpfPoint = System.Windows.Point;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow
    {
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
    }
}
