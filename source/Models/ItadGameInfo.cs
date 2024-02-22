using IsThereAnyDeal.Services;
using Playnite.SDK.Data;
using System;

namespace IsThereAnyDeal.Models
{
    public class ItadGameInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Slug { get; set; }
        public string StoreId { get; set; }
        public Guid SourceId { get; set; }
        public double PriceNew { get; set; }
        public double PriceOld { get; set; }
        public double PriceCut { get; set; }
        public string CurrencySign { get; set; }
        public string ShopName { get; set; }
        [DontSerialize]
        public string ShopColor => IsThereAnyDealApi.GetShopColor(ShopName);
        public string UrlBuy { get; set; }

        [DontSerialize]
        public string UrlGame => string.Format("https://isthereanydeal.com/game/{0}/info/", Slug);
    }
}
