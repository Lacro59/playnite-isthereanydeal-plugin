using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using AngleSharp.Parser.Html;
using AngleSharp.Dom.Html;

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
                Common.LogError(ex, false, $"GOG is not defined");
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

            bool HasError = false;

            if (gogAPI != null && gogAPI.GetIsUserLoggedIn())
            {
                string ResultWeb = string.Empty;

                try
                {
                    logger.Info($"IsThereAnyDeal - Get GOG Wishlist with api");

                    // Get wishlist
                    ResultWeb = gogAPI.GetWishList();

                    // Get game information for wishlist
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        // Not connected
                        if (ResultWeb.Contains("id=\"login_username\""))
                        {
                            logger.Warn($"IsThereAnyDeal - GOG is disconnected");
                        }
                        else
                        {
                            try
                            {
                                dynamic resultObj = Serialization.FromJson<dynamic>(ResultWeb);

                                if (((dynamic)resultObj["wishlist"]).Count > 0)
                                {
                                    foreach (var gameWishlist in (dynamic)resultObj["wishlist"])
                                    {
                                        if (((bool)gameWishlist.Value))
                                        {
                                            string StoreId = gameWishlist.Key;

                                            Wishlist wishlist = GetGameData(SourceId, StoreId, settings);
                                            if (wishlist != null)
                                            {
                                                Result.Add(wishlist);
                                            }
                                            else
                                            {
                                                logger.Warn($"IsThereAnyDeal - GOG wishlist is incomplet");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, $"Error in parse GOG wishlist");
                                HasError = true;
                            }
                        }
                    }


                    if (HasError)
                    {
                        logger.Info($"IsThereAnyDeal - Get GOG Wishlist without api");

                        // Get wishlist
                        ResultWeb = gogAPI.GetWishListWithoutAPI();

                        // Get game information for wishlist
                        if (!ResultWeb.IsNullOrEmpty())
                        {
                            try
                            {
                                HtmlParser parser = new HtmlParser();
                                IHtmlDocument HtmlRequirement = parser.Parse(ResultWeb);

                                foreach(var el in HtmlRequirement.QuerySelectorAll(".product-row-wrapper .product-state-holder"))
                                {
                                    string StoreId = el.GetAttribute("gog-product");

                                    Wishlist wishlist = GetGameData(SourceId, StoreId, settings);
                                    if (wishlist != null)
                                    {
                                        Result.Add(wishlist);
                                    }
                                    else
                                    {
                                        logger.Warn($"IsThereAnyDeal - GOG wishlist is incomplet - StoreId: {StoreId}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, $"Error in parse GOG wishlist");

                                if (ResultLoad != null)
                                {
                                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                                    SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                                    return ResultLoad;
                                }
                                return Result;
                            }
                        }
                        else
                        {

                        }
                    }
                }
                catch (WebException ex)
                {
                    Common.LogError(ex, false, "Error in download GOG wishlist");

                    if (ResultLoad != null)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                        SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
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


        private Wishlist GetGameData(Guid SourceId, string StoreId, IsThereAnyDealSettings settings)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

            string Name = string.Empty;
            DateTime ReleaseDate = default(DateTime);
            string Capsule = string.Empty;

            //Download game information
            string url = string.Format(@"https://api.gog.com/products/{0}", StoreId);
            string ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            try
            {
                dynamic resultObjGame = Serialization.FromJson<dynamic>(ResultWeb);
                ReleaseDate = (resultObjGame["release_date"].ToString().IsNullOrEmpty()) ? default(DateTime) : (DateTime)resultObjGame["release_date"];
                Name = (string)resultObjGame["title"];
                Capsule = "http:" + (string)resultObjGame["images"]["logo2x"];

                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("gog") > -1);

                return new Wishlist
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
                };
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Failed to download game information for {StoreId}");
                return null;
            }
        }
    }
}
