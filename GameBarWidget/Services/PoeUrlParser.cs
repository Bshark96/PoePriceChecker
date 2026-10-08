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
            @"pathofexile\.com/trade/(?:search|live)/(?<league>[^/]+)/(?<searchId>[a-zA-Z0-9]+)",
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
            var match = TradeUrlRegex.Match(trimmed);

            if (match.Success)
            {
                string league = Uri.UnescapeDataString(match.Groups["league"].Value ?? string.Empty).Trim();
                string searchId = match.Groups["searchId"].Value?.Trim() ?? string.Empty;

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
