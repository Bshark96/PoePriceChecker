using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HotkeyDaemon.Forms
{
    /// <summary>
    /// Win32 Chromium WebView2 Form that handles official Path of Exile login,
    /// solves Cloudflare challenges natively, and captures the POESESSID session cookie.
    /// Runs on an explicit STA thread with autofocus and verification of actual authenticated session.
    /// </summary>
    public sealed class PoeLoginForm : Form
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly Button _manualConfirmBtn;
        private readonly System.Windows.Forms.Timer _cookiePollTimer;
        private bool _isResolved;

        public string CapturedPoeSessId { get; private set; } = string.Empty;
        public string CapturedAccountName { get; private set; } = string.Empty;

        public PoeLoginForm()
        {
            Text = "Path of Exile Account Login - Overlay Authenticator";
            Width = 640;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = Color.FromArgb(17, 25, 36);
            ForeColor = Color.FromArgb(248, 250, 252);
            ShowIcon = true;
            TopMost = true;

            // Top Status Bar
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(11, 17, 24),
                Padding = new Padding(12, 6, 12, 6)
            };

            _statusLabel = new Label
            {
                Text = "Log in with Path of Exile, Steam, or Google. POESESSID will be captured after login completes.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _manualConfirmBtn = new Button
            {
                Text = "I Am Logged In",
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _manualConfirmBtn.FlatAppearance.BorderSize = 0;
            _manualConfirmBtn.Click += async (s, e) => await ForceCaptureSessionAsync();

            topPanel.Controls.Add(_statusLabel);
            topPanel.Controls.Add(_manualConfirmBtn);

            // Chromium WebView2 Host
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(17, 25, 36),
                TabIndex = 0
            };

            Controls.Add(_webView);
            Controls.Add(topPanel);

            // Periodic Cookie & Authentication Poller
            _cookiePollTimer = new System.Windows.Forms.Timer
            {
                Interval = 1200
            };
            _cookiePollTimer.Tick += async (s, e) => await CheckForAuthenticatedSessionAsync();

            Shown += async (s, e) =>
            {
                AutoFocusWindow();
                await InitializeWebViewAsync();
            };

            Activated += (s, e) =>
            {
                _webView.Focus();
            };

            FormClosing += (s, e) =>
            {
                _cookiePollTimer.Stop();
                _cookiePollTimer.Dispose();
            };
        }

        private void AutoFocusWindow()
        {
            try
            {
                Activate();
                BringToFront();
                SetForegroundWindow(Handle);
                SetFocus(Handle);
                _webView.Focus();
            }
            catch { }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PoeGameBarOverlay",
                    "ChromiumProfile"
                );

                Directory.CreateDirectory(dataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, dataFolder);
                await _webView.EnsureCoreWebView2Async(env);

                // Configure browser settings
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = true;

                _webView.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    if (e.Uri.Contains("steamcommunity.com"))
                    {
                        _statusLabel.Text = "Steam OpenID sign-in in progress... Complete login in window.";
                        _statusLabel.ForeColor = Color.FromArgb(56, 189, 248);
                    }
                    else if (e.Uri.Contains("accounts.google.com"))
                    {
                        _statusLabel.Text = "Google authentication in progress...";
                        _statusLabel.ForeColor = Color.FromArgb(56, 189, 248);
                    }
                    else
                    {
                        _statusLabel.Text = "Navigating to Path of Exile...";
                        _statusLabel.ForeColor = Color.FromArgb(148, 163, 184);
                    }
                };

                _webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        AutoFocusWindow();
                        await CheckForAuthenticatedSessionAsync();
                    }
                };

                _cookiePollTimer.Start();
                _webView.CoreWebView2.Navigate("https://www.pathofexile.com/login");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"WebView2 initialization error: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(248, 113, 113);
            }
        }

        private async Task CheckForAuthenticatedSessionAsync()
        {
            if (_isResolved || _webView.CoreWebView2 == null) return;

            try
            {
                string currentUrl = _webView.CoreWebView2.Source ?? string.Empty;

                // 1. Must be on pathofexile.com domain (not steamcommunity or google accounts)
                if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri)) return;
                if (!uri.Host.EndsWith("pathofexile.com", StringComparison.OrdinalIgnoreCase)) return;

                // 2. Must not be stuck on login or registration entry pages
                string path = uri.AbsolutePath.ToLowerInvariant();
                if (path == "/login" || path.StartsWith("/login/") || path == "/register")
                {
                    return;
                }

                // 3. Inspect DOM for genuine logged-in user profile or logout button
                string authProbeScript = @"(function() {
                    try {
                        var logoutEl = document.querySelector('a[href*=""/logout""], form[action*=""/logout""], a[href=""/logout""]');
                        var profileEl = document.querySelector('.profile-link a, .profile-name, a[href*=""/account/view-profile/""]');
                        var loginForm = document.querySelector('#login_form, input[name=""login_email""], input[type=""password""]');

                        if (loginForm && !logoutEl) {
                            return JSON.stringify({ ok: false, name: '' });
                        }

                        if (logoutEl || profileEl) {
                            var name = profileEl ? profileEl.innerText.trim() : '';
                            return JSON.stringify({ ok: true, name: name });
                        }

                        return JSON.stringify({ ok: false, name: '' });
                    } catch(e) {
                        return JSON.stringify({ ok: false, name: '' });
                    }
                })()";

                string probeJson = await _webView.CoreWebView2.ExecuteScriptAsync(authProbeScript);
                if (string.IsNullOrEmpty(probeJson) || probeJson == "null") return;

                // Parse probe result string (handle escaped JSON from ExecuteScriptAsync)
                string unescaped = probeJson.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\");
                bool isAuth = unescaped.Contains("\"ok\":true") || unescaped.Contains("\"ok\": true");

                if (!isAuth && !path.StartsWith("/my-account") && !path.StartsWith("/trade"))
                {
                    return;
                }

                // Extract account name from probe JSON
                string detectedName = string.Empty;
                int nameIdx = unescaped.IndexOf("\"name\":\"", StringComparison.OrdinalIgnoreCase);
                if (nameIdx != -1)
                {
                    nameIdx += 8;
                    int endIdx = unescaped.IndexOf("\"", nameIdx);
                    if (endIdx != -1)
                    {
                        detectedName = unescaped.Substring(nameIdx, endIdx - nameIdx);
                    }
                }

                // 4. Retrieve genuine authenticated POESESSID cookie
                var cookieManager = _webView.CoreWebView2.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync("https://www.pathofexile.com");
                var sessCookie = cookies.FirstOrDefault(c => string.Equals(c.Name, "POESESSID", StringComparison.OrdinalIgnoreCase));

                if (sessCookie != null && !string.IsNullOrWhiteSpace(sessCookie.Value))
                {
                    _isResolved = true;
                    _cookiePollTimer.Stop();

                    CapturedPoeSessId = sessCookie.Value;
                    CapturedAccountName = detectedName;

                    _statusLabel.Text = $"Authenticated as {(!string.IsNullOrEmpty(detectedName) ? detectedName : "Account")}! Syncing with Game Bar...";
                    _statusLabel.ForeColor = Color.FromArgb(74, 222, 128);

                    await Task.Delay(700);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch { }
        }

        private async Task ForceCaptureSessionAsync()
        {
            if (_webView.CoreWebView2 == null) return;

            try
            {
                var cookieManager = _webView.CoreWebView2.CookieManager;
                var cookies = await cookieManager.GetCookiesAsync("https://www.pathofexile.com");
                var sessCookie = cookies.FirstOrDefault(c => string.Equals(c.Name, "POESESSID", StringComparison.OrdinalIgnoreCase));

                if (sessCookie != null && !string.IsNullOrWhiteSpace(sessCookie.Value))
                {
                    _isResolved = true;
                    _cookiePollTimer.Stop();

                    CapturedPoeSessId = sessCookie.Value;
                    _statusLabel.Text = "Session captured manually! Syncing...";
                    _statusLabel.ForeColor = Color.FromArgb(74, 222, 128);

                    await Task.Delay(500);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _statusLabel.Text = "No POESESSID cookie found on pathofexile.com yet. Please log in first.";
                    _statusLabel.ForeColor = Color.FromArgb(248, 113, 113);
                }
            }
            catch { }
        }
    }
}
