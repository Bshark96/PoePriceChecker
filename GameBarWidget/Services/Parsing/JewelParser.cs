using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Jewels (Base Jewels, Abyss Jewels, Cluster Jewels, and Timeless Jewels).
    /// Leverages PoeItemsDatabase craftable categories and enforces Awakened crafting base selection.
    /// </summary>
    public sealed class JewelParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            if (item.Category.StartsWith("jewel", StringComparison.OrdinalIgnoreCase)) return true;
            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            if (PoeItemsDatabase.TryGetEntry(key, out var entry))
            {
                return entry.CraftableCategory.IndexOf("Jewel", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return (item.ItemClass ?? string.Empty).IndexOf("Jewel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;

            string key = !string.IsNullOrEmpty(item.BaseType) ? item.BaseType : item.Name;
            string craftCat = string.Empty;
            if (PoeItemsDatabase.TryGetEntry(key, out var entry))
            {
                craftCat = entry.CraftableCategory;
            }

            if (craftCat.Equals("Cluster Jewel", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "jewel.cluster";
                item.IsJewelCraftingBase = false;
                item.FilterBaseTypeActive = true;
            }
            else if (craftCat.Equals("Abyss Jewel", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "jewel.abyss";
                item.IsJewelCraftingBase = (item.Rarity != PoeRarity.Unique);
                item.FilterBaseTypeActive = (item.Rarity == PoeRarity.Unique);
            }
            else
            {
                item.Category = "jewel";
                item.IsJewelCraftingBase = (item.Rarity != PoeRarity.Unique);
                item.FilterBaseTypeActive = (item.Rarity == PoeRarity.Unique);
            }
        }
    }
}
