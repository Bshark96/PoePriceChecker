using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Life, Mana, and Utility Flasks.
    /// </summary>
    public sealed class FlaskParser : IItemTypeParser
    {
        private static readonly string[] FlaskBases = new[]
        {
            "Diamond Flask", "Quicksilver Flask", "Silver Flask", "Jade Flask", "Quartz Flask",
            "Granite Flask", "Basalt Flask", "Bismuth Flask", "Aquamarine Flask", "Amethyst Flask",
            "Ruby Flask", "Sapphire Flask", "Topaz Flask", "Stibnite Flask", "Corundum Flask",
            "Hallowed Life Flask", "Divine Life Flask", "Sanctified Life Flask", "Hallowed Mana Flask",
            "Divine Mana Flask", "Eternal Life Flask", "Eternal Mana Flask", "Large Life Flask",
            "Medium Life Flask", "Small Life Flask", "Colossal Life Flask", "Giant Life Flask"
        };

        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return itemClass.IndexOf("Flask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("flask");
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;
            item.Category = "flasks";

            foreach (var fb in FlaskBases)
            {
                if ((!string.IsNullOrEmpty(item.Name) && item.Name.IndexOf(fb, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(item.BaseType) && item.BaseType.IndexOf(fb, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    item.BaseType = fb;
                    break;
                }
            }
        }
    }
}
