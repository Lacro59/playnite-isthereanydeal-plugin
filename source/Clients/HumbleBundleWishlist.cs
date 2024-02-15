using Playnite.SDK;
using Playnite.SDK.Data;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using CommonPluginsShared;
using IsThereAnyDeal.Models.Api;

namespace IsThereAnyDeal.Services
{
    public class HumbleBundleWishlist : GenericWishlist
    {
        public HumbleBundleWishlist(IsThereAnyDeal plugin) : base(plugin, "HumbleBundle")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.HumbleLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (Settings.HumbleKey.IsNullOrEmpty())
            {
                Logger.Error($"{ClientName}: No key");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-{ClientName}-Key",
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoreBadConfiguration"), ClientName),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                return cachedData;
            }

            List<Wishlist> wishlists = new List<Wishlist>();
            string ResultWeb = string.Empty;
            string url = string.Format(@"https://www.humblebundle.com/store/wishlist/{0}", Settings.HumbleKey);

            ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            if (!ResultWeb.IsNullOrEmpty())
            {
                int startSub = ResultWeb.IndexOf("<script id=\"storefront-webpack-json-data\" type=\"application/json\">");
                if (startSub == -1)
                {
                    Logger.Warn($"No Humble wishlist?");
                    return wishlists;
                }
                ResultWeb = ResultWeb.Substring(startSub, ResultWeb.Length - startSub);

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
                    DateTime ReleaseDate = default;
                    string Capsule = string.Empty;

                    StoreId = (string)gameWish["machine_name"];
                    StoreUrl = "https://www.humblebundle.com/store/" + gameWish["human_url"];
                    Name = WebUtility.HtmlDecode((string)gameWish["human_name"]);
                    Capsule = (string)gameWish["standard_carousel_image"];

                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                    wishlists.Add(new Wishlist
                    {
                        StoreId = StoreId,
                        StoreName = "Humble",
                        ShopColor = GetShopColor(),
                        StoreUrl = StoreUrl,
                        Name = Name,
                        SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                        ReleaseDate = ReleaseDate.ToUniversalTime(),
                        Capsule = Capsule,
                        Game = gamesLookup.Found ? gamesLookup.Game : null,
                        IsActive = true
                    });
                }
            }

            wishlists = SetCurrentPrice(wishlists);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string StoreId)
        {
            IWebView view = API.Instance.WebViews.CreateOffscreenView();
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
