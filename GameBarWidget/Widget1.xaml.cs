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
        private readonly HashSet<string> _collapsedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Widget1()
        {
            this.InitializeComponent();

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _countdownTimer.Tick += OnCountdownTimerTick;

            this.Loaded += Widget1_Loaded;
            this.Unloaded += Widget1_Unloaded;

            // Pause countdown on mouse hover
            this.PointerEntered += (s, e) => _isTimerPaused = true;
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
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;

            if (_widget != null)
            {
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
            }

            _countdownTimer.Stop();
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

                    if (command.Equals("PriceCheck", StringComparison.OrdinalIgnoreCase))
                    {
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
            ItemNameText.Text = "Awaiting Item";
            ItemBaseTypeText.Text = "Hover over an item in Path of Exile and press the hotkey (CTRL+D)";
            ItemLevelText.Text = string.Empty;
            ItemSocketText.Text = string.Empty;
            ItemRarityText.Text = "READY";
            ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68));
            ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));

            BenchmarkDivineText.Text = "-";
            BenchmarkChaosText.Text = "No benchmark";
            OpenBrowserBtn.Visibility = Visibility.Collapsed;

            ModContainer.Children.Clear();
            ModContainer.Children.Add(new TextBlock
            {
                Text = "No item loaded yet.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
            });

            ListingsContainer.Children.Clear();
            ListingsContainer.Children.Add(new TextBlock
            {
                Text = "No trade queries performed yet. Use in-game hotkey over an item.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
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
            switch (item.Rarity)
            {
                case PoeRarity.Gem:
                    ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 118, 110));
                    ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 94, 234, 212));
                    break;
                case PoeRarity.Unique:
                    ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 53, 15));
                    ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 253, 224, 71));
                    break;
                case PoeRarity.Rare:
                    ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 113, 63, 18));
                    ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 254, 240, 138));
                    break;
                case PoeRarity.Magic:
                    ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 58, 138));
                    ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 147, 197, 253));
                    break;
                default:
                    ItemRarityBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 65, 85));
                    ItemRarityText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 226, 232, 240));
                    break;
            }

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

            // Sockets Group (Count & Links)
            if (item.SocketCount > 0)
            {
                ModContainer.Children.Add(CreateSocketsGroupSection(item));
                renderedGroups++;
            }

            // Quality Group
            bool canHaveQuality = item.Quality > 0 || item.Rarity == PoeRarity.Gem || item.Category == "weapon" || item.Category == "armour" || item.Category == "flask" || item.SocketCount > 0 || (!string.IsNullOrEmpty(item.ItemClass) && (item.ItemClass.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Flask", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0 || item.ItemClass.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0));
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
                ModContainer.Children.Add(CreateStatGroupSection("PSEUDO STATS", Windows.UI.Color.FromArgb(255, 56, 189, 248), pseudoMods));
                renderedGroups++;
            }

            // 2. Implicit & Enchant Group
            if (implicitMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("IMPLICIT & ENCHANT", Windows.UI.Color.FromArgb(255, 167, 139, 250), implicitMods));
                renderedGroups++;
            }

            // 3. Prefix Modifiers Group
            if (prefixMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("PREFIXES", Windows.UI.Color.FromArgb(255, 56, 189, 248), prefixMods));
                renderedGroups++;
            }

            // 4. Suffix Modifiers Group
            if (suffixMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("SUFFIXES", Windows.UI.Color.FromArgb(255, 192, 132, 252), suffixMods));
                renderedGroups++;
            }

            // 5. General Explicit / Gem Properties Group
            if (generalExplicitMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                string groupTitle = item.Rarity == PoeRarity.Gem ? "GEM PROPERTIES" : "EXPLICIT MODIFIERS";
                Windows.UI.Color groupColor = item.Rarity == PoeRarity.Gem ? Windows.UI.Color.FromArgb(255, 45, 212, 191) : Windows.UI.Color.FromArgb(255, 250, 204, 21);
                ModContainer.Children.Add(CreateStatGroupSection(groupTitle, groupColor, generalExplicitMods));
                renderedGroups++;
            }

            // 6. Fractured & Crafted Group
            if (specialMods.Count > 0)
            {
                if (renderedGroups > 0) AddDivider(ModContainer);
                ModContainer.Children.Add(CreateStatGroupSection("FRACTURED & CRAFTED", Windows.UI.Color.FromArgb(255, 45, 212, 191), specialMods));
                renderedGroups++;
            }

            if (renderedGroups == 0)
            {
                ModContainer.Children.Add(new TextBlock
                {
                    Text = "No numerical modifiers detected.",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
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

            if (anyExpanded)
            {
                ToggleCompactModsBtn.Content = "Collapse All";
                ToggleCompactModsBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
                ToggleCompactModsBtn.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 65, 85));
                ToggleCompactModsBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 41, 59));
            }
            else
            {
                ToggleCompactModsBtn.Content = "Expand All";
                ToggleCompactModsBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248));
                ToggleCompactModsBtn.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 14, 116, 144));
                ToggleCompactModsBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42));
            }
        }

        private UIElement CreateGenericGroupSection(string title, Windows.UI.Color accentColor, List<UIElement> rows, Func<int> getActiveCount)
        {
            var sectionPanel = new StackPanel { Spacing = 1 };

            var itemsPanel = new StackPanel { Spacing = 1 };
            foreach (var row in rows)
            {
                itemsPanel.Children.Add(row);
            }

            bool isCollapsed = _collapsedGroups.Contains(title);
            itemsPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;

            // Header Grid
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 3, 0, 2),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: accent bar + title
            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            var indicator = new Border
            {
                Width = 3,
                Height = 10,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(indicator);

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 8,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(titleText);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Right: Count badge + Collapse/Expand Button
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            var countBadge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 30, 44)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            var countText = new TextBlock
            {
                Text = rows.Count.ToString(),
                FontSize = 8,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
            };
            countBadge.Child = countText;
            rightStack.Children.Add(countBadge);

            var toggleBtn = new Button
            {
                FontSize = 7.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Padding = new Thickness(4, 0.5, 4, 0.5),
                Height = 18,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Center
            };

            void UpdateButtonVisuals()
            {
                bool collapsed = itemsPanel.Visibility == Visibility.Collapsed;
                int activeInGroup = getActiveCount != null ? getActiveCount() : 0;

                toggleBtn.Content = collapsed ? (activeInGroup > 0 ? $"Expand ({activeInGroup})" : "Expand") : "Collapse";
                toggleBtn.Foreground = collapsed 
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248)) 
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
                toggleBtn.BorderBrush = collapsed 
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 14, 116, 144)) 
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 65, 85));
                toggleBtn.Background = collapsed 
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42)) 
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 41, 59));
            }

            UpdateButtonVisuals();
            sectionPanel.Tag = (Action)UpdateButtonVisuals;

            void ToggleGroup()
            {
                if (itemsPanel.Visibility == Visibility.Visible)
                {
                    itemsPanel.Visibility = Visibility.Collapsed;
                    _collapsedGroups.Add(title);
                }
                else
                {
                    itemsPanel.Visibility = Visibility.Visible;
                    _collapsedGroups.Remove(title);
                }
                UpdateButtonVisuals();
                UpdateMasterToggleButton();
            }

            toggleBtn.Click += (s, e) => ToggleGroup();
            titleStack.PointerPressed += (s, e) => ToggleGroup();

            rightStack.Children.Add(toggleBtn);
            Grid.SetColumn(rightStack, 1);
            headerGrid.Children.Add(rightStack);

            sectionPanel.Children.Add(headerGrid);
            sectionPanel.Children.Add(itemsPanel);

            return sectionPanel;
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

            // 1. Sockets Count row
            rows.Add(CreateItemPropertyFilterRow(
                "Sockets",
                "SOCK",
                Windows.UI.Color.FromArgb(255, 30, 41, 59),
                Windows.UI.Color.FromArgb(255, 148, 163, 184),
                item.FilterSocketsMin,
                item.FilterSocketsMax,
                item.FilterSocketsActive,
                isActive => item.FilterSocketsActive = isActive,
                min => item.FilterSocketsMin = min,
                max => item.FilterSocketsMax = max,
                () => { },
                6));

            // 2. Links row
            rows.Add(CreateItemPropertyFilterRow(
                "Links",
                "LINK",
                Windows.UI.Color.FromArgb(255, 50, 35, 20),
                Windows.UI.Color.FromArgb(255, 251, 191, 36),
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
                Windows.UI.Color.FromArgb(255, 251, 191, 36),
                rows,
                () => (item.FilterSocketsActive ? 1 : 0) + (item.FilterLinksActive ? 1 : 0));
        }

        private UIElement CreateQualityGroupSection(PoeItem item)
        {
            var rows = new List<UIElement>();

            // Quality row
            rows.Add(CreateItemPropertyFilterRow(
                "Quality",
                "QUAL",
                Windows.UI.Color.FromArgb(255, 15, 60, 55),
                Windows.UI.Color.FromArgb(255, 45, 212, 191),
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
                Windows.UI.Color.FromArgb(255, 45, 212, 191),
                rows,
                () => item.FilterQualityActive ? 1 : 0);
        }

        private static void AddDivider(StackPanel container)
        {
            container.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 38, 54)),
                Margin = new Thickness(0, 2, 0, 1)
            });
        }

        private UIElement CreateModifierFilterRow(ItemModifier mod)
        {
            // Distinct visible slate/color tinting based on modifier affix type matching user mockup
            Windows.UI.Color rowBgColor;
            Windows.UI.Color rowBorderColor;
            Windows.UI.Color rowHoverColor;
            Windows.UI.Color textCol;
            string tierBadgeText = string.Empty;
            Windows.UI.Color tierBadgeBg = Windows.UI.Color.FromArgb(255, 15, 23, 42);
            Windows.UI.Color tierBadgeFg = Windows.UI.Color.FromArgb(255, 148, 163, 184);

            if (mod.IsPrefix || mod.TierInfo.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 22, 35, 52);      // Visible Navy/Azure tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 38, 64, 94);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 30, 48, 70);
                textCol = Windows.UI.Color.FromArgb(255, 241, 245, 249);
                tierBadgeText = !string.IsNullOrEmpty(mod.TierInfo) ? mod.TierInfo : "P";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 25, 55, 88);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 56, 189, 248);
            }
            else if (mod.IsSuffix || mod.TierInfo.StartsWith("S", StringComparison.OrdinalIgnoreCase))
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 36, 28, 54);      // Visible Purple/Violet tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 62, 48, 92);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 48, 38, 72);
                textCol = Windows.UI.Color.FromArgb(255, 241, 245, 249);
                tierBadgeText = !string.IsNullOrEmpty(mod.TierInfo) ? mod.TierInfo : "S";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 52, 38, 86);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 192, 132, 252);
            }
            else if (mod.Type == ModifierType.Implicit || mod.Type == ModifierType.Enchant)
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 26, 32, 52);      // Indigo/Slate tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 48, 58, 92);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 36, 44, 70);
                textCol = Windows.UI.Color.FromArgb(255, 196, 181, 253);
                tierBadgeText = mod.Type == ModifierType.Enchant ? "ENC" : "IMP";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 42, 38, 74);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 167, 139, 250);
            }
            else if (mod.Type == ModifierType.Fractured)
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 44, 34, 20);      // Amber tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 80, 62, 32);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 58, 46, 26);
                textCol = Windows.UI.Color.FromArgb(255, 254, 240, 138);
                tierBadgeText = "FRAC";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 68, 50, 20);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 251, 191, 36);
            }
            else if (mod.Type == ModifierType.Crafted)
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 19, 42, 39);      // Teal tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 34, 78, 72);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 26, 56, 52);
                textCol = Windows.UI.Color.FromArgb(255, 153, 246, 228);
                tierBadgeText = "CRAFT";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 18, 62, 56);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 45, 212, 191);
            }
            else if (mod.IsPseudo)
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 20, 36, 50);      // Cyan tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 34, 64, 88);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 28, 50, 68);
                textCol = Windows.UI.Color.FromArgb(255, 125, 211, 252);
                tierBadgeText = "PSEUDO";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 24, 58, 92);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 56, 189, 248);
            }
            else if (mod.TierInfo.Equals("UNI", StringComparison.OrdinalIgnoreCase))
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 36, 26, 16);      // Unique Gold/Amber tint
                rowBorderColor = Windows.UI.Color.FromArgb(255, 76, 52, 24);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 48, 36, 20);
                textCol = Windows.UI.Color.FromArgb(255, 254, 240, 138);
                tierBadgeText = "UNI";
                tierBadgeBg = Windows.UI.Color.FromArgb(255, 75, 45, 15);
                tierBadgeFg = Windows.UI.Color.FromArgb(255, 250, 204, 21);
            }
            else
            {
                rowBgColor = Windows.UI.Color.FromArgb(255, 28, 38, 52);      // Default Explicit tinted slate
                rowBorderColor = Windows.UI.Color.FromArgb(255, 44, 60, 82);
                rowHoverColor = Windows.UI.Color.FromArgb(255, 36, 48, 66);
                textCol = Windows.UI.Color.FromArgb(255, 226, 232, 240);
                tierBadgeText = !string.IsNullOrEmpty(mod.TierInfo) ? mod.TierInfo : "EXP";
            }

            var cardBorder = new Border
            {
                Background = new SolidColorBrush(rowBgColor),
                BorderBrush = new SolidColorBrush(rowBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 1, 0, 1)
            };

            cardBorder.PointerEntered += (s, e) =>
            {
                cardBorder.Background = new SolidColorBrush(rowHoverColor);
            };
            cardBorder.PointerExited += (s, e) =>
            {
                cardBorder.Background = new SolidColorBrush(rowBgColor);
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: CheckBox with Green Accent & Tier Badge
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };

            var cb = new CheckBox
            {
                IsChecked = mod.IsActive,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(tierBadgeText))
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(tierBadgeBg),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(3, 0.5, 3, 0.5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = tierBadgeText,
                    FontSize = 7.5,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(tierBadgeFg)
                };
                leftStack.Children.Add(badge);
            }

            if (mod.IsLocal)
            {
                var localBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 12, 74, 110)), // Cyan / Slate Local badge
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(3, 0.5, 3, 0.5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                localBadge.Child = new TextBlock
                {
                    Text = "LOCAL",
                    FontSize = 7.2,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 186, 230, 253))
                };
                leftStack.Children.Add(localBadge);
            }

            var modLabel = new TextBlock
            {
                Text = mod.RawText,
                FontSize = 9.2,
                Foreground = new SolidColorBrush(textCol),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(modLabel);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            bool showRollInputs = !mod.IsUnscalable && (mod.NumberValue.HasValue || mod.MinRoll.HasValue || mod.MaxRoll.HasValue);
            if (!showRollInputs)
            {
                cb.Checked += (s, e) =>
                {
                    mod.IsActive = true;
                    UpdateMasterToggleButton();
                };
                cb.Unchecked += (s, e) =>
                {
                    mod.IsActive = false;
                    UpdateMasterToggleButton();
                };
                leftStack.Children.Insert(0, cb);
                cardBorder.Child = grid;
                return cardBorder;
            }

            // Right: Dual Min-Max Inputs
            var inputStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            // 1. Min Input
            string initialMinText = mod.MinRoll.HasValue ? Math.Abs(mod.MinRoll.Value).ToString(CultureInfo.InvariantCulture) : (mod.NumberValue.HasValue ? Math.Abs(mod.NumberValue.Value).ToString(CultureInfo.InvariantCulture) : "");
            var minBox = new TextBox
            {
                Text = initialMinText,
                PlaceholderText = "min",
                Width = 38,
                Height = 20,
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 9,
                Padding = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 9, 14, 21)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 76)),
                CornerRadius = new CornerRadius(2),
                IsTabStop = true,
                IsHitTestVisible = true,
                IsReadOnly = false,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            minBox.GotFocus += (s, e) => _isTimerPaused = true;
            minBox.LostFocus += (s, e) => _isTimerPaused = false;
            minBox.PointerPressed += async (s, e) =>
            {
                _isTimerPaused = true;
                try
                {
                    Window.Current.Activate();
                    if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    await FocusManager.TryFocusAsync(minBox, FocusState.Programmatic);
                }
                catch { }
            };
            minBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(minBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    double cur = Math.Abs(mod.MinRoll ?? mod.NumberValue ?? 0);
                    double step = (cur >= 100) ? 5 : ((cur >= 10) ? 1 : 0.5);
                    cur = (delta > 0) ? cur + step : Math.Max(0, cur - step);
                    cur = Math.Abs(cur);
                    minBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    mod.MinRoll = cur;
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
            };
            minBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    _ = QueryMarketAsync(_currentItem);
                }
            };
            minBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double customMin))
                {
                    mod.MinRoll = Math.Abs(customMin);
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(minBox.Text))
                {
                    mod.MinRoll = null;
                }
            };

            // 2. Max Input
            string initialMaxText = mod.MaxRoll.HasValue ? Math.Abs(mod.MaxRoll.Value).ToString(CultureInfo.InvariantCulture) : "";
            var maxBox = new TextBox
            {
                Text = initialMaxText,
                PlaceholderText = "max",
                Width = 38,
                Height = 20,
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 9,
                Padding = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 9, 14, 21)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 76)),
                CornerRadius = new CornerRadius(2),
                IsTabStop = true,
                IsHitTestVisible = true,
                IsReadOnly = false,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            maxBox.GotFocus += (s, e) => _isTimerPaused = true;
            maxBox.LostFocus += (s, e) => _isTimerPaused = false;
            maxBox.PointerPressed += async (s, e) =>
            {
                _isTimerPaused = true;
                try
                {
                    Window.Current.Activate();
                    if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    await FocusManager.TryFocusAsync(maxBox, FocusState.Programmatic);
                }
                catch { }
            };
            maxBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(maxBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    double cur = Math.Abs(mod.MaxRoll ?? mod.NumberValue ?? 0);
                    double step = (cur >= 100) ? 5 : ((cur >= 10) ? 1 : 0.5);
                    cur = (delta > 0) ? cur + step : Math.Max(0, cur - step);
                    cur = Math.Abs(cur);
                    maxBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    mod.MaxRoll = cur;
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
            };
            maxBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    _ = QueryMarketAsync(_currentItem);
                }
            };
            maxBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double customMax))
                {
                    mod.MaxRoll = Math.Abs(customMax);
                    mod.IsActive = true;
                    cb.IsChecked = true;
                }
                else if (string.IsNullOrWhiteSpace(maxBox.Text))
                {
                    mod.MaxRoll = null;
                }
            };

            // CheckBox event binding after minBox and maxBox are both initialized
            cb.Checked += (s, e) =>
            {
                mod.IsActive = true;
                if (!mod.MinRoll.HasValue && double.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double minV))
                {
                    mod.MinRoll = Math.Abs(minV);
                }
                if (!mod.MaxRoll.HasValue && double.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxV))
                {
                    mod.MaxRoll = Math.Abs(maxV);
                }
                UpdateMasterToggleButton();
            };
            cb.Unchecked += (s, e) =>
            {
                mod.IsActive = false;
                UpdateMasterToggleButton();
            };
            leftStack.Children.Insert(0, cb);

            AttachQuickRollFlyout(minBox, mod, true, cb);
            AttachQuickRollFlyout(maxBox, mod, false, cb);

            inputStack.Children.Add(minBox);

            // Separator dash
            inputStack.Children.Add(new TextBlock
            {
                Text = "-",
                FontSize = 8.5,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center
            });

            inputStack.Children.Add(maxBox);

            Grid.SetColumn(inputStack, 1);
            grid.Children.Add(inputStack);

            cardBorder.Child = grid;
            return cardBorder;
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
            var cardBorder = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 18, 32, 48)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 56, 82)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 1, 0, 1)
            };

            cardBorder.PointerEntered += (s, e) =>
            {
                cardBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 44, 66));
            };
            cardBorder.PointerExited += (s, e) =>
            {
                cardBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 18, 32, 48));
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };

            var cb = new CheckBox
            {
                IsChecked = isActive,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(badgeBg),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 0.5, 3, 0.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 7.5,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(badgeFg)
            };
            leftStack.Children.Add(cb);
            leftStack.Children.Add(badge);

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 9.2,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249)),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(labelText);

            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // Right: Min - Max inputs
            var inputStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            var minBox = new TextBox
            {
                Text = initialMin.HasValue ? initialMin.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                PlaceholderText = "min",
                Width = 38,
                Height = 20,
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 9,
                Padding = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 9, 14, 21)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 76)),
                CornerRadius = new CornerRadius(2),
                IsTabStop = true,
                IsHitTestVisible = true,
                IsReadOnly = false,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var maxBox = new TextBox
            {
                Text = initialMax.HasValue ? initialMax.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                PlaceholderText = "max",
                Width = 38,
                Height = 20,
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 9,
                Padding = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 9, 14, 21)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 76)),
                CornerRadius = new CornerRadius(2),
                IsTabStop = true,
                IsHitTestVisible = true,
                IsReadOnly = false,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            minBox.GotFocus += (s, e) => _isTimerPaused = true;
            minBox.LostFocus += (s, e) => _isTimerPaused = false;
            minBox.PointerPressed += async (s, e) =>
            {
                _isTimerPaused = true;
                try
                {
                    Window.Current.Activate();
                    if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    await FocusManager.TryFocusAsync(minBox, FocusState.Programmatic);
                }
                catch { }
            };
            minBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(minBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    int cur = 0;
                    if (int.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)) cur = parsed;
                    else if (initialMin.HasValue) cur = initialMin.Value;

                    cur = (delta > 0) ? Math.Min(maxCap, cur + 1) : Math.Max(0, cur - 1);
                    minBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    onMinChanged(cur);
                    onActiveChanged(true);
                    cb.IsChecked = true;
                    onFilterChanged();
                    UpdateMasterToggleButton();
                }
            };

            maxBox.GotFocus += (s, e) => _isTimerPaused = true;
            maxBox.LostFocus += (s, e) => _isTimerPaused = false;
            maxBox.PointerPressed += async (s, e) =>
            {
                _isTimerPaused = true;
                try
                {
                    Window.Current.Activate();
                    if (_widgetControl != null) await _widgetControl.ActivateAsync("Widget1");
                    await FocusManager.TryFocusAsync(maxBox, FocusState.Programmatic);
                }
                catch { }
            };
            maxBox.PointerWheelChanged += (s, e) =>
            {
                var ptr = e.GetCurrentPoint(maxBox);
                int delta = ptr.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    e.Handled = true;
                    int cur = 0;
                    if (int.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)) cur = parsed;
                    else if (initialMax.HasValue) cur = initialMax.Value;
                    else if (initialMin.HasValue) cur = initialMin.Value;

                    cur = (delta > 0) ? Math.Min(maxCap, cur + 1) : Math.Max(0, cur - 1);
                    maxBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    onMaxChanged(cur);
                    onActiveChanged(true);
                    cb.IsChecked = true;
                    onFilterChanged();
                    UpdateMasterToggleButton();
                }
            };

            minBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    _ = QueryMarketAsync(_currentItem);
                }
            };
            maxBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    _ = QueryMarketAsync(_currentItem);
                }
            };

            minBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(minBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int v))
                {
                    onMinChanged(v);
                    cb.IsChecked = true;
                    onActiveChanged(true);
                }
                else if (string.IsNullOrWhiteSpace(minBox.Text))
                {
                    onMinChanged(null);
                }
                onFilterChanged();
                UpdateMasterToggleButton();
            };

            maxBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(maxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int v))
                {
                    onMaxChanged(v);
                    cb.IsChecked = true;
                    onActiveChanged(true);
                }
                else if (string.IsNullOrWhiteSpace(maxBox.Text))
                {
                    onMaxChanged(null);
                }
                onFilterChanged();
                UpdateMasterToggleButton();
            };

            cb.Checked += (s, e) =>
            {
                onActiveChanged(true);
                if (string.IsNullOrWhiteSpace(minBox.Text) && initialMin.HasValue)
                {
                    minBox.Text = initialMin.Value.ToString(CultureInfo.InvariantCulture);
                    onMinChanged(initialMin.Value);
                }
                onFilterChanged();
                UpdateMasterToggleButton();
            };

            cb.Unchecked += (s, e) =>
            {
                onActiveChanged(false);
                onFilterChanged();
                UpdateMasterToggleButton();
            };

            inputStack.Children.Add(minBox);
            inputStack.Children.Add(new TextBlock
            {
                Text = "-",
                FontSize = 8.5,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center
            });
            inputStack.Children.Add(maxBox);

            Grid.SetColumn(inputStack, 1);
            grid.Children.Add(inputStack);

            cardBorder.Child = grid;
            return cardBorder;
        }

        private void AttachQuickRollFlyout(TextBox targetBox, ItemModifier mod, bool isMin, CheckBox parentCb)
        {
            var flyout = new Flyout();
            var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, Width = 140, Padding = new Thickness(4) };

            var title = new TextBlock
            {
                Text = isMin ? "Adjust Min Roll" : "Adjust Max Roll",
                FontSize = 9,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
            };
            sp.Children.Add(title);

            // Stepper row: [-5] [-1] [+1] [+5]
            var stepRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            double[] steps = { -5, -1, 1, 5 };
            foreach (double step in steps)
            {
                var btn = new Button
                {
                    Content = (step > 0 ? $"+{step}" : $"{step}"),
                    FontSize = 8.5,
                    Padding = new Thickness(4, 1, 4, 1),
                    Height = 20,
                    MinWidth = 28,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 41, 59)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 226, 232, 240))
                };
                btn.Click += (s, e) =>
                {
                    double cur = Math.Abs((isMin ? mod.MinRoll : mod.MaxRoll) ?? mod.NumberValue ?? 0);
                    cur = Math.Max(0, cur + step);
                    targetBox.Text = cur.ToString(CultureInfo.InvariantCulture);
                    if (isMin) mod.MinRoll = cur; else mod.MaxRoll = cur;
                    mod.IsActive = true;
                    parentCb.IsChecked = true;
                };
                stepRow.Children.Add(btn);
            }
            sp.Children.Add(stepRow);

            // Preset options
            var presets = new List<(string Label, double Val)>();
            if (mod.NumberValue.HasValue)
            {
                double exact = Math.Abs(mod.NumberValue.Value);
                presets.Add(("Exact", exact));
                presets.Add(("-10%", Math.Floor(exact * 0.9)));
                presets.Add(("-20%", Math.Floor(exact * 0.8)));
            }
            if (mod.MinRoll.HasValue && !presets.Any(p => Math.Abs(p.Val - Math.Abs(mod.MinRoll.Value)) < 0.01))
            {
                presets.Add(("Min Tier", Math.Abs(mod.MinRoll.Value)));
            }
            if (mod.MaxRoll.HasValue && !presets.Any(p => Math.Abs(p.Val - Math.Abs(mod.MaxRoll.Value)) < 0.01))
            {
                presets.Add(("Max Tier", Math.Abs(mod.MaxRoll.Value)));
            }

            if (presets.Count > 0)
            {
                var presetStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
                foreach (var p in presets)
                {
                    var pBtn = new Button
                    {
                        Content = $"{p.Label} ({p.Val})",
                        FontSize = 8.5,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Padding = new Thickness(4, 2, 4, 2),
                        Height = 22,
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42)),
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248))
                    };
                    pBtn.Click += (s, e) =>
                    {
                        targetBox.Text = p.Val.ToString(CultureInfo.InvariantCulture);
                        if (isMin) mod.MinRoll = p.Val; else mod.MaxRoll = p.Val;
                        mod.IsActive = true;
                        parentCb.IsChecked = true;
                        flyout.Hide();
                        _ = QueryMarketAsync(_currentItem);
                    };
                    presetStack.Children.Add(pBtn);
                }
                sp.Children.Add(presetStack);
            }

            flyout.Content = sp;
            FlyoutBase.SetAttachedFlyout(targetBox, flyout);
            targetBox.DoubleTapped += (s, e) =>
            {
                flyout.ShowAt(targetBox);
            };
            targetBox.RightTapped += (s, e) =>
            {
                flyout.ShowAt(targetBox);
            };
        }

        private async Task QueryMarketAsync(PoeItem item)
        {
            if (item == null) return;
            string league = PoeSettingsManager.Instance.SelectedLeague;
            string sessId = PoeSettingsManager.Instance.PoeSessionId;

            MarketStatusText.Text = "Querying...";
            MarketStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248));

            var searchResult = await PoeOfficialTradeClient.Instance.SearchItemAsync(item, league, sessId);
            RenderListings(searchResult, league);

            // Update rate limit badge
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
                var row = new Grid
                {
                    Padding = new Thickness(4),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 35, 51)),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 1, 0, 1)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                // Price display
                string currencyUpper = l.PriceCurrency.ToUpperInvariant();
                string priceLabel = $"{l.PriceAmount:0.##} {currencyUpper}";
                if (currencyUpper.Contains("DIV") || currencyUpper.Contains("CHAOS"))
                {
                    priceLabel = $"{l.PriceAmount:0.##} {currencyUpper} (≈{l.PriceInChaos}c)";
                }

                var priceText = new TextBlock
                {
                    Text = priceLabel,
                    FontSize = 11,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = currencyUpper.Contains("DIV") 
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 251, 191, 36)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                leftStack.Children.Add(priceText);

                if (l.IsFaustusInstantTrade)
                {
                    var faustusBadge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 53, 15)),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(3, 1, 3, 1),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    faustusBadge.Child = new TextBlock
                    {
                        Text = "FAUSTUS",
                        FontSize = 8,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 253, 224, 71))
                    };
                    leftStack.Children.Add(faustusBadge);
                }

                var accountText = new TextBlock
                {
                    Text = $"@{l.AccountName}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                leftStack.Children.Add(accountText);

                string myAccount = PoeSettingsManager.Instance.AccountName;
                if (!string.IsNullOrWhiteSpace(myAccount) && 
                    (l.AccountName.IndexOf(myAccount, StringComparison.OrdinalIgnoreCase) >= 0 || myAccount.IndexOf(l.AccountName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var myBadge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 58, 138)),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(3, 1, 3, 1),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    myBadge.Child = new TextBlock
                    {
                        Text = "YOUR LISTING",
                        FontSize = 8,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 147, 197, 253))
                    };
                    leftStack.Children.Add(myBadge);
                }

                Grid.SetColumn(leftStack, 0);
                row.Children.Add(leftStack);

                // Action buttons
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

                // 1. Primary Action: "To Hideout" or "Whisper"
                string actionLabel = (l.IsFaustusInstantTrade || !string.IsNullOrEmpty(l.HideoutToken))
                    ? (l.GoldFee > 0 ? $"To Hideout ({l.GoldFee}g)" : "To Hideout")
                    : "Whisper";

                var actionBtn = new Button
                {
                    Content = actionLabel,
                    FontSize = 9,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Padding = new Thickness(5, 2, 5, 2),
                    Background = l.IsFaustusInstantTrade
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 44, 32))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)),
                    Foreground = l.IsFaustusInstantTrade
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 226, 232, 240)),
                    BorderBrush = l.IsFaustusInstantTrade
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94))
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 69, 96)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3)
                };
                actionBtn.Click += async (s, e) =>
                {
                    try
                    {
                        bool apiSent = false;
                        string token = !string.IsNullOrEmpty(l.HideoutToken) ? l.HideoutToken : l.WhisperToken;

                        if (!string.IsNullOrEmpty(token))
                        {
                            var hideoutRes = await PoeOfficialTradeClient.Instance.SendDirectHideoutTokenAsync(token, PoeSettingsManager.Instance.PoeSessionId);
                            if (hideoutRes.success)
                            {
                                apiSent = true;
                                actionBtn.Content = "Teleported!";
                            }
                            else
                            {
                                var whisperRes = await PoeOfficialTradeClient.Instance.SendDirectWhisperTokenAsync(token, PoeSettingsManager.Instance.PoeSessionId);
                                if (whisperRes.success)
                                {
                                    apiSent = true;
                                    actionBtn.Content = "Sent!";
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(l.WhisperString))
                        {
                            CopyWhisperToClipboard(l.WhisperString);
                        }

                        if (!apiSent)
                        {
                            actionBtn.Content = "Copied!";
                        }
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(l.WhisperString))
                        {
                            CopyWhisperToClipboard(l.WhisperString);
                        }
                        actionBtn.Content = "Copied!";
                    }
                };
                btnStack.Children.Add(actionBtn);

                // 2. Second Action: "Preview" Item on Sale
                var previewBtn = new Button
                {
                    Content = "Preview",
                    FontSize = 9,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Padding = new Thickness(5, 2, 5, 2),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 38, 58)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248)),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 70, 105)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3)
                };

                var itemFlyout = CreateItemPreviewFlyout(l);
                Windows.UI.Xaml.Controls.Primitives.FlyoutBase.SetAttachedFlyout(previewBtn, itemFlyout);
                previewBtn.Click += (s, e) =>
                {
                    Windows.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(previewBtn);
                };
                btnStack.Children.Add(previewBtn);

                Grid.SetColumn(btnStack, 1);
                row.Children.Add(btnStack);

                ListingsContainer.Children.Add(row);
            }
        }

        private Flyout CreateItemPreviewFlyout(TradeListing l)
        {
            var flyout = new Flyout
            {
                Placement = Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Left
            };

            var scroll = new ScrollViewer
            {
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var container = new StackPanel
            {
                Width = 310,
                Spacing = 4,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 18, 27)),
                Padding = new Thickness(10)
            };

            var item = l.FlyoutItem;
            if (item == null)
            {
                // Fallback attempt to parse raw json if FlyoutItem was not previously populated
                if (!string.IsNullOrEmpty(l.RawJson) && Windows.Data.Json.JsonObject.TryParse(l.RawJson, out var rawObj))
                {
                    item = PoeFlyoutParser.ParseTradeItem(rawObj);
                    l.FlyoutItem = item;
                }
                else
                {
                    item = new FlyoutItemModel
                    {
                        Name = !string.IsNullOrEmpty(l.ItemName) ? l.ItemName : "Item on Sale",
                        BaseType = l.ItemBaseType,
                        ItemLevel = l.ItemLevel,
                        IsCorrupted = l.IsCorrupted,
                        SocketsSummary = l.SocketsSummary,
                        PhysicalDps = l.PhysicalDps,
                        ElementalDps = l.ElementalDps,
                        TotalDps = l.TotalDps,
                        Requirements = l.RequirementsSummary ?? new List<string>(),
                        Properties = l.PropertiesSummary ?? new List<string>(),
                        FlavourText = l.FlavourText
                    };
                }
            }

            // Rarity color resolution
            Windows.UI.Color headerColor = Windows.UI.Color.FromArgb(255, 250, 204, 21);
            switch (item.Rarity)
            {
                case PoeRarity.Unique:
                    headerColor = Windows.UI.Color.FromArgb(255, 175, 96, 37);
                    break;
                case PoeRarity.Rare:
                    headerColor = Windows.UI.Color.FromArgb(255, 254, 240, 138);
                    break;
                case PoeRarity.Magic:
                    headerColor = Windows.UI.Color.FromArgb(255, 147, 197, 253);
                    break;
                case PoeRarity.Normal:
                    headerColor = Windows.UI.Color.FromArgb(255, 241, 245, 249);
                    break;
                case PoeRarity.Gem:
                    headerColor = Windows.UI.Color.FromArgb(255, 94, 234, 212);
                    break;
                case PoeRarity.Currency:
                    headerColor = Windows.UI.Color.FromArgb(255, 251, 191, 36);
                    break;
            }

            // Title / Item Name
            string displayName = !string.IsNullOrEmpty(item.Name) ? item.Name : (!string.IsNullOrEmpty(item.BaseType) ? item.BaseType : "Item on Sale");
            var nameBlock = new TextBlock
            {
                Text = displayName,
                FontSize = 12.5,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(headerColor),
                TextWrapping = TextWrapping.Wrap
            };
            container.Children.Add(nameBlock);

            if (!string.IsNullOrEmpty(item.BaseType) && !item.BaseType.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
            {
                container.Children.Add(new TextBlock
                {
                    Text = item.BaseType,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
                });
            }

            // Tags row: ilvl, sockets, corrupted, synthesised, mirrored
            var tagStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 4) };
            if (item.ItemLevel > 0)
            {
                tagStack.Children.Add(new TextBlock
                {
                    Text = $"ilvl: {item.ItemLevel}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225))
                });
            }
            if (!string.IsNullOrEmpty(item.SocketsSummary))
            {
                tagStack.Children.Add(new TextBlock
                {
                    Text = item.SocketsSummary,
                    FontSize = 9,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248))
                });
            }
            if (item.IsCorrupted)
            {
                var corruptBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 75, 20, 20)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                corruptBadge.Child = new TextBlock
                {
                    Text = "CORRUPTED",
                    FontSize = 8,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113))
                };
                tagStack.Children.Add(corruptBadge);
            }
            if (item.IsSynthesised)
            {
                var synthBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 50, 75)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                synthBadge.Child = new TextBlock
                {
                    Text = "SYNTHESISED",
                    FontSize = 8,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248))
                };
                tagStack.Children.Add(synthBadge);
            }
            if (item.IsMirrored)
            {
                var mirrBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 60)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1)
                };
                mirrBadge.Child = new TextBlock
                {
                    Text = "MIRRORED",
                    FontSize = 8,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225))
                };
                tagStack.Children.Add(mirrBadge);
            }
            if (tagStack.Children.Count > 0) container.Children.Add(tagStack);

            // Requirements
            if (item.Requirements != null && item.Requirements.Count > 0)
            {
                container.Children.Add(new TextBlock
                {
                    Text = $"Requires {string.Join(", ", item.Requirements)}",
                    FontSize = 8.5,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Divider
            container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 2, 0, 2) });

            // Properties
            if (item.Properties != null && item.Properties.Count > 0)
            {
                foreach (var p in item.Properties)
                {
                    container.Children.Add(new TextBlock
                    {
                        Text = p,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225))
                    });
                }
            }

            // DPS breakdown if weapon
            if (item.TotalDps > 0 || item.PhysicalDps > 0 || item.ElementalDps > 0)
            {
                container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 2, 0, 2) });
                var dpsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (item.PhysicalDps > 0)
                {
                    dpsStack.Children.Add(new TextBlock
                    {
                        Text = $"pDPS: {item.PhysicalDps:0.0}",
                        FontSize = 8.5,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225))
                    });
                }
                if (item.ElementalDps > 0)
                {
                    dpsStack.Children.Add(new TextBlock
                    {
                        Text = $"eDPS: {item.ElementalDps:0.0}",
                        FontSize = 8.5,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248))
                    });
                }
                dpsStack.Children.Add(new TextBlock
                {
                    Text = $"Total DPS: {item.TotalDps:0.0}",
                    FontSize = 8.5,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
                });
                container.Children.Add(dpsStack);
            }

            // Divider before mods
            container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 2, 0, 2) });

            // Render Modifiers using dedicated FlyoutModifier objects
            if (item.Modifiers != null && item.Modifiers.Count > 0)
            {
                foreach (var mod in item.Modifiers)
                {
                    var modRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 1, 0, 1) };
                    string badgeText = "EXP";
                    Windows.UI.Color badgeBg = Windows.UI.Color.FromArgb(255, 28, 38, 52);
                    Windows.UI.Color badgeFg = Windows.UI.Color.FromArgb(255, 148, 163, 184);
                    Windows.UI.Color textFg = Windows.UI.Color.FromArgb(255, 147, 197, 253);

                    if (mod.Type == ModifierType.Implicit)
                    {
                        badgeText = "IMP";
                        badgeBg = Windows.UI.Color.FromArgb(255, 42, 38, 74);
                        badgeFg = Windows.UI.Color.FromArgb(255, 167, 139, 250);
                        textFg = Windows.UI.Color.FromArgb(255, 196, 181, 253);
                    }
                    else if (mod.Type == ModifierType.Fractured)
                    {
                        badgeText = "FRAC";
                        badgeBg = Windows.UI.Color.FromArgb(255, 68, 50, 20);
                        badgeFg = Windows.UI.Color.FromArgb(255, 251, 191, 36);
                        textFg = Windows.UI.Color.FromArgb(255, 254, 240, 138);
                    }
                    else if (mod.Type == ModifierType.Crafted)
                    {
                        badgeText = "CRAFT";
                        badgeBg = Windows.UI.Color.FromArgb(255, 18, 62, 56);
                        badgeFg = Windows.UI.Color.FromArgb(255, 45, 212, 191);
                        textFg = Windows.UI.Color.FromArgb(255, 153, 246, 228);
                    }
                    else if (mod.Type == ModifierType.Enchant)
                    {
                        badgeText = "ENC";
                        badgeBg = Windows.UI.Color.FromArgb(255, 42, 38, 74);
                        badgeFg = Windows.UI.Color.FromArgb(255, 167, 139, 250);
                        textFg = Windows.UI.Color.FromArgb(255, 196, 181, 253);
                    }
                    else if (mod.IsLocal)
                    {
                        badgeText = "LOCAL";
                        badgeBg = Windows.UI.Color.FromArgb(255, 20, 48, 74);
                        badgeFg = Windows.UI.Color.FromArgb(255, 56, 189, 248);
                    }
                    else if (item.Rarity == PoeRarity.Unique)
                    {
                        badgeText = "UNI";
                        badgeBg = Windows.UI.Color.FromArgb(255, 75, 45, 15);
                        badgeFg = Windows.UI.Color.FromArgb(255, 250, 204, 21);
                    }

                    var badgeBorder = new Border
                    {
                        Background = new SolidColorBrush(badgeBg),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(3, 1, 3, 1),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    badgeBorder.Child = new TextBlock
                    {
                        Text = badgeText,
                        FontSize = 7.5,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(badgeFg)
                    };
                    modRow.Children.Add(badgeBorder);

                    string fullModText = mod.RawText;
                    if (!string.IsNullOrEmpty(mod.MagnitudesText))
                    {
                        fullModText = $"{fullModText} {mod.MagnitudesText}";
                    }

                    var modText = new TextBlock
                    {
                        Text = fullModText,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(textFg),
                        TextWrapping = TextWrapping.Wrap,
                        Width = 255
                    };
                    modRow.Children.Add(modText);

                    container.Children.Add(modRow);
                }
            }

            // Flavour text
            if (!string.IsNullOrWhiteSpace(item.FlavourText))
            {
                container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 2, 0, 2) });
                container.Children.Add(new TextBlock
                {
                    Text = item.FlavourText,
                    FontSize = 8.5,
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 217, 119, 6)),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Stash tab / coordinates note
            if (!string.IsNullOrEmpty(l.StashTabName))
            {
                container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 2, 0, 2) });
                container.Children.Add(new TextBlock
                {
                    Text = $"Stash: \"{l.StashTabName}\" (Pos: {l.StashX + 1}, {l.StashY + 1})",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
                });
            }

            // Bottom seller / price info
            container.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 48, 68)), Margin = new Thickness(0, 4, 0, 2) });
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            footer.Children.Add(new TextBlock
            {
                Text = $"{l.PriceAmount} {l.PriceCurrency.ToUpperInvariant()}",
                FontSize = 10,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
            });
            footer.Children.Add(new TextBlock
            {
                Text = $"@{l.AccountName}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
            });
            if (l.IsFaustusInstantTrade)
            {
                var fBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 60, 45)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(3, 1, 3, 1)
                };
                fBadge.Child = new TextBlock
                {
                    Text = "ASYNC",
                    FontSize = 8,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))
                };
                footer.Children.Add(fBadge);
            }

            // Raw GGG JSON viewer button
            if (!string.IsNullOrEmpty(l.RawJson))
            {
                var jsonBtn = new Button
                {
                    Content = "Raw GGG JSON",
                    FontSize = 7.5,
                    Height = 18,
                    Padding = new Thickness(3, 0, 3, 0),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248)),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 58, 138)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };

                var jsonFlyout = new Flyout();
                var jsonScroll = new ScrollViewer { MaxWidth = 360, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
                var jsonText = new TextBox
                {
                    Text = l.RawJson,
                    IsReadOnly = true,
                    FontFamily = new Windows.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 8.5,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 9, 14, 21)),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 213, 225)),
                    TextWrapping = TextWrapping.Wrap
                };
                jsonScroll.Content = jsonText;
                jsonFlyout.Content = jsonScroll;
                jsonBtn.Flyout = jsonFlyout;
                footer.Children.Add(jsonBtn);
            }

            container.Children.Add(footer);

            scroll.Content = container;
            flyout.Content = scroll;
            return flyout;
        }

        private void UpdateCorruptedFilterUI(string option)
        {
            if (CorruptAnyBtn == null || CorruptYesBtn == null || CorruptNoBtn == null) return;

            CorruptAnyBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42));
            CorruptAnyBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
            CorruptYesBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42));
            CorruptYesBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
            CorruptNoBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 42));
            CorruptNoBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));

            if (string.Equals(option, "true", StringComparison.OrdinalIgnoreCase))
            {
                CorruptYesBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 127, 29, 29));
                CorruptYesBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 254, 202, 202));
            }
            else if (string.Equals(option, "false", StringComparison.OrdinalIgnoreCase))
            {
                CorruptNoBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 41, 59));
                CorruptNoBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252));
            }
            else
            {
                CorruptAnyBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 41, 59));
                CorruptAnyBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 250, 252));
            }
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

            if (_isTimerPaused)
            {
                CountdownText.Text = $"Paused at {_remainingSeconds:F1}s (Hovering)";
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
