using System;
using System.Globalization;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class ModifierRowBuilder
    {
        public static UIElement BuildRow(
            ItemModifier mod,
            Action updateMasterToggle,
            Action<TextBox, ItemModifier, bool, CheckBox> attachQuickRollFlyout,
            Func<PoeItem> getCurrentItem,
            Func<PoeItem, System.Threading.Tasks.Task> queryMarket,
            Action<bool> setTimerPaused,
            Action requestWidgetFocus)
        {
            var style = ModifierRowStyle.Resolve(mod);
            var cardBorder = UiComponentFactory.CreateCardBorder(style.RowBgColor, style.RowBorderColor, style.RowHoverColor);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: CheckBox + Badges + Text
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };

            var cb = new CheckBox
            {
                IsChecked = mod.IsActive,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(style.TierBadgeText))
            {
                leftStack.Children.Add(UiComponentFactory.CreateBadge(style.TierBadgeText, style.TierBadgeBg, style.TierBadgeFg, 7.5));
            }

            if (mod.IsLocal)
            {
                leftStack.Children.Add(UiComponentFactory.CreateBadge("LOCAL", Color.FromArgb(255, 12, 74, 110), Color.FromArgb(255, 186, 230, 253), 7.2));
            }

            var modLabel = new TextBlock
            {
                Text = mod.RawText,
                FontSize = 9.2,
                Foreground = DesignPalette.Brush(style.TextColor),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(modLabel);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            bool showRollInputs = !mod.IsUnscalable && (mod.NumberValue.HasValue || mod.MinRoll.HasValue || mod.MaxRoll.HasValue);
            if (!showRollInputs)
            {
                cb.Checked += (s, e) =>
                {
                    mod.IsActive = true;
                    if (updateMasterToggle != null) updateMasterToggle();
                };
                cb.Unchecked += (s, e) =>
                {
                    mod.IsActive = false;
                    if (updateMasterToggle != null) updateMasterToggle();
                };
                leftStack.Children.Insert(0, cb);
                cardBorder.Child = grid;
                return cardBorder;
            }

            // Right: Dual Min-Max Inputs
            var inputStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            // 1. Min Input Box
            string initialMinText = mod.MinRoll.HasValue ? Math.Abs(mod.MinRoll.Value).ToString(CultureInfo.InvariantCulture) : (mod.NumberValue.HasValue ? Math.Abs(mod.NumberValue.Value).ToString(CultureInfo.InvariantCulture) : "");
            var minBox = UiComponentFactory.CreateNumericInputBox(initialMinText, "min");

            minBox.GotFocus += (s, e) => { if (setTimerPaused != null) setTimerPaused(true); };
            minBox.LostFocus += (s, e) => { if (setTimerPaused != null) setTimerPaused(false); };
            minBox.PointerPressed += (s, e) =>
            {
                if (setTimerPaused != null) setTimerPaused(true);
                if (requestWidgetFocus != null) requestWidgetFocus();
            };
            minBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(minBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    double cur = Math.Abs(mod.MinRoll ?? mod.NumberValue ?? 0);
                    double step = (cur >= 100) ? 5 : ((cur >= 10) ? 1 : 0.5);
                    cur = (delta > 0) ? cur + step : Math.Max(0, cur - step);
                    cur = Math.Abs(cur);
                    minBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    mod.MinRoll = cur;
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
            };
            minBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    if (queryMarket != null && getCurrentItem != null) _ = queryMarket(getCurrentItem());
                }
            };
            minBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double customMin))
                {
                    mod.MinRoll = Math.Abs(customMin);
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(minBox.Text))
                {
                    mod.MinRoll = null;
                }
            };

            // 2. Max Input Box
            string initialMaxText = mod.MaxRoll.HasValue ? Math.Abs(mod.MaxRoll.Value).ToString(CultureInfo.InvariantCulture) : "";
            var maxBox = UiComponentFactory.CreateNumericInputBox(initialMaxText, "max");

            maxBox.GotFocus += (s, e) => { if (setTimerPaused != null) setTimerPaused(true); };
            maxBox.LostFocus += (s, e) => { if (setTimerPaused != null) setTimerPaused(false); };
            maxBox.PointerPressed += (s, e) =>
            {
                if (setTimerPaused != null) setTimerPaused(true);
                if (requestWidgetFocus != null) requestWidgetFocus();
            };
            maxBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(maxBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    double cur = Math.Abs(mod.MaxRoll ?? mod.NumberValue ?? 0);
                    double step = (cur >= 100) ? 5 : ((cur >= 10) ? 1 : 0.5);
                    cur = (delta > 0) ? cur + step : Math.Max(0, cur - step);
                    cur = Math.Abs(cur);
                    maxBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    mod.MaxRoll = cur;
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
            };
            maxBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    if (queryMarket != null && getCurrentItem != null) _ = queryMarket(getCurrentItem());
                }
            };
            maxBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double customMax))
                {
                    mod.MaxRoll = Math.Abs(customMax);
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(maxBox.Text))
                {
                    mod.MaxRoll = null;
                }
            };

            cb.Checked += (s, e) =>
            {
                mod.IsActive = true;
                if (!mod.MinRoll.HasValue && double.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double minV))
                {
                    mod.MinRoll = Math.Abs(minV);
                }
                if (!mod.MaxRoll.HasValue && double.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxV))
                {
                    mod.MaxRoll = Math.Abs(maxV);
                }
                if (updateMasterToggle != null) updateMasterToggle();
            };
            cb.Unchecked += (s, e) =>
            {
                mod.IsActive = false;
                if (updateMasterToggle != null) updateMasterToggle();
            };
            leftStack.Children.Insert(0, cb);

            if (attachQuickRollFlyout != null)
            {
                attachQuickRollFlyout(minBox, mod, true, cb);
                attachQuickRollFlyout(maxBox, mod, false, cb);
            }

            inputStack.Children.Add(minBox);
            inputStack.Children.Add(new TextBlock
            {
                Text = "-",
                FontSize = 8.5,
                Foreground = DesignPalette.Brush(DesignPalette.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            });
            inputStack.Children.Add(maxBox);

            Grid.SetColumn(inputStack, 1);
            grid.Children.Add(inputStack);

            cardBorder.Child = grid;
            return cardBorder;
        }
    }
}
