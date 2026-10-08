using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using System.Text.RegularExpressions;

namespace GameBarWidget.Services
{

    public sealed class FlyoutModifier
    {
        public ModifierType Type { get; set; } = ModifierType.Explicit;
        public string RawText { get; set; } = string.Empty;
        public string StatId { get; set; } = string.Empty;
        public string TierInfo { get; set; } = string.Empty;
        public string MagnitudesText { get; set; } = string.Empty;
        public bool IsUnscalable { get; set; } = false;
        public bool IsPrefix { get; set; } = false;
        public bool IsSuffix { get; set; } = false;
        public bool IsLocal { get; set; } = false;
    }

    public sealed class FlyoutItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string TypeLine { get; set; } = string.Empty;
        public string BaseType { get; set; } = string.Empty;
        public PoeRarity Rarity { get; set; } = PoeRarity.Normal;
        public int ItemLevel { get; set; }
        public bool IsIdentified { get; set; } = true;
        public bool IsCorrupted { get; set; } = false;
        public bool IsMirrored { get; set; } = false;
        public bool IsSynthesised { get; set; } = false;
        public bool IsFractured { get; set; } = false;
        public string SocketsSummary { get; set; } = string.Empty;
        public List<string> Requirements { get; set; } = new List<string>();
        public List<string> Properties { get; set; } = new List<string>();
        public double PhysicalDps { get; set; }
        public double ElementalDps { get; set; }
        public double TotalDps { get; set; }
        public List<FlyoutModifier> Modifiers { get; set; } = new List<FlyoutModifier>();
        public string FlavourText { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public static class PoeFlyoutParser
    {
        private static readonly Regex UnscalableSuffixRegex = new Regex(@"\s*[—–-]\s*Unscalable(?:\s+Value)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UnscalableParenthesesRegex = new Regex(@"\s*\((?:unscalable(?:\s+value)?)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static FlyoutItemModel ParseTradeItem(JsonObject itemOrListingObj)
        {
            var model = new FlyoutItemModel();
            if (itemOrListingObj == null) return model;

            JsonObject itemObj = itemOrListingObj;
            if (itemOrListingObj.ContainsKey("item") && itemOrListingObj.GetNamedValue("item").ValueType == JsonValueType.Object)
            {
                itemObj = itemOrListingObj.GetNamedObject("item");
            }

            if (itemObj.ContainsKey("name") && itemObj.GetNamedValue("name").ValueType == JsonValueType.String)
                model.Name = itemObj.GetNamedString("name");
            if (itemObj.ContainsKey("typeLine") && itemObj.GetNamedValue("typeLine").ValueType == JsonValueType.String)
                model.TypeLine = itemObj.GetNamedString("typeLine");
            if (itemObj.ContainsKey("baseType") && itemObj.GetNamedValue("baseType").ValueType == JsonValueType.String)
                model.BaseType = itemObj.GetNamedString("baseType");

            if (string.IsNullOrEmpty(model.Name)) model.Name = model.TypeLine;
            if (string.IsNullOrEmpty(model.BaseType)) model.BaseType = model.TypeLine;

            model.Rarity = ResolveRarity(itemObj);

            if (itemObj.ContainsKey("ilvl") && itemObj.GetNamedValue("ilvl").ValueType == JsonValueType.Number)
                model.ItemLevel = (int)itemObj.GetNamedNumber("ilvl");

            if (itemObj.ContainsKey("identified") && itemObj.GetNamedValue("identified").ValueType == JsonValueType.Boolean)
                model.IsIdentified = itemObj.GetNamedBoolean("identified");
            if (itemObj.ContainsKey("corrupted") && itemObj.GetNamedValue("corrupted").ValueType == JsonValueType.Boolean)
                model.IsCorrupted = itemObj.GetNamedBoolean("corrupted");
            if (itemObj.ContainsKey("duplicated") && itemObj.GetNamedValue("duplicated").ValueType == JsonValueType.Boolean)
                model.IsMirrored = itemObj.GetNamedBoolean("duplicated");
            if (itemObj.ContainsKey("synthesised") && itemObj.GetNamedValue("synthesised").ValueType == JsonValueType.Boolean)
                model.IsSynthesised = itemObj.GetNamedBoolean("synthesised");
            if (itemObj.ContainsKey("fractured") && itemObj.GetNamedValue("fractured").ValueType == JsonValueType.Boolean)
                model.IsFractured = itemObj.GetNamedBoolean("fractured");

            if (itemObj.ContainsKey("note") && itemObj.GetNamedValue("note").ValueType == JsonValueType.String)
                model.Note = itemObj.GetNamedString("note");

            if (itemObj.ContainsKey("sockets") && itemObj.GetNamedValue("sockets").ValueType == JsonValueType.Array)
            {
                model.SocketsSummary = ParseSocketsSummary(itemObj.GetNamedArray("sockets"));
            }

            if (itemObj.ContainsKey("requirements") && itemObj.GetNamedValue("requirements").ValueType == JsonValueType.Array)
            {
                model.Requirements = ParseRequirements(itemObj.GetNamedArray("requirements"));
            }

            if (itemObj.ContainsKey("properties") && itemObj.GetNamedValue("properties").ValueType == JsonValueType.Array)
            {
                model.Properties = ParseProperties(itemObj.GetNamedArray("properties"));
            }

            if (itemObj.ContainsKey("flavourText"))
            {
                var fvVal = itemObj.GetNamedValue("flavourText");
                if (fvVal.ValueType == JsonValueType.Array)
                {
                    var lines = new List<string>();
                    foreach (var elem in fvVal.GetArray())
                    {
                        if (elem.ValueType == JsonValueType.String) lines.Add(elem.GetString());
                    }
                    model.FlavourText = string.Join(" ", lines);
                }
                else if (fvVal.ValueType == JsonValueType.String)
                {
                    model.FlavourText = fvVal.GetString();
                }
            }

            JsonObject extendedObj = null;
            if (itemObj.ContainsKey("extended") && itemObj.GetNamedValue("extended").ValueType == JsonValueType.Object)
            {
                extendedObj = itemObj.GetNamedObject("extended");
                if (extendedObj.ContainsKey("pdps") && extendedObj.GetNamedValue("pdps").ValueType == JsonValueType.Number)
                    model.PhysicalDps = Math.Round(extendedObj.GetNamedNumber("pdps"), 1);
                if (extendedObj.ContainsKey("edps") && extendedObj.GetNamedValue("edps").ValueType == JsonValueType.Number)
                    model.ElementalDps = Math.Round(extendedObj.GetNamedNumber("edps"), 1);
                if (extendedObj.ContainsKey("dps") && extendedObj.GetNamedValue("dps").ValueType == JsonValueType.Number)
                    model.TotalDps = Math.Round(extendedObj.GetNamedNumber("dps"), 1);
            }

            ParseModArray(itemObj, "enchantMods", ModifierType.Enchant, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "implicitMods", ModifierType.Implicit, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "fracturedMods", ModifierType.Fractured, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "explicitMods", ModifierType.Explicit, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "craftedMods", ModifierType.Crafted, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "scourgeMods", ModifierType.Explicit, model.Modifiers, extendedObj);
            ParseModArray(itemObj, "crucibleMods", ModifierType.Explicit, model.Modifiers, extendedObj);

            return model;
        }

        private static PoeRarity ResolveRarity(JsonObject itemObj)
        {
            if (itemObj.ContainsKey("frameType") && itemObj.GetNamedValue("frameType").ValueType == JsonValueType.Number)
            {
                int frame = (int)itemObj.GetNamedNumber("frameType");
                switch (frame)
                {
                    case 0: return PoeRarity.Normal;
                    case 1: return PoeRarity.Magic;
                    case 2: return PoeRarity.Rare;
                    case 3: return PoeRarity.Unique;
                    case 4: return PoeRarity.Gem;
                    case 5: return PoeRarity.Currency;
                    case 6: return PoeRarity.DivinationCard;
                    case 9: return PoeRarity.Unique;
                    default: break;
                }
            }

            if (itemObj.ContainsKey("rarity") && itemObj.GetNamedValue("rarity").ValueType == JsonValueType.String)
            {
                string rStr = itemObj.GetNamedString("rarity").ToLowerInvariant();
                if (rStr.Contains("unique")) return PoeRarity.Unique;
                if (rStr.Contains("rare")) return PoeRarity.Rare;
                if (rStr.Contains("magic")) return PoeRarity.Magic;
                if (rStr.Contains("gem")) return PoeRarity.Gem;
                if (rStr.Contains("currency")) return PoeRarity.Currency;
                if (rStr.Contains("divination")) return PoeRarity.DivinationCard;
            }

            return PoeRarity.Normal;
        }

        private static void ParseModArray(JsonObject itemObj, string key, ModifierType type, List<FlyoutModifier> targetList, JsonObject extendedObj)
        {
            if (!itemObj.ContainsKey(key)) return;
            var val = itemObj.GetNamedValue(key);
            if (val.ValueType != JsonValueType.Array) return;

            var arr = val.GetArray();
            int index = 0;
            foreach (var itemElem in arr)
            {
                string rawText = string.Empty;
                string statId = string.Empty;
                string magnitudesText = string.Empty;
                bool isUnscalable = false;

                if (itemElem.ValueType == JsonValueType.String)
                {
                    rawText = itemElem.GetString();
                }
                else if (itemElem.ValueType == JsonValueType.Object)
                {
                    var modObj = itemElem.GetObject();
                    if (modObj.ContainsKey("description") && modObj.GetNamedValue("description").ValueType == JsonValueType.String)
                    {
                        rawText = modObj.GetNamedString("description");
                    }
                    if (modObj.ContainsKey("hash") && modObj.GetNamedValue("hash").ValueType == JsonValueType.String)
                    {
                        statId = modObj.GetNamedString("hash");
                    }

                    if (modObj.ContainsKey("mods") && modObj.GetNamedValue("mods").ValueType == JsonValueType.Array)
                    {
                        var magList = new List<string>();
                        foreach (var mVal in modObj.GetNamedArray("mods"))
                        {
                            if (mVal.ValueType == JsonValueType.Object)
                            {
                                var mObj = mVal.GetObject();
                                if (mObj.ContainsKey("magnitudes") && mObj.GetNamedValue("magnitudes").ValueType == JsonValueType.Array)
                                {
                                    foreach (var magElem in mObj.GetNamedArray("magnitudes"))
                                    {
                                        if (magElem.ValueType == JsonValueType.Object)
                                        {
                                            var magObj = magElem.GetObject();
                                            string minVal = magObj.ContainsKey("min") ? magObj.GetNamedString("min", "") : "";
                                            string maxVal = magObj.ContainsKey("max") ? magObj.GetNamedString("max", "") : "";
                                            if (!string.IsNullOrEmpty(minVal) && !string.IsNullOrEmpty(maxVal))
                                            {
                                                if (minVal == maxVal)
                                                {
                                                    if (minVal != "1") magList.Add($"({minVal})");
                                                }
                                                else
                                                {
                                                    magList.Add($"({minVal}–{maxVal})");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (magList.Count > 0) magnitudesText = string.Join(" ", magList);
                    }
                }

                if (string.IsNullOrWhiteSpace(rawText)) continue;

                if (rawText.IndexOf("unscalable", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isUnscalable = true;
                }

                string cleanText = UnscalableSuffixRegex.Replace(rawText, "").Trim();
                cleanText = UnscalableParenthesesRegex.Replace(cleanText, "").Trim();
                cleanText = UnscalableSuffixRegex.Replace(cleanText, "").Trim();

                string tierInfo = string.Empty;
                bool isPrefix = false;
                bool isSuffix = false;

                if (extendedObj != null && extendedObj.ContainsKey("mods") && extendedObj.GetNamedValue("mods").ValueType == JsonValueType.Object)
                {
                    var extMods = extendedObj.GetNamedObject("mods");
                    string domainKey = type == ModifierType.Implicit ? "implicit" : (type == ModifierType.Enchant ? "enchant" : "explicit");
                    if (extMods.ContainsKey(domainKey) && extMods.GetNamedValue(domainKey).ValueType == JsonValueType.Array)
                    {
                        var extArr = extMods.GetNamedArray(domainKey);
                        if (index < extArr.Count && extArr[index].ValueType == JsonValueType.Object)
                        {
                            var infoObj = extArr[index].GetObject();
                            if (infoObj.ContainsKey("tier") && infoObj.GetNamedValue("tier").ValueType == JsonValueType.String)
                            {
                                tierInfo = infoObj.GetNamedString("tier");
                            }
                            if (infoObj.ContainsKey("name") && infoObj.GetNamedValue("name").ValueType == JsonValueType.String)
                            {
                                string modName = infoObj.GetNamedString("name");
                                if (!string.IsNullOrEmpty(modName))
                                {
                                    tierInfo = !string.IsNullOrEmpty(tierInfo) ? $"{tierInfo} ({modName})" : modName;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(tierInfo))
                {
                    if (tierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase)) isPrefix = true;
                    else if (tierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase)) isSuffix = true;
                }

                targetList.Add(new FlyoutModifier
                {
                    Type = type,
                    RawText = cleanText,
                    StatId = statId,
                    TierInfo = tierInfo,
                    MagnitudesText = magnitudesText,
                    IsUnscalable = isUnscalable,
                    IsPrefix = isPrefix,
                    IsSuffix = isSuffix
                });

                index++;
            }
        }

        private static string ParseSocketsSummary(JsonArray socketsArray)
        {
            if (socketsArray == null || socketsArray.Count == 0) return string.Empty;
            int totalSockets = socketsArray.Count;
            var groups = new Dictionary<int, List<string>>();

            foreach (var sv in socketsArray)
            {
                if (sv.ValueType == JsonValueType.Object)
                {
                    var so = sv.GetObject();
                    int grp = so.ContainsKey("group") ? (int)so.GetNamedNumber("group") : 0;
                    string col = so.ContainsKey("sColour") ? so.GetNamedString("sColour") : "W";
                    if (!groups.ContainsKey(grp)) groups[grp] = new List<string>();
                    groups[grp].Add(col);
                }
            }

            int maxLink = 0;
            var groupStrs = new List<string>();
            foreach (var kvp in groups)
            {
                if (kvp.Value.Count > maxLink) maxLink = kvp.Value.Count;
                groupStrs.Add(string.Join("-", kvp.Value));
            }

            return $"{totalSockets}S {maxLink}L ({string.Join(" ", groupStrs)})";
        }

        private static List<string> ParseRequirements(JsonArray reqArray)
        {
            var reqs = new List<string>();
            if (reqArray == null) return reqs;

            foreach (var rVal in reqArray)
            {
                if (rVal.ValueType != JsonValueType.Object) continue;
                var rObj = rVal.GetObject();
                string rName = rObj.ContainsKey("name") ? rObj.GetNamedString("name") : string.Empty;
                string rValStr = string.Empty;
                if (rObj.ContainsKey("values") && rObj.GetNamedValue("values").ValueType == JsonValueType.Array)
                {
                    var valsArr = rObj.GetNamedArray("values");
                    if (valsArr.Count > 0 && valsArr[0].ValueType == JsonValueType.Array)
                    {
                        var subArr = valsArr[0].GetArray();
                        if (subArr.Count > 0 && subArr[0].ValueType == JsonValueType.String)
                        {
                            rValStr = subArr[0].GetString();
                        }
                    }
                }
                if (!string.IsNullOrEmpty(rName))
                {
                    reqs.Add(string.IsNullOrEmpty(rValStr) ? rName : $"{rName} {rValStr}");
                }
            }
            return reqs;
        }

        private static List<string> ParseProperties(JsonArray propArray)
        {
            var props = new List<string>();
            if (propArray == null) return props;

            foreach (var pVal in propArray)
            {
                if (pVal.ValueType != JsonValueType.Object) continue;
                var pObj = pVal.GetObject();
                string pName = pObj.ContainsKey("name") ? pObj.GetNamedString("name") : string.Empty;
                var valParts = new List<string>();

                if (pObj.ContainsKey("values") && pObj.GetNamedValue("values").ValueType == JsonValueType.Array)
                {
                    var valsArr = pObj.GetNamedArray("values");
                    foreach (var vItem in valsArr)
                    {
                        if (vItem.ValueType == JsonValueType.Array)
                        {
                            var subArr = vItem.GetArray();
                            if (subArr.Count > 0 && subArr[0].ValueType == JsonValueType.String)
                            {
                                valParts.Add(subArr[0].GetString());
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(pName)) continue;

                if (pName.Contains("{0}"))
                {
                    string formatted = pName;
                    for (int vi = 0; vi < valParts.Count; vi++)
                    {
                        formatted = formatted.Replace($"{{{vi}}}", valParts[vi]);
                    }
                    props.Add(formatted);
                }
                else if (valParts.Count > 0)
                {
                    props.Add($"{pName}: {string.Join(", ", valParts)}");
                }
                else
                {
                    props.Add(pName);
                }
            }
            return props;
        }
    }

    public sealed class TradeListing
    {
        public string Id { get; set; } = string.Empty;
        public double PriceAmount { get; set; }
        public string PriceCurrency { get; set; } = "chaos";
        public double PriceInChaos { get; set; }
        public double PriceInDivine { get; set; }
        public string AccountName { get; set; } = "Exile";
        public string CharacterIgn { get; set; } = string.Empty;
        public string OnlineStatus { get; set; } = "online";
        public string ListedAge { get; set; } = "Recent";
        public string AgeText { get => ListedAge; set => ListedAge = value; }
        public string WhisperString { get; set; } = string.Empty;
        public string WhisperToken { get; set; } = string.Empty;
        public string HideoutToken { get; set; } = string.Empty;
        public int GoldFee { get; set; }
        public bool IsFaustusInstantTrade { get; set; }

        // Item preview fields
        public FlyoutItemModel FlyoutItem { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemBaseType { get; set; } = string.Empty;
        public int ItemLevel { get; set; }
        public bool IsCorrupted { get; set; }
        public string SocketsSummary { get; set; } = string.Empty;
        public PoeItem ParsedItem { get; set; }
        public string RawItemText { get; set; } = string.Empty;
        public double PhysicalDps { get; set; }
        public double ElementalDps { get; set; }
        public double TotalDps { get; set; }
        public string StashTabName { get; set; } = string.Empty;
        public int StashX { get; set; }
        public int StashY { get; set; }
        public List<string> ImplicitMods { get; set; } = new List<string>();
        public List<string> ExplicitMods { get; set; } = new List<string>();
        public List<string> CraftedMods { get; set; } = new List<string>();
        public List<string> FracturedMods { get; set; } = new List<string>();
        public List<string> EnchantMods { get; set; } = new List<string>();
        public List<string> ScourgeMods { get; set; } = new List<string>();
        public List<string> CrucibleMods { get; set; } = new List<string>();
        public List<string> PseudoMods { get; set; } = new List<string>();
        public List<string> RequirementsSummary { get; set; } = new List<string>();
        public List<string> PropertiesSummary { get; set; } = new List<string>();
        public string FlavourText { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
    }

    public sealed class TradeSearchResult
    {
        public bool IsSuccess { get; set; } = true;
        public string ErrorReason { get; set; } = string.Empty;
        public string QueryId { get; set; } = string.Empty;
        public string SearchUrl { get; set; } = string.Empty;
        public int TotalListings { get; set; }
        public double MinPriceChaos { get; set; }
        public double MedianPriceChaos { get; set; }
        public double Q1PriceChaos { get; set; }
        public double Q3PriceChaos { get; set; }
        public double DivinePriceChaosRate { get; set; } = 150.0;
        public List<TradeListing> Listings { get; set; } = new List<TradeListing>();

        private string? _customSummaryText;
        public string SummaryText
        {
            get
            {
                if (!string.IsNullOrEmpty(_customSummaryText)) return _customSummaryText;
                if (!IsSuccess) return ErrorReason;
                if (TotalListings == 0) return "No active listings found.";

                if (MedianPriceChaos >= DivinePriceChaosRate && DivinePriceChaosRate > 0)
                {
                    double divMin = Math.Round(MinPriceChaos / DivinePriceChaosRate, 1);
                    double divMed = Math.Round(MedianPriceChaos / DivinePriceChaosRate, 1);
                    return $"{divMin} - {divMed} div ({TotalListings} offers)";
                }

                return $"{Math.Round(MinPriceChaos)} - {Math.Round(MedianPriceChaos)} chaos ({TotalListings} offers)";
            }
            set => _customSummaryText = value;
        }
    }

    /// <summary>
    /// Client for the official Path of Exile Trade Search and Fetch APIs.
    /// Implements precise query construction, stat filtering, and currency normalization matching Awakened PoE Trade.
    /// </summary>
    public sealed class PoeOfficialTradeClient
    {
        private static readonly Lazy<PoeOfficialTradeClient> _lazy = new Lazy<PoeOfficialTradeClient>(() => new PoeOfficialTradeClient());
        public static PoeOfficialTradeClient Instance => _lazy.Value;

        private readonly HttpClient _httpClient;
        private const string TradeBaseUrl = "https://www.pathofexile.com/api/trade";

        private PoeOfficialTradeClient()
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        }

        public async Task<List<string>> GetLeaguesAsync()
        {
            var defaultLeagues = new List<string> { "Standard", "Hardcore" };
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.pathofexile.com/leagues?type=main"))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (JsonArray.TryParse(json, out var array))
                        {
                            var leagues = new List<string>();
                            foreach (var item in array)
                            {
                                if (item.ValueType == JsonValueType.Object)
                                {
                                    var obj = item.GetObject();
                                    if (obj.ContainsKey("id"))
                                    {
                                        string id = obj.GetNamedString("id");
                                        bool isSsf = id.IndexOf("SSF", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Solo", StringComparison.OrdinalIgnoreCase) >= 0;
                                        bool isRuthless = id.IndexOf("Ruthless", StringComparison.OrdinalIgnoreCase) >= 0;
                                        if (!isSsf && !isRuthless && !string.IsNullOrWhiteSpace(id) && !leagues.Contains(id))
                                        {
                                            leagues.Add(id);
                                        }
                                    }
                                }
                            }
                            if (leagues.Count > 0)
                            {
                                return leagues;
                            }
                        }
                    }
                }
            }
            catch { }

            return defaultLeagues;
        }

        private static string MapCurrencyToTradeId(string currencyName)
        {
            if (string.IsNullOrWhiteSpace(currencyName)) return "divine";
            string name = currencyName.Trim().ToLowerInvariant();
            if (name.Contains("divine")) return "divine";
            if (name.Contains("chaos")) return "chaos";
            if (name.Contains("mirror")) return "mirror";
            if (name.Contains("exalt")) return "exalted";
            if (name.Contains("vaal")) return "vaal";
            if (name.Contains("annul")) return "annul";
            if (name.Contains("alch")) return "alch";
            if (name.Contains("fusing")) return "fusing";
            if (name.Contains("chrom")) return "chrom";
            if (name.Contains("gcp") || name.Contains("gemcutter")) return "gcp";
            if (name.Contains("bauble")) return "bauble";
            if (name.Contains("regret")) return "regret";
            if (name.Contains("scour")) return "scour";
            if (name.Contains("blessed")) return "blessed";
            if (name.Contains("regal")) return "regal";
            if (name.Contains("veiled")) return "veiled-orb";
            if (name.Contains("ancient")) return "ancient";
            if (name.Contains("sacred")) return "sacred";
            return name.Replace(" ", "-");
        }

        public async Task<TradeSearchResult> SearchExchangeAsync(PoeItem item, string league, string? poesessid, double divineRate)
        {
            var result = new TradeSearchResult();
            result.DivinePriceChaosRate = divineRate;

            string currencyId = MapCurrencyToTradeId(!string.IsNullOrEmpty(item.Name) ? item.Name : item.BaseType);
            string wantCurrency = currencyId;
            string haveCurrency = (currencyId == "chaos") ? "divine" : "chaos";

            string payload = $"{{\"query\":{{\"status\":{{\"option\":\"online\"}},\"have\":[\"{haveCurrency}\"],\"want\":[\"{wantCurrency}\"]}},\"engine\":\"new\"}}";
            string url = $"{TradeBaseUrl}/exchange/{Uri.EscapeDataString(league)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrWhiteSpace(poesessid))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={poesessid.Trim()}");
                    }
                    else if (!string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.OAuthAccessToken))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {PoeSettingsManager.Instance.OAuthAccessToken.Trim()}");
                    }

                    var response = await PoeTradeRateLimiter.Instance.SendThrottledAsync(_httpClient, request);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();
                        if (JsonObject.TryParse(responseJson, out var rootObj))
                        {
                            string queryId = rootObj.ContainsKey("id") ? rootObj.GetNamedString("id") : string.Empty;
                            int total = rootObj.ContainsKey("total") ? (int)rootObj.GetNamedNumber("total") : 0;
                            result.QueryId = queryId;
                            result.SearchUrl = $"https://www.pathofexile.com/trade/exchange/{Uri.EscapeDataString(league)}/{queryId}";
                            result.TotalListings = total;

                            if (total > 0 && rootObj.ContainsKey("result") && rootObj.GetNamedValue("result").ValueType == JsonValueType.Object)
                            {
                                var resultMap = rootObj.GetNamedObject("result");
                                var keys = new List<string>();
                                foreach (var k in resultMap.Keys)
                                {
                                    keys.Add(k);
                                    if (keys.Count >= 10) break;
                                }

                                if (keys.Count > 0)
                                {
                                    string fetchUrl = $"{TradeBaseUrl}/fetch/{string.Join(",", keys)}?query={queryId}&exchange=true";
                                    using (var fetchReq = new HttpRequestMessage(HttpMethod.Get, fetchUrl))
                                    {
                                        if (!string.IsNullOrWhiteSpace(poesessid))
                                        {
                                            fetchReq.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={poesessid.Trim()}");
                                        }
                                        else if (!string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.OAuthAccessToken))
                                        {
                                            fetchReq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {PoeSettingsManager.Instance.OAuthAccessToken.Trim()}");
                                        }

                                        var fetchRes = await PoeTradeRateLimiter.Instance.SendThrottledAsync(_httpClient, fetchReq);
                                        if (fetchRes.IsSuccessStatusCode)
                                        {
                                            string fetchJson = await fetchRes.Content.ReadAsStringAsync();
                                            var listings = ParseFetchListings(fetchJson, league, divineRate);
                                            if (listings.Count > 0)
                                            {
                                                listings.Sort((a, b) => a.PriceInChaos.CompareTo(b.PriceInChaos));
                                                result.IsSuccess = true;
                                                result.Listings = listings;
                                                CalculateStatistics(result, listings);
                                                return result;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback to poe.ninja benchmark rate if exchange query returned no active offers or offline
            var benchmark = PoeNinjaClient.Instance.GetBenchmark(item);
            if (benchmark.Found)
            {
                result.IsSuccess = true;
                result.MinPriceChaos = benchmark.ChaosEquivalent;
                result.MedianPriceChaos = benchmark.ChaosEquivalent;
                result.SummaryText = $"{benchmark.ChaosEquivalent:0.##} Chaos (≈{benchmark.DivineEquivalent:0.##} Div)";
                result.TotalListings = 1;
                result.SearchUrl = $"https://www.pathofexile.com/trade/exchange/{Uri.EscapeDataString(league)}";
                result.Listings = new List<TradeListing>
                {
                    new TradeListing
                    {
                        AccountName = "poe.ninja rate",
                        ItemName = !string.IsNullOrEmpty(item.Name) ? item.Name : item.BaseType,
                        PriceAmount = benchmark.ChaosEquivalent,
                        PriceCurrency = "chaos",
                        PriceInChaos = benchmark.ChaosEquivalent,
                        PriceInDivine = benchmark.DivineEquivalent,
                        WhisperString = $"Exchange Rate: 1 {(!string.IsNullOrEmpty(item.Name) ? item.Name : item.BaseType)} = {benchmark.ChaosEquivalent:0.##} Chaos",
                        AgeText = "Live rate"
                    }
                };
                return result;
            }

            return result;
        }

        public async Task<TradeSearchResult> SearchItemAsync(PoeItem item, string? league = null, string? poesessid = null)
        {
            var result = new TradeSearchResult();
            if (item == null)
            {
                result.IsSuccess = false;
                result.ErrorReason = "No item provided.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(league))
            {
                league = PoeSettingsManager.Instance.SelectedLeague;
                if (string.IsNullOrWhiteSpace(league))
                {
                    league = "Standard";
                }
            }

            try
            {
                // Fetch live Divine rate for currency normalization
                double divineRate = 150.0;
                try
                {
                    var ninjaRates = await PoeNinjaClient.Instance.GetCurrencyRatesAsync(league);
                    if (ninjaRates != null && ninjaRates.ContainsKey("Divine Orb") && ninjaRates["Divine Orb"] > 0)
                    {
                        divineRate = ninjaRates["Divine Orb"];
                    }
                }
                catch { }
                result.DivinePriceChaosRate = divineRate;

                bool isCurrencyItem = item.Namespace == ItemNamespace.Currency || 
                                       item.Rarity == PoeRarity.Currency || 
                                       (!string.IsNullOrEmpty(item.Name) && (item.Name.EndsWith("Orb", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Mirror of Kalandra") || item.Name.Contains("Divine") || item.Name.Contains("Chaos")));

                if (isCurrencyItem)
                {
                    var exchangeRes = await SearchExchangeAsync(item, league, poesessid, divineRate);
                    if (exchangeRes != null && exchangeRes.IsSuccess && exchangeRes.Listings != null && exchangeRes.Listings.Count > 0)
                    {
                        return exchangeRes;
                    }
                }

                string queryJson = BuildSearchPayload(item);
                string searchUrl = $"{TradeBaseUrl}/search/{Uri.EscapeDataString(league)}";

                using (var request = new HttpRequestMessage(HttpMethod.Post, searchUrl))
                {
                    request.Content = new StringContent(queryJson, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrWhiteSpace(poesessid))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={poesessid.Trim()}");
                    }
                    else if (!string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.OAuthAccessToken))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {PoeSettingsManager.Instance.OAuthAccessToken.Trim()}");
                    }

                    var response = await PoeTradeRateLimiter.Instance.SendThrottledAsync(_httpClient, request);

                    if ((int)response.StatusCode == 429)
                    {
                        result.IsSuccess = false;
                        result.ErrorReason = "Rate limited by PathOfExile.com (429). Please wait a few seconds.";
                        return result;
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        result.IsSuccess = false;
                        result.ErrorReason = "Authorization required (401/403). Configure POESESSID in settings (Gear icon).";
                        return result;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorDetail = string.Empty;
                        try
                        {
                            string rawError = await response.Content.ReadAsStringAsync();
                            if (JsonObject.TryParse(rawError, out var errObj) && errObj.ContainsKey("error"))
                            {
                                var subErr = errObj.GetNamedObject("error");
                                if (subErr.ContainsKey("message"))
                                {
                                    errorDetail = subErr.GetNamedString("message");
                                }
                            }
                        }
                        catch { }

                        result.IsSuccess = false;
                        result.ErrorReason = !string.IsNullOrEmpty(errorDetail)
                            ? $"Trade API Error ({(int)response.StatusCode}): {errorDetail}"
                            : $"HTTP Error {(int)response.StatusCode} ({response.ReasonPhrase}).";
                        return result;
                    }

                    string searchResponseJson = await response.Content.ReadAsStringAsync();

                    if (!JsonObject.TryParse(searchResponseJson, out var searchObj))
                    {
                        result.IsSuccess = false;
                        result.ErrorReason = "Failed to parse trade search JSON response.";
                        return result;
                    }

                    // Parse query ID & total results
                    string queryId = searchObj.ContainsKey("id") ? searchObj.GetNamedString("id") : string.Empty;
                    int total = searchObj.ContainsKey("total") ? (int)searchObj.GetNamedNumber("total") : 0;
                    result.QueryId = queryId;
                    result.SearchUrl = $"https://www.pathofexile.com/trade/search/{Uri.EscapeDataString(league)}/{queryId}";
                    result.TotalListings = total;

                    if (total == 0 || string.IsNullOrEmpty(queryId))
                    {
                        var benchmark = PoeNinjaClient.Instance.GetBenchmark(item);
                        if (benchmark.Found)
                        {
                            result.IsSuccess = true;
                            result.MinPriceChaos = benchmark.ChaosEquivalent;
                            result.MedianPriceChaos = benchmark.ChaosEquivalent;
                            result.SummaryText = $"{benchmark.ChaosEquivalent:0.##} Chaos (≈{benchmark.DivineEquivalent:0.##} Div)";
                            result.TotalListings = 1;
                            result.SearchUrl = $"https://www.pathofexile.com/trade/exchange/{Uri.EscapeDataString(league)}";
                            result.Listings = new List<TradeListing>
                            {
                                new TradeListing
                                {
                                    AccountName = "poe.ninja rate",
                                    ItemName = !string.IsNullOrEmpty(item.Name) ? item.Name : item.BaseType,
                                    PriceAmount = benchmark.ChaosEquivalent,
                                    PriceCurrency = "chaos",
                                    PriceInChaos = benchmark.ChaosEquivalent,
                                    PriceInDivine = benchmark.DivineEquivalent,
                                    WhisperString = $"Exchange Rate: 1 {(!string.IsNullOrEmpty(item.Name) ? item.Name : item.BaseType)} = {benchmark.ChaosEquivalent:0.##} Chaos",
                                    AgeText = "Live rate"
                                }
                            };
                            return result;
                        }

                        result.IsSuccess = true;
                        result.Listings = new List<TradeListing>();
                        result.ErrorReason = $"No active listings found in '{league}'.";
                        return result;
                    }

                    var hashes = new List<string>();
                    if (searchObj.ContainsKey("result") && searchObj.GetNamedValue("result").ValueType == JsonValueType.Array)
                    {
                        var arr = searchObj.GetNamedArray("result");
                        foreach (var elem in arr)
                        {
                            if (elem.ValueType == JsonValueType.String)
                            {
                                hashes.Add(elem.GetString());
                            }
                        }
                    }

                    if (hashes.Count == 0)
                    {
                        result.IsSuccess = true;
                        result.Listings = new List<TradeListing>();
                        return result;
                    }

                    // Fetch top listings (up to 10)
                    int takeCount = Math.Min(10, hashes.Count);
                    var topHashes = hashes.GetRange(0, takeCount);

                    string fetchUrl = $"{TradeBaseUrl}/fetch/{string.Join(",", topHashes)}?query={queryId}";

                    using (var fetchRequest = new HttpRequestMessage(HttpMethod.Get, fetchUrl))
                    {
                        if (!string.IsNullOrWhiteSpace(poesessid))
                        {
                            fetchRequest.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={poesessid.Trim()}");
                        }
                        else if (!string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.OAuthAccessToken))
                        {
                            fetchRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {PoeSettingsManager.Instance.OAuthAccessToken.Trim()}");
                        }

                        var fetchResponse = await PoeTradeRateLimiter.Instance.SendThrottledAsync(_httpClient, fetchRequest);
                        if (fetchResponse.IsSuccessStatusCode)
                        {
                            string fetchJson = await fetchResponse.Content.ReadAsStringAsync();
                            var listings = ParseFetchListings(fetchJson, league, divineRate);

                            // Sort listings by normalized chaos price ascending
                            listings.Sort((a, b) => a.PriceInChaos.CompareTo(b.PriceInChaos));
                            result.Listings = listings;

                            // Calculate statistical percentiles
                            CalculateStatistics(result, listings);
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorReason = $"Network or trade error: {ex.Message}";
                return result;
            }
        }

        private static void CalculateStatistics(TradeSearchResult result, List<TradeListing> listings)
        {
            if (listings == null || listings.Count == 0) return;

            var prices = new List<double>();
            foreach (var l in listings)
            {
                if (l.PriceInChaos > 0)
                {
                    prices.Add(l.PriceInChaos);
                }
            }

            if (prices.Count == 0) return;

            prices.Sort();
            result.MinPriceChaos = prices[0];

            int count = prices.Count;
            int medIdx = count / 2;
            result.MedianPriceChaos = count % 2 == 0 ? (prices[medIdx - 1] + prices[medIdx]) / 2.0 : prices[medIdx];

            int q1Idx = count / 4;
            int q3Idx = (count * 3) / 4;
            result.Q1PriceChaos = prices[q1Idx];
            result.Q3PriceChaos = prices[Math.Min(q3Idx, count - 1)];
        }

        public static string BuildSearchPayload(PoeItem item)
        {
            string onlineStatus = PoeSettingsManager.Instance.OnlineStatusFilter;
            if (string.IsNullOrWhiteSpace(onlineStatus))
            {
                onlineStatus = "securable";
            }

            var sb = new StringBuilder();
            sb.Append("{\"query\":{");
            if (onlineStatus.Equals("async", StringComparison.OrdinalIgnoreCase) || onlineStatus.Equals("securable", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\"status\":{\"option\":\"securable\"}");
            }
            else if (onlineStatus.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\"status\":{\"option\":\"any\"}");
            }
            else if (onlineStatus.Equals("online", StringComparison.OrdinalIgnoreCase) || onlineStatus.Equals("sync", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\"status\":{\"option\":\"online\"}");
            }
            else
            {
                sb.Append($"\"status\":{{\"option\":\"{onlineStatus}\"}}");
            }

            // 1. Identity rules by ItemNamespace matching Awakened:
            switch (item.Namespace)
            {
                case ItemNamespace.Unique:
                    if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        sb.Append($",\"name\":\"{EscapeJson(item.Name)}\"");
                    }

                    // Awakened rule: only include type for variant-base uniques or if name equals base
                    if (!string.IsNullOrWhiteSpace(item.BaseType) &&
                        (IsVariantBaseUnique(item.Name) || string.IsNullOrWhiteSpace(item.Name)))
                    {
                        sb.Append($",\"type\":\"{EscapeJson(item.BaseType)}\"");
                    }
                    break;

                case ItemNamespace.Gem:
                    string gemName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
                    sb.Append($",\"type\":\"{EscapeJson(gemName)}\"");
                    break;

                case ItemNamespace.DivinationCard:
                case ItemNamespace.Currency:
                case ItemNamespace.CapturedBeast:
                    string directName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
                    sb.Append($",\"type\":\"{EscapeJson(directName)}\"");
                    break;

                case ItemNamespace.Map:
                case ItemNamespace.Item:
                default:
                    if (!string.IsNullOrWhiteSpace(item.BaseType))
                    {
                        sb.Append($",\"type\":\"{EscapeJson(item.BaseType)}\"");
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        sb.Append($",\"type\":\"{EscapeJson(item.Name)}\"");
                    }
                    break;
            }

            // 2. Stat filters: Include ONLY active modifiers (Awakened PoE Trade standard)
            // Omitting inactive modifiers prevents GGG from throwing 400 Unknown stat errors on obsolete IDs
            var statFilters = new List<string>();

            if (item.Namespace == ItemNamespace.Item || item.Namespace == ItemNamespace.Unique || item.Namespace == ItemNamespace.Map)
            {
                // Pseudo stats
                foreach (var pseudo in item.PseudoModifiers)
                {
                    if (pseudo.IsActive && !string.IsNullOrEmpty(pseudo.StatId) && !pseudo.StatId.Equals("pseudo.pseudo_total_dps", StringComparison.OrdinalIgnoreCase))
                    {
                        var filter = new StringBuilder();
                        filter.Append($"{{\"id\":\"{pseudo.StatId}\"");

                        var valueParts = new List<string>();
                        if (pseudo.MinRoll.HasValue && pseudo.MaxRoll.HasValue)
                        {
                            double p1 = Math.Abs(pseudo.MinRoll.Value);
                            double p2 = Math.Abs(pseudo.MaxRoll.Value);
                            valueParts.Add($"\"min\":{Math.Min(p1, p2).ToString(CultureInfo.InvariantCulture)}");
                            valueParts.Add($"\"max\":{Math.Max(p1, p2).ToString(CultureInfo.InvariantCulture)}");
                        }
                        else if (pseudo.MinRoll.HasValue)
                        {
                            valueParts.Add($"\"min\":{Math.Abs(pseudo.MinRoll.Value).ToString(CultureInfo.InvariantCulture)}");
                        }
                        else if (pseudo.MaxRoll.HasValue)
                        {
                            valueParts.Add($"\"max\":{Math.Abs(pseudo.MaxRoll.Value).ToString(CultureInfo.InvariantCulture)}");
                        }

                        if (valueParts.Count > 0)
                        {
                            filter.Append($",\"value\":{{{string.Join(",", valueParts)}}}");
                        }
                        filter.Append($",\"disabled\":false}}");
                        statFilters.Add(filter.ToString());
                    }
                }

                // Explicit / Implicit / Fractured / Crafted / Enchant mods - ONLY if user selected / active
                foreach (var mod in item.Modifiers)
                {
                    if (mod.IsActive && !string.IsNullOrEmpty(mod.StatId))
                    {
                        var filter = new StringBuilder();
                        filter.Append($"{{\"id\":\"{mod.StatId}\"");

                        var valueParts = new List<string>();
                        if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue)
                        {
                            double m1 = Math.Abs(mod.MinRoll.Value);
                            double m2 = Math.Abs(mod.MaxRoll.Value);
                            valueParts.Add($"\"min\":{Math.Min(m1, m2).ToString(CultureInfo.InvariantCulture)}");
                            valueParts.Add($"\"max\":{Math.Max(m1, m2).ToString(CultureInfo.InvariantCulture)}");
                        }
                        else if (mod.MinRoll.HasValue)
                        {
                            valueParts.Add($"\"min\":{Math.Abs(mod.MinRoll.Value).ToString(CultureInfo.InvariantCulture)}");
                        }
                        else if (mod.MaxRoll.HasValue)
                        {
                            valueParts.Add($"\"max\":{Math.Abs(mod.MaxRoll.Value).ToString(CultureInfo.InvariantCulture)}");
                        }

                        if (valueParts.Count > 0)
                        {
                            filter.Append($",\"value\":{{{string.Join(",", valueParts)}}}");
                        }
                        filter.Append($",\"disabled\":false}}");
                        statFilters.Add(filter.ToString());
                    }
                }
            }

            // Add stats array: match Awakened/official format
            if (statFilters.Count > 0)
            {
                sb.Append(",\"stats\":[{\"type\":\"and\",\"filters\":[");
                sb.Append(string.Join(",", statFilters));
                sb.Append("]}]");
            }

            // 3. Category & Type Filters
            var filterGroups = new List<string>();

            var typeFilters = new List<string>();
            if (!string.IsNullOrEmpty(item.Category) && item.Namespace != ItemNamespace.Unique && string.IsNullOrWhiteSpace(item.BaseType))
            {
                typeFilters.Add($"\"category\":{{\"option\":\"{item.Category}\"}}");
            }
            if (typeFilters.Count > 0)
            {
                filterGroups.Add($"\"type_filters\":{{\"filters\":{{{string.Join(",", typeFilters)}}}}}");
            }

            // 4. Misc & Map & Socket Filters
            var miscFilters = new List<string>();
            if (item.Rarity == PoeRarity.Rare && item.Namespace == ItemNamespace.Item)
            {
                miscFilters.Add("\"rarity\":{\"option\":\"rare\"}");
            }
            else if (item.Rarity == PoeRarity.Unique)
            {
                miscFilters.Add("\"rarity\":{\"option\":\"unique\"}");
            }

            if (!string.IsNullOrEmpty(item.CorruptedFilterOption))
            {
                if (item.CorruptedFilterOption.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    miscFilters.Add("\"corrupted\":{\"option\":\"true\"}");
                }
                else if (item.CorruptedFilterOption.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    miscFilters.Add("\"corrupted\":{\"option\":\"false\"}");
                }
            }
            else if (item.IsCorrupted)
            {
                miscFilters.Add("\"corrupted\":{\"option\":\"true\",\"disabled\":true}");
            }
            if (item.FilterQualityActive && (item.FilterQualityMin.HasValue || item.FilterQualityMax.HasValue))
            {
                var qParts = new List<string>();
                if (item.FilterQualityMin.HasValue) qParts.Add($"\"min\":{item.FilterQualityMin.Value}");
                if (item.FilterQualityMax.HasValue) qParts.Add($"\"max\":{item.FilterQualityMax.Value}");
                miscFilters.Add($"\"quality\":{{{string.Join(",", qParts)}}}");
            }
            if (item.ItemLevel > 0 && item.Namespace != ItemNamespace.Unique)
            {
                miscFilters.Add($"\"ilvl\":{{\"min\":{item.ItemLevel},\"disabled\":true}}");
            }

            if (item.Namespace == ItemNamespace.Gem)
            {
                if (item.GemLevel > 0)
                {
                    miscFilters.Add($"\"gem_level\":{{\"min\":{item.GemLevel}}}");
                }
            }
            if (miscFilters.Count > 0)
            {
                filterGroups.Add($"\"misc_filters\":{{\"filters\":{{{string.Join(",", miscFilters)}}}}}");
            }

            var socketFilters = new List<string>();
            if (item.FilterSocketsActive && (item.FilterSocketsMin.HasValue || item.FilterSocketsMax.HasValue))
            {
                var sParts = new List<string>();
                if (item.FilterSocketsMin.HasValue) sParts.Add($"\"min\":{item.FilterSocketsMin.Value}");
                if (item.FilterSocketsMax.HasValue) sParts.Add($"\"max\":{item.FilterSocketsMax.Value}");
                socketFilters.Add($"\"sockets\":{{{string.Join(",", sParts)}}}");
            }
            if (item.FilterLinksActive && (item.FilterLinksMin.HasValue || item.FilterLinksMax.HasValue))
            {
                var lParts = new List<string>();
                if (item.FilterLinksMin.HasValue) lParts.Add($"\"min\":{item.FilterLinksMin.Value}");
                if (item.FilterLinksMax.HasValue) lParts.Add($"\"max\":{item.FilterLinksMax.Value}");
                socketFilters.Add($"\"links\":{{{string.Join(",", lParts)}}}");
            }
            if (socketFilters.Count > 0)
            {
                filterGroups.Add($"\"socket_filters\":{{\"filters\":{{{string.Join(",", socketFilters)}}}}}");
            }

            var mapFilters = new List<string>();
            if (item.MapTier > 0)
            {
                mapFilters.Add($"\"map_tier\":{{\"min\":{item.MapTier},\"max\":{item.MapTier}}}");
            }
            if (mapFilters.Count > 0)
            {
                filterGroups.Add($"\"map_filters\":{{\"filters\":{{{string.Join(",", mapFilters)}}}}}");
            }

            if (filterGroups.Count > 0)
            {
                sb.Append($",\"filters\":{{{string.Join(",", filterGroups)}}}");
            }

            sb.Append("},\"sort\":{\"price\":\"asc\"}}");
            return sb.ToString();
        }

        private static bool IsVariantBaseUnique(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.ToLowerInvariant();
            return n.Contains("watcher's eye") ||
                   n.Contains("megalomaniac") ||
                   n.Contains("combat focus") ||
                   n.Contains("forbidden flame") ||
                   n.Contains("forbidden flesh") ||
                   n.Contains("thread of hope") ||
                   n.Contains("impossible escape") ||
                   n.Contains("sublime vision") ||
                   n.Contains("voices") ||
                   n.Contains("grand spectrum") ||
                   n.Contains("vessel of vinktar") ||
                   n.Contains("agnerod") ||
                   n.Contains("doryani's delusion") ||
                   n.Contains("lethal pride") ||
                   n.Contains("brutal restraint") ||
                   n.Contains("militant faith") ||
                   n.Contains("glorious vanity") ||
                   n.Contains("elegant hubris");
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static List<TradeListing> ParseFetchListings(string json, string league, double divineRate)
        {
            var listings = new List<TradeListing>();
            if (string.IsNullOrWhiteSpace(json)) return listings;

            try
            {
                if (!JsonObject.TryParse(json, out var root) || !root.ContainsKey("result"))
                {
                    return listings;
                }

                var resultArray = root.GetNamedArray("result");
                foreach (var itemVal in resultArray)
                {
                    if (itemVal.ValueType != JsonValueType.Object) continue;
                    var itemObj = itemVal.GetObject();

                    string listingId = itemObj.ContainsKey("id") ? itemObj.GetNamedString("id") : string.Empty;
                    if (!itemObj.ContainsKey("listing")) continue;

                    var listingObj = itemObj.GetNamedObject("listing");

                    string accountName = "Exile";
                    string charIgn = string.Empty;
                    if (listingObj.ContainsKey("account"))
                    {
                        var accObj = listingObj.GetNamedObject("account");
                        if (accObj.ContainsKey("name")) accountName = accObj.GetNamedString("name");
                        if (accObj.ContainsKey("lastCharacterName")) charIgn = accObj.GetNamedString("lastCharacterName");
                    }

                    double amount = 1.0;
                    string priceCurrency = "chaos";
                    if (listingObj.ContainsKey("price"))
                    {
                        var priceObj = listingObj.GetNamedObject("price");
                        if (priceObj.ContainsKey("amount")) amount = priceObj.GetNamedNumber("amount");
                        if (priceObj.ContainsKey("currency")) priceCurrency = priceObj.GetNamedString("currency");
                    }

                    double chaosVal = amount;
                    double divVal = amount / (divineRate > 0 ? divineRate : 150.0);

                    if (priceCurrency.Equals("divine", StringComparison.OrdinalIgnoreCase))
                    {
                        chaosVal = amount * divineRate;
                        divVal = amount;
                    }
                    else if (priceCurrency.Equals("mirror", StringComparison.OrdinalIgnoreCase))
                    {
                        chaosVal = amount * (divineRate * 600.0);
                        divVal = amount * 600.0;
                    }

                    string whisper = listingObj.ContainsKey("whisper") ? listingObj.GetNamedString("whisper") : string.Empty;
                    if (string.IsNullOrEmpty(whisper))
                    {
                        whisper = $"@{accountName} Hi, I'd like to buy your item for {amount} {priceCurrency} in {league}.";
                    }

                    string status = "online";
                    if (listingObj.ContainsKey("online"))
                    {
                        var onlObj = listingObj.GetNamedObject("online");
                        if (onlObj.ContainsKey("status")) status = onlObj.GetNamedString("status");
                    }

                    string whisperToken = listingObj.ContainsKey("whisper_token") ? listingObj.GetNamedString("whisper_token") : string.Empty;
                    if (string.IsNullOrEmpty(whisperToken) && listingObj.ContainsKey("token"))
                    {
                        whisperToken = listingObj.GetNamedString("token");
                    }
                    if (string.IsNullOrEmpty(whisperToken) && itemObj.ContainsKey("token"))
                    {
                        whisperToken = itemObj.GetNamedString("token");
                    }

                    string hideoutToken = listingObj.ContainsKey("hideout_token") ? listingObj.GetNamedString("hideout_token") : string.Empty;
                    if (string.IsNullOrEmpty(hideoutToken) && listingObj.ContainsKey("token"))
                    {
                        hideoutToken = listingObj.GetNamedString("token");
                    }
                    if (string.IsNullOrEmpty(hideoutToken) && itemObj.ContainsKey("token"))
                    {
                        hideoutToken = itemObj.GetNamedString("token");
                    }

                    int goldFee = 0;
                    if (listingObj.ContainsKey("fee"))
                    {
                        goldFee = (int)listingObj.GetNamedNumber("fee");
                    }

                    string stashTabName = string.Empty;
                    int stashX = 0;
                    int stashY = 0;
                    if (listingObj.ContainsKey("stash") && listingObj.GetNamedValue("stash").ValueType == JsonValueType.Object)
                    {
                        var sObj = listingObj.GetNamedObject("stash");
                        if (sObj.ContainsKey("name") && sObj.GetNamedValue("name").ValueType == JsonValueType.String)
                            stashTabName = sObj.GetNamedString("name");
                        if (sObj.ContainsKey("x")) stashX = (int)sObj.GetNamedNumber("x");
                        if (sObj.ContainsKey("y")) stashY = (int)sObj.GetNamedNumber("y");
                    }

                    bool isFaustus = (listingObj.ContainsKey("securable") && listingObj.GetNamedBoolean("securable")) ||
                                     goldFee > 0 ||
                                     !string.IsNullOrEmpty(hideoutToken) ||
                                     (listingObj.ContainsKey("method") && listingObj.GetNamedString("method").Equals("stash", StringComparison.OrdinalIgnoreCase));

                    // Use dedicated Flyout Parser for Trade API item JSON
                    var flyoutItem = PoeFlyoutParser.ParseTradeItem(itemObj);

                    listings.Add(new TradeListing
                    {
                        Id = listingId,
                        PriceAmount = amount,
                        PriceCurrency = priceCurrency,
                        PriceInChaos = Math.Round(chaosVal, 1),
                        PriceInDivine = Math.Round(divVal, 2),
                        AccountName = accountName,
                        CharacterIgn = charIgn,
                        OnlineStatus = status,
                        ListedAge = "Recent",
                        WhisperString = whisper,
                        WhisperToken = whisperToken,
                        HideoutToken = hideoutToken,
                        GoldFee = goldFee,
                        IsFaustusInstantTrade = isFaustus,
                        StashTabName = stashTabName,
                        StashX = stashX,
                        StashY = stashY,
                        FlyoutItem = flyoutItem,
                        ItemName = flyoutItem.Name,
                        ItemBaseType = !string.IsNullOrEmpty(flyoutItem.TypeLine) ? flyoutItem.TypeLine : flyoutItem.BaseType,
                        ItemLevel = flyoutItem.ItemLevel,
                        IsCorrupted = flyoutItem.IsCorrupted,
                        SocketsSummary = flyoutItem.SocketsSummary,
                        ParsedItem = null,
                        RawItemText = string.Empty,
                        PhysicalDps = flyoutItem.PhysicalDps,
                        ElementalDps = flyoutItem.ElementalDps,
                        TotalDps = flyoutItem.TotalDps,
                        RequirementsSummary = flyoutItem.Requirements,
                        PropertiesSummary = flyoutItem.Properties,
                        FlavourText = flyoutItem.FlavourText,
                        RawJson = itemVal.Stringify()
                    });
                }
            }
            catch { }

            return listings;
        }

        public async Task<(bool success, string message)> SendDirectWhisperTokenAsync(string token, string? poesessid)
        {
            return await SendTokenToEndpointAsync("whisper", token, poesessid);
        }

        public async Task<(bool success, string message)> SendDirectHideoutTokenAsync(string token, string? poesessid)
        {
            return await SendTokenToEndpointAsync("hideout", token, poesessid);
        }

        private async Task<(bool success, string message)> SendTokenToEndpointAsync(string endpoint, string token, string? poesessid)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "No token available.");
            }

            try
            {
                string league = PoeSettingsManager.Instance.SelectedLeague;
                string url = $"{TradeBaseUrl}/{endpoint}";
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Content = new StringContent($"{{\"token\":\"{token}\"}}", Encoding.UTF8, "application/json");
                    request.Headers.TryAddWithoutValidation("Accept", "*/*");
                    request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                    request.Headers.TryAddWithoutValidation("Origin", "https://www.pathofexile.com");
                    request.Headers.Referrer = new Uri($"https://www.pathofexile.com/trade/search/{Uri.EscapeDataString(league)}");

                    if (!string.IsNullOrWhiteSpace(poesessid))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={poesessid.Trim()}");
                    }
                    else if (!string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.OAuthAccessToken))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {PoeSettingsManager.Instance.OAuthAccessToken.Trim()}");
                    }

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        return (true, $"{endpoint} token sent successfully.");
                    }
                    else
                    {
                        return (false, $"Request failed (HTTP {(int)response.StatusCode}).");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Trade API error: {ex.Message}");
            }
        }

        public async Task<List<TradeListing>> FetchListingsAsync(List<string> itemHashes, string league, string queryId)
        {
            var listings = new List<TradeListing>();
            if (itemHashes == null || itemHashes.Count == 0) return listings;

            try
            {
                double divineRate = 150.0;
                string fetchUrl = $"{TradeBaseUrl}/fetch/{string.Join(",", itemHashes)}?query={Uri.EscapeDataString(queryId ?? "")}";
                using (var fetchRequest = new HttpRequestMessage(HttpMethod.Get, fetchUrl))
                {
                    fetchRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    fetchRequest.Headers.TryAddWithoutValidation("Origin", "https://www.pathofexile.com");
                    fetchRequest.Headers.Referrer = new Uri($"https://www.pathofexile.com/trade/search/{Uri.EscapeDataString(league)}");

                    string sessionId = PoeSettingsManager.Instance.PoeSessionId;
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        fetchRequest.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={sessionId.Trim()}");
                    }

                    var response = await PoeTradeRateLimiter.Instance.SendThrottledAsync(_httpClient, fetchRequest);
                    if (response.IsSuccessStatusCode)
                    {
                        string fetchJson = await response.Content.ReadAsStringAsync();
                        listings = ParseFetchListings(fetchJson, league, divineRate);
                    }
                }
            }
            catch { }

            return listings;
        }

        public async Task<string> ResolveSearchIdAsync(string league, string rawSearchId)
        {
            if (string.IsNullOrWhiteSpace(rawSearchId)) return rawSearchId;

            if (rawSearchId.Length <= 20 && !rawSearchId.StartsWith("H4sI", StringComparison.OrdinalIgnoreCase))
            {
                return rawSearchId;
            }

            try
            {
                string url = $"{TradeBaseUrl}/search/{Uri.EscapeDataString(league)}/{rawSearchId}";
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    request.Headers.TryAddWithoutValidation("Accept", "application/json");
                    request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                    request.Headers.TryAddWithoutValidation("Origin", "https://www.pathofexile.com");
                    request.Headers.Referrer = new Uri($"https://www.pathofexile.com/trade/search/{Uri.EscapeDataString(league)}");

                    string sessionId = PoeSettingsManager.Instance.PoeSessionId;
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={sessionId.Trim()}");
                    }

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (JsonObject.TryParse(json, out var obj) && obj.ContainsKey("id"))
                        {
                            string id = obj.GetNamedString("id");
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            catch { }

            return rawSearchId;
        }

        public async Task<List<TradeListing>> GetInitialSearchListingsAsync(string league, string searchId)
        {
            var listings = new List<TradeListing>();
            if (string.IsNullOrWhiteSpace(league) || string.IsNullOrWhiteSpace(searchId)) return listings;

            try
            {
                string resolvedId = await ResolveSearchIdAsync(league, searchId);
                string url = $"{TradeBaseUrl}/search/{Uri.EscapeDataString(league)}/{resolvedId}";

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    request.Headers.TryAddWithoutValidation("Accept", "application/json");
                    request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                    request.Headers.TryAddWithoutValidation("Origin", "https://www.pathofexile.com");

                    string sessionId = PoeSettingsManager.Instance.PoeSessionId;
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", $"POESESSID={sessionId.Trim()}");
                    }

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (JsonObject.TryParse(json, out var obj) && obj.ContainsKey("result") && obj.GetNamedValue("result").ValueType == JsonValueType.Array)
                        {
                            var arr = obj.GetNamedArray("result");
                            var hashes = new List<string>();
                            foreach (var elem in arr)
                            {
                                if (elem.ValueType == JsonValueType.String)
                                {
                                    hashes.Add(elem.GetString());
                                    if (hashes.Count >= 10) break;
                                }
                            }

                            if (hashes.Count > 0)
                            {
                                return await FetchListingsAsync(hashes, league, resolvedId);
                            }
                        }
                    }
                }
            }
            catch { }

            return listings;
        }
    }
}
