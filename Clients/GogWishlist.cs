using IsThereAnyDeal.Models;
using Newtonsoft.Json.Linq;
using Playnite.Common.Web;
using Playnite.SDK;
using PluginCommon;
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

        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool Force = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Gog", PluginUserDataPath);
            if (ResultLoad != null && !Force)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (CacheOnly)
            {
                return Result;
            }

            if (gogAPI != null && gogAPI.GetIsUserLoggedIn())
            {
                string ResultWeb = "";

                try
                {
                    // Get wishlist
                    ResultWeb = gogAPI.GetWishList();

                    // Get game information for wishlist
                    if (ResultWeb != "")
                    {
                        JObject resultObj = JObject.Parse(ResultWeb);
                        try
                        {
                            if (((JObject)resultObj["wishlist"]).Count > 0)
                            {
                                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                                foreach (var gameWishlist in (JObject)resultObj["wishlist"])
                                {
                                    if (((bool)gameWishlist.Value))
                                    {
                                        int StoreId = int.Parse(gameWishlist.Key);

                                        //Download game information
                                        string url = string.Format(@"https://api.gog.com/products/{0}", StoreId);
                                        ResultWeb = HttpDownloader.DownloadString(url, Encoding.UTF8);
                                        try
                                        {
                                            JObject resultObjGame = JObject.Parse(ResultWeb);
                                            DateTime ReleaseDate = (DateTime)resultObjGame["release_date"];       
                                            string Name = (string)resultObjGame["title"];
                                            string Capsule = "http:" + (string)resultObjGame["images"]["logo2x"];

                                            Result.Add(new Wishlist
                                            {
                                                StoreId = StoreId,
                                                StoreName = "GOG",
                                                StoreUrl = (string)resultObjGame["links"]["product_card"],
                                                Name = WebUtility.HtmlDecode(Name),
                                                SourceId = SourceId,
                                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                                Capsule = Capsule,
                                                Plain = isThereAnyDealApi.GetPlain(Name)
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Common.LogError(ex, "IsThereAnyDeal", $"Failed to download game inforamtion for {StoreId}");
                                            return Result;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, "IsThereAnyDeal", $"Error io parse GOG wishlist");
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
    }
}
