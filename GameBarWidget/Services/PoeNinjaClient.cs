using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace GameBarWidget.Services
{
    public sealed class BenchmarkResult
    {
        public bool Found { get; set; }
        public double ChaosEquivalent { get; set; }
        public double DivineEquivalent { get; set; }
        public double MinChaos { get; set; }
        public double MaxChaos { get; set; }
        public double ChangePercent { get; set; }
        public double DustValue { get; set; }
        public string FormattedText { get; set; } = string.Empty;
        public string DustText { get; set; } = string.Empty;
        public string Source { get; set; } = "poe.ninja";
        public string Confidence { get; set; } = "High";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    internal sealed class NinjaItemEntry
    {
        public string Name { get; set; } = string.Empty;
        public string BaseType { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public int ItemClass { get; set; }
        public int Links { get; set; }
        public int MapTier { get; set; }
        public double ChaosValue { get; set; }
        public double DivineValue { get; set; }
        public double ChangePercent { get; set; }
        public double DustValue { get; set; }
        public int ListingCount { get; set; }
    }

    internal sealed class NinjaGemEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public int GemLevel { get; set; } = 1;
        public int GemQuality { get; set; } = 0;
        public bool Corrupted { get; set; } = false;
        public bool IsExceptional { get; set; } = false;
        public double ChaosValue { get; set; }
        public double DivineValue { get; set; }
        public double ChangePercent { get; set; }
        public int ListingCount { get; set; }
    }

    /// <summary>
    /// Fast-path in-memory pricing client using live poe.ninja PoE 1 economy endpoints.
    /// Provides accurate variant-aware lookups for currency, skill gems, divination cards, maps, fragments, and uniques.
    /// </summary>
    public sealed class PoeNinjaClient
    {
        private static readonly Lazy<PoeNinjaClient> _lazy = new Lazy<PoeNinjaClient>(() => new PoeNinjaClient());
        public static PoeNinjaClient Instance => _lazy.Value;

        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, double> _currencyCache = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, double> _currencyChangeCache = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<NinjaItemEntry>> _uniqueCache = new ConcurrentDictionary<string, List<NinjaItemEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<NinjaItemEntry>> _generalItemCache = new ConcurrentDictionary<string, List<NinjaItemEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<NinjaGemEntry>> _gemCache = new ConcurrentDictionary<string, List<NinjaGemEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _loadedEndpoints = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private double _divinePriceInChaos = 160.0;
        public double DivineRate => _divinePriceInChaos;
        private string _activeLeague = string.Empty;
        private DateTime _lastFullFetchTime = DateTime.MinValue;

        private static readonly Regex FlaskVariantRegex = new Regex(@"Leftmost\s+(\d+)\s+Magic\s+Utility\s+Flask", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SmallPassiveVariantRegex = new Regex(@"Adds\s+(\d+)\s+Small\s+Passive\s+Skill", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RingSizeVariantRegex = new Regex(@"Only\s+affects\s+Passives\s+in\s+([A-Za-z\s]+?)\s+Ring", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private PoeNinjaClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

            _currencyCache["Chaos Orb"] = 1.0;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return key.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Trim();
        }

        private void CheckLeagueSwitch(string league)
        {
            if (string.IsNullOrWhiteSpace(league)) league = "Standard";
            if (!string.Equals(_activeLeague, league, StringComparison.OrdinalIgnoreCase))
            {
                _activeLeague = league;
                _loadedEndpoints.Clear();
                _currencyCache.Clear();
                _currencyChangeCache.Clear();
                _uniqueCache.Clear();
                _generalItemCache.Clear();
                _gemCache.Clear();
                _currencyCache["Chaos Orb"] = 1.0;
                _lastFullFetchTime = DateTime.MinValue;
            }
        }

        public async Task<Dictionary<string, double>> GetCurrencyRatesAsync(string league = "Standard")
        {
            if (string.IsNullOrWhiteSpace(league)) league = PoeSettingsManager.Instance.SelectedLeague ?? "Standard";
            CheckLeagueSwitch(league);

            await EnsureEndpointLoadedAsync(league, "Currency", isCurrencyOverview: true);
            return new Dictionary<string, double>(_currencyCache, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads Currency + the specific poe.ninja overview endpoint(s) matching the evaluated item in parallel.
        /// </summary>
        public async Task EnsureItemOverviewLoadedAsync(PoeItem item, string league = "Standard")
        {
            if (item == null) return;
            if (string.IsNullOrWhiteSpace(league)) league = PoeSettingsManager.Instance.SelectedLeague ?? "Standard";
            CheckLeagueSwitch(league);

            var tasks = new List<Task>
            {
                EnsureEndpointLoadedAsync(league, "Currency", isCurrencyOverview: true)
            };

            string cat = (item.Category ?? string.Empty).ToLowerInvariant();
            string cls = (item.ItemClass ?? string.Empty).ToLowerInvariant();
            string name = (item.Name ?? item.BaseType ?? string.Empty).ToLowerInvariant();

            if (item.Rarity == PoeRarity.Gem || item.Namespace == ItemNamespace.Gem || cls.Contains("gem"))
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "SkillGem", isCurrencyOverview: false));
            }
            else if (item.Rarity == PoeRarity.DivinationCard || item.Namespace == ItemNamespace.DivinationCard || cls.Contains("card"))
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "DivinationCard", isCurrencyOverview: false));
            }
            else if (item.Rarity == PoeRarity.Unique || item.Namespace == ItemNamespace.Unique)
            {
                if (cat.StartsWith("weapon") || cls.Contains("sword") || cls.Contains("axe") || cls.Contains("mace") ||
                    cls.Contains("bow") || cls.Contains("wand") || cls.Contains("dagger") || cls.Contains("claw") ||
                    cls.Contains("staff") || cls.Contains("sceptre"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueWeapon", isCurrencyOverview: false));
                }
                else if (cat.StartsWith("armour") || cls.Contains("armour") || cls.Contains("helmet") ||
                         cls.Contains("gloves") || cls.Contains("boots") || cls.Contains("shield") || cls.Contains("quiver"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueArmour", isCurrencyOverview: false));
                }
                else if (cat.StartsWith("accessory") || cls.Contains("amulet") || cls.Contains("ring") || cls.Contains("belt"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueAccessory", isCurrencyOverview: false));
                }
                else if (cat.StartsWith("flask") || cls.Contains("flask"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueFlask", isCurrencyOverview: false));
                }
                else if (cat.StartsWith("jewel") || cls.Contains("jewel"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueJewel", isCurrencyOverview: false));
                }
                else if (cat.StartsWith("map") || item.Namespace == ItemNamespace.Map || cls.Contains("map"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueMap", isCurrencyOverview: false));
                }
                else
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueWeapon", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueArmour", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueAccessory", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueJewel", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueFlask", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "UniqueMap", isCurrencyOverview: false));
                }
            }
            else if (item.Rarity == PoeRarity.Currency || item.Namespace == ItemNamespace.Currency ||
                     cls.Contains("currency") || cls.Contains("fragment") || cls.Contains("scarab") ||
                     cls.Contains("incubator") || cls.Contains("omen") || cls.Contains("tattoo"))
            {
                if (name.Contains("scarab"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Scarab", isCurrencyOverview: false));
                }
                else if (name.Contains("essence") || name.Contains("remnant of corruption"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Essence", isCurrencyOverview: false));
                }
                else if (name.Contains("fossil"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Fossil", isCurrencyOverview: false));
                }
                else if (name.Contains("resonator"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Resonator", isCurrencyOverview: false));
                }
                else if (name.Contains("oil") || name.Contains("extractor"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Oil", isCurrencyOverview: false));
                }
                else if (name.Contains("tattoo"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Tattoo", isCurrencyOverview: false));
                }
                else if (name.Contains("omen"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Omen", isCurrencyOverview: false));
                }
                else if (name.Contains("delirium orb"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "DeliriumOrb", isCurrencyOverview: false));
                }
                else if (name.Contains("incubator"))
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Incubator", isCurrencyOverview: false));
                }
                else
                {
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Fragment", isCurrencyOverview: true));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Scarab", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Essence", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Fossil", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Oil", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Tattoo", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "Omen", isCurrencyOverview: false));
                    tasks.Add(EnsureEndpointLoadedAsync(league, "DeliriumOrb", isCurrencyOverview: false));
                }
            }
            else if (item.Namespace == ItemNamespace.Map || cat.StartsWith("map") || cls.Contains("map"))
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "Map", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "BlightedMap", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "BlightRavagedMap", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Fragment", isCurrencyOverview: true));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Invitation", isCurrencyOverview: false));
            }
            else if (item.Namespace == ItemNamespace.CapturedBeast || cls.Contains("beast"))
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "Beast", isCurrencyOverview: false));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch { }
        }

        private async Task EnsureEndpointLoadedAsync(string league, string type, bool isCurrencyOverview)
        {
            string cacheKey = $"{league}:{type}";
            if (_loadedEndpoints.TryGetValue(cacheKey, out DateTime lastTime) &&
                DateTime.UtcNow - lastTime < TimeSpan.FromMinutes(15))
            {
                return;
            }

            try
            {
                string safeLeague = Uri.EscapeDataString(league);
                string primaryUrl = isCurrencyOverview
                    ? $"https://poe.ninja/poe1/api/economy/stash/current/currency/overview?league={safeLeague}&type={type}"
                    : $"https://poe.ninja/poe1/api/economy/stash/current/item/overview?league={safeLeague}&type={type}";

                string json = null;
                try
                {
                    var response = await _httpClient.GetAsync(primaryUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        json = await response.Content.ReadAsStringAsync();
                    }
                }
                catch { }

                // Fallback to legacy endpoint path if primary did not return valid JSON
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        string legacyUrl = isCurrencyOverview
                            ? $"https://poe.ninja/api/data/currencyoverview?league={safeLeague}&type={type}"
                            : $"https://poe.ninja/api/data/itemoverview?league={safeLeague}&type={type}";
                        var legacyResp = await _httpClient.GetAsync(legacyUrl);
                        if (legacyResp.IsSuccessStatusCode)
                        {
                            json = await legacyResp.Content.ReadAsStringAsync();
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{"))
                {
                    if (isCurrencyOverview)
                    {
                        ParsePoeNinjaCurrency(json);
                    }
                    else if (string.Equals(type, "SkillGem", StringComparison.OrdinalIgnoreCase))
                    {
                        ParsePoeNinjaGems(json);
                    }
                    else if (type.StartsWith("Unique", StringComparison.OrdinalIgnoreCase))
                    {
                        ParsePoeNinjaItems(json, _uniqueCache, isUniqueOverview: true);
                    }
                    else
                    {
                        ParsePoeNinjaItems(json, _generalItemCache, isUniqueOverview: false);
                    }
                    _loadedEndpoints[cacheKey] = DateTime.UtcNow;
                }
            }
            catch { }
        }

        public async Task RefreshRatesAsync(string league = "Standard")
        {
            CheckLeagueSwitch(league);
            if (DateTime.UtcNow - _lastFullFetchTime < TimeSpan.FromMinutes(15))
            {
                return;
            }

            _lastFullFetchTime = DateTime.UtcNow;

            try
            {
                var tasks = new List<Task>
                {
                    EnsureEndpointLoadedAsync(league, "Currency", true),
                    EnsureEndpointLoadedAsync(league, "Fragment", true),
                    EnsureEndpointLoadedAsync(league, "SkillGem", false),
                    EnsureEndpointLoadedAsync(league, "UniqueWeapon", false),
                    EnsureEndpointLoadedAsync(league, "UniqueArmour", false),
                    EnsureEndpointLoadedAsync(league, "UniqueAccessory", false),
                    EnsureEndpointLoadedAsync(league, "UniqueFlask", false),
                    EnsureEndpointLoadedAsync(league, "UniqueJewel", false),
                    EnsureEndpointLoadedAsync(league, "DivinationCard", false),
                    EnsureEndpointLoadedAsync(league, "Scarab", false),
                    EnsureEndpointLoadedAsync(league, "Essence", false)
                };
                await Task.WhenAll(tasks);
            }
            catch { }
        }

        private static double ExtractSparklineChange(JsonObject obj)
        {
            string[] sparkKeys = { "sparkLine", "sparkline", "receiveSparkLine", "paySparkLine" };
            foreach (string key in sparkKeys)
            {
                if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.Object)
                {
                    var spark = obj.GetNamedObject(key);
                    if (spark.ContainsKey("totalChange") && spark.GetNamedValue("totalChange").ValueType == JsonValueType.Number)
                    {
                        double val = spark.GetNamedNumber("totalChange");
                        if (val != 0) return val;
                    }
                }
            }
            return 0;
        }

        private void ParsePoeNinjaCurrency(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                if (!JsonObject.TryParse(json, out var root) || !root.ContainsKey("lines")) return;
                var linesVal = root.GetNamedValue("lines");
                if (linesVal.ValueType != JsonValueType.Array) return;

                var lines = linesVal.GetArray();
                foreach (var item in lines)
                {
                    try
                    {
                        if (item.ValueType != JsonValueType.Object) continue;
                        var obj = item.GetObject();

                        string name = (obj.ContainsKey("currencyTypeName") && obj.GetNamedValue("currencyTypeName").ValueType == JsonValueType.String)
                            ? NormalizeKey(obj.GetNamedString("currencyTypeName"))
                            : string.Empty;

                        double chaosValue = (obj.ContainsKey("chaosEquivalent") && obj.GetNamedValue("chaosEquivalent").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("chaosEquivalent")
                            : 0;

                        double changePct = ExtractSparklineChange(obj);

                        if (!string.IsNullOrEmpty(name) && chaosValue > 0)
                        {
                            _currencyCache[name] = chaosValue;
                            _currencyChangeCache[name] = changePct;
                            if (name.Equals("Divine Orb", StringComparison.OrdinalIgnoreCase))
                            {
                                _divinePriceInChaos = chaosValue;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ParsePoeNinjaGems(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                if (!JsonObject.TryParse(json, out var root) || !root.ContainsKey("lines")) return;
                var linesVal = root.GetNamedValue("lines");
                if (linesVal.ValueType != JsonValueType.Array) return;

                var lines = linesVal.GetArray();
                foreach (var item in lines)
                {
                    try
                    {
                        if (item.ValueType != JsonValueType.Object) continue;
                        var obj = item.GetObject();

                        string name = (obj.ContainsKey("name") && obj.GetNamedValue("name").ValueType == JsonValueType.String)
                            ? NormalizeKey(obj.GetNamedString("name"))
                            : string.Empty;

                        if (string.IsNullOrEmpty(name)) continue;

                        double chaosValue = (obj.ContainsKey("chaosValue") && obj.GetNamedValue("chaosValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("chaosValue")
                            : 0;

                        if (chaosValue <= 0) continue;

                        double divineValue = (obj.ContainsKey("divineValue") && obj.GetNamedValue("divineValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("divineValue")
                            : 0;

                        int gemLevel = (obj.ContainsKey("gemLevel") && obj.GetNamedValue("gemLevel").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("gemLevel")
                            : 1;

                        int gemQuality = (obj.ContainsKey("gemQuality") && obj.GetNamedValue("gemQuality").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("gemQuality")
                            : 0;

                        bool corrupted = obj.ContainsKey("corrupted") &&
                                         obj.GetNamedValue("corrupted").ValueType == JsonValueType.Boolean &&
                                         obj.GetNamedBoolean("corrupted");

                        string variant = (obj.ContainsKey("variant") && obj.GetNamedValue("variant").ValueType == JsonValueType.String)
                            ? obj.GetNamedString("variant")
                            : string.Empty;

                        int listingCount = (obj.ContainsKey("listingCount") && obj.GetNamedValue("listingCount").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("listingCount")
                            : ((obj.ContainsKey("count") && obj.GetNamedValue("count").ValueType == JsonValueType.Number) ? (int)obj.GetNamedNumber("count") : 0);

                        bool isExceptional = false;
                        if (obj.ContainsKey("metadata") && obj.GetNamedValue("metadata").ValueType == JsonValueType.Object)
                        {
                            var meta = obj.GetNamedObject("metadata");
                            if (meta.ContainsKey("gemKind") && meta.GetNamedValue("gemKind").ValueType == JsonValueType.String)
                            {
                                isExceptional = meta.GetNamedString("gemKind").IndexOf("exceptional", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                        }
                        if (!isExceptional &&
                            (name.StartsWith("Empower", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("Enlighten", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("Enhance", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("Awakened ", StringComparison.OrdinalIgnoreCase)))
                        {
                            isExceptional = true;
                        }

                        var entry = new NinjaGemEntry
                        {
                            Name = name,
                            Variant = variant,
                            GemLevel = gemLevel,
                            GemQuality = gemQuality,
                            Corrupted = corrupted,
                            IsExceptional = isExceptional,
                            ChaosValue = chaosValue,
                            DivineValue = divineValue,
                            ChangePercent = ExtractSparklineChange(obj),
                            ListingCount = listingCount
                        };

                        var list = _gemCache.GetOrAdd(name, _ => new List<NinjaGemEntry>());
                        lock (list)
                        {
                            list.Add(entry);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ParsePoeNinjaItems(string json, ConcurrentDictionary<string, List<NinjaItemEntry>> targetCache, bool isUniqueOverview)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                if (!JsonObject.TryParse(json, out var root) || !root.ContainsKey("lines")) return;
                var linesVal = root.GetNamedValue("lines");
                if (linesVal.ValueType != JsonValueType.Array) return;

                var lines = linesVal.GetArray();
                foreach (var item in lines)
                {
                    try
                    {
                        if (item.ValueType != JsonValueType.Object) continue;
                        var obj = item.GetObject();

                        string name = (obj.ContainsKey("name") && obj.GetNamedValue("name").ValueType == JsonValueType.String)
                            ? NormalizeKey(obj.GetNamedString("name"))
                            : string.Empty;

                        if (string.IsNullOrEmpty(name)) continue;

                        double chaosValue = (obj.ContainsKey("chaosValue") && obj.GetNamedValue("chaosValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("chaosValue")
                            : 0;

                        if (chaosValue <= 0) continue;

                        int itemClass = (obj.ContainsKey("itemClass") && obj.GetNamedValue("itemClass").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("itemClass")
                            : 0;

                        // Exclude Foil / Relic / Valdo variants (itemClass 9 or 10) from standard unique pricing
                        if (isUniqueOverview && (itemClass == 9 || itemClass == 10))
                        {
                            continue;
                        }

                        string baseType = (obj.ContainsKey("baseType") && obj.GetNamedValue("baseType").ValueType == JsonValueType.String)
                            ? NormalizeKey(obj.GetNamedString("baseType"))
                            : string.Empty;

                        string variant = (obj.ContainsKey("variant") && obj.GetNamedValue("variant").ValueType == JsonValueType.String)
                            ? obj.GetNamedString("variant").Trim()
                            : string.Empty;

                        int links = (obj.ContainsKey("links") && obj.GetNamedValue("links").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("links")
                            : 0;
                        if (links < 5) links = 0;

                        int mapTier = (obj.ContainsKey("mapTier") && obj.GetNamedValue("mapTier").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("mapTier")
                            : 0;

                        double divineValue = (obj.ContainsKey("divineValue") && obj.GetNamedValue("divineValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("divineValue")
                            : 0;

                        double dustVal = (obj.ContainsKey("dustValue") && obj.GetNamedValue("dustValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("dustValue")
                            : 0;

                        int listingCount = (obj.ContainsKey("listingCount") && obj.GetNamedValue("listingCount").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("listingCount")
                            : ((obj.ContainsKey("count") && obj.GetNamedValue("count").ValueType == JsonValueType.Number) ? (int)obj.GetNamedNumber("count") : 0);

                        var entry = new NinjaItemEntry
                        {
                            Name = name,
                            BaseType = baseType,
                            Variant = variant,
                            ItemClass = itemClass,
                            Links = links,
                            MapTier = mapTier,
                            ChaosValue = chaosValue,
                            DivineValue = divineValue,
                            ChangePercent = ExtractSparklineChange(obj),
                            DustValue = dustVal,
                            ListingCount = listingCount
                        };

                        var list = targetCache.GetOrAdd(name, _ => new List<NinjaItemEntry>());
                        lock (list)
                        {
                            list.Add(entry);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private string FormatSingleBenchmarkText(double chaosValue, double divineValue, double changePct, bool isDivineOrbItself = false)
        {
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;
            string changeSuffix = Math.Abs(changePct) >= 0.1
                ? $" ({changePct.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}%)"
                : string.Empty;

            if (isDivineOrbItself)
            {
                return $"{Math.Round(chaosValue, 1).ToString("0.#", CultureInfo.InvariantCulture)} Chaos{changeSuffix}";
            }

            double resolvedDivine = divineValue > 0 ? divineValue : (chaosValue / divRate);
            if (resolvedDivine >= 1.0 || chaosValue >= divRate)
            {
                double div = Math.Round(resolvedDivine, 1);
                return $"{div.ToString("0.#", CultureInfo.InvariantCulture)} Divine (≈ {Math.Round(chaosValue).ToString(CultureInfo.InvariantCulture)} C){changeSuffix}";
            }

            return $"{Math.Round(chaosValue, 1).ToString("0.#", CultureInfo.InvariantCulture)} Chaos{changeSuffix}";
        }

        private static string FormatDustText(double dustValue)
        {
            if (dustValue <= 0) return string.Empty;
            if (dustValue >= 1000000)
                return $"{(dustValue / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture)}M";
            if (dustValue >= 1000)
                return $"{(dustValue / 1000.0).ToString("0.#", CultureInfo.InvariantCulture)}K";
            return Math.Round(dustValue).ToString(CultureInfo.InvariantCulture);
        }

        private BenchmarkResult EvaluateGemBenchmark(PoeItem item, List<NinjaGemEntry> entries)
        {
            if (entries == null || entries.Count == 0) return new BenchmarkResult { Found = false };

            List<NinjaGemEntry> snapshot;
            lock (entries)
            {
                snapshot = new List<NinjaGemEntry>(entries);
            }

            int itemLvl = item.GemLevel > 0 ? item.GemLevel : 1;
            int itemQual = item.Quality;
            bool itemCorr = item.IsCorrupted;

            bool isExceptional = false;
            int maxUncorruptedLevel = 1;
            foreach (var e in snapshot)
            {
                if (e.IsExceptional) isExceptional = true;
                if (!e.Corrupted && e.GemLevel > maxUncorruptedLevel)
                {
                    maxUncorruptedLevel = e.GemLevel;
                }
            }
            if (maxUncorruptedLevel <= 6 && snapshot.Count > 1)
            {
                isExceptional = true;
            }

            NinjaGemEntry best = null;
            int bestScore = int.MaxValue;

            if (isExceptional)
            {
                // Exceptional / Awakened gems: level and corruption are the primary price determinants
                foreach (var e in snapshot)
                {
                    int score = 0;
                    if (e.Corrupted != itemCorr) score += 500;
                    score += Math.Abs(e.GemLevel - itemLvl) * 100;
                    score += Math.Abs(e.GemQuality - (itemQual >= 18 ? 20 : 0)) * 5;
                    if (e.ListingCount <= 1) score += 25;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = e;
                    }
                }
            }
            else
            {
                int targetLvl = itemLvl >= 21 ? 21 : (itemLvl >= 20 ? 20 : 1);
                int targetQual = itemQual >= 23 ? 23 : (itemQual >= 18 ? 20 : 0);

                foreach (var e in snapshot)
                {
                    int score = 0;
                    if (targetLvl >= 21 || targetQual >= 23)
                    {
                        if (!e.Corrupted) score += 1000;
                    }
                    else if (e.Corrupted != itemCorr)
                    {
                        score += 400;
                    }

                    score += Math.Abs(e.GemLevel - targetLvl) * 100;
                    score += Math.Abs(e.GemQuality - targetQual) * 15;
                    if (e.ListingCount <= 1) score += 20;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = e;
                    }
                }
            }

            if (best == null) best = snapshot[0];

            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;
            double divVal = best.DivineValue > 0 ? best.DivineValue : Math.Round(best.ChaosValue / divRate, 1);

            return new BenchmarkResult
            {
                Found = true,
                ChaosEquivalent = best.ChaosValue,
                DivineEquivalent = Math.Round(divVal, 1),
                MinChaos = best.ChaosValue,
                MaxChaos = best.ChaosValue,
                ChangePercent = best.ChangePercent,
                FormattedText = FormatSingleBenchmarkText(best.ChaosValue, best.DivineValue, best.ChangePercent),
                Confidence = "High",
                Source = "poe.ninja (Skill Gem)"
            };
        }

        private static NinjaItemEntry TryMatchUniqueVariant(PoeItem item, List<NinjaItemEntry> candidates)
        {
            if (item == null || candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            string desiredVariant = null;

            // 1. Check modifiers for specific roll-based variants (Mageblood, Voices, Thread of Hope, Abyss sockets)
            int abyssSocketsInMods = 0;
            if (item.Modifiers != null)
            {
                foreach (var mod in item.Modifiers)
                {
                    string text = mod?.RawText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var flaskMatch = FlaskVariantRegex.Match(text);
                    if (flaskMatch.Success)
                    {
                        desiredVariant = $"{flaskMatch.Groups[1].Value} Flasks";
                        break;
                    }

                    var passiveMatch = SmallPassiveVariantRegex.Match(text);
                    if (passiveMatch.Success)
                    {
                        string countStr = passiveMatch.Groups[1].Value;
                        desiredVariant = countStr == "1" ? "1 passives" : $"{countStr} passives";
                        break;
                    }

                    var ringMatch = RingSizeVariantRegex.Match(text);
                    if (ringMatch.Success)
                    {
                        desiredVariant = $"{ringMatch.Groups[1].Value.Trim()} Ring";
                        break;
                    }

                    if (text.IndexOf("Abyssal Socket", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var numMatch = Regex.Match(text, @"(\d+)");
                        if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int sc))
                        {
                            abyssSocketsInMods += sc;
                        }
                        else
                        {
                            abyssSocketsInMods += 1;
                        }
                    }
                }
            }

            // Voices with 1 small passive has no "Adds X Small Passive Skills" line on the item
            if (desiredVariant == null &&
                !string.IsNullOrWhiteSpace(item.Name) &&
                item.Name.Equals("Voices", StringComparison.OrdinalIgnoreCase) &&
                !item.IsUnidentified &&
                item.Modifiers != null &&
                item.Modifiers.Count > 0)
            {
                foreach (var c in candidates)
                {
                    if (string.IsNullOrEmpty(c.Variant) || c.Variant.StartsWith("1", StringComparison.OrdinalIgnoreCase))
                    {
                        return c;
                    }
                }
            }

            if (desiredVariant == null && abyssSocketsInMods > 0)
            {
                desiredVariant = abyssSocketsInMods == 1 ? "1 Socket" : $"{abyssSocketsInMods} Sockets";
            }

            if (!string.IsNullOrEmpty(desiredVariant))
            {
                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(c.Variant) &&
                        (c.Variant.Equals(desiredVariant, StringComparison.OrdinalIgnoreCase) ||
                         c.Variant.IndexOf(desiredVariant, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         desiredVariant.IndexOf(c.Variant, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return c;
                    }
                }
            }

            // 2. General modifier substring check against candidate Variant names
            if (item.Modifiers != null && item.Modifiers.Count > 0)
            {
                foreach (var c in candidates)
                {
                    if (string.IsNullOrWhiteSpace(c.Variant)) continue;
                    foreach (var mod in item.Modifiers)
                    {
                        if (!string.IsNullOrWhiteSpace(mod.RawText) &&
                            mod.RawText.IndexOf(c.Variant, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return c;
                        }
                    }
                }
            }

            // 3. Fallback: prefer candidate with no variant string or highest listing count (standard base version)
            NinjaItemEntry noVariantEntry = null;
            NinjaItemEntry mostListedEntry = null;
            foreach (var c in candidates)
            {
                if (string.IsNullOrEmpty(c.Variant) && (noVariantEntry == null || c.ListingCount > noVariantEntry.ListingCount))
                {
                    noVariantEntry = c;
                }
                if (mostListedEntry == null || c.ListingCount > mostListedEntry.ListingCount)
                {
                    mostListedEntry = c;
                }
            }

            return noVariantEntry ?? mostListedEntry ?? candidates[0];
        }

        private BenchmarkResult EvaluateItemEntries(PoeItem item, List<NinjaItemEntry> entries, string source)
        {
            if (entries == null || entries.Count == 0) return new BenchmarkResult { Found = false };

            List<NinjaItemEntry> snapshot;
            lock (entries)
            {
                snapshot = new List<NinjaItemEntry>(entries);
            }

            // 1. Filter by BaseType if applicable
            string itemBase = NormalizeKey(item.BaseType);
            if (!string.IsNullOrWhiteSpace(itemBase) &&
                !string.Equals(item.Name, item.BaseType, StringComparison.OrdinalIgnoreCase))
            {
                var baseMatches = new List<NinjaItemEntry>();
                foreach (var e in snapshot)
                {
                    if (!string.IsNullOrWhiteSpace(e.BaseType) &&
                        string.Equals(e.BaseType, itemBase, StringComparison.OrdinalIgnoreCase))
                    {
                        baseMatches.Add(e);
                    }
                }
                if (baseMatches.Count > 0)
                {
                    snapshot = baseMatches;
                }
            }

            // 2. Filter by Link tier (0-4L -> 0, 5L -> 5, 6L -> 6)
            int targetLinks = item.LinkCount >= 6 ? 6 : (item.LinkCount == 5 ? 5 : 0);
            var linkMatches = new List<NinjaItemEntry>();
            foreach (var e in snapshot)
            {
                if (e.Links == targetLinks)
                {
                    linkMatches.Add(e);
                }
            }
            if (linkMatches.Count == 0 && targetLinks > 0)
            {
                foreach (var e in snapshot)
                {
                    if (e.Links == 0) linkMatches.Add(e);
                }
            }
            if (linkMatches.Count > 0)
            {
                snapshot = linkMatches;
            }

            // 3. Filter by MapTier if evaluating a Map
            if (item.MapTier > 0 && snapshot.Count > 1)
            {
                NinjaItemEntry bestMap = null;
                int bestTierDiff = int.MaxValue;
                foreach (var e in snapshot)
                {
                    if (e.MapTier > 0)
                    {
                        int diff = Math.Abs(e.MapTier - item.MapTier);
                        if (diff < bestTierDiff)
                        {
                            bestTierDiff = diff;
                            bestMap = e;
                        }
                    }
                }
                if (bestMap != null)
                {
                    snapshot = new List<NinjaItemEntry> { bestMap };
                }
            }

            NinjaItemEntry chosen = TryMatchUniqueVariant(item, snapshot) ?? snapshot[0];
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;
            double divVal = chosen.DivineValue > 0 ? chosen.DivineValue : Math.Round(chosen.ChaosValue / divRate, 1);

            return new BenchmarkResult
            {
                Found = true,
                ChaosEquivalent = chosen.ChaosValue,
                DivineEquivalent = Math.Round(divVal, 1),
                MinChaos = chosen.ChaosValue,
                MaxChaos = chosen.ChaosValue,
                ChangePercent = chosen.ChangePercent,
                DustValue = chosen.DustValue,
                FormattedText = FormatSingleBenchmarkText(chosen.ChaosValue, chosen.DivineValue, chosen.ChangePercent),
                DustText = FormatDustText(chosen.DustValue),
                Confidence = "High",
                Source = source
            };
        }

        public BenchmarkResult GetBenchmark(PoeItem item)
        {
            if (item == null) return new BenchmarkResult { Found = false };

            // Never return poe.ninja benchmark for Rare or Magic equipment (unless it is a Map)
            if ((item.Rarity == PoeRarity.Rare || item.Rarity == PoeRarity.Magic) &&
                item.Namespace != ItemNamespace.Map)
            {
                return new BenchmarkResult { Found = false };
            }

            string lookupName = NormalizeKey(!string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType);
            string baseName = NormalizeKey(item.BaseType);
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;

            // 1. Check Currency & Fragment Cache
            if (item.Rarity == PoeRarity.Currency || item.Namespace == ItemNamespace.Currency ||
                item.Rarity == PoeRarity.Normal)
            {
                if (!string.IsNullOrWhiteSpace(lookupName) && _currencyCache.TryGetValue(lookupName, out double currencyChaos))
                {
                    _currencyChangeCache.TryGetValue(lookupName, out double currChange);
                    bool isDivine = lookupName.Equals("Divine Orb", StringComparison.OrdinalIgnoreCase);
                    return new BenchmarkResult
                    {
                        Found = true,
                        ChaosEquivalent = currencyChaos,
                        DivineEquivalent = Math.Round(currencyChaos / divRate, 1),
                        MinChaos = currencyChaos,
                        MaxChaos = currencyChaos,
                        ChangePercent = currChange,
                        FormattedText = FormatSingleBenchmarkText(currencyChaos, 0, currChange, isDivine),
                        Confidence = "Very High",
                        Source = "poe.ninja (Currency)"
                    };
                }
                if (!string.IsNullOrWhiteSpace(baseName) && _currencyCache.TryGetValue(baseName, out double currencyBaseChaos))
                {
                    _currencyChangeCache.TryGetValue(baseName, out double currBaseChange);
                    bool isDivine = baseName.Equals("Divine Orb", StringComparison.OrdinalIgnoreCase);
                    return new BenchmarkResult
                    {
                        Found = true,
                        ChaosEquivalent = currencyBaseChaos,
                        DivineEquivalent = Math.Round(currencyBaseChaos / divRate, 1),
                        MinChaos = currencyBaseChaos,
                        MaxChaos = currencyBaseChaos,
                        ChangePercent = currBaseChange,
                        FormattedText = FormatSingleBenchmarkText(currencyBaseChaos, 0, currBaseChange, isDivine),
                        Confidence = "High",
                        Source = "poe.ninja (Currency)"
                    };
                }
            }

            // 2. Check Skill Gem Cache (matched by exact gem name + level/quality/corrupted)
            if (item.Rarity == PoeRarity.Gem || item.Namespace == ItemNamespace.Gem)
            {
                if (!string.IsNullOrWhiteSpace(lookupName) && _gemCache.TryGetValue(lookupName, out var gemEntries))
                {
                    return EvaluateGemBenchmark(item, gemEntries);
                }
                return new BenchmarkResult { Found = false };
            }

            // 3. Check Unique Cache (only when item is Unique)
            if (item.Rarity == PoeRarity.Unique || item.Namespace == ItemNamespace.Unique)
            {
                if (!string.IsNullOrWhiteSpace(lookupName) && _uniqueCache.TryGetValue(lookupName, out var uniqueEntries))
                {
                    return EvaluateItemEntries(item, uniqueEntries, "poe.ninja (Unique)");
                }
                return new BenchmarkResult { Found = false };
            }

            // 4. Check General Item Cache (Divination Cards, Scarabs, Essences, Fossils, Oils, Maps, Invitations, Beasts)
            if (!string.IsNullOrWhiteSpace(lookupName) && _generalItemCache.TryGetValue(lookupName, out var genEntries))
            {
                return EvaluateItemEntries(item, genEntries, "poe.ninja (Market Benchmark)");
            }
            if (!string.IsNullOrWhiteSpace(baseName) && _generalItemCache.TryGetValue(baseName, out var baseGenEntries))
            {
                return EvaluateItemEntries(item, baseGenEntries, "poe.ninja (Market Benchmark)");
            }

            return new BenchmarkResult { Found = false };
        }
    }
}
