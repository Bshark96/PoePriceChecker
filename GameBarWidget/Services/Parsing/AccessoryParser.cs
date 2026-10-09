using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Rings, Amulets, Belts, and Trinkets.
    /// </summary>
    public sealed class AccessoryParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return itemClass.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Amulet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemClass.IndexOf("Trinket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("ring") || combined.Contains("amulet") || combined.Contains("talisman") ||
                   combined.Contains("belt") || combined.Contains("sash") || combined.Contains("girdle") ||
                   combined.Contains("trinket") || combined.Contains("tincture") || combined.Contains("charm");
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;
        }
    }
}
