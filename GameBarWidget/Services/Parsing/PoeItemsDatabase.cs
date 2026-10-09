using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GameBarWidget.Services.Parsing
{
    public sealed class ItemDbEntry
    {
        public string Name { get; set; } = string.Empty;
        public string RefName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string TradeTag { get; set; } = string.Empty;
        public string TradeDisc { get; set; } = string.Empty;
        public string CraftableCategory { get; set; } = string.Empty;
        public string UniqueBase { get; set; } = string.Empty;
        public bool IsExchangeable { get; set; }
        public bool IsTransfigured { get; set; }
        public bool IsVaal { get; set; }
        public string NormalGemVariant { get; set; } = string.Empty;
        public int MaxGemLevel { get; set; }
    }

    /// <summary>
    /// High-performance items dictionary loaded from Awakened PoE Trade's items.ndjson.
    /// Provides exact O(1) canonical item resolution, categories, transfigured gem variants,
    /// unique base types, and trade tags.
    /// </summary>
    public static class PoeItemsDatabase
    {
        private static readonly Dictionary<string, ItemDbEntry> ItemsByName =
            new Dictionary<string, ItemDbEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> CraftableCategoryToTradeCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Armour
                { "Helmet", "armour.helmet" },
                { "Body Armour", "armour.chest" },
                { "Gloves", "armour.gloves" },
                { "Boots", "armour.boots" },
                { "Shield", "armour.shield" },
                { "Quiver", "armour.quiver" },

                // Accessories
                { "Amulet", "accessory.amulet" },
                { "Ring", "accessory.ring" },
                { "Belt", "accessory.belt" },
                { "Trinket", "accessory.trinket" },
                { "Tincture", "accessory.tincture" },

                // Jewels
                { "Jewel", "jewel" },
                { "Abyss Jewel", "jewel.abyss" },
                { "Cluster Jewel", "jewel.cluster" },

                // Weapons
                { "One-Handed Sword", "weapon.onesword" },
                { "Two-Handed Sword", "weapon.twosword" },
                { "One-Handed Axe", "weapon.oneaxe" },
                { "Two-Handed Axe", "weapon.twoaxe" },
                { "One-Handed Mace", "weapon.onemace" },
                { "Two-Handed Mace", "weapon.twomace" },
                { "Bow", "weapon.bow" },
                { "Claw", "weapon.claw" },
                { "Dagger", "weapon.dagger" },
                { "Rune Dagger", "weapon.dagger" },
                { "Sceptre", "weapon.sceptre" },
                { "Staff", "weapon.staff" },
                { "Warstaff", "weapon.staff" },
                { "Wand", "weapon.wand" },
                { "Fishing Rod", "weapon.rod" },

                // Flasks & Endgame
                { "Flask", "flask" },
                { "Map", "map" },
                { "Expedition Logbook", "expedition.logbook" },
                { "Sanctum Relic", "sanctum.relic" },
                { "Heist Blueprint", "heistblueprint" },
                { "Heist Contract", "heistcontract" }
            };

        private static bool _isInitialized = false;

        public static async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                string ndjson = null;

                try
                {
                    var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(@"Assets\items.ndjson");
                    ndjson = await Windows.Storage.FileIO.ReadTextAsync(file);
                }
                catch
                {
                    try
                    {
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "items.ndjson");
                        if (File.Exists(localPath))
                        {
                            ndjson = File.ReadAllText(localPath);
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(ndjson)) return;

                using (var reader = new StringReader(ndjson))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        if (Windows.Data.Json.JsonObject.TryParse(line, out var obj))
                        {
                            string name = obj.ContainsKey("name") ? obj.GetNamedString("name", string.Empty) : string.Empty;
                            if (string.IsNullOrEmpty(name)) continue;

                            var entry = new ItemDbEntry
                            {
                                Name = name,
                                RefName = obj.ContainsKey("refName") ? obj.GetNamedString("refName", name) : name,
                                Namespace = obj.ContainsKey("namespace") ? obj.GetNamedString("namespace", string.Empty) : string.Empty,
                                TradeTag = obj.ContainsKey("tradeTag") ? obj.GetNamedString("tradeTag", string.Empty) : string.Empty,
                                TradeDisc = obj.ContainsKey("tradeDisc") ? obj.GetNamedString("tradeDisc", string.Empty) : string.Empty,
                                IsExchangeable = obj.ContainsKey("exchangeable") && obj.GetNamedBoolean("exchangeable")
                            };

                            // Craftable category mapping
                            if (obj.ContainsKey("craftable") && obj.GetNamedValue("craftable").ValueType == Windows.Data.Json.JsonValueType.Object)
                            {
                                var craftObj = obj.GetNamedObject("craftable");
                                entry.CraftableCategory = craftObj.ContainsKey("category") ? craftObj.GetNamedString("category", string.Empty) : string.Empty;
                            }

                            // Unique base mapping
                            if (obj.ContainsKey("unique") && obj.GetNamedValue("unique").ValueType == Windows.Data.Json.JsonValueType.Object)
                            {
                                var uniqueObj = obj.GetNamedObject("unique");
                                entry.UniqueBase = uniqueObj.ContainsKey("base") ? uniqueObj.GetNamedString("base", string.Empty) : string.Empty;
                            }

                            // Gem metadata mapping
                            if (obj.ContainsKey("gem") && obj.GetNamedValue("gem").ValueType == Windows.Data.Json.JsonValueType.Object)
                            {
                                var gemObj = obj.GetNamedObject("gem");
                                entry.IsTransfigured = gemObj.ContainsKey("transfigured") && gemObj.GetNamedBoolean("transfigured");
                                entry.IsVaal = gemObj.ContainsKey("vaal") && gemObj.GetNamedBoolean("vaal");
                                entry.NormalGemVariant = gemObj.ContainsKey("normalVariant") ? gemObj.GetNamedString("normalVariant", name) : name;
                                entry.MaxGemLevel = gemObj.ContainsKey("maxLevel") ? (int)gemObj.GetNamedNumber("maxLevel", 20) : 20;
                            }

                            ItemsByName[name] = entry;
                        }
                    }
                }
            }
            catch { }
        }

        public static bool TryGetEntry(string itemName, out ItemDbEntry entry)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                entry = null;
                return false;
            }
            return ItemsByName.TryGetValue(itemName.Trim(), out entry);
        }

        public static string ResolveTradeCategory(string craftableCategory)
        {
            if (string.IsNullOrWhiteSpace(craftableCategory)) return string.Empty;
            if (CraftableCategoryToTradeCategory.TryGetValue(craftableCategory.Trim(), out var tradeCat))
            {
                return tradeCat;
            }
            return string.Empty;
        }
    }
}
