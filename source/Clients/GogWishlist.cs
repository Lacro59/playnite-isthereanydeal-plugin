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


        public GogWishlist(IsThereAnyDeal plugin) : base(plugin, "GOG")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.EpicLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!GogAPI.GetIsUserLoggedIn())
            {
                Logger.Warn($"{ClientName}: Not authenticated");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-{ClientName}-NotAuthenticate",
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoresNoAuthenticate"), ClientName),
                    NotificationType.Error,
                    () => PlayniteTools.ShowPluginSettings(ExternalPlugin)
                ));

                return cachedData;
            }

            List<Wishlist> wishlists = new List<Wishlist>();
            bool HasError = false;
            string ResultWeb = string.Empty;

            Logger.Info($"Get GOG Wishlist with api");

            // Get wishlist
            ResultWeb = GogAPI.GetWishList();

            // Get game information for wishlist
            if (!ResultWeb.IsNullOrEmpty())
            {
                // Not connected
                if (ResultWeb.Contains("id=\"login_username\""))
                {
                    Logger.Warn($"GOG is disconnected");
                }
                else
                {
                    try
                    {
                        GogWishlistResult resultObj = Serialization.FromJson<GogWishlistResult>(ResultWeb);
                        foreach (dynamic gameWishlist in resultObj.wishlist)
                        {
                            if ((bool)gameWishlist.Value)
                            {
                                string StoreId = gameWishlist.Name;

                                Wishlist wishlist = GetGameData(StoreId);
                                if (wishlist != null)
                                {
                                    wishlists.Add(wishlist);
                                }
                                else
                                {
                                    Logger.Warn($"GOG wishlist is incomplet");
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
                Logger.Info($"Get GOG Wishlist without api");

                // Get wishlist
                ResultWeb = GogAPI.GetWishListWithoutAPI();

                // Get game information for wishlist
                if (!ResultWeb.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument HtmlRequirement = parser.Parse(ResultWeb);

                    foreach (var el in HtmlRequirement.QuerySelectorAll(".product-row-wrapper .product-state-holder"))
                    {
                        string StoreId = el.GetAttribute("gog-product");

                        Wishlist wishlist = GetGameData(StoreId);
                        if (wishlist != null)
                        {
                            wishlists.Add(wishlist);
                        }
                        else
                        {
                            Logger.Warn($"GOG wishlist is incomplet - StoreId: {StoreId}");
                        }
                    }
                }
                else
                {
                    return cachedData;
                }
            }

            wishlists = SetCurrentPrice(wishlists);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string StoreId)
        {
            return GogAPI.RemoveWishList(StoreId);
        }


        private Wishlist GetGameData(string StoreId)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

            //Download game information
            string url = string.Format(@"https://api.gog.com/products/{0}", StoreId);
            string ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            try
            {
                ProductApiDetail resultObjGame = Serialization.FromJson<ProductApiDetail>(ResultWeb);
                DateTime ReleaseDate = resultObjGame.release_date == null ? default : (DateTime)resultObjGame.release_date;
                string Name = resultObjGame.title;
                string Capsule = "http:" + resultObjGame.images.logo2x;

                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                return new Wishlist
                {
                    StoreId = StoreId,
                    StoreName = "GOG",
                    ShopColor = GetShopColor(),
                    StoreUrl = resultObjGame.links.product_card,
                    Name = WebUtility.HtmlDecode(Name),
                    SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                    Capsule = Capsule,
                    Game = gamesLookup.Found ? gamesLookup.Game : null,
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
