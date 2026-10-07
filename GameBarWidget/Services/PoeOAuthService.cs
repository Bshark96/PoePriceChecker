using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace GameBarWidget.Services
{
    public sealed class OAuthResult
    {
        public bool Success { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implements the official Grinding Gear Games (GGG) OAuth 2.0 PKCE flow with local loopback listener.
    /// </summary>
    public sealed class PoeOAuthService
    {
        private static readonly Lazy<PoeOAuthService> _lazy = new Lazy<PoeOAuthService>(() => new PoeOAuthService());
        public static PoeOAuthService Instance => _lazy.Value;

        private const string AuthEndpoint = "https://www.pathofexile.com/oauth/authorize";
        private const string TokenEndpoint = "https://www.pathofexile.com/oauth/token";
        private const string ProfileEndpoint = "https://api.pathofexile.com/profile";
        private const int CallbackPort = 59842;
        private static readonly string CallbackUri = $"http://127.0.0.1:{CallbackPort}/oauth/callback/";

        private readonly HttpClient _httpClient;

        private PoeOAuthService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PoeGameBarOverlay/1.0 (OAuth PKCE Client)");
        }

        public async Task<OAuthResult> StartOAuthFlowAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = PoeSettingsManager.Instance.OAuthClientId;
            }
            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = "poe_overlay";
            }

            // 1. Generate PKCE Verifier & Challenge
            string verifier = GenerateCodeVerifier();
            string challenge = GenerateCodeChallenge(verifier);
            string state = Guid.NewGuid().ToString("N");

            // 2. Start local HTTP callback listener
            HttpListener? listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(CallbackUri);
                listener.Start();
            }
            catch (Exception ex)
            {
                return new OAuthResult
                {
                    Success = false,
                    ErrorMessage = $"Could not start local listener on port {CallbackPort}: {ex.Message}"
                };
            }

            try
            {
                // 3. Construct Authorization URL
                string authUrl = $"{AuthEndpoint}?client_id={WebUtility.UrlEncode(clientId)}" +
                                $"&response_type=code" +
                                $"&scope=account:profile%20account:stashes" +
                                $"&redirect_uri={WebUtility.UrlEncode(CallbackUri)}" +
                                $"&code_challenge={WebUtility.UrlEncode(challenge)}" +
                                $"&code_challenge_method=S256" +
                                $"&state={WebUtility.UrlEncode(state)}";

                // 4. Open browser for user authorization
                await Launcher.LaunchUriAsync(new Uri(authUrl));

                // 5. Wait for browser redirect with timeout (120 seconds)
                var contextTask = listener.GetContextAsync();
                var timeoutTask = Task.Delay(120000);

                var completed = await Task.WhenAny(contextTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    return new OAuthResult
                    {
                        Success = false,
                        ErrorMessage = "Authentication timed out waiting for browser approval."
                    };
                }

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                // Extract query parameters
                string? code = request.QueryString["code"];
                string? receivedState = request.QueryString["state"];
                string? error = request.QueryString["error"];
                string? errorDescription = request.QueryString["error_description"];

                // Send response page to browser
                string responseHtml;
                if (!string.IsNullOrEmpty(code) && string.Equals(state, receivedState, StringComparison.Ordinal))
                {
                    responseHtml = @"<!DOCTYPE html><html><head><title>Authorization Successful</title><style>body{background:#0b1118;color:#f8fafc;font-family:sans-serif;text-align:center;padding-top:60px;}h1{color:#4ade80;}p{color:#94a3b8;}</style></head><body><h1>Authorization Successful</h1><p>You may now close this tab and return to your Path of Exile Game Bar overlay.</p></body></html>";
                }
                else
                {
                    responseHtml = $"<!DOCTYPE html><html><head><title>Authorization Failed</title><style>body{{background:#0b1118;color:#f8fafc;font-family:sans-serif;text-align:center;padding-top:60px;}}h1{{color:#f87171;}}p{{color:#94a3b8;}}</style></head><body><h1>Authorization Failed</h1><p>{error ?? "State mismatch or denied request"}: {errorDescription}</p></body></html>";
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=utf-8";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                if (string.IsNullOrEmpty(code))
                {
                    return new OAuthResult
                    {
                        Success = false,
                        ErrorMessage = errorDescription ?? error ?? "Authorization was denied by user."
                    };
                }

                // 6. Exchange Authorization Code for Tokens
                var tokenResult = await ExchangeCodeForTokenAsync(clientId, code, verifier);
                if (!tokenResult.Success)
                {
                    return tokenResult;
                }

                // 7. Fetch account profile
                string accountName = await FetchProfileAccountNameAsync(tokenResult.AccessToken);
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    accountName = "Connected Account";
                }

                tokenResult.AccountName = accountName;

                // 8. Persist settings
                var settings = PoeSettingsManager.Instance;
                settings.OAuthAccessToken = tokenResult.AccessToken;
                settings.OAuthRefreshToken = tokenResult.RefreshToken;
                settings.OAuthAccountName = accountName;
                settings.IsLoggedIn = true;

                return tokenResult;
            }
            catch (Exception ex)
            {
                return new OAuthResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch { }
            }
        }

        private async Task<OAuthResult> ExchangeCodeForTokenAsync(string clientId, string code, string verifier)
        {
            try
            {
                var dict = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", CallbackUri },
                    { "code_verifier", verifier }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(dict)
                };

                var response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string err = ExtractJsonString(json, "error_description");
                    if (string.IsNullOrEmpty(err)) err = ExtractJsonString(json, "error");
                    if (string.IsNullOrEmpty(err)) err = $"HTTP {(int)response.StatusCode}";

                    return new OAuthResult
                    {
                        Success = false,
                        ErrorMessage = $"Token exchange failed: {err}"
                    };
                }

                string accessToken = ExtractJsonString(json, "access_token");
                string refreshToken = ExtractJsonString(json, "refresh_token");
                string username = ExtractJsonString(json, "username");

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return new OAuthResult
                    {
                        Success = false,
                        ErrorMessage = "No access_token returned by GGG server."
                    };
                }

                return new OAuthResult
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    AccountName = username
                };
            }
            catch (Exception ex)
            {
                return new OAuthResult
                {
                    Success = false,
                    ErrorMessage = $"Token exchange exception: {ex.Message}"
                };
            }
        }

        private async Task<string> FetchProfileAccountNameAsync(string accessToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, ProfileEndpoint);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    string name = ExtractJsonString(json, "name");
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base64UrlEncode(bytes);
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            byte[] challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(challengeBytes);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            string base64 = Convert.ToBase64String(input);
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            string pattern = $"\"{key}\":\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1) return string.Empty;
            idx += pattern.Length;
            int endIdx = json.IndexOf("\"", idx);
            if (endIdx == -1) return string.Empty;
            return json.Substring(idx, endIdx - idx);
        }
    }
}
