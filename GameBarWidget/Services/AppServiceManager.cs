using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace GameBarWidget.Services
{
    public sealed class AppServiceManager
    {
        private static readonly Lazy<AppServiceManager> _lazyInstance = 
            new Lazy<AppServiceManager>(() => new AppServiceManager());

        public static AppServiceManager Instance => _lazyInstance.Value;

        private AppServiceConnection _connection;
        private BackgroundTaskDeferral _deferral;

        public string LastKnownClipboardText { get; set; } = string.Empty;

        public event EventHandler<ValueSet> MessageReceived;
        public event EventHandler<string> StatusChanged;

        private AppServiceManager()
        {
        }

        public void RegisterConnection(AppServiceConnection connection, BackgroundTaskDeferral deferral)
        {
            _connection = connection;
            _deferral = deferral;

            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            StatusChanged?.Invoke(this, "Daemon connected via AppService");
            _ = BridgeLogger.LogBridgeMessageAsync("[AppServiceManager] Daemon connected via AppService.");
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var requestDeferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;
                string cmd = message.ContainsKey("Command") ? message["Command"]?.ToString() : "Unknown";
                string hotkey = message.ContainsKey("Hotkey") ? message["Hotkey"]?.ToString() : "";
                if (message.ContainsKey("ClipboardText"))
                {
                    string clip = message["ClipboardText"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(clip))
                    {
                        LastKnownClipboardText = clip;
                    }
                }
                await BridgeLogger.LogBridgeMessageAsync($"[AppServiceManager] Request received: Command={cmd}, Hotkey={hotkey}");

                MessageReceived?.Invoke(this, message);

                // Acknowledge receipt
                var response = new ValueSet
                {
                    { "Status", "Success" },
                    { "Timestamp", DateTime.UtcNow.ToString("o") }
                };
                await args.Request.SendResponseAsync(response);
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"[AppServiceManager] Request Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AppService Request Error: {ex.Message}");
            }
            finally
            {
                requestDeferral.Complete();
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            StatusChanged?.Invoke(this, $"Daemon disconnected: {args.Status}");
            _ = BridgeLogger.LogBridgeMessageAsync($"[AppServiceManager] Daemon disconnected: {args.Status}");
            _connection = null;
            _deferral?.Complete();
            _deferral = null;
        }

        public async Task<bool> SendToDaemonAsync(ValueSet message)
        {
            if (_connection == null) return false;
            try
            {
                var response = await _connection.SendMessageAsync(message);
                return response.Status == AppServiceResponseStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetClipboardTextFromDaemonAsync()
        {
            if (_connection == null)
            {
                return LastKnownClipboardText ?? string.Empty;
            }

            try
            {
                var msg = new ValueSet { { "Command", "GetClipboardText" } };
                var response = await _connection.SendMessageAsync(msg);
                if (response.Status == AppServiceResponseStatus.Success &&
                    response.Message != null &&
                    response.Message.ContainsKey("ClipboardText"))
                {
                    string clip = response.Message["ClipboardText"]?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(clip))
                    {
                        LastKnownClipboardText = clip;
                    }
                    return clip;
                }
            }
            catch (Exception ex)
            {
                await BridgeLogger.LogBridgeMessageAsync($"[AppServiceManager] GetClipboardTextFromDaemonAsync note: {ex.Message}");
            }

            return LastKnownClipboardText ?? string.Empty;
        }

        public async Task RequestFocusWidgetUnderCursorAsync()
        {
            if (_connection == null) return;
            try
            {
                var msg = new ValueSet { { "Command", "FocusWidgetUnderCursor" } };
                await _connection.SendMessageAsync(msg);
            }
            catch { }
        }

        public async Task NotifyDaemonToCloseAsync()
        {
            if (_connection != null)
            {
                try
                {
                    var msg = new ValueSet { { "Command", "Shutdown" } };
                    await _connection.SendMessageAsync(msg);
                }
                catch { }

                try
                {
                    _connection.Dispose();
                }
                catch { }

                _connection = null;
                _deferral?.Complete();
                _deferral = null;
            }
        }
    }
}
