using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GameBarWidget.Services
{
    public static class PoeModifierParser
    {
        public static readonly Regex NumberRegex = new Regex(@"([+-]?\d+(?:\.\d+)?)", RegexOptions.Compiled);
        public static readonly Regex DigitsOnlyRegex = new Regex(@"(\d+(?:\.\d+)?)", RegexOptions.Compiled);
        public static readonly Regex AddsDamageRegex = new Regex(@"^Adds\s+(\d+)\s+to\s+(\d+)\s+(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex RangeDamageRegex = new Regex(@"(\d+)[-–](\d+)", RegexOptions.Compiled);
        public static readonly Regex AdvancedModHeaderRegex = new Regex(@"^\{.*\}", RegexOptions.Compiled);
        public static readonly Regex RangeBracketsRegex = new Regex(@"\s*\(([+-]?\d+(?:\.\d+)?)[-–]([+-]?\d+(?:\.\d+)?)\)", RegexOptions.Compiled);
        public static readonly Regex TierRegex = new Regex(@"(?:Tier:\s*(\d+)|Rank:\s*(\d+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ItemModifier ParseModifier(string line, ModifierType headerType, string tierInfo, string category)
        {
            var mod = new ItemModifier { Type = headerType, TierInfo = tierInfo };

            // Detect if modifier is unscalable
            bool isUnscalable = line.IndexOf("unscalable", StringComparison.OrdinalIgnoreCase) >= 0;
            mod.IsUnscalable = isUnscalable;

            // Strip unscalable suffix and annotations (handling all dashes: em-dash, en-dash, ASCII hyphen, etc.)
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*\((?:unscalable(?:\s+value)?)\)", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();

            if (Regex.IsMatch(line, @"\(implicit\)", RegexOptions.IgnoreCase))
            {
                mod.Type = ModifierType.Implicit;
                line = Regex.Replace(line, @"\s*\(implicit\)", "", RegexOptions.IgnoreCase).Trim();
            }
            else if (Regex.IsMatch(line, @"\(fractured\)", RegexOptions.IgnoreCase))
            {
                mod.Type = ModifierType.Fractured;
                line = Regex.Replace(line, @"\s*\(fractured\)", "", RegexOptions.IgnoreCase).Trim();
            }
            else if (Regex.IsMatch(line, @"\(crafted\)", RegexOptions.IgnoreCase))
            {
                mod.Type = ModifierType.Crafted;
                line = Regex.Replace(line, @"\s*\(crafted\)", "", RegexOptions.IgnoreCase).Trim();
            }
            else if (Regex.IsMatch(line, @"\(enchant\)", RegexOptions.IgnoreCase))
            {
                mod.Type = ModifierType.Enchant;
                line = Regex.Replace(line, @"\s*\(enchant\)", "", RegexOptions.IgnoreCase).Trim();
            }

            if (string.IsNullOrEmpty(mod.TierInfo) ||
                (mod.Type == ModifierType.Implicit && mod.TierInfo != "IMP" && mod.TierInfo != "CORRUPT") ||
                (mod.Type == ModifierType.Enchant && mod.TierInfo != "ENC") ||
                (mod.Type == ModifierType.Crafted && mod.TierInfo != "CRAFT"))
            {
                if (mod.Type == ModifierType.Implicit) mod.TierInfo = "IMP";
                else if (mod.Type == ModifierType.Enchant) mod.TierInfo = "ENC";
                else if (mod.Type == ModifierType.Crafted) mod.TierInfo = "CRAFT";
                else if (mod.Type == ModifierType.Fractured && string.IsNullOrEmpty(mod.TierInfo)) mod.TierInfo = "FRAC";
            }

            line = Regex.Replace(line, @"\s*\((?:augmented|unscalable(?:\s+value)?|synthesised|crucible|scourge)\)", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();

            // Set clean RawText (without unscalable or tag annotations)
            mod.RawText = line;

            string clean = RangeBracketsRegex.Replace(line, "").Trim();
            clean = Regex.Replace(clean, @"\s*to\s*$", "").Trim();

            var addsMatch = AddsDamageRegex.Match(clean);
            if (addsMatch.Success)
            {
                double.TryParse(addsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double minV);
                double.TryParse(addsMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxV);
                minV = Math.Abs(minV);
                maxV = Math.Abs(maxV);
                if (minV > maxV) { double tmp = minV; minV = maxV; maxV = tmp; }
                string dt = addsMatch.Groups[3].Value.Trim();
                double avg = Math.Round((minV + maxV) / 2.0, 1);

                mod.Template = $"Adds # to # {dt}";

                if (!mod.IsUnscalable)
                {
                    mod.NumberValue = avg;
                    mod.MinRoll = ApplyTolerance(avg);
                    mod.MaxRoll = null;
                }

                mod.StatId = PoeStatsDatabase.LookupScopedStatId(mod.Template, mod.Type, clean, category);
            }
            else
            {
                string template = DigitsOnlyRegex.Replace(clean, "#");
                if (template.StartsWith("-"))
                {
                    template = "+" + template.Substring(1);
                }
                mod.Template = template;
                mod.StatId = PoeStatsDatabase.LookupScopedStatId(template, mod.Type, clean, category);

                if (!mod.IsUnscalable)
                {
                    var match = NumberRegex.Match(clean);
                    if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rawVal))
                    {
                        double val = Math.Abs(rawVal); // Treat negative values as positive
                        mod.NumberValue = val;
                        mod.MinRoll = ApplyTolerance(val);
                        mod.MaxRoll = null;
                    }
                }
            }

            if (!string.IsNullOrEmpty(tierInfo))
            {
                if (tierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase)) mod.IsPrefix = true;
                else if (tierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase)) mod.IsSuffix = true;
            }
            else if (mod.Type == ModifierType.Explicit)
            {
                string cl = clean.ToLowerInvariant();
                if (cl.Contains("resistance") || cl.Contains("to strength") || cl.Contains("to dexterity") || cl.Contains("to intelligence") || cl.Contains("attack speed") || cl.Contains("cast speed") || cl.Contains("critical strike") || cl.Contains("accuracy rating"))
                {
                    mod.IsSuffix = true; mod.TierInfo = "Suffix";
                }
                else if (cl.Contains("maximum life") || cl.Contains("maximum mana") || cl.Contains("maximum energy shield") || cl.Contains("damage") || cl.Contains("armour") || cl.Contains("evasion"))
                {
                    mod.IsPrefix = true; mod.TierInfo = "Prefix";
                }
            }

            // Determine Local vs Global context
            bool isWeapon = (!string.IsNullOrEmpty(category) && (category.StartsWith("weapon.", StringComparison.OrdinalIgnoreCase) || category.Equals("weapon", StringComparison.OrdinalIgnoreCase)))
                || (category != null && category.Contains("weapon"));
            bool isArmour = (!string.IsNullOrEmpty(category) && (category.StartsWith("armour.", StringComparison.OrdinalIgnoreCase) || category.Equals("armour", StringComparison.OrdinalIgnoreCase)))
                || (category != null && category.Contains("armour"));

            if (mod.Type == ModifierType.Explicit || mod.Type == ModifierType.Fractured || mod.Type == ModifierType.Crafted)
            {
                string tm = (mod.Template ?? string.Empty).ToLowerInvariant();
                string cl = clean.ToLowerInvariant();
                if (isWeapon)
                {
                    if (tm.Contains("adds # to #") || cl.Contains("adds ") ||
                        tm.Contains("attack speed") || tm.Contains("critical strike") || tm.Contains("physical damage") || tm.Contains("accuracy rating"))
                    {
                        mod.IsLocal = true;
                    }
                }
                else if (isArmour)
                {
                    if (tm.Contains("to armour") || tm.Contains("to evasion rating") || tm.Contains("to energy shield") || tm.Contains("to ward") ||
                        tm.Contains("increased armour") || tm.Contains("increased evasion rating") || tm.Contains("increased energy shield") || tm.Contains("increased ward"))
                    {
                        mod.IsLocal = true;
                    }
                }
            }

            mod.IsActive = !string.IsNullOrEmpty(mod.StatId);
            return mod;
        }

        public static bool IsModifierLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string lower = line.ToLowerInvariant();
            if (lower.StartsWith("item class:") || lower.StartsWith("rarity:") || lower.StartsWith("requirements:") ||
                lower.StartsWith("level:") || lower.StartsWith("str:") || lower.StartsWith("dex:") || lower.StartsWith("int:") ||
                lower.StartsWith("strength:") || lower.StartsWith("dexterity:") || lower.StartsWith("intelligence:") ||
                lower.StartsWith("item level:") || lower.StartsWith("quality:") || lower.StartsWith("sockets:") ||
                lower.StartsWith("physical damage:") || lower.StartsWith("elemental damage:") || lower.StartsWith("chaos damage:") ||
                lower.StartsWith("attacks per second:") || lower.StartsWith("critical strike chance:") || lower.StartsWith("weapon range:") ||
                lower.StartsWith("chance to block:") || lower.StartsWith("armour:") || lower.StartsWith("evasion rating:") ||
                lower.StartsWith("energy shield:") || lower.StartsWith("ward:") || lower.StartsWith("note:") ||
                lower == "corrupted" || lower == "mirrored" || lower == "unidentified" || lower == "synthesised" ||
                lower.StartsWith("two handed") || lower.StartsWith("one handed") || lower.StartsWith("one hand") || lower.StartsWith("two hand"))
            {
                return false;
            }
            return true;
        }

        public static double ApplyTolerance(double val)
        {
            int pct = 10;
            try { pct = PoeSettingsManager.Instance.ModRollTolerancePercent; } catch { }
            double tol = Math.Max(0.0, Math.Min(100.0, pct)) / 100.0;
            double scaled = val * (1.0 - tol);
            if (Math.Abs(val % 1) < 0.0001)
            {
                return Math.Floor(Math.Round(scaled, 6));
            }
            return Math.Round(scaled, 2);
        }

        public static string ExtractTierText(string headerLine, string prefixChar)
        {
            var m = TierRegex.Match(headerLine);
            return m.Success ? $"{prefixChar}{(!string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value)}" : prefixChar;
        }
    }
}
