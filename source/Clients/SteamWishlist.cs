using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using CommonPlayniteShared.PluginLibrary.SteamLibrary.SteamShared;

namespace IsThereAnyDeal.Services
{
    class SteamWishlist : GenericWishlist
    {
        public readonly string UrlWishlist = @"https://store.steampowered.com/wishlist/profiles/{0}/wishlistdata/?p={1}&v=";
        private readonly string UrlAppData = @"https://store.steampowered.com/api/appdetails?appids={0}";


        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Steam", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"IsThereAnyDeal - Load from web for Steam");

            // Get Steam configuration if exist.
            string userId = string.Empty;
            string apiKey = string.Empty;
            try
            {
                dynamic SteamConfig = Serialization.FromJsonFile<dynamic>(PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json");
                userId = (string)SteamConfig["UserId"];
                //apiKey = (string)SteamConfig["ApiKey"];
            }
            catch
            {
            }

            if (userId.IsNullOrEmpty())
            {
                logger.Error($"ISThereAnyDeal - No Steam configuration.");
                
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Steam-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationsSteamBadConfig"), "Steam"),
                    NotificationType.Error
                ));

                ResultLoad = LoadWishlists("Steam", PluginUserDataPath, true);
                if (ResultLoad != null && CacheOnly)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                    SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }


            string ResultWeb = string.Empty;
            string url = string.Empty;

            for (int iPage = 0; iPage < 10; iPage++)
            {
                url = string.Format(UrlWishlist, userId, iPage);

                try
                {
                    ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                    {
                        Common.LogError(ex, false, $"Error download Steam wishlist for page {iPage}");
                        return Result;
                    }
                }

                
                if (ResultWeb.ToLower().Contains("{\"success\":2}"))
                {
                    logger.Warn($"IsThereAnyDeal - Private wishlist for {userId}?");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Steam-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationErrorSteamPrivate"), "Steam"),
                        NotificationType.Error
                    ));
                }


                if (!ResultWeb.IsNullOrEmpty())
                {
                    dynamic resultObj = null;

                    if (ResultWeb == "[]")
                    {
                        logger.Info($"IsThereAnyDeal - No result after page {iPage} for Steam wishlist");
                        break;
                    }

                    try
                    {
                        resultObj = Serialization.FromJson<dynamic>(ResultWeb);

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (var gameWishlist in resultObj)
                        {
                            string StoreId = string.Empty;
                            string Name = string.Empty;
                            DateTime ReleaseDate = default(DateTime);
                            string Capsule = string.Empty;

                            try
                            {
                                dynamic gameWishlistData = (dynamic)gameWishlist.Value;

                                StoreId = gameWishlist.Name;
                                Name = WebUtility.HtmlDecode((string)gameWishlistData["name"]);

                                string release_date = ((string)gameWishlistData["release_date"])?.Split('.')[0];
                                int.TryParse(release_date, out int release_date_int);
                                ReleaseDate = (release_date_int == 0) ? default(DateTime) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(release_date_int);

                                Capsule = (string)gameWishlistData["capsule"];

                                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("steam") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Steam",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = "https://store.steampowered.com/app/" + StoreId,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Plain = plainData.Plain,
                                    IsActive = plainData.IsActive
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error in parse Steam wishlist - {Name}");
                                logger.Warn($"IsThereAnyDeal - Error in parse Steam wishlist - {Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, "Error in parse Steam wishlist");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-Error",
                            "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationError"), "Steam"),
                            NotificationType.Error
                        ));

                        ResultLoad = LoadWishlists("Steam", PluginUserDataPath, true);
                        if (ResultLoad != null && CacheOnly)
                        {
                            ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                            SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                            return ResultLoad;
                        }
                        return Result;
                    }
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Steam", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            string Url = @"https://store.steampowered.com/wishlist/profiles/76561198003215440/remove/";
            // formfata : appid=632470&sessionid=8e1207c6343129ee6b8098a2
            return false;
        }

        public bool ImportWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, string FilePath)
        {
            List<Wishlist> Result = new List<Wishlist>();

            if (File.Exists(FilePath))
            {
                try
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    
                    dynamic jObject = Serialization.FromJsonFile<dynamic>(FilePath);

                    var rgWishlist = jObject["rgWishlist"];
                    foreach(var el in rgWishlist)
                    {
                        // Respect API limitation
                        Thread.Sleep(1000);

                        string ResultWeb = string.Empty;
                        try
                        {
                            ResultWeb = Web.DownloadStringData(string.Format(UrlAppData, (string)el)).GetAwaiter().GetResult();
                        }
                        catch (WebException ex)
                        {
                            if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                            {
                                Common.LogError(ex, false, $"Error download Steam app data - {el.ToString()}");
                                return false;
                            }
                        }

                        if (!ResultWeb.IsNullOrEmpty())
                        {
                            var parsedData = Serialization.FromJson<Dictionary<string, StoreAppDetailsResult>>(ResultWeb);
                            var AppDetails = parsedData[el.ToString()].data;

                            if (AppDetails == null)
                            {
                                continue;
                            }

                            string StoreId = (string)el;
                            string Name = WebUtility.HtmlDecode(AppDetails.name);
                            DateTime ReleaseDate = (AppDetails.release_date.date == null) ? default(DateTime) : (DateTime)AppDetails.release_date.date;
                            string Capsule = AppDetails.header_image;

                            PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                            var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("steam") > -1);

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId,
                                StoreName = "Steam",
                                ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                                Name = Name,
                                SourceId = SourceId,
                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                Capsule = Capsule,
                                Plain = plainData.Plain,
                                IsActive = plainData.IsActive
                            });
                        }
                    }

                    Result = SetCurrentPrice(Result, settings, PlayniteApi);
                    SaveWishlist("Steam", PluginUserDataPath, Result);

                    return true;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                }
            }

            return false;
        }
    }
}
