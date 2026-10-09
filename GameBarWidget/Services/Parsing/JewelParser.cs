using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Jewels (Base Jewels, Abyss Jewels, Cluster Jewels, and Timeless Jewels).
    /// Implements Awakened PoE Trade standard for jewel crafting bases:
    /// For non-unique base jewels (Cobalt, Crimson, Viridian, Abyss), the exact base type is
    /// deselected by default (FilterBaseTypeActive = false) so that search queries match across
    /// any equivalent jewel base for crafting, while cluster jewels preserve base selection.
    /// </summary>
    public sealed class JewelParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return itemClass.IndexOf("Jewel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("jewel");
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;
            string combined = $"{item.ItemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            if (combined.Contains("cluster"))
            {
                item.Category = "jewel.cluster";
                item.IsJewelCraftingBase = false;
                item.FilterBaseTypeActive = true;
            }
            else if (combined.Contains("abyss"))
            {
                item.Category = "jewel.abyss";
                // Abyss jewels (Hypnotic, Murderous, Searching, Ghastly) can be searched as generic abyss base
                item.IsJewelCraftingBase = (item.Rarity != PoeRarity.Unique);
                item.FilterBaseTypeActive = (item.Rarity == PoeRarity.Unique);
            }
            else if (combined.Contains("timeless"))
            {
                item.Category = "jewel.timeless";
                item.IsJewelCraftingBase = false;
                item.FilterBaseTypeActive = true;
            }
            else
            {
                item.Category = "jewel";
                // Standard base jewels (Crimson, Viridian, Cobalt, Prismatic):
                // For rare/magic crafting bases, Awakened PoE Trade deselects the exact jewel base type
                // so trade queries search by category: "jewel" with the chosen stat affixes.
                item.IsJewelCraftingBase = (item.Rarity != PoeRarity.Unique);
                item.FilterBaseTypeActive = (item.Rarity == PoeRarity.Unique);
            }
        }
    }
}
