using Newtonsoft.Json;
using System;

namespace IsThereAnyDeal.Models
{
    public class ItadGameInfo
    {
        public string Name { get; set; }
        public string Plain { get; set; }
        public string StoreId { get; set; }
        public Guid SourceId { get; set; }
        public double PriceNew { get; set; }
        public double PriceOld { get; set; }
        public double PriceCut { get; set; }
        public string CurrencySign { get; set; }
        public string ShopName { get; set; }
        public string ShopColor { get; set; }
        public string UrlBuy { get; set; }

        [JsonIgnore]
        public string UrlGame
        {
            get
            {
                return string.Format("https://isthereanydeal.com/game/{0}/info/", Plain);
            }
        }
    }
}
