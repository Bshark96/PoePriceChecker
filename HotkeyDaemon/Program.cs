using System;
using System.Threading;
using System.Windows.Forms;

namespace HotkeyDaemon
{
    internal static class Program
    {
        private const string MutexId = "Global\\XboxGameBar_HotkeyDaemon_SingleInstanceMutex";

        [STAThread]
        private static void Main(string[] args)
        {
            // Enforce single instance to prevent duplicate keyboard hooks and IPC ports
            using var mutex = new Mutex(true, MutexId, out bool createdNew);
            if (!createdNew)
            {
                // Daemon is already running in background/tray
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}

