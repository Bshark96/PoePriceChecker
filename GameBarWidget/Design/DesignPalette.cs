using System;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace GameBarWidget.Design
{
    public static class DesignPalette
    {
        // Backgrounds & Surface Colors
        public static readonly Color SurfaceDark = Color.FromArgb(255, 9, 14, 21);
        public static readonly Color SurfaceCard = Color.FromArgb(255, 18, 32, 48);
        public static readonly Color SurfaceCardHover = Color.FromArgb(255, 26, 44, 66);
        public static readonly Color SurfaceHeaderCollapsed = Color.FromArgb(255, 15, 23, 42);
        public static readonly Color SurfaceHeaderExpanded = Color.FromArgb(255, 30, 41, 59);

        // Borders & Dividers
        public static readonly Color BorderSubtle = Color.FromArgb(255, 34, 56, 82);
        public static readonly Color BorderInput = Color.FromArgb(255, 38, 54, 76);
        public static readonly Color BorderDivider = Color.FromArgb(255, 26, 38, 54);

        // Typography Colors
        public static readonly Color TextPrimary = Color.FromArgb(255, 248, 250, 252);
        public static readonly Color TextSecondary = Color.FromArgb(255, 148, 163, 184);
        public static readonly Color TextMuted = Color.FromArgb(255, 100, 116, 139);

        // Accent Colors
        public static readonly Color AccentCyan = Color.FromArgb(255, 56, 189, 248);
        public static readonly Color AccentCyanDark = Color.FromArgb(255, 14, 116, 144);
        public static readonly Color AccentAmber = Color.FromArgb(255, 251, 191, 36);
        public static readonly Color AccentTeal = Color.FromArgb(255, 45, 212, 191);
        public static readonly Color AccentPurple = Color.FromArgb(255, 192, 132, 252);
        public static readonly Color AccentIndigo = Color.FromArgb(255, 167, 139, 250);
        public static readonly Color AccentRed = Color.FromArgb(255, 248, 113, 113);

        // Brushes
        public static SolidColorBrush Brush(Color color) => new SolidColorBrush(color);

        public static Color GetSectionAccentColor(string sectionTitle)
        {
            if (string.IsNullOrEmpty(sectionTitle)) return AccentCyan;
            return sectionTitle.ToUpperInvariant() switch
            {
                "PSEUDO STATS" => AccentCyan,
                "IMPLICIT & ENCHANT" => AccentIndigo,
                "PREFIXES" => AccentCyan,
                "SUFFIXES" => AccentPurple,
                "GEM PROPERTIES" => AccentTeal,
                "GEM LEVEL" => AccentCyan,
                "EXPLICIT MODIFIERS" => AccentAmber,
                "FRACTURED & CRAFTED" => AccentTeal,
                "SOCKETS" => AccentAmber,
                "QUALITY" => AccentTeal,
                _ => AccentCyan
            };
        }
    }
}
