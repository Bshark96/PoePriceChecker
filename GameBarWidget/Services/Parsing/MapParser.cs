using System;

namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Specialized parser for Maps, Blighted Maps, Waystones, Fragments, and Scarabs.
    /// </summary>
    public sealed class MapParser : IItemTypeParser
    {
        public bool CanParse(PoeItem item, string[] headerLines, string[] blocks)
        {
            string itemClass = item.ItemClass ?? string.Empty;
            string combined = $"{itemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            return item.MapTier > 0 ||
                   itemClass.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.Contains("waystone") ||
                   combined.Contains("scarab") ||
                   combined.Contains("fragment") ||
                   combined.Contains("logbook") ||
                   combined.Contains("relic") ||
                   combined.Contains("contract") ||
                   combined.Contains("blueprint");
        }

        public void Parse(PoeItem item, string[] headerLines, string[] blocks)
        {
            item.Namespace = ItemNamespace.Map;
            string combined = $"{item.ItemClass} {item.Name} {item.BaseType}".ToLowerInvariant();

            // Extract Map Tier from blocks
            for (int b = 1; b < blocks.Length; b++)
            {
                string[] lines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("Map Tier:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(line.Substring(9).Trim(), out int tier))
                        {
                            item.MapTier = tier;
                        }
                    }
                }
            }

            if (combined.Contains("blight-ravaged") || combined.Contains("blight ravaged")) item.Category = "map.ravaged";
            else if (combined.Contains("blighted")) item.Category = "map.blighted";
            else if (combined.Contains("waystone")) item.Category = "map.waystone";
            else if (combined.Contains("scarab")) item.Category = "map.scarab";
            else if (combined.Contains("fragment") || combined.Contains("sacrifice") || combined.Contains("mortal") || combined.Contains("key") || combined.Contains("vessel") || combined.Contains("offering") || combined.Contains("splinter")) item.Category = "map.fragment";
            else if (combined.Contains("logbook")) item.Category = "expedition.logbook";
            else if (combined.Contains("relic")) item.Category = "sanctum.relic";
            else if (combined.Contains("blueprint")) item.Category = "heistblueprint";
            else if (combined.Contains("contract")) item.Category = "heistcontract";
            else item.Category = "map";
        }
    }
}
