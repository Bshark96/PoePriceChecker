using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace GameBarWidget.Design
{
    public static class UiComponentFactory
    {
        public static Border CreateCardBorder(Color bg, Color border, Color hover)
        {
            var cardBorder = new Border
            {
                Background = DesignPalette.Brush(bg),
                BorderBrush = DesignPalette.Brush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 0.5, 4, 0.5),
                Margin = new Thickness(0, 0.5, 0, 0.5)
            };

            cardBorder.PointerEntered += (s, e) =>
            {
                cardBorder.Background = DesignPalette.Brush(hover);
            };
            cardBorder.PointerExited += (s, e) =>
            {
                cardBorder.Background = DesignPalette.Brush(bg);
            };

            return cardBorder;
        }

        public static Border CreateBadge(string text, Color bg, Color fg, double fontSize = 7.5)
        {
            var badge = new Border
            {
                Background = DesignPalette.Brush(bg),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 0.5, 3, 0.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(fg)
            };
            return badge;
        }

        public static void SuppressContextMenu(UIElement element)
        {
            if (element == null) return;
            element.ContextFlyout = null;
            element.ContextRequested += (s, e) => e.Handled = true;
            element.RightTapped += (s, e) => e.Handled = true;

            if (element is TextBox tb)
            {
                tb.ContextFlyout = null;
                tb.ContextMenuOpening += (s, e) => e.Handled = true;
            }
            else if (element is PasswordBox pb)
            {
                pb.ContextFlyout = null;
                pb.ContextMenuOpening += (s, e) => e.Handled = true;
            }
            else if (element is Control ctrl)
            {
                ctrl.ContextFlyout = null;
            }
        }

        public static TextBox CreateNumericInputBox(string initialText, string placeholder)
        {
            var box = new TextBox
            {
                Text = initialText ?? string.Empty,
                PlaceholderText = placeholder,
                Width = 36,
                Height = 16,
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 8.5,
                Padding = new Thickness(2, 0, 2, 0),
                Background = DesignPalette.Brush(DesignPalette.SurfaceDark),
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                BorderBrush = DesignPalette.Brush(DesignPalette.BorderInput),
                CornerRadius = new CornerRadius(2),
                IsTabStop = true,
                IsHitTestVisible = true,
                IsReadOnly = false,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            box.PointerPressed += (s, e) =>
            {
                box.Focus(FocusState.Pointer);
            };

            box.GotFocus += (s, e) =>
            {
                box.SelectAll();
            };

            SuppressContextMenu(box);
            return box;
        }

        public static void AddDivider(StackPanel container)
        {
            if (container == null) return;
            container.Children.Add(new Border
            {
                Height = 1,
                Background = DesignPalette.Brush(DesignPalette.BorderDivider),
                Margin = new Thickness(0, 2, 0, 1)
            });
        }

        public static UIElement CreateGenericGroupSection(
            string title,
            Color accentColor,
            List<UIElement> rows,
            Func<int> getActiveCount,
            HashSet<string> collapsedGroups,
            Action onMasterToggleRequested)
        {
            var sectionPanel = new StackPanel { Spacing = 1 };

            var itemsPanel = new StackPanel { Spacing = 1 };
            foreach (var row in rows)
            {
                itemsPanel.Children.Add(row);
            }

            bool isCollapsed = collapsedGroups != null && collapsedGroups.Contains(title);
            itemsPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;

            // Header Grid
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 3, 0, 2),
                Background = DesignPalette.Brush(Color.FromArgb(0, 0, 0, 0))
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: accent bar + title
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            var indicator = new Border
            {
                Width = 3,
                Height = 10,
                CornerRadius = new CornerRadius(1),
                Background = DesignPalette.Brush(accentColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(indicator);

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(accentColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(titleText);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Right: Count badge + Collapse/Expand Button
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            var countBadge = new Border
            {
                Background = DesignPalette.Brush(Color.FromArgb(255, 20, 30, 44)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            var countText = new TextBlock
            {
                Text = rows.Count.ToString(),
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
            };
            countBadge.Child = countText;
            rightStack.Children.Add(countBadge);

            var toggleBtn = new Button
            {
                FontSize = 7.5,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 0.5, 4, 0.5),
                Height = 18,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Center
            };

            void UpdateButtonVisuals()
            {
                bool collapsed = itemsPanel.Visibility == Visibility.Collapsed;
                int activeInGroup = getActiveCount != null ? getActiveCount() : 0;

                toggleBtn.Content = collapsed ? (activeInGroup > 0 ? $"Expand ({activeInGroup})" : "Expand") : "Collapse";
                toggleBtn.Foreground = collapsed 
                    ? DesignPalette.Brush(DesignPalette.AccentCyan) 
                    : DesignPalette.Brush(DesignPalette.TextSecondary);
                toggleBtn.BorderBrush = collapsed 
                    ? DesignPalette.Brush(DesignPalette.AccentCyanDark) 
                    : DesignPalette.Brush(Color.FromArgb(255, 51, 65, 85));
                toggleBtn.Background = collapsed 
                    ? DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed) 
                    : DesignPalette.Brush(DesignPalette.SurfaceHeaderExpanded);
            }

            UpdateButtonVisuals();
            sectionPanel.Tag = (Action)UpdateButtonVisuals;

            void ToggleGroup()
            {
                if (itemsPanel.Visibility == Visibility.Visible)
                {
                    itemsPanel.Visibility = Visibility.Collapsed;
                    if (collapsedGroups != null) collapsedGroups.Add(title);
                }
                else
                {
                    itemsPanel.Visibility = Visibility.Visible;
                    if (collapsedGroups != null) collapsedGroups.Remove(title);
                }
                UpdateButtonVisuals();
                if (onMasterToggleRequested != null) onMasterToggleRequested();
            }

            toggleBtn.Click += (s, e) => ToggleGroup();
            titleStack.PointerPressed += (s, e) => ToggleGroup();

            rightStack.Children.Add(toggleBtn);
            Grid.SetColumn(rightStack, 1);
            headerGrid.Children.Add(rightStack);

            sectionPanel.Children.Add(headerGrid);
            sectionPanel.Children.Add(itemsPanel);

            return sectionPanel;
        }
    }
}
