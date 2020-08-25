using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using PluginCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Clients
{
    class IsThereAnyDealApi
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private string baseAddress = "https://api.isthereanydeal.com/";
        private string key = "fa49308286edcaf76fea58926fd2ea2d216a17ff";


        private async Task<string> DownloadStringData(string url)
        {
            //logger.Info($"IsTherAnyDeal - Download {url}");
            using (var client = new HttpClient())
            {
                string responseData = await client.GetStringAsync(url).ConfigureAwait(false);
                return responseData;
            }
        }


        public List<Wishlist> LoadWishlist(IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, string PluginUserDataPath, bool CacheOnly = false, bool Force = false)
        {
            Guid SteamId = new Guid();
            Guid GogId = new Guid();
            Guid EpicId = new Guid();
            Guid HumbleId = new Guid();
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
            }


            List<Wishlist> ListWishlistSteam = new List<Wishlist>();
            if (settings.EnableSteam)
            {
                if (!Tools.IsDisabledPlaynitePlugins("SteamLibrary", PluginUserDataPath))
                {
                    SteamWishlist steamWishlist = new SteamWishlist();
                    ListWishlistSteam = steamWishlist.GetWishlist(PlayniteApi, SteamId, PluginUserDataPath, settings, CacheOnly, Force);
                }
                else
                {
                    logger.Warn("IsThereAnyDeal - Steam is enable then disabled");
                }
            }

            List<Wishlist> ListWishlistGog = new List<Wishlist>();
            if (settings.EnableGog)
            {
                if (!Tools.IsDisabledPlaynitePlugins("GogLibrary", PluginUserDataPath))
                {
                    GogWishlist gogWishlist = new GogWishlist(PlayniteApi);
                    ListWishlistGog = gogWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly, Force);
                }
                else
                {
                    logger.Warn("IsThereAnyDeal - GOG is enable then disabled");
                }
            }

            List<Wishlist> ListWishlistEpic = new List<Wishlist>();
            if (settings.EnableEpic)
            {
                if (!Tools.IsDisabledPlaynitePlugins("EpicLibrary", PluginUserDataPath))
                {
                    EpicWishlist epicWishlist = new EpicWishlist();
                    ListWishlistEpic = epicWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly, Force);
                }
                else
                {
                    logger.Warn("IsThereAnyDeal - Epic Game Store is enable then disabled");
                }
            }

            List<Wishlist> ListWishlistHumble = new List<Wishlist>();
            if (settings.EnableHumble)
            {
                if (!Tools.IsDisabledPlaynitePlugins("HumbleLibrary", PluginUserDataPath))
                {
                    HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                    ListWishlistHumble = humbleBundleWishlist.GetWishlist(PlayniteApi, HumbleId, settings.HumbleKey, PluginUserDataPath, settings, CacheOnly, Force);
                }
                else
                {
                    logger.Warn("IsThereAnyDeal - Humble Bundle is enable then disabled");
                }
            }

            List<Wishlist> ListWishlist = ListWishlistSteam.Concat(ListWishlistGog).Concat(ListWishlistHumble).Concat(ListWishlistEpic).ToList();


            // Group same game
            var listDuplicates = ListWishlist.GroupBy(c => c.Name.ToLower()).Where(g => g.Skip(1).Any());
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
                        keepDuplicates.Add(wish);
                        keep.Duplicates = keepDuplicates;

                        ListWishlist.Find(x => x == keep).Duplicates = keepDuplicates;
                        ListWishlist.Find(x => x == keep).hasDuplicates = true;
                        ListWishlist.Remove(wish);
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
                string responseData = DownloadStringData(baseAddress + "v01/web/regions/").GetAwaiter().GetResult();

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
                            region = dataObj.Key,
                            currencyName = (string)dataObj.Value["currency"]["name"],
                            currencyCode = (string)dataObj.Value["currency"]["code"],
                            currencySign = (string)dataObj.Value["currency"]["sign"],
                            countries = countries
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
                string responseData = DownloadStringData(url).GetAwaiter().GetResult();

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

        public string GetPlain(string title)
        {
            string Plain = "";
            try
            {
                string url = baseAddress + $"v02/game/plain/?key={key}&title={WebUtility.UrlEncode(WebUtility.HtmlDecode(title))}";
                string responseData = DownloadStringData(url).GetAwaiter().GetResult();

                JObject datasObj = JObject.Parse(responseData);
                if ((string)datasObj[".meta"]["match"] != "false")
                {
                    Plain = (string)datasObj["data"]["plain"];
                }
                else
                {
                    logger.Info($"IsThereAnyDeal - not find for {WebUtility.HtmlDecode(title)}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"Error in GetPlain({WebUtility.HtmlDecode(title)})");
            }

            return Plain;
        }

        public List<ItadGameInfo> SearchGame(string q, string region, string country)
        {
            logger.Info($"IsThereAnyDeal - SearchGame({q})");

            List<ItadGameInfo> itadGameInfos = new List<ItadGameInfo>();
            try
            {
                string url = baseAddress + $"v01/search/search/?key={key}&q={q}&region{region}&country={country}";
                string responseData = DownloadStringData(url).GetAwaiter().GetResult();

                JObject datasObj = JObject.Parse(responseData);
                if (((JArray)datasObj["data"]["list"]).Count > 0)
                {
                    foreach (JObject dataObj in ((JArray)datasObj["data"]["list"]))
                    {
                        itadGameInfos.Add(new ItadGameInfo
                        {
                            plain = (string)dataObj["plain"],
                            //title = (string)dataObj["title"],
                            price_new = (double)dataObj["price_new"],
                            price_old = (double)dataObj["price_old"],
                            price_cut = (double)dataObj["price_cut"],
                            //added = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)dataObj["added"]),
                            shop_name = (string)dataObj["shop"]["name"],
                            //shop_color = GetShopColor((string)dataObj["shop"]["name"], settings.Stores),
                            url_buy = (string)dataObj["urls"]["buy"]
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
            // IS allready load?
            if (wishlists.Count > 0)
            {
                foreach(Wishlist wishlist in wishlists)
                {
                    if (wishlist.itadGameInfos != null && wishlist.itadGameInfos.Keys.Contains(DateTime.Now.ToString("yyyy-MM-dd")))
                    {
                        logger.Info("IsThereAnyDeal - Current price is allready load");
                        return wishlists;
                    }
                }
            }


            List<Wishlist> Result = new List<Wishlist>();

            string plains = "";
            foreach (Wishlist wishlist in wishlists)
            {
                if (plains == "")
                {
                    plains += wishlist.Plain;
                }
                else
                {
                    plains += "," + wishlist.Plain;
                }
            }
            logger.Info($"IsThereAnyDeal - GetCurrentPrice({plains})");

            string shops = "";
            foreach (ItadStore Store in settings.Stores)
            {
                if (Store.IsCheck)
                {
                    if (shops == "")
                    {
                        shops += Store.id;
                    }
                    else
                    {
                        shops += "," + Store.id;
                    }
                }
            }

            if (!plains.IsNullOrEmpty())
            {
                try
                {
                    string url = baseAddress + $"v01/game/prices/?key={key}&plains={plains}&region{settings.Region}&country={settings.Country}&shops={shops}";
                    string responseData = DownloadStringData(url).GetAwaiter().GetResult();

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
                        foreach (var game in PlayniteApi.Database.Games.Where(a => a.Name.ToLower() == wishlist.Name.ToLower()))
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


                        if (((JArray)datasObj["data"][wishlist.Plain]["list"]).Count > 0)
                        {
                            foreach (JObject dataObj in ((JArray)datasObj["data"][wishlist.Plain]["list"]))
                            {
                                //logger.Debug(JsonConvert.SerializeObject(dataObj));
                                dataCurrentPrice.Add(new ItadGameInfo
                                {
                                    plain = wishlist.Plain,
                                    //title = (string)dataObj["title"],
                                    price_new = Math.Round((double)dataObj["price_new"], 2),
                                    price_old = Math.Round((double)dataObj["price_old"], 2),
                                    price_cut = (double)dataObj["price_cut"],
                                    currency_sign = settings.CurrencySign,
                                    //added = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)dataObj["added"]),
                                    //added = DateTime.Now,
                                    shop_name = (string)dataObj["shop"]["name"],
                                    shop_color = GetShopColor((string)dataObj["shop"]["name"], settings.Stores),
                                    url_buy = (string)dataObj["url"]
                                    //url_game = (string)dataObj["urls"]["game"],
                                });
                            }
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
                logger.Info("IsThereAnyDeal - No plain");
            }

            return Result;
        }

        private string GetShopColor(string ShopName, List<ItadStore> itadStores)
        {
            foreach (ItadStore store in itadStores)
            {
                if (ShopName == store.title)
                {
                    return store.color;
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
                string url = @"https://isthereanydeal.com/specials/#/filter:&giveaway,&active";
                try
                {
                    string responseData = DownloadStringData(url).GetAwaiter().GetResult();

                    if (responseData != "")
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
                            bundleShop = bundleShop.Replace("FREE Games on", "").Replace("Always FREE For", "")
                                .Replace("FREE For", "").Replace("FREE on", "");

                            string bundleTitle = "";
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
                logger.Info("IsThereAnyDeal - Compare with cache");
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
                logger.Info("IsThereAnyDeal - No data for GetGiveaways()");
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
    }
}
