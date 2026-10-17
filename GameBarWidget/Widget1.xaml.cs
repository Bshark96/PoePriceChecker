using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;
using GameBarWidget.Services;
using GameBarWidget.Services.Parsing;

using GameBarWidget.Design;

namespace GameBarWidget
{
    /// <summary>
    /// Pure C# Backend Host for the Xbox Game Bar PoE 1 Price Checker Web UI.
    /// Manages Game Bar lifecycle, hotkeys, clipboard listener, official trade API queries,
    /// poe.ninja rate benchmarks, and live search engine, bridging directly into EdgeHTML WebView.
    /// </summary>
    public sealed partial class Widget1 : Page
    {
        private XboxGameBarWidget _widget;
        private XboxGameBarWidgetControl _widgetControl;
        private PoeWebBridgeService _bridgeService;
        private PoeItem _currentItem;

        // Auto-minimize countdown management
        private readonly DispatcherTimer _countdownTimer;
        private double _totalDurationSeconds = 12.0;
        private double _remainingSeconds = 12.0;
        private bool _isTimerPaused = false;
        private DateTime _settingsOpenedTime = DateTime.MinValue;
        private DateTime _lastRestoredTime = DateTime.MinValue;

        public Widget1()
        {
            this.InitializeComponent();
            UiComponentFactory.SuppressContextMenu(this);

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _countdownTimer.Tick += OnCountdownTimerTick;

            this.Loaded += Widget1_Loaded;
            this.Unloaded += Widget1_Unloaded;

            // Pause countdown on mouse interaction (filtering synthetic restore hover events)
            this.PointerEntered += (s, e) => { if ((DateTime.UtcNow - _lastRestoredTime).TotalMilliseconds >= 800) _isTimerPaused = true; };
            this.PointerMoved += (s, e) => { if ((DateTime.UtcNow - _lastRestoredTime).TotalMilliseconds >= 800) _isTimerPaused = true; };
            this.PointerPressed += (s, e) => { if ((DateTime.UtcNow - _lastRestoredTime).TotalMilliseconds >= 800) _isTimerPaused = true; };
            this.PointerReleased += (s, e) => { if ((DateTime.UtcNow - _lastRestoredTime).TotalMilliseconds >= 800) _isTimerPaused = true; };
            this.PointerExited += (s, e) => _isTimerPaused = false;

            // Bind CoreWindow events for reliable restoration
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;
            Window.Current.Activated += Window_Activated;
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                _isTimerPaused = false;
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                StartAutoMinimizeCountdown();
            }
            else
            {
                PreviewWebView.Visibility = Visibility.Collapsed;
                _countdownTimer.Stop();
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                _isTimerPaused = false;
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                StartAutoMinimizeCountdown();
            }
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape)
            {
                args.Handled = true;
                if (!PoeSettingsManager.Instance.IsSettingsOpen)
                {
                    DismissOverlay();
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _widget = e.Parameter as XboxGameBarWidget;

            if (_widget != null)
            {
                _widgetControl = new XboxGameBarWidgetControl(_widget);
                _widget.WindowStateChanged += OnWidgetWindowStateChanged;
                _widget.VisibleChanged += OnWidgetVisibleChanged;
                _widget.SettingsClicked += OnWidgetSettingsClicked;
                _widget.PinnedChanged += OnWidgetPinnedChanged;
            }
        }

        private async void OnWidgetSettingsClicked(XboxGameBarWidget sender, object args)
        {
            _settingsOpenedTime = DateTime.UtcNow;
            PoeSettingsManager.Instance.IsSettingsOpen = true;
            try
            {
                await sender.ActivateSettingsAsync();
            }
            catch { }
        }

        private async void Widget1_Loaded(object sender, RoutedEventArgs e)
        {
            await BridgeLogger.LogStartupAsync();
            AppServiceManager.Instance.MessageReceived += OnAppServiceMessageReceived;
            PoeSettingsManager.Instance.SettingsClosed += OnSettingsClosed;
            PoeSettingsManager.Instance.SettingsSaved += OnSettingsSaved;

            PoeLiveSearchClient.Instance.ItemReceived += OnLiveItemReceived;
            PoeLiveSearchClient.Instance.StatusChanged += OnLiveStatusChanged;

            // Load full official stat database and canonical item dictionary in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await PoeItemParser.InitializeStatsDatabaseAsync();
                    await BridgeLogger.LogBridgeMessageAsync("[Widget1] Stats database initialized in background.");
                }
                catch (Exception ex)
                {
                    await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Stats database background init note: {ex.Message}");
                }
            });

            // Navigate WebView to Web UI immediately
            LoadPreviewHtml();

            if (RootGrid != null)
            {
                RootGrid.PointerEntered += (s, e) => { _isTimerPaused = true; };
                RootGrid.PointerExited += (s, e) => { _isTimerPaused = false; };
            }

            // Initialize EdgeHTML Web Bridge
            if (PreviewWebView != null)
            {
                _bridgeService = new PoeWebBridgeService(PreviewWebView);
                _bridgeService.Initialize();

                _bridgeService.OnEvaluateRequested += async () =>
                {
                    await BridgeLogger.LogBridgeMessageAsync("[Widget1] OnEvaluateRequested from Web Bridge");
                    if (_currentItem != null)
                    {
                        await QueryMarketAsync(_currentItem);
                    }
                    else
                    {
                        await TryLoadFromClipboardAsync();
                    }
                };

                _bridgeService.OnSearchRequested += async (preset, filters) =>
                {
                    if (_currentItem != null)
                    {
                        ApplyWebFiltersToItem(_currentItem, filters);
                        await QueryMarketAsync(_currentItem);
                    }
                };

                _bridgeService.OnDismissRequested += () =>
                {
                    DismissOverlay();
                };

                _bridgeService.OnHoverStateChanged += (isHovered) =>
                {
                    if (isHovered && (DateTime.UtcNow - _lastRestoredTime).TotalMilliseconds < 800)
                    {
                        return;
                    }
                    _isTimerPaused = isHovered;
                };

                _bridgeService.OnWhisperCopied += (seller, text) =>
                {
                    PoeAudioService.PlayNotificationSound();
                };

                _bridgeService.OnHideoutRequested += (seller) =>
                {
                    PoeAudioService.PlayNotificationSound();
                };

                _bridgeService.OnOpenBrowserRequested += async (url) =>
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await Launcher.LaunchUriAsync(new Uri(url));
                    }
                };

                _bridgeService.OnLiveSearchAdded += async (title, maxPrice, currency) =>
                {
                    await AddLiveSearchQueryAsync(title, maxPrice, currency);
                };

                _bridgeService.OnLiveSearchRemoved += (id) =>
                {
                    PoeLiveSearchClient.Instance.RemoveQuery(id);
                };

                _bridgeService.OnWebReady += async () =>
                {
                    if (_currentItem != null)
                    {
                        await _bridgeService.SendItemToWebAsync(_currentItem);
                    }
                };
            }

            // Sync settings
            LoadSettings();

            // Validate and sync live league
            try
            {
                var leagues = await PoeOfficialTradeClient.Instance.GetLeaguesAsync();
                if (leagues != null && leagues.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(PoeSettingsManager.Instance.SelectedLeague))
                    {
                        PoeSettingsManager.Instance.SelectedLeague = leagues[0];
                    }
                }
            }
            catch { }

            // Ensure background hotkey daemon is running
            await EnsureDaemonStartedAsync();

            // Check if clipboard already contains an item on launch
            await TryLoadFromClipboardAsync();

            StartAutoMinimizeCountdown();
        }

        private void Widget1_Unloaded(object sender, RoutedEventArgs e)
        {
            _bridgeService?.Detach();
            AppServiceManager.Instance.MessageReceived -= OnAppServiceMessageReceived;
            PoeSettingsManager.Instance.SettingsClosed -= OnSettingsClosed;
            PoeSettingsManager.Instance.SettingsSaved -= OnSettingsSaved;

            PoeLiveSearchClient.Instance.ItemReceived -= OnLiveItemReceived;
            PoeLiveSearchClient.Instance.StatusChanged -= OnLiveStatusChanged;

            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;

            if (_widget != null)
            {
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
                _widget.SettingsClicked -= OnWidgetSettingsClicked;
                _widget.PinnedChanged -= OnWidgetPinnedChanged;
            }

            _countdownTimer.Stop();
        }

        private async void OnSettingsClosed(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                LoadSettings();
                await RestoreWidgetAsync();
                StartAutoMinimizeCountdown();
            });
        }

        private async void OnSettingsSaved(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                LoadSettings();
            });
        }

        private void LoadSettings()
        {
            var settings = PoeSettingsManager.Instance;
            _totalDurationSeconds = settings.AutoDismissDurationSeconds;
            _remainingSeconds = _totalDurationSeconds;
        }

        private async void LoadPreviewHtml()
        {
            if (PreviewWebView == null) return;
            try
            {
                await BridgeLogger.LogBridgeMessageAsync("Initializing WinUI 2 WebView2...");
                await PreviewWebView.EnsureCoreWebView2Async();
                await BridgeLogger.LogBridgeMessageAsync("WebView2 EnsureCoreWebView2Async completed successfully!");

                PreviewWebView.WebMessageReceived += PreviewWebView_WebMessageReceived;
                PreviewWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                PreviewWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                string installedPath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                await BridgeLogger.LogBridgeMessageAsync($"Mapping virtual host 'poe.local' to: {installedPath}");

                try
                {
                    PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "poe.local",
                        installedPath,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                    await BridgeLogger.LogBridgeMessageAsync("Navigating to https://poe.local/index.html");
                    PreviewWebView.Source = new Uri("https://poe.local/index.html");
                }
                catch (Exception exMapping)
                {
                    await BridgeLogger.LogBridgeMessageAsync($"VirtualHostMapping error: {exMapping.Message}. Falling back to NavigateToString.");
                    var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync("index.html");
                    string html = await Windows.Storage.FileIO.ReadTextAsync(file);
                    PreviewWebView.NavigateToString(html);
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"WebView2 initialization exception: {ex.Message}");
            }
        }

        #region WinUI 2 WebView2 Bridge Interceptor

        private async void PreviewWebView_WebMessageReceived(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string raw = args.TryGetWebMessageAsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    await ProcessBridgeMessagePayloadAsync(raw);
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"WebMessageReceived exception: {ex.Message}");
            }
        }

        private async void CoreWebView2_NavigationCompleted(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            await BridgeLogger.LogBridgeMessageAsync($"WebView2 NavigationCompleted: IsSuccess={args.IsSuccess}, WebErrorStatus={args.WebErrorStatus}");
            if (args.IsSuccess && _currentItem != null && _bridgeService != null)
            {
                await _bridgeService.SendItemToWebAsync(_currentItem);
            }
        }

        private async void CoreWebView2_NewWindowRequested(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            if (!string.IsNullOrEmpty(args.Uri))
            {
                try
                {
                    await Launcher.LaunchUriAsync(new Uri(args.Uri));
                }
                catch { }
            }
        }

        private async Task ProcessBridgeMessagePayloadAsync(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            // Log every message received by C# to desktop text file
            await BridgeLogger.LogBridgeMessageAsync(raw);

            try
            {
                if (JsonObject.TryParse(raw, out var json))
                {
                    string action = json.ContainsKey("action") ? json.GetNamedString("action") : string.Empty;
                    switch (action.ToLowerInvariant())
                    {
                        case "whisper":
                            string whisperText = json.ContainsKey("text") ? json.GetNamedString("text") : string.Empty;
                            CopyTextToClipboard(whisperText);
                            PoeAudioService.PlayNotificationSound();
                            break;

                        case "hideout":
                            string cmd = json.ContainsKey("command") ? json.GetNamedString("command") : string.Empty;
                            CopyTextToClipboard(cmd);
                            PoeAudioService.PlayNotificationSound();
                            break;

                        case "search":
                            if (_currentItem != null)
                            {
                                await QueryMarketAsync(_currentItem);
                            }
                            break;

                        case "dismiss":
                        case "minimize":
                            DismissOverlay();
                            break;

                        case "open_browser":
                            string url = json.ContainsKey("url") ? json.GetNamedString("url") : string.Empty;
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                await Launcher.LaunchUriAsync(new Uri(url));
                            }
                            break;

                        case "add_live":
                            string queryTitle = json.ContainsKey("title") ? json.GetNamedString("title") : string.Empty;
                            double? maxP = json.ContainsKey("maxPrice") ? (double?)json.GetNamedNumber("maxPrice") : null;
                            string curr = json.ContainsKey("currency") ? json.GetNamedString("currency") : "Chaos";
                            await AddLiveSearchQueryAsync(queryTitle, maxP, curr);
                            break;

                        case "delete_live":
                            string queryId = json.ContainsKey("id") ? json.GetNamedString("id") : string.Empty;
                            if (!string.IsNullOrEmpty(queryId))
                            {
                                PoeLiveSearchClient.Instance.RemoveQuery(queryId);
                            }
                            break;
                    }
                }
            }
            catch { }
        }

        #endregion

        #region AppService & Hotkey Backend Listener

        private async Task EnsureDaemonStartedAsync()
        {
            try
            {
                await FullTrustLauncherHelper.LaunchDaemonAsync();
            }
            catch { }
        }

        private async void OnAppServiceMessageReceived(object sender, ValueSet message)
        {
            try
            {
                await BridgeLogger.LogBridgeMessageAsync("[Widget1] OnAppServiceMessageReceived triggered from Daemon.");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    if (message.ContainsKey("Command"))
                    {
                        string command = message["Command"]?.ToString() ?? string.Empty;
                        string hotkey = message.ContainsKey("Hotkey") ? message["Hotkey"]?.ToString() : "";
                        await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Processing Daemon command: {command} (Hotkey: {hotkey})");

                        if (command.Equals("PriceCheck", StringComparison.OrdinalIgnoreCase))
                        {
                            string rawText = message.ContainsKey("RawItemText") ? message["RawItemText"]?.ToString() : string.Empty;
                            if (!string.IsNullOrWhiteSpace(rawText))
                            {
                                await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Evaluating parsed item from hotkey ({rawText.Length} chars)...");
                                var parsed = PoeItemParser.Parse(rawText);
                                await LoadAndEvaluateItemAsync(parsed);
                            }
                        }

                        // Restore window & restart countdown
                        PoeSettingsManager.Instance.IsSettingsOpen = false;
                        await RestoreWidgetAsync();
                        StartAutoMinimizeCountdown();
                    }
                });
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"[Widget1] OnAppServiceMessageReceived exception: {ex.Message}");
            }
        }

        private async Task<bool> TryLoadFromClipboardAsync()
        {
            try
            {
                var content = Clipboard.GetContent();
                if (content.Contains(StandardDataFormats.Text))
                {
                    string text = await content.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) &&
                        (text.StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase) ||
                         text.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase)))
                    {
                        await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Successfully read item from clipboard ({text.Length} chars)");
                        var parsed = PoeItemParser.Parse(text);
                        await LoadAndEvaluateItemAsync(parsed);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"[Widget1] TryLoadFromClipboardAsync note: {ex.Message}");
            }
            return false;
        }

        private async Task LoadAndEvaluateItemAsync(PoeItem item)
        {
            if (item == null) return;
            _currentItem = item;
            await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Evaluating item: '{item.Name ?? item.BaseType}' (Rarity: {item.Rarity})");

            // Push parsed item directly into Web UI
            if (_bridgeService != null)
            {
                await _bridgeService.SendItemToWebAsync(item);
                await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Dispatched item '{item.Name ?? item.BaseType}' to Web UI");
            }
            else
            {
                await BridgeLogger.LogBridgeMessageAsync("[Widget1] _bridgeService is null!");
            }

            // Auto-query official trade API and poe.ninja pricing
            await QueryMarketAsync(item);
        }

        #endregion

        #region Market Queries & Live Search Backend

        private void ApplyWebFiltersToItem(PoeItem item, JsonObject filters)
        {
            if (item == null || filters == null) return;
            try
            {
                if (filters.ContainsKey("modifiers") && filters.GetNamedValue("modifiers").ValueType == JsonValueType.Array)
                {
                    var arr = filters.GetNamedArray("modifiers");
                    var modsList = item.ExplicitMods;
                    int i = 0;
                    foreach (var val in arr)
                    {
                        int currentIdx = i++;
                        if (val.ValueType != JsonValueType.Object) continue;
                        var mObj = val.GetObject();
                        int idx = mObj.ContainsKey("index") ? (int)mObj.GetNamedNumber("index") : currentIdx;
                        bool active = mObj.ContainsKey("active") && mObj.GetNamedBoolean("active");

                        if (idx >= 0 && idx < modsList.Count)
                        {
                            var mod = modsList[idx];
                            mod.IsActive = active;

                            if (mObj.ContainsKey("min") && mObj.GetNamedValue("min").ValueType == JsonValueType.String)
                            {
                                string minStr = mObj.GetNamedString("min");
                                if (double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double minVal))
                                    mod.MinRoll = minVal;
                                else
                                    mod.MinRoll = null;
                            }
                            else if (mObj.ContainsKey("min") && mObj.GetNamedValue("min").ValueType == JsonValueType.Number)
                            {
                                mod.MinRoll = mObj.GetNamedNumber("min");
                            }

                            if (mObj.ContainsKey("max") && mObj.GetNamedValue("max").ValueType == JsonValueType.String)
                            {
                                string maxStr = mObj.GetNamedString("max");
                                if (double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxVal))
                                    mod.MaxRoll = maxVal;
                                else
                                    mod.MaxRoll = null;
                            }
                            else if (mObj.ContainsKey("max") && mObj.GetNamedValue("max").ValueType == JsonValueType.Number)
                            {
                                mod.MaxRoll = mObj.GetNamedNumber("max");
                            }
                        }
                    }
                }

                if (filters.ContainsKey("properties") && filters.GetNamedValue("properties").ValueType == JsonValueType.Array)
                {
                    var pArr = filters.GetNamedArray("properties");
                    foreach (var pVal in pArr)
                    {
                        if (pVal.ValueType != JsonValueType.Object) continue;
                        var pObj = pVal.GetObject();
                        string label = pObj.ContainsKey("label") ? pObj.GetNamedString("label") : string.Empty;
                        bool active = pObj.ContainsKey("active") && pObj.GetNamedBoolean("active");

                        if (label.IndexOf("Quality", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterQualityActive = active;
                        }
                        else if (label.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterSocketsActive = active;
                        }
                        else if (label.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterLinksActive = active;
                        }
                        else if (label.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 || label.IndexOf("Gem Level", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterGemLevelActive = active;
                        }
                    }
                }

                if (filters.ContainsKey("corrupted") && filters.GetNamedValue("corrupted").ValueType == JsonValueType.String)
                {
                    item.CorruptedFilterOption = filters.GetNamedString("corrupted");
                }
            }
            catch { }
        }

        private async Task QueryMarketAsync(PoeItem item)
        {
            if (item == null) return;

            string league = PoeSettingsManager.Instance.SelectedLeague;
            string poesessid = PoeSettingsManager.Instance.PoeSessId;
            await BridgeLogger.LogBridgeMessageAsync($"[Widget1] QueryMarketAsync started for '{item.Name ?? item.BaseType}' on league '{league}'");

            try
            {
                TradeSearchResult searchResult;
                if (item.Rarity == PoeRarity.Currency || item.Rarity == PoeRarity.DivinationCard)
                {
                    double divineRate = PoeNinjaClient.Instance.DivineRate;
                    searchResult = await PoeOfficialTradeClient.Instance.SearchExchangeAsync(item, league, poesessid, divineRate);
                }
                else
                {
                    searchResult = await PoeOfficialTradeClient.Instance.SearchItemAsync(item, league, poesessid);
                }

                int count = searchResult?.Listings?.Count ?? 0;
                await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Official Trade search returned {count} listings (total: {searchResult?.TotalListings ?? 0})");

                // Push search results into Web UI
                if (_bridgeService != null && searchResult != null)
                {
                    await _bridgeService.SendTradeResultsToWebAsync(searchResult);
                    await BridgeLogger.LogBridgeMessageAsync("[Widget1] Dispatched search results to Web UI");
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"[Widget1 Backend] Market query error: {ex.Message}");
                Debug.WriteLine($"[Widget1 Backend] Market query error: {ex.Message}");
                if (_bridgeService != null)
                {
                    var errResult = new TradeSearchResult
                    {
                        TotalListings = 0,
                        SummaryText = $"Error: {ex.Message}",
                        Listings = new List<TradeListing>()
                    };
                    await _bridgeService.SendTradeResultsToWebAsync(errResult);
                }
            }
        }

        private async Task AddLiveSearchQueryAsync(string title, double? maxPrice, string currency)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            try
            {
                var query = new PoeLiveSearchQuery
                {
                    Title = title.Trim(),
                    MaxPrice = maxPrice,
                    Currency = currency ?? "Chaos",
                    League = PoeSettingsManager.Instance.SelectedLeague
                };

                await PoeLiveSearchClient.Instance.AddQueryAsync(query);
            }
            catch { }
        }

        private async void OnLiveItemReceived(object sender, LiveItemEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (e?.Listing == null) return;

                // Play instant alert audio chime
                PoeAudioService.PlayLiveAlertSound();

                // Push real-time listing into Web UI
                if (_bridgeService != null)
                {
                    await _bridgeService.PushLiveListingToWebAsync(
                        e.Listing.ItemName,
                        e.Listing.AccountName,
                        e.Listing.CharacterIgn,
                        e.Listing.PriceAmount.ToString(CultureInfo.InvariantCulture),
                        e.Listing.PriceCurrency,
                        e.Listing.AgeText ?? "RECENT",
                        e.Listing.GoldFee
                    );
                }

                // Restore/activate widget so user sees the live alert
                await RestoreWidgetAsync();
                StartAutoMinimizeCountdown();
            });
        }

        private void OnLiveStatusChanged(object sender, LiveSearchStatusEventArgs e)
        {
            // Status logged for live search connections
            LiveSearchLogger.Log($"LiveSearch: {e?.Message}");
        }

        #endregion

        #region Clipboard & Window State Management

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



        public async Task RestoreWidgetAsync()
        {
            _isTimerPaused = false;
            PreviewWebView.Visibility = Visibility.Visible;
            PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };

            if (_widgetControl != null)
            {
                try
                {
                    await _widgetControl.ActivateAsync("Widget1");
                }
                catch { }
            }
            StartAutoMinimizeCountdown();
        }

        public async void DismissOverlay()
        {
            PreviewWebView.Visibility = Visibility.Collapsed;
            PreviewWebView.RenderTransform = new TranslateTransform { X = -9999, Y = -9999 };

            if (_widgetControl != null)
            {
                try
                {
                    await _widgetControl.MinimizeAsync("Widget1");
                }
                catch { }
            }
        }

        private void StartAutoMinimizeCountdown()
        {
            _lastRestoredTime = DateTime.UtcNow;
            _totalDurationSeconds = PoeSettingsManager.Instance.AutoDismissDurationSeconds > 0 
                ? (double)PoeSettingsManager.Instance.AutoDismissDurationSeconds 
                : 12.0;
            _remainingSeconds = _totalDurationSeconds;
            _isTimerPaused = false;
            _countdownTimer.Stop();

            if (PoeSettingsManager.Instance.AutoDismissEnabled)
            {
                _countdownTimer.Start();
            }

            _bridgeService?.SendTimerTickToWebAsync(_remainingSeconds, _totalDurationSeconds, _isTimerPaused);
        }

        private void OnCountdownTimerTick(object sender, object e)
        {
            if (PoeSettingsManager.Instance.IsSettingsOpen) return;

            if (_isTimerPaused)
            {
                _bridgeService?.SendTimerTickToWebAsync(_remainingSeconds, _totalDurationSeconds, true);
                return;
            }

            _remainingSeconds -= 0.1;
            if (_remainingSeconds <= 0)
            {
                _remainingSeconds = 0;
                _countdownTimer.Stop();
                _bridgeService?.SendTimerTickToWebAsync(0, _totalDurationSeconds, false);
                DismissOverlay();
                return;
            }

            _bridgeService?.SendTimerTickToWebAsync(_remainingSeconds, _totalDurationSeconds, false);
        }

        private async void OnWidgetWindowStateChanged(XboxGameBarWidget sender, object args)
        {
            if (sender == null) return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender.WindowState == XboxGameBarWidgetWindowState.Minimized)
                {
                    PreviewWebView.Visibility = Visibility.Collapsed;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = -9999, Y = -9999 };
                    _countdownTimer.Stop();
                }
                else
                {
                    PreviewWebView.Visibility = Visibility.Visible;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                    StartAutoMinimizeCountdown();
                }
            });
        }

        private void OnWidgetPinnedChanged(XboxGameBarWidget sender, object args)
        {
            // Auto-minimize operates independently of pinned state
        }

        private async void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender != null && sender.Visible)
                {
                    PreviewWebView.Visibility = Visibility.Visible;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                    StartAutoMinimizeCountdown();
                }
                else
                {
                    PreviewWebView.Visibility = Visibility.Collapsed;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = -9999, Y = -9999 };
                    _countdownTimer.Stop();
                }
            });
        }

        #endregion
    }
}
