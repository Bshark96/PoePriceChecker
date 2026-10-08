using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;
using GameBarWidget.Services;

namespace GameBarWidget
{
    public sealed partial class Widget1Settings : Page
    {
        private XboxGameBarWidget _widget;

        public Widget1Settings()
        {
            this.InitializeComponent();
            this.Loaded += Widget1Settings_Loaded;
            this.Unloaded += Widget1Settings_Unloaded;
            AppServiceManager.Instance.MessageReceived += OnAppServiceMessageReceived;
        }

        private void Widget1Settings_Unloaded(object sender, RoutedEventArgs e)
        {
            PoeSettingsManager.Instance.IsSettingsOpen = false;
            if (_widget != null)
            {
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
            }
            AppServiceManager.Instance.MessageReceived -= OnAppServiceMessageReceived;
        }

        private async void OnAppServiceMessageReceived(object sender, ValueSet message)
        {
            if (message != null && message.ContainsKey("Command"))
            {
                string cmd = message["Command"]?.ToString() ?? string.Empty;
                if (string.Equals(cmd, "PoeLoginCompleted", StringComparison.OrdinalIgnoreCase))
                {
                    string poeSessId = message.ContainsKey("PoeSessId") ? message["PoeSessId"]?.ToString() ?? string.Empty : string.Empty;
                    string accountName = message.ContainsKey("AccountName") ? message["AccountName"]?.ToString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrWhiteSpace(poeSessId))
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            var settings = PoeSettingsManager.Instance;
                            settings.PoeSessionId = poeSessId;
                            if (!string.IsNullOrWhiteSpace(accountName))
                            {
                                settings.AccountName = accountName;
                                AccountNameBox.Text = accountName;
                            }
                            settings.IsLoggedIn = true;
                            settings.Save();

                            PoeSessIdBox.Password = poeSessId;
                            LoginStatusCard.Visibility = Visibility.Collapsed;
                            UpdateAuthUi();
                        });
                    }
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                PoeSettingsManager.Instance.IsSettingsOpen = _widget.Visible;
                _widget.VisibleChanged += OnWidgetVisibleChanged;
            }
            else
            {
                PoeSettingsManager.Instance.IsSettingsOpen = true;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            PoeSettingsManager.Instance.IsSettingsOpen = false;
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            if (sender != null)
            {
                PoeSettingsManager.Instance.IsSettingsOpen = sender.Visible;
            }
        }

        private async void Widget1Settings_Loaded(object sender, RoutedEventArgs e)
        {
            PoeSettingsManager.Instance.IsSettingsOpen = true;
            LoadSettingsIntoUi();
            await PopulateLeaguesAsync();
        }

        private async Task PopulateLeaguesAsync()
        {
            try
            {
                var leagues = await PoeOfficialTradeClient.Instance.GetLeaguesAsync();
                string currentLeague = PoeSettingsManager.Instance.SelectedLeague;

                if (!string.IsNullOrWhiteSpace(currentLeague) && !leagues.Any(l => string.Equals(l, currentLeague, StringComparison.OrdinalIgnoreCase)))
                {
                    leagues.Insert(0, currentLeague);
                }

                LeagueCombo.Items.Clear();
                int selectedIdx = 0;

                for (int i = 0; i < leagues.Count; i++)
                {
                    string l = leagues[i];
                    var item = new ComboBoxItem { Content = l };
                    LeagueCombo.Items.Add(item);

                    if (string.Equals(l, currentLeague, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIdx = i;
                    }
                }

                if (LeagueCombo.Items.Count > 0)
                {
                    LeagueCombo.SelectedIndex = selectedIdx;
                }
            }
            catch { }
        }

        private async void RefreshLeaguesBtn_Click(object sender, RoutedEventArgs e)
        {
            await PopulateLeaguesAsync();
        }

        private void LoadSettingsIntoUi()
        {
            var settings = PoeSettingsManager.Instance;

            PoeSessIdBox.Password = settings.PoeSessionId;
            AccountNameBox.Text = settings.AccountName;
            string filter = settings.OnlineStatusFilter;
            if (filter.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                OnlineStatusCombo.SelectedIndex = 1; // Any (Both async and not async)
            }
            else if (filter.Equals("online", StringComparison.OrdinalIgnoreCase) || filter.Equals("sync", StringComparison.OrdinalIgnoreCase) || filter.Equals("onlineleague", StringComparison.OrdinalIgnoreCase))
            {
                OnlineStatusCombo.SelectedIndex = 2; // Only sync
            }
            else
            {
                OnlineStatusCombo.SelectedIndex = 0; // Only async (Default)
            }
            AutoSearchCheck.IsChecked = settings.AutoSearchOfficialTrade;

            ToleranceSlider.Value = settings.ModRollTolerancePercent;
            ToleranceLabel.Text = $"-{settings.ModRollTolerancePercent}%";

            DurationSlider.Value = settings.AutoDismissDurationSeconds;
            DurationLabel.Text = $"{settings.AutoDismissDurationSeconds}s";

            // Load Hotkey Combo selection
            string currentHotkey = settings.Hotkey.ToUpperInvariant();
            if (currentHotkey.Contains("CTRL+E")) HotkeyCombo.SelectedIndex = 1;
            else if (currentHotkey.Contains("CTRL+F")) HotkeyCombo.SelectedIndex = 2;
            else if (currentHotkey.Contains("ALT+D")) HotkeyCombo.SelectedIndex = 3;
            else if (currentHotkey.Contains("ALT+E")) HotkeyCombo.SelectedIndex = 4;
            else HotkeyCombo.SelectedIndex = 0; // Default CTRL+D

            UpdateAuthUi();
        }

        private void UpdateAuthUi()
        {
            var settings = PoeSettingsManager.Instance;
            bool hasSession = !string.IsNullOrWhiteSpace(settings.PoeSessionId);
            if (hasSession)
            {
                AuthStatusText.Text = "SESSION ACTIVE";
                AuthStatusText.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.LightGreen);
                AccountDisplayNameText.Text = string.IsNullOrEmpty(settings.AccountName) ? "POESESSID Configured" : settings.AccountName;
                ClearSessionBtn.Visibility = Visibility.Visible;
            }
            else
            {
                AuthStatusText.Text = "Not Logged In";
                AuthStatusText.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Gray);
                AccountDisplayNameText.Text = string.Empty;
                ClearSessionBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void InAppLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginStatusCard.Visibility = Visibility.Visible;
                LoginPromptText.Text = "Opening browser login window...";

                var msg = new ValueSet
                {
                    { "Command", "LaunchPoeLogin" }
                };

                bool sent = await AppServiceManager.Instance.SendToDaemonAsync(msg);
                if (sent)
                {
                    LoginPromptText.Text = "Login window active — Sign in to Path of Exile or Steam";
                }
                else
                {
                    LoginPromptText.Text = "Daemon not responding. Launching default browser...";
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.pathofexile.com/login"));
                }
            }
            catch
            {
                LoginPromptText.Text = "Opening browser...";
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.pathofexile.com/login"));
            }
        }

        private async void PasteSessionBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (package != null && package.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    string text = (await package.GetTextAsync())?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        PoeSessIdBox.Password = text;
                        var settings = PoeSettingsManager.Instance;
                        settings.PoeSessionId = text;
                        settings.IsLoggedIn = true;
                        settings.Save();
                        UpdateAuthUi();
                    }
                }
            }
            catch { }
        }

        private async void OpenBrowserBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.pathofexile.com/login"));
            }
            catch { }
        }

        private void ClearSessionBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = PoeSettingsManager.Instance;
            settings.IsLoggedIn = false;
            settings.PoeSessionId = string.Empty;
            settings.OAuthAccessToken = string.Empty;
            settings.OAuthRefreshToken = string.Empty;
            PoeSessIdBox.Password = string.Empty;
            UpdateAuthUi();
        }

        private void ToleranceSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (ToleranceLabel != null)
            {
                ToleranceLabel.Text = $"-{e.NewValue:F0}%";
            }
        }

        private void DurationSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (DurationLabel != null)
            {
                DurationLabel.Text = $"{e.NewValue:F0}s";
            }
        }

        private async void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = PoeSettingsManager.Instance;

            if (LeagueCombo.SelectedItem is ComboBoxItem selectedLeagueItem && selectedLeagueItem.Content != null)
            {
                settings.SelectedLeague = selectedLeagueItem.Content.ToString() ?? "Standard";
            }
            else if (!string.IsNullOrWhiteSpace(LeagueCombo.Text))
            {
                settings.SelectedLeague = LeagueCombo.Text.Trim();
            }
            settings.PoeSessionId = PoeSessIdBox.Password.Trim();
            settings.AccountName = AccountNameBox.Text.Trim();
            if (OnlineStatusCombo.SelectedIndex == 1)
            {
                settings.OnlineStatusFilter = "any"; // Any (Both async and not async)
            }
            else if (OnlineStatusCombo.SelectedIndex == 2)
            {
                settings.OnlineStatusFilter = "online"; // Only sync
            }
            else
            {
                settings.OnlineStatusFilter = "securable"; // Only async (Default)
            }
            settings.AutoSearchOfficialTrade = AutoSearchCheck.IsChecked == true;
            settings.ModRollTolerancePercent = (int)ToleranceSlider.Value;
            settings.AutoDismissDurationSeconds = (int)DurationSlider.Value;

            // Save selected hotkey
            switch (HotkeyCombo.SelectedIndex)
            {
                case 1: settings.Hotkey = "CTRL+E"; break;
                case 2: settings.Hotkey = "CTRL+F"; break;
                case 3: settings.Hotkey = "ALT+D"; break;
                case 4: settings.Hotkey = "ALT+E"; break;
                default: settings.Hotkey = "CTRL+D"; break;
            }

            // Sync with daemon
            var syncMsg = new ValueSet
            {
                { "Command", "UpdateSettings" },
                { "DurationSeconds", settings.AutoDismissDurationSeconds },
                { "Hotkey", settings.Hotkey }
            };

            await AppServiceManager.Instance.SendToDaemonAsync(syncMsg);

            SaveSettingsBtn.Content = "Settings Saved Successfully!";
            await Task.Delay(1200);
            SaveSettingsBtn.Content = "Save & Apply All Settings";
        }
    }
}
