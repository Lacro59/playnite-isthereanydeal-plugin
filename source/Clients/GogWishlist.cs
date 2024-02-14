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
using CommonPlayniteShared.PluginLibrary.GogLibrary.Models;
using IsThereAnyDeal.Models.Api;

namespace IsThereAnyDeal.Services
{
    public class GogWishlist : GenericWishlist
    {
        protected static GogAccountClientExtand _GogAPI;
        internal static GogAccountClientExtand GogAPI
        {
            get
            {
                if (_GogAPI == null)
                {
                    _GogAPI = new GogAccountClientExtand(WebViewOffscreen);
                }
                return _GogAPI;
            }

            set => _GogAPI = value;
        }


        public GogWishlist()
        {

        }

        public List<Wishlist> GetWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Gog", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                }
                SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for GOG");

            bool HasError = false;

            if (GogAPI.GetIsUserLoggedIn())
            {
                string ResultWeb = string.Empty;

                try
                {
                    logger.Info($"Get GOG Wishlist with api");

                    // Get wishlist
                    ResultWeb = GogAPI.GetWishList();

                    // Get game information for wishlist
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        // Not connected
                        if (ResultWeb.Contains("id=\"login_username\""))
                        {
                            logger.Warn($"GOG is disconnected");
                        }
                        else
                        {
                            try
                            {
                                GogWishlistResult resultObj = Serialization.FromJson<GogWishlistResult>(ResultWeb);
                                foreach (var gameWishlist in resultObj.wishlist)
                                {
                                    if (((bool)gameWishlist.Value))
                                    {
                                        string StoreId = gameWishlist.Name;

                                        Wishlist wishlist = GetGameData(SourceId, StoreId, settings);
                                        if (wishlist != null)
                                        {
                                            Result.Add(wishlist);
                                        }
                                        else
                                        {
                                            logger.Warn($"GOG wishlist is incomplet");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, $"Error in parse GOG wishlist", true, "IsThereAnyDeal");
                                HasError = true;
                            }
                        }
                    }


                    if (HasError)
                    {
                        logger.Info($"Get GOG Wishlist without api");

                        // Get wishlist
                        ResultWeb = GogAPI.GetWishListWithoutAPI();

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
                                        logger.Warn($"GOG wishlist is incomplet - StoreId: {StoreId}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, $"Error in parse GOG wishlist", true, "IsThereAnyDeal");
                                if (ResultLoad != null)
                                {
                                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
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
                    Common.LogError(ex, false, "Error in download GOG wishlist", true, "IsThereAnyDeal");
                    if (ResultLoad != null)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings);
                        SaveWishlist("Gog", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
                    return Result;
                }
            }
            else
            {
                logger.Warn($"GOG user is not authenticated");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"isthereanydeal-gog-noauthenticate",
                    $"IsThereAnyDeal\r\nGOG - {resourceProvider.GetString("LOCLoginRequired")}",
                    NotificationType.Error
                ));
            }

            Result = SetCurrentPrice(Result, settings);
            SaveWishlist("Gog", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            return GogAPI.RemoveWishList(StoreId);
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
                ProductApiDetail resultObjGame = Serialization.FromJson<ProductApiDetail>(ResultWeb);
                ReleaseDate = resultObjGame.release_date == null ? default(DateTime) : (DateTime)resultObjGame.release_date;
                Name = resultObjGame.title;
                Capsule = "http:" + resultObjGame.images.logo2x;

                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("gog") > -1);

                return new Wishlist
                {
                    StoreId = StoreId,
                    StoreName = "GOG",
                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                    StoreUrl = resultObjGame.links.product_card,
                    Name = WebUtility.HtmlDecode(Name),
                    SourceId = SourceId,
                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                    Capsule = Capsule,
                    Game = gamesLookup.Game,
                    IsActive = true
                };
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Failed to download game information for {StoreId}", true, "IsThereAnyDeal");
                return null;
            }
        }
    }
}
