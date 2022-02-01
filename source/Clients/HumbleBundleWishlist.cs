using Playnite.SDK;
using Playnite.SDK.Data;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using CommonPluginsShared;

namespace IsThereAnyDeal.Services
{
    class HumbleBundleWishlist : GenericWishlist
    {
        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string HumbleBundleId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("HumbleBundle", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                }
                SaveWishlist("HumbleBundle", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (HumbleBundleId.IsNullOrEmpty())
            {
                return Result;
            }

            logger.Info($"Load from web for HumbleBundle");

            string ResultWeb = string.Empty;
            string url = string.Format(@"https://www.humblebundle.com/store/wishlist/{0}", HumbleBundleId);

            try
            {
                ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
                if (!ResultWeb.IsNullOrEmpty())
                {
                    int startSub = ResultWeb.IndexOf("<script id=\"storefront-webpack-json-data\" type=\"application/json\">");
                    if (startSub == -1)
                    {
                        logger.Warn($"No Humble wishlist?");
                        return Result;
                    }
                    ResultWeb = ResultWeb.Substring(startSub, (ResultWeb.Length - startSub));

                    int endSub = ResultWeb.IndexOf("</script>");
                    ResultWeb = ResultWeb.Substring(0, endSub);

                    ResultWeb = ResultWeb.Replace("<script id=\"storefront-webpack-json-data\" type=\"application/json\">", string.Empty);

                    dynamic dataObj = Serialization.FromJson<dynamic>(ResultWeb);

                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    foreach (dynamic gameWish in dataObj["products_json"])
                    {
                        string StoreId = string.Empty;
                        string StoreUrl = string.Empty;
                        string Name = string.Empty;
                        DateTime ReleaseDate = default(DateTime);
                        string Capsule = string.Empty;

                        try
                        {
                            StoreId = (string)gameWish["machine_name"];
                            StoreUrl = "https://www.humblebundle.com/store/" + gameWish["human_url"];
                            Name = WebUtility.HtmlDecode((string)gameWish["human_name"]);
                            Capsule = (string)gameWish["standard_carousel_image"];

                            PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                            var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("humble") > -1);

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId,
                                StoreName = "Humble",
                                ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                StoreUrl = StoreUrl,
                                Name = Name,
                                SourceId = SourceId,
                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                Capsule = Capsule,
                                Plain = plainData.Plain,
                                IsActive = plainData.IsActive
                            });
                        }
                        catch(Exception ex)
                        {
                            Common.LogError(ex, true, $"Error in parse Humble wishlist - {Name}");
                            logger.Warn($"Error in parse Humble wishlist - {Name}");
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    Common.LogError(ex, false, "Error in download HumbleBundle wishlist", true, "IsThereAnyDeal");

                    ResultLoad = LoadWishlists("Humble", PluginUserDataPath, true);
                    if (ResultLoad != null)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                        SaveWishlist("Humble", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
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
            //    logger.Warn($"Humble account is not logged");
            //}
            return false;
        }
    }
}
