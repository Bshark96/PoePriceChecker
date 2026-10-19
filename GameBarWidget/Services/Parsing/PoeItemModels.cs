using System;
using System.Collections.Generic;
using System.Globalization;

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

        public string Text
        {
            get => !string.IsNullOrEmpty(RawText) ? RawText : DisplayText;
            set => RawText = value ?? string.Empty;
        }

        public double Value
        {
            get => NumberValue ?? 0.0;
            set => NumberValue = value;
        }

        public string Tier
        {
            get
            {
                if (string.IsNullOrEmpty(TierInfo)) return null;
                string digits = System.Text.RegularExpressions.Regex.Match(TierInfo, @"\d+").Value;
                return !string.IsNullOrEmpty(digits) ? digits : TierInfo;
            }
            set => TierInfo = value ?? string.Empty;
        }

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
        public bool FilterBaseTypeActive { get; set; } = true;
        public bool IsJewelCraftingBase { get; set; } = false;
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

        public int? FilterGemLevelMin { get; set; }
        public int? FilterGemLevelMax { get; set; }
        public bool FilterGemLevelActive { get; set; } = false;

        public int? FilterMapTierMin { get; set; }
        public int? FilterMapTierMax { get; set; }
        public bool FilterMapTierActive { get; set; } = false;

        public int? FilterItemLevelMin { get; set; }
        public int? FilterItemLevelMax { get; set; }
        public bool FilterItemLevelActive { get; set; } = false;

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

        private Dictionary<string, object> _properties;
        public Dictionary<string, object> Properties
        {
            get
            {
                if (_properties != null && _properties.Count > 0)
                    return _properties;

                var dict = new Dictionary<string, object>();
                if (ItemLevel > 0) dict["Item Level"] = ItemLevel;
                if (SocketCount > 0) dict["Sockets"] = SocketCount;
                if (LinkCount > 0) dict["Links"] = LinkCount;
                if (Quality > 0) dict["Quality"] = $"+{Quality}%";
                if (PhysDamageMin > 0 || PhysDamageMax > 0) dict["Physical Damage"] = $"{PhysDamageMin}-{PhysDamageMax}";
                if (EleDamageMin > 0 || EleDamageMax > 0) dict["Elemental Damage"] = $"{EleDamageMin}-{EleDamageMax}";
                if (CritChance > 0) dict["Critical Strike Chance"] = $"{CritChance.ToString("0.00", CultureInfo.InvariantCulture)}%";
                if (AttacksPerSecond > 0) dict["Attacks per Second"] = AttacksPerSecond.ToString("0.00", CultureInfo.InvariantCulture);
                if (TotalDps > 0) dict["Total DPS"] = TotalDps.ToString("0.0", CultureInfo.InvariantCulture);
                if (Armour > 0) dict["Armour"] = Armour;
                if (Evasion > 0) dict["Evasion Rating"] = Evasion;
                if (EnergyShield > 0) dict["Energy Shield"] = EnergyShield;
                if (MapTier > 0) dict["Map Tier"] = MapTier;
                if (GemLevel > 0) dict["Level"] = GemLevel;

                return dict;
            }
            set => _properties = value;
        }

        public List<ItemModifier> ExplicitMods
        {
            get
            {
                if (Modifiers == null) return new List<ItemModifier>();
                var result = new List<ItemModifier>();
                foreach (var mod in Modifiers)
                {
                    if (mod != null && !mod.IsPseudo && mod.Type != ModifierType.Pseudo)
                    {
                        result.Add(mod);
                    }
                }
                return result;
            }
            set
            {
                if (value != null)
                {
                    if (Modifiers == null) Modifiers = new List<ItemModifier>();
                    Modifiers.AddRange(value);
                }
            }
        }
    }
}
