using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HotkeyDaemon.Services
{
    public sealed class HotkeySpec
    {
        public string Name { get; set; } = string.Empty;
        public int VkCode { get; set; }
        public bool RequireCtrl { get; set; }
        public bool RequireAlt { get; set; }
        public bool IsLiveSearch { get; set; }
        public bool IsDismiss { get; set; }
    }

    public sealed class HotkeyListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private readonly List<HotkeySpec> _specs = new List<HotkeySpec>();
        private bool _isKeyPressed = false;
        private DateTime _lastTriggerTime = DateTime.MinValue;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(400);

        public event EventHandler<HotkeySpec>? HotkeyPressed;
        public event EventHandler<string>? LogMessage;

        public HotkeyListener()
        {
            _proc = HookCallback;
            ConfigureHotkeys("CTRL+D", "ALT+A");
        }

        public void ConfigureHotkeys(string priceCheckHotkey, string liveSearchHotkey)
        {
            _specs.Clear();

            var priceSpec = ParseHotkey(priceCheckHotkey, false);
            if (priceSpec != null) _specs.Add(priceSpec);

            var liveSpec = ParseHotkey(liveSearchHotkey, true);
            if (liveSpec != null) _specs.Add(liveSpec);

            // Register global ESC key for instant overlay dismissal
            _specs.Add(new HotkeySpec
            {
                Name = "ESC",
                VkCode = 0x1B, // VK_ESCAPE
                RequireCtrl = false,
                RequireAlt = false,
                IsDismiss = true
            });

            LogMessage?.Invoke(this, $"[HotkeyListener] Configured hotkeys: PriceCheck={priceCheckHotkey}, LiveSearch={liveSearchHotkey}");
        }

        public void ConfigureHotkey(string hotkey)
        {
            ConfigureHotkeys(hotkey, "ALT+A");
        }

        private static HotkeySpec? ParseHotkey(string hotkey, bool isLiveSearch)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return null;
            string upper = hotkey.ToUpperInvariant().Trim();

            bool reqCtrl = upper.Contains("CTRL") || upper.Contains("CONTROL");
            bool reqAlt = upper.Contains("ALT");

            int vk = 0x44; // Default 'D'
            char lastChar = upper[upper.Length - 1];
            if (lastChar >= 'A' && lastChar <= 'Z')
            {
                vk = 0x41 + (lastChar - 'A');
            }

            return new HotkeySpec
            {
                Name = upper,
                VkCode = vk,
                RequireCtrl = reqCtrl,
                RequireAlt = reqAlt,
                IsLiveSearch = isLiveSearch
            };
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                LogMessage?.Invoke(this, "[HotkeyListener] Keyboard hook installed.");
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                LogMessage?.Invoke(this, "[HotkeyListener] Keyboard hook uninstalled.");
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr moduleHandle = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    bool isCtrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool isAltPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

                    foreach (var spec in _specs)
                    {
                        if (vkCode == spec.VkCode)
                        {
                            bool ctrlMatches = !spec.RequireCtrl || isCtrlPressed;
                            bool altMatches = !spec.RequireAlt || isAltPressed;

                            if (ctrlMatches && altMatches && !_isKeyPressed)
                            {
                                _isKeyPressed = true;
                                var now = DateTime.UtcNow;

                                if (now - _lastTriggerTime > _debounceInterval)
                                {
                                    _lastTriggerTime = now;
                                    LogMessage?.Invoke(this, $"[HotkeyListener] Detected global hotkey: {spec.Name} (LiveSearch={spec.IsLiveSearch})");
                                    Task.Run(() => HotkeyPressed?.Invoke(this, spec));
                                }
                                break;
                            }
                        }
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    foreach (var spec in _specs)
                    {
                        if (vkCode == spec.VkCode)
                        {
                            _isKeyPressed = false;
                            break;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }

        #region Win32 P/Invoke

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetAsyncKeyState(int vKey);

        #endregion
    }
}
