using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using CommonPluginsShared.Extensions;
using Playnite.SDK.Models;

namespace IsThereAnyDeal.Services
{
    class IsThereAnyDealApi
    {
        private static ILogger logger => LogManager.GetLogger();
        private static IResourceProvider resources => new ResourceProvider();

        private string baseAddress => "https://api.isthereanydeal.com/";
        private string key => "fa49308286edcaf76fea58926fd2ea2d216a17ff";

        public List<CountData> countDatas { get; set; } = new List<CountData>();


        public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, IsThereAnyDealSettings settings, string PluginUserDataPath, bool CacheOnly = false, bool ForcePrice = false)
        {
            Guid SteamId = new Guid();
            Guid GogId = new Guid();
            Guid EpicId = new Guid();
            Guid HumbleId = new Guid();
            Guid XboxId = new Guid();
            Guid OriginId = new Guid();
            Guid UbisoftId = new Guid();

            foreach (GameSource Source in API.Instance.Database.Sources)
            {
                if (Source.Name.IsEqual("Steam"))
                {
                    SteamId = Source.Id;
                }

                if (Source.Name.IsEqual("GOG"))
                {
                    GogId = Source.Id;
                }

                if (Source.Name.IsEqual("Epic"))
                {
                    EpicId = Source.Id;
                }

                if (Source.Name.IsEqual("Humble"))
                {
                    HumbleId = Source.Id;
                }

                if (Source.Name.IsEqual("Xbox"))
                {
                    XboxId = Source.Id;
                }

                if (Source.Name.IsEqual("Origin") || Source.Name.IsEqual("EA app"))
                {
                    OriginId = Source.Id;
                }

                if (Source.Name.IsEqual("Ubisoft Connect") || Source.Name.IsEqual("Ubisoft") || Source.Name.IsEqual("UPlay"))
                {
                    UbisoftId = Source.Id;
                }
            }


            List<Wishlist> ListWishlistSteam = new List<Wishlist>();
            if (settings.EnableSteam)
            {
                try
                {
                    if (!PlayniteTools.IsDisabledPlaynitePlugins("SteamLibrary"))
                    {
                        SteamWishlist steamWishlist = new SteamWishlist();
                        ListWishlistSteam = steamWishlist.GetWishlist(SteamId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistSteam == null)
                        {
                            ListWishlistSteam = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Steam",
                            Count = ListWishlistSteam.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Steam is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorSteam"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch(Exception ex)
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
                        GogWishlist gogWishlist = new GogWishlist();
                        ListWishlistGog = gogWishlist.GetWishlist(GogId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistGog == null)
                        {
                            ListWishlistGog = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "GOG",
                            Count = ListWishlistGog.Count
                        });
                    }
                    else
                    {
                        logger.Warn("GOG is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-GOG-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorGog"),
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
                        EpicWishlist epicWishlist = new EpicWishlist();
                        ListWishlistEpic = epicWishlist.GetWishlist(GogId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistEpic == null)
                        {
                            ListWishlistEpic = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Epic Game Store",
                            Count = ListWishlistEpic.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Epic Game Store is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-EpicGameStore-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorEpic"),
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
                        HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                        ListWishlistHumble = humbleBundleWishlist.GetWishlist(HumbleId, settings.HumbleKey, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistHumble == null)
                        {
                            ListWishlistHumble = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Humble Bundle",
                            Count = ListWishlistHumble.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Humble Bundle is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-HumbleBundle-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorHumble"),
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
                        XboxWishlist xboxWishlist = new XboxWishlist();
                        ListWishlistXbox = xboxWishlist.GetWishlist(API.Instance, XboxId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistXbox == null)
                        {
                            ListWishlistXbox = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Xbox",
                            Count = ListWishlistXbox.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Xbox is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Xbox-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorXbox"),
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
                        UbisoftWishlist ubisoftWishlist = new UbisoftWishlist();
                        ListWishlistUbisoft = ubisoftWishlist.GetWishlist(UbisoftId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlistUbisoft == null)
                        {
                            ListWishlistUbisoft = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Ubisoft Connect",
                            Count = ListWishlistUbisoft.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Ubisoft is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Ubisoft-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorUbisoft"),
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
                        OriginWishlist originWishlist = new OriginWishlist();
                        ListWishlisOrigin = originWishlist.GetWishlist(OriginId, PluginUserDataPath, settings, CacheOnly, ForcePrice);
                        if (ListWishlisOrigin == null)
                        {
                            ListWishlisOrigin = new List<Wishlist>();
                        }
                        countDatas.Add(new CountData
                        {
                            StoreName = "Origin",
                            Count = ListWishlisOrigin.Count
                        });
                    }
                    else
                    {
                        logger.Warn("Origin is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Origin-disabled",
                            "IsThereAnyDeal\r\n" + resources.GetString("LOCItadNotificationErrorOrigin"),
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
            var listDuplicates = ListWishlist.GroupBy(c => PlayniteTools.NormalizeGameName(c.Name).ToLower()).Where(g => g.Skip(1).Any());
            foreach (IGrouping<string, Wishlist> duplicates in listDuplicates)
            {
                bool isFirst = true;
                Wishlist keep = new Wishlist();
                foreach(Wishlist wish in duplicates)
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


            if (!CacheOnly || ForcePrice)
            {
                settings.LastRefresh = DateTime.Now.ToUniversalTime();
                plugin.SavePluginSettings(settings);
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
                    Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                }

                ItadRegionsResult datasObj = Serialization.FromJson<ItadRegionsResult>(responseData);
                if (datasObj != null)
                {
                    foreach (var dataObj in datasObj.data.GetType().GetProperties())
                    {
                        var Key = dataObj.Name;
                        var Value = dataObj.GetValue(datasObj.data, null);

                        if (Value is Models.Region region)
                        {
                            List<string> countries = new List<string>();
                            foreach (string country in region.countries)
                            {
                                countries.Add(country);
                            }

                            itadRegions.Add(new ItadRegion
                            {
                                Region = Key,
                                CurrencyName = region.currency.name,
                                CurrencyCode = region.currency.code,
                                CurrencySign = region.currency.sign,
                                Countries = countries
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
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
                    Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                }

                dynamic datasObj = Serialization.FromJson<dynamic>(responseData);
                if (((dynamic)datasObj["data"]).Count > 0)
                {
                    RegionStores = Serialization.FromJson<List<ItadStore>>(Serialization.ToJson((dynamic)datasObj["data"]));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }

            return RegionStores;
        }

        public PlainData GetPlain(string title, bool isSecond = false)
        {
            PlainData plainData = new PlainData();
            try
            {
                string url = baseAddress + $"v02/game/plain/?key={key}&title={WebUtility.UrlEncode(WebUtility.HtmlDecode(title))}";
                Common.LogDebug(true, $"GetPlain({title}) - url: {url}");

                string responseData = string.Empty;
                try
                {
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                }

                ItadPlain itadPlain = Serialization.FromJson<ItadPlain>(responseData);
                if (itadPlain.Meta.Match != "false")
                {
                    plainData.Plain = itadPlain.data?["plain"];
                    plainData.IsActive = itadPlain.Meta.Active;
                }
                else
                {
                    logger.Warn($"Not find for {WebUtility.HtmlDecode(title)}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetPlain({WebUtility.HtmlDecode(title)})", true, "IsThereAnyDeal");
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
                Common.LogDebug(true, $"SearchGame({q}) - {url}");

                string responseData = string.Empty;
                try
                {
                    responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                }

                dynamic datasObj = Serialization.FromJson<dynamic>(responseData);


                if (((dynamic)datasObj["data"]["list"]).Count > 0)
                {
                    foreach (dynamic dataObj in (dynamic)datasObj["data"]["list"])
                    {
                        //if ((double)dataObj["price_new"] != 0 && (double)dataObj["price_old"] != 0 && (double)dataObj["price_cut"] != 0)
                        //{
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
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }

            return itadGameInfos;
        }

        public List<Wishlist> GetCurrentPrice(List<Wishlist> wishlists, IsThereAnyDealSettings settings)
        {
            try
            {
                List<string> plains = new List<string>();
                string plain = string.Empty;
                int countString = 0;
                foreach (Wishlist wishlist in wishlists)
                {
                    if (countString == 200)
                    {
                        plains.Add(plain);
                        countString = 0;
                    }

                    if (countString == 0)
                    {
                        plain = string.Empty;
                    }

                    if (!wishlist.itadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true)
                    {
                        if (plain == string.Empty)
                        {
                            plain += wishlist.Plain;
                        }
                        else
                        {
                            plain += "," + wishlist.Plain;
                        }
                        countString++;
                    }
                }
                plains.Add(plain);


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

                if (plains.Count != 0)
                {
                    // Check if in library (exclude game emulated)
                    List<Guid> ListEmulators = new List<Guid>();
                    foreach (Emulator item in API.Instance.Database.Emulators)
                    {
                        ListEmulators.Add(item.Id);
                    }

                    foreach (string plainData in plains)
                    {
                        try
                        {
                            Thread.Sleep(1500);
                            string url = baseAddress + $"v01/game/prices/?key={key}&plains={plainData}&region{settings.Region}&country={settings.Country}&shops={shops}";
                            string responseData = string.Empty;
                            try
                            {
                                responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
                            }


                            dynamic datasObj = Serialization.FromJson<dynamic>(responseData);
                           

                            foreach (Wishlist wishlist in wishlists)
                            {
                                if (!wishlist.itadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true)
                                {
                                    ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos = new ConcurrentDictionary<string, List<ItadGameInfo>>();
                                    List<ItadGameInfo> dataCurrentPrice = new List<ItadGameInfo>();

                                    try
                                    {
                                        string ListPrice = Serialization.ToJson((dynamic)datasObj["data"]?[wishlist.Plain]?["list"]);
                                        List<ItadPrice> itadPrices = Serialization.FromJson<List<ItadPrice>>(ListPrice);

                                        if (itadPrices?.Count > 0)
                                        {
                                            foreach (ItadPrice dataObj in itadPrices)
                                            {
                                                try
                                                {
                                                    if (dataObj.price_new != 0 && itadPrices.Where(x => x.price_new > 0).Count() > 1)
                                                    { 
                                                        dataCurrentPrice.Add(new ItadGameInfo
                                                        {
                                                            Name = wishlist.Name,
                                                            StoreId = wishlist.StoreId,
                                                            SourceId = wishlist.SourceId,
                                                            Plain = wishlist.Plain,
                                                            PriceNew = Math.Round(dataObj.price_new, 2),
                                                            PriceOld = Math.Round(dataObj.price_old, 2),
                                                            PriceCut = dataObj.price_cut,
                                                            CurrencySign = settings.CurrencySign,
                                                            ShopName = dataObj.shop.name,
                                                            ShopColor = GetShopColor(dataObj.shop.name, settings.Stores),
                                                            UrlBuy = dataObj.url
                                                        });
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                                                }
                                            }

                                            itadGameInfos.TryAdd(DateTime.Now.ToString("yyyy-MM-dd"), dataCurrentPrice);
                                            wishlist.itadGameInfos = itadGameInfos;
                                        }
                                        else
                                        {
                                            Common.LogDebug(true, $"No data for {wishlist.Name} - {plainData}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Common.LogError(ex, true);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, $"Error in GetCurrentPrice({plainData})", true, "IsThereAnyDeal");
                        }
                    }
                }
                else
                {
                    Common.LogDebug(true, $"No plain");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return wishlists;
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


        public List<ItadGiveaway> GetGiveaways(string PluginUserDataPath, bool CacheOnly = false)
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
                    itadGiveawaysCache = Serialization.FromJsonFile<List<ItadGiveaway>>(PluginFileCache);
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
                    string url = @"https://isthereanydeal.com/specials/#/filter:&giveaway,&active";
                    string responseData = string.Empty;
                    try
                    {
                        responseData = Web.DownloadStringData(url).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Failed to download {url}", true, "IsThereAnyDeal");
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
                logger.Warn("No new data for GetGiveaways()");
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


        public static void CheckNotifications(IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            Task taskNotifications = Task.Run(() =>
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                if (settings.EnableNotification)
                {
                    List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(plugin, settings, plugin.GetPluginUserDataPath(), true, true);
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
                                    if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                                    {
                                        var windowOptions = new WindowOptions
                                        {
                                            ShowMinimizeButton = false,
                                            ShowMaximizeButton = false,
                                            ShowCloseButton = true,
                                            Width = 1280,
                                            Height = 740
                                        };

                                        var ViewExtension = new IsThereAnyDealView(plugin, plugin.GetPluginUserDataPath(), settings, wishlist.Plain);
                                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension, windowOptions);
                                        windowExtension.ShowDialog();
                                    }
                                    else
                                    {
                                        Process.Start(wishlist.UrlGame);
                                    }
                                }
                            ));
                        }
                    }
                }

                if (settings.EnableNotificationGiveaways)
                {
                    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(plugin.GetPluginUserDataPath());
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
                logger.Info($"Task UpdateDatas()");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                try
                {
                    isThereAnyDealApi.LoadWishlist(plugin, settings, plugin.GetPluginUserDataPath());
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"Task UpdateDatas() - {String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }
    }
}
