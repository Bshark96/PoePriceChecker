using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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

    internal sealed class NinjaItemRecord
    {
        public double BaseChaos { get; set; }
        public double Links5Chaos { get; set; }
        public double Links6Chaos { get; set; }
        public double MinChaos { get; set; }
        public double MaxChaos { get; set; }
        public double ChangePercent { get; set; }
        public double DustValue { get; set; }
    }

    /// <summary>
    /// Fast-path in-memory pricing client using poe.ninja benchmark dumps.
    /// Provides targeted parallel fetching and lookups for currency, skill gems, divination cards, and uniques.
    /// </summary>
    public sealed class PoeNinjaClient
    {
        private static readonly Lazy<PoeNinjaClient> _lazy = new Lazy<PoeNinjaClient>(() => new PoeNinjaClient());
        public static PoeNinjaClient Instance => _lazy.Value;

        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, double> _currencyCache = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, double> _currencyChangeCache = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, NinjaItemRecord> _itemCache = new ConcurrentDictionary<string, NinjaItemRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, NinjaItemRecord> _gemCache = new ConcurrentDictionary<string, NinjaItemRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _loadedEndpoints = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private double _divinePriceInChaos = 160.0;
        public double DivineRate => _divinePriceInChaos;
        private string _activeLeague = string.Empty;
        private DateTime _lastFullFetchTime = DateTime.MinValue;

        private PoeNinjaClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PoeGameBarOverlay/1.0 (Contact: admin@example.com)");

            SeedFallbackData();
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return key.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Trim();
        }

        private void SeedFallbackData()
        {
            _currencyCache["Divine Orb"] = 160.0;
            _currencyCache["Mirror of Kalandra"] = 185000.0;
            _currencyCache["Exalted Orb"] = 18.0;
            _currencyCache["Veiled Orb"] = 145.0;
            _currencyCache["Ancient Orb"] = 12.0;
            _currencyCache["Orb of Annulment"] = 8.0;
            _currencyCache["Sacred Orb"] = 42.0;
            _currencyCache["Awakened Sextant"] = 4.0;
            _currencyCache["Chaos Orb"] = 1.0;

            SeedItemRecord(_itemCache, "Mageblood", 32000.0, 0, 295000);
            SeedItemRecord(_itemCache, "Headhunter", 7200.0, 0, 185000);
            SeedItemRecord(_itemCache, "Ventor's Gamble", 640.0, 0, 12000);
            SeedItemRecord(_itemCache, "The Squire", 3200.0, 0, 95000);
            SeedItemRecord(_itemCache, "Ashes of the Stars", 4800.0, 0, 64000);
            SeedItemRecord(_itemCache, "Kalandra's Touch", 2800.0, 0, 110000);
            SeedItemRecord(_itemCache, "Rakiata's Dance", 640.0, 1920.0, 44300);

            SeedItemRecord(_gemCache, "Flameblast of Celerity", 20.0, 0, 0);
            SeedItemRecord(_gemCache, "Flameblast of Contraction", 15.0, 0, 0);
            SeedItemRecord(_gemCache, "Tornado of Elemental Turbulence", 35.0, 0, 0);
            SeedItemRecord(_gemCache, "Viper Strike of the Mamba", 45.0, 0, 0);
            SeedItemRecord(_gemCache, "Volcanic Fissure of Snaking", 25.0, 0, 0);
            SeedItemRecord(_gemCache, "Empower Support", 320.0, 0, 0);
            SeedItemRecord(_gemCache, "Enlighten Support", 480.0, 0, 0);
            SeedItemRecord(_gemCache, "Enhance Support", 160.0, 0, 0);
        }

        private static void SeedItemRecord(ConcurrentDictionary<string, NinjaItemRecord> dict, string name, double baseChaos, double links6Chaos, double dust)
        {
            string norm = NormalizeKey(name);
            dict[norm] = new NinjaItemRecord
            {
                BaseChaos = baseChaos,
                Links6Chaos = links6Chaos,
                MinChaos = baseChaos,
                MaxChaos = links6Chaos > baseChaos ? links6Chaos : baseChaos,
                DustValue = dust
            };
        }

        private void CheckLeagueSwitch(string league)
        {
            if (string.IsNullOrWhiteSpace(league)) league = "Standard";
            if (!string.Equals(_activeLeague, league, StringComparison.OrdinalIgnoreCase))
            {
                _activeLeague = league;
                _loadedEndpoints.Clear();
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
        /// Quickly loads Currency + the specific poe.ninja overview endpoint(s) matching the evaluated item in parallel.
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

            if (item.Rarity == PoeRarity.Gem || item.Namespace == ItemNamespace.Gem)
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "SkillGem", isCurrencyOverview: false));
            }
            else if (item.Rarity == PoeRarity.DivinationCard || item.Namespace == ItemNamespace.DivinationCard)
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
                }
            }
            else if (item.Rarity == PoeRarity.Currency || item.Namespace == ItemNamespace.Currency)
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "Fragment", isCurrencyOverview: true));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Scarab", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Essence", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Fossil", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Oil", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Tattoo", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "Omen", isCurrencyOverview: false));
            }
            else if (item.Namespace == ItemNamespace.Map || cat.StartsWith("map"))
            {
                tasks.Add(EnsureEndpointLoadedAsync(league, "Map", isCurrencyOverview: false));
                tasks.Add(EnsureEndpointLoadedAsync(league, "BlightedMap", isCurrencyOverview: false));
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
                string url = isCurrencyOverview
                    ? $"https://poe.ninja/api/data/currencyoverview?league={safeLeague}&type={type}"
                    : $"https://poe.ninja/api/data/itemoverview?league={safeLeague}&type={type}";

                string json = await _httpClient.GetStringAsync(url);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    if (isCurrencyOverview)
                    {
                        ParsePoeNinjaCurrency(json);
                    }
                    else if (string.Equals(type, "SkillGem", StringComparison.OrdinalIgnoreCase))
                    {
                        ParsePoeNinjaItems(json, _gemCache);
                    }
                    else
                    {
                        ParsePoeNinjaItems(json, _itemCache);
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

                        double changePct = 0;
                        if (obj.ContainsKey("receiveSparkLine") && obj.GetNamedValue("receiveSparkLine").ValueType == JsonValueType.Object)
                        {
                            var spark = obj.GetNamedObject("receiveSparkLine");
                            if (spark.ContainsKey("totalChange") && spark.GetNamedValue("totalChange").ValueType == JsonValueType.Number)
                            {
                                changePct = spark.GetNamedNumber("totalChange");
                            }
                        }

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

        private void ParsePoeNinjaItems(string json, ConcurrentDictionary<string, NinjaItemRecord> targetCache)
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

                        // Skip Relic / Foil variants (itemClass == 9) if normal unique already exists
                        if (itemClass == 9 && targetCache.ContainsKey(name))
                        {
                            continue;
                        }

                        int links = (obj.ContainsKey("links") && obj.GetNamedValue("links").ValueType == JsonValueType.Number)
                            ? (int)obj.GetNamedNumber("links")
                            : 0;

                        double dustVal = (obj.ContainsKey("dustValue") && obj.GetNamedValue("dustValue").ValueType == JsonValueType.Number)
                            ? obj.GetNamedNumber("dustValue")
                            : 0;

                        double changePct = 0;
                        if (obj.ContainsKey("sparkline") && obj.GetNamedValue("sparkline").ValueType == JsonValueType.Object)
                        {
                            var spark = obj.GetNamedObject("sparkline");
                            if (spark.ContainsKey("totalChange") && spark.GetNamedValue("totalChange").ValueType == JsonValueType.Number)
                            {
                                changePct = spark.GetNamedNumber("totalChange");
                            }
                        }

                        if (!targetCache.TryGetValue(name, out var rec))
                        {
                            rec = new NinjaItemRecord
                            {
                                BaseChaos = chaosValue,
                                MinChaos = chaosValue,
                                MaxChaos = chaosValue,
                                ChangePercent = changePct,
                                DustValue = dustVal
                            };
                            targetCache[name] = rec;
                        }

                        if (links >= 6)
                        {
                            rec.Links6Chaos = chaosValue;
                        }
                        else if (links == 5)
                        {
                            rec.Links5Chaos = chaosValue;
                        }
                        else
                        {
                            rec.BaseChaos = chaosValue;
                            if (changePct != 0) rec.ChangePercent = changePct;
                        }

                        if (dustVal > 0) rec.DustValue = dustVal;
                        if (rec.MinChaos <= 0 || chaosValue < rec.MinChaos) rec.MinChaos = chaosValue;
                        if (chaosValue > rec.MaxChaos) rec.MaxChaos = chaosValue;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private string FormatBenchmarkText(double primaryChaos, double minChaos, double maxChaos, double changePct)
        {
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;
            string changeSuffix = Math.Abs(changePct) >= 0.1
                ? $" ({changePct.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}%)"
                : string.Empty;

            if (minChaos > 0 && maxChaos > minChaos * 1.15 && maxChaos >= divRate)
            {
                double minDiv = Math.Round(minChaos / divRate, 1);
                double maxDiv = Math.Round(maxChaos / divRate, 1);
                if (maxDiv > minDiv)
                {
                    return $"{minDiv.ToString("0.#", CultureInfo.InvariantCulture)} -> {maxDiv.ToString("0.#", CultureInfo.InvariantCulture)} Divine{changeSuffix}";
                }
            }

            if (primaryChaos >= divRate)
            {
                double div = Math.Round(primaryChaos / divRate, 1);
                return $"{div.ToString("0.#", CultureInfo.InvariantCulture)} Divine (≈ {Math.Round(primaryChaos).ToString(CultureInfo.InvariantCulture)} C){changeSuffix}";
            }

            return $"{Math.Round(primaryChaos, 1).ToString("0.#", CultureInfo.InvariantCulture)} Chaos{changeSuffix}";
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

        private BenchmarkResult BuildItemBenchmarkResult(PoeItem item, NinjaItemRecord rec, string source, string confidence)
        {
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;
            double primaryChaos = rec.BaseChaos > 0 ? rec.BaseChaos : rec.MinChaos;
            if (item.LinkCount >= 6 && rec.Links6Chaos > 0)
            {
                primaryChaos = rec.Links6Chaos;
            }
            else if (item.LinkCount == 5 && rec.Links5Chaos > 0)
            {
                primaryChaos = rec.Links5Chaos;
            }

            return new BenchmarkResult
            {
                Found = true,
                ChaosEquivalent = primaryChaos,
                DivineEquivalent = Math.Round(primaryChaos / divRate, 1),
                MinChaos = rec.MinChaos,
                MaxChaos = rec.MaxChaos,
                ChangePercent = rec.ChangePercent,
                DustValue = rec.DustValue,
                FormattedText = FormatBenchmarkText(primaryChaos, rec.MinChaos, rec.MaxChaos, rec.ChangePercent),
                DustText = FormatDustText(rec.DustValue),
                Confidence = confidence,
                Source = source
            };
        }

        public BenchmarkResult GetBenchmark(PoeItem item)
        {
            if (item == null) return new BenchmarkResult();

            string lookupName = NormalizeKey(!string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType);
            string baseName = NormalizeKey(item.BaseType);
            double divRate = _divinePriceInChaos > 0 ? _divinePriceInChaos : 160.0;

            // 1. Check Currency Cache
            if (!string.IsNullOrWhiteSpace(lookupName) && _currencyCache.TryGetValue(lookupName, out double currencyChaos))
            {
                _currencyChangeCache.TryGetValue(lookupName, out double currChange);
                return new BenchmarkResult
                {
                    Found = true,
                    ChaosEquivalent = currencyChaos,
                    DivineEquivalent = Math.Round(currencyChaos / divRate, 1),
                    ChangePercent = currChange,
                    FormattedText = FormatBenchmarkText(currencyChaos, currencyChaos, currencyChaos, currChange),
                    Confidence = "Very High",
                    Source = "poe.ninja (Currency)"
                };
            }
            if (!string.IsNullOrWhiteSpace(baseName) && _currencyCache.TryGetValue(baseName, out double currencyBaseChaos))
            {
                _currencyChangeCache.TryGetValue(baseName, out double currBaseChange);
                return new BenchmarkResult
                {
                    Found = true,
                    ChaosEquivalent = currencyBaseChaos,
                    DivineEquivalent = Math.Round(currencyBaseChaos / divRate, 1),
                    ChangePercent = currBaseChange,
                    FormattedText = FormatBenchmarkText(currencyBaseChaos, currencyBaseChaos, currencyBaseChaos, currBaseChange),
                    Confidence = "High",
                    Source = "poe.ninja (Currency)"
                };
            }

            // 2. Check Gem Cache
            if (item.Rarity == PoeRarity.Gem || item.Namespace == ItemNamespace.Gem || (!string.IsNullOrWhiteSpace(lookupName) && _gemCache.ContainsKey(lookupName)))
            {
                if (!string.IsNullOrWhiteSpace(lookupName) && _gemCache.TryGetValue(lookupName, out var gemRec))
                {
                    return BuildItemBenchmarkResult(item, gemRec, "poe.ninja (Skill Gem)", "High");
                }
                string normVariant = NormalizeKey(item.NormalGemVariant);
                if (!string.IsNullOrWhiteSpace(normVariant) && _gemCache.TryGetValue(normVariant, out var baseGemRec))
                {
                    return BuildItemBenchmarkResult(item, baseGemRec, "poe.ninja (Base Gem Benchmark)", "Medium");
                }
            }

            // 3. Check Unique & Item Cache
            if (!string.IsNullOrWhiteSpace(lookupName) && _itemCache.TryGetValue(lookupName, out var itemRec))
            {
                return BuildItemBenchmarkResult(item, itemRec, "poe.ninja (Market Benchmark)", "High");
            }
            if (!string.IsNullOrWhiteSpace(baseName) && _itemCache.TryGetValue(baseName, out var baseItemRec))
            {
                return BuildItemBenchmarkResult(item, baseItemRec, "poe.ninja (Base Market Benchmark)", "Medium");
            }

            return new BenchmarkResult { Found = false };
        }
    }
}
