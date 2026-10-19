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

        /// <summary>
        /// Maps an item to its designated Evaluation Theme:
        /// Theme 1: Accessories (non-unique), Jewels, Currency & Stackables, Divination Cards
        /// Theme 2: Weapons, Armour & Off-Hands, Accessories (Unique)
        /// Theme 3: Skill & Support Gems, Flasks
        /// Theme 4: Maps & Endgame Content, League & Specialized Content
        /// </summary>
        public static string ResolveThemeKey(PoeItem item)
        {
            if (item == null) return "theme4";

            string cat = item.Category ?? string.Empty;
            string itemClass = item.ItemClass ?? string.Empty;
            var rarity = item.Rarity;

            // Theme 3:
            // 1) Skill & Support Gems
            // 2) Flasks
            if (rarity == PoeRarity.Gem ||
                cat.StartsWith("gem", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cat.StartsWith("flask", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Flask", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "theme3";
            }

            // Theme 4:
            // 1) Maps & Endgame Content
            // 2) League & Specialized Content
            if (cat.StartsWith("map", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.MapTier > 0 ||
                cat.StartsWith("expedition", StringComparison.OrdinalIgnoreCase) ||
                cat.StartsWith("sanctum", StringComparison.OrdinalIgnoreCase) ||
                cat.StartsWith("heist", StringComparison.OrdinalIgnoreCase) ||
                cat.StartsWith("monster.beast", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Logbook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Relic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Blueprint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Contract", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Beast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Ultimatum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Invitation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "theme4";
            }

            // Theme 1:
            // 2) Jewels
            // 3) Currency & Stackables
            // 4) Divination Cards
            if (rarity == PoeRarity.Currency ||
                cat.StartsWith("currency", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Currency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Essence", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Fossil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Resonator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Oil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Catalyst", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Tattoo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemClass.IndexOf("Omen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rarity == PoeRarity.DivinationCard ||
                cat.StartsWith("card", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cat.StartsWith("jewel", StringComparison.OrdinalIgnoreCase) ||
                itemClass.IndexOf("Jewel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "theme1";
            }

            // Accessories:
            // If Unique -> Theme 2 (Weapons, Armour & Off-Hands, Accessories)
            // If Rare / Non-Unique -> Theme 1 (Accessories, Jewels, Currency, Div Cards)
            bool isAccessory = cat.StartsWith("accessory", StringComparison.OrdinalIgnoreCase) ||
                               itemClass.IndexOf("Amulet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               itemClass.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               itemClass.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               itemClass.IndexOf("Trinket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               itemClass.IndexOf("Tincture", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isAccessory)
            {
                return (rarity == PoeRarity.Unique) ? "theme2" : "theme1";
            }

            // Theme 2:
            // 1) Weapons
            // 2) Armour & Off-Hands
            // 3) Accessories (Unique handled above)
            bool isWeaponOrArmour = cat.StartsWith("weapon", StringComparison.OrdinalIgnoreCase) ||
                                    cat.StartsWith("armour", StringComparison.OrdinalIgnoreCase) ||
                                    itemClass.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Axe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Mace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Dagger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Sceptre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Wand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Rod", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Body Armour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Gloves", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Boots", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    itemClass.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isWeaponOrArmour)
            {
                return "theme2";
            }

            if (rarity == PoeRarity.Unique) return "theme2";
            return "theme1";
        }
    }
}
