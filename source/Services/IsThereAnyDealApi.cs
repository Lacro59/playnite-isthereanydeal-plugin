using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using CommonPluginsShared.Extensions;
using System.Text;
using System.Net.Http;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Models.ApiWebsite;

namespace IsThereAnyDeal.Services
{
    public class IsThereAnyDealApi
    {
        private static ILogger Logger => LogManager.GetLogger();
        private static IResourceProvider ResourceProvider => new ResourceProvider();

        private string BaseUrl => "https://isthereanydeal.com";
        private string ApiUrl => "https://api.isthereanydeal.com";
        private string Key => "fa49308286edcaf76fea58926fd2ea2d216a17ff";

        public List<CountData> CountDatas { get; set; } = new List<CountData>();

        #region Api
        /// <summary>
        /// Return information about shops
        /// </summary>
        /// <param name="country"></param>
        /// <returns></returns>
        public async Task<List<ServiceShop>> GetServiceShops(string country)
        {
            Thread.Sleep(500);
            try
            {
                string url = ApiUrl + $"/service/shops/v1?country={country}";
                string data = await Web.DownloadStringData(url).ConfigureAwait(false);

                _ = Serialization.TryFromJson(data, out List<ServiceShop> serviceShops);
                return serviceShops;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetGamesPrices({country})", true, "IsThereAnyDeal");
            }

            return null;
        }


        /// <summary>
        /// Lookup game based on title
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public async Task<GameLookup> GetGamesLookup(string title)
        {
            return await GetGamesLookup(title, string.Empty);
        }

        /// <summary>
        /// Lookup game based on Steam appid
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public async Task<GameLookup> GetGamesLookup(int appId)
        {
            return await GetGamesLookup(string.Empty, appId.ToString());
        }

        /// <summary>
        /// Lookup game based on title or Steam appid
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        private async Task<GameLookup> GetGamesLookup(string title, string appId)
        {
            Thread.Sleep(500);
            try
            {
                if (!title.IsNullOrEmpty() || !appId.IsNullOrEmpty())
                {
                    string url = ApiUrl + $"/games/lookup/v1?key={Key}&"
                        + (!title.IsNullOrEmpty() 
                                ? $"title={WebUtility.UrlEncode(WebUtility.HtmlDecode(PlayniteTools.RemoveGameEdition(title)))}"
                                : $"appid={appId}");
                    string data = await Web.DownloadStringData(url);

                    _ = Serialization.TryFromJson(data, out GameLookup gamesLookup);
                    return gamesLookup;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetGamesLookup({(title.IsNullOrEmpty() ? appId : title)})", true, "IsThereAnyDeal");
            }

            return null;
        }


        /// <summary>
        /// Get games' prices
        /// </summary>
        /// <param name="country"></param>
        /// <param name="shopsId"></param>
        /// <param name="gamesId"></param>
        /// <returns></returns>
        public async Task<List<GamePrices>> GetGamesPrices(string country, List<int> shopsId, List<string> gamesId)
        {

            Thread.Sleep(500);
            try
            {
                string shops = string.Join(",", shopsId);
                string url = ApiUrl + $"/games/prices/v2?key={Key}&nondeals=true&vouchers=true&capacity=0&country={country}&shops={shops}";
                string payload = Serialization.ToJson(gamesId);
                string data = await Web.PostStringDataPayload(url, payload);

                _ = Serialization.TryFromJson(data, out List<GamePrices> gamesPrices);
                return gamesPrices;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetGamesPrices({country} / {string.Join(",", shopsId)} / {string.Join(",", gamesId)})", true, "IsThereAnyDeal");
            }

            return null;
        }
        #endregion

        #region Api from website
        public async Task<List<Country>> GetCountries()
        {
            try
            {
                string url = BaseUrl + $"/api/country/";
                string data = await Web.DownloadStringData(url);

                _ = Serialization.TryFromJson(data, out List<Country> countries);
                return countries;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetCountries()", true, "IsThereAnyDeal");
            }

            return null;
        }
        #endregion

        #region Plugin
        public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, IsThereAnyDealSettings settings, string PluginUserDataPath, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> ListWishlistSteam = new List<Wishlist>();
            if (settings.EnableSteam)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("SteamLibrary"))
                    {
                        SteamWishlist steamWishlist = new SteamWishlist(plugin);
                        ListWishlistSteam = steamWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistSteam == null)
                        {
                            ListWishlistSteam = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Steam",
                            Count = ListWishlistSteam.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Steam is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorSteam"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistSteam", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistGog = new List<Wishlist>();
            if (settings.EnableGog)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("GogLibrary"))
                    {
                        GogWishlist gogWishlist = new GogWishlist(plugin);
                        ListWishlistGog = gogWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistGog == null)
                        {
                            ListWishlistGog = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "GOG",
                            Count = ListWishlistGog.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("GOG is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-GOG-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorGog"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistGog", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistEpic = new List<Wishlist>();
            if (settings.EnableEpic)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("EpicLibrary"))
                    {
                        EpicWishlist epicWishlist = new EpicWishlist(plugin);
                        ListWishlistEpic = epicWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistEpic == null)
                        {
                            ListWishlistEpic = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Epic Game Store",
                            Count = ListWishlistEpic.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Epic Game Store is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-EpicGameStore-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorEpic"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistEpic", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistHumble = new List<Wishlist>();
            if (settings.EnableHumble)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("HumbleLibrary"))
                    {
                        HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist(plugin);
                        ListWishlistHumble = humbleBundleWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistHumble == null)
                        {
                            ListWishlistHumble = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Humble Bundle",
                            Count = ListWishlistHumble.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Humble Bundle is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-HumbleBundle-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorHumble"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistHumble", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistXbox = new List<Wishlist>();
            if (settings.EnableXbox)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("XboxLibrary"))
                    {
                        XboxWishlist xboxWishlist = new XboxWishlist(plugin);
                        ListWishlistXbox = xboxWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistXbox == null)
                        {
                            ListWishlistXbox = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Xbox",
                            Count = ListWishlistXbox.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Xbox is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Xbox-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorXbox"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistXbox", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistUbisoft = new List<Wishlist>();
            if (settings.EnableUbisoft)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("UbisoftLibrary"))
                    {
                        UbisoftWishlist ubisoftWishlist = new UbisoftWishlist(plugin);
                        ListWishlistUbisoft = ubisoftWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlistUbisoft == null)
                        {
                            ListWishlistUbisoft = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Ubisoft Connect",
                            Count = ListWishlistUbisoft.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Ubisoft is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Ubisoft-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorUbisoft"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistUbisoft", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlisOrigin = new List<Wishlist>();
            if (settings.EnableOrigin)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("OriginLibrary"))
                    {
                        OriginWishlist originWishlist = new OriginWishlist(plugin);
                        ListWishlisOrigin = originWishlist.GetWishlist(CacheOnly, ForcePrice);
                        if (ListWishlisOrigin == null)
                        {
                            ListWishlisOrigin = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Origin",
                            Count = ListWishlisOrigin.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Origin is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Origin-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorOrigin"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlisOrigin", true, "IsThereAnyDeal");
                }
            }


            List<Wishlist> ListWishlist = ListWishlistSteam
                .Concat(ListWishlistGog)
                .Concat(ListWishlistHumble)
                .Concat(ListWishlistEpic)
                .Concat(ListWishlistXbox)
                .Concat(ListWishlisOrigin)
                .Concat(ListWishlistUbisoft)
                .ToList();


            // Group same game
            IEnumerable<IGrouping<string, Wishlist>> listDuplicates = ListWishlist.GroupBy(c => PlayniteTools.NormalizeGameName(c.Name).ToLower()).Where(g => g.Skip(1).Any());
            foreach (IGrouping<string, Wishlist> duplicates in listDuplicates)
            {
                bool isFirst = true;
                Wishlist keep = new Wishlist();
                foreach (Wishlist wish in duplicates)
                {
                    if (isFirst)
                    {
                        keep = wish;
                        isFirst = false;
                    }
                    else
                    {
                        List<Wishlist> keepDuplicates = keep.Duplicates;

                        if (wish.StoreName != ListWishlist.Find(x => x == keep).StoreName)
                        {
                            keepDuplicates.Add(wish);
                            keep.Duplicates = keepDuplicates;

                            ListWishlist.Find(x => x == keep).Duplicates = keepDuplicates;
                            ListWishlist.Find(x => x == keep).hasDuplicates = true;
                            _ = ListWishlist.Remove(wish);
                        }
                    }
                }
            }


            if (!CacheOnly || ForcePrice)
            {
                settings.LastRefresh = DateTime.Now.ToUniversalTime();
                plugin.SavePluginSettings(settings);
            }


            return ListWishlist.OrderBy(wishlist => wishlist.Name).ToList();
        }

        public async Task<List<ItadShops>> GetShops(string country)
        {
            List<ServiceShop> serviceShops = await GetServiceShops(country);
            List<ItadShops> itadShops = new List<ItadShops>();
            itadShops = serviceShops?.Select(x => new ItadShops
            {
                Id = x.Id.ToString(),
                Title = x.Title,
                IsCheck = false,
                Color = string.Empty
            }).ToList();

            return itadShops;
        }

        public async Task<List<Wishlist>> GetCurrentPrice(List<Wishlist> wishlists, IsThereAnyDealSettings settings)
        {
            try
            {
                // Games list
                List<string> gamesId = wishlists
                    .Where(x => (!x.ItadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true) && (!x.Game?.Id?.IsNullOrEmpty() ?? false))
                    .Select(x => x.Game.Id)
                    .ToList();

                // Stores list
                List<int> shopsId = settings.Stores.Select(x => int.Parse(x.Id)).ToList();

                if (gamesId.Count != 0)
                {
                    // Check if in library (exclude game emulated)
                    List<Guid> ListEmulators = API.Instance.Database.Emulators.Select(x => x.Id).ToList();

                    List<GamePrices> gamesPrices = await GetGamesPrices(settings.CountrySelected.Alpha2, shopsId, gamesId);

                    wishlists.Where(x => (!x.ItadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true) && (!x.Game?.Id?.IsNullOrEmpty() ?? false))
                        .ForEach(y =>
                        {
                            ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos = new ConcurrentDictionary<string, List<ItadGameInfo>>();
                            List<ItadGameInfo> dataCurrentPrice = new List<ItadGameInfo>();

                            try
                            {
                                GamePrices gamePrices = gamesPrices.Where(x => x.Id.IsEqual(y.Game.Id))?.FirstOrDefault();
                                if (gamePrices?.Deals?.Count > 0)
                                {
                                    foreach (Deal deal in gamePrices.Deals)
                                    {
                                        try
                                        {
                                            dataCurrentPrice.Add(new ItadGameInfo
                                            {
                                                Name = y.Name,
                                                StoreId = y.StoreId,
                                                SourceId = y.SourceId,
                                                Id = y.Game.Id,
                                                Slug = y.Game.Slug,
                                                PriceNew = Math.Round(deal.Price.Amount, 2),
                                                PriceOld = Math.Round(deal.Regular.Amount, 2),
                                                PriceCut = deal.Cut,
                                                CurrencySign = GetCurrencySymbol(deal.Price.Currency),
                                                ShopName = deal.Shop.Name,
                                                UrlBuy = deal.Url
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Common.LogError(ex, false, true, "IsThereAnyDeal");
                                        }
                                    }

                                    _ = itadGameInfos.TryAdd(DateTime.Now.ToString("yyyy-MM-dd"), dataCurrentPrice);
                                    y.ItadGameInfos = itadGameInfos;
                                }
                                else
                                {
                                    Common.LogDebug(true, $"No data for {y.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true);
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return wishlists;
        }

        private string GetCurrencySymbol(string currency)
        {
            switch (currency.ToLower())
            {
                case "eur":
                    return "€";
                case "usd":
                    return "$";
                case "gpb":
                    return "£";
                case "aud":
                    return "$";
                case "brl":
                    return "R$";
                case "cad":
                    return "$";
                case "cny":
                    return "¥";
                default:
                    return currency;
            }
        }

        public static string GetShopColor(string ShopName)
        {
            Dictionary<string, string> shopColor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Adventure Shop", "#3e6517" },
                { "AllYouPlay", "#e9267b" },
                { "Amazon", "#fcc588" },
                { "Blizzard", "#00cbe6" },
                { "Bohemia Interactive Store", "#f74040" },
                { "Steam", "#9ffc3a" },
                { "GamersGate", "#fc5d5d" },
                { "Fanatical", "#ff9800" },
                { "Impulse", "#c63f62" },
                { "GamesPlanet UK", "#f6a740" },
                { "GamesPlanet DE", "#f6a740" },
                { "GamesPlanet FR", "#f6a740" },
                { "GamesPlanet US", "#f6a740" },
                { "GameTap", "#f6a740" },
                { "GreenManGaming", "#21a930" },
                { "GetGames", "#fa1f1f" },
                { "Desura", "#03bee0" },
                { "GOG", "#f16421" },
                { "DotEmu", "#f6931c" },
                { "Nuuvem", "#b5e0f4" },
                { "IndieGala Store", "#ffb4e0" },
                { "DLGamer", "#f5fe94" },
                { "GameFly", "#f0a690" },
                { "Direct2Drive", "#1df884" },
                { "EA Store", "#ddff1c" },
                { "Ubisoft Store", "#01657a" },
                { "Uplay", "#01657a" },
                { "ShinyLoot", "#bfa236" },
                { "Humble Store", "#ff3e1b" },
                { "Humble Widgets", "#f8300c" },
                { "IndieGameStand", "#73c175" },
                { "GamesRocket", "#e1bc4e" },
                { "Squenix", "#b41919" },
                { "Gameolith", "#80e5ff" },
                { "Fireflower", "#29698c" },
                { "Newegg", "#f79328" },
                { "Games Republic", "#ef0e38" },
                { "Coinplay", "#1b4284" },
                { "Funstock", "#7f3f98" },
                { "WinGameStore", "#2790da" },
                { "MacGameStore", "#2790da" },
                { "GameBillet", "#f22f15" },
                { "Sila Games", "#f9cf6b" },
                { "Playfield", "#e84c31" },
                { "Imperial Games", "#16a085" },
                { "Itch.io", "#fa5c5c" },
                { "Itchio", "#fa5c5c" },
                { "Game Jolt", "#2f7f6f" },
                { "Digital Download", "#0166ff" },
                { "DreamGame", "#497791" },
                { "Paradox", "#bc2a31" },
                { "Chrono", "#59c4c5" },
                { "TwoGame", "#523f95" },
                { "2Game", "#523f95" },
                { "Less4Games", "#ff9900" },
                { "Savemi", "#01add3" },
                { "Gemly", "#ce2745" },
                { "Voidu", "#f47820" },
                { "Cybermanta", "#00b2ee" },
                { "LBOstore", "#005268" },
                { "Razer", "#00ff00" },
                { "Microsoft Store", "#ffd800" },
                { "Oculus", "#5161a6" },
                { "Discord", "#6f85d4" },
                { "Epic", "#0078f2" },
                { "Epic Game Store", "#0078f2" },
                { "Playism", "#b8934f" },
                { "GamesLoad", "#b76cc7" },
                { "JoyBuggy", "#43c68d" },
                { "Noctre", "#1a83ff" },
                { "ETailMarket", "#358192" },
                { "ETail.Market", "#358192" }
            };

            return ShopName.IsNullOrEmpty() || !shopColor.TryGetValue(ShopName, out string value) ? "#000000" : value;
        }

        /*
        public List<ItadGiveaway> GetGiveaways(string PluginUserDataPath, bool CacheOnly = false)
        {
            // Load previous
            string PluginDirectoryCache = PluginUserDataPath + "\\cache";
            string PluginFileCache = PluginDirectoryCache + "\\giveways.json";
            List<ItadGiveaway> itadGiveawaysCache = new List<ItadGiveaway>();

            try
            {
                FileSystem.CreateDirectory(PluginDirectoryCache, false);
                if (File.Exists(PluginFileCache))
                {
                    _ = Serialization.TryFromJsonFile(PluginFileCache, out itadGiveawaysCache);
                    if (itadGiveawaysCache == null)
                    {
                        itadGiveawaysCache = new List<ItadGiveaway>();
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error in GetGiveAway() with cache data", true, "IsThereAnyDeal");
            }


            // Load on web
            List<ItadGiveaway> itadGiveaways = new List<ItadGiveaway>();
            if (!CacheOnly && itadGiveawaysCache != new List<ItadGiveaway>())
            {
                try
                {
                    string url = @"https://isthereanydeal.com/giveaways/";
                    string responseData = string.Empty;
                    try
                    {
                        responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                    }

                    if (!responseData.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(responseData);
                        foreach (IElement SearchElement in htmlDocument.QuerySelectorAll("div.giveaway"))
                        {
                            bool HasSeen = (SearchElement.ClassName.IndexOf("Seen") > -1);

                            var row1 = SearchElement.QuerySelector("div.bundle-row1");

                            DateTime? bundleTime = null;
                            if (!row1.QuerySelector("div.bundle-time").GetAttribute("title").IsNullOrEmpty())
                            {
                                bundleTime = Convert.ToDateTime(row1.QuerySelector("div.bundle-time").GetAttribute("title"));
                            }

                            string TitleAll = row1.QuerySelector("div.bundle-title a").InnerHtml.Trim();

                            List<string> arrBundleTitle = TitleAll.Split('-').ToList();

                            string bundleShop = arrBundleTitle[arrBundleTitle.Count - 1].Trim();
                            bundleShop = bundleShop.Replace("FREE Games on", string.Empty).Replace("Always FREE For", string.Empty)
                                .Replace("FREE For", string.Empty).Replace("FREE on", string.Empty);

                            string bundleTitle = string.Empty;
                            arrBundleTitle.RemoveAt(arrBundleTitle.Count - 1);
                            bundleTitle = String.Join("-", arrBundleTitle.ToArray()).Trim();

                            string bundleLink = row1.QuerySelector("div.bundle-title a").GetAttribute("href");

                            var row2 = SearchElement.QuerySelector("div.bundle-row2");

                            string bundleDescCount = row2.QuerySelector("div.bundle-desc span.lg").InnerHtml;

                            itadGiveaways.Add(new ItadGiveaway
                            {
                                TitleAll = TitleAll,
                                Title = bundleTitle,
                                Time = bundleTime,
                                Link = bundleLink,
                                ShopName = bundleShop,
                                Count = bundleDescCount,
                                HasSeen = HasSeen
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error in GetGiveAway() with web data", true, "IsThereAnyDeal");
                }
            }

            // Compare new with cache
            if (itadGiveaways.Count != 0)
            {
                Common.LogDebug(true, $"Compare with cache");
                foreach (ItadGiveaway itadGiveaway in itadGiveawaysCache)
                {
                    if (itadGiveaways.Find(x => x.TitleAll == itadGiveaway.TitleAll) != null)
                    {
                        itadGiveaways.Find(x => x.TitleAll == itadGiveaway.TitleAll).HasSeen = true;
                    }
                }
            }
            // No data
            else
            {
                Logger.Warn("No new data for GetGiveaways()");
                itadGiveaways = itadGiveawaysCache;
            }

            // Save new
            try
            {
                File.WriteAllText(PluginFileCache, Serialization.ToJson(itadGiveaways));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error in GetGiveAway() with save data", true, "IsThereAnyDeal");
            }

            return itadGiveaways;
        }
        */

        public static async Task CheckNotifications(IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            await Task.Run(() =>
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                if (settings.EnableNotification)
                {
                    List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(plugin, settings, plugin.GetPluginUserDataPath(), true, true);
                    ListWishlist.Where(x => x.Game != null && x.GetNotification(settings.NotificationCriterias))
                      .ForEach(x =>
                      {
                          API.Instance.Notifications.Add(new NotificationMessage(
                                  $"IsThereAnyDeal-{x.Game.Slug}",
                                  "IsThereAnyDeal\r\n" + string.Format(ResourceProvider.GetString("LOCItadNotification"),
                                      x.Name, x.ItadBestPrice.PriceNew, x.ItadBestPrice.CurrencySign, x.ItadBestPrice.PriceCut),
                                  NotificationType.Info,
                                  () =>
                                  {
                                      if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                                      {
                                          WindowOptions windowOptions = new WindowOptions
                                          {
                                              ShowMinimizeButton = false,
                                              ShowMaximizeButton = false,
                                              ShowCloseButton = true,
                                              CanBeResizable = false,
                                              Width = 1180,
                                              Height = 720
                                          };

                                          IsThereAnyDealView ViewExtension = new IsThereAnyDealView(plugin, settings, x.Game.Id);
                                          Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(API.Instance, ResourceProvider.GetString("LOCItad"), ViewExtension, windowOptions);
                                          _ = windowExtension.ShowDialog();
                                      }
                                      else
                                      {
                                          _ = Process.Start(x.UrlGame);
                                      }
                                  }
                              ));
                      });
                }

                //if (settings.EnableNotificationGiveaways)
                //{
                //    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(plugin.GetPluginUserDataPath());
                //    itadGiveaways.Where(x => !x.HasSeen).ForEach(x =>
                //    {
                //        API.Instance.Notifications.Add(new NotificationMessage(
                //              $"IsThereAnyDeal-{x.Title}",
                //              "IsThereAnyDeal\r\n" + string.Format(ResourceProvider.GetString("LOCItadNotificationGiveaway"), x.TitleAll, x.Count),
                //              NotificationType.Info,
                //              () => Process.Start(x.Link)
                //          ));
                //    });
                //}
            });
        }

        public static void UpdateDatas(IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCITADDataDownloading"),
                false
            )
            {
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Logger.Info($"Task UpdateDatas()");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                try
                {
                    _ = isThereAnyDealApi.LoadWishlist(plugin, settings, plugin.GetPluginUserDataPath());
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task UpdateDatas() - {string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }
        #endregion
    }
}
