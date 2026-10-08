using System;
using Windows.Storage;

namespace GameBarWidget.Services
{
    /// <summary>
    /// Manages persistent overlay and official trade settings via UWP ApplicationData.Current.LocalSettings.
    /// </summary>
    public sealed class PoeSettingsManager
    {
        private static readonly Lazy<PoeSettingsManager> _lazy = new Lazy<PoeSettingsManager>(() => new PoeSettingsManager());
        public static PoeSettingsManager Instance => _lazy.Value;

        private readonly ApplicationDataContainer _settings;

        private PoeSettingsManager()
        {
            _settings = ApplicationData.Current.LocalSettings;
        }

        // Runtime UI State
        private bool _isSettingsOpen = false;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set
            {
                if (_isSettingsOpen != value)
                {
                    _isSettingsOpen = value;
                    IsSettingsOpenChanged?.Invoke(this, value);
                    if (!value)
                    {
                        SettingsClosed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public event EventHandler<bool> IsSettingsOpenChanged;
        public event EventHandler SettingsClosed;
        public event EventHandler SettingsSaved;

        // Overlay Behavior
        public int AutoDismissDurationSeconds
        {
            get => GetValue("AutoDismissDurationSeconds", 10);
            set => SetValue("AutoDismissDurationSeconds", value);
        }

        public string Hotkey
        {
            get => GetValue("Hotkey", "CTRL+D");
            set => SetValue("Hotkey", value);
        }

        public string LiveSearchHotkey
        {
            get => GetValue("LiveSearchHotkey", "ALT+A");
            set => SetValue("LiveSearchHotkey", value);
        }

        // Official Trade API Settings: defaults to active league
        public string SelectedLeague
        {
            get => GetValue("SelectedLeague", "Standard");
            set => SetValue("SelectedLeague", value);
        }

        public string PoeSessionId
        {
            get => GetValue("PoeSessionId", string.Empty);
            set => SetValue("PoeSessionId", value);
        }

        public bool IsLoggedIn
        {
            get => GetValue("IsLoggedIn", false);
            set => SetValue("IsLoggedIn", value);
        }

        public string OAuthAccountName
        {
            get => GetValue("OAuthAccountName", string.Empty);
            set => SetValue("OAuthAccountName", value);
        }

        public string OAuthAccessToken
        {
            get => GetValue("OAuthAccessToken", string.Empty);
            set => SetValue("OAuthAccessToken", value);
        }

        public string OAuthRefreshToken
        {
            get => GetValue("OAuthRefreshToken", string.Empty);
            set => SetValue("OAuthRefreshToken", value);
        }

        public string OAuthClientId
        {
            get => GetValue("OAuthClientId", "poe_overlay");
            set => SetValue("OAuthClientId", value);
        }

        public string AccountName
        {
            get => GetValue("AccountName", string.Empty);
            set => SetValue("AccountName", value);
        }

        // Online Status Filter: defaults to "securable" (Only async / Faustus instant buyout)
        public string OnlineStatusFilter
        {
            get => GetValue("OnlineStatusFilter", "securable");
            set => SetValue("OnlineStatusFilter", value);
        }

        public bool CompactModifiers
        {
            get => GetValue("CompactModifiers", false);
            set => SetValue("CompactModifiers", value);
        }

        public bool AutoSearchOfficialTrade
        {
            get => GetValue("AutoSearchOfficialTrade", true);
            set => SetValue("AutoSearchOfficialTrade", value);
        }

        public int ModRollTolerancePercent
        {
            get => GetValue("ModRollTolerancePercent", 10);
            set => SetValue("ModRollTolerancePercent", value);
        }

        public int MaxListingsCount
        {
            get => GetValue("MaxListingsCount", 10);
            set => SetValue("MaxListingsCount", value);
        }

        public void Save()
        {
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            if (_settings.Values.TryGetValue(key, out object raw) && raw is T val)
            {
                return val;
            }
            return defaultValue;
        }

        private void SetValue<T>(string key, T value)
        {
            _settings.Values[key] = value;
        }
    }
}
