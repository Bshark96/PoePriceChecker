using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Skill Gems, Support Gems, and Transfigured Gems.
    /// Uses PoeItemsDatabase for canonical variants, discriminators, and level parsing.
    /// </summary>
    public sealed class GemParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            return item.Namespace == ItemNamespace.Gem ||
                   item.Rarity == PoeRarity.Gem ||
                   (item.ItemClass ?? string.Empty).IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.Gem;
            string gemFullName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
            item.Name = gemFullName;
            item.BaseType = gemFullName;

            if (PoeItemsDatabase.TryGetEntry(gemFullName, out var entry))
            {
                item.Category = entry.Name.IndexOf("Support", StringComparison.OrdinalIgnoreCase) >= 0 ? "gem.supportgem" : "gem.activegem";
                if (entry.IsTransfigured)
                {
                    item.IsTransfiguredGem = true;
                    item.NormalGemVariant = entry.NormalGemVariant;
                    item.TradeDiscriminator = entry.TradeDisc;
                }
            }
            else
            {
                int ofIdx = gemFullName.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
                if (ofIdx > 0)
                {
                    item.IsTransfiguredGem = true;
                    item.NormalGemVariant = gemFullName.Substring(0, ofIdx).Trim();
                    item.TradeDiscriminator = "alt_x";
                }
            }

            // Extract Gem Level and Quality from blocks
            for (int b = 0; b < blocks.Length; b++)
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

            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
