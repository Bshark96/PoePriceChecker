using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class LiveSearchCardBuilder
    {
        public static UIElement BuildActiveQueryRow(
            PoeLiveSearchQuery query,
            Action<PoeLiveSearchQuery, bool> onToggleActive,
            Action<PoeLiveSearchQuery> onDelete)
        {
            if (query == null) return new Grid();

            var card = UiComponentFactory.CreateCardBorder(
                DesignPalette.SurfaceCard,
                DesignPalette.BorderSubtle,
                DesignPalette.SurfaceCardHover);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

            var cb = new CheckBox
            {
                IsChecked = query.IsActive,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.Click += (s, e) =>
            {
                bool isChecked = cb.IsChecked == true;
                query.IsActive = isChecked;
                onToggleActive?.Invoke(query, isChecked);
            };
            leftStack.Children.Add(cb);

            string title = !string.IsNullOrWhiteSpace(query.Label) ? query.Label : $"{query.League}/{query.SearchId}";
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(titleText);

            if (query.MaxPriceAmount.HasValue && query.MaxPriceAmount.Value > 0)
            {
                string priceBadge = $"Max {query.MaxPriceAmount.Value:0.##} {query.MaxPriceCurrency?.ToUpperInvariant()}";
                leftStack.Children.Add(UiComponentFactory.CreateBadge(priceBadge, Color.FromArgb(255, 20, 50, 40), Color.FromArgb(255, 74, 222, 128), 7.5));
            }

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            var deleteBtn = new Button
            {
                Content = "Remove",
                FontSize = 8.5,
                Padding = new Thickness(5, 1, 5, 1),
                Background = DesignPalette.Brush(Color.FromArgb(255, 60, 20, 25)),
                Foreground = DesignPalette.Brush(DesignPalette.AccentRed),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 120, 30, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center
            };
            UiComponentFactory.SuppressContextMenu(deleteBtn);
            deleteBtn.Click += (s, e) => onDelete?.Invoke(query);

            Grid.SetColumn(deleteBtn, 1);
            grid.Children.Add(deleteBtn);

            card.Child = grid;
            return card;
        }

        public static UIElement BuildLiveListingNotificationCard(
            PoeLiveSearchQuery query,
            TradeListing l,
            Action<string> copyWhisper)
        {
            if (l == null) return new Grid();

            var card = new Border
            {
                Background = DesignPalette.Brush(Color.FromArgb(255, 18, 32, 28)),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 34, 197, 94)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 2)
            };

            var mainStack = new StackPanel { Spacing = 3 };

            // Header line: LIVE ALERT badge + Query label + Age
            var topHeader = new Grid();
            topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tagStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            tagStack.Children.Add(UiComponentFactory.CreateBadge("LIVE ALERT", Color.FromArgb(255, 22, 101, 52), Color.FromArgb(255, 74, 222, 128), 8));

            string qTitle = query != null && !string.IsNullOrEmpty(query.Label) ? query.Label : "Live Search Match";
            tagStack.Children.Add(new TextBlock
            {
                Text = qTitle,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });

            Grid.SetColumn(tagStack, 0);
            topHeader.Children.Add(tagStack);

            var timeText = new TextBlock
            {
                Text = l.AgeText ?? "Just now",
                FontSize = 8.5,
                Foreground = DesignPalette.Brush(DesignPalette.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeText, 1);
            topHeader.Children.Add(timeText);

            mainStack.Children.Add(topHeader);

            // Listing details row using TradeRowBuilder
            var tradeRow = TradeRowBuilder.BuildTradeListingRow(l, copyWhisper);
            mainStack.Children.Add(tradeRow);

            card.Child = mainStack;
            return card;
        }
    }
}
