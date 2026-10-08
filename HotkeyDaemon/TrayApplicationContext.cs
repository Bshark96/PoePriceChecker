using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HotkeyDaemon.Services;

namespace HotkeyDaemon
{
    /// <summary>
    /// System tray application context for the Xbox Game Bar hotkey daemon.
    /// Runs silently in the background with tray icon controls, status indicators, and global hotkey capturing.
    /// </summary>
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _connectionItem;
        private readonly AppServiceClient _appServiceClient;
        private readonly HotkeyListener _hotkeyListener;

        public TrayApplicationContext()
        {
            _appServiceClient = new AppServiceClient();
            _hotkeyListener = new HotkeyListener();

            // Initialize Context Menu
            _contextMenu = new ContextMenuStrip();

            var titleItem = new ToolStripMenuItem("Xbox Game Bar Hotkey Daemon")
            {
                Enabled = false,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };

            _statusItem = new ToolStripMenuItem("Hotkey: CTRL+D (Active)")
            {
                Enabled = false
            };

            _connectionItem = new ToolStripMenuItem("AppService: Connecting...")
            {
                Enabled = false
            };

            var triggerItem = new ToolStripMenuItem("Trigger Restore (CTRL+D)", null, OnTriggerClicked);
            var loginItem = new ToolStripMenuItem("Path of Exile Account Login (Browser)", null, OnLoginMenuClicked);
            var testItem = new ToolStripMenuItem("Populate Buffer (Test Mode Item)", null, OnSendTestItemClicked);
            var reconnectItem = new ToolStripMenuItem("Reconnect AppService", null, OnReconnectClicked);
            var separator = new ToolStripSeparator();
            var exitItem = new ToolStripMenuItem("Exit Daemon", null, OnExitClicked);

            _contextMenu.Items.Add(titleItem);
            _contextMenu.Items.Add(_statusItem);
            _contextMenu.Items.Add(_connectionItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(triggerItem);
            _contextMenu.Items.Add(loginItem);
            _contextMenu.Items.Add(testItem);
            _contextMenu.Items.Add(reconnectItem);
            _contextMenu.Items.Add(separator);
            _contextMenu.Items.Add(exitItem);

            // Initialize System Tray Icon
            _trayIcon = new NotifyIcon
            {
                Text = "Xbox Game Bar Hotkey Daemon (CTRL+D)",
                Icon = CreateTrayIcon(),
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => OnTriggerClicked(s, e);

            // Wire Service Events
            _appServiceClient.ConnectionStatusChanged += OnAppServiceStatusChanged;
            _appServiceClient.ShutdownRequested += (s, e) =>
            {
                try
                {
                    if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
                    {
                        _trayIcon.ContextMenuStrip.BeginInvoke(new Action(() => OnExitClicked(s, e)));
                    }
                    else
                    {
                        OnExitClicked(s, e);
                    }
                }
                catch { }
            };
            _appServiceClient.SettingsUpdated += (s, settings) =>
            {
                if (settings != null)
                {
                    string hotkey = settings.ContainsKey("Hotkey") ? settings["Hotkey"]?.ToString() ?? "CTRL+D" : "CTRL+D";
                    string liveHotkey = settings.ContainsKey("LiveSearchHotkey") ? settings["LiveSearchHotkey"]?.ToString() ?? "ALT+A" : "ALT+A";

                    _hotkeyListener.ConfigureHotkeys(hotkey, liveHotkey);
                    _statusItem.Text = $"PriceCheck: {hotkey} | LiveSearch: {liveHotkey}";
                    _trayIcon.Text = $"Xbox Game Bar Hotkey Daemon ({hotkey})";
                    ShowBalloonNotification("Settings Updated", $"PriceCheck: {hotkey} | LiveSearch: {liveHotkey}");
                }
            };
            _appServiceClient.PoeLoginRequested += (s, e) =>
            {
                ShowPoeLoginForm();
            };
            _hotkeyListener.HotkeyPressed += OnHotkeyPressed;

            // Start Services
            _hotkeyListener.Start();
            _ = Task.Run(async () =>
            {
                await _appServiceClient.EnsureConnectedAsync();
            });

            ShowBalloonNotification("Hotkey Daemon Started", "Listening for CTRL+D (PriceCheck) and ALT+A (LiveSearch).");
        }

        private void OnAppServiceStatusChanged(object? sender, bool connected)
        {
            if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
            {
                _trayIcon.ContextMenuStrip.BeginInvoke(new Action(() => OnAppServiceStatusChanged(sender, connected)));
                return;
            }

            if (connected)
            {
                _connectionItem.Text = "AppService: Connected to Widget";
                _trayIcon.Text = "Game Bar Daemon: Connected";
            }
            else
            {
                _connectionItem.Text = "AppService: Disconnected (Will Reconnect)";
                _trayIcon.Text = "Game Bar Daemon: Standby";
            }
        }

        private async void OnHotkeyPressed(object? sender, HotkeySpec spec)
        {
            if (spec == null) return;

            if (spec.IsDismiss || spec.Name == "ESC")
            {
                await _appServiceClient.SendRestoreCommandAsync("ESC");
                GameInputSimulator.RestoreFocusToGameWindow();
                return;
            }

            if (spec.IsLiveSearch)
            {
                bool success = await _appServiceClient.SendLiveSearchCommandAsync(spec.Name);
                if (!success)
                {
                    ShowBalloonNotification("Live Search Hotkey", $"Captured {spec.Name}. Opening Live Search view.");
                }
            }
            else
            {
                // Synthesize Ctrl+Alt+C and capture highlighted POE item from clipboard
                string? itemText = await GameInputSimulator.CaptureClipboardItemAsync();

                bool success;
                if (!string.IsNullOrWhiteSpace(itemText))
                {
                    success = await _appServiceClient.SendPriceCheckCommandAsync(itemText, spec.Name);
                }
                else
                {
                    success = await _appServiceClient.SendRestoreCommandAsync(spec.Name);
                }

                if (!success)
                {
                    ShowBalloonNotification("Game Bar Signal Sent", $"Captured {spec.Name}. Attempting to restore widget.");
                }
            }
        }

        private async void OnTriggerClicked(object? sender, EventArgs e)
        {
            bool success = await _appServiceClient.SendRestoreCommandAsync("Manual Tray Trigger");
            if (!success)
            {
                ShowBalloonNotification("Trigger Sent", "Sent restore command. Ensure widget is open in Game Bar (Win+G).");
            }
        }

        private void OnLoginMenuClicked(object? sender, EventArgs e)
        {
            ShowPoeLoginForm();
        }

        private void ShowPoeLoginForm()
        {
            var staThread = new System.Threading.Thread(() =>
            {
                try
                {
                    using var form = new HotkeyDaemon.Forms.PoeLoginForm();
                    var result = form.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrEmpty(form.CapturedPoeSessId))
                    {
                        ShowBalloonNotification("Path of Exile Logged In", "POESESSID captured. Synchronizing with Game Bar overlay.");
                        _ = _appServiceClient.SendLoginCompletedAsync(form.CapturedPoeSessId, form.CapturedAccountName);
                    }
                }
                catch (Exception ex)
                {
                    ShowBalloonNotification("Login Note", $"Could not launch WebView2 login: {ex.Message}");
                }
            });

            staThread.SetApartmentState(System.Threading.ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Start();
        }

        private async void OnSendTestItemClicked(object? sender, EventArgs e)
        {
            GameInputSimulator.PopulateClipboardWithTestItem();
            bool success = await _appServiceClient.SendPriceCheckCommandAsync(GameInputSimulator.TestModeItemText, "Test Mode");
            ShowBalloonNotification("Test Mode Buffer Populated", success ? "Mageblood test data sent to Game Bar widget." : "Clipboard buffer populated with Mageblood test item.");
        }

        private async void OnReconnectClicked(object? sender, EventArgs e)
        {
            _connectionItem.Text = "AppService: Reconnecting...";
            bool connected = await _appServiceClient.ConnectAsync();
            if (connected)
            {
                ShowBalloonNotification("AppService Connected", "Successfully linked with Xbox Game Bar widget.");
            }
            else
            {
                ShowBalloonNotification("AppService Standby", "Widget not active in Game Bar yet. Will connect upon widget launch.");
            }
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _hotkeyListener.Dispose();
            _appServiceClient.Dispose();
            _trayIcon.Dispose();
            Application.Exit();
        }

        private void ShowBalloonNotification(string title, string text)
        {
            try
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = text;
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(2000);
            }
            catch { }
        }

        private static Icon CreateTrayIcon()
        {
            // Generate a clean 16x16 icon dynamically with green accent (representing active Game Bar daemon)
            using var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.FromArgb(34, 197, 94)); // Emerald green
                using var innerBrush = new SolidBrush(Color.FromArgb(240, 253, 244));
                g.FillEllipse(brush, 1, 1, 14, 14);
                g.FillEllipse(innerBrush, 4, 4, 8, 8);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeyListener.Dispose();
                _appServiceClient.Dispose();
                _trayIcon.Dispose();
                _contextMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
