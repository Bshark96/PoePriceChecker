using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Currency, Essences, Fossils, Resonators, Catalysts, and Oils.
    /// </summary>
    public sealed class CurrencyParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return item.Rarity == PoeRarity.Currency ||
                   itemClass.IndexOf("Currency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("essence") ||
                   combined.Contains("fossil") ||
                   combined.Contains("resonator") ||
                   combined.Contains("omen") ||
                   combined.Contains("tattoo") ||
                   combined.Contains("oil") ||
                   combined.Contains("catalyst") ||
                   combined.Contains("incubator") ||
                   combined.Contains("lifeforce");
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.Currency;
            string combined = $"{item.ItemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            if (combined.Contains("essence")) item.Category = "currency.essence";
            else if (combined.Contains("fossil")) item.Category = "currency.fossil";
            else if (combined.Contains("resonator")) item.Category = "currency.resonator";
            else if (combined.Contains("omen")) item.Category = "currency.omen";
            else if (combined.Contains("tattoo")) item.Category = "currency.tattoo";
            else if (combined.Contains("oil")) item.Category = "currency.oil";
            else if (combined.Contains("catalyst")) item.Category = "currency.catalyst";
            else if (combined.Contains("incubator")) item.Category = "currency.incubator";
            else if (combined.Contains("lifeforce")) item.Category = "currency.lifeforce";
            else item.Category = "currency";

            // Currency items do not possess explicit rollable modifier filters
            item.Modifiers.Clear();
            item.PseudoModifiers.Clear();
        }
    }
}
