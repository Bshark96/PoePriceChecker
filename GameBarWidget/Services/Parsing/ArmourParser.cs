using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Body Armours, Helmets, Gloves, Boots, and Shields.
    /// Extracts Armour, Evasion, and Energy Shield values.
    /// </summary>
    public sealed class ArmourParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            if (item.Category.StartsWith("armour", StringComparison.OrdinalIgnoreCase)) return true;
            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            if (PoeItemsDatabase.TryGetEntry(key, out var entry))
            {
                string cat = PoeItemsDatabase.ResolveTradeCategory(entry.CraftableCategory);
                if (cat.StartsWith("armour", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return (item.ItemClass ?? string.Empty).IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0;
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

                    if (line.StartsWith("Armour:", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = PoeModifierParser.NumberRegex.Match(line);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int arm))
                        {
                            item.Armour = arm;
                        }
                    }
                    else if (line.StartsWith("Evasion Rating:", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = PoeModifierParser.NumberRegex.Match(line);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int eva))
                        {
                            item.Evasion = eva;
                        }
                    }
                    else if (line.StartsWith("Energy Shield:", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = PoeModifierParser.NumberRegex.Match(line);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int es))
                        {
                            item.EnergyShield = es;
                        }
                    }
                }
            }
        }
    }
}
