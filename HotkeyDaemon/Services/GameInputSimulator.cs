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
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

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
            uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
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
        }

        /// <summary>
        /// Sends a chat command (e.g. /hideout) safely using SendInput KEYEVENTF_UNICODE.
        /// </summary>
        public static void SendChatMacro(string chatCommand)
        {
            if (string.IsNullOrWhiteSpace(chatCommand)) return;

            try
            {
                // 1. Press Enter to open chat
                SendKey(VK_RETURN, SCAN_RETURN, false);
                Thread.Sleep(20);
                SendKey(VK_RETURN, SCAN_RETURN, true);
                Thread.Sleep(40);

                // 2. Type characters cleanly via Win32 Unicode SendInput
                foreach (char c in chatCommand)
                {
                    SendUnicodeChar(c);
                    Thread.Sleep(10);
                }
                Thread.Sleep(30);

                // 3. Press Enter to submit
                SendKey(VK_RETURN, SCAN_RETURN, false);
                Thread.Sleep(20);
                SendKey(VK_RETURN, SCAN_RETURN, true);
            }
            catch { }
        }

        private static void SendUnicodeChar(char c)
        {
            var down = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)c,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            var up = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            INPUT[] inputs = new[] { down, up };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
