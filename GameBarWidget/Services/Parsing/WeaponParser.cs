using System;
using System.Globalization;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Weapons (Bows, Wands, Swords, Axes, Maces, Daggers, Staffs).
    /// Extracts attack speed, critical strike chance, physical and elemental damage ranges, and DPS.
    /// </summary>
    public sealed class WeaponParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return itemClass.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Axe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Mace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Dagger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Wand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Sceptre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("bow") || combined.Contains("wand") || combined.Contains("dagger") ||
                   combined.Contains("sword") || combined.Contains("axe") || combined.Contains("mace") ||
                   combined.Contains("sceptre") || combined.Contains("staff") || combined.Contains("claw");
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

            // Calculate weapon DPS metrics
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
