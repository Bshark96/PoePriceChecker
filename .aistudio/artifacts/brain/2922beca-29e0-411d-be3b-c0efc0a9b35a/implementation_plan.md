# Implementation Plan - Path of Exile 1 Live Search Feature & Multi-View Architecture

Architectural plan for implementing hotkey-driven multi-view switching in the widget overlay, featuring a dedicated `LiveSearchView` containing trade URL entry controls, active search listing management, maximum price notification thresholds, real-time WebSocket Live Search streaming, and direct hideout travel within the C# Xbox Game Bar overlay widget architecture.

---

## 1. Core Objectives

- **Hotkey-Driven Multi-View Architecture**:
  - `CTRL+D` (or price check hotkey): Automatically opens widget and activates `PriceCheckView`.
  - `ALT+A` / `ALT+D` / `ALT+F` (or live search hotkey): Automatically opens widget and activates `LiveSearchView`.
  - Header tab buttons ("PRICE CHECK" / "LIVE SEARCH") allow manual view toggling anytime.
- **Dedicated `LiveSearchView` Layout**:
  - Trade URL entry bar directly inside `LiveSearchView` to paste trade links (e.g. `https://www.pathofexile.com/trade/search/Standard/xyz123`).
  - Max price notification threshold input (max Chaos / Divine value).
  - Active search queries manager showing list of currently monitored live searches with toggle/delete controls.
  - Live search stream panel rendering incoming notifications with silent border flash animation.
- **Automatic Link Parsing**: Extract league name and search query ID directly from trade links via `PoeUrlParser.cs`.
- **Official PoE Live Search WebSocket Streaming**: Connect directly to `wss://www.pathofexile.com/api/trade/live/<league>/<searchId>` endpoints.
- **Automatic Reconnect & Ping Heartbeat**: Implement automatic WebSocket heartbeat ping handling (30s intervals) and background auto-reconnect with exponential backoff on network disconnects.
- **Direct Hideout & Whisper Action**: Instant "To Hideout" direct travel token execution (`SendDirectHideoutTokenAsync`) and "Whisper" clipboard buttons for incoming live listings matching price thresholds.

---

## 2. Architecture & File Structure

```
GameBarWidget/
├── Services/
│   ├── PoeLiveSearchClient.cs         # WebSocket manager supporting multiple active search streams
│   ├── PoeLiveSearchQuery.cs          # Model holding query URL, search ID, league, label, and max price threshold
│   ├── PoeUrlParser.cs                # URL helper parsing league and search ID from trade links
│   └── PoeSettingsManager.cs          # Persistent storage for active live search queries & view state
├── Design/
│   ├── LiveSearchCardBuilder.cs       # Active search list item & live notification card builder
│   └── DesignPalette.cs              # View tab brushes & pulse/flash animation colors
├── Widget1.xaml                       # Main overlay with header view switcher tabs (PriceCheckView & LiveSearchView)
└── Widget1.xaml.cs                    # Multi-view switching logic, hotkey handlers & WebSocket stream events
```

---

## 3. Detailed Component Plan

### A. Hotkey-Driven Multi-View Switching (`Widget1.xaml` & `Widget1.xaml.cs`)
- Adds header tab navigation bar: `PriceCheckTabBtn` and `LiveSearchTabBtn`.
- Listens for daemon hotkey commands:
  - `"ShowPriceCheck"` or `CTRL+D` -> Activates `PriceCheckView` and restores widget visibility.
  - `"ShowLiveSearch"` or `ALT+A`/`ALT+D`/`ALT+F` -> Activates `LiveSearchView` and restores widget visibility.

### B. Dedicated `LiveSearchView` Layout (`Widget1.xaml`)
- **Trade URL Entry Bar**:
  - `TextBox` (`LiveSearchUrlBox`) with "Paste Link" clipboard button.
  - `MaxPriceBox` (`TextBox`) and `MaxPriceCurrencyCombo` (`ComboBox`) for notification price limits.
  - "Start Search" button to parse and activate query.
  - All input boxes configured with `ContextFlyout="{x:Null}"` and context menu suppression.
- **Active Search List Panel**:
  - List of active queries showing search token/label, active CheckBox, max price threshold badge, and remove button.
- **Live Notifications Stream Container**:
  - Stream of incoming item listing cards with price highlights, "To Hideout" direct teleport, and "Whisper" buttons.

### C. `PoeLiveSearchQuery.cs`, `PoeUrlParser.cs` & `PoeLiveSearchClient.cs`
- `PoeUrlParser`: Parses search/live trade links into `(league, searchId)`.
- `PoeLiveSearchQuery`: Represents an active live query with ID, league, label, max price limit, and toggle state.
- `PoeLiveSearchClient`: Handles WebSocket connections (`wss://www.pathofexile.com/api/trade/live/<league>/<searchId>`), 30s ping heartbeats, exponential backoff auto-reconnects, and price threshold filtering.

---

## 4. Verification & Constraints

- Strict C# WinRT / UWP architecture without web dependencies.
- Zero emojis in all UI code, messages, and comments.
- Context menu suppression preserved across all new live search UI components.
