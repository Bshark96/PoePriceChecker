using System;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public static class StatusStyleHelper
    {
        public static void ApplyCorruptedFilterStyles(string option, Button anyBtn, Button yesBtn, Button noBtn)
        {
            if (anyBtn == null || yesBtn == null || noBtn == null) return;

            // Default dark slate state for all buttons
            anyBtn.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed);
            anyBtn.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);

            yesBtn.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed);
            yesBtn.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);

            noBtn.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed);
            noBtn.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);

            if (string.Equals(option, "true", StringComparison.OrdinalIgnoreCase))
            {
                yesBtn.Background = DesignPalette.Brush(Color.FromArgb(255, 127, 29, 29));
                yesBtn.Foreground = DesignPalette.Brush(Color.FromArgb(255, 254, 202, 202));
            }
            else if (string.Equals(option, "false", StringComparison.OrdinalIgnoreCase))
            {
                noBtn.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderExpanded);
                noBtn.Foreground = DesignPalette.Brush(DesignPalette.TextPrimary);
            }
            else
            {
                anyBtn.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderExpanded);
                anyBtn.Foreground = DesignPalette.Brush(DesignPalette.TextPrimary);
            }
        }

        public static void ApplyRarityBadgeStyles(PoeRarity rarity, Border badgeBorder, TextBlock badgeText)
        {
            if (badgeBorder == null || badgeText == null) return;

            switch (rarity)
            {
                case PoeRarity.Gem:
                    badgeBorder.Background = DesignPalette.Brush(Color.FromArgb(255, 15, 118, 110));
                    badgeText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 94, 234, 212));
                    break;
                case PoeRarity.Unique:
                    badgeBorder.Background = DesignPalette.Brush(Color.FromArgb(255, 120, 53, 15));
                    badgeText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 253, 224, 71));
                    break;
                case PoeRarity.Rare:
                    badgeBorder.Background = DesignPalette.Brush(Color.FromArgb(255, 113, 63, 18));
                    badgeText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 254, 240, 138));
                    break;
                case PoeRarity.Magic:
                    badgeBorder.Background = DesignPalette.Brush(Color.FromArgb(255, 30, 58, 138));
                    badgeText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 147, 197, 253));
                    break;
                default:
                    badgeBorder.Background = DesignPalette.Brush(Color.FromArgb(255, 51, 65, 85));
                    badgeText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 226, 232, 240));
                    break;
            }
        }

        public static void ApplyMarketStatusStyle(TextBlock textBlock, string state)
        {
            if (textBlock == null) return;
            switch (state?.ToLowerInvariant())
            {
                case "querying":
                    textBlock.Foreground = DesignPalette.Brush(DesignPalette.AccentCyan);
                    break;
                case "error":
                    textBlock.Foreground = DesignPalette.Brush(Color.FromArgb(255, 248, 113, 113));
                    break;
                case "success":
                    textBlock.Foreground = DesignPalette.Brush(Color.FromArgb(255, 74, 222, 128));
                    break;
                case "warning":
                    textBlock.Foreground = DesignPalette.Brush(DesignPalette.AccentAmber);
                    break;
                default:
                    textBlock.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);
                    break;
            }
        }

        public static void ApplyAwaitingItemStyles(TextBlock nameText, TextBlock subText, TextBlock rarityText, Border rarityBadge)
        {
            if (nameText != null) nameText.Text = "Awaiting Item";
            if (subText != null) subText.Text = "Hover over an item in Path of Exile and press the hotkey (CTRL+D)";
            if (rarityText != null)
            {
                rarityText.Text = "READY";
                rarityText.Foreground = DesignPalette.Brush(Color.FromArgb(255, 74, 222, 128));
            }
            if (rarityBadge != null)
            {
                rarityBadge.Background = DesignPalette.Brush(Color.FromArgb(255, 30, 48, 68));
            }
        }

        public static void ApplyCompactButtonStyles(bool anyExpanded, Button button)
        {
            if (button == null) return;
            if (anyExpanded)
            {
                button.Content = "Collapse All";
                button.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);
                button.BorderBrush = DesignPalette.Brush(Color.FromArgb(255, 51, 65, 85));
                button.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderExpanded);
            }
            else
            {
                button.Content = "Expand All";
                button.Foreground = DesignPalette.Brush(DesignPalette.AccentCyan);
                button.BorderBrush = DesignPalette.Brush(DesignPalette.AccentCyanDark);
                button.Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed);
            }
        }
    }
}
