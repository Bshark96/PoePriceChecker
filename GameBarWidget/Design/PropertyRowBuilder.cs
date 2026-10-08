using System;
using System.Globalization;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class PropertyRowBuilder
    {
        public static UIElement BuildPropertyRow(
            string label,
            string badgeText,
            Color badgeBg,
            Color badgeFg,
            int? initialMin,
            int? initialMax,
            bool isActive,
            Action<bool> onActiveChanged,
            Action<int?> onMinChanged,
            Action<int?> onMaxChanged,
            Action onFilterChanged,
            Func<PoeItem> getCurrentItem,
            Func<PoeItem, System.Threading.Tasks.Task> queryMarket,
            Action<bool> setTimerPaused,
            Action requestWidgetFocus,
            int maxCap = 100)
        {
            var cardBorder = UiComponentFactory.CreateCardBorder(
                DesignPalette.SurfaceCard,
                DesignPalette.BorderSubtle,
                DesignPalette.SurfaceCardHover);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };

            var cb = new CheckBox
            {
                IsChecked = isActive,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var badge = UiComponentFactory.CreateBadge(badgeText, badgeBg, badgeFg, 7.5);
            leftStack.Children.Add(cb);
            leftStack.Children.Add(badge);

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 9.2,
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(labelText);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // Right: Min - Max inputs
            var inputStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            var minBox = UiComponentFactory.CreateNumericInputBox(initialMin.HasValue ? initialMin.Value.ToString(CultureInfo.InvariantCulture) : string.Empty, "min");
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
                    int cur = 0;
                    int.TryParse(minBox.Text, out cur);
                    cur = (delta > 0) ? cur + 1 : Math.Max(0, cur - 1);
                    minBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    if (onMinChanged != null) onMinChanged(cur);
                    if (onActiveChanged != null) onActiveChanged(true);
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
                if (int.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int customMin))
                {
                    if (onMinChanged != null) onMinChanged(customMin);
                    if (onActiveChanged != null) onActiveChanged(true);
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(minBox.Text))
                {
                    if (onMinChanged != null) onMinChanged(null);
                }
                if (onFilterChanged != null) onFilterChanged();
            };

            var maxBox = UiComponentFactory.CreateNumericInputBox(initialMax.HasValue ? initialMax.Value.ToString(CultureInfo.InvariantCulture) : string.Empty, "max");
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
                    int cur = 0;
                    int.TryParse(maxBox.Text, out cur);
                    cur = (delta > 0) ? cur + 1 : Math.Max(0, cur - 1);
                    maxBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    if (onMaxChanged != null) onMaxChanged(cur);
                    if (onActiveChanged != null) onActiveChanged(true);
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
                if (int.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int customMax))
                {
                    if (onMaxChanged != null) onMaxChanged(customMax);
                    if (onActiveChanged != null) onActiveChanged(true);
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(maxBox.Text))
                {
                    if (onMaxChanged != null) onMaxChanged(null);
                }
                if (onFilterChanged != null) onFilterChanged();
            };

            cb.Checked += (s, e) =>
            {
                if (onActiveChanged != null) onActiveChanged(true);
                if (onFilterChanged != null) onFilterChanged();
            };
            cb.Unchecked += (s, e) =>
            {
                if (onActiveChanged != null) onActiveChanged(false);
                if (onFilterChanged != null) onFilterChanged();
            };

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
