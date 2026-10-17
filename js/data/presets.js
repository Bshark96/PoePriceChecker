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
      { label: "Room Tier:", val: "Tier III (Inscribed)", min: "3", max: "3" },
      { label: "Architect:", val: "Guatelitzi, Architect of Flesh", min: "", max: "" },
      { label: "Inscribed Altar:", val: "Active", min: "1", max: "" },
      { label: "Item Quantity:", val: "+25%", min: "25", max: "" }
    ],
    mods: [
      { roll: "T3", text: "CONTAINS THE MONOLITH OF SACRIFICIAL INSCRIPTION", min: "null", max: "" },
      { roll: "100%", text: "SACRIFICE A UNIQUE ITEM FOR ANOTHER RANDOM UNIQUE OF SAME CLASS", min: "null", max: "" },
      { roll: "+25%", text: "MONSTERS IN THIS ROOM HAVE 25% INCREASED ITEM QUANTITY", min: "25", max: "" },
      { roll: "+15%", text: "MONSTERS IN THIS ROOM DEAL 15% INCREASED DAMAGE", min: "15", max: "" }
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
      { label: "Item Level:", val: "70", min: "70", max: "67" },
      { label: "Sockets:", val: "6", min: "6", max: "" },
      { label: "Links:", val: "5", min: "5", max: "" },
      { label: "Quality:", val: "+9%", min: "9", max: "" },
      { label: "Armour:", val: "1638", min: "1638", max: "" },
      { label: "Energy Shield:", val: "280", min: "280", max: "" }
    ],
    mods: [
      { roll: "100%", text: "200% INCREASED ARMOUR AND ENERGY SHIELD", min: "196", max: "" },
      { roll: "17%", text: "65 TO MAXIMUM LIFE", min: "65", max: "" },
      { roll: "", text: "DEAL NO NON-LIGHTNING DAMAGE", min: "null", max: "" },
      { roll: "", text: "ARMOUR ALSO APPLIES TO LIGHTNING DAMAGE TAKEN FROM HITS", min: "null", max: "" },
      { roll: "", text: "LIGHTNING RESISTANCE DOES NOT AFFECT LIGHTNING DAMAGE TAKEN", min: "null", max: "" },
      { roll: "", text: "NEARBY ENEMIES HAVE LIGHTNING RESISTANCE EQUAL TO YOURS", min: "null", max: "" }
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
      { label: "Gem Level:", val: "20", min: "20", max: "20" },
      { label: "Quality:", val: "+20%", min: "20", max: "" }
    ],
    mods: [
      { roll: "100%", text: "EXPANDS RADIUS PER STAGE", min: "null", max: "" },
      { roll: "100%", text: "DEALS 120% MORE DAMAGE PER STAGE", min: "null", max: "" }
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
      { label: "Item Level:", val: "85", min: "85", max: "" },
      { label: "Sockets:", val: "6", min: "6", max: "" },
      { label: "Quality:", val: "+20%", min: "20", max: "" },
      { label: "Physical DPS:", val: "840", min: "800", max: "" }
    ],
    mods: [
      { roll: "92%", text: "165% INCREASED PHYSICAL DAMAGE", min: "160", max: "" },
      { roll: "88%", text: "18% INCREASED ATTACK SPEED", min: "17", max: "" }
    ]
  }
};

// Backward-compatibility aliases
dataPresets.chambers = dataPresets.theme1;
dataPresets.doryani = dataPresets.theme2;
dataPresets.gem = dataPresets.theme3;
dataPresets.rare = dataPresets.theme4;
