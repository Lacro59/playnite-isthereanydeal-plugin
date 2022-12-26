using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CommonPluginsShared.Extensions;
using CommonPlayniteShared;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;

namespace IsThereAnyDeal.Models
{
    public class Wishlist
    {
        public string StoreId { get; set; }
        public string StoreName { get; set; }
        public string ShopColor { get; set; }
        public string StoreUrl { get; set; }
        public string Name { get; set; }
        public Guid SourceId { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Capsule { get; set; }
        [DontSerialize]
        public BitmapImage CapsuleImage => ImageSourceManagerPlugin.GetImage(Capsule, false, new BitmapLoadProperties(200, 200));
        public string Plain { get; set; }

        [DontSerialize]
        public bool InLibrary => API.Instance.Database.Games.Where(x => x.Name.IsEqual(Name) && !x.Hidden)?.Count() > 0;

        public bool IsActive { get; set; }
        public ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos { get; set; }

        [DontSerialize]
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
        [DontSerialize]
        public List<ItadGameInfo> ItadLastPrice
        {
            get
            {
                List<ItadGameInfo> Result = new List<ItadGameInfo>();
                DateTime last = Convert.ToDateTime("1982-12-15");

                if (itadGameInfos != null)
                {
                    foreach (var item in itadGameInfos)
                    {
                        if (Convert.ToDateTime(item.Key) > last)
                        {
                            Result = item.Value;
                        }
                    }
                }

                return Result;
            }
        }
        [DontSerialize]
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
        [DontSerialize]
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

        [DontSerialize]
        public bool HasItadData => ItadBestPrice.CurrencySign != null && !ItadBestPrice.CurrencySign.IsNullOrEmpty();

        [DontSerialize]
        public string UrlGame => string.Format("https://isthereanydeal.com/game/{0}/info/", Plain);

        [DontSerialize]
        public List<Wishlist> Duplicates = new List<Wishlist>();
        [DontSerialize]
        public bool hasDuplicates = false;
        [DontSerialize]
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

                //When no data or not active
                if (list.Count == 0 || list[0].ShopName.IsNullOrEmpty())
                {
                    list = new List<ItadGameInfo>();

                    // Store
                    list.Add(new ItadGameInfo
                    {
                        Name = Name,
                        StoreId = StoreId,
                        SourceId = SourceId,
                        ShopName = StoreName,
                        ShopColor = ShopColor,
                        UrlBuy = StoreUrl
                    });

                    // Duplicate
                    if (hasDuplicates)
                    {
                        foreach (var duplicate in Duplicates)
                        {
                            list.Add(new ItadGameInfo
                            {
                                Name = duplicate.Name,
                                StoreId = duplicate.StoreId,
                                SourceId = duplicate.SourceId,
                                ShopName = duplicate.StoreName,
                                ShopColor = duplicate.ShopColor,
                                UrlBuy = duplicate.StoreUrl
                            });
                        }
                    }
                }
                return list;
            }
        }

        public bool GetNotification(List<ItadNotificationCriteria> Criterias)
        {
            foreach (ItadGameInfo item in ItadLastPrice)
            {
                foreach(ItadNotificationCriteria Criteria in Criterias)
                {
                    if (Criteria.PriceCut > -1 && Criteria.PriceInferior > -1)
                    {
                        if (item.PriceCut >= Criteria.PriceCut && item.PriceNew <= Criteria.PriceInferior)
                        {
                            return true;
                        }
                    }
                    else if (Criteria.PriceCut > -1)
                    {
                        if (item.PriceCut >= Criteria.PriceCut)
                        {
                            return true;
                        }
                    }
                    else if (Criteria.PriceInferior > -1)
                    {
                        if (item.PriceNew <= Criteria.PriceInferior)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public class WishlistIgnore
    {
        public string StoreId { get; set; }
        public string StoreName { get; set; }
        public string Name { get; set; }
        public string Plain { get; set; }
    }
}
