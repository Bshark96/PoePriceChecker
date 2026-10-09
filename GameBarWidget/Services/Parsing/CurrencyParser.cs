using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Currency, Essences, Fossils, Resonators, Catalysts, and Oils.
    /// Uses PoeItemsDatabase for exchangeable status and trade tags.
    /// </summary>
    public sealed class CurrencyParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            if (item.Namespace == ItemNamespace.Currency || item.Rarity == PoeRarity.Currency) return true;
            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            return PoeItemsDatabase.TryGetEntry(key, out var entry) && entry.IsExchangeable;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.Currency;
            item.Category = "currency";

            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            if (PoeItemsDatabase.TryGetEntry(key, out var entry))
            {
                if (!string.IsNullOrEmpty(entry.TradeTag))
                {
                    item.TradeDiscriminator = entry.TradeTag;
                }
            }

            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
