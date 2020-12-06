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
using System.Net;
using System.Text;

namespace IsThereAnyDeal.Services
{
    public class GogWishlist : GenericWishlist
    {
        private GogAccountClientExtand gogAPI;


        public GogWishlist(IPlayniteAPI PlayniteApi)
        {
            try
            {
                var view = PlayniteApi.WebViews.CreateOffscreenView();
                gogAPI = new GogAccountClientExtand(view);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"GOG is not defined");
            }
        }

        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Gog", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"IsThereAnyDeal - Load from web for GOG");

            if (gogAPI != null && gogAPI.GetIsUserLoggedIn())
            {
                string ResultWeb = string.Empty;

                try
                {
                    // Get wishlist
                    ResultWeb = gogAPI.GetWishList();

                    // Get game information for wishlist
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        string StoreId = string.Empty;
                        string Name = string.Empty;
                        DateTime ReleaseDate = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        string Capsule = string.Empty;

                        try
                        { 
                            JObject resultObj = JObject.Parse(ResultWeb);
            
                            if (((JObject)resultObj["wishlist"]).Count > 0)
                            {
                                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                                foreach (var gameWishlist in (JObject)resultObj["wishlist"])
                                {
                                    try
                                    {
                                        if (((bool)gameWishlist.Value))
                                        {
                                            StoreId = gameWishlist.Key;

                                            //Download game information
                                            string url = string.Format(@"https://api.gog.com/products/{0}", StoreId);
                                            ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
                                            try
                                            {
                                                JObject resultObjGame = JObject.Parse(ResultWeb);
                                                ReleaseDate = (DateTime)(resultObjGame["release_date"] ?? defult(DateTime));
                                                Name = (string)resultObjGame["title"];
                                                Capsule = "http:" + (string)resultObjGame["images"]["logo2x"];

                                                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                                                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("gog") > -1);

                                                Result.Add(new Wishlist
                                                {
                                                    StoreId = StoreId,
                                                    StoreName = "GOG",
                                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                                    StoreUrl = (string)resultObjGame["links"]["product_card"],
                                                    Name = WebUtility.HtmlDecode(Name),
                                                    SourceId = SourceId,
                                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                                    Capsule = Capsule,
                                                    Plain = plainData.Plain,
                                                    IsActive = plainData.IsActive
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                Common.LogError(ex, "IsThereAnyDeal", $"Failed to download game information for {StoreId}");
                                                return Result;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
#if DEBUG
                                        Common.LogError(ex, "IsThereAnyDeal", $"Error in parse GOG wishlist - {Name}");
#endif
                                        logger.Warn($"IsThereAnyDeal - Error in parse GOG wishlist - {Name}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, "IsThereAnyDeal", $"Error in parse GOG wishlist");

                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                $"IsThereAnyDeal-Gog-Error",
                                string.Format(resources.GetString("LOCItadNotificationError"), "GOG"),
                                NotificationType.Error
                            ));

                            return Result;
                        }
                    }
                }
                catch (WebException ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error in download GOG wishlist");
                    return Result;
                }
            }
            
            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Gog", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            return gogAPI.RemoveWishList(StoreId);
        }
    }
}
