using IsThereAnyDeal.Models;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace IsThereAnyDeal.Services
{
    class SteamWishlist : GenericWishlist
    {
        public string UrlWishlist = @"https://store.steampowered.com/wishlist/profiles/{0}/wishlistdata/?p={1}&v=";


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
                JObject SteamConfig = JObject.Parse(File.ReadAllText(PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json"));
                userId = (string)SteamConfig["UserId"];
                apiKey = (string)SteamConfig["ApiKey"];
            }
            catch
            {
            }

            if (userId.IsNullOrEmpty() || apiKey.IsNullOrEmpty())
            {
                logger.Error($"ISThereAnyDeal - No Steam configuration.");
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
                        Common.LogError(ex, "IsThereAnyDeal", $"Error download Steam wishlist for page {iPage}");
                        return Result;
                    }
                }

                if (!ResultWeb.IsNullOrEmpty())
                {
                    JObject resultObj = new JObject();
                    JArray resultItems = new JArray();

                    if (ResultWeb == "[]")
                    {
                        logger.Info($"IsThereAnyDeal - No result after page {iPage} for Steam wishlist");
                        break;
                    }

                    try
                    {
                        resultObj = JObject.Parse(ResultWeb);

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (var gameWishlist in resultObj)
                        {
                            string StoreId = string.Empty;
                            string Name = string.Empty;
                            DateTime ReleaseDate = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            string Capsule = string.Empty;

                            try
                            {
                                JObject gameWishlistData = (JObject)gameWishlist.Value;

                                StoreId = gameWishlist.Key;
                                Name = WebUtility.HtmlDecode((string)gameWishlistData["name"]);
                                ReleaseDate = ((int)gameWishlistData["release_date"] == 0) ? default(DateTime) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)gameWishlistData["release_date"]);
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
#if DEBUG
                                Common.LogError(ex, "IsThereAnyDeal", $"Error in parse Steam wishlist - {Name}");
#endif
                                logger.Warn($"IsThereAnyDeal - Error in parse Steam wishlist - {Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, "IsThereAnyDeal", "Error in parse Steam wishlist");

                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-Error",
                            string.Format(resources.GetString("LOCItadNotificationError"), "Steam"),
                            NotificationType.Error
                        ));

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
    }
}
