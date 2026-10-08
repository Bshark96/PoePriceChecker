using System;
using System.Text.RegularExpressions;

namespace GameBarWidget.Services
{
    public sealed class PoeTradeUrlInfo
    {
        public bool IsValid { get; set; }
        public string League { get; set; }
        public string SearchId { get; set; }
        public string RawUrl { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class PoeUrlParser
    {
        private static readonly Regex TradeUrlRegex = new Regex(
            @"pathofexile\.com/trade/(?:search|live)/(?<league>[^/]+)/(?<searchId>[^/?#\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static PoeTradeUrlInfo Parse(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new PoeTradeUrlInfo
                {
                    IsValid = false,
                    ErrorMessage = "URL cannot be empty."
                };
            }

            string trimmed = url.Trim();
            if (trimmed.EndsWith("/live", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 5);
            }
            else if (trimmed.EndsWith("live", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 4).TrimEnd('/');
            }

            var match = TradeUrlRegex.Match(trimmed);

            if (match.Success)
            {
                string league = Uri.UnescapeDataString(match.Groups["league"].Value ?? string.Empty).Trim();
                string searchId = match.Groups["searchId"].Value?.Trim() ?? string.Empty;
                if (searchId.EndsWith("live", StringComparison.OrdinalIgnoreCase))
                {
                    searchId = searchId.Substring(0, searchId.Length - 4).TrimEnd('/');
                }

                if (!string.IsNullOrEmpty(league) && !string.IsNullOrEmpty(searchId))
                {
                    return new PoeTradeUrlInfo
                    {
                        IsValid = true,
                        League = league,
                        SearchId = searchId,
                        RawUrl = trimmed
                    };
                }
            }

            return new PoeTradeUrlInfo
            {
                IsValid = false,
                RawUrl = trimmed,
                ErrorMessage = "Invalid Path of Exile trade URL format. Expected: pathofexile.com/trade/search/<league>/<searchId>"
            };
        }
    }
}
