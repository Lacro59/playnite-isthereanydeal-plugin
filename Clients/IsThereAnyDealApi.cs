using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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


        internal async Task<string> DownloadStringData(string url)
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
                SteamWishlist steamWishlist = new SteamWishlist();
                ListWishlistSteam = steamWishlist.GetWishlist(PlayniteApi, SteamId, PluginUserDataPath, settings, CacheOnly, Force);
            }

            List<Wishlist> ListWishlistGog = new List<Wishlist>();
            if (settings.EnableGog)
            {
                GogWishlist gogWishlist = new GogWishlist(PlayniteApi);
                ListWishlistGog = gogWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly, Force);
            }

            List<Wishlist> ListWishlistEpic = new List<Wishlist>();
            if (settings.EnableEpic)
            {
                EpicWishlist epicWishlist = new EpicWishlist();
                ListWishlistEpic = epicWishlist.GetWishlist(PlayniteApi, GogId, PluginUserDataPath, settings, CacheOnly, Force);
            }

            List<Wishlist> ListWishlistHumble = new List<Wishlist>();
            if (settings.EnableHumble)
            {
                HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                ListWishlistHumble = humbleBundleWishlist.GetWishlist(PlayniteApi, HumbleId, settings.HumbleKey, PluginUserDataPath, settings, CacheOnly, Force);
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

                        // Check if in library
                        bool InLibrary = false;
                        foreach (var game in PlayniteApi.Database.Games.Where(a => a.Name.ToLower() == wishlist.Name.ToLower()))
                        {
                            InLibrary = true;
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



        // TODO Have a price history
        //private bool VerifDataDifferent(Wishlist wishlist, JObject dataObj)
        //{
        //    if (wishlist.itadGameInfos.Count != 0)
        //    {
        //        ItadGameInfo dataPrice = wishlist.itadGameInfos[(wishlist.itadGameInfos.Count - 1)];
        //        if (dataPrice.price_new != (double)dataObj["price_new"] || dataPrice.price_old != (double)dataObj["price_old"] || dataPrice.price_cut != (double)dataObj["price_cut"])
        //        {
        //            logger.Info("IsTherAnyDeal - Data is different");
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        logger.Info("IsTherAnyDeal - Data is different");
        //        return true;
        //    }
        //
        //    logger.Info("IsTherAnyDeal - Data is not different");
        //    return false;
        //}
    }
}
