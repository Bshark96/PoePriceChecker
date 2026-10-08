using System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class TradeRowBuilder
    {
        public static UIElement BuildTradeListingRow(TradeListing l, Action<string> copyWhisperToClipboard)
        {
            if (l == null) return new Grid();

            var row = new Grid
            {
                Padding = new Thickness(4),
                Background = DesignPalette.Brush(Color.FromArgb(255, 24, 35, 51)),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 1, 0, 1)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            // Price display
            string currencyUpper = (l.PriceCurrency ?? string.Empty).ToUpperInvariant();
            string priceLabel = $"{l.PriceAmount:0.##} {currencyUpper}";
            if (currencyUpper.Contains("DIV") || currencyUpper.Contains("CHAOS"))
            {
                priceLabel = $"{l.PriceAmount:0.##} {currencyUpper} (≈{l.PriceInChaos}c)";
            }

            Color priceColor = currencyUpper.Contains("DIV")
                ? Color.FromArgb(255, 74, 222, 128)
                : DesignPalette.AccentAmber;

            var priceText = new TextBlock
            {
                Text = priceLabel,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = DesignPalette.Brush(priceColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(priceText);

            if (l.IsFaustusInstantTrade)
            {
                leftStack.Children.Add(UiComponentFactory.CreateBadge("FAUSTUS", Color.FromArgb(255, 120, 53, 15), Color.FromArgb(255, 253, 224, 71), 8));
            }

            var accountText = new TextBlock
            {
                Text = $"@{l.AccountName}",
                FontSize = 9,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(accountText);

            string myAccount = PoeSettingsManager.Instance.AccountName;
            if (!string.IsNullOrWhiteSpace(myAccount) && !string.IsNullOrWhiteSpace(l.AccountName) &&
                (l.AccountName.IndexOf(myAccount, StringComparison.OrdinalIgnoreCase) >= 0 || myAccount.IndexOf(l.AccountName, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                leftStack.Children.Add(UiComponentFactory.CreateBadge("YOUR LISTING", Color.FromArgb(255, 30, 58, 138), Color.FromArgb(255, 147, 197, 253), 8));
            }

            Grid.SetColumn(leftStack, 0);
            row.Children.Add(leftStack);

            // Action buttons
            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

            // 1. Primary Action: "To Hideout" or "Whisper"
            string actionLabel = (l.IsFaustusInstantTrade || !string.IsNullOrEmpty(l.HideoutToken))
                ? (l.GoldFee > 0 ? $"To Hideout ({l.GoldFee}g)" : "To Hideout")
                : "Whisper";

            Color actionBg = l.IsFaustusInstantTrade ? Color.FromArgb(255, 22, 44, 32) : Color.FromArgb(255, 30, 48, 68);
            Color actionFg = l.IsFaustusInstantTrade ? Color.FromArgb(255, 74, 222, 128) : Color.FromArgb(255, 226, 232, 240);
            Color actionBorder = l.IsFaustusInstantTrade ? Color.FromArgb(255, 34, 197, 94) : Color.FromArgb(255, 45, 69, 96);

            var actionBtn = new Button
            {
                Content = actionLabel,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(5, 2, 5, 2),
                Background = DesignPalette.Brush(actionBg),
                Foreground = DesignPalette.Brush(actionFg),
                BorderBrush = DesignPalette.Brush(actionBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };

            actionBtn.Click += async (s, e) =>
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
                            actionBtn.Content = "Teleported!";
                        }
                        else
                        {
                            var whisperRes = await PoeOfficialTradeClient.Instance.SendDirectWhisperTokenAsync(token, PoeSettingsManager.Instance.PoeSessionId);
                            if (whisperRes.success)
                            {
                                apiSent = true;
                                actionBtn.Content = "Sent!";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(l.WhisperString) && copyWhisperToClipboard != null)
                    {
                        copyWhisperToClipboard(l.WhisperString);
                    }

                    if (!apiSent)
                    {
                        actionBtn.Content = "Copied!";
                    }
                }
                catch
                {
                    if (!string.IsNullOrEmpty(l.WhisperString) && copyWhisperToClipboard != null)
                    {
                        copyWhisperToClipboard(l.WhisperString);
                    }
                    actionBtn.Content = "Copied!";
                }
            };
            btnStack.Children.Add(actionBtn);

            // 2. Second Action: "Preview" Item on Sale
            var previewBtn = new Button
            {
                Content = "Preview",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(5, 2, 5, 2),
                Background = DesignPalette.Brush(Color.FromArgb(255, 24, 38, 58)),
                Foreground = DesignPalette.Brush(DesignPalette.AccentCyan),
                BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 38, 70, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };

            var itemFlyout = TradeCardBuilder.CreateTradeListingFlyout(l);
            FlyoutBase.SetAttachedFlyout(previewBtn, itemFlyout);
            previewBtn.Click += (s, e) =>
            {
                FlyoutBase.ShowAttachedFlyout(previewBtn);
            };
            btnStack.Children.Add(previewBtn);

            Grid.SetColumn(btnStack, 1);
            row.Children.Add(btnStack);

            return row;
        }
    }
}
