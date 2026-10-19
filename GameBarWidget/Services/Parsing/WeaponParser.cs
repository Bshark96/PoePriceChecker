using System;
using System.Globalization;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Weapons.
    /// Extracts attack speed, crit chance, damage ranges, and DPS metrics.
    /// </summary>
    public sealed class WeaponParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            if (item.Category.StartsWith("weapon", StringComparison.OrdinalIgnoreCase)) return true;
            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            if (PoeItemsDatabase.TryGetEntry(key, out var entry))
            {
                string cat = PoeItemsDatabase.ResolveTradeCategory(entry.CraftableCategory);
                if (cat.StartsWith("weapon", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return (item.ItemClass ?? string.Empty).IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.ItemClass ?? string.Empty).IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;

            for (int b = 1; b < blocks.Length; b++)
            {
                string[] lines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();

                    if (line.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = PoeModifierParser.NumberRegex.Match(line);
                        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double aps))
                        {
                            item.AttacksPerSecond = aps;
                        }
                    }
                    else if (line.StartsWith("Critical Strike Chance:", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = PoeModifierParser.NumberRegex.Match(line);
                        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double crit))
                        {
                            item.CritChance = crit;
                        }
                    }
                    else if (line.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase))
                    {
                        var rm = PoeModifierParser.RangeDamageRegex.Match(line);
                        if (rm.Success)
                        {
                            double.TryParse(rm.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pmin);
                            double.TryParse(rm.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pmax);
                            item.PhysDamageMin = pmin;
                            item.PhysDamageMax = pmax;
                        }
                    }
                    else if (line.StartsWith("Elemental Damage:", StringComparison.OrdinalIgnoreCase))
                    {
                        double tmin = 0, tmax = 0;
                        foreach (System.Text.RegularExpressions.Match m in PoeModifierParser.RangeDamageRegex.Matches(line))
                        {
                            double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double emin);
                            double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double emax);
                            tmin += emin;
                            tmax += emax;
                        }
                        item.EleDamageMin = tmin;
                        item.EleDamageMax = tmax;
                    }
                }
            }

            if (item.AttacksPerSecond > 0)
            {
                if (item.PhysDamageMax > 0)
                {
                    item.PhysicalDps = Math.Round(((item.PhysDamageMin + item.PhysDamageMax) / 2.0) * item.AttacksPerSecond, 1);
                }
                if (item.EleDamageMax > 0)
                {
                    item.ElementalDps = Math.Round(((item.EleDamageMin + item.EleDamageMax) / 2.0) * item.AttacksPerSecond, 1);
                }
                item.TotalDps = Math.Round(item.PhysicalDps + item.ElementalDps, 1);
            }
        }
    }
}
