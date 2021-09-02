using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace IsThereAnyDeal.Services
{
    class IsThereAnyDealApi
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private readonly string baseAddress = "https://api.isthereanydeal.com/";
        private readonly string key = "fa49308286edcaf76fea58926fd2ea2d216a17ff";

        public List<CountData> countDatas = new List<CountData>();

        public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, string PluginUserDataPath, bool CacheOnly = false)
        {
            Guid SteamId = new Guid();
            Guid GogId = new Guid();
            Guid EpicId = new Guid();
            Guid HumbleId = new Guid();
            Guid XboxId = new Guid();
            Guid OriginId = new Guid();

            foreach (var Source in PlayniteApi.Database.Sources)
            {
                if (Source.Name.ToLower() == "steam")
                {
                    SteamId = Source.Id;
                }

                if (Source.Name.ToLower() == "gog")
                {
                    GogId = Source.Id;
                }

                if (Source.Name.ToLower() == "epic")
                {
                    EpicId = Source.Id;
                }

                if (Source.Name.ToLower() == "humble")
                {
                    HumbleId = Source.Id;
                }

                if (Source.Name.ToLower() == "xbox")
                {
                    XboxId = Source.Id;
                }

                if (Source.Name.ToLower() == "origin")
                {
                    OriginId = Source.Id;
                }
            }


            List<Wishlist> ListWishlistSteam = new List<Wishlist>();
            if (settings.EnableSteam)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("SteamLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        SteamWishlist steamWishlist = new SteamWishlist();
                        ListWishlistSteam = steamWishlist.GetWishlist(PlayniteApi, SteamId, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "Steam",
                            Count = ListWishlistSteam.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - Steam is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorSteam"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch(Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlistSteam");
                }
            }

            List<Wishlist> ListWishlistGog = new List<Wishlist>();
            if (settings.EnableGog)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("GogLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        GogWishlist gogWishlist = new GogWishlist(PlayniteApi);
                        ListWishlistGog = gogWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "GOG",
                            Count = ListWishlistGog.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - GOG is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-GOG-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorGog"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlistGog");
                }
            }

            List<Wishlist> ListWishlistEpic = new List<Wishlist>();
            if (settings.EnableEpic)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("EpicLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        EpicWishlist epicWishlist = new EpicWishlist();
                        ListWishlistEpic = epicWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "Epic Game Store",
                            Count = ListWishlistEpic.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - Epic Game Store is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-EpicGameStore-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorEpic"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlistEpic");
                }
            }

            List<Wishlist> ListWishlistHumble = new List<Wishlist>();
            if (settings.EnableHumble)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("HumbleLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                        ListWishlistHumble = humbleBundleWishlist.GetWishlist(PlayniteApi, HumbleId, settings.HumbleKey, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "Humble Bundle",
                            Count = ListWishlistHumble.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - Humble Bundle is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-HumbleBundle-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorHumble"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlistHumble");
                }
            }

            List<Wishlist> ListWishlistXbox = new List<Wishlist>();
            if (settings.EnableXbox)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("XboxLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        XboxWishlist xboxWishlist = new XboxWishlist();
                        ListWishlistXbox = xboxWishlist.GetWishlist(PlayniteApi, XboxId, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "Xbox",
                            Count = ListWishlistXbox.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - Xbox is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Xbox-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorXbox"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlistXbox");
                }
            }

            List<Wishlist> ListWishlisOrigin = new List<Wishlist>();
            if (settings.EnableOrigin)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("OriginLibrary", PlayniteApi.Paths.ConfigurationPath))
                    {
                        OriginWishlist originWishlist = new OriginWishlist();
                        ListWishlisOrigin = originWishlist.GetWishlist(PlayniteApi, OriginId, PluginUserDataPath, settings, CacheOnly);
                        countDatas.Add(new CountData
                        {
                            StoreName = "Origin",
                            Count = ListWishlisOrigin.Count
                        });
                    }
                    else
                    {
                        logger.Warn("IsThereAnyDeal - Origin is enable then disabled");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Origin-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorOrigin"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on ListWishlisOrigin");
                }
            }


            List<Wishlist> ListWishlist = ListWishlistSteam.Concat(ListWishlistGog).Concat(ListWishlistHumble)
                .Concat(ListWishlistEpic).Concat(ListWishlistXbox).Concat(ListWishlisOrigin).ToList();


            // Group same game
            var listDuplicates = ListWishlist.GroupBy(c => Common.NormalizeGameName(c.Name).ToLower()).Where(g => g.Skip(1).Any());
            foreach (var duplicates in listDuplicates)
            {
                bool isFirst = true;
                Wishlist keep = new Wishlist();
                foreach(var wish in duplicates)
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
                            ListWishlist.Remove(wish);
                        }
                    }
                }
            }

            return ListWishlist.OrderBy(wishlist => wishlist.Name).ToList();
        }


        public List<ItadRegion> GetCoveredRegions()
        {
            List<ItadRegion> itadRegions = new List<ItadRegion>();
            try
            {
                string responseData = string.Empty;
                string url = baseAddress + "v01/web/regions/";
                try
                { 
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                }

                JObject datasObj = JObject.Parse(responseData);
                if (((JObject)datasObj["data"]).Count > 0)
                {
                    foreach (var dataObj in ((JObject)datasObj["data"]))
                    {
                        List<string> countries = new List<string>();
                        foreach (string country in ((JArray)dataObj.Value["countries"]))
                        {
                            countries.Add(country);
                        }

                        itadRegions.Add(new ItadRegion
                        {
                            Region = dataObj.Key,
                            CurrencyName = (string)dataObj.Value["currency"]["name"],
                            CurrencyCode = (string)dataObj.Value["currency"]["code"],
                            CurrencySign = (string)dataObj.Value["currency"]["sign"],
                            Countries = countries
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", "Error to parse downloaded data in GetCoveredRegions()");
            }

            return itadRegions;
        }

        public List<ItadStore> GetRegionStores(string region, string country)
        {
            List<ItadStore> RegionStores = new List<ItadStore>();
            try
            {
                string url = baseAddress + $"v02/web/stores/?region={region}&country={country}";
                string responseData = string.Empty;
                try
                {
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                }

                JObject datasObj = JObject.Parse(responseData);
                if (((JArray)datasObj["data"]).Count > 0)
                {
                    RegionStores = JsonConvert.DeserializeObject<List<ItadStore>>((JsonConvert.SerializeObject((JArray)datasObj["data"])));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", "Error to parse downloaded data in GetRegionStores()");
            }

            return RegionStores;
        }

        public PlainData GetPlain(string title, bool isSecond = false)
        {
            PlainData plainData = new PlainData();
            try
            {
                string url = baseAddress + $"v02/game/plain/?key={key}&title={WebUtility.UrlEncode(WebUtility.HtmlDecode(title))}";
#if DEBUG
                logger.Debug($"IsThereAnyDeal - GetPlain({title}) - url: {url}");
#endif
                string responseData = string.Empty;
                try
                {
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                }

                ItadPlain itadPlain = JsonConvert.DeserializeObject<ItadPlain>(responseData);
                if (itadPlain.Meta.Match != "false")
                {
                    plainData.Plain = itadPlain.Data.Plain;
                    plainData.IsActive = itadPlain.Meta.Active;
                }
                else
                {
                    logger.Warn($"IsThereAnyDeal - not find for {WebUtility.HtmlDecode(title)}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"Error in GetPlain({WebUtility.HtmlDecode(title)})");
            }

            if (!isSecond && plainData.Plain.IsNullOrEmpty())
            {
                plainData = GetPlain(title.Split('-')[0].Trim(), true);
            }

            return plainData;
        }


        // TODO Use for add data
        public List<ItadGameInfo> SearchGame(string q, string region, string country)
        {
            List<ItadGameInfo> itadGameInfos = new List<ItadGameInfo>();
            try
            {
                string url = baseAddress + $"v01/search/search/?key={key}&q={q}&region{region}&country={country}";
#if DEBUG
                logger.Debug($"IsThereAnyDeal - SearchGame({q}) - {url}");
#endif
                string responseData = string.Empty;
                try
                {
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                }

                JObject datasObj = JObject.Parse(responseData);
                if (((JArray)datasObj["data"]["list"]).Count > 0)
                {
                    foreach (JObject dataObj in ((JArray)datasObj["data"]["list"]))
                    {
                        itadGameInfos.Add(new ItadGameInfo
                        {
                            Plain = (string)dataObj["plain"],
                            //title = (string)dataObj["title"],
                            PriceNew = (double)dataObj["price_new"],
                            PriceOld = (double)dataObj["price_old"],
                            PriceCut = (double)dataObj["price_cut"],
                            //added = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)dataObj["added"]),
                            ShopName = (string)dataObj["shop"]["name"],
                            //shop_color = GetShopColor((string)dataObj["shop"]["name"], settings.Stores),
                            UrlBuy = (string)dataObj["urls"]["buy"]
                            //url_game = (string)dataObj["urls"]["game"],
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", "Error in SearchGame()");
            }

            return itadGameInfos;
        }

        public List<Wishlist> GetCurrentPrice(List<Wishlist> wishlists, IsThereAnyDealSettings settings, IPlayniteAPI PlayniteApi)
        {
            List<Wishlist> Result = new List<Wishlist>();

            try
            {
                // IS allready load?
                if (wishlists.Count > 0)
                {
                    foreach (Wishlist wishlist in wishlists)
                    {
                        if (wishlist.itadGameInfos != null && wishlist.itadGameInfos.Keys.Contains(DateTime.Now.ToString("yyyy-MM-dd")))
                        {
#if DEBUG
                            logger.Debug("IsThereAnyDeal - Current price is allready load");
#endif
                            return wishlists;
                        }
                    }
                }


                string plains = string.Empty;
                foreach (Wishlist wishlist in wishlists)
                {
                    if (plains == string.Empty)
                    {
                        plains += wishlist.Plain;
                    }
                    else
                    {
                        plains += "," + wishlist.Plain;
                    }
                }
#if DEBUG
                logger.Debug($"IsThereAnyDeal - GetCurrentPrice({plains})");
#endif

                string shops = string.Empty;
                foreach (ItadStore Store in settings.Stores)
                {
                    if (Store.IsCheck)
                    {
                        if (shops == string.Empty)
                        {
                            shops += Store.Id;
                        }
                        else
                        {
                            shops += "," + Store.Id;
                        }
                    }
                }

                if (!plains.IsNullOrEmpty())
                {
                    try
                    {
                        string url = baseAddress + $"v01/game/prices/?key={key}&plains={plains}&region{settings.Region}&country={settings.Country}&shops={shops}";
                        string responseData = string.Empty;
                        try
                        {
                            responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                        }

                        foreach (Wishlist wishlist in wishlists)
                        {
                            ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos = new ConcurrentDictionary<string, List<ItadGameInfo>>();
                            List<ItadGameInfo> dataCurrentPrice = new List<ItadGameInfo>();
                            JObject datasObj = JObject.Parse(responseData);

                            // Check if in library (exclude game emulated)
                            List<Guid> ListEmulators = new List<Guid>();
                            foreach (var item in PlayniteApi.Database.Emulators)
                            {
                                ListEmulators.Add(item.Id);
                            }

                            bool InLibrary = false;
                            foreach (var game in PlayniteApi.Database.Games.Where(a => a.Name.ToLower() == wishlist.Name.ToLower() && a.Hidden == false))
                            {
                                if (game.PlayAction != null && game.PlayAction.EmulatorId != null && ListEmulators.Contains(game.PlayAction.EmulatorId))
                                {
                                    InLibrary = false;
                                }
                                else
                                {
                                    InLibrary = true;
                                }
                            }
                            wishlist.InLibrary = InLibrary;

                            try
                            {
                                if (((JArray)datasObj["data"][wishlist.Plain]["list"]).Count > 0)
                                {
                                    foreach (JObject dataObj in ((JArray)datasObj["data"][wishlist.Plain]["list"]))
                                    {
                                        try
                                        {
                                            dataCurrentPrice.Add(new ItadGameInfo
                                            {
                                                Name = wishlist.Name,
                                                StoreId = wishlist.StoreId,
                                                SourceId = wishlist.SourceId,
                                                Plain = wishlist.Plain,
                                                PriceNew = Math.Round((double)dataObj["price_new"], 2),
                                                PriceOld = Math.Round((double)dataObj["price_old"], 2),
                                                PriceCut = (double)dataObj["price_cut"],
                                                CurrencySign = settings.CurrencySign,
                                                ShopName = (string)dataObj["shop"]["name"],
                                                ShopColor = GetShopColor((string)dataObj["shop"]["name"], settings.Stores),
                                                UrlBuy = (string)dataObj["url"]
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Common.LogError(ex, "IsThereAnyDeal");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Warn($"IsThereAnyDeal - No data for {wishlist.Name} - {plains}");
#if DEBUG
                                Common.LogError(ex, "IsThereAnyDeal [Ignored]", "Error on get dataCurrentPrice");
#endif
                            }

                            itadGameInfos.TryAdd(DateTime.Now.ToString("yyyy-MM-dd"), dataCurrentPrice);

                            wishlist.itadGameInfos = itadGameInfos;
                            Result.Add(wishlist);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, "IsThereAnyDeal", $"Error in GetCurrentPrice({plains})");
                    }
                }
                else
                {
#if DEBUG
                    logger.Debug("IsThereAnyDeal - No plain");
#endif
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal");
            }

            return Result;
        }

        private string GetShopColor(string ShopName, List<ItadStore> itadStores)
        {
            foreach (ItadStore store in itadStores)
            {
                if (ShopName == store.Title)
                {
                    return store.Color;
                }
            }
            return null;
        }


        public List<ItadGiveaway> GetGiveaways(IPlayniteAPI PlayniteApi, string PluginUserDataPath, bool CacheOnly = false)
        {
            // Load previous
            string PluginDirectoryCache = PluginUserDataPath + "\\cache";
            string PluginFileCache = PluginDirectoryCache + "\\giveways.json";
            List<ItadGiveaway> itadGiveawaysCache = new List<ItadGiveaway>();
            try
            {
                if (!Directory.Exists(PluginDirectoryCache))
                {
                    Directory.CreateDirectory(PluginDirectoryCache);
                }
                
                if (File.Exists(PluginFileCache))
                {
                    string fileData = File.ReadAllText(PluginFileCache);
                    itadGiveawaysCache = JsonConvert.DeserializeObject<List<ItadGiveaway>>(fileData);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", "Error in GetGiveAway() with cache data");
            }


            // Load on web
            List<ItadGiveaway> itadGiveaways = new List<ItadGiveaway>();
            if (!CacheOnly && itadGiveawaysCache != new List<ItadGiveaway>())
            {
                try
                {
                    string url = @"https://isthereanydeal.com/specials/#/filter:&giveaway,&active";
                    string responseData = string.Empty;
                    try
                    {
                        responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, "IsThereAnyDeal", $"Failed to download {url}");
                    }

                    if (responseData != string.Empty)
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(responseData);
                        foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.giveaway"))
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
                    Common.LogError(ex, "IsThereAnyDeal", "Error in GetGiveAway() with web data");
                }
            }

            // Compare new with cache
            if (itadGiveaways.Count != 0)
            {
#if DEBUG
                logger.Debug("IsThereAnyDeal - Compare with cache");
#endif
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
                logger.Warn("IsThereAnyDeal - No new data for GetGiveaways()");
                itadGiveaways = itadGiveawaysCache;
            }

            // Save new
            try
            {
                File.WriteAllText(PluginFileCache, JsonConvert.SerializeObject(itadGiveaways));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", "Error in GetGiveAway() with save data");
            }

            return itadGiveaways;
        }


        public static void CheckNotifications(IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            Task taskNotifications = Task.Run(() =>
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                if (settings.EnableNotification)
                {
                    List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(plugin, PlayniteApi, settings, plugin.GetPluginUserDataPath(), true);
                    foreach (Wishlist wishlist in ListWishlist)
                    {
                        if (wishlist.GetNotification(settings.NotificationCriterias))
                        {
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                $"IsThereAnyDeal-{wishlist.Plain}",
                                "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotification"),
                                    wishlist.Name, wishlist.ItadBestPrice.PriceNew, wishlist.ItadBestPrice.CurrencySign, wishlist.ItadBestPrice.PriceCut),
                                NotificationType.Info,
                                () =>
                                {
                                    var ViewExtension = new IsThereAnyDealView(plugin, PlayniteApi, plugin.GetPluginUserDataPath(), settings, wishlist.Plain);
                                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension);
                                    windowExtension.ShowDialog();
                                }
                            ));
                        }
                    }
                }

                if (settings.EnableNotificationGiveaways)
                {
                    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(PlayniteApi, plugin.GetPluginUserDataPath());
                    foreach (ItadGiveaway itadGiveaway in itadGiveaways)
                    {
                        if (!itadGiveaway.HasSeen)
                        {
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                $"IsThereAnyDeal-{itadGiveaway.Title}",
                                "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationGiveaway"), itadGiveaway.TitleAll, itadGiveaway.Count),
                                NotificationType.Info,
                                () => Process.Start(itadGiveaway.Link)
                            ));
                        }
                    }
                }
            });
        }

        public static void UpdateDatas(IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                resources.GetString("LOCITADDataDownloading"),
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                logger.Info($"IsThereAnyDeal - Task UpdateDatas()");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                try
                {
                    isThereAnyDealApi.LoadWishlist(plugin, PlayniteApi, settings, plugin.GetPluginUserDataPath());
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error on UpdateDatas()");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"IsThereAnyDeal - Task UpdateDatas() - {String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }
    }

    public class PlainData
    {
        public string Plain { get; set; } = string.Empty;
        public bool IsActive { get; set; } = false;
    }

    public class CountData
    {
        public string StoreName { get; set; }
        public int Count { get; set; }
    }
}
