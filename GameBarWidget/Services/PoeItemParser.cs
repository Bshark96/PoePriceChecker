using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameBarWidget.Services
{
    public enum PoeRarity
    {
        Normal,
        Magic,
        Rare,
        Unique,
        Gem,
        Currency,
        DivinationCard
    }

    public enum ItemNamespace
    {
        Item,
        Unique,
        Gem,
        DivinationCard,
        Currency,
        Map,
        CapturedBeast
    }

    public enum ModifierType
    {
        Explicit,
        Implicit,
        Fractured,
        Crafted,
        Enchant,
        Pseudo
    }

    public sealed class ItemModifier
    {
        public ModifierType Type { get; set; } = ModifierType.Explicit;
        public string StatId { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public double? NumberValue { get; set; }
        public double? MinRoll { get; set; }
        public double? MaxRoll { get; set; }
        public string TierInfo { get; set; } = string.Empty;
        public bool IsPrefix { get; set; } = false;
        public bool IsSuffix { get; set; } = false;
        public bool IsLocal { get; set; } = false;
        public bool IsActive { get; set; } = false;
        public bool IsPseudo { get; set; } = false;
        public bool IsUnscalable { get; set; } = false;

        public string DisplayText
        {
            get
            {
                string localTag = IsLocal ? "[Local] " : string.Empty;
                string tierPrefix = !string.IsNullOrEmpty(TierInfo) ? $"[{TierInfo}] " : (IsPrefix ? "[Prefix] " : (IsSuffix ? "[Suffix] " : string.Empty));
                if (IsPseudo) return $"[Pseudo] {RawText}";
                if (Type == ModifierType.Implicit) return $"[Implicit] {tierPrefix}{RawText}";
                if (Type == ModifierType.Fractured) return $"[Fractured] {tierPrefix}{RawText}";
                if (Type == ModifierType.Crafted) return $"[Crafted] {tierPrefix}{RawText}";
                if (Type == ModifierType.Enchant) return $"[Enchant] {RawText}";
                return $"{localTag}{tierPrefix}{RawText}";
            }
        }
    }

    public sealed class PoeItem
    {
        public string ItemClass { get; set; } = string.Empty;
        public PoeRarity Rarity { get; set; } = PoeRarity.Normal;
        public ItemNamespace Namespace { get; set; } = ItemNamespace.Item;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BaseType { get; set; } = string.Empty;
        public int ItemLevel { get; set; }
        public int Quality { get; set; }
        public int GemLevel { get; set; }
        public int MapTier { get; set; }
        public bool IsCorrupted { get; set; }
        public string CorruptedFilterOption { get; set; } = "any";
        public bool IsMirrored { get; set; }
        public bool IsUnidentified { get; set; }
        public bool IsSynthesised { get; set; }
        public bool IsFractured { get; set; }
        public bool IsShaper { get; set; }
        public bool IsElder { get; set; }
        public bool IsCrusader { get; set; }
        public bool IsRedeemer { get; set; }
        public bool IsHunter { get; set; }
        public bool IsWarlord { get; set; }
        public bool IsTransfiguredGem { get; set; }
        public string NormalGemVariant { get; set; } = string.Empty;
        public string TradeDiscriminator { get; set; } = string.Empty;

        public string SocketRaw { get; set; } = string.Empty;
        public int SocketCount { get; set; }
        public int LinkCount { get; set; }

        public int? FilterSocketsMin { get; set; }
        public int? FilterSocketsMax { get; set; }
        public bool FilterSocketsActive { get; set; } = false;

        public int? FilterLinksMin { get; set; }
        public int? FilterLinksMax { get; set; }
        public bool FilterLinksActive { get; set; } = false;

        public int? FilterQualityMin { get; set; }
        public int? FilterQualityMax { get; set; }
        public bool FilterQualityActive { get; set; } = false;

        public double AttacksPerSecond { get; set; }
        public double CritChance { get; set; }
        public double PhysDamageMin { get; set; }
        public double PhysDamageMax { get; set; }
        public double EleDamageMin { get; set; }
        public double EleDamageMax { get; set; }
        public double PhysicalDps { get; set; }
        public double ElementalDps { get; set; }
        public double TotalDps { get; set; }

        public int Armour { get; set; }
        public int Evasion { get; set; }
        public int EnergyShield { get; set; }

        public double PseudoTotalLife { get; set; }
        public double PseudoTotalMana { get; set; }
        public double PseudoTotalElementalResistance { get; set; }
        public double PseudoTotalAllResistance { get; set; }
        public double PseudoTotalFireResistance { get; set; }
        public double PseudoTotalColdResistance { get; set; }
        public double PseudoTotalLightningResistance { get; set; }
        public double PseudoTotalChaosResistance { get; set; }
        public double PseudoTotalAttackSpeed { get; set; }
        public double PseudoTotalAttributes { get; set; }

        public List<ItemModifier> Modifiers { get; set; } = new List<ItemModifier>();
        public List<ItemModifier> PseudoModifiers { get; set; } = new List<ItemModifier>();
        public string RawText { get; set; } = string.Empty;
    }

    public static class PoeItemParser
    {
        private static readonly Regex NumberRegex = new Regex(@"([+-]?\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex DigitsOnlyRegex = new Regex(@"(\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex AddsDamageRegex = new Regex(@"^Adds\s+(\d+)\s+to\s+(\d+)\s+(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RangeDamageRegex = new Regex(@"(\d+)[-–](\d+)", RegexOptions.Compiled);
        private static readonly Regex AdvancedModHeaderRegex = new Regex(@"^\{.*\}", RegexOptions.Compiled);
        private static readonly Regex RangeBracketsRegex = new Regex(@"\s*\(([+-]?\d+(?:\.\d+)?)[-–]([+-]?\d+(?:\.\d+)?)\)", RegexOptions.Compiled);
        private static readonly Regex TierRegex = new Regex(@"(?:Tier:\s*(\d+)|Rank:\s*(\d+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string> StatTemplateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ExplicitStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ImplicitStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> FracturedStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> CraftedStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> EnchantStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> PseudoStatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _isInitialized = false;

        public static async Task InitializeStatsDatabaseAsync()
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

        private static string StripScopeSuffix(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return key;
            int idx = key.LastIndexOf('(');
            if (idx > 0 && key.EndsWith(")", StringComparison.Ordinal))
            {
                string suffix = key.Substring(idx).ToLowerInvariant();
                if (suffix.Contains("explicit") || suffix.Contains("implicit") || suffix.Contains("fractured") || 
                    suffix.Contains("crafted") || suffix.Contains("enchant") || suffix.Contains("pseudo"))
                {
                    return key.Substring(0, idx).Trim();
                }
            }
            return key;
        }

        public static PoeItem Parse(string rawText)
        {
            var item = new PoeItem { RawText = rawText ?? string.Empty };
            if (string.IsNullOrWhiteSpace(rawText)) return item;

            // Normalize curly apostrophes and quotes
            rawText = rawText.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');

            string[] blocks = rawText.Split(new[] { "--------" }, StringSplitOptions.None);
            if (blocks.Length == 0) return item;

            // Block 0: Header & Names
            string[] headerLines = blocks[0].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int nameLineIndex = -1;
            for (int i = 0; i < headerLines.Length; i++)
            {
                string line = headerLines[i].Trim();
                if (line.StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase)) item.ItemClass = line.Substring(11).Trim();
                else if (line.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase))
                {
                    item.Rarity = ParseRarity(line.Substring(7).Trim());
                    nameLineIndex = i + 1;
                }
            }

            if (nameLineIndex >= 0 && nameLineIndex < headerLines.Length)
            {
                item.Name = headerLines[nameLineIndex].Trim();
                item.BaseType = (nameLineIndex + 1 < headerLines.Length) ? headerLines[nameLineIndex + 1].Trim() : item.Name;
            }

            // Immediately contextualize item category and namespace from Header Block (like Awakened PoE Trade)
            ResolveItemNamespaceAndCategory(item);

            // Determine if Alt-copy ({ Prefix/Suffix ... }) is used
            bool hasAdvancedHeaders = false;
            var modifierBlockIndices = new List<int>();

            for (int b = 1; b < blocks.Length; b++)
            {
                string[] bLines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                bool hasMod = false, isPropOrReq = false;
                foreach (var bl in bLines)
                {
                    string trimmed = bl.Trim();
                    if (AdvancedModHeaderRegex.IsMatch(trimmed)) hasAdvancedHeaders = true;
                    if (trimmed.StartsWith("Requirements:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Elemental Damage:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Armour:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Evasion Rating:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Energy Shield:", StringComparison.OrdinalIgnoreCase))
                    {
                        isPropOrReq = true;
                        break;
                    }
                    if (IsModifierLine(trimmed) || AdvancedModHeaderRegex.IsMatch(trimmed)) hasMod = true;
                }
                if (hasMod && !isPropOrReq) modifierBlockIndices.Add(b);
            }

            int implicitBlockIndex = (!hasAdvancedHeaders && modifierBlockIndices.Count >= 2) ? modifierBlockIndices[0] : -1;

            ModifierType pendingType = ModifierType.Explicit;
            string pendingTier = string.Empty;

            for (int b = 1; b < blocks.Length; b++)
            {
                ModifierType blockDefaultType = (b == implicitBlockIndex) ? ModifierType.Implicit : ModifierType.Explicit;
                pendingType = blockDefaultType;

                string[] lines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                // If advanced mod headers exist on this item, skip non-headed blocks that lack properties/requirements (e.g. unique flavor text)
                if (hasAdvancedHeaders)
                {
                    bool blockHasHeader = false;
                    bool hasItemProperties = false;
                    foreach (var bl in lines)
                    {
                        string trimmed = bl.Trim();
                        if (AdvancedModHeaderRegex.IsMatch(trimmed)) { blockHasHeader = true; break; }
                        if (trimmed.StartsWith("Requirements:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Elemental Damage:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Armour:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Energy Shield:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Corrupted", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Mirrored", StringComparison.OrdinalIgnoreCase))
                        {
                            hasItemProperties = true;
                            break;
                        }
                    }
                    if (!blockHasHeader && !hasItemProperties) continue; // Skip lore / flavor text
                }

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (AdvancedModHeaderRegex.IsMatch(line))
                    {
                        if (line.IndexOf("Prefix", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = ExtractTierText(line, "P"); }
                        else if (line.IndexOf("Suffix", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = ExtractTierText(line, "S"); }
                        else if (line.IndexOf("Implicit", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Implicit; pendingTier = line.IndexOf("Corruption", StringComparison.OrdinalIgnoreCase) >= 0 ? "CORRUPT" : "IMP"; }
                        else if (line.IndexOf("Fractured", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Fractured; pendingTier = ExtractTierText(line, "FRAC"); }
                        else if (line.IndexOf("Crafted", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Crafted; pendingTier = "CRAFT"; }
                        else if (line.IndexOf("Enchant", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Enchant; pendingTier = "ENC"; }
                        else if (line.IndexOf("Unique", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = "UNI"; }
                        continue;
                    }

                    if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Substring(11).Trim(), out int ilvl)) item.ItemLevel = ilvl;
                    else if ((line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Gem Level:", StringComparison.OrdinalIgnoreCase)) && NumberRegex.Match(line).Success && int.TryParse(NumberRegex.Match(line).Groups[1].Value, out int gLvl)) { item.GemLevel = gLvl; }
                    else if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase) && NumberRegex.Match(line).Success && int.TryParse(NumberRegex.Match(line).Groups[1].Value, out int q)) item.Quality = q;
                    else if (line.StartsWith("Map Tier:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Substring(9).Trim(), out int tier)) item.MapTier = tier;
                    else if (line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase)) ParseSockets(line.Substring(8).Trim(), item);
                    else if (line.StartsWith("Attacks per Second:", StringComparison.OrdinalIgnoreCase) && double.TryParse(NumberRegex.Match(line).Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double aps)) item.AttacksPerSecond = aps;
                    else if (line.StartsWith("Critical Strike Chance:", StringComparison.OrdinalIgnoreCase) && double.TryParse(NumberRegex.Match(line).Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double crit)) item.CritChance = crit;
                    else if (line.StartsWith("Physical Damage:", StringComparison.OrdinalIgnoreCase))
                    {
                        var rm = RangeDamageRegex.Match(line);
                        if (rm.Success) { double.TryParse(rm.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pmin); double.TryParse(rm.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pmax); item.PhysDamageMin = pmin; item.PhysDamageMax = pmax; }
                    }
                    else if (line.StartsWith("Elemental Damage:", StringComparison.OrdinalIgnoreCase))
                    {
                        double tmin = 0, tmax = 0;
                        foreach (Match m in RangeDamageRegex.Matches(line))
                        {
                            double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double emin);
                            double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double emax);
                            tmin += emin; tmax += emax;
                        }
                        item.EleDamageMin = tmin; item.EleDamageMax = tmax;
                    }
                    else if (line.StartsWith("Armour:", StringComparison.OrdinalIgnoreCase) && int.TryParse(NumberRegex.Match(line).Groups[1].Value, out int arm)) item.Armour = arm;
                    else if (line.StartsWith("Evasion Rating:", StringComparison.OrdinalIgnoreCase) && int.TryParse(NumberRegex.Match(line).Groups[1].Value, out int eva)) item.Evasion = eva;
                    else if (line.StartsWith("Energy Shield:", StringComparison.OrdinalIgnoreCase) && int.TryParse(NumberRegex.Match(line).Groups[1].Value, out int es)) item.EnergyShield = es;
                    else if (line.Equals("Corrupted", StringComparison.OrdinalIgnoreCase)) item.IsCorrupted = true;
                    else if (line.Equals("Mirrored", StringComparison.OrdinalIgnoreCase)) item.IsMirrored = true;
                    else if (line.Equals("Unidentified", StringComparison.OrdinalIgnoreCase)) item.IsUnidentified = true;
                    else if (IsModifierLine(line))
                    {
                        var mod = ParseModifier(line, pendingType, pendingTier, item.Category);
                        item.Modifiers.Add(mod);
                        pendingType = blockDefaultType;
                        pendingTier = string.Empty;
                    }
                }
            }

            // DPS Math
            if (item.AttacksPerSecond > 0)
            {
                if (item.PhysDamageMax > 0) item.PhysicalDps = Math.Round(((item.PhysDamageMin + item.PhysDamageMax) / 2.0) * item.AttacksPerSecond, 1);
                if (item.EleDamageMax > 0) item.ElementalDps = Math.Round(((item.EleDamageMin + item.EleDamageMax) / 2.0) * item.AttacksPerSecond, 1);
                item.TotalDps = Math.Round(item.PhysicalDps + item.ElementalDps, 1);
            }

            CalculatePseudoStats(item);
            return item;
        }

        private static string LookupScopedStatId(string template, ModifierType type, string cleanText, string category)
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

        private static ItemModifier ParseModifier(string line, ModifierType headerType, string tierInfo, string category)
        {
            var mod = new ItemModifier { Type = headerType, TierInfo = tierInfo };

            // Detect if modifier is unscalable
            bool isUnscalable = line.IndexOf("unscalable", StringComparison.OrdinalIgnoreCase) >= 0;
            mod.IsUnscalable = isUnscalable;

            // Strip unscalable suffix and annotations (handling all dashes: em-dash, en-dash, ASCII hyphen, etc.)
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*\((?:unscalable(?:\s+value)?)\)", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();

            if (line.EndsWith("(implicit)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Implicit; line = line.Substring(0, line.Length - 10).Trim(); }
            else if (line.EndsWith("(fractured)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Fractured; line = line.Substring(0, line.Length - 11).Trim(); }
            else if (line.EndsWith("(crafted)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Crafted; line = line.Substring(0, line.Length - 9).Trim(); }
            else if (line.EndsWith("(enchant)", StringComparison.OrdinalIgnoreCase)) { mod.Type = ModifierType.Enchant; line = line.Substring(0, line.Length - 9).Trim(); }

            line = Regex.Replace(line, @"\s*\((?:augmented|unscalable(?:\s+value)?|synthesised|crucible|scourge)\)", "", RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();

            // Set clean RawText (without unscalable or tag annotations)
            mod.RawText = line;

            // Extract brackets only if scalable
            var rbMatches = RangeBracketsRegex.Matches(line);
            if (!mod.IsUnscalable && rbMatches.Count > 0)
            {
                if (double.TryParse(rbMatches[0].Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmin)) mod.MinRoll = Math.Abs(rmin);
                if (double.TryParse(rbMatches[0].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax)) mod.MaxRoll = Math.Abs(rmax);
                if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                {
                    double tmp = mod.MinRoll.Value;
                    mod.MinRoll = mod.MaxRoll.Value;
                    mod.MaxRoll = tmp;
                }
                if (rbMatches.Count > 1 && double.TryParse(rbMatches[1].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax2))
                {
                    rmax2 = Math.Abs(rmax2);
                    if (!mod.MaxRoll.HasValue || rmax2 > mod.MaxRoll.Value) mod.MaxRoll = rmax2;
                }
            }

            string clean = RangeBracketsRegex.Replace(line, "").Trim();
            clean = Regex.Replace(clean, @"\s*to\s*$", "").Trim();

            var addsMatch = AddsDamageRegex.Match(clean);
            if (addsMatch.Success)
            {
                double.TryParse(addsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double minV);
                double.TryParse(addsMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxV);
                minV = Math.Abs(minV);
                maxV = Math.Abs(maxV);
                if (minV > maxV) { double tmp = minV; minV = maxV; maxV = tmp; }
                string dt = addsMatch.Groups[3].Value.Trim();
                double avg = (minV + maxV) / 2.0;

                mod.Template = $"Adds # to # {dt}";
                
                if (!mod.IsUnscalable)
                {
                    // If only 1 bracket was on the max roll (e.g. Adds 1 to 575(550-600))
                    if (rbMatches.Count == 1 && minV <= 1)
                    {
                        mod.NumberValue = maxV;
                        if (double.TryParse(rbMatches[0].Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmin)) mod.MinRoll = Math.Abs(rmin);
                        if (double.TryParse(rbMatches[0].Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rmax)) mod.MaxRoll = Math.Abs(rmax);
                        if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                        {
                            double tmp = mod.MinRoll.Value;
                            mod.MinRoll = mod.MaxRoll.Value;
                            mod.MaxRoll = tmp;
                        }
                    }
                    else
                    {
                        mod.NumberValue = avg;
                        if (!mod.MinRoll.HasValue) mod.MinRoll = avg;
                        if (!mod.MaxRoll.HasValue) mod.MaxRoll = avg;
                    }
                }

                mod.StatId = LookupScopedStatId(mod.Template, mod.Type, clean, category);
            }
            else
            {
                string template = DigitsOnlyRegex.Replace(clean, "#");
                if (template.StartsWith("-"))
                {
                    template = "+" + template.Substring(1);
                }
                mod.Template = template;
                mod.StatId = LookupScopedStatId(template, mod.Type, clean, category);

                if (!mod.IsUnscalable)
                {
                    var match = NumberRegex.Match(clean);
                    if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double rawVal))
                    {
                        double val = Math.Abs(rawVal); // Treat negative values as positive
                        mod.NumberValue = val;
                        if (!mod.MinRoll.HasValue) mod.MinRoll = Math.Floor(val * 0.85);
                        if (!mod.MaxRoll.HasValue) mod.MaxRoll = val;
                        if (mod.MinRoll.HasValue && mod.MaxRoll.HasValue && mod.MinRoll.Value > mod.MaxRoll.Value)
                        {
                            double tmp = mod.MinRoll.Value;
                            mod.MinRoll = mod.MaxRoll.Value;
                            mod.MaxRoll = tmp;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(tierInfo))
            {
                if (tierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase)) mod.IsPrefix = true;
                else if (tierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase)) mod.IsSuffix = true;
            }
            else if (mod.Type == ModifierType.Explicit)
            {
                string cl = clean.ToLowerInvariant();
                if (cl.Contains("resistance") || cl.Contains("to strength") || cl.Contains("to dexterity") || cl.Contains("to intelligence") || cl.Contains("attack speed") || cl.Contains("cast speed") || cl.Contains("critical strike") || cl.Contains("accuracy rating"))
                {
                    mod.IsSuffix = true; mod.TierInfo = "Suffix";
                }
                else if (cl.Contains("maximum life") || cl.Contains("maximum mana") || cl.Contains("maximum energy shield") || cl.Contains("damage") || cl.Contains("armour") || cl.Contains("evasion"))
                {
                    mod.IsPrefix = true; mod.TierInfo = "Prefix";
                }
            }

            // Determine Local vs Global context
            bool isWeapon = (!string.IsNullOrEmpty(category) && (category.StartsWith("weapon.", StringComparison.OrdinalIgnoreCase) || category.Equals("weapon", StringComparison.OrdinalIgnoreCase)))
                || (category != null && category.Contains("weapon"));
            bool isArmour = (!string.IsNullOrEmpty(category) && (category.StartsWith("armour.", StringComparison.OrdinalIgnoreCase) || category.Equals("armour", StringComparison.OrdinalIgnoreCase)))
                || (category != null && category.Contains("armour"));

            if (mod.Type == ModifierType.Explicit || mod.Type == ModifierType.Fractured || mod.Type == ModifierType.Crafted)
            {
                string tm = (mod.Template ?? string.Empty).ToLowerInvariant();
                string cl = clean.ToLowerInvariant();
                if (isWeapon)
                {
                    if (tm.Contains("adds # to #") || cl.Contains("adds ") ||
                        tm.Contains("attack speed") || tm.Contains("critical strike") || tm.Contains("physical damage") || tm.Contains("accuracy rating"))
                    {
                        mod.IsLocal = true;
                    }
                }
                else if (isArmour)
                {
                    if (tm.Contains("to armour") || tm.Contains("to evasion rating") || tm.Contains("to energy shield") || tm.Contains("to ward") ||
                        tm.Contains("increased armour") || tm.Contains("increased evasion rating") || tm.Contains("increased energy shield") || tm.Contains("increased ward"))
                    {
                        mod.IsLocal = true;
                    }
                }
            }

            mod.IsActive = false;
            return mod;
        }

        private static void CalculatePseudoStats(PoeItem item)
        {
            double str = 0, dex = 0, intl = 0, allAttr = 0, fireRes = 0, coldRes = 0, lightRes = 0, allRes = 0, chaosRes = 0, maxLife = 0, maxMana = 0, totalAspd = 0;
            double flatColdMin = 0, flatColdMax = 0, flatLightMin = 0, flatLightMax = 0, flatFireMin = 0, flatFireMax = 0, flatPhysMin = 0, flatPhysMax = 0;

            foreach (var mod in item.Modifiers)
            {
                string l = mod.RawText.ToLowerInvariant();
                double val = mod.NumberValue ?? 0;
                if (l.Contains("to strength") && !l.Contains("req")) str += val;
                else if (l.Contains("to dexterity") && !l.Contains("req")) dex += val;
                else if (l.Contains("to intelligence") && !l.Contains("req")) intl += val;
                else if (l.Contains("to all attributes")) allAttr += val;

                if (l.Contains("to fire resistance")) fireRes += val;
                else if (l.Contains("to cold resistance")) coldRes += val;
                else if (l.Contains("to lightning resistance")) lightRes += val;
                else if (l.Contains("to all elemental resistances")) allRes += val;
                else if (l.Contains("to chaos resistance")) chaosRes += val;

                if (l.Contains("to maximum life")) maxLife += val;
                if (l.Contains("to maximum mana")) maxMana += val;
                if (l.Contains("increased attack speed")) totalAspd += val;

                var am = AddsDamageRegex.Match(mod.RawText);
                if (am.Success)
                {
                    double.TryParse(am.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double min);
                    double.TryParse(am.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double max);
                    string dt = am.Groups[3].Value.ToLowerInvariant();
                    if (dt.Contains("cold")) { flatColdMin += min; flatColdMax += max; }
                    else if (dt.Contains("light")) { flatLightMin += min; flatLightMax += max; }
                    else if (dt.Contains("fire")) { flatFireMin += min; flatFireMax += max; }
                    else if (dt.Contains("phys")) { flatPhysMin += min; flatPhysMax += max; }
                }
            }

            item.PseudoTotalLife = maxLife + Math.Floor((str + allAttr) / 2.0);
            item.PseudoTotalMana = maxMana + Math.Floor((intl + allAttr) / 2.0);
            item.PseudoTotalElementalResistance = fireRes + coldRes + lightRes + (allRes * 3.0);
            item.PseudoTotalAllResistance = item.PseudoTotalElementalResistance + chaosRes;

            AddPseudo(item, "pseudo.pseudo_total_life", $"+{item.PseudoTotalLife} total maximum Life", item.PseudoTotalLife, Math.Floor(item.PseudoTotalLife * 0.85), item.PseudoTotalLife);
            AddPseudo(item, "pseudo.pseudo_total_elemental_resistance", $"+{item.PseudoTotalElementalResistance}% total Elemental Resistance", item.PseudoTotalElementalResistance, Math.Floor(item.PseudoTotalElementalResistance * 0.85), item.PseudoTotalElementalResistance);
            if (flatColdMax > 0) AddPseudo(item, "pseudo.pseudo_adds_cold_damage_to_attacks", $"Adds {flatColdMin} to {flatColdMax} Cold Damage to Attacks", (flatColdMin + flatColdMax) / 2.0, (flatColdMin + flatColdMax) / 2.0, (flatColdMin + flatColdMax) / 2.0);
            if (flatLightMax > 0) AddPseudo(item, "pseudo.pseudo_adds_lightning_damage_to_attacks", $"Adds {flatLightMin} to {flatLightMax} Lightning Damage to Attacks", (flatLightMin + flatLightMax) / 2.0, (flatLightMin + flatLightMax) / 2.0, (flatLightMin + flatLightMax) / 2.0);
            if (flatFireMax > 0) AddPseudo(item, "pseudo.pseudo_adds_fire_damage_to_attacks", $"Adds {flatFireMin} to {flatFireMax} Fire Damage to Attacks", (flatFireMin + flatFireMax) / 2.0, (flatFireMin + flatFireMax) / 2.0, (flatFireMin + flatFireMax) / 2.0);
            if (flatPhysMax > 0) AddPseudo(item, "pseudo.pseudo_adds_physical_damage_to_attacks", $"Adds {flatPhysMin} to {flatPhysMax} Physical Damage to Attacks", (flatPhysMin + flatPhysMax) / 2.0, (flatPhysMin + flatPhysMax) / 2.0, (flatPhysMin + flatPhysMax) / 2.0);
            if (totalAspd > 0) AddPseudo(item, "pseudo.pseudo_total_attack_speed", $"{totalAspd}% total Attack Speed", totalAspd, totalAspd, totalAspd);
            if (item.TotalDps > 0) AddPseudo(item, "", $"{item.TotalDps} Total DPS", item.TotalDps, Math.Floor(item.TotalDps * 0.9), item.TotalDps);
        }

        private static void AddPseudo(PoeItem item, string statId, string text, double val, double min, double max)
        {
            if (val <= 0) return;
            item.PseudoModifiers.Add(new ItemModifier
            {
                Type = ModifierType.Pseudo,
                IsPseudo = true,
                StatId = statId,
                RawText = text,
                NumberValue = val,
                MinRoll = min,
                MaxRoll = max,
                IsActive = false
            });
        }

        private static readonly (string Match, string Category)[] CategoryMap =
        {
            // Armour
            ("helmet", "armour.helmet"), ("circlet", "armour.helmet"), ("cap", "armour.helmet"), ("crown", "armour.helmet"), ("coif", "armour.helmet"), ("hood", "armour.helmet"), ("mask", "armour.helmet"), ("sallet", "armour.helmet"), ("helm", "armour.helmet"),
            ("body armour", "armour.chest"), ("chest", "armour.chest"), ("vestment", "armour.chest"), ("plate", "armour.chest"), ("coat", "armour.chest"), ("tunic", "armour.chest"), ("hauberk", "armour.chest"), ("garb", "armour.chest"), ("regalia", "armour.chest"), ("robe", "armour.chest"), ("jacket", "armour.chest"), ("brigandine", "armour.chest"), ("doublet", "armour.chest"), ("chainmail", "armour.chest"), ("cuirass", "armour.chest"),
            ("glove", "armour.gloves"), ("gauntlet", "armour.gloves"), ("mitt", "armour.gloves"), ("bracer", "armour.gloves"),
            ("boot", "armour.boots"), ("greave", "armour.boots"), ("shoe", "armour.boots"), ("slipper", "armour.boots"),
            ("shield", "armour.shield"), ("buckler", "armour.shield"), ("bundle", "armour.shield"),
            ("quiver", "armour.quiver"),
            ("focus", "armour.focus"),

            // Accessories
            ("ring", "accessory.ring"),
            ("amulet", "accessory.amulet"), ("talisman", "accessory.amulet"),
            ("belt", "accessory.belt"), ("sash", "accessory.belt"), ("girdle", "accessory.belt"),
            ("trinket", "accessory.trinket"),
            ("tincture", "accessory.tincture"),
            ("charm", "accessory.charm"),

            // Flasks
            ("flask", "flask"),

            // Jewels
            ("cluster jewel", "jewel.cluster"),
            ("abyss jewel", "jewel.abyss"),
            ("timeless jewel", "jewel.timeless"),
            ("jewel", "jewel"),

            // Weapons
            ("thrusting one hand sword", "weapon.onesword"), ("one hand sword", "weapon.onesword"), ("one-hand sword", "weapon.onesword"),
            ("two hand sword", "weapon.twosword"), ("two-hand sword", "weapon.twosword"),
            ("sword", "weapon.sword"),
            ("one hand axe", "weapon.oneaxe"), ("one-hand axe", "weapon.oneaxe"),
            ("two hand axe", "weapon.twoaxe"), ("two-hand axe", "weapon.twoaxe"),
            ("axe", "weapon.axe"),
            ("sceptre", "weapon.sceptre"),
            ("one hand mace", "weapon.onemace"), ("one-hand mace", "weapon.onemace"),
            ("two hand mace", "weapon.twomace"), ("two-hand mace", "weapon.twomace"),
            ("mace", "weapon.mace"),
            ("rune dagger", "weapon.dagger"), ("dagger", "weapon.dagger"),
            ("warstaff", "weapon.staff"), ("staff", "weapon.staff"), ("stave", "weapon.staff"),
            ("claw", "weapon.claw"),
            ("wand", "weapon.wand"),
            ("bow", "weapon.bow"),
            ("crossbow", "weapon.crossbow"),
            ("flail", "weapon.flail"),
            ("spear", "weapon.spear"),
            ("fishing rod", "weapon.rod"), ("rod", "weapon.rod")
        };

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

        private static void ResolveItemNamespaceAndCategory(PoeItem item)
        {
            string c = (((item.ItemClass ?? "") + " " + (item.Name ?? "") + " " + (item.BaseType ?? "")).Trim()).ToLowerInvariant();

            // 1. Gems
            if (item.Rarity == PoeRarity.Gem || c.Contains("gem"))
            {
                item.Namespace = ItemNamespace.Gem;
                if (c.Contains("support")) item.Category = "gem.supportgem";
                else if (c.Contains("meta")) item.Category = "gem.metagem";
                else item.Category = "gem.activegem";

                // Transfigured Gem check
                string gemFullName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
                if (!string.IsNullOrWhiteSpace(gemFullName) && gemFullName.IndexOf(" of ", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    item.IsTransfiguredGem = true;
                    int idxOf = gemFullName.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
                    item.NormalGemVariant = gemFullName.Substring(0, idxOf).Trim();
                }
                return;
            }

            // 2. Divination Cards
            if (item.Rarity == PoeRarity.DivinationCard || c.Contains("card") || c.Contains("divination"))
            {
                item.Namespace = ItemNamespace.DivinationCard;
                item.Category = "card";
                return;
            }

            // 3. Currency & Stackables
            if (item.Rarity == PoeRarity.Currency || c.Contains("currency") || c.Contains("essence") || c.Contains("fossil") || c.Contains("resonator") || c.Contains("omen") || c.Contains("tattoo") || c.Contains("oil") || c.Contains("catalyst") || c.Contains("incubator") || c.Contains("lifeforce"))
            {
                item.Namespace = ItemNamespace.Currency;
                if (c.Contains("essence")) item.Category = "currency.essence";
                else if (c.Contains("fossil")) item.Category = "currency.fossil";
                else if (c.Contains("resonator")) item.Category = "currency.resonator";
                else if (c.Contains("omen")) item.Category = "currency.omen";
                else if (c.Contains("tattoo")) item.Category = "currency.tattoo";
                else if (c.Contains("oil")) item.Category = "currency.oil";
                else if (c.Contains("catalyst")) item.Category = "currency.catalyst";
                else if (c.Contains("incubator")) item.Category = "currency.incubator";
                else if (c.Contains("lifeforce")) item.Category = "currency.lifeforce";
                else item.Category = "currency";
                return;
            }

            // 4. Maps & Endgame Items
            if (c.Contains("map") || c.Contains("waystone") || c.Contains("scarab") || c.Contains("fragment") || c.Contains("logbook") || c.Contains("relic") || c.Contains("memory") || c.Contains("contract") || c.Contains("blueprint") || item.MapTier > 0)
            {
                item.Namespace = ItemNamespace.Map;
                if (c.Contains("blight-ravaged") || c.Contains("blight ravaged")) item.Category = "map.ravaged";
                else if (c.Contains("blighted")) item.Category = "map.blighted";
                else if (c.Contains("waystone")) item.Category = "map.waystone";
                else if (c.Contains("scarab")) item.Category = "map.scarab";
                else if (c.Contains("fragment") || c.Contains("sacrifice") || c.Contains("mortal") || c.Contains("key") || c.Contains("vessel") || c.Contains("offering") || c.Contains("splinter")) item.Category = "map.fragment";
                else if (c.Contains("logbook")) item.Category = "expedition.logbook";
                else if (c.Contains("relic")) item.Category = "sanctum.relic";
                else if (c.Contains("memory")) item.Category = "memory";
                else if (c.Contains("blueprint")) item.Category = "heistblueprint";
                else if (c.Contains("contract")) item.Category = "heistcontract";
                else item.Category = "map";
                return;
            }

            // 5. Beasts & Corpse / Samples
            if (c.Contains("beast") || c.Contains("corpse") || c.Contains("metamorph sample"))
            {
                item.Namespace = ItemNamespace.CapturedBeast;
                item.Category = c.Contains("beast") ? "monster.beast" : "monster.sample";
                return;
            }

            // 6. Standard Equippable Items & Uniques
            item.Namespace = (item.Rarity == PoeRarity.Unique) ? ItemNamespace.Unique : ItemNamespace.Item;

            foreach (var (match, cat) in CategoryMap)
            {
                if (c.Contains(match))
                {
                    item.Category = cat;
                    break;
                }
            }
        }

        private static string ExtractTierText(string headerLine, string prefixChar)
        {
            var m = TierRegex.Match(headerLine);
            return m.Success ? $"{prefixChar}{(!string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value)}" : prefixChar;
        }

        private static void ParseSockets(string socketStr, PoeItem item)
        {
            item.SocketRaw = socketStr;
            if (string.IsNullOrWhiteSpace(socketStr)) return;
            string[] groups = socketStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int totalSockets = 0, maxLinks = 0;
            foreach (var g in groups)
            {
                string[] sockets = g.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                totalSockets += sockets.Length;
                if (sockets.Length > maxLinks) maxLinks = sockets.Length;
            }
            item.SocketCount = totalSockets;
            item.LinkCount = maxLinks;
        }

        private static PoeRarity ParseRarity(string rarityStr)
        {
            if (Enum.TryParse<PoeRarity>(rarityStr, true, out var r)) return r;
            if (rarityStr.Equals("Currency", StringComparison.OrdinalIgnoreCase)) return PoeRarity.Currency;
            if (rarityStr.Equals("Gem", StringComparison.OrdinalIgnoreCase)) return PoeRarity.Gem;
            if (rarityStr.Equals("Divination Card", StringComparison.OrdinalIgnoreCase)) return PoeRarity.DivinationCard;
            return PoeRarity.Normal;
        }

        private static bool IsModifierLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string lower = line.ToLowerInvariant();
            if (lower.StartsWith("item class:") || lower.StartsWith("rarity:") || lower.StartsWith("requirements:") ||
                lower.StartsWith("level:") || lower.StartsWith("str:") || lower.StartsWith("dex:") || lower.StartsWith("int:") ||
                lower.StartsWith("strength:") || lower.StartsWith("dexterity:") || lower.StartsWith("intelligence:") ||
                lower.StartsWith("item level:") || lower.StartsWith("quality:") || lower.StartsWith("sockets:") ||
                lower.StartsWith("physical damage:") || lower.StartsWith("elemental damage:") || lower.StartsWith("chaos damage:") ||
                lower.StartsWith("attacks per second:") || lower.StartsWith("critical strike chance:") || lower.StartsWith("weapon range:") ||
                lower.StartsWith("chance to block:") || lower.StartsWith("armour:") || lower.StartsWith("evasion rating:") ||
                lower.StartsWith("energy shield:") || lower.StartsWith("ward:") || lower.StartsWith("note:") ||
                lower == "corrupted" || lower == "mirrored" || lower == "unidentified" || lower == "synthesised" ||
                lower.StartsWith("two handed") || lower.StartsWith("one handed") || lower.StartsWith("one hand") || lower.StartsWith("two hand"))
            {
                return false;
            }
            return true;
        }

        private static string GuessStatId(string rawText)
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
