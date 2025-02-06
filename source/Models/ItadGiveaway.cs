using IsThereAnyDeal.Services;
using Playnite.SDK.Data;
using System;

namespace IsThereAnyDeal.Models
{
    public class ItadGiveaway
    {
        public string TitleAll { get; set; }
        public string Title { get; set; }
        public DateTime? Time { get; set; }
        public string Link { get; set; }
        public int Count { get; set; }
        public string ShopName { get; set; }
        [DontSerialize]
        public string ShopColor => IsThereAnyDealApi.GetShopColor(ShopName);
        public bool HasSeen { get; set; }
        public bool InWaitlist { get; set; }
        public bool InCollection { get; set; }
    }
}
