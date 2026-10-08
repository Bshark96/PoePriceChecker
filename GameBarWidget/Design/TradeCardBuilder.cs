using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class TradeCardBuilder
    {
        public static Color ResolveHeaderColor(PoeRarity rarity)
        {
            return rarity switch
            {
                PoeRarity.Unique => Color.FromArgb(255, 175, 96, 37),
                PoeRarity.Rare => Color.FromArgb(255, 254, 240, 138),
                PoeRarity.Magic => Color.FromArgb(255, 147, 197, 253),
                PoeRarity.Gem => Color.FromArgb(255, 94, 234, 212),
                PoeRarity.Currency => Color.FromArgb(255, 251, 191, 36),
                _ => Color.FromArgb(255, 241, 245, 249)
            };
        }

        public static Flyout CreateTradeListingFlyout(TradeListing l)
        {
            if (l == null) return null;

            var flyout = new Flyout();
            var scroll = new ScrollViewer
            {
                MaxWidth = 290,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var container = new StackPanel
            {
                Spacing = 4,
                Background = DesignPalette.Brush(Color.FromArgb(255, 11, 18, 27)),
                Padding = new Thickness(10)
            };

            var item = l.FlyoutItem;
            if (item == null)
            {
                if (!string.IsNullOrEmpty(l.RawJson) && Windows.Data.Json.JsonObject.TryParse(l.RawJson, out var rawObj))
                {
                    item = PoeFlyoutParser.ParseTradeItem(rawObj);
                    l.FlyoutItem = item;
                }
                else
                {
                    item = new FlyoutItemModel
                    {
                        Name = !string.IsNullOrEmpty(l.ItemName) ? l.ItemName : "Item on Sale",
                        BaseType = l.ItemBaseType,
                        ItemLevel = l.ItemLevel,
                        IsCorrupted = l.IsCorrupted,
                        SocketsSummary = l.SocketsSummary,
                        PhysicalDps = l.PhysicalDps,
                        ElementalDps = l.ElementalDps,
                        TotalDps = l.TotalDps,
                        Requirements = l.RequirementsSummary ?? new List<string>(),
                        Properties = l.PropertiesSummary ?? new List<string>(),
                        FlavourText = l.FlavourText
                    };
                }
            }

            Color headerColor = ResolveHeaderColor(item.Rarity);

            // Title / Item Name
            string displayName = !string.IsNullOrEmpty(item.Name) ? item.Name : (!string.IsNullOrEmpty(item.BaseType) ? item.BaseType : "Item on Sale");
            var nameBlock = new TextBlock
            {
                Text = displayName,
                FontSize = 12.5,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = DesignPalette.Brush(headerColor),
                TextWrapping = TextWrapping.Wrap
            };
            container.Children.Add(nameBlock);

            if (!string.IsNullOrEmpty(item.BaseType) && !item.BaseType.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
            {
                container.Children.Add(new TextBlock
                {
                    Text = item.BaseType,
                    FontSize = 10,
                    Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
                });
            }

            // Tags row
            var tagStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 4) };
            if (item.ItemLevel > 0)
            {
                tagStack.Children.Add(new TextBlock
                {
                    Text = $"ilvl: {item.ItemLevel}",
                    FontSize = 9,
                    Foreground = DesignPalette.Brush(Color.FromArgb(255, 203, 213, 225))
                });
            }
            if (!string.IsNullOrEmpty(item.SocketsSummary))
            {
                tagStack.Children.Add(new TextBlock
                {
                    Text = item.SocketsSummary,
                    FontSize = 9,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = DesignPalette.Brush(DesignPalette.AccentCyan)
                });
            }
            if (item.IsCorrupted)
            {
                tagStack.Children.Add(UiComponentFactory.CreateBadge("CORRUPTED", Color.FromArgb(255, 75, 20, 20), Color.FromArgb(255, 248, 113, 113), 8));
            }
            if (item.IsSynthesised)
            {
                tagStack.Children.Add(UiComponentFactory.CreateBadge("SYNTHESISED", Color.FromArgb(255, 20, 50, 75), DesignPalette.AccentCyan, 8));
            }
            if (item.IsMirrored)
            {
                tagStack.Children.Add(UiComponentFactory.CreateBadge("MIRRORED", Color.FromArgb(255, 45, 45, 60), Color.FromArgb(255, 203, 213, 225), 8));
            }
            if (tagStack.Children.Count > 0) container.Children.Add(tagStack);

            // Requirements
            if (item.Requirements != null && item.Requirements.Count > 0)
            {
                container.Children.Add(new TextBlock
                {
                    Text = $"Requires {string.Join(", ", item.Requirements)}",
                    FontSize = 8.5,
                    Foreground = DesignPalette.Brush(DesignPalette.TextSecondary),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Divider
            UiComponentFactory.AddDivider(container);

            // Properties
            if (item.Properties != null && item.Properties.Count > 0)
            {
                foreach (var p in item.Properties)
                {
                    container.Children.Add(new TextBlock
                    {
                        Text = p,
                        FontSize = 9,
                        Foreground = DesignPalette.Brush(Color.FromArgb(255, 203, 213, 225))
                    });
                }
            }

            // DPS breakdown if weapon
            if (item.TotalDps > 0 || item.PhysicalDps > 0 || item.ElementalDps > 0)
            {
                UiComponentFactory.AddDivider(container);
                var dpsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (item.PhysicalDps > 0)
                {
                    dpsStack.Children.Add(new TextBlock
                    {
                        Text = $"pDPS: {item.PhysicalDps:0.0}",
                        FontSize = 8.5,
                        Foreground = DesignPalette.Brush(Color.FromArgb(255, 203, 213, 225))
                    });
                }
                if (item.ElementalDps > 0)
                {
                    dpsStack.Children.Add(new TextBlock
                    {
                        Text = $"eDPS: {item.ElementalDps:0.0}",
                        FontSize = 8.5,
                        Foreground = DesignPalette.Brush(DesignPalette.AccentCyan)
                    });
                }
                dpsStack.Children.Add(new TextBlock
                {
                    Text = $"Total DPS: {item.TotalDps:0.0}",
                    FontSize = 8.5,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = DesignPalette.Brush(Color.FromArgb(255, 74, 222, 128))
                });
                container.Children.Add(dpsStack);
            }

            // Divider before mods
            UiComponentFactory.AddDivider(container);

            // Render Modifiers using ModifierRowStyle theme
            if (item.Modifiers != null && item.Modifiers.Count > 0)
            {
                foreach (var mod in item.Modifiers)
                {
                    var modRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 1, 0, 1) };
                    var style = ModifierRowStyle.Resolve(mod);

                    modRow.Children.Add(UiComponentFactory.CreateBadge(style.TierBadgeText, style.TierBadgeBg, style.TierBadgeFg, 7.5));

                    string fullModText = mod.RawText;
                    if (!string.IsNullOrEmpty(mod.MagnitudesText))
                    {
                        fullModText = $"{fullModText} {mod.MagnitudesText}";
                    }

                    var modText = new TextBlock
                    {
                        Text = fullModText,
                        FontSize = 9,
                        Foreground = DesignPalette.Brush(style.TextColor),
                        TextWrapping = TextWrapping.Wrap,
                        Width = 255
                    };
                    modRow.Children.Add(modText);

                    container.Children.Add(modRow);
                }
            }

            // Flavour text
            if (!string.IsNullOrWhiteSpace(item.FlavourText))
            {
                UiComponentFactory.AddDivider(container);
                container.Children.Add(new TextBlock
                {
                    Text = item.FlavourText,
                    FontSize = 8.5,
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = DesignPalette.Brush(Color.FromArgb(255, 217, 119, 6)),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Stash tab note
            if (!string.IsNullOrEmpty(l.StashTabName))
            {
                UiComponentFactory.AddDivider(container);
                container.Children.Add(new TextBlock
                {
                    Text = $"Stash: \"{l.StashTabName}\" (Pos: {l.StashX + 1}, {l.StashY + 1})",
                    FontSize = 8,
                    Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
                });
            }

            // Footer price info
            UiComponentFactory.AddDivider(container);
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            footer.Children.Add(new TextBlock
            {
                Text = $"{l.PriceAmount} {l.PriceCurrency.ToUpperInvariant()}",
                FontSize = 10,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = DesignPalette.Brush(Color.FromArgb(255, 74, 222, 128))
            });
            footer.Children.Add(new TextBlock
            {
                Text = $"@{l.AccountName}",
                FontSize = 9,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
            });
            if (l.IsFaustusInstantTrade)
            {
                footer.Children.Add(UiComponentFactory.CreateBadge("ASYNC", Color.FromArgb(255, 20, 60, 45), Color.FromArgb(255, 74, 222, 128), 8));
            }

            // Raw GGG JSON viewer button
            if (!string.IsNullOrEmpty(l.RawJson))
            {
                var jsonBtn = new Button
                {
                    Content = "Raw GGG JSON",
                    FontSize = 7.5,
                    Height = 18,
                    Padding = new Thickness(3, 0, 3, 0),
                    Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed),
                    Foreground = DesignPalette.Brush(DesignPalette.AccentCyan),
                    BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 30, 58, 138)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };

                var jsonFlyout = new Flyout();
                var jsonScroll = new ScrollViewer { MaxWidth = 360, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
                var jsonText = new TextBox
                {
                    Text = l.RawJson,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 8.5,
                    Background = DesignPalette.Brush(DesignPalette.SurfaceDark),
                    Foreground = DesignPalette.Brush(Color.FromArgb(255, 203, 213, 225)),
                    TextWrapping = TextWrapping.Wrap
                };
                jsonScroll.Content = jsonText;
                jsonFlyout.Content = jsonScroll;
                jsonBtn.Flyout = jsonFlyout;

                footer.Children.Add(jsonBtn);
            }

            container.Children.Add(footer);

            scroll.Content = container;
            flyout.Content = scroll;
            return flyout;
        }
    }
}
