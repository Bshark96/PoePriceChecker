# Path of Exile 1 Widget - Web & C# Bridge Data Reference

This document details all supported data structures, properties, field types, and default fallbacks passed between the C# backend (`GameBarWidget`) and the Web front-end (`WebView2`).

---

## Table of Contents
1. [Overview & Communication Flow](#overview--communication-flow)
2. [View 1: Price Check & Evaluation (`evaluate`)](#view-1-price-check--evaluation-evaluate)
   - [Item Data Payload (`onItemLoaded`)](#item-data-payload-onitemloaded)
   - [Trade Results Data Payload (`onTradeResultsLoaded` / `renderTradeResultsData`)](#trade-results-data-payload-ontraderesultsloaded--rendertraderesultsdata)
   - [Trade Listing Object Fields & Renderer Mapping](#trade-listing-object-fields--renderer-mapping)
3. [View 2: Live Search Feed (`live`)](#view-2-live-search-feed-live)
   - [Live Search Result Item Payload (`onLiveSearchResult`)](#live-search-result-item-payload-onlivesearchresult)
   - [Monitored Search Query Model](#monitored-search-query-model)
4. [View 3: Alarm Mode (`alarm`)](#view-3-alarm-mode-alarm)
   - [Alarm Alert Notification Payload](#alarm-alert-notification-payload)
   - [Alarm Rule Model](#alarm-rule-model)
5. [System & Global Handlers](#system--global-handlers)
   - [Auto-Minimize Countdown Timer (`onTimerTick`)](#auto-minimize-countdown-timer-ontimertick)
   - [API Rate Limiter Status (`onRateLimitUpdated`)](#api-rate-limiter-status-onratelimitupdated)
6. [Web to C# Bridge Actions (`window.PoeBridge.notifyHost`)](#web-to-c-bridge-actions-windowpoebridgenotifyhost)

---

## 1. Overview & Communication Flow

* **C# to Web**: Invoked via `CoreWebView2.ExecuteScriptAsync(...)` in `GameBarWidget/Services/PoeWebBridgeService.cs`. Global JavaScript hooks are registered on `window.PoeBridge`.
* **Web to C#**: Transmitted by calling `window.chrome.webview.postMessage(JSON.stringify(payload))` or `window.external.notify(JSON.stringify(payload))` through `window.PoeBridge.notifyHost(payload)`.

---

## 2. View 1: Price Check & Evaluation (`evaluate`)

### Item Data Payload (`onItemLoaded`)

* **C# Method**: `PoeWebBridgeService.SendItemToWebAsync(PoeItem item)`
* **JS Receiver**: `window.PoeBridge.onItemLoaded(item)` / `window.loadPoe1Item(item)`
* **JS Handler**: `window.itemController.loadCustomItem(itemData)`
* **Domain Model**: `js/models/ItemModel.js`

#### Payload Structure
```json
{
  "name": "Doryani's Prototype",
  "base": "Saint's Hauberk",
  "rarityKey": "theme2",
  "rarity": "Unique",
  "itemLevel": 84,
  "isCorrupted": false,
  "ninjaPrice": "1.0 - 1.2 Divine",
  "properties": [
    {
      "label": "Quality:",
      "val": "+20%"
    },
    {
      "label": "Armour:",
      "val": "1638"
    }
  ],
  "modifiers": [
    {
      "text": "Armour also applies to Lightning Damage taken from Hits",
      "min": "0",
      "max": "0",
      "roll": "",
      "isUnscalable": true
    },
    {
      "text": "Nearby Enemies have Lightning Resistance equal to yours",
      "min": "0",
      "max": "0",
      "roll": "T1",
      "isUnscalable": false
    }
  ]
}
```

#### Fields Description
| Field | Type | Default | Description |
|---|---|---|---|
| `name` | string | `'Unknown Item'` | Item explicit name. |
| `base` | string | `''` | Item base type (e.g. `Saint's Hauberk`). |
| `rarityKey` | string | `'theme4'` | Theme identifier for banner styling: `'theme1'` (Amber Gold), `'theme2'` (Deep Copper), `'theme3'` (Cyan Teal), `'theme4'` (Golden Yellow). |
| `rarity` | string | `'Rare'` | PoE rarity string (`'Normal'`, `'Magic'`, `'Rare'`, `'Unique'`, `'Currency'`, `'Gem'`, `'DivinationCard'`). |
| `itemLevel` | number | `84` | Item level (`ilvl`). |
| `isCorrupted` | boolean | `false` | True if the item is corrupted. |
| `ninjaPrice` | string | `''` | Benchmark price summary string (e.g. from poe.ninja). |
| `properties` | array | `[]` | Array of objects `{ label: string, val: string }`. |
| `modifiers` | array | `[]` | Array of modifier objects (see sub-fields below). |

##### Modifier Object Sub-Fields
| Sub-Field | Type | Default | Description |
|---|---|---|---|
| `text` | string | `''` | Clean modifier description text (stripped of raw `— Unscalable Value` or tag annotations). |
| `min` | string | `''` | Current or minimum roll value. For unscalable mods, equals `max`. |
| `max` | string | `''` | Current or maximum roll value. For unscalable mods, equals `min`. |
| `roll` | string | `''` | Tier indicator string (e.g. `'T1'`, `'T2'`, or percentage roll). |
| `isUnscalable` | boolean | `false` | Flag indicating the modifier has fixed, non-scalable values that cannot be rerolled or scaled with catalysts. Web UI can use this to render an `[Unscalable]` badge or lock/disable the `.poe-input` boxes. |

---

### Trade Results Data Payload (`onTradeResultsLoaded` / `renderTradeResultsData`)

* **C# Method**: `PoeWebBridgeService.SendTradeResultsToWebAsync(TradeSearchResult searchResult)`
* **JS Receiver**: `window.PoeBridge.onTradeResultsLoaded(data)` / `window.loadPoe1TradeResults(data)`
* **JS Handler**: `window.tradeController.renderTradeResultsData(data)`
* **Target Container**: `#resultsList` in `#viewEvaluatePanel`

#### Payload Structure
```json
{
  "totalCount": 18,
  "summary": "1.0 - 1.2 div (18 offers)",
  "listings": [
    {
      "seller": "FastExile_Trade",
      "character": "FastVaultRunner",
      "priceVal": "1.0",
      "priceType": "Divine",
      "status": "online",
      "age": "JUST NOW",
      "goldVal": 450,
      "itemName": "Doryani's Prototype",
      "highlights": ["6 Sockets", "5 Links", "iLvl 84"]
    }
  ]
}
```

#### Root Fields Description
| Field | Type | Default | Description |
|---|---|---|---|
| `totalCount` | number | `0` | Total number of listings found on official trade API. Displayed in `#resultsCount`. |
| `summary` | string | `''` | Human-readable price range summary (e.g. `1.0 - 1.2 div (18 offers)`). |
| `listings` | array | `[]` | Array of trade listing objects. |

---

### Trade Listing Object Fields & Renderer Mapping

Instantiated via `new TradeListingModel(item)` and passed into `TradeCardRenderer.prototype.createCardElement(listing, preset)`.

| Field | Type | Default | HTML / DOM Element | Description |
|---|---|---|---|---|
| `seller` | string | `'Exile'` | `.seller-name` (`@seller`), `data-seller` attribute on buttons | Seller PoE account name. Used for `@seller` whisper and `/hideout seller`. |
| `character` (or `char` / `ign`) | string | `'Unknown'` | `.seller-char` (`ign: character`), `data-char` attribute | In-game character name (`lastCharacterName` or extracted from `@<CharName>` whisper). Also aliased as `char` and `ign` for total compatibility. |
| `priceVal` | string | `'1'` | `.price-amount`, `data-price` attribute | Numerical price value (e.g. `'2.5'`). |
| `priceType` | string | `'Divine'` | `.currency-badge`, `data-currency` attribute | Currency denomination: `'Divine'`, `'Chaos'`, `'Mirror'`, etc. Controls card color theme (`trade-card-divine` vs `trade-card-chaos`). |
| `status` | string | `'online'` | `.status-dot` (`online`, `afk`, `offline`) | Seller online presence state. |
| `age` | string | `'RECENT'` | `.listing-age` (`• JUST NOW`, `• 5m ago`) | Time elapsed since item was indexed. |
| `goldVal` | number | `450` | `.btn-gold-val` (`(450)`) | Faustus instant currency exchange / settlement gold fee. |
| `itemName` | string | `''` | `.item-name-title` | Title of the specific item listing. |
| `highlights` | array of strings | `[]` | `.trade-highlight-badge` | Badges displayed next to item title (e.g. `Tier III`, `6 Sockets`). |

#### Available Methods on `TradeListingModel`
* `listing.getNormalizedPriceInChaos(divineRatio)`: Converts divine values to chaos equivalent based on current ratio.
* `listing.getWhisperMessage(league)`: Generates full whisper message string.
* `listing.getHideoutCommand()`: Returns `'/hideout ' + this.seller`.

---

## 3. View 2: Live Search Feed (`live`)

### Live Search Result Item Payload (`onLiveSearchResult`)

* **C# Method**: `PoeWebBridgeService.PushLiveListingToWebAsync(itemName, seller, character, price, currency, age, goldCost)`
* **JS Receiver**: `window.PoeBridge.onLiveSearchResult(listing)`
* **JS Handler**: `window.liveSearchController.pushLiveListing(data)`
* **Target Container**: `#liveResultsList` in `#viewLivePanel`

#### Payload Structure
```json
{
  "name": "CHAMBERS OF INSCRIPTION",
  "seller": "GodSpeedTrader",
  "char": "FastExile",
  "price": "2.4",
  "curr": "Divine",
  "age": "JUST NOW",
  "gold": 450
}
```

#### Fields Description
| Field | Type | Description |
|---|---|---|
| `name` | string | Name of the live matched item. |
| `seller` | string | Account name of the seller (`@seller`). |
| `char` | string | Character name of the seller (`ign: char`). |
| `price` | string | Price amount. |
| `curr` | string | Currency name (`'Divine'`, `'Chaos'`). |
| `age` | string | Elapsed time string (`'JUST NOW'`, `'10s AGO'`). |
| `gold` | number | Faustus gold cost integer. |

---

### Monitored Search Query Model

* **Domain Model**: `js/models/LiveSearchQueryModel.js`
* **Storage**: `LiveSearchController.prototype.monitoredSearches` array
* **Trigger to C#**: `notifyHost({ action: 'live_add', name: ..., maxPrice: ..., currency: ... })`

#### Fields Description
| Field | Type | Default | Description |
|---|---|---|---|
| `id` | string | Auto-generated | Unique identifier (e.g. `lsq_1729000000_123`). |
| `queryText` | string | `''` | Item title or search filter phrase. |
| `maxPrice` | number \| null | `null` | Maximum price threshold (or null for any price). |
| `currency` | string | `'Divine'` | Target currency (`'Divine'` or `'Chaos'`). |
| `active` | boolean | `true` | Active monitoring status toggle. |

---

## 4. View 3: Alarm Mode (`alarm`)

### Alarm Alert Notification Payload

* **JS Handler**: `window.alarmController.showAlert(alertData)`
* **Target Container**: `#viewAlarmPanel`

#### Structure
```json
{
  "preset": "theme1",
  "name": "CHAMBERS OF INSCRIPTION",
  "base": "TEMPLE OF ATZOATL - TIER 3",
  "seller": "InscribedGod_US#1395",
  "priceVal": "2.5 DIVINE",
  "priceType": "Divine",
  "gold": 450,
  "ruleId": "r_1729000000"
}
```

---

### Alarm Rule Model

* **Domain Model**: `js/models/AlarmRuleModel.js`
* **Storage**: `AlarmController.prototype.alarmRules` array
* **Trigger to C#**: `notifyHost({ action: 'alarm_add', preset: ..., maxPrice: ..., currency: ..., maxGold: ... })`

#### Fields Description
| Field | Type | Default | Description |
|---|---|---|---|
| `id` | string | Auto-generated | Unique rule identifier (e.g. `r_1729000000`). |
| `maxPrice` | number | `0` | Upper price limit. |
| `currency` | string | `'Divine'` | Currency denomination (`'Divine'` or `'Chaos'`). |
| `maxGold` | number | `500` | Maximum acceptable Faustus gold fee. |
| `active` | boolean | `true` | Rule active toggle. |

---

## 5. System & Global Handlers

### Auto-Minimize Countdown Timer (`onTimerTick`)

* **C# Method**: `PoeWebBridgeService.SendTimerTickToWebAsync(remainingSeconds, totalDurationSeconds, isPaused)`
* **JS Receiver**: `window.PoeBridge.onTimerTick(remainingSeconds, totalDurationSeconds, isPaused)`
* **DOM Targets**:
  - `#autoMinimizeTimer`: Remaining seconds formatted to one decimal place (`11.8`).
  - `#autoMinimizeText`: Descriptive status text (`Auto-minimizing in 11.8s (Hover to pause)` or paused notice).
  - `#bottomProgressBar` / `.bottom-progress-bar`: Progress bar width in percentage (`(rem / tot) * 100%`).

---

### API Rate Limiter Status (`onRateLimitUpdated`)

* **C# Method**: `PoeWebBridgeService.UpdateRateLimitStatusAsync(statusText, isThrottled)`
* **JS Receiver**: `window.PoeBridge.onRateLimitUpdated(statusText, isThrottled)`
* **DOM Target**: `.rate-limit-badge`
  - Text: `RATE LIMIT: NORMAL` or `RATE LIMIT: THROTTLED`
  - Color: Red (`#ff6b6b`) when throttled.

---

## 6. Web to C# Bridge Actions (`window.PoeBridge.notifyHost`)

Sent from JavaScript to C# via `window.PoeBridge.notifyHost(payload)`. Handled inside `PoeWebBridgeService.OnWebMessageReceived(string raw)`.

| Action Name | Payload Fields | Purpose / Behavior |
|---|---|---|
| `whisper` | `{ action: 'whisper', seller: string, character: string, text: string, price: string, currency: string }` | Copies whisper message to clipboard and plays notification audio. |
| `hideout` | `{ action: 'hideout', seller: string, command: string, token: string? }` | Sends direct API hideout teleport request via `SendDirectHideoutTokenAsync` using POESESSID cookie if `token` is present; otherwise copies `/hideout <seller>` to clipboard. |
| `search` | `{ action: 'search', preset: string, filters: { modifiers: [...], properties: [...] } }` | Requests market search from official PoE trade API for current item with active modifier/property filters. |
| `evaluate` | `{ action: 'evaluate' }` | Triggers evaluation re-check of current clipboard/item. |
| `preset` | `{ action: 'preset', key: string }` | Notifies C# backend of active preset change. |
| `view_changed` | `{ action: 'view_changed', mode: 'evaluate' | 'live' | 'alarm' }` | Notifies C# of active navigation tab change. |
| `set_hover` | `{ action: 'set_hover', isHovered: boolean }` | Pauses or resumes the auto-minimize countdown timer. |
| `minimize` | `{ action: 'minimize' }` | Programmatically triggers widget minimization. |
| `restore` | `{ action: 'restore' }` | Programmatically triggers widget restoration. |
| `open_browser` | `{ action: 'open_browser', url: string }` | Opens external URL in default desktop browser via `Launcher.LaunchUriAsync`. |
| `live_add` | `{ action: 'live_add', name: string, maxPrice: number?, currency: string }` | Registers a new live search query session with C# WebSocket. |
| `live_remove` | `{ action: 'live_remove', id: string }` | Unregisters an active live search query session. |
| `alarm_add` | `{ action: 'alarm_add', preset: string, maxPrice: number, currency: string, maxGold: number }` | Adds a new alarm price threshold rule in backend. |

---

## 7. Unscalable Modifiers (`isUnscalable`) Pipeline

### 7.1 C# Backend Detection & Normalization
* **File Reference**: `GameBarWidget/Services/Parsing/PoeModifierParser.cs` and `GameBarWidget/Services/PoeOfficialTradeClient.cs`
* **Detection Logic**:
  Modifiers containing `unscalable` or `— Unscalable Value` in raw text (case-insensitive) are flagged:
  ```csharp
  bool isUnscalable = line.IndexOf("unscalable", StringComparison.OrdinalIgnoreCase) >= 0;
  mod.IsUnscalable = isUnscalable;
  ```
* **Text Sanitation**:
  The unscalable tag is cleaned using regexes so the user sees only the clean modifier wording:
  ```csharp
  line = Regex.Replace(line, @"\s*[\u2014\u2013\u2015\u2212\u002D—–-]\s*Unscalable(?:\s+Value)?", "", RegexOptions.IgnoreCase).Trim();
  line = Regex.Replace(line, @"\s*\((?:unscalable(?:\s+value)?)\)", "", RegexOptions.IgnoreCase).Trim();
  ```
* **Numerical Bounds**:
  For unscalable mods, roll variance calculations (e.g. 10% lower bounds) are bypassed: `mod.MinRoll = mod.MaxRoll = mod.NumberValue`.

### 7.2 Bridge Serialization (`PoeWebBridgeService.cs`)
In `SendItemToWebAsync`:
```csharp
mObj["isUnscalable"] = JsonValue.CreateBooleanValue(m.IsUnscalable);
```

### 7.3 Web Front-End Handling (`ItemController.js` & `css/tooltip.css`)
* **Visual Indicator**: Appends an `[Unscalable]` badge element (`.unscalable-badge`) next to the modifier text.
* **Input Lockout**: The numeric `min` and `max` input fields (`.poe-input`) are disabled (`disabled = true`, `opacity: 0.4`, `cursor: not-allowed`), preventing the user from entering invalid or non-existent roll ranges.

---

## 8. IGN and Seller Resolution Engine

### 8.1 Root Cause of Previous Undefined/Wrong IGN in Web
1. **Private/Missing Profiles in GGG Trade API**: When querying `/api/trade/fetch/{ids}`, GGG does not guarantee `account.lastCharacterName` is present; it may be `null` or omitted if the player's profile is hidden.
2. **Exchange Search Variations**: The bulk/exchange endpoint (`/api/trade/exchange/{league}`) does not include character IGNs in its response objects.
3. **String Fallback Discrepancy**: In C#, expressions like `l.AccountName ?? "Exile"` left empty strings `""` untouched because `""` is non-null. When serialized to JSON, JavaScript received `""`, displaying blank seller tags (`@` with nothing after).
4. **Property Naming Mismatch**: Different components in JavaScript previously expected `data.seller`, `data.account`, `data.char`, `data.ign`, or `data.character`.

### 8.2 C# Backend Resolution Fix
* **File Reference**: `GameBarWidget/Services/PoeOfficialTradeClient.cs` (`ParseFetchListings`)
* **Regex Whisper Extraction**: If `charIgn` is missing from the `account` object, it is extracted directly from the official whisper string:
  ```csharp
  if (string.IsNullOrWhiteSpace(charIgn) && !string.IsNullOrWhiteSpace(whisper))
  {
      var ignMatch = Regex.Match(whisper, @"^@([^\s]+)");
      if (ignMatch.Success)
      {
          charIgn = ignMatch.Groups[1].Value.Trim();
      }
  }
  ```
* **Bidirectional Fallback**:
  ```csharp
  if (string.IsNullOrWhiteSpace(charIgn))
  {
      charIgn = !string.IsNullOrWhiteSpace(accountName) && !accountName.Equals("Exile", StringComparison.OrdinalIgnoreCase)
          ? accountName
          : "Unknown";
  }
  if (string.IsNullOrWhiteSpace(accountName) || accountName.Equals("Exile", StringComparison.OrdinalIgnoreCase))
  {
      if (!string.IsNullOrWhiteSpace(charIgn) && !charIgn.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
      {
          accountName = charIgn;
      }
  }
  ```
* **Payload Normalization**:
  In `PoeWebBridgeService.cs`:
  ```csharp
  string safeSeller = !string.IsNullOrWhiteSpace(l.AccountName) ? l.AccountName : (!string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : "Exile");
  string safeChar = !string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : safeSeller;

  itemObj["seller"] = JsonValue.CreateStringValue(safeSeller);
  itemObj["character"] = JsonValue.CreateStringValue(safeChar);
  itemObj["char"] = JsonValue.CreateStringValue(safeChar);
  itemObj["ign"] = JsonValue.CreateStringValue(safeChar);
  ```

---

## 9. Modifier and Property Selection Flow

### 9.1 No Checkboxes: Clickable Row Elements with Background Tint
* **Interaction**: Clicking anywhere on the modifier label container (`.stat-label-wrap`) or property label container (`.prop-label-wrap`) toggles selection. No checkbox widgets are used.
* **Selection State Class**: `.is-selected` is toggled on `.stat-line` and `.property-line`.
* **Background Tint Behind Text**:
  - Selected text receives class `.tinted-badge`:
    ```css
    .stat-text.tinted-badge,
    .property-value.tinted-badge {
      background: rgba(88, 101, 242, 0.28);
      border-color: rgba(130, 150, 220, 0.35);
      color: #e0e7ff;
    }
    ```
  - Unselected rows have `opacity: 0.5` with transparent background.

### 9.2 Triggering Search & Transmitting Filters to C#
When the player clicks "Search Market" (`#searchMarketBtn`):
1. `TradeController.prototype.triggerSearch()` scans all `.stat-line` and `.property-line` elements.
2. It packages active flags and custom `min`/`max` bounds:
   ```json
   {
     "action": "search",
     "preset": "chambers",
     "filters": {
       "modifiers": [
         { "index": 0, "active": true, "min": "20", "max": "25" },
         { "index": 1, "active": false, "min": null, "max": null }
       ],
       "properties": [
         { "index": 0, "label": "Quality", "active": true, "min": "20", "max": null }
       ]
     }
   }
   ```
3. Dispatched via `window.PoeBridge.notifyHost(...)`.

### 9.3 Backend Application & Query Execution (`Widget1.xaml.cs`)
1. `PoeWebBridgeService` invokes `OnSearchRequested(preset, filters)`.
2. `Widget1.ApplyWebFiltersToItem(_currentItem, filters)`:
   - Matches modifier indices with `_currentItem.ExplicitMods[idx]`.
   - Sets `mod.IsActive = active`, `mod.MinRoll = min`, `mod.MaxRoll = max`.
   - Sets item property filter states (`FilterQualityActive`, `FilterSocketsActive`, etc.).
3. `QueryMarketAsync(_currentItem)` executes official trade query via `PoeOfficialTradeClient.SearchItemAsync(_currentItem)`.
4. `SearchItemAsync` adds stats to GGG query ONLY for `mod.IsActive && !string.IsNullOrEmpty(mod.StatId)`. Inactive mods are omitted to prevent GGG 400 Bad Request errors.
