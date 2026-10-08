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
            Action<string> copyWhisper,
            Action dismissAction = null)
        {
            if (l == null) return new Grid();

            // Root Grid allowing badge to overlap top border line at exact midpoint
            var rootGrid = new Grid();

            // 1. OUTER CARD CONTAINER (Top margin offset at y=18 so badge intersects top border line)
            var cardBorder = new Border
            {
                Background = DesignPalette.Brush(Color.FromArgb(255, 3, 14, 22)),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 0, 230, 118)), // Bright Neon Green
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 22, 16, 14),
                Margin = new Thickness(4, 18, 4, 4)
            };

            var mainStack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };

            // 2. CENTER ITEM NAME & BASE TYPE
            string qTitle = query != null && !string.IsNullOrEmpty(query.Label) ? query.Label : "Live Search Item";
            string itemName = !string.IsNullOrEmpty(l.ItemName) ? l.ItemName : (!string.IsNullOrEmpty(l.ItemBaseType) ? l.ItemBaseType : qTitle);
            string baseType = l.ItemBaseType;

            var titleStack = new StackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };

            var nameTextBlock = new TextBlock
            {
                Text = itemName,
                FontSize = 24,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 254, 240, 138)), // Cream off-white
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            titleStack.Children.Add(nameTextBlock);

            if (!string.IsNullOrEmpty(baseType) && !string.Equals(itemName, baseType, StringComparison.OrdinalIgnoreCase))
            {
                var baseTextBlock = new TextBlock
                {
                    Text = baseType,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = DesignPalette.Brush(Color.FromArgb(255, 254, 240, 138)),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                titleStack.Children.Add(baseTextBlock);
            }

            mainStack.Children.Add(titleStack);

            // 3. MASSIVE CENTERED PRICE DISPLAY
            string currencyUpper = (l.PriceCurrency ?? "chaos").ToUpperInvariant();
            string priceString = $"{l.PriceAmount:0.##} {currencyUpper} (≈{l.PriceInChaos:0.##}c)";

            var priceTextBlock = new TextBlock
            {
                Text = priceString,
                FontSize = 28,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 245, 158, 11)), // Golden Amber Yellow
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
            mainStack.Children.Add(priceTextBlock);

            // 4. SELLER ACCOUNT TAG
            string sellerTag = !string.IsNullOrEmpty(l.AccountName) ? $"@{l.AccountName}" : "@Exile";
            var sellerTextBlock = new TextBlock
            {
                Text = sellerTag,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 148, 163, 184)), // Muted Slate Light Blue
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainStack.Children.Add(sellerTextBlock);

            // 5. BOTTOM ACTION BUTTON GRID (TO HIDEOUT / WHISPER & DISMISS)
            var actionGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacing
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Button 1: TO HIDEOUT or WHISPER
            string primaryLabel = (l.IsFaustusInstantTrade || !string.IsNullOrEmpty(l.HideoutToken))
                ? (l.GoldFee > 0 ? $"TO HIDEOUT ({l.GoldFee}g)" : "TO HIDEOUT")
                : "WHISPER";

            var primaryBtn = new Button
            {
                Content = primaryLabel,
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Height = 42,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = DesignPalette.Brush(Color.FromArgb(255, 4, 47, 26)),
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 0, 230, 118)),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 0, 230, 118)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6)
            };

            primaryBtn.Click += async (s, e) =>
            {
                try
                {
                    bool apiSent = false;
                    string token = !string.IsNullOrEmpty(l.HideoutToken) ? l.HideoutToken : l.WhisperToken;

                    if (!string.IsNullOrEmpty(token))
                    {
                        var hideoutRes = await PoeOfficialTradeClient.Instance.SendDirectHideoutTokenAsync(token, PoeSettingsManager.Instance.PoeSessionId);
                        if (hideoutRes.success)
                        {
                            apiSent = true;
                            primaryBtn.Content = "TELEPORTED!";
                        }
                        else
                        {
                            var whisperRes = await PoeOfficialTradeClient.Instance.SendDirectWhisperTokenAsync(token, PoeSettingsManager.Instance.PoeSessionId);
                            if (whisperRes.success)
                            {
                                apiSent = true;
                                primaryBtn.Content = "SENT!";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(l.WhisperString) && copyWhisper != null)
                    {
                        copyWhisper(l.WhisperString);
                    }

                    if (!apiSent)
                    {
                        primaryBtn.Content = "COPIED!";
                    }
                }
                catch
                {
                    if (!string.IsNullOrEmpty(l.WhisperString) && copyWhisper != null)
                    {
                        copyWhisper(l.WhisperString);
                    }
                    primaryBtn.Content = "COPIED!";
                }
            };

            Grid.SetColumn(primaryBtn, 0);
            actionGrid.Children.Add(primaryBtn);

            // Button 2: DISMISS
            var dismissBtn = new Button
            {
                Content = "DISMISS",
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Height = 42,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = DesignPalette.Brush(Color.FromArgb(255, 63, 15, 23)),
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 248, 113, 113)),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 239, 68, 68)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6)
            };

            dismissBtn.Click += (s, e) =>
            {
                dismissAction?.Invoke();
            };

            Grid.SetColumn(dismissBtn, 2);
            actionGrid.Children.Add(dismissBtn);

            mainStack.Children.Add(actionGrid);

            cardBorder.Child = mainStack;
            rootGrid.Children.Add(cardBorder);

            // 6. TOP CENTERED LIVE ALERT BADGE (Positioned directly over top border line)
            var badgeBorder = new Border
            {
                Background = DesignPalette.Brush(Color.FromArgb(255, 3, 14, 22)), // Matches card inner background to cut through top border
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 0, 230, 118)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(28, 4, 28, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var badgeText = new TextBlock
            {
                Text = "LIVE ALERT",
                FontSize = 22,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 0, 255, 136)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            badgeBorder.Child = badgeText;

            rootGrid.Children.Add(badgeBorder);

            return rootGrid;
        }
    }
}
