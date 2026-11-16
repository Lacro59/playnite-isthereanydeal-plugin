using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPluginsShared;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace IsThereAnyDeal.Clients
{
    public class UbisoftWishlist : GenericWishlist
    {
        public UbisoftWishlist(IsThereAnyDeal plugin) : base(plugin, "Ubisoft Connect")
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

                foreach (IElement searchElement in HtmlRequirement.QuerySelectorAll(".wishlist-items-list li"))
                {
                    string storeId = string.Empty;
                    string name = string.Empty;
                    DateTime releaseDate = default;
                    string capsule = string.Empty;

                    storeId = searchElement.QuerySelector("div.product-tile").GetAttribute("data-itemid");
                    capsule = searchElement.QuerySelector("img").GetAttribute("data-src");
                    name = searchElement.QuerySelector("div.card-title div.prod-title").InnerHtml.Trim();

                    wishlists.Add(new Wishlist
                    {
                        StoreId = storeId.Trim(),
                        StoreName = ClientName,
                        ShopColor = GetShopColor(),
                        StoreUrl = @"https://store.ubi.com/fr/game?pid=" + storeId.Trim(),
                        Name = name.Trim(),
                        SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                        ReleaseDate = releaseDate.ToUniversalTime(),
                        Capsule = capsule.Trim(),
                        Game = null,
                        IsActive = true
                    });
                }
            }
            else
            {
                return cachedData;
            }

            MergeCachedItadData(wishlists, cachedData);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string storeId)
        {
            return false;
        }
    }
}
