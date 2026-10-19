// Unified PoE 1 UI Themes
// theme1: Amber Gold (Inscribed / Temple / Vaal)
// theme2: Deep Copper (Unique Gear / Legendary)
// theme3: Cyan Teal (Gems / Transfigured / Skill)
// theme4: Golden Yellow (Rare Gear / Crafted)

var dataPresets = {
  theme1: {
    key: "theme1",
    name: "CHAMBERS OF INSCRIPTION",
    base: "TEMPLE OF ATZOATL - TIER 3",
    bannerBg: "linear-gradient(180deg, #1d0f04 0%, #2e1806 45%, #201004 75%, #0f0701 100%)",
    bannerBorder: "#a86d26",
    nameColor: "#e6a243",
    endcapFilter: "sepia(100%) hue-rotate(5deg) saturate(320%) brightness(0.9) contrast(1.15)",
    gemFilter: "sepia(100%) hue-rotate(85deg) saturate(450%) brightness(1.05) contrast(1.15)",
    ninja: "Room Value: 2.5 -> 3.8 Divine",
    props: [
      { label: "Room Tier:", val: "Tier III (Inscribed)", min: "3", max: "3", active: true },
      { label: "Architect:", val: "Guatelitzi, Architect of Flesh", min: "", max: "", active: false },
      { label: "Inscribed Altar:", val: "Active", min: "1", max: "", active: true },
      { label: "Item Quantity:", val: "+25%", min: "22", max: "", active: false }
    ],
    mods: [
      { type: "implicit", roll: "IMP", text: "AREA IS AT MONSTER LEVEL 83", min: "74", max: "", active: false },
      { type: "explicit", roll: "T3", text: "CONTAINS THE MONOLITH OF SACRIFICIAL INSCRIPTION", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "100%", text: "SACRIFICE A UNIQUE ITEM FOR ANOTHER RANDOM UNIQUE OF SAME CLASS", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "+25%", text: "MONSTERS IN THIS ROOM HAVE 25% INCREASED ITEM QUANTITY", min: "22", max: "", active: false },
      { type: "explicit", roll: "+15%", text: "MONSTERS IN THIS ROOM DEAL 15% INCREASED DAMAGE", min: "13", max: "", active: false }
    ]
  },
  theme2: {
    key: "theme2",
    name: "DORYANI'S PROTOTYPE",
    base: "SAINT'S HAUBERK",
    bannerBg: "linear-gradient(180deg, #180903 0%, #290f05 45%, #1c0a03 75%, #0c0401 100%)",
    bannerBorder: "#a34918",
    nameColor: "#d86a22",
    endcapFilter: "sepia(100%) hue-rotate(-25deg) saturate(450%) brightness(0.75) contrast(1.2)",
    gemFilter: "sepia(100%) hue-rotate(-25deg) saturate(450%) brightness(0.85) contrast(1.2)",
    ninja: "1 -> 10 Divine (-44.9%)",
    props: [
      { label: "Item Level:", val: "70", min: "70", max: "", active: false },
      { label: "Sockets:", val: "6", min: "6", max: "", active: true },
      { label: "Links:", val: "5", min: "5", max: "", active: true },
      { label: "Quality:", val: "+9%", min: "8", max: "", active: false },
      { label: "Armour:", val: "1638", min: "1474", max: "", active: false },
      { label: "Energy Shield:", val: "280", min: "252", max: "", active: false }
    ],
    mods: [
      { type: "implicit", roll: "IMP", text: "+2 TO LEVEL OF SOCKETED AOE GEMS", min: "1", max: "", active: false },
      { type: "explicit", roll: "100%", text: "200% INCREASED ARMOUR AND ENERGY SHIELD", min: "180", max: "", active: false },
      { type: "explicit", roll: "17%", text: "+65 TO MAXIMUM LIFE", min: "58", max: "", active: false },
      { type: "explicit", roll: "UNI", text: "DEAL NO NON-LIGHTNING DAMAGE", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "UNI", text: "ARMOUR ALSO APPLIES TO LIGHTNING DAMAGE TAKEN FROM HITS", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "UNI", text: "LIGHTNING RESISTANCE DOES NOT AFFECT LIGHTNING DAMAGE TAKEN", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "UNI", text: "NEARBY ENEMIES HAVE LIGHTNING RESISTANCE EQUAL TO YOURS", min: "null", max: "", isUnscalable: true, active: false }
    ]
  },
  theme3: {
    key: "theme3",
    name: "FLAMEBLAST OF CINDER",
    base: "TRANSFIGURED GEM",
    bannerBg: "linear-gradient(180deg, #041416 0%, #092225 45%, #051618 75%, #020b0d 100%)",
    bannerBorder: "#198c86",
    nameColor: "#22baba",
    endcapFilter: "sepia(100%) hue-rotate(135deg) saturate(380%) brightness(0.85) contrast(1.15)",
    gemFilter: "grayscale(100%) brightness(1.75) contrast(1.25)",
    ninja: "1.8 Divine (approx 270 C)",
    props: [
      { label: "Level:", val: "20", min: "20", max: "", active: true },
      { label: "Quality:", val: "+20%", min: "20", max: "", active: true }
    ],
    mods: [
      { type: "implicit", roll: "IMP", text: "QUALITY: +20% INCREASED BURNING DAMAGE", min: "18", max: "", active: false },
      { type: "explicit", roll: "100%", text: "EXPANDS RADIUS PER STAGE", min: "null", max: "", isUnscalable: true, active: false },
      { type: "explicit", roll: "100%", text: "DEALS 120% MORE DAMAGE PER STAGE", min: "108", max: "", active: false }
    ]
  },
  theme4: {
    key: "theme4",
    name: "DEMON SLAYER SWORD",
    base: "TWO HANDED SWORD",
    bannerBg: "linear-gradient(180deg, #1b1704 0%, #292407 45%, #1c1804 75%, #0e0c01 100%)",
    bannerBorder: "#a89022",
    nameColor: "#e0c548",
    endcapFilter: "sepia(100%) hue-rotate(15deg) saturate(350%) brightness(1.0) contrast(1.15)",
    gemFilter: "sepia(100%) hue-rotate(150deg) saturate(420%) brightness(1.05) contrast(1.15)",
    ninja: "5.0 Divine (approx 750 C)",
    props: [
      { label: "Item Level:", val: "85", min: "85", max: "", active: false },
      { label: "Sockets:", val: "6", min: "6", max: "", active: true },
      { label: "Links:", val: "6", min: "6", max: "", active: true },
      { label: "Quality:", val: "+20%", min: "18", max: "", active: false },
      { label: "Total DPS:", val: "840.0", min: "756", max: "", active: false }
    ],
    mods: [
      { type: "implicit", roll: "IMP", text: "60% INCREASED GLOBAL ACCURACY RATING", min: "54", max: "", active: false },
      { type: "fractured", roll: "FRAC", text: "179% INCREASED PHYSICAL DAMAGE", min: "161", max: "", active: false },
      { type: "explicit", roll: "P1", text: "ADDS 34 TO 68 PHYSICAL DAMAGE", min: "45.9", max: "", active: false },
      { type: "explicit", roll: "S1", text: "26% INCREASED ATTACK SPEED", min: "23", max: "", active: false },
      { type: "crafted", roll: "CRAFT", text: "+28% TO GLOBAL CRITICAL STRIKE MULTIPLIER", min: "25", max: "", active: false }
    ]
  }
};

// Backward-compatibility aliases
dataPresets.chambers = dataPresets.theme1;
dataPresets.doryani = dataPresets.theme2;
dataPresets.gem = dataPresets.theme3;
dataPresets.rare = dataPresets.theme4;
