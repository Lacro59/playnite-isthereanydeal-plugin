using System.Collections.Generic;

namespace IsThereAnyDeal.Models
{
    /// <summary>
    /// Persisted wishlist panel filter state (silent settings blob — not shown in Settings UI).
    /// Search text and alerts-only remain session-only.
    /// </summary>
    public class WishlistUiFilters
    {
        /// <summary>Include games already in the Playnite library (default on for first-run / reset).</summary>
        public bool IncludeInLibrary { get; set; } = true;

        /// <summary>Show only games already in the Playnite library.</summary>
        public bool OnlyInLibrary { get; set; }

        /// <summary>When true, discount/price filters use best price; otherwise wishlist-store price.</summary>
        public bool UseBestPrice { get; set; } = true;

        /// <summary>Minimum discount percent (0–100).</summary>
        public double DiscountPercent { get; set; }

        /// <summary>Maximum price filter value.</summary>
        public double PriceLimit { get; set; } = 100;

        /// <summary>Checked wishlist source store names (empty = all).</summary>
        public List<string> SelectedStores { get; set; } = new List<string>();

        /// <summary>Order type combo <c>Tag</c> (0–5).</summary>
        public string OrderTypeTag { get; set; } = "0";

        /// <summary>Order direction combo <c>Tag</c> (0 ascending, 1 descending).</summary>
        public string OrderDirectionTag { get; set; } = "0";

        /// <summary>Returns factory defaults for reset and first-run.</summary>
        public static WishlistUiFilters CreateDefaults()
        {
            return new WishlistUiFilters();
        }

        /// <summary>Null-safe copy with list clone for assignment onto settings.</summary>
        public WishlistUiFilters Clone()
        {
            return new WishlistUiFilters
            {
                IncludeInLibrary = IncludeInLibrary,
                OnlyInLibrary = OnlyInLibrary,
                UseBestPrice = UseBestPrice,
                DiscountPercent = DiscountPercent,
                PriceLimit = PriceLimit,
                SelectedStores = SelectedStores != null ? new List<string>(SelectedStores) : new List<string>(),
                OrderTypeTag = string.IsNullOrEmpty(OrderTypeTag) ? "0" : OrderTypeTag,
                OrderDirectionTag = string.IsNullOrEmpty(OrderDirectionTag) ? "0" : OrderDirectionTag
            };
        }
    }
}
