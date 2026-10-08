using System;
using Windows.UI;
using GameBarWidget.Services;

namespace GameBarWidget.Design
{
    public sealed class ModifierRowStyle
    {
        public Color RowBgColor { get; set; }
        public Color RowBorderColor { get; set; }
        public Color RowHoverColor { get; set; }
        public Color TextColor { get; set; }
        public string TierBadgeText { get; set; } = string.Empty;
        public Color TierBadgeBg { get; set; }
        public Color TierBadgeFg { get; set; }

        public static ModifierRowStyle Resolve(ItemModifier mod)
        {
            if (mod == null)
            {
                return DefaultStyle();
            }

            if (mod.IsPrefix || (mod.TierInfo != null && mod.TierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase)))
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 22, 35, 52),      // Navy/Azure tint
                    RowBorderColor = Color.FromArgb(255, 38, 64, 94),
                    RowHoverColor = Color.FromArgb(255, 30, 48, 70),
                    TextColor = Color.FromArgb(255, 241, 245, 249),
                    TierBadgeText = !string.IsNullOrEmpty(mod.TierInfo) ? mod.TierInfo : "P",
                    TierBadgeBg = Color.FromArgb(255, 25, 55, 88),
                    TierBadgeFg = Color.FromArgb(255, 56, 189, 248)
                };
            }

            if (mod.IsSuffix || (mod.TierInfo != null && mod.TierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase)))
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 36, 28, 54),      // Purple/Violet tint
                    RowBorderColor = Color.FromArgb(255, 62, 48, 92),
                    RowHoverColor = Color.FromArgb(255, 48, 38, 72),
                    TextColor = Color.FromArgb(255, 241, 245, 249),
                    TierBadgeText = !string.IsNullOrEmpty(mod.TierInfo) ? mod.TierInfo : "S",
                    TierBadgeBg = Color.FromArgb(255, 52, 38, 86),
                    TierBadgeFg = Color.FromArgb(255, 192, 132, 252)
                };
            }

            if (mod.Type == ModifierType.Implicit || mod.Type == ModifierType.Enchant)
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 26, 32, 52),      // Indigo/Slate tint
                    RowBorderColor = Color.FromArgb(255, 48, 58, 92),
                    RowHoverColor = Color.FromArgb(255, 36, 44, 70),
                    TextColor = Color.FromArgb(255, 196, 181, 253),
                    TierBadgeText = mod.Type == ModifierType.Enchant ? "ENC" : "IMP",
                    TierBadgeBg = Color.FromArgb(255, 42, 38, 74),
                    TierBadgeFg = Color.FromArgb(255, 167, 139, 250)
                };
            }

            if (mod.Type == ModifierType.Fractured)
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 44, 34, 20),      // Amber tint
                    RowBorderColor = Color.FromArgb(255, 80, 62, 32),
                    RowHoverColor = Color.FromArgb(255, 58, 46, 26),
                    TextColor = Color.FromArgb(255, 254, 240, 138),
                    TierBadgeText = "FRAC",
                    TierBadgeBg = Color.FromArgb(255, 68, 50, 20),
                    TierBadgeFg = Color.FromArgb(255, 251, 191, 36)
                };
            }

            if (mod.Type == ModifierType.Crafted)
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 19, 42, 39),      // Teal tint
                    RowBorderColor = Color.FromArgb(255, 34, 78, 72),
                    RowHoverColor = Color.FromArgb(255, 26, 56, 52),
                    TextColor = Color.FromArgb(255, 153, 246, 228),
                    TierBadgeText = "CRAFT",
                    TierBadgeBg = Color.FromArgb(255, 18, 62, 56),
                    TierBadgeFg = Color.FromArgb(255, 45, 212, 191)
                };
            }

            if (mod.IsPseudo)
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 20, 36, 50),      // Cyan tint
                    RowBorderColor = Color.FromArgb(255, 34, 64, 88),
                    RowHoverColor = Color.FromArgb(255, 28, 50, 68),
                    TextColor = Color.FromArgb(255, 125, 211, 252),
                    TierBadgeText = "PSEUDO",
                    TierBadgeBg = Color.FromArgb(255, 24, 58, 92),
                    TierBadgeFg = Color.FromArgb(255, 56, 189, 248)
                };
            }

            if (mod.TierInfo != null && mod.TierInfo.Equals("UNI", StringComparison.OrdinalIgnoreCase))
            {
                return new ModifierRowStyle
                {
                    RowBgColor = Color.FromArgb(255, 36, 26, 16),      // Unique Gold/Amber tint
                    RowBorderColor = Color.FromArgb(255, 76, 52, 24),
                    RowHoverColor = Color.FromArgb(255, 48, 36, 20),
                    TextColor = Color.FromArgb(255, 254, 240, 138),
                    TierBadgeText = "UNI",
                    TierBadgeBg = Color.FromArgb(255, 75, 45, 15),
                    TierBadgeFg = Color.FromArgb(255, 250, 204, 21)
                };
            }

            return DefaultStyle(mod);
        }

        private static ModifierRowStyle DefaultStyle(ItemModifier mod = null)
        {
            return new ModifierRowStyle
            {
                RowBgColor = Color.FromArgb(255, 28, 38, 52),      // Default Explicit tinted slate
                RowBorderColor = Color.FromArgb(255, 44, 60, 82),
                RowHoverColor = Color.FromArgb(255, 36, 48, 66),
                TextColor = Color.FromArgb(255, 226, 232, 240),
                TierBadgeText = (mod != null && !string.IsNullOrEmpty(mod.TierInfo)) ? mod.TierInfo : "EXP",
                TierBadgeBg = Color.FromArgb(255, 15, 23, 42),
                TierBadgeFg = Color.FromArgb(255, 148, 163, 184)
            };
        }
    }
}
