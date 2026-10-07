using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HotkeyDaemon.Services
{
    /// <summary>
    /// Captures global keyboard shortcuts using a low-level Win32 hook.
    /// Intercepts CTRL+D without interfering with the system message stream.
    /// </summary>
    public sealed class HotkeyListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // ALT key
        private const int VK_D = 0x44;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private int _targetVkCode = VK_D;
        private bool _requireCtrl = true;
        private bool _requireAlt = false;
        private string _hotkeyString = "CTRL+D";

        private bool _isKeyPressed = false;
        private DateTime _lastTriggerTime = DateTime.MinValue;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(400);

        public event EventHandler<string>? HotkeyPressed;
        public event EventHandler<string>? LogMessage;

        public string CurrentHotkey => _hotkeyString;

        public HotkeyListener()
        {
            _proc = HookCallback;
        }

        public void ConfigureHotkey(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return;
            string upper = hotkey.ToUpperInvariant().Trim();
            _hotkeyString = upper;

            _requireCtrl = upper.Contains("CTRL") || upper.Contains("CONTROL");
            _requireAlt = upper.Contains("ALT");

            if (upper.EndsWith("E")) _targetVkCode = 0x45;
            else if (upper.EndsWith("F")) _targetVkCode = 0x46;
            else if (upper.EndsWith("C")) _targetVkCode = 0x43;
            else _targetVkCode = VK_D;
        }

        public void Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                LogMessage?.Invoke(this, $"[HotkeyListener] Keyboard hook installed. Listening for {_hotkeyString}.");
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
                    if (vkCode == _targetVkCode)
                    {
                        bool isCtrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                        bool isAltPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

                        bool ctrlMatches = !_requireCtrl || isCtrlPressed;
                        bool altMatches = !_requireAlt || isAltPressed;

                        if (ctrlMatches && altMatches && !_isKeyPressed)
                        {
                            _isKeyPressed = true;
                            var now = DateTime.UtcNow;

                            if (now - _lastTriggerTime > _debounceInterval)
                            {
                                _lastTriggerTime = now;
                                string hk = _hotkeyString;
                                LogMessage?.Invoke(this, $"[HotkeyListener] Detected global hotkey: {hk}");
                                Task.Run(() => HotkeyPressed?.Invoke(this, hk));
                            }
                        }
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    if (vkCode == _targetVkCode)
                    {
                        _isKeyPressed = false;
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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        #endregion
    }
}
