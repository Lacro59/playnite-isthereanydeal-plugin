using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPluginsShared;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IsThereAnyDeal.Clients
{
    public class UplayWishlist : GenericWishlist
    {
        public UplayWishlist(IsThereAnyDeal plugin) : base(plugin, "Ubisoft")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.UplayLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (Settings.UbisoftLink.IsNullOrEmpty())
            {
                Logger.Error($"{ClientName}: No url");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-{ClientName}-Url",
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoreBadConfiguration"), ClientName),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                return cachedData;
            }

            List<Wishlist> wishlists = new List<Wishlist>();

            // Get wishlist
            string response = Web.DownloadStringData(Settings.UbisoftLink).GetAwaiter().GetResult();

            if (!response.IsNullOrEmpty())
            {
                HtmlParser parser = new HtmlParser();
                IHtmlDocument HtmlRequirement = parser.Parse(response);

                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                foreach (IElement SearchElement in HtmlRequirement.QuerySelectorAll(".wishlist-items-list li"))
                {
                    string StoreId = string.Empty;
                    string Name = string.Empty;
                    DateTime ReleaseDate = default;
                    string Capsule = string.Empty;

                    StoreId = SearchElement.QuerySelector("div.wishlist-product-tile.product-tile").GetAttribute("data-itemid");
                    Capsule = SearchElement.QuerySelector("img").GetAttribute("data-src");
                    Name = SearchElement.QuerySelector("div.wishlist-product-tile.product-tile .prod-title").InnerHtml.Trim();

                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                    wishlists.Add(new Wishlist
                    {
                        StoreId = StoreId.Trim(),
                        StoreName = "Ubisoft Connect",
                        ShopColor = GetShopColor(),
                        StoreUrl = @"https://store.ubi.com/fr/game?pid=" + StoreId.Trim(),
                        Name = Name.Trim(),
                        SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                        ReleaseDate = ReleaseDate.ToUniversalTime(),
                        Capsule = Capsule.Trim(),
                        Game = gamesLookup.Found ? gamesLookup.Game : null,
                        IsActive = true
                    });
                }
            }
            else
            {
                return cachedData;
            }

            wishlists = SetCurrentPrice(wishlists, false);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string storeId)
        {
            return false;
        }
    }
}
