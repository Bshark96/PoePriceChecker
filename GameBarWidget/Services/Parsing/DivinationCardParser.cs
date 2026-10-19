using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Divination Cards.
    /// Uses PoeItemsDatabase for canonical validation.
    /// </summary>
    public sealed class DivinationCardParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            if (item.Namespace == ItemNamespace.DivinationCard || item.Rarity == PoeRarity.DivinationCard) return true;
            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            return PoeItemsDatabase.TryGetEntry(key, out var entry) && entry.Namespace == "DIVINATION_CARD";
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.DivinationCard;
            item.Category = "card";

            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
