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

            if (line.EndsWith("(implicit)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Implicit; line = line.Substring(0, line.Length - 10).Trim(); }
            else if (line.EndsWith("(fractured)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Fractured; line = line.Substring(0, line.Length - 11).Trim(); }
            else if (line.EndsWith("(crafted)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Crafted; line = line.Substring(0, line.Length - 9).Trim(); }
            else if (line.EndsWith("(enchant)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Enchant; line = line.Substring(0, line.Length - 9).Trim(); }

            line = Regex.Replace(line, @"\s*\((?:augmented|unscalable(?:\s+value)?|synthesised|crucible|scourge)\)", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();

            // Set clean RawText (without unscalable or tag annotations)
            mod.RawText = line;

            // Extract brackets only if scalable
            var rbMatches = RangeBracketsRegex.Matches(line);
            if (!mod.IsUnscalable && rbMatches.Count > 0)
            {
                if (double.TryParse(rbMatches[0].Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmin)) mod.MinRoll = Math.Abs(rmin);
                if (double.TryParse(rbMatches[0].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax)) mod.MaxRoll = Math.Abs(rmax);
                if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                {
                    double tmp = mod.MinRoll.Value;
                    mod.MinRoll = mod.MaxRoll.Value;
                    mod.MaxRoll = tmp;
                }
                if (rbMatches.Count > 1 && double.TryParse(rbMatches[1].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax2))
                {
                    rmax2 = Math.Abs(rmax2);
                    if (!mod.MaxRoll.HasValue || rmax2 > mod.MaxRoll.Value) mod.MaxRoll = rmax2;
                }
            }

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
                double avg = (minV + maxV) / 2.0;

                mod.Template = $"Adds # to # {dt}";
                
                if (!mod.IsUnscalable)
                {
                    // If only 1 bracket was on the max roll (e.g. Adds 1 to 575(550-600))
                    if (rbMatches.Count == 1 && minV <= 1)
                    {
                        mod.NumberValue = maxV;
                        if (double.TryParse(rbMatches[0].Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmin)) mod.MinRoll = Math.Abs(rmin);
                        if (double.TryParse(rbMatches[0].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax)) mod.MaxRoll = Math.Abs(rmax);
                        if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                        {
                            double tmp = mod.MinRoll.Value;
                            mod.MinRoll = mod.MaxRoll.Value;
                            mod.MaxRoll = tmp;
                        }
                    }
                    else
                    {
                        mod.NumberValue = avg;
                        if (!mod.MinRoll.HasValue) mod.MinRoll = avg;
                        if (!mod.MaxRoll.HasValue) mod.MaxRoll = avg;
                    }
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
                        if (!mod.MinRoll.HasValue) mod.MinRoll = Math.Floor(val * 0.85);
                        if (!mod.MaxRoll.HasValue) mod.MaxRoll = val;
                        if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                        {
                            double tmp = mod.MinRoll.Value;
                            mod.MinRoll = mod.MaxRoll.Value;
                            mod.MaxRoll = tmp;
                        }
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

            mod.IsActive = false;
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

        public static string ExtractTierText(string headerLine, string prefixChar)
        {
            var m = TierRegex.Match(headerLine);
            return m.Success ? $"{prefixChar}{(!string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value)}" : prefixChar;
        }
    }
}
