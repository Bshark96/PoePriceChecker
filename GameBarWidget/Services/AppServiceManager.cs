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
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var requestDeferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;
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
