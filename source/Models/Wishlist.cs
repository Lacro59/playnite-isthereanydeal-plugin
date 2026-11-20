using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using CommonPluginsShared.Images;
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
        public bool InLibrary => EnumerateMatchingLibraryGames().Any();

        /// <summary>
        /// First matching Playnite library game id (for <see cref="GoToGame"/>). Prefer the earliest match; tooltip lists all sources.
        /// </summary>
        [DontSerialize]
        public Guid GameId => EnumerateMatchingLibraryGames().FirstOrDefault()?.Id ?? default;

        /// <summary>
        /// Distinct Playnite source names for all library games matching this wishlist title (name equality, non-hidden).
        /// </summary>
        [DontSerialize]
        public List<string> LibrarySourceNames
        {
            get
            {
                List<string> names = new List<string>();
                foreach (Playnite.SDK.Models.Game game in EnumerateMatchingLibraryGames())
                {
                    string sourceName = game.Source?.Name;
                    if (sourceName.IsNullOrEmpty())
                    {
                        sourceName = "Playnite";
                    }

                    if (!names.Any(n => n.Equals(sourceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        names.Add(sourceName);
                    }
                }

                names.Sort(StringComparer.OrdinalIgnoreCase);
                return names;
            }
        }

        /// <summary>
        /// Newline-separated source names for the library badge ToolTip (same shape as <see cref="AlertCriteriaTooltip"/>).
        /// </summary>
        [DontSerialize]
        public string LibrarySourcesTooltip => string.Join("\n", LibrarySourceNames);

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
        public bool HasItadData => ItadBestPrice.HasPrice;

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

        /// <summary>
        /// Criteria snapshot used by UI bindings (<see cref="IsAlert"/> / tooltip). Not persisted.
        /// </summary>
        [DontSerialize]
        public List<ItadNotificationCriteria> NotificationCriterias { get; set; }

        /// <summary>
        /// Whether the current deals match the attached notification criteria.
        /// </summary>
        [DontSerialize]
        public bool IsAlert => GetNotification(NotificationCriterias);

        /// <summary>
        /// Localized criteria labels matched by the current deals (newline-separated for ToolTip).
        /// </summary>
        [DontSerialize]
        public string AlertCriteriaTooltip
        {
            get
            {
                List<ItadNotificationCriteria> matching = GetMatchingCriterias(NotificationCriterias);
                if (matching.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join("\n", matching.Select(c => c.Criteria).Where(s => !s.IsNullOrEmpty()));
            }
        }

        /// <summary>
        /// Attaches the settings criteria list for alert bindings on the wishlist item.
        /// </summary>
        /// <param name="criterias">Plugin notification criteria.</param>
        public void AttachNotificationCriterias(List<ItadNotificationCriteria> criterias)
        {
            NotificationCriterias = criterias;
        }

        /// <summary>
        /// Returns whether any current deal matches at least one notification criteria.
        /// </summary>
        /// <param name="criterias">Configured price-cut / price-cap rules.</param>
        /// <returns><c>true</c> when at least one deal matches.</returns>
        public bool GetNotification(List<ItadNotificationCriteria> criterias)
        {
            return GetMatchingNotificationDeals(criterias).Count > 0;
        }

        /// <summary>
        /// Lists notification criteria that match at least one current deal.
        /// </summary>
        /// <param name="criterias">Configured rules.</param>
        /// <returns>Matching criteria; never <c>null</c>.</returns>
        public List<ItadNotificationCriteria> GetMatchingCriterias(List<ItadNotificationCriteria> criterias)
        {
            List<ItadNotificationCriteria> result = new List<ItadNotificationCriteria>();
            if (criterias == null || criterias.Count == 0 || ItadLastPrice == null)
            {
                return result;
            }

            foreach (ItadNotificationCriteria criteria in criterias)
            {
                if (criteria == null)
                {
                    continue;
                }

                foreach (ItadGameInfo item in ItadLastPrice)
                {
                    if (item == null || item.TypePrice != TypePrice.Deal)
                    {
                        continue;
                    }

                    if (MatchesSingleCriteria(item, criteria))
                    {
                        result.Add(criteria);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Lists deals from the latest price snapshot that match the given criteria.
        /// </summary>
        /// <param name="criterias">Configured price-cut / price-cap rules.</param>
        /// <returns>Matching deals (may be empty); never <c>null</c>.</returns>
        public List<ItadGameInfo> GetMatchingNotificationDeals(List<ItadNotificationCriteria> criterias)
        {
            List<ItadGameInfo> matches = new List<ItadGameInfo>();
            if (criterias == null || criterias.Count == 0 || ItadLastPrice == null)
            {
                return matches;
            }

            foreach (ItadGameInfo item in ItadLastPrice)
            {
                if (item == null || item.TypePrice != TypePrice.Deal)
                {
                    continue;
                }

                if (MatchesAnyCriteria(item, criterias))
                {
                    matches.Add(item);
                }
            }

            return matches;
        }

        /// <summary>
        /// Playnite library games matching this wishlist title (case-insensitive name, not hidden).
        /// </summary>
        private IEnumerable<Playnite.SDK.Models.Game> EnumerateMatchingLibraryGames()
        {
            if (API.Instance?.Database?.Games == null || Name.IsNullOrEmpty())
            {
                return Enumerable.Empty<Playnite.SDK.Models.Game>();
            }

            return API.Instance.Database.Games.Where(x => x.Name.IsEqual(Name) && !x.Hidden);
        }

        private static bool MatchesAnyCriteria(ItadGameInfo item, List<ItadNotificationCriteria> criterias)
        {
            foreach (ItadNotificationCriteria criteria in criterias)
            {
                if (criteria != null && MatchesSingleCriteria(item, criteria))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesSingleCriteria(ItadGameInfo item, ItadNotificationCriteria criteria)
        {
            if (criteria.PriceCut > -1 && criteria.PriceInferior > -1)
            {
                return item.PriceCut >= criteria.PriceCut && item.PriceNew <= criteria.PriceInferior;
            }

            if (criteria.PriceCut > -1)
            {
                return item.PriceCut >= criteria.PriceCut;
            }

            if (criteria.PriceInferior > -1)
            {
                return item.PriceNew <= criteria.PriceInferior;
            }

            return false;
        }
    }
}
