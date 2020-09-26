using IsThereAnyDeal.Models;
using Newtonsoft.Json.Linq;
using Playnite.Common.Web;
using Playnite.SDK;
using PluginCommon;
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

            // Get Steam configuration if exist.
            string userId = "";
            string apiKey = "";
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


            string ResultWeb = "";
            string url = string.Format(@"https://store.steampowered.com/wishlist/profiles/{0}/wishlistdata/", userId);

            ResultWeb = "";
            try
            {
                ResultWeb = HttpDownloader.DownloadString(url, Encoding.UTF8);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error in download Steam wishlist");
                    return Result;
                }
            }

            if (ResultWeb != "")
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

                        int StoreId = int.Parse(gameWishlist.Key);
                        string Name = (string)gameWishlistData["name"];
                        DateTime ReleaseDate = ((int)gameWishlistData["release_date"] == 0) ? default(DateTime) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((int)gameWishlistData["release_date"]);
                        string Capsule = (string)gameWishlistData["capsule"];

                        Result.Add(new Wishlist
                        {
                            StoreId = StoreId,
                            StoreName = "Steam",
                            StoreUrl = "https://store.steampowered.com/app/" + StoreId,
                            Name = WebUtility.HtmlDecode(Name),
                            SourceId = SourceId,
                            ReleaseDate = ReleaseDate.ToUniversalTime(),
                            Capsule = Capsule,
                            Plain = isThereAnyDealApi.GetPlain(Name)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error io parse Steam wishlist");
                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Steam", PluginUserDataPath, Result);
            return Result;
        }
    }
}
