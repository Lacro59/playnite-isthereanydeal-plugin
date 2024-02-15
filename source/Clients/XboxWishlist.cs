using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AngleSharp.Dom;
using IsThereAnyDeal.Models.Api;

namespace IsThereAnyDeal.Clients
{
    public class XboxWishlist : GenericWishlist
    {
        public XboxWishlist(IsThereAnyDeal plugin) : base(plugin, "Xbox")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.XboxLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (Settings.XboxLink.IsNullOrEmpty())
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

            List<Wishlist> Result = new List<Wishlist>();

            // Get wishlist
            string baseUrl = "https://www.microsoft.com";

            IWebView view = API.Instance.WebViews.CreateOffscreenView();
            view.NavigateAndWait(Settings.XboxLink);
            string ResultWeb = view.GetPageSource();

            if (!ResultWeb.IsNullOrEmpty())
            {
                HtmlParser parser = new HtmlParser();
                IHtmlDocument HtmlRequirement = parser.Parse(ResultWeb);

                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                foreach (IElement SearchElement in HtmlRequirement.QuerySelectorAll("li.product-wishlist-item"))
                {
                    string StoreId = string.Empty;
                    string StoreUrl = string.Empty;
                    string Name = string.Empty;
                    DateTime ReleaseDate = default;
                    string Capsule = string.Empty;

                    StoreId = SearchElement.GetAttribute("data-product-id");
                    Capsule = SearchElement.QuerySelector("img.c-image").GetAttribute("data-src");
                    Name = SearchElement.QuerySelector("h3.c-heading").InnerHtml.Trim();
                    StoreUrl = SearchElement.QuerySelector("a.c-button").GetAttribute("href");

                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                    Result.Add(new Wishlist
                    {
                        StoreId = StoreId.Trim(),
                        StoreName = "Microsoft Store",
                        ShopColor = GetShopColor(),
                        StoreUrl = baseUrl + StoreUrl.Trim(),
                        Name = Name.Trim(),
                        SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                        ReleaseDate = ReleaseDate.ToUniversalTime(),
                        Capsule = Capsule.Trim(),
                        Game = gamesLookup.Game,
                        IsActive = true
                    });
                }
            }
            else
            {
                return cachedData;
            }

            Result = SetCurrentPrice(Result);
            SaveWishlist(Result);
            return Result;
        }

        public override bool RemoveWishlist(string StoreId)
        {
            return false;
        }
    }
}
