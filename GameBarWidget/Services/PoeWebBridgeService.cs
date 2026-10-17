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
        public event Action<string, double?, string> OnLiveSearchAdded;
        public event Action<string> OnLiveSearchRemoved;
        public event Action<string, double, string, int> OnAlarmRuleAdded;
        public event Action OnDismissRequested;
        public event Action<string> OnOpenBrowserRequested;
        public event Func<Task> OnEvaluateRequested;
        public event Action<bool> OnHoverStateChanged;
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
                            OnWebReady?.Invoke();
                            break;

                        case "whisper":
                            string whisperText = json.ContainsKey("text") ? json.GetNamedString("text") : string.Empty;
                            string seller = json.ContainsKey("seller") ? json.GetNamedString("seller") : string.Empty;
                            CopyTextToClipboard(whisperText);
                            OnWhisperCopied?.Invoke(seller, whisperText);
                            break;

                        case "hideout":
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

                        case "search":
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
                            string selectedPreset = json.ContainsKey("key") ? json.GetNamedString("key") : string.Empty;
                            OnPresetChanged?.Invoke(selectedPreset);
                            break;

                        case "live_add":
                            string lName = json.ContainsKey("name") ? json.GetNamedString("name") : string.Empty;
                            double? lPrice = json.ContainsKey("maxPrice") && json.GetNamedValue("maxPrice").ValueType == JsonValueType.Number 
                                ? (double?)json.GetNamedNumber("maxPrice") : null;
                            string lCurr = json.ContainsKey("currency") ? json.GetNamedString("currency") : "Chaos";
                            OnLiveSearchAdded?.Invoke(lName, lPrice, lCurr);
                            break;

                        case "live_remove":
                            string lId = json.ContainsKey("id") ? json.GetNamedString("id") : string.Empty;
                            OnLiveSearchRemoved?.Invoke(lId);
                            break;

                        case "alarm_add":
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
                            OnHoverStateChanged?.Invoke(isHovered);
                            break;

                        case "open_browser":
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

                    var propArray = new JsonArray();
                    if (item.Properties != null)
                    {
                        foreach (var kvp in item.Properties)
                        {
                            var pObj = new JsonObject();
                            pObj["label"] = JsonValue.CreateStringValue(kvp.Key);
                            pObj["val"] = JsonValue.CreateStringValue(kvp.Value?.ToString() ?? string.Empty);
                            pObj["active"] = JsonValue.CreateBooleanValue(true);
                            propArray.Add(pObj);
                        }
                    }
                    payload["properties"] = propArray;

                    var modArray = new JsonArray();
                    if (item.ExplicitMods != null)
                    {
                        foreach (var m in item.ExplicitMods)
                        {
                            var mObj = new JsonObject();
                            mObj["text"] = JsonValue.CreateStringValue(m.Text ?? string.Empty);
                            mObj["min"] = JsonValue.CreateStringValue(m.Value.ToString(CultureInfo.InvariantCulture));
                            mObj["max"] = JsonValue.CreateStringValue(m.Value.ToString(CultureInfo.InvariantCulture));
                            mObj["roll"] = JsonValue.CreateStringValue(m.Tier != null ? $"T{m.Tier}" : string.Empty);
                            mObj["isUnscalable"] = JsonValue.CreateBooleanValue(m.IsUnscalable);
                            mObj["active"] = JsonValue.CreateBooleanValue(!string.IsNullOrEmpty(m.StatId));
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

        /// <summary>
        /// Sends official PoE 1 trade search results into WebView2.
        /// </summary>
        public async Task SendTradeResultsToWebAsync(TradeSearchResult searchResult)
        {
            if (_webView == null || searchResult == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var root = new JsonObject();
                    root["totalCount"] = JsonValue.CreateNumberValue(searchResult.TotalListings);
                    root["summary"] = JsonValue.CreateStringValue(searchResult.SummaryText ?? string.Empty);

                    var listArray = new JsonArray();
                    if (searchResult.Listings != null)
                    {
                        foreach (var l in searchResult.Listings)
                        {
                            var itemObj = new JsonObject();
                            string safeSeller = !string.IsNullOrWhiteSpace(l.AccountName) ? l.AccountName : (!string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : "Exile");
                            string safeChar = !string.IsNullOrWhiteSpace(l.CharacterIgn) ? l.CharacterIgn : safeSeller;

                            itemObj["seller"] = JsonValue.CreateStringValue(safeSeller);
                            itemObj["character"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["char"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["ign"] = JsonValue.CreateStringValue(safeChar);
                            itemObj["priceVal"] = JsonValue.CreateStringValue(l.PriceAmount.ToString(CultureInfo.InvariantCulture));
                            itemObj["priceType"] = JsonValue.CreateStringValue(l.PriceCurrency == "divine" ? "Divine" : "Chaos");
                            itemObj["status"] = JsonValue.CreateStringValue(l.OnlineStatus == "online" ? "online" : "afk");
                            itemObj["age"] = JsonValue.CreateStringValue(l.AgeText ?? "RECENT");
                            itemObj["goldVal"] = JsonValue.CreateNumberValue(450);
                            itemObj["itemName"] = JsonValue.CreateStringValue(l.ItemName ?? string.Empty);
                            itemObj["hideoutToken"] = JsonValue.CreateStringValue(l.HideoutToken ?? string.Empty);

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

        /// <summary>
        /// Pushes a real-time live alert listing to WebView2.
        /// </summary>
        public async Task PushLiveListingToWebAsync(string itemName, string seller, string character, string price, string currency, string age, int goldCost)
        {
            if (_webView == null) return;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var obj = new JsonObject();
                    string safeSeller = !string.IsNullOrWhiteSpace(seller) ? seller : "Exile";
                    string safeChar = !string.IsNullOrWhiteSpace(character) ? character : safeSeller;

                    obj["name"] = JsonValue.CreateStringValue(itemName);
                    obj["seller"] = JsonValue.CreateStringValue(safeSeller);
                    obj["character"] = JsonValue.CreateStringValue(safeChar);
                    obj["char"] = JsonValue.CreateStringValue(safeChar);
                    obj["ign"] = JsonValue.CreateStringValue(safeChar);
                    obj["price"] = JsonValue.CreateStringValue(price);
                    obj["curr"] = JsonValue.CreateStringValue(currency);
                    obj["age"] = JsonValue.CreateStringValue(age);
                    obj["gold"] = JsonValue.CreateNumberValue(goldCost);

                    string json = obj.Stringify();
                    string script = $"if (window.PoeBridge && window.PoeBridge.onLiveSearchResult) {{ window.PoeBridge.onLiveSearchResult({json}); }}";
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

        #endregion
    }
}
