using Newtonsoft.Json;
using PluginCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IsThereAnyDeal.Models
{
    public class Wishlist
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; }
        public string StoreUrl { get; set; }
        public string Name { get; set; }
        public Guid SourceId { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Capsule { get; set; }
        public string Plain { get; set; }
        public bool InLibrary { get; set; }
        public ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos { get; set; }

        [JsonIgnore]
        public string StoreNameIcon
        {
            get
            {
                string storeNameIcon = TransformIcon.Get(StoreName);
                if (hasDuplicates)
                {
                    foreach (var duplicate in Duplicates)
                    {
                        storeNameIcon += " " + TransformIcon.Get(duplicate.StoreName);
                    }
                }
                return storeNameIcon;
            }
        }
        [JsonIgnore]
        public List<ItadGameInfo> ItadLastPrice
        {
            get
            {
                List<ItadGameInfo> Result = new List<ItadGameInfo>();
                DateTime last = Convert.ToDateTime("1982-12-15");
                foreach (var item in itadGameInfos)
                {
                    if (Convert.ToDateTime(item.Key) > last)
                    {
                        Result = item.Value;
                    }
                }
                return Result;
            }
        }
        [JsonIgnore]
        public ItadGameInfo ItadBestPrice
        {
            get
            {
                ItadGameInfo Result = new ItadGameInfo();
                double last = 5000;
                foreach (var item in ItadLastPrice)
                {
                    if (item.PriceNew < last)
                    {
                        last = item.PriceNew;
                        Result = item;
                    }
                }
                return Result;
            }
        }
        [JsonIgnore]
        public ItadGameInfo ItadPriceForWishlistStore
        {
            get
            {
                ItadGameInfo Result = new ItadGameInfo();
                foreach (var item in ItadLastPrice)
                {
                    if (item.ShopName.ToLower().IndexOf(StoreName.ToLower()) > -1)
                    {
                        Result = item;
                    }
                }
                return Result;
            }
        }

        [JsonIgnore]
        public List<Wishlist> Duplicates = new List<Wishlist>();
        [JsonIgnore]
        public bool hasDuplicates = false;
        [JsonIgnore]
        public List<ItadGameInfo> ListItadPriceForWishlistStore
        {
            get
            {
                List<ItadGameInfo> list = new List<ItadGameInfo>();
                list.Add(ItadPriceForWishlistStore);
                if (hasDuplicates)
                {
                    foreach(var duplicate in Duplicates)
                    {
                        list.Add(duplicate.ItadPriceForWishlistStore);
                    }
                }
                return list;
            }
        }

        public bool GetNotification(int LimitNotification)
        {
            List<ItadGameInfo> Result = new List<ItadGameInfo>();
            foreach (var item in ItadLastPrice)
            {
                if (item.PriceCut >= LimitNotification)
                {
                    return true;
                }
            }
            return false;
        }

        public bool GetNotificationPrice(int LimitNotificationPrice)
        {
            List<ItadGameInfo> Result = new List<ItadGameInfo>();
            foreach (var item in ItadLastPrice)
            {
                if (item.PriceNew <= LimitNotificationPrice)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
