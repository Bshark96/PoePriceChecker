using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using GameBarWidget.Services.Parsing;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Master item parser. Orchestrates block tokenization, modifier parsing,
    /// and delegates domain-specific logic to modular IItemTypeParser implementations.
    /// </summary>
    public static class PoeItemParser
    {
        private static readonly List<IItemTypeParser> TypeParsers = new List<IItemTypeParser>
        {
            new GemParser(),
            new CurrencyParser(),
            new DivinationCardParser(),
            new MapParser(),
            new WeaponParser(),
            new ArmourParser(),
            new AccessoryParser(),
            new JewelParser(),
            new FlaskParser()
        };

        public static async Task InitializeStatsDatabaseAsync()
        {
            await PoeStatsDatabase.InitializeAsync();
            await PoeItemsDatabase.InitializeAsync();
        }

        public static PoeItem Parse(string rawText)
        {
            var item = new PoeItem { RawText = rawText ?? string.Empty };
            if (string.IsNullOrWhiteSpace(rawText)) return item;

            // Normalize curly apostrophes and quotes
            rawText = rawText.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');

            string[] blocks = rawText.Split(new[] { "--------" }, StringSplitOptions.None);
            if (blocks.Length == 0) return item;

            // Block 0: Header, Item Class, Rarity, and Names
            string[] headerLines = blocks[0].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int nameLineIndex = -1;
            for (int i = 0; i < headerLines.Length; i++)
            {
                string line = headerLines[i].Trim();
                if (line.StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase))
                {
                    item.ItemClass = line.Substring(11).Trim();
                }
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

            // Contextualize base item category
            PoeCategoryResolver.ResolveItemNamespaceAndCategory(item);

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
                    if (PoeModifierParser.AdvancedModHeaderRegex.IsMatch(trimmed)) hasAdvancedHeaders = true;
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
                    if (PoeModifierParser.IsModifierLine(trimmed) || PoeModifierParser.AdvancedModHeaderRegex.IsMatch(trimmed)) hasMod = true;
                }
                if (hasMod && !isPropOrReq) modifierBlockIndices.Add(b);
            }

            int implicitBlockIndex = (!hasAdvancedHeaders && modifierBlockIndices.Count >= 2) ? modifierBlockIndices[0] : -1;

            ModifierType pendingType = ModifierType.Explicit;
            string pendingTier = string.Empty;

            // Parse general properties and modifiers across all blocks
            for (int b = 1; b < blocks.Length; b++)
            {
                ModifierType blockDefaultType = (b == implicitBlockIndex) ? ModifierType.Implicit : ModifierType.Explicit;
                pendingType = blockDefaultType;

                string[] lines = blocks[b].Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (hasAdvancedHeaders)
                {
                    bool blockHasHeader = false;
                    bool hasItemProperties = false;
                    foreach (var bl in lines)
                    {
                        string trimmed = bl.Trim();
                        if (PoeModifierParser.AdvancedModHeaderRegex.IsMatch(trimmed)) { blockHasHeader = true; break; }
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
                    if (!blockHasHeader && !hasItemProperties) continue;
                }

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (PoeModifierParser.AdvancedModHeaderRegex.IsMatch(line))
                    {
                        if (line.IndexOf("Prefix", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = PoeModifierParser.ExtractTierText(line, "P"); }
                        else if (line.IndexOf("Suffix", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = PoeModifierParser.ExtractTierText(line, "S"); }
                        else if (line.IndexOf("Implicit", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Implicit; pendingTier = line.IndexOf("Corruption", StringComparison.OrdinalIgnoreCase) >= 0 ? "CORRUPT" : "IMP"; }
                        else if (line.IndexOf("Fractured", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Fractured; pendingTier = PoeModifierParser.ExtractTierText(line, "FRAC"); }
                        else if (line.IndexOf("Crafted", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Crafted; pendingTier = "CRAFT"; }
                        else if (line.IndexOf("Enchant", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Enchant; pendingTier = "ENC"; }
                        else if (line.IndexOf("Unique", StringComparison.OrdinalIgnoreCase) >= 0) { pendingType = ModifierType.Explicit; pendingTier = "UNI"; }
                        continue;
                    }

                    if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Substring(11).Trim(), out int ilvl)) item.ItemLevel = ilvl;
                    else if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase) && PoeModifierParser.NumberRegex.Match(line).Success && int.TryParse(PoeModifierParser.NumberRegex.Match(line).Groups[1].Value, out int q)) item.Quality = q;
                    else if (line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase)) ParseSockets(line.Substring(8).Trim(), item);
                    else if (line.Equals("Corrupted", StringComparison.OrdinalIgnoreCase)) item.IsCorrupted = true;
                    else if (line.Equals("Mirrored", StringComparison.OrdinalIgnoreCase)) item.IsMirrored = true;
                    else if (line.Equals("Unidentified", StringComparison.OrdinalIgnoreCase)) item.IsUnidentified = true;
                    else if (PoeModifierParser.IsModifierLine(line))
                    {
                        var mod = PoeModifierParser.ParseModifier(line, pendingType, pendingTier, item.Category);
                        item.Modifiers.Add(mod);
                        pendingType = blockDefaultType;
                        pendingTier = string.Empty;
                    }
                }
            }

            // Delegate to specialized <itemType>Parser implementations
            foreach (var parser in TypeParsers)
            {
                if (parser.CanParse(item, headerLines, blocks))
                {
                    parser.Parse(item, headerLines, blocks);
                    break;
                }
            }

            PoePseudoStatsCalculator.CalculatePseudoStats(item);

            return item;
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
    }
}
