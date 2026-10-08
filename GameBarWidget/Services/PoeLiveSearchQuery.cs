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

            double divineRate = 150.0;
            string targetCurr = (MaxPriceCurrency ?? string.Empty).ToLowerInvariant();
            string itemCurr = (currency ?? string.Empty).ToLowerInvariant();

            double maxInChaos = targetCurr.Contains("div") ? MaxPriceAmount.Value * divineRate : MaxPriceAmount.Value;
            double itemInChaos = priceAmount;

            if (itemCurr.Contains("div"))
            {
                itemInChaos = priceAmount * divineRate;
            }
            else if (itemCurr.Contains("mirror"))
            {
                itemInChaos = priceAmount * divineRate * 600.0;
            }

            return itemInChaos <= (maxInChaos + 0.01);
        }
    }
}
