using System;

namespace GameBarWidget.Services
{
    using GameBarWidget.Services.Parsing;

    /// <summary>
    /// Resolves canonical PoE namespaces and categories using exact lookups in PoeItemsDatabase
    /// with clean fallback mappings for synthesized, un-indexed, or rare base types.
    /// </summary>
    public static class PoeCategoryResolver
    {
        public static void ResolveItemNamespaceAndCategory(PoeItem item)
        {
            string name = item.Name ?? string.Empty;
            string baseType = item.BaseType ?? string.Empty;

            // 1. Direct Canonical Database Lookup
            if (PoeItemsDatabase.TryGetEntry(name, out var nameEntry))
            {
                ApplyDatabaseEntry(item, nameEntry);
                return;
            }

            if (PoeItemsDatabase.TryGetEntry(baseType, out var baseEntry))
            {
                ApplyDatabaseEntry(item, baseEntry);
                return;
            }

            // 2. Fast Fallback resolution based on ItemClass & Rarity
            string itemClass = item.ItemClass ?? string.Empty;

            if (item.Rarity == PoeRarity.Gem || itemClass.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.Namespace = ItemNamespace.Gem;
                item.Category = itemClass.IndexOf("Support", StringComparison.OrdinalIgnoreCase) >= 0 ? "gem.supportgem" : "gem.activegem";
                return;
            }

            if (item.Rarity == PoeRarity.DivinationCard || itemClass.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.Namespace = ItemNamespace.DivinationCard;
                item.Category = "card";
                return;
            }

            if (item.Rarity == PoeRarity.Currency || itemClass.IndexOf("Currency", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.Namespace = ItemNamespace.Currency;
                item.Category = "currency";
                return;
            }

            if (itemClass.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0 || item.MapTier > 0)
            {
                item.Namespace = ItemNamespace.Map;
                item.Category = "map";
                return;
            }

            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;
        }

        private static void ApplyDatabaseEntry(PoeItem item, ItemDbEntry entry)
        {
            switch (entry.Namespace)
            {
                case "GEM":
                    item.Namespace = ItemNamespace.Gem;
                    item.Category = entry.Name.IndexOf("Support", StringComparison.OrdinalIgnoreCase) >= 0 ? "gem.supportgem" : "gem.activegem";
                    if (entry.IsTransfigured)
                    {
                        item.IsTransfiguredGem = true;
                        item.NormalGemVariant = entry.NormalGemVariant;
                        item.TradeDiscriminator = entry.TradeDisc;
                    }
                    break;

                case "UNIQUE":
                    item.Namespace = ItemNamespace.Unique;
                    if (!string.IsNullOrEmpty(entry.UniqueBase))
                    {
                        item.BaseType = entry.UniqueBase;
                        if (PoeItemsDatabase.TryGetEntry(entry.UniqueBase, out var bEntry) && !string.IsNullOrEmpty(bEntry.CraftableCategory))
                        {
                            item.Category = PoeItemsDatabase.ResolveTradeCategory(bEntry.CraftableCategory);
                        }
                    }
                    break;

                case "DIVINATION_CARD":
                    item.Namespace = ItemNamespace.DivinationCard;
                    item.Category = "card";
                    break;

                case "CAPTURED_BEAST":
                    item.Namespace = ItemNamespace.CapturedBeast;
                    item.Category = "monster.beast";
                    break;

                case "ITEM":
                default:
                    if (entry.IsExchangeable)
                    {
                        item.Namespace = ItemNamespace.Currency;
                        item.Category = "currency";
                    }
                    else
                    {
                        item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;
                        if (!string.IsNullOrEmpty(entry.CraftableCategory))
                        {
                            item.Category = PoeItemsDatabase.ResolveTradeCategory(entry.CraftableCategory);
                        }
                    }
                    break;
            }
        }
    }
}
