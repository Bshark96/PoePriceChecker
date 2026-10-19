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

        // Auto-minimize countdown & hover detection management
        private readonly DispatcherTimer _countdownTimer;
        private readonly PoeHoverDetector _hoverDetector;
        private double _totalDurationSeconds = 12.0;
        private double _remainingSeconds = 12.0;
        private bool _isOverlayVisible = true;
        private DateTime _settingsOpenedTime = DateTime.MinValue;

        public Widget1()
        {
            this.InitializeComponent();
            UiComponentFactory.SuppressContextMenu(this);

            _hoverDetector = new PoeHoverDetector();
            _hoverDetector.HoverStateChanged += OnHoverDetectorStateChanged;

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _countdownTimer.Tick += OnCountdownTimerTick;

            this.Loaded += Widget1_Loaded;
            this.Unloaded += Widget1_Unloaded;

            // Treat direct XAML pointer press as user interaction without subscribing to PointerExited
            // (which falsely fires when the pointer enters the child WebView2 HWND airspace).
            this.PointerPressed += (s, e) => _hoverDetector.OnUserInteraction();

            // Bind CoreWindow events for reliable restoration and keyboard input forwarding
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;
            Window.Current.Activated += Window_Activated;
        }

        private void OnHoverDetectorStateChanged(bool isHovered)
        {
            _bridgeService?.SendTimerTickToWebAsync(_remainingSeconds, _totalDurationSeconds, isHovered);
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                try { PreviewWebView.Focus(FocusState.Programmatic); } catch { }
                if (!_isOverlayVisible)
                {
                    _isOverlayVisible = true;
                    StartAutoMinimizeCountdown();
                }
            }
            else
            {
                _isOverlayVisible = false;
                _hoverDetector.ResetOnDismiss();
                PreviewWebView.Visibility = Visibility.Collapsed;
                _countdownTimer.Stop();
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                PreviewWebView.Visibility = Visibility.Visible;
                PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                try { PreviewWebView.Focus(FocusState.Programmatic); } catch { }
                if (!_isOverlayVisible)
                {
                    _isOverlayVisible = true;
                    StartAutoMinimizeCountdown();
                }
                else if (e.WindowActivationState == CoreWindowActivationState.PointerActivated)
                {
                    _hoverDetector.OnUserInteraction();
                }
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
                return;
            }

            bool isAltDown = (sender.GetKeyState(VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool isCtrlDown = (sender.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            if (isAltDown && !isCtrlDown && args.VirtualKey == VirtualKey.A)
            {
                args.Handled = true;
                PoeSettingsManager.Instance.IsSettingsOpen = false;
                _ = _bridgeService?.ShowLiveSearchViewAsync();
                _ = RestoreWidgetAsync();
                return;
            }

            if (isCtrlDown && !isAltDown)
            {
                if (args.VirtualKey == VirtualKey.V)
                {
                    args.Handled = true;
                    _hoverDetector.OnUserInteraction();
                    _ = _bridgeService?.PasteClipboardToLiveSearchAsync();
                    return;
                }
                if (args.VirtualKey == VirtualKey.A)
                {
                    args.Handled = true;
                    _hoverDetector.OnUserInteraction();
                    _ = _bridgeService?.SendSpecialKeyToWebInputAsync("SelectAll");
                    return;
                }
            }

            if (!isCtrlDown && !isAltDown)
            {
                if (args.VirtualKey == VirtualKey.Back)
                {
                    _hoverDetector.OnUserInteraction();
                    _ = _bridgeService?.SendSpecialKeyToWebInputAsync("Back");
                }
                else if (args.VirtualKey == VirtualKey.Delete)
                {
                    _hoverDetector.OnUserInteraction();
                    _ = _bridgeService?.SendSpecialKeyToWebInputAsync("Delete");
                }
                else if (args.VirtualKey == VirtualKey.Enter)
                {
                    _hoverDetector.OnUserInteraction();
                    _ = _bridgeService?.SendSpecialKeyToWebInputAsync("Enter");
                }
            }
        }

        private void CoreWindow_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            bool isAltDown = (sender.GetKeyState(VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool isCtrlDown = (sender.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            if (isAltDown || isCtrlDown) return;

            char ch = (char)args.KeyCode;
            if (ch >= 32 && ch != 127)
            {
                _hoverDetector.OnUserInteraction();
                _ = _bridgeService?.SendCharToWebInputAsync(ch);
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

            // Initialize Web Bridge
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
                    _hoverDetector.OnUserInteraction();
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

                _bridgeService.OnHoverStateChanged += (isHovered, eventType, x, y) =>
                {
                    _hoverDetector.OnPointerEvent(isHovered, eventType, x, y);
                };

                _bridgeService.OnUserInteracted += () =>
                {
                    _hoverDetector.OnUserInteraction();
                };

                _bridgeService.OnInputFocusRequested += async (inputId) =>
                {
                    _hoverDetector.OnUserInteraction();
                    try
                    {
                        PreviewWebView?.Focus(FocusState.Programmatic);
                    }
                    catch { }
                    await AppServiceManager.Instance.RequestFocusWidgetUnderCursorAsync();
                };

                _bridgeService.OnOpenBrowserRequested += async (url) =>
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await Launcher.LaunchUriAsync(new Uri(url));
                    }
                };

                _bridgeService.OnLiveSearchAdded += async (id, titleOrUrl, maxPrice, currency) =>
                {
                    await AddLiveSearchQueryAsync(id, titleOrUrl, maxPrice, currency);
                };

                _bridgeService.OnLiveSearchRemoved += (id) =>
                {
                    PoeLiveSearchClient.Instance.RemoveQuery(id);
                };

                _bridgeService.OnLiveSearchHistoryCleared += () =>
                {
                    PoeLiveSearchClient.Instance.ClearHistory();
                };

                _bridgeService.OnLiveSearchViewRequested += async () =>
                {
                    PoeSettingsManager.Instance.IsSettingsOpen = false;
                    await RestoreWidgetAsync();
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
            Window.Current.CoreWindow.CharacterReceived -= CoreWindow_CharacterReceived;

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
            });
        }

        private async void OnSettingsSaved(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                LoadSettings();
                if (_currentItem != null)
                {
                    if (_currentItem.Modifiers != null)
                    {
                        foreach (var mod in _currentItem.Modifiers)
                        {
                            if (!mod.IsUnscalable && mod.NumberValue.HasValue)
                            {
                                mod.MinRoll = PoeModifierParser.ApplyTolerance(mod.NumberValue.Value);
                            }
                        }
                    }
                    if (_bridgeService != null)
                    {
                        await _bridgeService.SendItemToWebAsync(_currentItem);
                    }
                }
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

                if (PreviewWebView.CoreWebView2.Settings != null)
                {
                    PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                }

                try
                {
                    await PreviewWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled", "{\"cacheDisabled\":true}");
                }
                catch { }

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

                    string navUrl = $"https://poe.local/index.html?t={DateTime.UtcNow.Ticks}";
                    await BridgeLogger.LogBridgeMessageAsync($"Navigating to {navUrl}");
                    PreviewWebView.Source = new Uri(navUrl);
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
            if (args.IsSuccess && _bridgeService != null)
            {
                await _bridgeService.InjectHoverTrackerScriptAsync();
                if (_currentItem != null)
                {
                    await _bridgeService.SendItemToWebAsync(_currentItem);
                }
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
                            break;

                        case "hideout":
                            string cmd = json.ContainsKey("command") ? json.GetNamedString("command") : string.Empty;
                            CopyTextToClipboard(cmd);
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
                        case "live_add":
                            string addQueryId = json.ContainsKey("id") && json.GetNamedValue("id").ValueType == JsonValueType.String
                                ? json.GetNamedString("id")
                                : string.Empty;
                            string rawUrl = json.ContainsKey("url") && json.GetNamedValue("url").ValueType == JsonValueType.String
                                ? json.GetNamedString("url")
                                : string.Empty;
                            string queryTitle = !string.IsNullOrWhiteSpace(rawUrl)
                                ? rawUrl
                                : (json.ContainsKey("name") ? json.GetNamedString("name") : (json.ContainsKey("title") ? json.GetNamedString("title") : string.Empty));
                            double? maxP = json.ContainsKey("maxPrice") && json.GetNamedValue("maxPrice").ValueType == JsonValueType.Number
                                ? (double?)json.GetNamedNumber("maxPrice")
                                : null;
                            string curr = json.ContainsKey("currency") ? json.GetNamedString("currency") : "Chaos";
                            await AddLiveSearchQueryAsync(addQueryId, queryTitle, maxP, curr);
                            break;

                        case "delete_live":
                        case "live_remove":
                            string queryId = json.ContainsKey("id") ? json.GetNamedString("id") : string.Empty;
                            if (!string.IsNullOrEmpty(queryId))
                            {
                                PoeLiveSearchClient.Instance.RemoveQuery(queryId);
                            }
                            break;

                        case "live_paste":
                        case "paste_live":
                            if (_bridgeService != null)
                            {
                                await _bridgeService.PasteClipboardToLiveSearchAsync();
                            }
                            break;

                        case "live_clear_history":
                        case "clear_live_history":
                            PoeLiveSearchClient.Instance.ClearHistory();
                            break;

                        case "open_live_search":
                        case "show_live_search":
                            if (_bridgeService != null)
                            {
                                await _bridgeService.ShowLiveSearchViewAsync();
                            }
                            await RestoreWidgetAsync();
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
                        else if (command.Equals("ShowLiveSearch", StringComparison.OrdinalIgnoreCase) ||
                                 command.Equals("LiveSearch", StringComparison.OrdinalIgnoreCase))
                        {
                            await BridgeLogger.LogBridgeMessageAsync($"[Widget1] Opening Live Search view from {hotkey} hotkey...");
                            if (_bridgeService != null)
                            {
                                await _bridgeService.ShowLiveSearchViewAsync();
                            }
                        }

                        // Restore window & restart countdown
                        PoeSettingsManager.Instance.IsSettingsOpen = false;
                        await RestoreWidgetAsync();
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

                        int? pMin = null;
                        int? pMax = null;
                        if (pObj.ContainsKey("min") && pObj.GetNamedValue("min").ValueType == JsonValueType.String &&
                            double.TryParse(pObj.GetNamedString("min"), NumberStyles.Any, CultureInfo.InvariantCulture, out double dMin))
                        {
                            pMin = (int)Math.Round(dMin);
                        }
                        if (pObj.ContainsKey("max") && pObj.GetNamedValue("max").ValueType == JsonValueType.String &&
                            double.TryParse(pObj.GetNamedString("max"), NumberStyles.Any, CultureInfo.InvariantCulture, out double dMax))
                        {
                            pMax = (int)Math.Round(dMax);
                        }

                        if (label.IndexOf("Quality", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterQualityActive = active;
                            item.FilterQualityMin = pMin ?? (active && item.Quality > 0 ? (int?)item.Quality : null);
                            item.FilterQualityMax = pMax;
                        }
                        else if (label.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterSocketsActive = active;
                            item.FilterSocketsMin = pMin ?? (active && item.SocketCount > 0 ? (int?)item.SocketCount : null);
                            item.FilterSocketsMax = pMax;
                        }
                        else if (label.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterLinksActive = active;
                            item.FilterLinksMin = pMin ?? (active && item.LinkCount > 0 ? (int?)item.LinkCount : null);
                            item.FilterLinksMax = pMax;
                        }
                        else if (label.IndexOf("Map Tier", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterMapTierActive = active;
                            item.FilterMapTierMin = pMin ?? (active && item.MapTier > 0 ? (int?)item.MapTier : null);
                            item.FilterMapTierMax = pMax ?? (active && item.MapTier > 0 ? (int?)item.MapTier : null);
                        }
                        else if (label.IndexOf("Item Level", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterItemLevelActive = active;
                            item.FilterItemLevelMin = pMin ?? (active && item.ItemLevel > 0 ? (int?)item.ItemLevel : null);
                            item.FilterItemLevelMax = pMax;
                        }
                        else if (label.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 || label.IndexOf("Gem Level", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            item.FilterGemLevelActive = active;
                            item.FilterGemLevelMin = pMin ?? (active && item.GemLevel > 0 ? (int?)item.GemLevel : null);
                            item.FilterGemLevelMax = pMax;
                        }
                    }
                }

                if (filters.ContainsKey("corrupted") && filters.GetNamedValue("corrupted").ValueType == JsonValueType.String)
                {
                    string corrOpt = filters.GetNamedString("corrupted");
                    item.CorruptedFilterOption = string.IsNullOrWhiteSpace(corrOpt) ? "any" : corrOpt;
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
                await PoeNinjaClient.Instance.EnsureItemOverviewLoadedAsync(item, league);
                var ninjaBenchmark = PoeNinjaClient.Instance.GetBenchmark(item);
                if (ninjaBenchmark != null && ninjaBenchmark.Found && _bridgeService != null)
                {
                    await _bridgeService.SendNinjaPriceToWebAsync(ninjaBenchmark.FormattedText, ninjaBenchmark.DustText);
                }

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
                    string priceSummary = (ninjaBenchmark != null && ninjaBenchmark.Found && !string.IsNullOrWhiteSpace(ninjaBenchmark.FormattedText))
                        ? ninjaBenchmark.FormattedText
                        : searchResult.SummaryText;
                    await _bridgeService.SendTradeResultsToWebAsync(searchResult, priceSummary);
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
                    await _bridgeService.SendTradeResultsToWebAsync(errResult, errResult.SummaryText);
                }
            }
        }

        private async Task AddLiveSearchQueryAsync(string id, string titleOrUrl, double? maxPrice, string currency)
        {
            if (string.IsNullOrWhiteSpace(titleOrUrl)) return;
            try
            {
                string trimmed = titleOrUrl.Trim();
                var urlInfo = PoeUrlParser.Parse(trimmed);
                string defaultLeague = PoeSettingsManager.Instance.SelectedLeague;
                if (string.IsNullOrWhiteSpace(defaultLeague)) defaultLeague = "Standard";

                var query = new PoeLiveSearchQuery
                {
                    Id = !string.IsNullOrWhiteSpace(id) ? id : Guid.NewGuid().ToString("N"),
                    Title = trimmed,
                    MaxPrice = maxPrice,
                    Currency = currency ?? "Chaos",
                    League = urlInfo.IsValid && !string.IsNullOrWhiteSpace(urlInfo.League) ? urlInfo.League : defaultLeague,
                    RawUrl = urlInfo.IsValid ? urlInfo.RawUrl : trimmed,
                    SearchId = urlInfo.IsValid ? urlInfo.SearchId : trimmed
                };

                await PoeLiveSearchClient.Instance.AddQueryAsync(query);
            }
            catch { }
        }

        private bool IsWidgetMinimized()
        {
            if (!_isOverlayVisible || PreviewWebView == null || PreviewWebView.Visibility != Visibility.Visible)
            {
                return true;
            }

            if (_widget != null && (_widget.WindowState == XboxGameBarWidgetWindowState.Minimized || !_widget.Visible))
            {
                return true;
            }

            return false;
        }

        private async void OnLiveItemReceived(object sender, LiveItemEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (e?.Listing == null) return;

                bool wasMinimized = IsWidgetMinimized();

                // Push real-time listing into Web UI; only open Alarm view automatically if widget was minimized
                if (_bridgeService != null)
                {
                    await _bridgeService.PushLiveListingToWebAsync(e.Listing, wasMinimized);
                }

                // Restore/activate widget when minimized so user sees the automatic live search alarm
                if (wasMinimized)
                {
                    await RestoreWidgetAsync();
                }
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
            _isOverlayVisible = true;
            PreviewWebView.Visibility = Visibility.Visible;
            PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
            try { PreviewWebView.Focus(FocusState.Programmatic); } catch { }
            StartAutoMinimizeCountdown();

            if (_widgetControl != null)
            {
                try
                {
                    await _widgetControl.ActivateAsync("Widget1");
                }
                catch { }
            }
        }

        public async void DismissOverlay()
        {
            _isOverlayVisible = false;
            _hoverDetector.ResetOnDismiss();
            _countdownTimer.Stop();
            if (_bridgeService != null)
            {
                await _bridgeService.ResetViewFromAlarmAsync();
            }
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
            _hoverDetector.ResetOnRestore();
            _totalDurationSeconds = PoeSettingsManager.Instance.AutoDismissDurationSeconds > 0 
                ? (double)PoeSettingsManager.Instance.AutoDismissDurationSeconds 
                : 12.0;
            _remainingSeconds = _totalDurationSeconds;
            _countdownTimer.Stop();

            if (PoeSettingsManager.Instance.AutoDismissEnabled)
            {
                _countdownTimer.Start();
            }

            _bridgeService?.SendTimerTickToWebAsync(_remainingSeconds, _totalDurationSeconds, _hoverDetector.IsHovered);
        }

        private void OnCountdownTimerTick(object sender, object e)
        {
            if (PoeSettingsManager.Instance.IsSettingsOpen) return;

            if (_hoverDetector.IsHovered)
            {
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
                    _isOverlayVisible = false;
                    _hoverDetector.ResetOnDismiss();
                    _ = _bridgeService?.ResetViewFromAlarmAsync();
                    PreviewWebView.Visibility = Visibility.Collapsed;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = -9999, Y = -9999 };
                    _countdownTimer.Stop();
                }
                else
                {
                    PreviewWebView.Visibility = Visibility.Visible;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = 0, Y = 0 };
                    if (!_isOverlayVisible)
                    {
                        _isOverlayVisible = true;
                        StartAutoMinimizeCountdown();
                    }
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
                    if (!_isOverlayVisible)
                    {
                        _isOverlayVisible = true;
                        StartAutoMinimizeCountdown();
                    }
                }
                else
                {
                    _isOverlayVisible = false;
                    _hoverDetector.ResetOnDismiss();
                    _ = _bridgeService?.ResetViewFromAlarmAsync();
                    PreviewWebView.Visibility = Visibility.Collapsed;
                    PreviewWebView.RenderTransform = new TranslateTransform { X = -9999, Y = -9999 };
                    _countdownTimer.Stop();
                }
            });
        }

        #endregion
    }
}
