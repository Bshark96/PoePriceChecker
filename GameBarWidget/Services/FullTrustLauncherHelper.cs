using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Helper to launch desktop FullTrust companion process (HotkeyDaemon.exe) with detailed diagnostic feedback.
    /// </summary>
    public static class FullTrustLauncherHelper
    {
        public static async Task<(bool Success, string Message)> LaunchDaemonAsync()
        {
            try
            {
                if (!ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
                {
                    return (false, "FullTrustProcessLauncher API not available on this platform.");
                }

                try
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                    return (true, "Triggered FullTrustProcessLauncher for HotkeyDaemon.exe");
                }
                catch (Exception ex)
                {
                    return (false, $"FullTrust launch failed: {ex.Message} (0x{ex.HResult:X8})");
                }
            }
            catch (Exception ex)
            {
                return (false, $"FullTrust error: {ex.Message}");
            }
        }
    }
}
