using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GameBarWidget.Services
{
    public sealed class RateLimitRule
    {
        public int MaxHits { get; set; }
        public int WindowSeconds { get; set; }
        public int LockoutSeconds { get; set; }
        public int CurrentHits { get; set; }
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;

        public bool IsNearThreshold => CurrentHits >= (int)(MaxHits * 0.85);

        public TimeSpan DelayRequired
        {
            get
            {
                if (!IsNearThreshold) return TimeSpan.Zero;
                var elapsed = DateTime.UtcNow - WindowStart;
                var window = TimeSpan.FromSeconds(WindowSeconds);
                return elapsed < window ? (window - elapsed) : TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Thread-safe sliding-window rate limiter designed for the official Path of Exile Trade API.
    /// Parses X-Rate-Limit-Account, X-Rate-Limit-Ip, and corresponding -State headers.
    /// </summary>
    public sealed class PoeTradeRateLimiter
    {
        private static readonly Lazy<PoeTradeRateLimiter> _lazy = new Lazy<PoeTradeRateLimiter>(() => new PoeTradeRateLimiter());
        public static PoeTradeRateLimiter Instance => _lazy.Value;

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly List<RateLimitRule> _ipRules = new List<RateLimitRule>();
        private readonly List<RateLimitRule> _accountRules = new List<RateLimitRule>();

        public string CurrentStatusText { get; private set; } = "NORMAL";

        private PoeTradeRateLimiter()
        {
            // Default baseline rule: 12 requests per 6 seconds
            _ipRules.Add(new RateLimitRule
            {
                MaxHits = 12,
                WindowSeconds = 6,
                LockoutSeconds = 60,
                CurrentHits = 0
            });
        }

        private DateTime _globalLockoutUntil = DateTime.MinValue;

        public async Task<HttpResponseMessage> SendThrottledAsync(HttpClient client, HttpRequestMessage request)
        {
            await _gate.WaitAsync();
            try
            {
                if (DateTime.UtcNow < _globalLockoutUntil)
                {
                    var lockDelay = _globalLockoutUntil - DateTime.UtcNow;
                    CurrentStatusText = $"RATE LIMITED ({lockDelay.TotalSeconds:F0}s)";
                    await Task.Delay(lockDelay);
                }

                TimeSpan delay = GetMaxRequiredDelay();
                if (delay > TimeSpan.Zero)
                {
                    CurrentStatusText = $"WAIT ({delay.TotalSeconds:F1}s)";
                    await Task.Delay(delay);
                }

                CurrentStatusText = "DISPATCHING";
                var response = await client.SendAsync(request);

                UpdateHeaders(response);
                return response;
            }
            finally
            {
                _gate.Release();
            }
        }

        private TimeSpan GetMaxRequiredDelay()
        {
            TimeSpan maxDelay = TimeSpan.Zero;

            foreach (var rule in _ipRules)
            {
                var delay = rule.DelayRequired;
                if (delay > maxDelay) maxDelay = delay;
            }

            foreach (var rule in _accountRules)
            {
                var delay = rule.DelayRequired;
                if (delay > maxDelay) maxDelay = delay;
            }

            return maxDelay;
        }

        private void UpdateHeaders(HttpResponseMessage response)
        {
            if (response == null) return;

            if ((int)response.StatusCode == 429)
            {
                _globalLockoutUntil = DateTime.UtcNow.AddSeconds(15);
                CurrentStatusText = "RATE LIMITED (429)";
                return;
            }

            if (response.Headers != null)
            {
                IEnumerable<string> values = null;
                if (response.Headers.TryGetValues("X-Rate-Limit-Account-State", out values) ||
                    response.Headers.TryGetValues("X-Rate-Limit-Ip-State", out values))
                {
                    foreach (var val in values)
                    {
                        var parts = val.Split(',');
                        foreach (var part in parts)
                        {
                            var sub = part.Trim().Split(':');
                            if (sub.Length >= 3 &&
                                int.TryParse(sub[0], out int hits) &&
                                int.TryParse(sub[1], out int window) &&
                                int.TryParse(sub[2], out int lockout))
                            {
                                if (lockout > 0)
                                {
                                    _globalLockoutUntil = DateTime.UtcNow.AddSeconds(lockout);
                                    CurrentStatusText = $"LOCKED OUT ({lockout}s)";
                                }
                            }
                        }
                    }
                }
            }

            foreach (var rule in _ipRules)
            {
                rule.CurrentHits++;
                if (DateTime.UtcNow - rule.WindowStart > TimeSpan.FromSeconds(rule.WindowSeconds))
                {
                    rule.WindowStart = DateTime.UtcNow;
                    rule.CurrentHits = 1;
                }
            }
        }
    }
}
