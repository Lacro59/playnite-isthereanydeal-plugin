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
        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool Force = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Steam", PluginUserDataPath);
            if (ResultLoad != null && !Force)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (CacheOnly)
            {
                return Result;
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
            string url = string.Format(@"https://store.steampowered.com/wishlist/profiles/{0}/wishlistdata/", userId);
            try
            {
                ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error in download Steam wishlist");
                    return Result;
                }
            }

            if (!ResultWeb.IsNullOrEmpty())
            {
                JObject resultObj = new JObject();
                JArray resultItems = new JArray();

                try
                {
                    resultObj = JObject.Parse(ResultWeb);

                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    foreach (var gameWishlist in resultObj)
                    {
                        JObject gameWishlistData = (JObject)gameWishlist.Value;

                        string StoreId = gameWishlist.Key;
                        string Name = (string)gameWishlistData["name"];
                        DateTime ReleaseDate = ((int)gameWishlistData["release_date"] == 0) ? default(DateTime) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)gameWishlistData["release_date"]);
                        string Capsule = (string)gameWishlistData["capsule"];

                        PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                        Result.Add(new Wishlist
                        {
                            StoreId = StoreId,
                            StoreName = "Steam",
                            ShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("steam") > -1).Color,
                            StoreUrl = "https://store.steampowered.com/app/" + StoreId,
                            Name = WebUtility.HtmlDecode(Name),
                            SourceId = SourceId,
                            ReleaseDate = ReleaseDate.ToUniversalTime(),
                            Capsule = Capsule,
                            Plain = plainData.Plain,
                            IsActive = plainData.IsActive
                        });
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error io parse Steam wishlist");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Steam-Error",
                        string.Format(resources.GetString("LOCItadNotificationError"), "Steam"),
                        NotificationType.Error
                    ));

                    return Result;
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
