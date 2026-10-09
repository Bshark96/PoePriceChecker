using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Skill Gems, Support Gems, and Transfigured Gems.
    /// Handles Gem Level, Quality, and Transfigured variant resolution.
    /// </summary>
    public sealed class GemParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            return item.Rarity == PoeRarity.Gem ||
                   itemClass.Equals("Skill Gems", StringComparison.OrdinalIgnoreCase) ||
                   itemClass.Equals("Support Gems", StringComparison.OrdinalIgnoreCase) ||
                   itemClass.Equals("Active Skill Gems", StringComparison.OrdinalIgnoreCase) ||
                   itemClass.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.Gem;
            string combined = $"{item.ItemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            if (combined.Contains("support"))
            {
                item.Category = "gem.supportgem";
            }
            else if (combined.Contains("meta"))
            {
                item.Category = "gem.metagem";
            }
            else
            {
                item.Category = "gem.activegem";
            }

            // In PoE clipboard text, gems only have one header line (Name equals BaseType)
            string gemFullName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
            item.Name = gemFullName;
            item.BaseType = gemFullName;

            // Transfigured Gem detection (e.g., "Cyclone of Tumult")
            int ofIndex = gemFullName.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
            if (ofIndex > 0)
            {
                item.IsTransfiguredGem = true;
                item.NormalGemVariant = gemFullName.Substring(0, ofIndex).Trim();
            }
            else
            {
                item.IsTransfiguredGem = false;
                item.NormalGemVariant = gemFullName;
            }

            // Extract Gem Level and Quality from blocks
            for (int b = 1; b < blocks.Length; b++)
            {
                string[] lines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Gem Level:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = PoeModifierParser.NumberRegex.Match(line);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int gLvl))
                        {
                            item.GemLevel = gLvl;
                            item.FilterGemLevelMin = gLvl;
                            item.FilterGemLevelActive = true;
                        }
                    }
                    else if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = PoeModifierParser.NumberRegex.Match(line);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int q))
                        {
                            item.Quality = q;
                            if (q > 0)
                            {
                                item.FilterQualityMin = q;
                                item.FilterQualityActive = true;
                            }
                        }
                    }
                }
            }

            // Gems do not have explicit stat roll filters; clear description text lines
            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
