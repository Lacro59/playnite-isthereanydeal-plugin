using Playnite.SDK;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;
using Newtonsoft.Json.Linq;
using IsThereAnyDeal.Resources.PlayniteResources.HumbleLibrary;

namespace IsThereAnyDeal.Services
{
    class HumbleBundleWishlist : GenericWishlist
    {
        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string HumbleBundleId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("HumbleBundle", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("HumbleBundle", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (HumbleBundleId.IsNullOrEmpty())
            {
                return Result;
            }

            logger.Info($"IsThereAnyDeal - Load from web for HumbleBundle");

            string ResultWeb = string.Empty;
            string url = string.Format(@"https://www.humblebundle.com/store/wishlist/{0}", HumbleBundleId);

            try
            {
                ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
                if (!ResultWeb.IsNullOrEmpty())
                {
                    int startSub = ResultWeb.IndexOf("<script id=\"storefront-webpack-json-data\" type=\"application/json\">");
                    ResultWeb = ResultWeb.Substring(startSub, (ResultWeb.Length - startSub));

                    int endSub = ResultWeb.IndexOf("</script>");
                    ResultWeb = ResultWeb.Substring(0, endSub);

                    ResultWeb = ResultWeb.Replace("<script id=\"storefront-webpack-json-data\" type=\"application/json\">", string.Empty);

                    JObject dataObj = JObject.Parse(ResultWeb);

                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    foreach (JObject gameWish in dataObj["products_json"])
                    {
                        string StoreId = (string)gameWish["machine_name"];
                        string StoreUrl = "https://www.humblebundle.com/store/" + gameWish["human_url"];
                        string Name = (string)gameWish["human_name"];
                        DateTime ReleaseDate = default(DateTime);
                        string Capsule = (string)gameWish["standard_carousel_image"];

                        PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                        Result.Add(new Wishlist
                        {
                            StoreId = StoreId,
                            StoreName = "Humble",
                            ShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("humble") > -1).Color,
                            StoreUrl = StoreUrl,
                            Name = WebUtility.HtmlDecode(Name),
                            SourceId = SourceId,
                            ReleaseDate = ReleaseDate.ToUniversalTime(),
                            Capsule = Capsule,
                            Plain = plainData.Plain,
                            IsActive = plainData.IsActive
                        });
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error in download HumbleBundle wishlist");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Humble-Error",
                        string.Format(resources.GetString("LOCItadNotificationError"), "Humble Bundle"),
                        NotificationType.Error
                    ));

                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("HumbleBundle", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(IPlayniteAPI PlayniteApi, string StoreId)
        {
            var view = PlayniteApi.WebViews.CreateOffscreenView();
            HumbleAccountClientExtand humbleAccountClient = new HumbleAccountClientExtand(view);
            //if (humbleAccountClient.GetIsUserLoggedIn())
            //{
            //    return humbleAccountClient.RemoveWishList(StoreId);
            //}
            //else
            //{
            //    logger.Warn($"IsThereAnyDeal - Humble account is not logged");
            //}
            return false;
        }
    }
}
