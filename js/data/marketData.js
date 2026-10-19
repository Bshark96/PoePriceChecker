// PoE 1 Market Listings Cache / Mock Data
var marketListings = {
  chambers: [
    {
      seller: "FastVaultRunner",
      character: "TempleFarmer_US",
      status: "online",
      age: "just now",
      goldVal: "450",
      priceVal: "2.5",
      priceType: "Divine",
      itemName: "Chambers of Inscription",
      itemBaseType: "Chronicle of Atzoatl",
      itemLevel: 83,
      isCorrupted: false,
      properties: ["Area Level: 83", "Room Tier: Tier III (Inscribed)"],
      requirements: ["Requires Quest: Incursion"],
      implicits: ["Area is at Monster Level 83"],
      explicits: [
        "Contains the Monolith of Sacrificial Inscription",
        "Sacrifice a Unique Item for another Random Unique of same Class",
        "Monsters in this Room have 25% increased Item Quantity",
        "Monsters in this Room deal 15% increased Damage"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Area is at Monster Level 83", magnitudes: "(83)" },
        { type: "explicit", tier: "T3", text: "Contains the Monolith of Sacrificial Inscription", isUnscalable: true },
        { type: "explicit", tier: "T3", text: "Sacrifice a Unique Item for another Random Unique of same Class", isUnscalable: true },
        { type: "explicit", tier: "P1", text: "Monsters in this Room have 25% increased Item Quantity", magnitudes: "(20–30)" },
        { type: "explicit", tier: "S1", text: "Monsters in this Room deal 15% increased Damage", magnitudes: "(10–20)" }
      ],
      highlights: ["Tier III", "Guatelitzi"],
      whisper: "@FastVaultRunner Hi, I would like to buy your Chambers of Inscription listed for 2.5 Divine in Settlers."
    },
    {
      seller: "AtzoatlCrafter",
      character: "Chronos_Master",
      status: "online",
      age: "12m ago",
      goldVal: "500",
      priceVal: "2.8",
      priceType: "Divine",
      itemName: "Chambers of Inscription",
      itemBaseType: "Chronicle of Atzoatl",
      itemLevel: 83,
      isCorrupted: false,
      properties: ["Area Level: 83", "Room Tier: Tier III (Inscribed)", "Locus of Corruption: Tier III"],
      implicits: ["Area is at Monster Level 83"],
      explicits: [
        "Contains the Monolith of Sacrificial Inscription",
        "Contains the Altar of Corruption",
        "Monsters in this Room have 28% increased Item Quantity"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Area is at Monster Level 83", magnitudes: "(83)" },
        { type: "explicit", tier: "T3", text: "Contains the Monolith of Sacrificial Inscription", isUnscalable: true },
        { type: "explicit", tier: "T3", text: "Contains the Altar of Corruption", isUnscalable: true },
        { type: "explicit", tier: "P1", text: "Monsters in this Room have 28% increased Item Quantity", magnitudes: "(20–30)" }
      ],
      highlights: ["Tier III", "Inscribed Altar"],
      whisper: "@AtzoatlCrafter Hi, I would like to buy your Chambers of Inscription listed for 2.8 Divine in Settlers."
    },
    {
      seller: "VaalOverlord",
      character: "SacrificeKing",
      status: "afk",
      age: "45m ago",
      goldVal: "400",
      priceVal: "380",
      priceType: "Chaos",
      itemName: "Chambers of Inscription",
      itemBaseType: "Chronicle of Atzoatl",
      itemLevel: 81,
      isCorrupted: false,
      properties: ["Area Level: 81", "Room Tier: Tier III (Inscribed)"],
      implicits: ["Area is at Monster Level 81"],
      explicits: [
        "Contains the Monolith of Sacrificial Inscription",
        "Monsters in this Room have 22% increased Item Quantity"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Area is at Monster Level 81", magnitudes: "(81)" },
        { type: "explicit", tier: "T3", text: "Contains the Monolith of Sacrificial Inscription", isUnscalable: true },
        { type: "explicit", tier: "P2", text: "Monsters in this Room have 22% increased Item Quantity", magnitudes: "(20–30)" }
      ],
      highlights: ["Tier III"],
      whisper: "@VaalOverlord Hi, I would like to buy your Chambers of Inscription listed for 380 Chaos in Settlers."
    }
  ],
  doryani: [
    {
      seller: "ArmorSmith_Oriath",
      character: "Jugg_Unbreakable",
      status: "online",
      age: "5m ago",
      goldVal: "650",
      priceVal: "1.0",
      priceType: "Divine",
      itemName: "Doryani's Prototype",
      itemBaseType: "Saint's Hauberk",
      itemLevel: 70,
      socketsSummary: "6S / 5L (B-B-R-R-R B)",
      isCorrupted: true,
      properties: ["Quality: +9%", "Armour: 1638", "Energy Shield: 280"],
      requirements: ["Requires Level 67, 109 Str, 94 Int"],
      implicits: ["+2 to Level of Socketed AoE Gems"],
      explicits: [
        "200% increased Armour and Energy Shield",
        "+65 to maximum Life",
        "Deal no Non-Lightning Damage",
        "Armour also applies to Lightning Damage taken from Hits",
        "Lightning Resistance does not affect Lightning Damage taken",
        "Nearby Enemies have Lightning Resistance equal to yours"
      ],
      modifiers: [
        { type: "implicit", tier: "CORRUPT", text: "+2 to Level of Socketed AoE Gems", magnitudes: "(2)" },
        { type: "explicit", tier: "UNI", text: "200% increased Armour and Energy Shield", magnitudes: "(150–200)" },
        { type: "explicit", tier: "UNI", text: "+65 to maximum Life", magnitudes: "(60–90)" },
        { type: "explicit", tier: "UNI", text: "Deal no Non-Lightning Damage", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Armour also applies to Lightning Damage taken from Hits", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Lightning Resistance does not affect Lightning Damage taken", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Nearby Enemies have Lightning Resistance equal to yours", isUnscalable: true }
      ],
      highlights: ["6 Sockets", "5 Links", "iLvl 70"],
      whisper: "@ArmorSmith_Oriath Hi, I would like to buy your Doryani's Prototype listed for 1.0 Divine in Settlers."
    },
    {
      seller: "NegativeRes_God",
      character: "LightningStrike_Beast",
      status: "online",
      age: "18m ago",
      goldVal: "700",
      priceVal: "1.2",
      priceType: "Divine",
      itemName: "Doryani's Prototype",
      itemBaseType: "Saint's Hauberk",
      itemLevel: 72,
      socketsSummary: "6S / 5L (B-B-B-R-R G)",
      isCorrupted: false,
      properties: ["Quality: +20%", "Armour: 1790", "Energy Shield: 306"],
      requirements: ["Requires Level 67, 109 Str, 94 Int"],
      enchant: ["Quality does not increase Defences, grants +1 to Maximum Life per 2% Quality"],
      explicits: [
        "198% increased Armour and Energy Shield",
        "+88 to maximum Life",
        "Deal no Non-Lightning Damage",
        "Armour also applies to Lightning Damage taken from Hits",
        "Lightning Resistance does not affect Lightning Damage taken",
        "Nearby Enemies have Lightning Resistance equal to yours"
      ],
      modifiers: [
        { type: "enchant", tier: "ENC", text: "Quality does not increase Defences, grants +1 to Maximum Life per 2% Quality" },
        { type: "explicit", tier: "UNI", text: "198% increased Armour and Energy Shield", magnitudes: "(150–200)" },
        { type: "explicit", tier: "UNI", text: "+88 to maximum Life", magnitudes: "(60–90)" },
        { type: "explicit", tier: "UNI", text: "Deal no Non-Lightning Damage", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Armour also applies to Lightning Damage taken from Hits", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Lightning Resistance does not affect Lightning Damage taken", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Nearby Enemies have Lightning Resistance equal to yours", isUnscalable: true }
      ],
      highlights: ["6 Sockets", "5 Links", "iLvl 72"],
      whisper: "@NegativeRes_God Hi, I would like to buy your Doryani's Prototype listed for 1.2 Divine in Settlers."
    },
    {
      seller: "HeistRunner_Speed",
      character: "ZoomZoom_Ranger",
      status: "online",
      age: "35m ago",
      goldVal: "600",
      priceVal: "160",
      priceType: "Chaos",
      itemName: "Doryani's Prototype",
      itemBaseType: "Saint's Hauberk",
      itemLevel: 70,
      socketsSummary: "6S / 4L (R-R-R-B B-G)",
      isCorrupted: false,
      properties: ["Quality: +14%", "Armour: 1610", "Energy Shield: 275"],
      requirements: ["Requires Level 67, 109 Str, 94 Int"],
      explicits: [
        "184% increased Armour and Energy Shield",
        "+72 to maximum Life",
        "Deal no Non-Lightning Damage",
        "Armour also applies to Lightning Damage taken from Hits",
        "Lightning Resistance does not affect Lightning Damage taken",
        "Nearby Enemies have Lightning Resistance equal to yours"
      ],
      modifiers: [
        { type: "explicit", tier: "UNI", text: "184% increased Armour and Energy Shield", magnitudes: "(150–200)" },
        { type: "explicit", tier: "UNI", text: "+72 to maximum Life", magnitudes: "(60–90)" },
        { type: "explicit", tier: "UNI", text: "Deal no Non-Lightning Damage", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Armour also applies to Lightning Damage taken from Hits", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Lightning Resistance does not affect Lightning Damage taken", isUnscalable: true },
        { type: "explicit", tier: "UNI", text: "Nearby Enemies have Lightning Resistance equal to yours", isUnscalable: true }
      ],
      highlights: ["6 Sockets", "iLvl 70"],
      whisper: "@HeistRunner_Speed Hi, I would like to buy your Doryani's Prototype listed for 160 Chaos in Settlers."
    }
  ],
  gem: [
    {
      seller: "GemMaster_EU",
      character: "Elementalist_Ignite",
      status: "online",
      age: "3m ago",
      goldVal: "350",
      priceVal: "1.8",
      priceType: "Divine",
      itemName: "Flameblast of Cinder",
      itemBaseType: "Transfigured Spell Gem",
      itemLevel: 84,
      isCorrupted: false,
      properties: ["Level: 20 (Max)", "Quality: +20%", "Cast Time: 0.20 sec", "Critical Strike Chance: 5.00%"],
      requirements: ["Requires Level 70, 155 Int"],
      implicits: ["Quality: +20% increased Burning Damage"],
      explicits: [
        "Deals 245 to 368 Fire Damage",
        "Expands Radius per Stage",
        "Deals 120% more Spell Damage for each stage",
        "90% more Damage with Ailments for each stage"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Quality: +20% increased Burning Damage" },
        { type: "explicit", tier: "GEM", text: "Deals 245 to 368 Fire Damage" },
        { type: "explicit", tier: "GEM", text: "Expands Radius per Stage", isUnscalable: true },
        { type: "explicit", tier: "GEM", text: "Deals 120% more Spell Damage for each stage" },
        { type: "explicit", tier: "GEM", text: "90% more Damage with Ailments for each stage" }
      ],
      highlights: ["Lvl 20", "Quality +20%"],
      whisper: "@GemMaster_EU Hi, I would like to buy your Flameblast of Cinder listed for 1.8 Divine in Settlers."
    },
    {
      seller: "TransfiguredGems",
      character: "LabFarmer_Grand",
      status: "online",
      age: "14m ago",
      goldVal: "380",
      priceVal: "2.0",
      priceType: "Divine",
      itemName: "Flameblast of Cinder",
      itemBaseType: "Transfigured Spell Gem",
      itemLevel: 85,
      isCorrupted: true,
      properties: ["Level: 20 (Max)", "Quality: +20%", "Cast Time: 0.20 sec"],
      requirements: ["Requires Level 70, 155 Int"],
      implicits: ["Quality: +20% increased Burning Damage"],
      explicits: [
        "Deals 245 to 368 Fire Damage",
        "Expands Radius per Stage",
        "Deals 120% more Spell Damage for each stage"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Quality: +20% increased Burning Damage" },
        { type: "explicit", tier: "GEM", text: "Deals 245 to 368 Fire Damage" },
        { type: "explicit", tier: "GEM", text: "Expands Radius per Stage", isUnscalable: true },
        { type: "explicit", tier: "GEM", text: "Deals 120% more Spell Damage for each stage" }
      ],
      highlights: ["Lvl 20", "Quality +20%"],
      whisper: "@TransfiguredGems Hi, I would like to buy your Flameblast of Cinder listed for 2 Divine in Settlers."
    },
    {
      seller: "Spellcaster_PRO",
      character: "WitchIgnite",
      status: "afk",
      age: "1h ago",
      goldVal: "300",
      priceVal: "300",
      priceType: "Chaos",
      itemName: "Flameblast of Cinder",
      itemBaseType: "Transfigured Spell Gem",
      itemLevel: 82,
      isCorrupted: false,
      properties: ["Level: 20 (Max)", "Quality: +18%", "Cast Time: 0.20 sec"],
      requirements: ["Requires Level 70, 155 Int"],
      implicits: ["Quality: +18% increased Burning Damage"],
      explicits: [
        "Deals 245 to 368 Fire Damage",
        "Expands Radius per Stage",
        "Deals 120% more Spell Damage for each stage"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "Quality: +18% increased Burning Damage" },
        { type: "explicit", tier: "GEM", text: "Deals 245 to 368 Fire Damage" },
        { type: "explicit", tier: "GEM", text: "Expands Radius per Stage", isUnscalable: true },
        { type: "explicit", tier: "GEM", text: "Deals 120% more Spell Damage for each stage" }
      ],
      highlights: ["Lvl 20", "Quality +18%"],
      whisper: "@Spellcaster_PRO Hi, I would like to buy your Flameblast of Cinder listed for 300 Chaos in Settlers."
    }
  ],
  rare: [
    {
      seller: "SwordCrafter_US",
      character: "PhysDPS_Beast_SlayerOverlord",
      status: "online",
      age: "1m ago",
      goldVal: "850",
      priceVal: "5.0",
      priceType: "Divine",
      itemName: "Demon Slayer Sword",
      itemBaseType: "Exquisite Blade",
      itemLevel: 85,
      socketsSummary: "6S / 6L (R-R-R-R-G-B)",
      totalDps: 840.5,
      physicalDps: 840.5,
      elementalDps: 0,
      isCorrupted: false,
      properties: ["Two Handed Sword", "Quality: +20%", "Physical Damage: 390-650", "Critical Strike Chance: 6.80%", "Attacks per Second: 1.62"],
      requirements: ["Requires Level 70, 119 Str, 131 Dex"],
      implicits: ["60% increased Global Accuracy Rating"],
      fractured: ["179% increased Physical Damage"],
      explicits: [
        "Adds 34 to 68 Physical Damage",
        "26% increased Attack Speed",
        "+320 to Accuracy Rating"
      ],
      crafted: ["+28% to Global Critical Strike Multiplier"],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "60% increased Global Accuracy Rating" },
        { type: "fractured", tier: "FRAC", text: "179% increased Physical Damage", magnitudes: "(170–179)" },
        { type: "explicit", tier: "P1", text: "Adds 34 to 68 Physical Damage", magnitudes: "(32–42 to 65–76)" },
        { type: "explicit", tier: "S1", text: "26% increased Attack Speed", magnitudes: "(25–27)" },
        { type: "explicit", tier: "S2", text: "+320 to Accuracy Rating", magnitudes: "(250–350)" },
        { type: "crafted", tier: "CRAFT", text: "+28% to Global Critical Strike Multiplier", magnitudes: "(25–30)" }
      ],
      highlights: ["840 pDPS", "iLvl 85", "18% Atk Speed"],
      whisper: "@SwordCrafter_US Hi, I would like to buy your Demon Slayer Sword listed for 5 Divine in Settlers."
    },
    {
      seller: "BladeMaster_EU",
      character: "Slayer_Spin2Win",
      status: "online",
      age: "10m ago",
      goldVal: "900",
      priceVal: "5.2",
      priceType: "Divine",
      itemName: "Demon Slayer Sword",
      itemBaseType: "Exquisite Blade",
      itemLevel: 85,
      socketsSummary: "6S / 6L (R-R-R-G-G-R)",
      totalDps: 835.2,
      physicalDps: 835.2,
      elementalDps: 0,
      isCorrupted: false,
      properties: ["Two Handed Sword", "Quality: +20%", "Physical Damage: 385-645", "Critical Strike Chance: 6.70%", "Attacks per Second: 1.62"],
      requirements: ["Requires Level 70, 119 Str, 131 Dex"],
      implicits: ["+50% to Global Critical Strike Multiplier"],
      fractured: ["174% increased Physical Damage"],
      explicits: [
        "Adds 36 to 66 Physical Damage",
        "25% increased Attack Speed",
        "Hits can't be Evaded"
      ],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "+50% to Global Critical Strike Multiplier" },
        { type: "fractured", tier: "FRAC", text: "174% increased Physical Damage", magnitudes: "(170–179)" },
        { type: "explicit", tier: "P1", text: "Adds 36 to 66 Physical Damage", magnitudes: "(32–42 to 65–76)" },
        { type: "explicit", tier: "S1", text: "25% increased Attack Speed", magnitudes: "(25–27)" },
        { type: "crafted", tier: "CRAFT", text: "Hits can't be Evaded", isUnscalable: true }
      ],
      highlights: ["835 pDPS", "iLvl 85", "6 Sockets"],
      whisper: "@BladeMaster_EU Hi, I would like to buy your Demon Slayer Sword listed for 5.2 Divine in Settlers."
    },
    {
      seller: "RareHunter",
      character: "Cyclone_God",
      status: "online",
      age: "22m ago",
      goldVal: "720",
      priceVal: "750",
      priceType: "Chaos",
      itemName: "Demon Slayer Sword",
      itemBaseType: "Vaal Greatsword",
      itemLevel: 84,
      socketsSummary: "6S / 5L (R-R-R-R-G B)",
      totalDps: 820.0,
      physicalDps: 820.0,
      elementalDps: 0,
      isCorrupted: false,
      properties: ["Two Handed Sword", "Quality: +20%", "Physical Damage: 380-635", "Attacks per Second: 1.61"],
      requirements: ["Requires Level 68, 122 Str, 104 Dex"],
      implicits: ["+470 to Accuracy Rating"],
      explicits: [
        "168% increased Physical Damage",
        "Adds 31 to 64 Physical Damage",
        "24% increased Attack Speed"
      ],
      crafted: ["+24% to Global Critical Strike Multiplier"],
      modifiers: [
        { type: "implicit", tier: "IMP", text: "+470 to Accuracy Rating" },
        { type: "explicit", tier: "P1", text: "168% increased Physical Damage", magnitudes: "(155–169)" },
        { type: "explicit", tier: "P2", text: "Adds 31 to 64 Physical Damage" },
        { type: "explicit", tier: "S1", text: "24% increased Attack Speed" },
        { type: "crafted", tier: "CRAFT", text: "+24% to Global Critical Strike Multiplier" }
      ],
      highlights: ["820 pDPS", "iLvl 84", "Quality +20%"],
      whisper: "@RareHunter Hi, I would like to buy your Demon Slayer Sword listed for 750 Chaos in Settlers."
    },
    {
      seller: "CraftingBench",
      character: "TwoHander_King",
      status: "afk",
      age: "55m ago",
      goldVal: "950",
      priceVal: "5.8",
      priceType: "Divine",
      itemName: "Demon Slayer Sword",
      itemBaseType: "Exquisite Blade",
      itemLevel: 86,
      socketsSummary: "6S / 6L (R-R-R-G-G-W)",
      totalDps: 850.4,
      physicalDps: 850.4,
      elementalDps: 0,
      isCorrupted: false,
      properties: ["Two Handed Sword", "Quality: +30%", "Physical Damage: 400-662", "Critical Strike Chance: 7.00%", "Attacks per Second: 1.60"],
      requirements: ["Requires Level 70, 119 Str, 131 Dex"],
      enchant: ["8% increased Explicit Physical Modifier magnitudes"],
      implicits: ["+50% to Global Critical Strike Multiplier"],
      fractured: ["179% increased Physical Damage"],
      explicits: [
        "Adds 38 to 72 Physical Damage",
        "27% increased Attack Speed"
      ],
      crafted: ["+38% to Critical Strike Chance, +25% to Quality"],
      modifiers: [
        { type: "enchant", tier: "ENC", text: "8% increased Explicit Physical Modifier magnitudes" },
        { type: "implicit", tier: "IMP", text: "+50% to Global Critical Strike Multiplier" },
        { type: "fractured", tier: "FRAC", text: "179% increased Physical Damage", magnitudes: "(170–179)" },
        { type: "explicit", tier: "P1", text: "Adds 38 to 72 Physical Damage", magnitudes: "(32–42 to 65–76)" },
        { type: "explicit", tier: "S1", text: "27% increased Attack Speed", magnitudes: "(25–27)" },
        { type: "crafted", tier: "CRAFT", text: "+38% to Critical Strike Chance, +25% to Quality" }
      ],
      highlights: ["850 pDPS", "iLvl 86"],
      whisper: "@CraftingBench Hi, I would like to buy your Demon Slayer Sword listed for 5.8 Divine in Settlers."
    }
  ]
};
