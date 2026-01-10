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
using IsThereAnyDeal.Models.Api;

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

        public DateTime? ReleaseDate { get; set; }
        [DontSerialize]
		public bool IsReleaseDateNull => ReleaseDate == null || ReleaseDate == default || ((DateTime)ReleaseDate).Year == 1;

		public DateTime? Added { get; set; }

        public string Capsule { get; set; }

        [DontSerialize]
        public BitmapImage CapsuleImage => ImageSourceManagerPlugin.GetImage(Capsule, false, new BitmapLoadProperties(200, 200));

        [DontSerialize]
        public string CapsuleImagePath => ImageSourceManagerPlugin.GetImagePath(Capsule);

        public Game Game { get; set; }

        [DontSerialize]
        public RelayCommand<Guid> GoToGame => new RelayCommand<Guid>((Id) =>
        {
            API.Instance.MainView.SelectGame(Id);
            API.Instance.MainView.SwitchToLibraryView();
        });

        [DontSerialize]
        public bool InLibrary => API.Instance.Database.Games.Where(x => x.Name.IsEqual(Name) && !x.Hidden)?.Count() > 0;

        [DontSerialize]
        public Guid GameId => API.Instance.Database.Games.Where(x => x.Name.IsEqual(Name) && !x.Hidden)?.FirstOrDefault()?.Id ?? default;

        // TODO
        public bool IsActive { get; set; }

        [DontSerialize]
        public bool IsFind => Game != null;

        public ConcurrentDictionary<string, List<ItadGameInfo>> ItadGameInfos { get; set; }

        public List<ItadGameInfo> ItadLow { get; set; } = new List<ItadGameInfo>();

        [DontSerialize]
        public string StoreNameIcon
        {
            get
            {
                string storeNameIcon = TransformIcon.Get(StoreName);
                if (hasDuplicates)
                {
                    foreach (Wishlist duplicate in Duplicates)
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

                if (ItadGameInfos != null)
                {
                    foreach (KeyValuePair<string, List<ItadGameInfo>> item in ItadGameInfos)
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
        public ItadGameInfo ItadBestPrice => ItadLastPrice?.OrderBy(item => item.PriceNew).FirstOrDefault(x => x.TypePrice == TypePrice.Deal) ?? new ItadGameInfo();

        [DontSerialize]
        public ItadGameInfo ItadAllTimeLow => ItadLow.FirstOrDefault(x => x.TypePrice == TypePrice.All) ?? new ItadGameInfo();

        [DontSerialize]
        public ItadGameInfo ItadY1 => ItadLow.FirstOrDefault(x => x.TypePrice == TypePrice.Y1) ?? new ItadGameInfo();

        [DontSerialize]
        public ItadGameInfo ItadM3 => ItadLow.FirstOrDefault(x => x.TypePrice == TypePrice.M3) ?? new ItadGameInfo();

        [DontSerialize]
        public ItadGameInfo ItadPriceForWishlistStore => ItadLastPrice?.FirstOrDefault(item => item.ShopName?.ToLower().Contains(StoreName.ToLower()) == true) ?? new ItadGameInfo();

        [DontSerialize]
        public bool HasItadData => ItadBestPrice.CurrencySign != null && !ItadBestPrice.CurrencySign.IsNullOrEmpty();

        [DontSerialize]
        public string UrlGame => Game == null ? string.Empty : string.Format("https://isthereanydeal.com/game/{0}/info/", Game.Slug);

        [DontSerialize]
        public List<Wishlist> Duplicates = new List<Wishlist>();

        [DontSerialize]
        public bool hasDuplicates = false;

        [DontSerialize]
        public List<ItadGameInfo> ListItadPriceForWishlistStore
        {
            get
            {
                List<ItadGameInfo> list = new List<ItadGameInfo> { ItadPriceForWishlistStore };
                if (hasDuplicates)
                {
                    foreach (Wishlist duplicate in Duplicates)
                    {
                        list.Add(duplicate.ItadPriceForWishlistStore);
                    }
                }

                //When no data or not active
                if (list.Count == 0 || list[0].ShopName.IsNullOrEmpty())
                {
                    list = new List<ItadGameInfo>
                    {
                        // Store
                        new ItadGameInfo
                        {
                            Name = Name,
                            StoreId = StoreId,
                            SourceId = SourceId,
                            ShopName = StoreName,
                            UrlBuy = StoreUrl
                        }
                    };

                    // Duplicate
                    if (hasDuplicates)
                    {
                        foreach (Wishlist duplicate in Duplicates)
                        {
                            list.Add(new ItadGameInfo
                            {
                                Name = duplicate.Name,
                                StoreId = duplicate.StoreId,
                                SourceId = duplicate.SourceId,
                                ShopName = duplicate.StoreName,
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
                foreach (ItadNotificationCriteria Criteria in Criterias)
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
}