using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HotkeyDaemon.Services
{
    /// <summary>
    /// Win32 input simulator for interacting with the Path of Exile game client.
    /// Provides hardware scancode keystroke synthesis (Ctrl+C for item capture) and safe Unicode text input.
    /// </summary>
    public static class GameInputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private const byte VK_RETURN = 0x0D;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;
        private const byte VK_C = 0x43;
        private const byte VK_D = 0x44;

        // Hardware scancodes for DirectX/Vulkan game compatibility
        private const byte SCAN_LCTRL = 0x1D;
        private const byte SCAN_LALT = 0x38;
        private const byte SCAN_C = 0x2E;
        private const byte SCAN_D = 0x20;
        private const byte SCAN_RETURN = 0x1C;

        private const byte VK_SHIFT = 0x10;
        private const byte SCAN_LSHIFT = 0x2A;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static IntPtr _lastGameHwnd = IntPtr.Zero;

        public static void RememberGameWindow()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    _lastGameHwnd = hwnd;
                }
            }
            catch { }
        }

        public static void ReleaseStuckModifierKeys()
        {
            try
            {
                SendKey(VK_CONTROL, SCAN_LCTRL, true);
                SendKey(VK_MENU, SCAN_LALT, true);
                SendKey(VK_SHIFT, SCAN_LSHIFT, true);
            }
            catch { }
        }

        public static void RestoreFocusToGameWindow()
        {
            try
            {
                ReleaseStuckModifierKeys();
                if (_lastGameHwnd != IntPtr.Zero)
                {
                    IntPtr currentForeground = GetForegroundWindow();
                    if (currentForeground != _lastGameHwnd)
                    {
                        uint currentThreadId = GetCurrentThreadId();
                        uint gameThreadId = GetWindowThreadProcessId(_lastGameHwnd, out _);

                        if (currentThreadId > 0 && gameThreadId > 0 && currentThreadId != gameThreadId)
                        {
                            AttachThreadInput(currentThreadId, gameThreadId, true);
                            SetForegroundWindow(_lastGameHwnd);
                            AttachThreadInput(currentThreadId, gameThreadId, false);
                        }
                        else
                        {
                            SetForegroundWindow(_lastGameHwnd);
                        }
                    }
                }
                ReleaseStuckModifierKeys();
            }
            catch { }
        }
        public const string TestModeItemText = @"Item Class: Belts
Rarity: Unique
Mageblood
Heavy Belt
--------
Requirements:
Level: 48
--------
Item Level: 84
--------
Bleeding cannot be inflicted on you
--------
+41 to Dexterity
+16% to Fire Resistance
+22% to Cold Resistance
Magic Utility Flasks cannot be Used
Leftmost 2 Magic Utility Flasks constantly apply their Flask Effects to you
Magic Utility Flask Effects cannot be removed
--------
Rivers of power course through your veins.
--------
Corrupted
--------";

        /// <summary>
        /// Populates the system clipboard with the test mode Mageblood item data.
        /// </summary>
        public static void PopulateClipboardWithTestItem()
        {
            try
            {
                Clipboard.SetText(TestModeItemText);
            }
            catch { }
        }

        /// <summary>
        /// Synthesizes Ctrl+Alt+C to the active window to capture advanced item descriptions (with tiers and affix headers).
        /// </summary>
        public static async Task<string?> CaptureClipboardItemAsync()
        {
            RememberGameWindow();
            string? initialText = GetClipboardTextSafe();

            // Synthesize Ctrl+Alt+C exclusively (captures Advanced Mod Descriptions with tiers and affix headers)
            SendCtrlAltC();

            // Poll clipboard for target window to write new data
            for (int retry = 0; retry < 15; retry++)
            {
                await Task.Delay(35);
                string? current = GetClipboardTextSafe();
                if (!string.IsNullOrWhiteSpace(current) && 
                    (!string.Equals(current, initialText, StringComparison.Ordinal) || current.Contains("Item Class:") || current.Contains("Rarity:")))
                {
                    return current;
                }
            }

            return GetClipboardTextSafe();
        }

        private static string? GetClipboardTextSafe()
        {
            string? text = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        text = Clipboard.GetText();
                    }
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(50);
            return text;
        }

        private static void SendKey(byte vkCode, byte scanCode, bool keyUp)
        {
            uint flags = KEYEVENTF_SCANCODE;
            if (keyUp) flags |= KEYEVENTF_KEYUP;
            keybd_event(vkCode, scanCode, flags, UIntPtr.Zero);
        }

        private static void SendCtrlAltC()
        {
            SendKey(VK_D, SCAN_D, true);
            Thread.Sleep(10);

            SendKey(VK_CONTROL, SCAN_LCTRL, false);
            SendKey(VK_MENU, SCAN_LALT, false);
            Thread.Sleep(15);
            SendKey(VK_C, SCAN_C, false);
            Thread.Sleep(45);

            SendKey(VK_C, SCAN_C, true);
            Thread.Sleep(15);
            SendKey(VK_MENU, SCAN_LALT, true);
            SendKey(VK_CONTROL, SCAN_LCTRL, true);
            Thread.Sleep(10);
            ReleaseStuckModifierKeys();
        }
    }
}
