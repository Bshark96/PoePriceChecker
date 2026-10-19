using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Thread-safe logger that appends all incoming bridge messages received by C#
    /// directly to a desktop text file ('poe_bridge_log.txt').
    /// </summary>
    public static class BridgeLogger
    {
        private static readonly object _fileLock = new object();
        private const string LogFileName = "poe_bridge_log.txt";

        public static async Task LogStartupAsync()
        {
            await LogBridgeMessageAsync("=== POE 1 BRIDGE LOGGER INITIALIZED ===");
        }

        /// <summary>
        /// Logs a raw message payload with timestamp to the desktop log text file.
        /// </summary>
        public static async Task LogBridgeMessageAsync(string rawPayload)
        {
            if (string.IsNullOrWhiteSpace(rawPayload)) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] [C# Bridge Received] {rawPayload}{Environment.NewLine}";

            Debug.WriteLine(logEntry);

            // 1. Try Windows UWP StorageFolder API for Desktop
            try
            {
                StorageFolder desktopFolder = KnownFolders.Objects3D;
                if (desktopFolder != null)
                {
                    StorageFile file = await desktopFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);
                    await FileIO.AppendTextAsync(file, logEntry);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BridgeLogger KnownFolders Error] {ex.Message}");
            }

            // 2. Direct System.IO to Desktop with broadFileSystemAccess
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrWhiteSpace(desktopPath))
                {
                    string fullPath = Path.Combine(desktopPath, LogFileName);
                    lock (_fileLock)
                    {
                        File.AppendAllText(fullPath, logEntry);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BridgeLogger Desktop File Error] {ex.Message}");
            }

            // 3. Fallback to LocalFolder AppData
            try
            {
                string localPath = ApplicationData.Current.LocalFolder.Path;
                string fallbackPath = Path.Combine(localPath, LogFileName);
                lock (_fileLock)
                {
                    File.AppendAllText(fallbackPath, logEntry);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BridgeLogger LocalFolder Error] {ex.Message}");
            }
        }
    }
}
