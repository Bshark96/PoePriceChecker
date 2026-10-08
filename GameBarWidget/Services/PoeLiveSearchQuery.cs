using System;

namespace GameBarWidget.Services
{
    public sealed class PoeLiveSearchQuery
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SearchId { get; set; }
        public string League { get; set; }
        public string Label { get; set; }
        public string RawUrl { get; set; }
        public double? MaxPriceAmount { get; set; }
        public string MaxPriceCurrency { get; set; } = "divine";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool MatchesPriceThreshold(double priceAmount, string currency)
        {
            if (!MaxPriceAmount.HasValue || MaxPriceAmount.Value <= 0)
            {
                return true;
            }

            string targetCurr = (MaxPriceCurrency ?? string.Empty).ToLowerInvariant();
            string itemCurr = (currency ?? string.Empty).ToLowerInvariant();

            if (targetCurr.Contains("div") && itemCurr.Contains("div"))
            {
                return priceAmount <= MaxPriceAmount.Value;
            }
            if (targetCurr.Contains("chaos") && itemCurr.Contains("chaos"))
            {
                return priceAmount <= MaxPriceAmount.Value;
            }

            // Fallback estimation
            return priceAmount <= MaxPriceAmount.Value;
        }
    }
}
