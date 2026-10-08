export type PoeRarity =
  | "Normal"
  | "Magic"
  | "Rare"
  | "Unique"
  | "Gem"
  | "Currency"
  | "DivinationCard";

export type ModifierType =
  | "Explicit"
  | "Implicit"
  | "Fractured"
  | "Crafted"
  | "Enchant"
  | "Pseudo";

export interface ItemModifier {
  id: string;
  type: ModifierType;
  statId: string;
  rawText: string;
  cleanText: string;
  numberValue?: number;
  minRoll?: number;
  maxRoll?: number;
  tierInfo?: string;
  isPrefix?: boolean;
  isSuffix?: boolean;
  isLocal?: boolean;
  isActive: boolean;
  isPseudo?: boolean;
  isUnscalable?: boolean;
}

export interface PoeItem {
  rawText: string;
  itemClass: string;
  rarity: PoeRarity;
  name: string;
  baseType: string;
  itemLevel: number;
  quality: number;
  gemLevel: number;
  mapTier: number;

  isCorrupted: boolean;
  isMirrored: boolean;
  isUnidentified: boolean;
  isSynthesised: boolean;
  isFractured: boolean;

  // Influences
  isShaper: boolean;
  isElder: boolean;
  isCrusader: boolean;
  isRedeemer: boolean;
  isHunter: boolean;
  isWarlord: boolean;

  // Sockets & links
  socketRaw: string;
  socketCount: number;
  linkCount: number;

  // Weapon Stats
  attacksPerSecond: number;
  critChance: number;
  physDamageMin: number;
  physDamageMax: number;
  eleDamageMin: number;
  eleDamageMax: number;
  physicalDps: number;
  elementalDps: number;
  totalDps: number;

  // Defences
  armour: number;
  evasion: number;
  energyShield: number;

  // Calculated Pseudos
  pseudoTotalLife: number;
  pseudoTotalEleRes: number;

  // Modifiers
  modifiers: ItemModifier[];
}

export interface BenchmarkResult {
  found: boolean;
  chaosEquivalent: number;
  divineEquivalent: number;
  source: string;
  confidence: "High" | "Medium" | "Low";
  lastUpdated: string;
}

export interface TradeListing {
  id: string;
  accountName: string;
  characterName: string;
  status: "online" | "afk" | "offline";
  priceAmount: number;
  priceCurrency: string;
  priceInChaos: number;
  priceInDivine: number;
  age: string;
  whisper: string;
  itemLevel?: number;
  sockets?: string;
  corrupted?: boolean;
  note?: string;
}

export interface TradeSearchResult {
  success: boolean;
  queryId?: string;
  searchUrl?: string;
  totalListings: number;
  minPriceChaos: number;
  medianPriceChaos: number;
  divineRate: number;
  listings: TradeListing[];
  rateLimitStatus?: string;
  errorReason?: string;
}
