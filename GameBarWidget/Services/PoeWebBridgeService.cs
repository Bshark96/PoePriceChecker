using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.UI.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Service that bridges C# backend logic and the modern WinUI 2 WebView2 frontend.
    /// Handles bidirectional communication via window.chrome.webview.postMessage and ExecuteScriptAsync.
    /// </summary>
    public sealed class PoeWebBridgeService
    {
        private readonly WebView2 _webView;
        private readonly CoreDispatcher _dispatcher;

        // C# Action Events dispatched from Web WebMessageReceived
        public event Action<string> OnPresetChanged;
        public event Action<string, string> OnWhisperCopied;
        public event Action<string> OnHideoutRequested;
        public event Func<string, JsonObject, Task> OnSearchRequested;
        public event Action<string, string, double?, string> OnLiveSearchAdded;
        public event Action<string> OnLiveSearchRemoved;
        public event Action OnLiveSearchHistoryCleared;
        public event Action OnLiveSearchViewRequested;
        public event Action<string, double, string, int> OnAlarmRuleAdded;
        public event Action OnDismissRequested;
        public event Action<string> OnOpenBrowserRequested;
        public event Func<Task> OnEvaluateRequested;
        public event Action<bool, string, double, double> OnHoverStateChanged;
        public event Action OnUserInteracted;
        public event Action<string> OnInputFocusRequested;
        public event Action OnWebReady;

        public PoeWebBridgeService(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _dispatcher = webView.Dispatcher;
        }

        public void Initialize()
        {
            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        public void Detach()
        {
            _webView.WebMessageReceived -= OnWebMessageReceived;
        }

        #region Web Message Handling (Web -> C#)

        private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(raw)) return;

                // Log incoming C# bridge message to desktop text file
                await BridgeLogger.LogBridgeMessageAsync(raw);

                if (JsonObject.TryParse(raw, out var json))
                {
                    string action = json.ContainsKey("action") ? json.GetNamedString("action") : string.Empty;
                    switch (action.ToLowerInvariant())
                    {
                        case "ready":
                            await InjectHoverTrackerScriptAsync();
                            OnWebReady?.Invoke();
                            break;

                        case "whisper":
                            OnUserInteracted?.Invoke();
                            string whisperText = json.ContainsKey("text") ? json.GetNamedString("text") : string.Empty;
                            string seller = json.ContainsKey("seller") ? json.GetNamedString("seller") : string.Empty;
                            CopyTextToClipboard(whisperText);
                            OnWhisperCopied?.Invoke(seller, whisperText);
                            break;

                        case "hideout":
                            OnUserInteracted?.Invoke();
                            string hSeller = json.ContainsKey("seller") ? json.GetNamedString("seller") : string.Empty;
                            string hToken = json.ContainsKey("token") ? json.GetNamedString("token") : string.Empty;
                            if (!string.IsNullOrEmpty(hToken))
                            {
                                string poesessid = PoeSettingsManager.Instance.PoeSessId;
                                var res = await PoeOfficialTradeClient.Instance.SendDirectHideoutTokenAsync(hToken, poesessid);
                                await BridgeLogger.LogBridgeMessageAsync($"[Bridge] Hideout token teleport result: {res.success} - {res.message}");
                            }
                            else
                            {
                                await BridgeLogger.LogBridgeMessageAsync("[Bridge] Hideout action ignored: No hideout token present on listing.");
                            }
                            OnHideoutRequested?.Invoke(hSeller);
                            break;

                        case "evaluate":
                            if (OnEvaluateRequested != null)
                            {
                                await OnEvaluateRequested.Invoke();
                            }
                            break;

                        case "preview-item":
                            OnUserInteracted?.Invoke();
                            string pItemId = json.ContainsKey("itemId") ? json.GetNamedString("itemId") : string.Empty;
                            string pQueryId = json.ContainsKey("queryId") ? json.GetNamedString("queryId") : string.Empty;
                            if (!string.IsNullOrEmpty(pItemId))
                            {
                                string league = PoeSettingsManager.Instance.SelectedLeague;
                                var fetchedListings = await PoeOfficialTradeClient.Instance.FetchListingsAsync(new List<string> { pItemId }, league, pQueryId);
                                if (fetchedListings != null && fetchedListings.Count > 0)
                                {
                                    await SendItemPreviewToWebAsync(fetchedListings[0]);
                                }
                            }
                            break;

                        case "search":
                            OnUserInteracted?.Invoke();
                            string preset = json.ContainsKey("preset") ? json.GetNamedString("preset") : "chambers";
                            JsonObject filters = json.ContainsKey("filters") && json.GetNamedValue("filters").ValueType == JsonValueType.Object
                                ? json.GetNamedObject("filters")
                                : null;
                            if (OnSearchRequested != null)
                            {
                                await OnSearchRequested.Invoke(preset, filters);
                            }
                            break;

                        case "preset":
                            OnUserInteracted?.Invoke();
                            string selectedPreset = json.ContainsKey("key") ? json.GetNamedString("key") : string.Empty;
                            OnPresetChanged?.Invoke(selectedPreset);
                            break;

                        case "live_add":
                        case "add_live":
                            OnUserInteracted?.Invoke();
                            string lQueryId = json.ContainsKey("id") && json.GetNamedValue("id").ValueType == JsonValueType.String
                                ? json.GetNamedString("id")
                                : string.Empty;
                            string lRawUrl = json.ContainsKey("url") && json.GetNamedValue("url").ValueType == JsonValueType.String
                                ? json.GetNamedString("url")
                                : string.Empty;
                            string lName = json.ContainsKey("name") && json.GetNamedValue("name").ValueType == JsonValueType.String
                                ? json.GetNamedString("name")
                                : (json.ContainsKey("title") && json.GetNamedValue("title").ValueType == JsonValueType.String ? json.GetNamedString("title") : string.Empty);
                            double? lPrice = null;
                            if (json.ContainsKey("maxPrice"))
                            {
                                var mpVal = json.GetNamedValue("maxPrice");
                                if (mpVal.ValueType == JsonValueType.Number)
                                {
                                    lPrice = json.GetNamedNumber("maxPrice");
                                }
                                else if (mpVal.ValueType == JsonValueType.String &&
                                         double.TryParse(json.GetNamedString("maxPrice"), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMp))
                                {
                                    lPrice = parsedMp;
                                }
                            }
                            string lCurr = json.ContainsKey("currency") && json.GetNamedValue("currency").ValueType == JsonValueType.String
                                ? json.GetNamedString("currency")
                                : "Chaos";
                            OnLiveSearchAdded?.Invoke(lQueryId, !string.IsNullOrWhiteSpace(lRawUrl) ? lRawUrl : lName, lPrice, lCurr);
                            break;

                        case "live_remove":
                        case "delete_live":
                            OnUserInteracted?.Invoke();
                            string lId = json.ContainsKey("id") ? json.GetNamedString("id") : string.Empty;
                            OnLiveSearchRemoved?.Invoke(lId);
                            break;

                        case "live_paste":
                        case "paste_live":
                            OnUserInteracted?.Invoke();
                            string pasteTargetId = json.ContainsKey("targetId") && json.GetNamedValue("targetId").ValueType == JsonValueType.String
                                ? json.GetNamedString("targetId")
                                : "liveSearchInput";
                            await PasteClipboardToLiveSearchAsync(pasteTargetId);
                            break;

                        case "focus_input":
                            OnUserInteracted?.Invoke();
                            string focusId = json.ContainsKey("id") && json.GetNamedValue("id").ValueType == JsonValueType.String
                                ? json.GetNamedString("id")
                                : string.Empty;
                            OnInputFocusRequested?.Invoke(focusId);
                            break;

                        case "live_clear_history":
                        case "clear_live_history":
                            OnUserInteracted?.Invoke();
                            PoeLiveSearchClient.Instance.ClearHistory();
                            OnLiveSearchHistoryCleared?.Invoke();
                            break;

                        case "open_live_search":
                        case "show_live_search":
                            OnUserInteracted?.Invoke();
                            OnLiveSearchViewRequested?.Invoke();
                            await ShowLiveSearchViewAsync();
                            break;

                        case "alarm_add":
                            OnUserInteracted?.Invoke();
                            string aPreset = json.ContainsKey("preset") ? json.GetNamedString("preset") : string.Empty;
                            double aPrice = json.ContainsKey("maxPrice") ? json.GetNamedNumber("maxPrice") : 0;
                            string aCurr = json.ContainsKey("currency") ? json.GetNamedString("currency") : "Divine";
                            int aGold = json.ContainsKey("maxGold") ? (int)json.GetNamedNumber("maxGold") : 500;
                            OnAlarmRuleAdded?.Invoke(aPreset, aPrice, aCurr, aGold);
                            break;

                        case "dismiss":
                        case "minimize":
                            OnDismissRequested?.Invoke();
                            break;

                        case "set_hover":
                            bool isHovered = json.ContainsKey("isHovered") && json.GetNamedBoolean("isHovered");
                            string eventType = json.ContainsKey("eventType") && json.GetNamedValue("eventType").ValueType == JsonValueType.String
                                ? json.GetNamedString("eventType")
                                : (isHovered ? "enter" : "leave");
                            double ptrX = json.ContainsKey("x") && json.GetNamedValue("x").ValueType == JsonValueType.Number
                                ? json.GetNamedNumber("x")
                                : double.NaN;
                            double ptrY = json.ContainsKey("y") && json.GetNamedValue("y").ValueType == JsonValueType.Number
                                ? json.GetNamedNumber("y")
                                : double.NaN;
                            OnHoverStateChanged?.Invoke(isHovered, eventType, ptrX, ptrY);
                            break;

                        case "open_browser":
                            OnUserInteracted?.Invoke();
                            string url = json.ContainsKey("url") ? json.GetNamedString("url") : string.Empty;
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                OnOpenBrowserRequested?.Invoke(url);
                                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"Bridge Dispatch Error: {ex.Message}");
            }
        }

        private void CopyTextToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
            }
            catch { }
        }

        #endregion

        #region Script Invocation (C# -> WebView2)

        /// <summary>
        /// Sends a parsed PoE 1 item to the WebView2 control.
        /// </summary>
        public async Task SendItemToWebAsync(PoeItem item)
        {
            if (_webView == null || item == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string rarityKey = PoeCategoryResolver.ResolveThemeKey(item);

                    var payload = new JsonObject();
                    payload["name"] = JsonValue.CreateStringValue(item.Name ?? item.BaseType ?? "Unknown Item");
                    payload["base"] = JsonValue.CreateStringValue(item.BaseType ?? string.Empty);
                    payload["rarityKey"] = JsonValue.CreateStringValue(rarityKey);
                    payload["rarity"] = JsonValue.CreateStringValue(item.Rarity.ToString());
                    payload["itemLevel"] = JsonValue.CreateNumberValue(item.ItemLevel);
                    payload["isCorrupted"] = JsonValue.CreateBooleanValue(item.IsCorrupted);
                    bool corrActive = !string.IsNullOrEmpty(item.CorruptedFilterOption)
                        ? item.CorruptedFilterOption.Equals("true", StringComparison.OrdinalIgnoreCase)
                        : item.IsCorrupted;
                    payload["corruptedFilterActive"] = JsonValue.CreateBooleanValue(corrActive);

                    var benchmark = PoeNinjaClient.Instance.GetBenchmark(item);
                    if (benchmark != null && benchmark.Found && !string.IsNullOrWhiteSpace(benchmark.FormattedText))
                    {
                        payload["ninjaPrice"] = JsonValue.CreateStringValue(benchmark.FormattedText);
                        if (!string.IsNullOrWhiteSpace(benchmark.DustText))
                        {
                            payload["dustValue"] = JsonValue.CreateStringValue(benchmark.DustText);
                        }
                    }

                    var propArray = new JsonArray();
                    if (item.Properties != null)
                    {
                        foreach (var kvp in item.Properties)
                        {
                            string key = kvp.Key ?? string.Empty;
                            string valStr = kvp.Value?.ToString() ?? string.Empty;
                            bool isActive = false;
                            string minStr = string.Empty;
                            string maxStr = string.Empty;

                            if (key.IndexOf("Quality", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterQualityActive;
                                int qVal = item.FilterQualityMin ?? item.Quality;
                                if (qVal > 0)
                                {
                                    double qMin = item.FilterQualityMin.HasValue ? item.FilterQualityMin.Value : PoeModifierParser.ApplyTolerance(qVal);
                                    minStr = qMin.ToString(CultureInfo.InvariantCulture);
                                }
                                if (item.FilterQualityMax.HasValue) maxStr = item.FilterQualityMax.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.Equals("Level", StringComparison.OrdinalIgnoreCase) || key.IndexOf("Gem Level", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterGemLevelActive || (item.Namespace == ItemNamespace.Gem && item.GemLevel > 0);
                                int gMin = item.FilterGemLevelMin ?? item.GemLevel;
                                if (gMin > 0) minStr = gMin.ToString(CultureInfo.InvariantCulture);
                                if (item.FilterGemLevelMax.HasValue) maxStr = item.FilterGemLevelMax.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Map Tier", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterMapTierActive;
                                int mMin = item.FilterMapTierMin ?? item.MapTier;
                                int mMax = item.FilterMapTierMax ?? item.MapTier;
                                if (mMin > 0) minStr = mMin.ToString(CultureInfo.InvariantCulture);
                                if (mMax > 0) maxStr = mMax.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Item Level", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterItemLevelActive;
                                int iMin = item.FilterItemLevelMin ?? item.ItemLevel;
                                if (iMin > 0) minStr = iMin.ToString(CultureInfo.InvariantCulture);
                                if (item.FilterItemLevelMax.HasValue) maxStr = item.FilterItemLevelMax.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterSocketsActive;
                                int sMin = item.FilterSocketsMin ?? item.SocketCount;
                                if (sMin > 0) minStr = sMin.ToString(CultureInfo.InvariantCulture);
                                if (item.FilterSocketsMax.HasValue) maxStr = item.FilterSocketsMax.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isActive = item.FilterLinksActive;
                                int lMin = item.FilterLinksMin ?? item.LinkCount;
                                if (lMin > 0) minStr = lMin.ToString(CultureInfo.InvariantCulture);
                                if (item.FilterLinksMax.HasValue) maxStr = item.FilterLinksMax.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0 && item.Armour > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.Armour).ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Evasion", StringComparison.OrdinalIgnoreCase) >= 0 && item.Evasion > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.Evasion).ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Energy Shield", StringComparison.OrdinalIgnoreCase) >= 0 && item.EnergyShield > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.EnergyShield).ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Attacks per Second", StringComparison.OrdinalIgnoreCase) >= 0 && item.AttacksPerSecond > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.AttacksPerSecond).ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Critical Strike Chance", StringComparison.OrdinalIgnoreCase) >= 0 && item.CritChance > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.CritChance).ToString(CultureInfo.InvariantCulture);
                            }
                            else if (key.IndexOf("Total DPS", StringComparison.OrdinalIgnoreCase) >= 0 && item.TotalDps > 0)
                            {
                                isActive = false;
                                minStr = PoeModifierParser.ApplyTolerance(item.TotalDps).ToString(CultureInfo.InvariantCulture);
                            }

                            var pObj = new JsonObject();
                            pObj["label"] = JsonValue.CreateStringValue(key);
                            pObj["val"] = JsonValue.CreateStringValue(valStr);
                            pObj["min"] = JsonValue.CreateStringValue(minStr);
                            pObj["max"] = JsonValue.CreateStringValue(maxStr);
                            pObj["active"] = JsonValue.CreateBooleanValue(isActive);
                            propArray.Add(pObj);
                        }
                    }
                    payload["properties"] = propArray;

                    var modArray = new JsonArray();
                    if (item.ExplicitMods != null)
                    {
                        foreach (var m in item.ExplicitMods)
                        {
                            string rollBadge = m.TierInfo ?? string.Empty;
                            if (string.IsNullOrEmpty(rollBadge))
                            {
                                if (m.Type == ModifierType.Implicit) rollBadge = "IMP";
                                else if (m.Type == ModifierType.Enchant) rollBadge = "ENC";
                                else if (m.Type == ModifierType.Crafted) rollBadge = "CRAFT";
                                else if (m.Type == ModifierType.Fractured) rollBadge = "FRAC";
                            }
                            else if (int.TryParse(rollBadge, out _))
                            {
                                rollBadge = $"T{rollBadge}";
                            }

                            string minRollStr = (!m.IsUnscalable && m.MinRoll.HasValue)
                                ? m.MinRoll.Value.ToString(CultureInfo.InvariantCulture)
                                : string.Empty;
                            string maxRollStr = (!m.IsUnscalable && m.MaxRoll.HasValue)
                                ? m.MaxRoll.Value.ToString(CultureInfo.InvariantCulture)
                                : string.Empty;

                            var mObj = new JsonObject();
                            mObj["type"] = JsonValue.CreateStringValue(m.Type.ToString().ToLowerInvariant());
                            mObj["text"] = JsonValue.CreateStringValue(m.Text ?? string.Empty);
                            mObj["min"] = JsonValue.CreateStringValue(minRollStr);
                            mObj["max"] = JsonValue.CreateStringValue(maxRollStr);
                            mObj["roll"] = JsonValue.CreateStringValue(rollBadge);
                            mObj["isUnscalable"] = JsonValue.CreateBooleanValue(m.IsUnscalable);
                            mObj["active"] = JsonValue.CreateBooleanValue(m.IsActive && !string.IsNullOrEmpty(m.StatId));
                            modArray.Add(mObj);
                        }
                    }
                    payload["modifiers"] = modArray;

                    string json = payload.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onItemLoaded) {{ window.PoeBridge.onItemLoaded({json}); }} else if (window.loadPoe1Item) {{ window.loadPoe1Item({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task SendNinjaPriceToWebAsync(string ninjaPrice, string dustValue = null)
        {
            if (_webView == null || string.IsNullOrWhiteSpace(ninjaPrice)) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var payload = new JsonObject();
                    payload["ninjaPrice"] = JsonValue.CreateStringValue(ninjaPrice);
                    if (!string.IsNullOrWhiteSpace(dustValue))
                    {
                        payload["dustValue"] = JsonValue.CreateStringValue(dustValue);
                    }
                    string json = payload.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onNinjaPriceUpdated) {{ window.PoeBridge.onNinjaPriceUpdated({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        /// <summary>
        /// Sends official PoE 1 trade search results into WebView2.
        /// </summary>
        public async Task SendTradeResultsToWebAsync(TradeSearchResult searchResult, string ninjaPriceFallback = null)
        {
            if (_webView == null || searchResult == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var root = new JsonObject();
                    root["totalCount"] = JsonValue.CreateNumberValue(searchResult.TotalListings);
                    root["summary"] = JsonValue.CreateStringValue(searchResult.SummaryText ?? string.Empty);
                    root["queryId"] = JsonValue.CreateStringValue(searchResult.QueryId ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(ninjaPriceFallback))
                    {
                        root["ninjaPrice"] = JsonValue.CreateStringValue(ninjaPriceFallback);
                    }

                    var listArray = new JsonArray();
                    if (searchResult.Listings != null)
                    {
                        foreach (var l in searchResult.Listings)
                        {
                            var itemObj = new JsonObject();
                            string safeSeller = !string.IsNullOrWhiteSpace(l.AccountName) ? l.AccountName : (!string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : "Exile");
                            string safeChar = !string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : safeSeller;

                            itemObj["id"] = JsonValue.CreateStringValue(l.Id ?? string.Empty);
                            itemObj["seller"] = JsonValue.CreateStringValue(safeSeller);
                            itemObj["character"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["char"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["ign"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["priceVal"] = JsonValue.CreateStringValue(l.PriceAmount.ToString(CultureInfo.InvariantCulture));
                            itemObj["priceType"] = JsonValue.CreateStringValue(l.PriceCurrency == "divine" ? "Divine" : "Chaos");
                            itemObj["status"] = JsonValue.CreateStringValue(l.OnlineStatus == "online" ? "online" : "afk");
                            itemObj["age"] = JsonValue.CreateStringValue(l.AgeText ?? "RECENT");
                            itemObj["goldVal"] = JsonValue.CreateNumberValue(l.GoldFee);
                            itemObj["itemName"] = JsonValue.CreateStringValue(l.ItemName ?? string.Empty);
                            itemObj["itemBaseType"] = JsonValue.CreateStringValue(l.ItemBaseType ?? string.Empty);
                            itemObj["itemLevel"] = JsonValue.CreateNumberValue(l.ItemLevel);
                            itemObj["isCorrupted"] = JsonValue.CreateBooleanValue(l.IsCorrupted);
                            itemObj["socketsSummary"] = JsonValue.CreateStringValue(l.SocketsSummary ?? string.Empty);
                            itemObj["physicalDps"] = JsonValue.CreateNumberValue(l.PhysicalDps);
                            itemObj["elementalDps"] = JsonValue.CreateNumberValue(l.ElementalDps);
                            itemObj["totalDps"] = JsonValue.CreateNumberValue(l.TotalDps);
                            itemObj["hideoutToken"] = JsonValue.CreateStringValue(l.HideoutToken ?? string.Empty);

                            var propsArr = new JsonArray();
                            if (l.PropertiesSummary != null)
                            {
                                foreach (var p in l.PropertiesSummary)
                                    propsArr.Add(JsonValue.CreateStringValue(p));
                            }
                            itemObj["properties"] = propsArr;

                            var reqsArr = new JsonArray();
                            if (l.RequirementsSummary != null)
                            {
                                foreach (var r in l.RequirementsSummary)
                                    reqsArr.Add(JsonValue.CreateStringValue(r));
                            }
                            itemObj["requirements"] = reqsArr;

                            var impArr = new JsonArray();
                            if (l.ImplicitMods != null)
                            {
                                foreach (var m in l.ImplicitMods)
                                    impArr.Add(JsonValue.CreateStringValue(m));
                            }
                            itemObj["implicits"] = impArr;

                            var expArr = new JsonArray();
                            if (l.ExplicitMods != null) foreach (var m in l.ExplicitMods) expArr.Add(JsonValue.CreateStringValue(m));
                            itemObj["explicits"] = expArr;

                            var craftArr = new JsonArray();
                            if (l.CraftedMods != null) foreach (var m in l.CraftedMods) craftArr.Add(JsonValue.CreateStringValue(m));
                            itemObj["crafted"] = craftArr;

                            var fracArr = new JsonArray();
                            if (l.FracturedMods != null) foreach (var m in l.FracturedMods) fracArr.Add(JsonValue.CreateStringValue(m));
                            itemObj["fractured"] = fracArr;

                            var enchArr = new JsonArray();
                            if (l.EnchantMods != null) foreach (var m in l.EnchantMods) enchArr.Add(JsonValue.CreateStringValue(m));
                            itemObj["enchant"] = enchArr;

                            var modsArr = new JsonArray();
                            if (l.FlyoutItem?.Modifiers != null)
                            {
                                foreach (var fm in l.FlyoutItem.Modifiers)
                                {
                                    var mObj = new JsonObject();
                                    mObj["type"] = JsonValue.CreateStringValue(fm.Type.ToString().ToLowerInvariant());
                                    mObj["text"] = JsonValue.CreateStringValue(fm.RawText ?? string.Empty);
                                    mObj["tier"] = JsonValue.CreateStringValue(fm.TierInfo ?? string.Empty);
                                    mObj["magnitudes"] = JsonValue.CreateStringValue(fm.MagnitudesText ?? string.Empty);
                                    mObj["isUnscalable"] = JsonValue.CreateBooleanValue(fm.IsUnscalable);
                                    modsArr.Add(mObj);
                                }
                            }
                            itemObj["modifiers"] = modsArr;

                            var highlights = new JsonArray();
                            if (l.Highlights != null)
                            {
                                foreach (var h in l.Highlights)
                                {
                                    highlights.Add(JsonValue.CreateStringValue(h));
                                }
                            }
                            itemObj["highlights"] = highlights;

                            listArray.Add(itemObj);
                        }
                    }
                    root["listings"] = listArray;

                    string json = root.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onTradeResultsLoaded) {{ window.PoeBridge.onTradeResultsLoaded({json}); }} else if (window.loadPoe1TradeResults) {{ window.loadPoe1TradeResults({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task SendItemPreviewToWebAsync(TradeListing l)
        {
            if (_webView == null || l == null) return;
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var obj = new JsonObject();
                    obj["itemName"] = JsonValue.CreateStringValue(l.ItemName ?? string.Empty);
                    obj["itemBaseType"] = JsonValue.CreateStringValue(l.ItemBaseType ?? string.Empty);
                    obj["itemLevel"] = JsonValue.CreateNumberValue(l.ItemLevel);
                    obj["isCorrupted"] = JsonValue.CreateBooleanValue(l.IsCorrupted);
                    obj["socketsSummary"] = JsonValue.CreateStringValue(l.SocketsSummary ?? string.Empty);
                    obj["physicalDps"] = JsonValue.CreateNumberValue(l.PhysicalDps);
                    obj["elementalDps"] = JsonValue.CreateNumberValue(l.ElementalDps);
                    obj["totalDps"] = JsonValue.CreateNumberValue(l.TotalDps);

                    var propsArr = new JsonArray();
                    if (l.PropertiesSummary != null)
                    {
                        foreach (var prop in l.PropertiesSummary)
                            propsArr.Add(JsonValue.CreateStringValue(prop));
                    }
                    obj["properties"] = propsArr;

                    var reqsArr = new JsonArray();
                    if (l.RequirementsSummary != null)
                    {
                        foreach (var req in l.RequirementsSummary)
                            reqsArr.Add(JsonValue.CreateStringValue(req));
                    }
                    obj["requirements"] = reqsArr;

                    var enchantArr = new JsonArray();
                    if (l.EnchantMods != null)
                    {
                        foreach (var m in l.EnchantMods)
                            enchantArr.Add(JsonValue.CreateStringValue(m));
                    }
                    obj["enchant"] = enchantArr;

                    var implicitsArr = new JsonArray();
                    if (l.ImplicitMods != null)
                    {
                        foreach (var m in l.ImplicitMods)
                            implicitsArr.Add(JsonValue.CreateStringValue(m));
                    }
                    obj["implicits"] = implicitsArr;

                    var explicitsArr = new JsonArray();
                    if (l.ExplicitMods != null)
                    {
                        foreach (var m in l.ExplicitMods)
                            explicitsArr.Add(JsonValue.CreateStringValue(m));
                    }
                    obj["explicits"] = explicitsArr;

                    var craftedArr = new JsonArray();
                    if (l.CraftedMods != null)
                    {
                        foreach (var m in l.CraftedMods)
                            craftedArr.Add(JsonValue.CreateStringValue(m));
                    }
                    obj["crafted"] = craftedArr;

                    var fracturedArr = new JsonArray();
                    if (l.FracturedMods != null)
                    {
                        foreach (var m in l.FracturedMods)
                            fracturedArr.Add(JsonValue.CreateStringValue(m));
                    }
                    obj["fractured"] = fracturedArr;

                    var modsArr = new JsonArray();
                    if (l.FlyoutItem?.Modifiers != null)
                    {
                        foreach (var fm in l.FlyoutItem.Modifiers)
                        {
                            var mObj = new JsonObject();
                            mObj["type"] = JsonValue.CreateStringValue(fm.Type.ToString().ToLowerInvariant());
                            mObj["text"] = JsonValue.CreateStringValue(fm.RawText ?? string.Empty);
                            mObj["tier"] = JsonValue.CreateStringValue(fm.TierInfo ?? string.Empty);
                            mObj["magnitudes"] = JsonValue.CreateStringValue(fm.MagnitudesText ?? string.Empty);
                            mObj["isUnscalable"] = JsonValue.CreateBooleanValue(fm.IsUnscalable);
                            modsArr.Add(mObj);
                        }
                    }
                    obj["modifiers"] = modsArr;

                    string json = obj.Stringify();
                    string script = $"if (window.openItemPreviewModalFromBackend) {{ window.openItemPreviewModalFromBackend({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        /// <summary>
        /// Pushes a real-time live alert listing to WebView2 and optionally opens the Alarm view
        /// when the notification arrives while the widget is minimized.
        /// </summary>
        public async Task PushLiveListingToWebAsync(TradeListing listing, bool openAlarmView, string queryId = null)
        {
            if (_webView == null || listing == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var obj = new JsonObject();
                    string safeSeller = !string.IsNullOrWhiteSpace(listing.AccountName) ? listing.AccountName : "Exile";
                    string safeChar = !string.IsNullOrWhiteSpace(listing.CharacterIgn) ? listing.CharacterIgn : safeSeller;
                    string itemName = !string.IsNullOrWhiteSpace(listing.ItemName) ? listing.ItemName : (listing.ItemBaseType ?? "LIVE SEARCH ITEM");
                    string baseType = !string.IsNullOrWhiteSpace(listing.ItemBaseType) ? listing.ItemBaseType : itemName;
                    string currDisplay = FormatCurrencyDisplay(listing.PriceCurrency);
                    string priceStr = listing.PriceAmount.ToString("0.##", CultureInfo.InvariantCulture);

                    string approxStr = string.Empty;
                    if (!string.IsNullOrWhiteSpace(listing.PriceCurrency) &&
                        listing.PriceCurrency.IndexOf("divine", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        PoeNinjaClient.Instance.DivineRate > 0)
                    {
                        double chaosEquiv = Math.Round(listing.PriceAmount * PoeNinjaClient.Instance.DivineRate);
                        approxStr = $"(~{chaosEquiv.ToString("0", CultureInfo.InvariantCulture)}c)";
                    }

                    obj["queryId"] = JsonValue.CreateStringValue(queryId ?? string.Empty);
                    obj["name"] = JsonValue.CreateStringValue(itemName);
                    obj["baseType"] = JsonValue.CreateStringValue(baseType);
                    obj["rarity"] = JsonValue.CreateStringValue((listing.FlyoutItem?.Rarity ?? PoeRarity.Unique).ToString().ToLowerInvariant());
                    obj["seller"] = JsonValue.CreateStringValue(safeSeller);
                    obj["character"] = JsonValue.CreateStringValue(safeChar);
                    obj["char"] = JsonValue.CreateStringValue(safeChar);
                    obj["ign"] = JsonValue.CreateStringValue(safeChar);
                    obj["price"] = JsonValue.CreateStringValue(priceStr);
                    obj["curr"] = JsonValue.CreateStringValue(currDisplay);
                    obj["approx"] = JsonValue.CreateStringValue(approxStr);
                    obj["age"] = JsonValue.CreateStringValue(listing.AgeText ?? "JUST NOW");
                    obj["gold"] = JsonValue.CreateNumberValue(listing.GoldFee);
                    obj["hideoutToken"] = JsonValue.CreateStringValue(listing.HideoutToken ?? string.Empty);
                    obj["whisper"] = JsonValue.CreateStringValue(listing.WhisperString ?? string.Empty);
                    obj["openAlarm"] = JsonValue.CreateBooleanValue(openAlarmView);
                    obj["soundPlayedByHost"] = JsonValue.CreateBooleanValue(true);

                    string json = obj.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onLiveSearchResult) {{ window.PoeBridge.onLiveSearchResult({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        private static string FormatCurrencyDisplay(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                return "Chaos";
            }

            string lower = currency.Trim().ToLowerInvariant();
            if (lower.Contains("divine") || lower == "div")
            {
                return "Divine";
            }
            if (lower.Contains("chaos") || lower == "c")
            {
                return "Chaos";
            }
            if (lower.Contains("exalt") || lower == "ex")
            {
                return "Exalted";
            }
            if (lower.Contains("mirror") || lower == "mir")
            {
                return "Mirror";
            }
            if (lower.Contains("alch"))
            {
                return "Alchemy";
            }
            if (lower.Contains("alter") || lower == "alt")
            {
                return "Alteration";
            }
            if (lower.Contains("vaal"))
            {
                return "Vaal";
            }

            return char.ToUpperInvariant(lower[0]) + (lower.Length > 1 ? lower.Substring(1) : string.Empty);
        }

        public async Task PushLiveListingToWebAsync(string itemName, string seller, string character, string price, string currency, string age, int goldCost, bool openAlarmView = false)
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var obj = new JsonObject();
                    string safeSeller = !string.IsNullOrWhiteSpace(seller) ? seller : "Exile";
                    string safeChar = !string.IsNullOrWhiteSpace(character) ? character : safeSeller;

                    obj["name"] = JsonValue.CreateStringValue(itemName ?? string.Empty);
                    obj["baseType"] = JsonValue.CreateStringValue(itemName ?? string.Empty);
                    obj["seller"] = JsonValue.CreateStringValue(safeSeller);
                    obj["character"] = JsonValue.CreateStringValue(safeChar);
                    obj["char"] = JsonValue.CreateStringValue(safeChar);
                    obj["ign"] = JsonValue.CreateStringValue(safeChar);
                    obj["price"] = JsonValue.CreateStringValue(price ?? string.Empty);
                    obj["curr"] = JsonValue.CreateStringValue(currency ?? "Chaos");
                    obj["age"] = JsonValue.CreateStringValue(age ?? "JUST NOW");
                    obj["gold"] = JsonValue.CreateNumberValue(goldCost);
                    obj["openAlarm"] = JsonValue.CreateBooleanValue(openAlarmView);

                    string json = obj.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onLiveSearchResult) {{ window.PoeBridge.onLiveSearchResult({json}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task ResetViewFromAlarmAsync()
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string script = "if (window.viewController && window.viewController.getCurrentView() === 'alarm') { window.viewController.switchView('evaluate'); }";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task ShowLiveSearchViewAsync()
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string script = @"
if (window.viewController) { window.viewController.switchView('live'); }
var inp = document.getElementById('liveSearchInput');
if (inp) {
    window.__lastFocusedInput = inp;
    inp.focus();
}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task<string> GetClipboardTextReliableAsync()
        {
            string clipboardText = string.Empty;

            // 1. Try UWP Clipboard API first inside isolated try/catch (throws UnauthorizedAccessException if GameBar isn't foreground)
            try
            {
                var tcs = new TaskCompletionSource<string>();
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        var content = Clipboard.GetContent();
                        if (content != null && content.Contains(StandardDataFormats.Text))
                        {
                            string txt = await content.GetTextAsync();
                            tcs.TrySetResult(txt?.Trim() ?? string.Empty);
                            return;
                        }
                    }
                    catch { }
                    tcs.TrySetResult(string.Empty);
                });
                clipboardText = await tcs.Task;
            }
            catch { }

            // 2. Fallback to Win32 HotkeyDaemon clipboard reader via AppService (immune to UWP foreground window restrictions)
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                try
                {
                    clipboardText = (await AppServiceManager.Instance.GetClipboardTextFromDaemonAsync())?.Trim() ?? string.Empty;
                }
                catch { }
            }

            // 3. Fallback to last known clipboard cached from daemon
            if (string.IsNullOrWhiteSpace(clipboardText) && !string.IsNullOrWhiteSpace(AppServiceManager.Instance.LastKnownClipboardText))
            {
                clipboardText = AppServiceManager.Instance.LastKnownClipboardText.Trim();
            }

            return clipboardText ?? string.Empty;
        }

        public async Task PasteClipboardToLiveSearchAsync(string targetInputId = "liveSearchInput")
        {
            if (_webView == null) return;

            string clipboardText = await GetClipboardTextReliableAsync();
            await BridgeLogger.LogBridgeMessageAsync($"[Bridge] PasteClipboardToLiveSearchAsync: target='{targetInputId}', length={clipboardText.Length}");

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string jsonVal = JsonValue.CreateStringValue(clipboardText ?? string.Empty).Stringify();
                    string jsonId = JsonValue.CreateStringValue(string.IsNullOrWhiteSpace(targetInputId) ? "liveSearchInput" : targetInputId).Stringify();

                    string script = $@"
(function(text, targetId) {{
    var trimmed = (text || '').trim();
    var el = (targetId && document.getElementById(targetId)) ||
             (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA') ? document.activeElement : null) ||
             window.__lastFocusedInput ||
             document.getElementById('liveSearchInput');
    if (!trimmed) {{
        if (window.PoeBridge && typeof window.PoeBridge.showToast === 'function') {{
            window.PoeBridge.showToast('Clipboard is empty.');
        }}
        return;
    }}
    if (el) {{
        el.value = trimmed;
        window.__lastFocusedInput = el;
        try {{ el.focus(); }} catch (e) {{}}
        try {{
            var ev = document.createEvent('Event');
            ev.initEvent('input', true, true);
            el.dispatchEvent(ev);
        }} catch (e2) {{}}
    }}
    if (window.PoeBridge && typeof window.PoeBridge.showToast === 'function') {{
        window.PoeBridge.showToast('Pasted link from clipboard');
    }}
}})({jsonVal}, {jsonId});";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    await BridgeLogger.LogBridgeMessageAsync($"[Bridge] PasteClipboardToLiveSearchAsync script error: {ex.Message}");
                }
            });
        }

        public async Task SendCharToWebInputAsync(char ch)
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string jsonChar = JsonValue.CreateStringValue(ch.ToString()).Stringify();
                    string script = $"if (window.__poeHandleHostChar) {{ window.__poeHandleHostChar({jsonChar}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task SendSpecialKeyToWebInputAsync(string keyName)
        {
            if (_webView == null || string.IsNullOrEmpty(keyName)) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string jsonKey = JsonValue.CreateStringValue(keyName).Stringify();
                    string script = $"if (window.__poeHandleHostKey) {{ window.__poeHandleHostKey({jsonKey}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task ClearLiveHistoryInWebAsync()
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string script = "if (window.PoeBridge && window.PoeBridge.onLiveSearchHistoryCleared) { window.PoeBridge.onLiveSearchHistoryCleared(); }";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        public async Task UpdateMonitoredSearchNameInWebAsync(string queryId, string itemName)
        {
            if (_webView == null || string.IsNullOrWhiteSpace(itemName)) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string jsonId = JsonValue.CreateStringValue(queryId ?? string.Empty).Stringify();
                    string jsonName = JsonValue.CreateStringValue(itemName.Trim()).Stringify();
                    string script = $@"
(function(qId, name) {{
    if (!name) return;
    var upper = name.toUpperCase();
    if (window.liveSearchController && typeof window.liveSearchController.updateMonitoredSearchName === 'function') {{
        window.liveSearchController.updateMonitoredSearchName(qId, upper);
        return;
    }}
    if (window.liveSearchController && Array.isArray(window.liveSearchController.monitoredSearches)) {{
        var updated = false;
        for (var i = 0; i < window.liveSearchController.monitoredSearches.length; i++) {{
            var m = window.liveSearchController.monitoredSearches[i];
            if ((qId && m.id === qId) || (!qId && m.queryText && m.queryText.indexOf('TRADE #') === 0)) {{
                m.queryText = upper;
                updated = true;
                if (qId) break;
            }}
        }}
        if (updated && typeof window.liveSearchController.renderMonitoredSearches === 'function') {{
            window.liveSearchController.renderMonitoredSearches();
        }}
    }}
}})({jsonId}, {jsonName});";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        /// <summary>
        /// Updates rate limit status shown in the bottom bar.
        /// </summary>
        public async Task UpdateRateLimitStatusAsync(string statusText, bool isThrottled)
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string safeText = statusText?.Replace("'", "\\'") ?? "NORMAL";
                    string script = $"if (window.PoeBridge && window.PoeBridge.onRateLimitUpdated) {{ window.PoeBridge.onRateLimitUpdated('{safeText}', {(isThrottled ? "true" : "false")}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        /// <summary>
        /// Sends real-time countdown timer tick and pause state to WebView2 visualization.
        /// </summary>
        public async Task SendTimerTickToWebAsync(double remainingSeconds, double totalDurationSeconds, bool isPaused)
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    double rem = Math.Max(0, Math.Round(remainingSeconds, 1));
                    double tot = Math.Max(1.0, Math.Round(totalDurationSeconds, 1));
                    string script = $"if (window.PoeBridge && window.PoeBridge.onTimerTick) {{ window.PoeBridge.onTimerTick({rem.ToString(CultureInfo.InvariantCulture)}, {tot.ToString(CultureInfo.InvariantCulture)}, {(isPaused ? "true" : "false")}); }}";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        /// <summary>
        /// Injects C#-managed pointer hover, input focus, paste, and keyboard listeners into WebView2.
        /// </summary>
        public async Task InjectHoverTrackerScriptAsync()
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string script = @"
(function() {
  if (!document.getElementById('poeInputFixStyle')) {
    var st = document.createElement('style');
    st.id = 'poeInputFixStyle';
    st.textContent = 'input, textarea, select { user-select: text !important; -webkit-user-select: text !important; -ms-user-select: text !important; pointer-events: auto !important; cursor: text !important; } input[type=number]::-webkit-outer-spin-button, input[type=number]::-webkit-inner-spin-button { -webkit-appearance: none !important; margin: 0 !important; display: none !important; } .item-banner, .alert-banner { align-items: center !important; justify-content: center !important; text-align: center !important; } .item-name, .item-base, #alertItemName, #alertItemBase { text-align: center !important; width: 100% !important; } .top-bar.is-hidden, .overlay-wrapper.is-alarm-view .top-bar { display: none !important; }';
    (document.head || document.documentElement).appendChild(st);
  }

  if (window.viewController && !window.viewController.__poeAlarmNavPatched) {
    window.viewController.__poeAlarmNavPatched = true;
    var origSwitchView = window.viewController.switchView;
    window.viewController.switchView = function(mode) {
      origSwitchView.call(this, mode);
      var tb = document.querySelector('.top-bar');
      if (tb) tb.classList.toggle('is-hidden', mode === 'alarm');
      var wr = document.getElementById('overlayWrapper');
      if (wr) wr.classList.toggle('is-alarm-view', mode === 'alarm');
    };
  }

  function getTargetInput() {
    var active = document.activeElement;
    if (active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA') && active.type !== 'checkbox' && active.type !== 'button') {
      return active;
    }
    if (window.__lastFocusedInput && document.body.contains(window.__lastFocusedInput)) {
      return window.__lastFocusedInput;
    }
    var livePanel = document.getElementById('viewLivePanel');
    if (livePanel && !livePanel.classList.contains('is-hidden')) {
      return document.getElementById('liveSearchInput');
    }
    return null;
  }

  window.__poeHandleHostChar = function(ch) {
    if (!ch) return;
    if (Date.now() - (window.__lastNativeKeyTime || 0) < 45) return;
    var el = getTargetInput();
    if (!el) return;
    try { el.focus(); } catch (e) {}
    var val = el.value || '';
    try {
      if (typeof el.selectionStart === 'number' && typeof el.selectionEnd === 'number') {
        var start = el.selectionStart;
        var end = el.selectionEnd;
        el.value = val.substring(0, start) + ch + val.substring(end);
        el.selectionStart = el.selectionEnd = start + ch.length;
      } else {
        el.value = val + ch;
      }
    } catch (e2) {
      el.value = val + ch;
    }
    try {
      var ev = document.createEvent('Event');
      ev.initEvent('input', true, true);
      el.dispatchEvent(ev);
    } catch (e3) {}
  };

  window.__poeHandleHostKey = function(keyName) {
    if (!keyName) return;
    if (Date.now() - (window.__lastNativeKeyTime || 0) < 45) return;
    var el = getTargetInput();
    if (!el) return;
    try { el.focus(); } catch (e) {}
    var val = el.value || '';
    if (keyName === 'Back') {
      try {
        if (typeof el.selectionStart === 'number' && typeof el.selectionEnd === 'number') {
          var s = el.selectionStart;
          var e = el.selectionEnd;
          if (s !== e) {
            el.value = val.substring(0, s) + val.substring(e);
            el.selectionStart = el.selectionEnd = s;
          } else if (s > 0) {
            el.value = val.substring(0, s - 1) + val.substring(s);
            el.selectionStart = el.selectionEnd = s - 1;
          }
        } else if (val.length > 0) {
          el.value = val.substring(0, val.length - 1);
        }
      } catch (ex) {
        if (val.length > 0) el.value = val.substring(0, val.length - 1);
      }
    } else if (keyName === 'Delete') {
      try {
        if (typeof el.selectionStart === 'number' && typeof el.selectionEnd === 'number') {
          var s2 = el.selectionStart;
          var e2 = el.selectionEnd;
          if (s2 !== e2) {
            el.value = val.substring(0, s2) + val.substring(e2);
            el.selectionStart = el.selectionEnd = s2;
          } else if (s2 < val.length) {
            el.value = val.substring(0, s2) + val.substring(s2 + 1);
            el.selectionStart = el.selectionEnd = s2;
          }
        } else {
          el.value = '';
        }
      } catch (ex2) {}
    } else if (keyName === 'SelectAll') {
      try { el.select(); } catch (ex3) {}
    } else if (keyName === 'Enter') {
      if (el.id === 'liveSearchInput' && window.liveSearchController && typeof window.liveSearchController.addNewLiveSearch === 'function') {
        window.liveSearchController.addNewLiveSearch();
      }
    } else if (keyName === 'ArrowUp' && el.id === 'liveMaxPriceInput') {
      if (window.liveSearchController && typeof window.liveSearchController.stepMaxPrice === 'function') {
        window.liveSearchController.stepMaxPrice(1);
      }
    } else if (keyName === 'ArrowDown' && el.id === 'liveMaxPriceInput') {
      if (window.liveSearchController && typeof window.liveSearchController.stepMaxPrice === 'function') {
        window.liveSearchController.stepMaxPrice(-1);
      }
    }
  };

  var pasteBtn = document.getElementById('livePasteBtn');
  if (pasteBtn && !pasteBtn.__poeBound) {
    pasteBtn.__poeBound = true;
    pasteBtn.addEventListener('click', function(ev) {
      ev.stopPropagation();
      if (window.PoeBridge && typeof window.PoeBridge.notifyHost === 'function') {
        window.PoeBridge.notifyHost({ action: 'live_paste', targetId: 'liveSearchInput' });
      }
    });
  }

  var clearBtn = document.getElementById('liveClearHistoryBtn');
  if (clearBtn && !clearBtn.__poeBound) {
    clearBtn.__poeBound = true;
    clearBtn.addEventListener('click', function(ev) {
      ev.stopPropagation();
      if (window.liveSearchController && typeof window.liveSearchController.clearHistory === 'function') {
        window.liveSearchController.clearHistory(false);
      } else if (window.PoeBridge && typeof window.PoeBridge.notifyHost === 'function') {
        var listEl = document.getElementById('liveResultsList');
        if (listEl) listEl.innerHTML = '';
        window.PoeBridge.notifyHost({ action: 'live_clear_history' });
      }
    });
  }

  if (window.__poeInputTrackerInstalled) return;
  window.__poeInputTrackerInstalled = true;

  var lastMoveSent = 0;
  var lastX = -1;
  var lastY = -1;
  var isPointerInside = false;

  function postHover(isHovered, eventType, x, y) {
    if (window.PoeBridge && typeof window.PoeBridge.notifyHost === 'function') {
      window.PoeBridge.notifyHost({
        action: 'set_hover',
        isHovered: isHovered,
        eventType: eventType,
        x: typeof x === 'number' ? x : 0,
        y: typeof y === 'number' ? y : 0
      });
    }
  }

  document.addEventListener('pointerenter', function(e) {
    isPointerInside = true;
    lastX = e.clientX;
    lastY = e.clientY;
    postHover(true, 'enter', e.clientX, e.clientY);
  }, true);

  document.addEventListener('pointermove', function(e) {
    var now = Date.now();
    var dx = Math.abs(e.clientX - lastX);
    var dy = Math.abs(e.clientY - lastY);
    var hostPaused = !!(window.PoeBridge && window.PoeBridge.hostTimerPaused);

    if (!isPointerInside || (!hostPaused && (dx >= 2 || dy >= 2) && (now - lastMoveSent >= 50))) {
      isPointerInside = true;
      lastX = e.clientX;
      lastY = e.clientY;
      lastMoveSent = now;
      postHover(true, 'move', e.clientX, e.clientY);
    }
  }, { passive: true, capture: true });

  document.addEventListener('pointerdown', function(e) {
    isPointerInside = true;
    postHover(true, 'interact', e.clientX, e.clientY);

    var t = e.target;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT')) {
      if (t.type !== 'checkbox' && t.type !== 'button') {
        window.__lastFocusedInput = t;
      }
      try { t.focus(); } catch (err) {}
      if (window.PoeBridge && typeof window.PoeBridge.notifyHost === 'function') {
        window.PoeBridge.notifyHost({ action: 'focus_input', id: t.id || '' });
      }
    }
  }, true);

  document.addEventListener('focusin', function(e) {
    var t = e.target;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA') && t.type !== 'checkbox' && t.type !== 'button') {
      window.__lastFocusedInput = t;
    }
  }, true);

  document.addEventListener('input', function() {
    window.__lastNativeKeyTime = Date.now();
  }, true);

  document.addEventListener('wheel', function(e) {
    isPointerInside = true;
    postHover(true, 'interact', e.clientX, e.clientY);
  }, { passive: true, capture: true });

  document.addEventListener('keydown', function(e) {
    if (e.key !== 'Escape') {
      postHover(true, 'interact', lastX >= 0 ? lastX : 0, lastY >= 0 ? lastY : 0);
    }
    if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 'v' || e.key === 'V' || e.keyCode === 86)) {
      var targetInp = getTargetInput();
      if (targetInp && window.PoeBridge && typeof window.PoeBridge.notifyHost === 'function') {
        e.preventDefault();
        window.__lastNativeKeyTime = Date.now();
        window.PoeBridge.notifyHost({ action: 'live_paste', targetId: targetInp.id || 'liveSearchInput' });
        return;
      }
    }
    if (!e.ctrlKey && !e.altKey && !e.metaKey) {
      window.__lastNativeKeyTime = Date.now();
    }
  }, true);

  document.documentElement.addEventListener('mouseleave', function(e) {
    if (e.relatedTarget === null) {
      isPointerInside = false;
      postHover(false, 'leave', e.clientX, e.clientY);
    }
  });
})();";
                    await _webView.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }

        #endregion
    }
}
