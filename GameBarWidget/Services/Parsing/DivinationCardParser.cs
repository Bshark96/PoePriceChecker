using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Divination Cards.
    /// </summary>
    public sealed class DivinationCardParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            return item.Rarity == PoeRarity.DivinationCard ||
                   itemClass.IndexOf("Divination", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.DivinationCard;
            item.Category = "card";

            // Divination cards match on exact card name with no explicit modifier rolls
            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
