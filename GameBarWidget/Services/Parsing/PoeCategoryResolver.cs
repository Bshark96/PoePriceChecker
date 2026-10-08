using System;

namespace GameBarWidget.Services
{
    public static class PoeCategoryResolver
    {
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

        public static void ResolveItemNamespaceAndCategory(PoeItem item)
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
    }
}
