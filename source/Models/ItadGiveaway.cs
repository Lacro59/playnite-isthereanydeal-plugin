using CommonPluginsShared;
using CommonPluginsShared.Images;
using IsThereAnyDeal.Services;
using Playnite.SDK.Data;
using System;
using System.Windows.Media;

namespace IsThereAnyDeal.Models
{
    /// <summary>
    /// Giveaway entry for the plugin UI (API fields + list helpers).
    /// </summary>
    public class ItadGiveaway
    {
        public string TitleAll { get; set; }
        public string Title { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? Time { get; set; }
        public string Link { get; set; }
        public int Count { get; set; }
        public string ShopName { get; set; }
        public int? ShopId { get; set; }

        [DontSerialize]
        public string ShopColor => IsThereAnyDealApi.GetShopColor(ShopName);

        [DontSerialize]
        public Brush ShopColorBrush
        {
            get
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ShopColor));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public string Banner145 { get; set; }
        public string Boxart { get; set; }

        /// <summary>
        /// List vignette URL: <c>banner145</c> with fallback to <c>boxart</c>.
        /// </summary>
        [DontSerialize]
        public string ImageUrl => !string.IsNullOrEmpty(Banner145) ? Banner145 : Boxart;

        [DontSerialize]
        public string ImagePath => ImageSourceManagerPlugin.GetImagePath(ImageUrl, 145);

        [DontSerialize]
        public bool HasImage => !string.IsNullOrEmpty(ImageUrl);

        [DontSerialize]
        public bool HasStart => Start.HasValue;

        [DontSerialize]
        public bool HasExpiry => Time.HasValue;

        [DontSerialize]
        public bool IsExpired => Time.HasValue && Time.Value < DateTime.Now;

        [DontSerialize]
        public bool HasMultipleGames => Count > 1;

        [DontSerialize]
        public string ExtraGamesLabel => HasMultipleGames ? $"+{Count - 1}" : string.Empty;

        public bool HasSeen { get; set; }
        public bool InWaitlist { get; set; }
        public bool InCollection { get; set; }
        public bool IsMature { get; set; }
        public bool DrmFree { get; set; }
        public int Comments { get; set; }
    }
}
