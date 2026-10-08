# Live Search Feature Architecture (C# GameBarWidget)

This document outlines the software architecture, data flows, and design patterns for integrating official Path of Exile Live Search monitoring into the C# Xbox GameBarWidget overlay without touching example projects or web components.

> [!IMPORTANT]
> The implementation strictly maintains C# native UWP/WinUI architecture, isolating live search connection handlers into dedicated C# service modules while preserving clean human-readable code layout.

---

## User Review & Critical Decisions

- **Hotkey Activation**: Configured default binding `ALT+A` (with `ALT+D`, `ALT+F` layout options) to trigger the live search overlay layer.
- **Data Source Engine**: Direct WebSocket connection to official Path of Exile trade live endpoints (`/api/trade/live/...`), managed via a clean C# background service.
- **Example Isolation**: Code under `/example` is strictly treated as read-only showcase reference and will not be linked to main app code.
- **Zero Web Stack**: Pure C# and XAML UWP architecture; no JavaScript, HTML, or web runtime bridges.

---

## 1. Overview & Core Concept

- **What It Does**: Establishes a persistent, lightweight WebSocket client to listen for instant item listing notifications from Path of Exile trade search URLs. When a matching item is posted, it instantly formats trade listings into native C# UI rows.
- **Target Audience / Persona**: Path of Exile traders who need instant in-game notifications for live trade queries without leaving their active game screen.
- **Key Value**: Minimum latency from listing to whisper copy, direct overlay UI integration, and modular C# class separation.

---

## 2. User Experience & Visual Design

- **Overlay Windows & Views**:
  - Compact Live Search layout window toggleable via assigned hotkeys (`ALT+A`, `ALT+D`, `ALT+F`).
  - Active search subscription card displaying live connection status, search query name, and listing counter.
  - Live listing cards showing item name, explicit modifiers, price in Chaos/Divine Orbs, seller online status, and direct copy whisper button.
- **Aesthetic Direction**: Deep dark ambient slate theme (`#0b1118` / `#131b26`), crisp typography, emerald/blue status badges, muted borders.
- **Interactive Micro-Feedback**:
  - Connection status badge transitions: Connecting (yellow/amber), Active (emerald), Reconnecting (soft crimson).
  - One-click whisper copying with immediate visual confirmation feedback.

---

## 3. Key Product Decisions & Trade-Offs

- **Decision 1: WebSocket Management via Dedicated C# Service Class**
  - *Chosen Approach*: `PoeLiveSearchService` class encapsulating `ClientWebSocket` lifecycle, automatic ping/heartbeat, reconnection retry backoff, and JSON deserialization.
  - *Why*: Prevents WebSocket state management from bloating UI controllers (`Widget1.xaml.cs`). Maintains easy, human-readable single-responsibility architecture.
  - *Alternatives Considered*: Polling HTTP endpoint (high latency, rate-limit risk) or embedding socket logic directly into UI code (violates single responsibility).

- **Decision 2: UI Delegation to Dedicated Row Builders**
  - *Chosen Approach*: Delegate card and row rendering to static builder factory methods in `GameBarWidget/Design/`.
  - *Why*: Keeps XAML code-behind minimal and easily auditable.

---

## 4. Technical Architecture & Data Strategy

```
+-------------------------------------------------------------------+
|                        GameBarWidget UI                           |
|  +-----------------------+     +-------------------------------+  |
|  |   Widget1.xaml.cs     |     |  LiveSearchCardBuilder.cs     |  |
|  |  (Layout & Dispatch)  | <-> |  (XAML Control Generation)    |  |
|  +-----------------------+     +-------------------------------+  |
+-------------------------------------------------------------------+
                                   ^
                                   | Event Callback (OnListingReceived)
+-------------------------------------------------------------------+
|                     Core Service Architecture                     |
|  +-------------------------------------------------------------+  |
|  |                   PoeLiveSearchService.cs                   |  |
|  |  - ClientWebSocket Manager                                  |  |
|  |  - PoeSession Authenticator & Cookie Container              |  |
|  |  - Connection Lifecycle & Reconnect Backoff Loop            |  |
|  +-------------------------------------------------------------+  |
+-------------------------------------------------------------------+
                                   |
                                   v
+-------------------------------------------------------------------+
|                   Official PoE Trade Live API                     |
|            wss://www.pathofexile.com/api/trade/live/...            |
+-------------------------------------------------------------------+
```

### Component Breakdown

1. **`PoeLiveSearchService.cs`**:
   - Manages connection lifecycle to `wss://www.pathofexile.com/api/trade/live/{league}/{searchId}`.
   - Handles headers (`POESESSID` cookie, `User-Agent`).
   - Dispatches parsed `TradeListing` DTO objects to the UI thread via `CoreDispatcher`.

2. **`LiveSearchCardBuilder.cs`**:
   - Constructs structured `Grid` and `StackPanel` rows for newly received live search results.
   - Suppresses right-click context menus (`SuppressContextMenu`) to match application design guidelines.

3. **`PoeSettingsManager.cs`**:
   - Stores user preferences (`LiveSearchHotkey`, saved search URLs, `POESESSID`).
