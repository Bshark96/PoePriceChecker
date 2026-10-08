using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Management.Deployment;

namespace HotkeyDaemon.Services
{
    /// <summary>
    /// AppService connection client with dynamic PackageFamilyName discovery and multi-probe resilience.
    /// Manages connection lifecycle and automatic reconnection to the Game Bar overlay.
    /// </summary>
    public sealed class AppServiceClient : IDisposable
    {
        private static readonly string[] PreferredServiceNames = new[]
        {
            "GameBarHotkeyService",
            "CounterWidgetService"
        };

        private static readonly string[] FallbackPackageFamilyNames = new[]
        {
            "GameBarWidget_3jmhdb37jepa2",
            "CounterOverlay_2egvyt1yq25ey",
            "GameBarHotkeyWidget_3jmhdb37jepa2"
        };

        private static readonly string[] TargetPackagePrefixes = new[]
        {
            "GameBarWidget",
            "CounterOverlay",
            "GameBarHotkeyWidget",
            "WidgetSampleCS"
        };

        private AppServiceConnection? _uwpConnection;
        private string? _resolvedPackageFamilyName;
        private string? _activeServiceName;
        private readonly object _lock = new object();
        private bool _isDisposed;

        public event EventHandler<string>? LogMessage;
        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler? ShutdownRequested;
        public event EventHandler<ValueSet>? SettingsUpdated;
        public event EventHandler? PoeLoginRequested;

        public bool IsConnected => _uwpConnection != null;

        public string GetPackageFamilyName() => _resolvedPackageFamilyName ?? FallbackPackageFamilyNames[0];

        public Task<bool> EnsureConnectedAsync() => ConnectAsync();

        /// <summary>
        /// Scans local installed packages for matching package prefix to get the exact PackageFamilyName.
        /// </summary>
        public string? DiscoverPackageFamilyName()
        {
            try
            {
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(string.Empty);

                foreach (var pkg in packages)
                {
                    try
                    {
                        string name = pkg.Id.Name;
                        foreach (string prefix in TargetPackagePrefixes)
                        {
                            if (string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase) ||
                                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                string family = pkg.Id.FamilyName;
                                LogMessage?.Invoke(this, $"[AppService] Discovered installed package: '{name}' (FamilyName: '{family}')");
                                return family;
                            }
                        }
                    }
                    catch
                    {
                        // Skip any individual package lookup error
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Dynamic package lookup note: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Connects to the UWP widget in Game Bar with dynamic discovery and fallback probing.
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            lock (_lock)
            {
                if (_uwpConnection != null)
                {
                    try { _uwpConnection.Dispose(); } catch { }
                    _uwpConnection = null;
                }
            }

            // 1. Discover installed package family name dynamically
            if (string.IsNullOrEmpty(_resolvedPackageFamilyName))
            {
                _resolvedPackageFamilyName = DiscoverPackageFamilyName();
            }

            // 2. Build candidate list of (ServiceName, PackageFamilyName)
            var candidates = new List<(string Service, string Family)>();

            if (!string.IsNullOrEmpty(_resolvedPackageFamilyName))
            {
                foreach (string svc in PreferredServiceNames)
                {
                    candidates.Add((svc, _resolvedPackageFamilyName));
                }
            }

            foreach (string fam in FallbackPackageFamilyNames)
            {
                if (!string.Equals(fam, _resolvedPackageFamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string svc in PreferredServiceNames)
                    {
                        candidates.Add((svc, fam));
                    }
                }
            }

            // 3. Attempt connection on candidates
            foreach (var (svc, fam) in candidates)
            {
                try
                {
                    LogMessage?.Invoke(this, $"[AppService] Probing connection to '{svc}' at '{fam}'...");

                    var connection = new AppServiceConnection
                    {
                        AppServiceName = svc,
                        PackageFamilyName = fam
                    };

                    AppServiceConnectionStatus status = await connection.OpenAsync();
                    if (status == AppServiceConnectionStatus.Success)
                    {
                        connection.RequestReceived += OnConnectionRequestReceived;
                        connection.ServiceClosed += OnConnectionServiceClosed;

                        lock (_lock)
                        {
                            _uwpConnection = connection;
                            _resolvedPackageFamilyName = fam;
                            _activeServiceName = svc;
                        }

                        LogMessage?.Invoke(this, $"[AppService] Connected successfully to Game Bar widget via '{svc}' ({fam})!");
                        ConnectionStatusChanged?.Invoke(this, true);
                        return true;
                    }
                    else
                    {
                        connection.Dispose();
                        if (status != AppServiceConnectionStatus.AppNotInstalled &&
                            status != AppServiceConnectionStatus.AppServiceUnavailable)
                        {
                            LogMessage?.Invoke(this, $"[AppService] Candidate '{svc}' ({fam}) returned: {status}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"[AppService] Probe error ({svc}@{fam}): {ex.Message}");
                }
            }

            LogMessage?.Invoke(this, "[AppService] Could not connect. Please ensure the widget is registered/opened in Xbox Game Bar (Win+G).");
            ConnectionStatusChanged?.Invoke(this, false);
            return false;
        }

        /// <summary>
        /// Sends Restore command to Game Bar widget.
        /// </summary>
        public async Task<bool> SendRestoreCommandAsync(string hotkeyName = "CTRL+D")
        {
            if (_uwpConnection == null)
            {
                bool connected = await ConnectAsync();
                if (!connected || _uwpConnection == null)
                {
                    LogMessage?.Invoke(this, "[AppService] Cannot send command: Widget not connected. Open widget in Game Bar (Win+G).");
                    return false;
                }
            }

            try
            {
                var message = new ValueSet
                {
                    { "Command", "Restore" },
                    { "Hotkey", hotkeyName },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                };

                LogMessage?.Invoke(this, $"[AppService] Sending 'Restore' signal for {hotkeyName}...");
                AppServiceResponse response = await _uwpConnection.SendMessageAsync(message);

                AppServiceResponseStatus status = response.Status;
                if (status == AppServiceResponseStatus.Success)
                {
                    LogMessage?.Invoke(this, "[AppService] Restore signal delivered to Game Bar widget.");
                    return true;
                }
                else
                {
                    LogMessage?.Invoke(this, $"[AppService] Send failed ({status}). Reconnecting...");
                    await ConnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Error sending message: {ex.Message}. Reconnecting...");
                await ConnectAsync();
                return false;
            }
        }

        public async Task<bool> SendPriceCheckCommandAsync(string rawItemText, string hotkeyName)
        {
            if (_uwpConnection == null)
            {
                bool connected = await ConnectAsync();
                if (!connected || _uwpConnection == null)
                {
                    LogMessage?.Invoke(this, "[AppService] Cannot send price check: Game Bar widget connection unavailable.");
                    return false;
                }
            }

            try
            {
                var message = new ValueSet
                {
                    { "Command", "PriceCheck" },
                    { "RawItemText", rawItemText },
                    { "Hotkey", hotkeyName },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                };

                LogMessage?.Invoke(this, $"[AppService] Sending 'PriceCheck' payload ({rawItemText.Length} chars)...");
                AppServiceResponse response = await _uwpConnection.SendMessageAsync(message);
                return response.Status == AppServiceResponseStatus.Success;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Price check dispatch error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendLiveSearchCommandAsync(string hotkeyName)
        {
            if (_uwpConnection == null)
            {
                bool connected = await ConnectAsync();
                if (!connected || _uwpConnection == null)
                {
                    LogMessage?.Invoke(this, "[AppService] Cannot send Live Search signal: Game Bar widget connection unavailable.");
                    return false;
                }
            }

            try
            {
                var message = new ValueSet
                {
                    { "Command", "ShowLiveSearch" },
                    { "Hotkey", hotkeyName },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                };

                LogMessage?.Invoke(this, $"[AppService] Sending 'ShowLiveSearch' signal for {hotkeyName}...");
                AppServiceResponse response = await _uwpConnection.SendMessageAsync(message);
                return response.Status == AppServiceResponseStatus.Success;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Live Search signal error: {ex.Message}");
                return false;
            }
        }

        private void OnConnectionRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            try
            {
                var message = args.Request.Message;
                if (message.ContainsKey("Command"))
                {
                    string command = message["Command"]?.ToString() ?? string.Empty;
                    if (string.Equals(command, "Shutdown", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "Exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "Close", StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessage?.Invoke(this, "[AppService] Widget commanded daemon shutdown.");
                        ShutdownRequested?.Invoke(this, EventArgs.Empty);
                    }
                    else if (string.Equals(command, "UpdateSettings", StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessage?.Invoke(this, "[AppService] Received updated settings from Game Bar widget.");
                        SettingsUpdated?.Invoke(this, message);
                    }
                    else if (string.Equals(command, "LaunchPoeLogin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(command, "StartPoeLogin", StringComparison.OrdinalIgnoreCase))
                    {
                        LogMessage?.Invoke(this, "[AppService] Widget requested PoE Chromium login dialog.");
                        PoeLoginRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Request handling note: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends captured POESESSID and optional account name back to the Game Bar widget.
        /// </summary>
        public async Task<bool> SendLoginCompletedAsync(string poeSessId, string accountName)
        {
            if (_uwpConnection == null)
            {
                bool connected = await ConnectAsync();
                if (!connected || _uwpConnection == null) return false;
            }

            try
            {
                var message = new ValueSet
                {
                    { "Command", "PoeLoginCompleted" },
                    { "PoeSessId", poeSessId },
                    { "AccountName", accountName ?? string.Empty },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                };

                LogMessage?.Invoke(this, "[AppService] Transmitting captured POESESSID to Game Bar overlay...");
                AppServiceResponse response = await _uwpConnection.SendMessageAsync(message);
                return response.Status == AppServiceResponseStatus.Success;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"[AppService] Login transmission error: {ex.Message}");
                return false;
            }
        }

        private void OnConnectionServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            LogMessage?.Invoke(this, $"[AppService] Widget closed ({args.Status}). Shutting down daemon.");
            ConnectionStatusChanged?.Invoke(this, false);
            ShutdownRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_lock)
            {
                if (_uwpConnection != null)
                {
                    try { _uwpConnection.Dispose(); } catch { }
                    _uwpConnection = null;
                }
            }
        }
    }
}
