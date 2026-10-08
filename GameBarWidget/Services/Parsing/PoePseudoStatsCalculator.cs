using System;
using System.Globalization;

namespace GameBarWidget.Services
{
    public static class PoePseudoStatsCalculator
    {
        public static void CalculatePseudoStats(PoeItem item)
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

                var am = PoeModifierParser.AddsDamageRegex.Match(mod.RawText);
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
            val = Math.Abs(val);
            min = Math.Abs(min);
            max = Math.Abs(max);
            if (min > max) { double tmp = min; min = max; max = tmp; }
            if (val == 0) return;
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
    }
}
