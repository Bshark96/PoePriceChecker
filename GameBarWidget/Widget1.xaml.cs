using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;
using GameBarWidget.Services;
using GameBarWidget.Design;

namespace GameBarWidget
{
    public sealed partial class Widget1 : Page
    {
        private XboxGameBarWidget _widget;
        private XboxGameBarWidgetControl _widgetControl;
        private readonly DispatcherTimer _countdownTimer;
        private double _totalDurationSeconds = 10;
        private double _remainingSeconds = 10;
        private bool _isTimerPaused = false;
        private DateTime _settingsOpenedTime = DateTime.MinValue;
        private PoeItem _currentItem;
        private string _activeSearchUrl = string.Empty;
        private readonly HashSet<string> _collapsedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SOCKETS",
            "QUALITY",
            "PSEUDO STATS",
            "PSEUDO"
        };

        public Widget1()
        {
            this.InitializeComponent();
            UiComponentFactory.SuppressContextMenu(this);

            if (LiveSearchUrlBox != null) NumpadFlyoutBuilder.AttachNumpadContextMenu(LiveSearchUrlBox);
            if (LiveSearchMaxPriceBox != null) NumpadFlyoutBuilder.AttachNumpadContextMenu(LiveSearchMaxPriceBox);
            if (LiveSearchDebugLogText != null) NumpadFlyoutBuilder.AttachNumpadContextMenu(LiveSearchDebugLogText);

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _countdownTimer.Tick += OnCountdownTimerTick;

            this.Loaded += Widget1_Loaded;
            this.Unloaded += Widget1_Unloaded;

            // Pause countdown on mouse hover or click interaction
            this.PointerEntered += (s, e) => _isTimerPaused = true;
            this.PointerMoved += (s, e) => _isTimerPaused = true;
            this.PointerPressed += (s, e) => _isTimerPaused = true;
            this.PointerReleased += (s, e) => _isTimerPaused = true;
            this.PointerExited += (s, e) => _isTimerPaused = false;

            // Bind ESC key to dismiss
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
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
            AppServiceManager.Instance.MessageReceived += OnAppServiceMessageReceived;
            PoeSettingsManager.Instance.SettingsClosed += OnSettingsClosed;
            PoeSettingsManager.Instance.SettingsSaved += OnSettingsSaved;

            PoeLiveSearchClient.Instance.ItemReceived += OnLiveItemReceived;
            PoeLiveSearchClient.Instance.StatusChanged += OnLiveStatusChanged;

            LiveSearchLogger.OnLogAdded += OnLiveSearchLogAdded;
            if (LiveSearchDebugLogText != null)
            {
                LiveSearchDebugLogText.Text = LiveSearchLogger.GetFullLog();
            }

            // Load full official stat database (21k+ entries)
            await PoeItemParser.InitializeStatsDatabaseAsync();

            // Load saved settings into UI
            LoadSettingsIntoUi();
            UpdateMasterToggleButton();

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

            // Check if clipboard already contains an item, otherwise await hotkey
            bool loaded = await TryLoadFromClipboardAsync();
            if (!loaded)
            {
                ShowAwaitingItemState();
            }

            StartAutoMinimizeCountdown();
        }

        private void Widget1_Unloaded(object sender, RoutedEventArgs e)
        {
            AppServiceManager.Instance.MessageReceived -= OnAppServiceMessageReceived;
            PoeSettingsManager.Instance.SettingsClosed -= OnSettingsClosed;
            PoeSettingsManager.Instance.SettingsSaved -= OnSettingsSaved;

            PoeLiveSearchClient.Instance.ItemReceived -= OnLiveItemReceived;
            PoeLiveSearchClient.Instance.StatusChanged -= OnLiveStatusChanged;

            LiveSearchLogger.OnLogAdded -= OnLiveSearchLogAdded;

            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;

            if (_widget != null)
            {
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
            }

            _countdownTimer.Stop();
        }

        private async void OnSettingsClosed(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                LoadSettingsIntoUi();
                await RestoreWidgetAsync();
                StartAutoMinimizeCountdown();
            });
        }

        private async void OnSettingsSaved(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                LoadSettingsIntoUi();
            });
        }

        private async Task EnsureDaemonStartedAsync()
        {
            try
            {
                await FullTrustLauncherHelper.LaunchDaemonAsync();
            }
            catch { }
        }

        private void LoadSettingsIntoUi()
        {
            var settings = PoeSettingsManager.Instance;
            _totalDurationSeconds = settings.AutoDismissDurationSeconds;
            _remainingSeconds = _totalDurationSeconds;
        }

        #region AppService Handling

        private async void OnAppServiceMessageReceived(object sender, ValueSet message)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (message.ContainsKey("Command"))
                {
                    string command = message["Command"]?.ToString() ?? string.Empty;

                    if (command.Equals("LiveSearch", StringComparison.OrdinalIgnoreCase) || command.Equals("ShowLiveSearch", StringComparison.OrdinalIgnoreCase))
                    {
                        SwitchToLiveSearchView();
                    }
                    else if (command.Equals("PriceCheck", StringComparison.OrdinalIgnoreCase))
                    {
                        SwitchToPriceCheckView();
                        string rawText = message.ContainsKey("RawItemText") ? message["RawItemText"]?.ToString() : string.Empty;
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            var parsed = PoeItemParser.Parse(rawText);
                            DisplayItem(parsed);

                            if (PoeSettingsManager.Instance.AutoSearchOfficialTrade)
                            {
                                await QueryMarketAsync(parsed);
                            }
                        }
                        else
                        {
                            await TryLoadFromClipboardAsync();
                        }
                    }
                    else
                    {
                        await TryLoadFromClipboardAsync();
                    }

                    // Restore window & restart countdown
                    PoeSettingsManager.Instance.IsSettingsOpen = false;
                    await RestoreWidgetAsync();
                    StartAutoMinimizeCountdown();
                }
            });
        }

        #region Multi-View Navigation & Live Search Handlers

        private readonly List<PoeLiveSearchQuery> _activeQueries = new List<PoeLiveSearchQuery>();

        private void PriceCheckTabBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchToPriceCheckView();
        }

        private void LiveSearchTabBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchToLiveSearchView();
        }

        private void SwitchToPriceCheckView()
        {
            TopHeaderGrid.Visibility = Visibility.Visible;
            PriceCheckView.Visibility = Visibility.Visible;
            LiveSearchView.Visibility = Visibility.Collapsed;
            LiveNotificationView.Visibility = Visibility.Collapsed;

            PriceCheckTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
            PriceCheckTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));

            LiveSearchTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 48, 68));
            LiveSearchTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
        }

        private void SwitchToLiveSearchView()
        {
            TopHeaderGrid.Visibility = Visibility.Visible;
            PriceCheckView.Visibility = Visibility.Collapsed;
            LiveSearchView.Visibility = Visibility.Visible;
            LiveNotificationView.Visibility = Visibility.Collapsed;

            LiveSearchTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
            LiveSearchTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));

            PriceCheckTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 48, 68));
            PriceCheckTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
        }

        private void SwitchToLiveNotificationView()
        {
            TopHeaderGrid.Visibility = Visibility.Collapsed;
            PriceCheckView.Visibility = Visibility.Collapsed;
            LiveSearchView.Visibility = Visibility.Collapsed;
            LiveNotificationView.Visibility = Visibility.Visible;

            PriceCheckTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 48, 68));
            PriceCheckTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));

            LiveSearchTabBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 48, 68));
            LiveSearchTabBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
        }

        private async void PasteLiveUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = Clipboard.GetContent();
                if (package != null && package.Contains(StandardDataFormats.Text))
                {
                    string text = (await package.GetTextAsync())?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        LiveSearchUrlBox.Text = text;
                    }
                }
            }
            catch { }
        }

        private async void AddLiveSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            string rawUrl = LiveSearchUrlBox.Text?.Trim() ?? string.Empty;
            var urlInfo = PoeUrlParser.Parse(rawUrl);

            if (!urlInfo.IsValid)
            {
                LiveSearchConnectionStatus.Text = urlInfo.ErrorMessage ?? "Invalid trade URL";
                LiveSearchConnectionStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
                return;
            }

            double? maxPrice = null;
            if (double.TryParse(LiveSearchMaxPriceBox.Text?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedMax) && parsedMax > 0)
            {
                maxPrice = parsedMax;
            }

            string currency = "divine";
            if (LiveSearchCurrencyCombo.SelectedIndex == 1)
            {
                currency = "chaos";
            }

            LiveSearchConnectionStatus.Text = "Connecting live search...";
            LiveSearchConnectionStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 251, 191, 36));

            string shortLabel = urlInfo.SearchId.Length > 12 ? (urlInfo.SearchId.Substring(0, 10) + "...") : urlInfo.SearchId;

            var query = new PoeLiveSearchQuery
            {
                League = urlInfo.League,
                SearchId = urlInfo.SearchId,
                RawUrl = urlInfo.RawUrl,
                Label = $"{urlInfo.League}/{shortLabel}",
                MaxPriceAmount = maxPrice,
                MaxPriceCurrency = currency,
                IsActive = true
            };

            _activeQueries.Add(query);
            RenderActiveQueries();

            PoeLiveSearchClient.Instance.StartQuery(query);
            LiveSearchUrlBox.Text = string.Empty;
            LiveSearchMaxPriceBox.Text = string.Empty;

            LiveSearchConnectionStatus.Text = "Search active & streaming";
            LiveSearchConnectionStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
        }

        private void RenderActiveQueries()
        {
            ActiveQueriesContainer.Children.Clear();

            if (_activeQueries.Count == 0)
            {
                ActiveQueriesContainer.Children.Add(new TextBlock
                {
                    Text = "No active live searches. Paste a trade link above to monitor listings.",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)),
                    Margin = new Thickness(2, 2, 0, 2)
                });
                return;
            }

            foreach (var q in _activeQueries)
            {
                var card = LiveSearchCardBuilder.BuildActiveQueryRow(
                    q,
                    (query, isActive) =>
                    {
                        if (isActive) PoeLiveSearchClient.Instance.StartQuery(query);
                        else PoeLiveSearchClient.Instance.StopQuery(query.Id);
                    },
                    (query) =>
                    {
                        PoeLiveSearchClient.Instance.StopQuery(query.Id);
                        _activeQueries.Remove(query);
                        RenderActiveQueries();
                    });

                ActiveQueriesContainer.Children.Add(card);
            }
        }

        private void OnLiveItemReceived(object sender, LiveItemEventArgs e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (e.Query != null && e.Listing != null)
                {
                    // Play instant live search alert audio notification
                    PoeAudioService.PlayLiveAlertSound();

                    // 1. Update View 2 Live Listings Stream (Normal compact trade log row, max 10 items)
                    if (LiveListingsContainer.Children.Count == 1 && LiveListingsContainer.Children[0] is TextBlock)
                    {
                        LiveListingsContainer.Children.Clear();
                    }

                    var logRow = TradeRowBuilder.BuildTradeListingRow(e.Listing, CopyWhisperToClipboard);
                    LiveListingsContainer.Children.Insert(0, logRow);

                    while (LiveListingsContainer.Children.Count > 10)
                    {
                        LiveListingsContainer.Children.RemoveAt(LiveListingsContainer.Children.Count - 1);
                    }

                    // 2. Update View 3 Live Notification Card Overlay (Show ONLY 1 latest item card with Dismiss action)
                    NotificationCardsContainer.Children.Clear();

                    var notificationCard = LiveSearchCardBuilder.BuildLiveListingNotificationCard(
                        e.Query,
                        e.Listing,
                        CopyWhisperToClipboard,
                        () =>
                        {
                            DismissOverlay();
                            ClearNotificationBtn_Click(null, null);
                            SwitchToPriceCheckView();
                        });

                    if (notificationCard is FrameworkElement feCard)
                    {
                        feCard.VerticalAlignment = VerticalAlignment.Stretch;
                        feCard.HorizontalAlignment = HorizontalAlignment.Stretch;
                    }

                    NotificationCardsContainer.Children.Add(notificationCard);

                    // 3. Automatically restore window and switch to View 3 ONLY IF window was minimized/hidden
                    bool isMinimizedOrHidden = (_widget == null || !_widget.Visible);
                    if (isMinimizedOrHidden)
                    {
                        await RestoreWidgetAsync();
                        SwitchToLiveNotificationView();
                        StartAutoMinimizeCountdown();
                    }
                }
            });
        }

        private void ClearNotificationBtn_Click(object sender, RoutedEventArgs e)
        {
            NotificationCardsContainer.Children.Clear();
            NotificationCardsContainer.Children.Add(new TextBlock
            {
                Text = "No active live notifications. New incoming items will display here automatically.",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)),
                Margin = new Thickness(4)
            });
        }

        private void OnLiveStatusChanged(object sender, LiveSearchStatusEventArgs e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                LiveSearchConnectionStatus.Text = e.StatusText;
                LiveSearchConnectionStatus.Foreground = e.IsConnected
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 251, 191, 36));
            });
        }

        private void ClearLiveListingsBtn_Click(object sender, RoutedEventArgs e)
        {
            LiveListingsContainer.Children.Clear();
            LiveListingsContainer.Children.Add(new TextBlock
            {
                Text = "Incoming live search items will appear here in real time.",
                FontSize = 9,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)),
                Margin = new Thickness(2, 2, 0, 2)
            });
        }

        private void OnLiveSearchLogAdded(string entry)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (LiveSearchDebugLogText != null)
                {
                    LiveSearchDebugLogText.Text = LiveSearchLogger.GetFullLog();
                }
            });
        }

        private void CopyDebugLogBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(LiveSearchLogger.GetFullLog());
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
            catch { }
        }

        private void ClearDebugLogBtn_Click(object sender, RoutedEventArgs e)
        {
            LiveSearchLogger.Clear();
            if (LiveSearchDebugLogText != null)
            {
                LiveSearchDebugLogText.Text = string.Empty;
            }
        }

        #endregion

        private async Task<bool> TryLoadFromClipboardAsync()
        {
            try
            {
                var dataPackage = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (dataPackage != null && dataPackage.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    string text = await dataPackage.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) && (text.Contains("Item Class:") || text.Contains("Rarity:") || text.Contains("Requirements:")))
                    {
                        var parsed = PoeItemParser.Parse(text);
                        DisplayItem(parsed);
                        if (PoeSettingsManager.Instance.AutoSearchOfficialTrade)
                        {
                            await QueryMarketAsync(parsed);
                        }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region UI Rendering & Pricing Logic

        private void ShowAwaitingItemState()
        {
            StatusStyleHelper.ApplyAwaitingItemStyles(ItemNameText, ItemBaseTypeText, ItemRarityText, ItemRarityBadge);
            ItemLevelText.Text = string.Empty;
            ItemSocketText.Text = string.Empty;

            BenchmarkDivineText.Text = "-";
            BenchmarkChaosText.Text = "No benchmark";
            OpenBrowserBtn.Visibility = Visibility.Collapsed;

            ModContainer.Children.Clear();
            ModContainer.Children.Add(new TextBlock
            {
                Text = "No item loaded yet.",
                FontSize = 10,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
            });

            ListingsContainer.Children.Clear();
            ListingsContainer.Children.Add(new TextBlock
            {
                Text = "No trade queries performed yet. Use in-game hotkey over an item.",
                FontSize = 10,
                Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
            });
        }

        private void DisplayItem(PoeItem item)
        {
            if (item == null) return;
            _currentItem = item;

            // Header info
            ItemNameText.Text = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.BaseType;
            if (item.Rarity == PoeRarity.Gem)
            {
                ItemBaseTypeText.Text = $"Level {item.GemLevel} | Quality +{item.Quality}%";
            }
            else
            {
                ItemBaseTypeText.Text = item.BaseType;
            }
            ItemLevelText.Text = item.ItemLevel > 0 ? $"ilvl: {item.ItemLevel}" : string.Empty;

            if (item.SocketCount > 0)
            {
                ItemSocketText.Text = $"{item.SocketCount}S {item.LinkCount}L ({item.SocketRaw})";
            }
            else
            {
                ItemSocketText.Text = string.Empty;
            }

            // Initialize Sockets filter state (default inactive/ignored)
            if (item.SocketCount > 0 && !item.FilterSocketsActive && !item.FilterSocketsMin.HasValue && !item.FilterSocketsMax.HasValue)
            {
                item.FilterSocketsMin = item.SocketCount;
                item.FilterSocketsActive = false;
            }

            // Initialize Links filter state (default active only if 5 or 6 link non-unique, matching Awakened standard)
            if (item.LinkCount > 0 && !item.FilterLinksActive && !item.FilterLinksMin.HasValue && !item.FilterLinksMax.HasValue)
            {
                item.FilterLinksMin = item.LinkCount;
                if (item.LinkCount >= 5 && item.Namespace != ItemNamespace.Unique)
                {
                    item.FilterLinksActive = true;
                }
                else
                {
                    item.FilterLinksActive = false;
                }
            }

            // Initialize Quality filter state (default inactive/ignored)
            if (item.Quality > 0 && !item.FilterQualityActive && !item.FilterQualityMin.HasValue && !item.FilterQualityMax.HasValue)
            {
                item.FilterQualityMin = item.Quality;
                item.FilterQualityActive = false;
            }

            ItemRarityText.Text = item.IsCorrupted ? $"{item.Rarity.ToString().ToUpperInvariant()} (CORRUPTED)" : item.Rarity.ToString().ToUpperInvariant();

            // Initialize Corrupted Filter selection
            item.CorruptedFilterOption = item.IsCorrupted ? "true" : "any";
            UpdateCorruptedFilterUI(item.CorruptedFilterOption);

            // Set Rarity badge color
            StatusStyleHelper.ApplyRarityBadgeStyles(item.Rarity, ItemRarityBadge, ItemRarityText);

            // Benchmark
            var benchmark = PoeNinjaClient.Instance.GetBenchmark(item);
            if (benchmark.Found)
            {
                BenchmarkDivineText.Text = $"{benchmark.DivineEquivalent:F1} Div";
                BenchmarkChaosText.Text = $"≈ {benchmark.ChaosEquivalent:N0} Chaos";
            }
            else
            {
                BenchmarkDivineText.Text = "Custom";
                BenchmarkChaosText.Text = "No direct benchmark";
            }

            // Populate Categorized Modifiers
            ModContainer.Children.Clear();

            // Group modifiers into distinct categories
            var pseudoMods = item.PseudoModifiers;
            var implicitMods = new List<ItemModifier>();
            var prefixMods = new List<ItemModifier>();
            var suffixMods = new List<ItemModifier>();
            var generalExplicitMods = new List<ItemModifier>();
            var specialMods = new List<ItemModifier>();

            foreach (var mod in item.Modifiers)
            {
                if (mod.Type == ModifierType.Implicit || mod.Type == ModifierType.Enchant)
                {
                    implicitMods.Add(mod);
                }
                else if (mod.Type == ModifierType.Fractured || mod.Type == ModifierType.Crafted)
                {
                    specialMods.Add(mod);
                }
                else if (mod.IsPrefix || mod.TierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                {
                    prefixMods.Add(mod);
                }
                else if (mod.IsSuffix || mod.TierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    suffixMods.Add(mod);
                }
                else
                {
                    generalExplicitMods.Add(mod);
                }
            }

            int renderedGroups = 0;

            // Jewel Base Type Crafting Filter Group (Awakened PoE Trade standard: deselected exact base by default)
            if (item.Category.StartsWith("jewel", StringComparison.OrdinalIgnoreCase))
            {
                ModContainer.Children.Add(CreateJewelBaseFilterSection(item));
                renderedGroups++;
            }

            // Sockets Group (Count & Links)
            if (item.SocketCount > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateSocketsGroupSection(item));
                renderedGroups++;
            }

            // Quality Group
            bool canHaveQuality = item.Quality > 0 || item.Rarity == PoeRarity.Gem || item.Category.StartsWith("weapon", StringComparison.OrdinalIgnoreCase) || item.Category.StartsWith("armour", StringComparison.OrdinalIgnoreCase) || item.Category == "flask" || item.Category == "map" || item.SocketCount > 0 || (!string.IsNullOrEmpty(item.ItemClass) && (item.ItemClass.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Flask", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0));
            if (canHaveQuality)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateQualityGroupSection(item));
                renderedGroups++;
            }

            // 1. Pseudo Modifiers Group
            if (pseudoMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("PSEUDO STATS", DesignPalette.GetSectionAccentColor("PSEUDO STATS"), pseudoMods));
                renderedGroups++;
            }

            // 2. Implicit & Enchant Group
            if (implicitMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("IMPLICIT & ENCHANT", DesignPalette.GetSectionAccentColor("IMPLICIT & ENCHANT"), implicitMods));
                renderedGroups++;
            }

            // 3. Prefix Modifiers Group
            if (prefixMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("PREFIXES", DesignPalette.GetSectionAccentColor("PREFIXES"), prefixMods));
                renderedGroups++;
            }

            // 4. Suffix Modifiers Group
            if (suffixMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("SUFFIXES", DesignPalette.GetSectionAccentColor("SUFFIXES"), suffixMods));
                renderedGroups++;
            }

            // 5. General Explicit / Gem Properties Group
            if (generalExplicitMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                string groupTitle = item.Rarity == PoeRarity.Gem ? "GEM PROPERTIES" : "EXPLICIT MODIFIERS";
                ModContainer.Children.Add(CreateStatGroupSection(groupTitle, DesignPalette.GetSectionAccentColor(groupTitle), generalExplicitMods));
                renderedGroups++;
            }

            // 6. Fractured & Crafted Group
            if (specialMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("FRACTURED & CRAFTED", DesignPalette.GetSectionAccentColor("FRACTURED & CRAFTED"), specialMods));
                renderedGroups++;
            }

            if (renderedGroups == 0)
            {
                ModContainer.Children.Add(new TextBlock
                {
                    Text = "No numerical modifiers detected.",
                    FontSize = 10,
                    Foreground = DesignPalette.Brush(DesignPalette.TextSecondary)
                });
            }

            UpdateMasterToggleButton();
        }

        private void ToggleCompactModsBtn_Click(object sender, RoutedEventArgs e)
        {
            bool anyExpanded = false;
            foreach (var child in ModContainer.Children)
            {
                if (child is StackPanel sectionPanel && sectionPanel.Children.Count > 1 && sectionPanel.Children[1] is StackPanel itemsPanel)
                {
                    if (itemsPanel.Visibility == Visibility.Visible)
                    {
                        anyExpanded = true;
                        break;
                    }
                }
            }

            bool shouldCollapse = anyExpanded;

            foreach (var child in ModContainer.Children)
            {
                if (child is StackPanel sectionPanel && sectionPanel.Children.Count > 1 && sectionPanel.Children[1] is StackPanel itemsPanel)
                {
                    itemsPanel.Visibility = shouldCollapse ? Visibility.Collapsed : Visibility.Visible;
                    string title = string.Empty;
                    if (sectionPanel.Children[0] is Grid headerGrid && headerGrid.Children.Count > 0 && headerGrid.Children[0] is StackPanel titleStack && titleStack.Children.Count > 1 && titleStack.Children[1] is TextBlock titleText)
                    {
                        title = titleText.Text;
                    }

                    if (shouldCollapse && !string.IsNullOrEmpty(title))
                    {
                        _collapsedGroups.Add(title);
                    }
                    else if (!shouldCollapse && !string.IsNullOrEmpty(title))
                    {
                        _collapsedGroups.Remove(title);
                    }

                    if (sectionPanel.Tag is Action updateVisuals)
                    {
                        updateVisuals();
                    }
                }
            }

            if (!shouldCollapse)
            {
                _collapsedGroups.Clear();
            }

            UpdateMasterToggleButton();
        }

        private void UpdateMasterToggleButton()
        {
            if (ToggleCompactModsBtn == null) return;

            bool anyExpanded = false;
            foreach (var child in ModContainer.Children)
            {
                if (child is StackPanel sectionPanel && sectionPanel.Children.Count > 1 && sectionPanel.Children[1] is StackPanel itemsPanel)
                {
                    if (itemsPanel.Visibility == Visibility.Visible)
                    {
                        anyExpanded = true;
                        break;
                    }
                }
            }

            StatusStyleHelper.ApplyCompactButtonStyles(anyExpanded, ToggleCompactModsBtn);
        }

        private UIElement CreateGenericGroupSection(string title, Windows.UI.Color accentColor, List<UIElement> rows, Func<int> getActiveCount)
        {
            return UiComponentFactory.CreateGenericGroupSection(
                title,
                accentColor,
                rows,
                getActiveCount,
                _collapsedGroups,
                UpdateMasterToggleButton);
        }

        private UIElement CreateStatGroupSection(string title, Windows.UI.Color accentColor, List<ItemModifier> mods)
        {
            var rows = new List<UIElement>();
            foreach (var mod in mods)
            {
                rows.Add(CreateModifierFilterRow(mod));
            }
            return CreateGenericGroupSection(title, accentColor, rows, () =>
            {
                int count = 0;
                foreach (var m in mods)
                {
                    if (m.IsActive) count++;
                }
                return count;
            });
        }

        private UIElement CreateSocketsGroupSection(PoeItem item)
        {
            var rows = new List<UIElement>();

            rows.Add(CreateItemPropertyFilterRow(
                "Sockets",
                "SOCK",
                DesignPalette.SurfaceHeaderExpanded,
                DesignPalette.TextSecondary,
                item.FilterSocketsMin,
                item.FilterSocketsMax,
                item.FilterSocketsActive,
                isActive => item.FilterSocketsActive = isActive,
                min => item.FilterSocketsMin = min,
                max => item.FilterSocketsMax = max,
                () => { },
                6));

            rows.Add(CreateItemPropertyFilterRow(
                "Links",
                "LINK",
                Windows.UI.Color.FromArgb(255, 50, 35, 20),
                DesignPalette.AccentAmber,
                item.FilterLinksMin,
                item.FilterLinksMax,
                item.FilterLinksActive,
                isActive => item.FilterLinksActive = isActive,
                min => item.FilterLinksMin = min,
                max => item.FilterLinksMax = max,
                () => { },
                6));

            return CreateGenericGroupSection(
                "SOCKETS",
                DesignPalette.GetSectionAccentColor("SOCKETS"),
                rows,
                () => (item.FilterSocketsActive ? 1 : 0) + (item.FilterLinksActive ? 1 : 0));
        }

        private UIElement CreateQualityGroupSection(PoeItem item)
        {
            var rows = new List<UIElement>();

            rows.Add(CreateItemPropertyFilterRow(
                "Quality",
                "QUAL",
                Windows.UI.Color.FromArgb(255, 15, 60, 55),
                DesignPalette.AccentTeal,
                item.FilterQualityMin,
                item.FilterQualityMax,
                item.FilterQualityActive,
                isActive => item.FilterQualityActive = isActive,
                min => item.FilterQualityMin = min,
                max => item.FilterQualityMax = max,
                () => { },
                30));

            return CreateGenericGroupSection(
                "QUALITY",
                DesignPalette.GetSectionAccentColor("QUALITY"),
                rows,
                () => item.FilterQualityActive ? 1 : 0);
        }

        private UIElement CreateJewelBaseFilterSection(PoeItem item)
        {
            var rows = new List<UIElement>();
            string baseLabel = !string.IsNullOrWhiteSpace(item.BaseType) ? item.BaseType : item.Name;

            var rowGrid = new Grid
            {
                Padding = new Thickness(4, 2, 4, 2),
                Background = DesignPalette.Brush(DesignPalette.SurfaceHeaderCollapsed)
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var baseCheck = new CheckBox
            {
                IsChecked = item.FilterBaseTypeActive,
                Content = $"Exact Base: {baseLabel}",
                FontSize = 9.5,
                Foreground = DesignPalette.Brush(item.FilterBaseTypeActive ? DesignPalette.TextPrimary : DesignPalette.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            baseCheck.Checked += (s, e) =>
            {
                item.FilterBaseTypeActive = true;
                baseCheck.Foreground = DesignPalette.Brush(DesignPalette.TextPrimary);
            };
            baseCheck.Unchecked += (s, e) =>
            {
                item.FilterBaseTypeActive = false;
                baseCheck.Foreground = DesignPalette.Brush(DesignPalette.TextSecondary);
            };
            checkStack.Children.Add(baseCheck);
            Grid.SetColumn(checkStack, 0);
            rowGrid.Children.Add(checkStack);

            var badgeBorder = new Border
            {
                Background = DesignPalette.Brush(Windows.UI.Color.FromArgb(255, 30, 41, 59)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = new TextBlock
            {
                Text = item.IsJewelCraftingBase ? "ANY JEWEL BASE" : "EXACT BASE",
                FontSize = 8,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = DesignPalette.Brush(item.IsJewelCraftingBase ? DesignPalette.AccentCyan : DesignPalette.TextSecondary)
            };
            Grid.SetColumn(badgeBorder, 1);
            rowGrid.Children.Add(badgeBorder);

            rows.Add(rowGrid);

            return CreateGenericGroupSection(
                "ITEM BASE",
                DesignPalette.GetSectionAccentColor("ITEM BASE"),
                rows,
                () => item.FilterBaseTypeActive ? 1 : 0);
        }

        private static void AddDivider(StackPanel container)
        {
            UiComponentFactory.AddDivider(container);
        }

        private UIElement CreateModifierFilterRow(ItemModifier mod)
        {
            return ModifierRowBuilder.BuildRow(
                mod,
                UpdateMasterToggleButton,
                AttachQuickRollFlyout,
                () => _currentItem,
                QueryMarketAsync,
                isPaused => _isTimerPaused = isPaused,
                async () =>
                {
                    try
                    {
                        Window.Current.Activate();
                        if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    }
                    catch { }
                });
        }

        private UIElement CreateItemPropertyFilterRow(
            string label,
            string badgeText,
            Windows.UI.Color badgeBg,
            Windows.UI.Color badgeFg,
            int? initialMin,
            int? initialMax,
            bool isActive,
            Action<bool> onActiveChanged,
            Action<int?> onMinChanged,
            Action<int?> onMaxChanged,
            Action onFilterChanged,
            int maxCap = 100)
        {
            return PropertyRowBuilder.BuildPropertyRow(
                label,
                badgeText,
                badgeBg,
                badgeFg,
                initialMin,
                initialMax,
                isActive,
                onActiveChanged,
                onMinChanged,
                onMaxChanged,
                () =>
                {
                    if (onFilterChanged != null) onFilterChanged();
                    UpdateMasterToggleButton();
                },
                () => _currentItem,
                QueryMarketAsync,
                isPaused => _isTimerPaused = isPaused,
                async () =>
                {
                    try
                    {
                        Window.Current.Activate();
                        if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    }
                    catch { }
                },
                maxCap);
        }

        private void AttachQuickRollFlyout(TextBox targetBox, ItemModifier mod, bool isMin, CheckBox parentCb)
        {
            NumpadFlyoutBuilder.AttachNumpadContextMenu(
                targetBox,
                onValueChanged: () =>
                {
                    if (double.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        if (isMin) mod.MinRoll = Math.Abs(val); else mod.MaxRoll = Math.Abs(val);
                        mod.IsActive = true;
                        parentCb.IsChecked = true;
                    }
                    else if (string.IsNullOrWhiteSpace(targetBox.Text))
                    {
                        if (isMin) mod.MinRoll = null; else mod.MaxRoll = null;
                    }
                },
                onSearchRequested: () =>
                {
                    _ = QueryMarketAsync(_currentItem);
                },
                populateModifiersSubTab: modifiersSubMenu =>
                {
                    var presets = new List<(string Label, double Val)>();

                    // Exact (Item's parsed value)
                    if (mod.NumberValue.HasValue)
                    {
                        presets.Add(("Exact Roll", Math.Abs(mod.NumberValue.Value)));
                    }

                    // Current (Currently typed value in target box)
                    if (double.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double curVal) && Math.Abs(curVal) > 0)
                    {
                        if (!presets.Any(p => Math.Abs(p.Val - Math.Abs(curVal)) < 0.01))
                        {
                            presets.Add(("Current", Math.Abs(curVal)));
                        }
                    }

                    // Min (Tier Min or Range Min)
                    if (mod.MinRoll.HasValue && !presets.Any(p => Math.Abs(p.Val - Math.Abs(mod.MinRoll.Value)) < 0.01))
                    {
                        presets.Add(("Min Roll", Math.Abs(mod.MinRoll.Value)));
                    }

                    // Max (Tier Max or Range Max)
                    if (mod.MaxRoll.HasValue && !presets.Any(p => Math.Abs(p.Val - Math.Abs(mod.MaxRoll.Value)) < 0.01))
                    {
                        presets.Add(("Max Roll", Math.Abs(mod.MaxRoll.Value)));
                    }

                    foreach (var p in presets)
                    {
                        var pItem = new MenuFlyoutItem { Text = $"{p.Label} ({p.Val})" };
                        pItem.Click += (s, e) =>
                        {
                            targetBox.Text = p.Val.ToString(CultureInfo.InvariantCulture);
                            if (isMin) mod.MinRoll = p.Val; else mod.MaxRoll = p.Val;
                            mod.IsActive = true;
                            parentCb.IsChecked = true;
                            _ = QueryMarketAsync(_currentItem);
                        };
                        modifiersSubMenu.Items.Add(pItem);
                    }
                },
                populateCustomActions: menuFlyout =>
                {
                    var clearItem = new MenuFlyoutItem { Text = "Clear Roll" };
                    clearItem.Click += (s, e) =>
                    {
                        targetBox.Text = string.Empty;
                        if (isMin) mod.MinRoll = null; else mod.MaxRoll = null;
                        _ = QueryMarketAsync(_currentItem);
                    };
                    menuFlyout.Items.Add(clearItem);

                    var searchItem = new MenuFlyoutItem { Text = "Search Market Now" };
                    searchItem.Click += (s, e) =>
                    {
                        _ = QueryMarketAsync(_currentItem);
                    };
                    menuFlyout.Items.Add(searchItem);
                });
        }

        private void AttachQuickPropertyFlyout(TextBox targetBox, bool isMin, CheckBox parentCb, Action<int?> onValChanged, Action onFilterChanged, int? initialVal)
        {
            NumpadFlyoutBuilder.AttachNumpadContextMenu(
                targetBox,
                onValueChanged: () =>
                {
                    if (int.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int val))
                    {
                        onValChanged(val);
                        parentCb.IsChecked = true;
                    }
                    else if (string.IsNullOrWhiteSpace(targetBox.Text))
                    {
                        onValChanged(null);
                    }
                    onFilterChanged();
                    UpdateMasterToggleButton();
                },
                onSearchRequested: () =>
                {
                    _ = QueryMarketAsync(_currentItem);
                },
                populateModifiersSubTab: modifiersSubMenu =>
                {
                    if (initialVal.HasValue)
                    {
                        var exactItem = new MenuFlyoutItem { Text = $"Exact ({initialVal.Value})" };
                        exactItem.Click += (s, e) =>
                        {
                            targetBox.Text = initialVal.Value.ToString(CultureInfo.InvariantCulture);
                            onValChanged(initialVal.Value);
                            parentCb.IsChecked = true;
                            onFilterChanged();
                            UpdateMasterToggleButton();
                            _ = QueryMarketAsync(_currentItem);
                        };
                        modifiersSubMenu.Items.Add(exactItem);
                    }

                    if (int.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int curVal) && curVal > 0)
                    {
                        if (!initialVal.HasValue || initialVal.Value != curVal)
                        {
                            var curItem = new MenuFlyoutItem { Text = $"Current ({curVal})" };
                            curItem.Click += (s, e) =>
                            {
                                targetBox.Text = curVal.ToString(CultureInfo.InvariantCulture);
                                onValChanged(curVal);
                                parentCb.IsChecked = true;
                                onFilterChanged();
                                UpdateMasterToggleButton();
                                _ = QueryMarketAsync(_currentItem);
                            };
                            modifiersSubMenu.Items.Add(curItem);
                        }
                    }
                },
                populateCustomActions: menuFlyout =>
                {
                    var clearItem = new MenuFlyoutItem { Text = "Clear Value" };
                    clearItem.Click += (s, e) =>
                    {
                        targetBox.Text = string.Empty;
                        onValChanged(null);
                        onFilterChanged();
                        UpdateMasterToggleButton();
                        _ = QueryMarketAsync(_currentItem);
                    };
                    menuFlyout.Items.Add(clearItem);

                    var searchItem = new MenuFlyoutItem { Text = "Search Market Now" };
                    searchItem.Click += (s, e) =>
                    {
                        _ = QueryMarketAsync(_currentItem);
                    };
                    menuFlyout.Items.Add(searchItem);
                });
        }

        private async Task QueryMarketAsync(PoeItem item)
        {
            if (item == null) return;

            if (PoeTradeRateLimiter.Instance.IsRateLimited)
            {
                var remaining = PoeTradeRateLimiter.Instance.LockoutRemaining;
                double seconds = Math.Ceiling(remaining.TotalSeconds);
                if (seconds < 1) seconds = 1;

                MarketStatusText.Text = $"Rate Limited (Wait {seconds}s)";
                MarketStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
                RateLimitStatusText.Text = $"RATE LIMIT COOLDOWN ACTIVE ({seconds}s)";
                return;
            }

            string league = PoeSettingsManager.Instance.SelectedLeague;
            string sessId = PoeSettingsManager.Instance.PoeSessionId;

            MarketStatusText.Text = "Querying...";
            MarketStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248));

            var searchResult = await PoeOfficialTradeClient.Instance.SearchItemAsync(item, league, sessId);
            RenderListings(searchResult, league);

            RateLimitStatusText.Text = $"RATE LIMIT: {PoeTradeRateLimiter.Instance.CurrentStatusText}";
        }

        private void RenderListings(TradeSearchResult searchResult, string league)
        {
            ListingsContainer.Children.Clear();

            if (searchResult == null || !searchResult.IsSuccess)
            {
                MarketStatusText.Text = "Error";
                MarketStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
                OpenBrowserBtn.Visibility = Visibility.Collapsed;

                string errorReason = searchResult?.ErrorReason ?? "Unable to connect to PathOfExile.com trade API.";
                ListingsContainer.Children.Add(new TextBlock
                {
                    Text = $"Trade Search Error: {errorReason}",
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113))
                });
                return;
            }

            _activeSearchUrl = searchResult.SearchUrl;
            if (!string.IsNullOrEmpty(_activeSearchUrl))
            {
                OpenBrowserBtn.Visibility = Visibility.Visible;
            }

            // Update header with summary
            BenchmarkDivineText.Text = searchResult.SummaryText;
            BenchmarkChaosText.Text = $"{searchResult.TotalListings} listings found";
            MarketStatusText.Text = $"{searchResult.TotalListings} listings";
            MarketStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));

            if (searchResult.Listings == null || searchResult.Listings.Count == 0)
            {
                string reason = !string.IsNullOrEmpty(searchResult.ErrorReason)
                    ? searchResult.ErrorReason
                    : $"No active offers found for '{_currentItem?.Name ?? "item"}' in '{league}' league.";

                ListingsContainer.Children.Add(new TextBlock
                {
                    Text = reason,
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 251, 191, 36))
                });
                return;
            }

            foreach (var l in searchResult.Listings)
            {
                var row = TradeRowBuilder.BuildTradeListingRow(l, CopyWhisperToClipboard);
                ListingsContainer.Children.Add(row);
            }
        }

        private Flyout CreateItemPreviewFlyout(TradeListing l)
        {
            return TradeCardBuilder.CreateTradeListingFlyout(l);
        }

        private void UpdateCorruptedFilterUI(string option)
        {
            StatusStyleHelper.ApplyCorruptedFilterStyles(option, CorruptAnyBtn, CorruptYesBtn, CorruptNoBtn);
        }

        private void CorruptFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string opt && _currentItem != null)
            {
                _currentItem.CorruptedFilterOption = opt;
                UpdateCorruptedFilterUI(opt);
                _ = QueryMarketAsync(_currentItem);
            }
        }

        private void CopyWhisperToClipboard(string whisperText)
        {
            if (string.IsNullOrWhiteSpace(whisperText)) return;
            try
            {
                var dp = new DataPackage();
                dp.SetText(whisperText);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        #endregion

        #region Actions & Controls

        private async void OpenBrowserBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_activeSearchUrl))
            {
                try
                {
                    await Launcher.LaunchUriAsync(new Uri(_activeSearchUrl));
                }
                catch { }
            }
        }

        private async void UpdatePricesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentItem != null)
            {
                await QueryMarketAsync(_currentItem);
            }
        }

        private void DismissBtn_Click(object sender, RoutedEventArgs e)
        {
            DismissOverlay();
        }

        private async void DismissOverlay()
        {
            if (PoeSettingsManager.Instance.IsSettingsOpen)
            {
                return;
            }

            _countdownTimer.Stop();
            try
            {
                if (_widgetControl != null)
                {
                    await _widgetControl.MinimizeAsync("Widget1");
                }
            }
            catch { }

        }

        private async Task RestoreWidgetAsync()
        {
            try
            {
                Window.Current?.Activate();
                if (_widgetControl != null)
                {
                    await _widgetControl.RestoreAsync("Widget1");
                }
            }
            catch { }
        }

        private void StartAutoMinimizeCountdown()
        {
            _remainingSeconds = _totalDurationSeconds;
            CountdownBar.Value = 100;
            _countdownTimer.Start();
        }

        private void OnCountdownTimerTick(object sender, object e)
        {
            if (PoeSettingsManager.Instance.IsSettingsOpen)
            {
                CountdownText.Text = "Paused (Settings Open)";
                _remainingSeconds = _totalDurationSeconds;
                return;
            }

            if (_isTimerPaused || NumpadFlyoutBuilder.IsFlyoutOpen)
            {
                CountdownText.Text = $"Paused at {_remainingSeconds:F1}s (Menu Open)";
                return;
            }

            _remainingSeconds -= 0.1;
            if (_remainingSeconds <= 0)
            {
                _remainingSeconds = 0;
                _countdownTimer.Stop();
                DismissOverlay();
                return;
            }

            double percent = (_remainingSeconds / _totalDurationSeconds) * 100.0;
            CountdownBar.Value = percent;
            CountdownText.Text = $"Auto-minimizing in {_remainingSeconds:F1}s (Hover to pause)";
        }

        private void OnWidgetWindowStateChanged(XboxGameBarWidget sender, object args)
        {
            if (sender != null && sender.Visible)
            {
                if (!_countdownTimer.IsEnabled)
                {
                    StartAutoMinimizeCountdown();
                }
            }
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            if (sender != null && sender.Visible)
            {
                if (!_countdownTimer.IsEnabled)
                {
                    StartAutoMinimizeCountdown();
                }
            }
        }

        #endregion
    }
}
