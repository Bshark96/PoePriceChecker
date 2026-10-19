using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameBarWidget.Services
{
    public static class PoeStatsDatabase
    {
        public static readonly Dictionary<string, string> StatTemplateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> ExplicitStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> ImplicitStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> FracturedStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> CraftedStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> EnchantStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> PseudoStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly (string Key, string Id)[] FallbackStats =
        {
            ("fire resistance", "explicit.stat_3372524247"),
            ("cold resistance", "explicit.stat_4220027924"),
            ("lightning resistance", "explicit.stat_1671376347"),
            ("chaos resistance", "explicit.stat_2923486259"),
            ("all elemental resistances", "explicit.stat_2901986750"),
            ("maximum life", "explicit.stat_3299347043"),
            ("maximum energy shield", "explicit.stat_4052037485"),
            ("maximum mana", "explicit.stat_1050105434"),
            ("strength", "explicit.stat_4086418403"),
            ("dexterity", "explicit.stat_3261801346"),
            ("intelligence", "explicit.stat_328541901"),
            ("movement speed", "explicit.stat_2250533757"),
            ("attack speed", "pseudo.pseudo_total_attack_speed"),
            ("global accuracy rating", "explicit.stat_624954515"),
            ("accuracy rating", "explicit.stat_691932474"),
            ("critical strike", "explicit.stat_3556824919"),
            ("flask effect", "explicit.stat_3489115984"),
            ("elemental resistance values as inverted", "explicit.stat_2750800428"),
            ("fortify", "implicit.stat_107118693"),
            ("cold damage", "explicit.stat_1037193709"),
            ("lightning damage", "explicit.stat_3336890334"),
            ("fire damage", "explicit.stat_709508306"),
            ("physical damage", "explicit.stat_3032599211"),
            ("attack speed", "explicit.stat_210067635")
        };

        private static bool _isInitialized = false;

        public static async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            try
            {
                string ndjson = null;

                // Load exclusively from Assets\stats.ndjson
                try
                {
                    var ndjsonFile = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(@"Assets\stats.ndjson");
                    ndjson = await Windows.Storage.FileIO.ReadTextAsync(ndjsonFile);
                }
                catch
                {
                    // Fallback to local file path if running outside packaged UWP context
                    try
                    {
                        string localPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "stats.ndjson");
                        if (System.IO.File.Exists(localPath))
                        {
                            ndjson = System.IO.File.ReadAllText(localPath);
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(ndjson)) return;

                using (var reader = new System.IO.StringReader(ndjson))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        if (Windows.Data.Json.JsonObject.TryParse(line, out var obj))
                        {
                            string refText = obj.ContainsKey("ref") ? obj.GetNamedString("ref", string.Empty) : string.Empty;
                            if (string.IsNullOrEmpty(refText)) continue;

                            if (obj.ContainsKey("trade") && obj.GetNamedObject("trade") is var tradeObj && tradeObj.ContainsKey("ids"))
                            {
                                var idsObj = tradeObj.GetNamedObject("ids");
                                foreach (var scopePair in idsObj)
                                {
                                    string scope = scopePair.Key.ToLowerInvariant();
                                    if (scopePair.Value.ValueType == Windows.Data.Json.JsonValueType.Array)
                                    {
                                        var arr = scopePair.Value.GetArray();
                                        if (arr.Count > 0)
                                        {
                                            string firstId = arr.GetStringAt(0);
                                            StatTemplateMap[$"{refText} ({scope})"] = firstId;

                                            if (scope == "explicit") { ExplicitStatMap[refText] = firstId; if (!StatTemplateMap.ContainsKey(refText)) StatTemplateMap[refText] = firstId; }
                                            else if (scope == "implicit") { ImplicitStatMap[refText] = firstId; if (!StatTemplateMap.ContainsKey(refText)) StatTemplateMap[refText] = firstId; }
                                            else if (scope == "fractured") { FracturedStatMap[refText] = firstId; }
                                            else if (scope == "crafted") { CraftedStatMap[refText] = firstId; }
                                            else if (scope == "enchant") { EnchantStatMap[refText] = firstId; }
                                            else if (scope == "pseudo") { PseudoStatMap[refText] = firstId; }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static string LookupScopedStatId(string template, ModifierType type, string cleanText, string category)
        {
            bool isWeapon = !string.IsNullOrEmpty(category) && (category.StartsWith("weapon.", StringComparison.OrdinalIgnoreCase) || category.Equals("weapon", StringComparison.OrdinalIgnoreCase));

            // Category-specific weapon local vs global stat overrides
            if (isWeapon && type == ModifierType.Explicit)
            {
                if (template.Equals("Adds # to # Cold Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_1037193709";
                if (template.Equals("Adds # to # Lightning Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_3336890334";
                if (template.Equals("Adds # to # Fire Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_709508306";
                if (template.Equals("Adds # to # Physical Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_3032599211";
                if (template.Equals("#% increased Attack Speed", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_210067635";
                if (template.Equals("#% increased Critical Strike Chance", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_2375314420";
                if (template.Equals("#% increased Physical Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_1509134228";
            }
            else if (!isWeapon && type == ModifierType.Explicit)
            {
                if (template.Equals("Adds # to # Cold Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_2361430260";
                if (template.Equals("Adds # to # Lightning Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_1754445556";
                if (template.Equals("Adds # to # Fire Damage", StringComparison.OrdinalIgnoreCase)) return "explicit.stat_1335054179";
            }

            Dictionary<string, string> scopedMap = type switch
            {
                ModifierType.Implicit => ImplicitStatMap,
                ModifierType.Fractured => FracturedStatMap,
                ModifierType.Crafted => CraftedStatMap,
                ModifierType.Enchant => EnchantStatMap,
                ModifierType.Pseudo => PseudoStatMap,
                _ => ExplicitStatMap
            };

            // 1. Direct scoped dictionary lookup
            if (scopedMap.TryGetValue(template, out string sid1)) return sid1;
            if (template.StartsWith("+") && scopedMap.TryGetValue(template.Substring(1), out string sid2)) return sid2;
            if (template.StartsWith("-") && scopedMap.TryGetValue("+" + template.Substring(1), out string sidPos1)) return sidPos1;
            if (template.StartsWith("-") && scopedMap.TryGetValue(template.Substring(1), out string sidPos2)) return sidPos2;
            if (scopedMap.TryGetValue(cleanText, out string sid3)) return sid3;
            if (cleanText.StartsWith("-") && scopedMap.TryGetValue("+" + cleanText.Substring(1), out string sidCleanPos1)) return sidCleanPos1;
            if (cleanText.StartsWith("-") && scopedMap.TryGetValue(cleanText.Substring(1), out string sidCleanPos2)) return sidCleanPos2;

            // 2. Exact scope suffix lookup in global database
            string scopeSuffix = $" ({type.ToString().ToLowerInvariant()})";
            if (StatTemplateMap.TryGetValue(template + scopeSuffix, out string g1)) return g1;
            if (template.StartsWith("+") && StatTemplateMap.TryGetValue(template.Substring(1) + scopeSuffix, out string g2)) return g2;
            if (template.StartsWith("-") && StatTemplateMap.TryGetValue("+" + template.Substring(1) + scopeSuffix, out string gPos1)) return gPos1;
            if (template.StartsWith("-") && StatTemplateMap.TryGetValue(template.Substring(1) + scopeSuffix, out string gPos2)) return gPos2;
            if (StatTemplateMap.TryGetValue(cleanText + scopeSuffix, out string g3)) return g3;
            if (cleanText.StartsWith("-") && StatTemplateMap.TryGetValue("+" + cleanText.Substring(1) + scopeSuffix, out string gCleanPos1)) return gCleanPos1;
            if (cleanText.StartsWith("-") && StatTemplateMap.TryGetValue(cleanText.Substring(1) + scopeSuffix, out string gCleanPos2)) return gCleanPos2;

            // 3. General template match
            if (StatTemplateMap.TryGetValue(template, out string g4)) return g4;
            if (template.StartsWith("+") && StatTemplateMap.TryGetValue(template.Substring(1), out string g5)) return g5;
            if (template.StartsWith("-") && StatTemplateMap.TryGetValue("+" + template.Substring(1), out string gPos3)) return gPos3;
            if (template.StartsWith("-") && StatTemplateMap.TryGetValue(template.Substring(1), out string gPos4)) return gPos4;
            if (StatTemplateMap.TryGetValue(cleanText, out string g6)) return g6;
            if (cleanText.StartsWith("-") && StatTemplateMap.TryGetValue("+" + cleanText.Substring(1), out string gCleanPos3)) return gCleanPos3;
            if (cleanText.StartsWith("-") && StatTemplateMap.TryGetValue(cleanText.Substring(1), out string gCleanPos4)) return gCleanPos4;

            // 4. Fallback heuristics
            string cleanForHeuristics = cleanText.StartsWith("-") ? cleanText.Substring(1) : cleanText;
            return GuessStatId(cleanForHeuristics);
        }

        public static string GuessStatId(string rawText)
        {
            string lower = rawText.ToLowerInvariant();
            foreach (var (key, id) in FallbackStats)
            {
                if (lower.Contains(key)) return id;
            }
            return string.Empty;
        }
    }
}
