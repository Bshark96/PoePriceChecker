using System;
using System.Collections.Generic;
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
        public string Source { get; set; } = "poe.ninja";
        public string Confidence { get; set; } = "High";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Fast-path in-memory pricing client using poe.ninja benchmark dumps.
    /// Provides 0ms in-game lookups for currency, skill gems (including transfigured), divination cards, and standard uniques.
    /// </summary>
    public sealed class PoeNinjaClient
    {
        private static readonly Lazy<PoeNinjaClient> _lazy = new Lazy<PoeNinjaClient>(() => new PoeNinjaClient());
        public static PoeNinjaClient Instance => _lazy.Value;

        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, double> _currencyCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _itemCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _gemCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private double _divinePriceInChaos = 160.0;
        private DateTime _lastFetchTime = DateTime.MinValue;

        private PoeNinjaClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PoeGameBarOverlay/1.0 (Contact: admin@example.com)");

            // Seed fallback benchmark database so the tool works even before network fetch
            SeedFallbackData();
        }

        private void SeedFallbackData()
        {
            // Standard Currency
            _currencyCache["Divine Orb"] = 160.0;
            _currencyCache["Mirror of Kalandra"] = 185000.0;
            _currencyCache["Exalted Orb"] = 18.0;
            _currencyCache["Veiled Orb"] = 145.0;
            _currencyCache["Ancient Orb"] = 12.0;
            _currencyCache["Orb of Annulment"] = 8.0;
            _currencyCache["Sacred Orb"] = 42.0;
            _currencyCache["Awakened Sextant"] = 4.0;
            _currencyCache["Chaos Orb"] = 1.0;

            // Popular Uniques
            _itemCache["Mageblood"] = 32000.0; // ~200 Divines
            _itemCache["Headhunter"] = 7200.0;  // ~45 Divines
            _itemCache["Ventor's Gamble"] = 640.0; // ~4 Divines
            _itemCache["The Squire"] = 3200.0;  // ~20 Divines
            _itemCache["Ashes of the Stars"] = 4800.0; // ~30 Divines
            _itemCache["Kalandra's Touch"] = 2800.0; // ~17.5 Divines

            // Popular Gems
            _gemCache["Flameblast of Celerity"] = 20.0;
            _gemCache["Flameblast of Contraction"] = 15.0;
            _gemCache["Tornado of Elemental Turbulence"] = 35.0;
            _gemCache["Viper Strike of the Mamba"] = 45.0;
            _gemCache["Volcanic Fissure of Snaking"] = 25.0;
            _gemCache["Empower Support"] = 320.0;
            _gemCache["Enlighten Support"] = 480.0;
            _gemCache["Enhance Support"] = 160.0;
        }

        public async Task<Dictionary<string, double>> GetCurrencyRatesAsync(string league = "Standard")
        {
            await RefreshRatesAsync(league);
            return new Dictionary<string, double>(_currencyCache, StringComparer.OrdinalIgnoreCase);
        }

        public async Task RefreshRatesAsync(string league = "Standard")
        {
            if (DateTime.UtcNow - _lastFetchTime < TimeSpan.FromMinutes(15))
            {
                return;
            }

            try
            {
                string safeLeague = Uri.EscapeDataString(league);

                // 1. Fetch Currency Overview
                string currUrl = $"https://poe.ninja/api/data/currencyoverview?league={safeLeague}&type=Currency";
                string currJson = await _httpClient.GetStringAsync(currUrl);
                ParsePoeNinjaCurrency(currJson);

                // 2. Fetch Skill Gems Overview (including Transfigured Gems)
                string gemUrl = $"https://poe.ninja/api/data/itemoverview?league={safeLeague}&type=SkillGem";
                string gemJson = await _httpClient.GetStringAsync(gemUrl);
                ParsePoeNinjaItems(gemJson, _gemCache);

                // 3. Fetch Unique Weapons, Armours, Accessories
                string[] itemTypes = new[] { "UniqueWeapon", "UniqueArmour", "UniqueAccessory", "UniqueFlask", "UniqueJewel", "DivinationCard" };
                foreach (string t in itemTypes)
                {
                    try
                    {
                        string uUrl = $"https://poe.ninja/api/data/itemoverview?league={safeLeague}&type={t}";
                        string uJson = await _httpClient.GetStringAsync(uUrl);
                        ParsePoeNinjaItems(uJson, _itemCache);
                    }
                    catch { }
                }

                _lastFetchTime = DateTime.UtcNow;
            }
            catch
            {
                // Fallback to seeded benchmarks
            }
        }

        private void ParsePoeNinjaCurrency(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                if (JsonObject.TryParse(json, out var root) && root.ContainsKey("lines"))
                {
                    var lines = root.GetNamedArray("lines");
                    foreach (var item in lines)
                    {
                        if (item.ValueType == JsonValueType.Object)
                        {
                            var obj = item.GetObject();
                            string name = obj.ContainsKey("currencyTypeName") ? obj.GetNamedString("currencyTypeName") : string.Empty;
                            double chaosValue = obj.ContainsKey("chaosEquivalent") ? obj.GetNamedNumber("chaosEquivalent") : 0;

                            if (!string.IsNullOrEmpty(name) && chaosValue > 0)
                            {
                                _currencyCache[name] = chaosValue;
                                if (name.Equals("Divine Orb", StringComparison.OrdinalIgnoreCase))
                                {
                                    _divinePriceInChaos = chaosValue;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void ParsePoeNinjaItems(string json, Dictionary<string, double> targetCache)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                if (JsonObject.TryParse(json, out var root) && root.ContainsKey("lines"))
                {
                    var lines = root.GetNamedArray("lines");
                    foreach (var item in lines)
                    {
                        if (item.ValueType == JsonValueType.Object)
                        {
                            var obj = item.GetObject();
                            string name = obj.ContainsKey("name") ? obj.GetNamedString("name") : string.Empty;
                            double chaosValue = obj.ContainsKey("chaosValue") ? obj.GetNamedNumber("chaosValue") : 0;

                            if (!string.IsNullOrEmpty(name) && chaosValue > 0)
                            {
                                targetCache[name] = chaosValue;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public BenchmarkResult GetBenchmark(PoeItem item)
        {
            if (item == null) return new BenchmarkResult();

            string lookupName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;

            // 1. Check Currency Cache
            if (_currencyCache.TryGetValue(lookupName, out double currencyChaos))
            {
                return new BenchmarkResult
                {
                    Found = true,
                    ChaosEquivalent = currencyChaos,
                    DivineEquivalent = Math.Round(currencyChaos / _divinePriceInChaos, 1),
                    Confidence = "Very High",
                    Source = "poe.ninja (Currency)"
                };
            }

            // 2. Check Gem Cache
            if (item.Rarity == PoeRarity.Gem || _gemCache.ContainsKey(lookupName))
            {
                if (_gemCache.TryGetValue(lookupName, out double gemChaos))
                {
                    return new BenchmarkResult
                    {
                        Found = true,
                        ChaosEquivalent = gemChaos,
                        DivineEquivalent = Math.Round(gemChaos / _divinePriceInChaos, 1),
                        Confidence = "High",
                        Source = "poe.ninja (Skill Gem)"
                    };
                }
            }

            // 3. Check Unique & Item Cache
            if (_itemCache.TryGetValue(lookupName, out double itemChaos))
            {
                return new BenchmarkResult
                {
                    Found = true,
                    ChaosEquivalent = itemChaos,
                    DivineEquivalent = Math.Round(itemChaos / _divinePriceInChaos, 1),
                    Confidence = "High",
                    Source = "poe.ninja (Market Benchmark)"
                };
            }

            return new BenchmarkResult { Found = false };
        }
    }
}
