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
            string resultWeb = string.Empty;
            string url = string.Format(@"https://www.humblebundle.com/store/wishlist/{0}", Settings.HumbleKey);

            resultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            if (!resultWeb.IsNullOrEmpty())
            {
                int startSub = resultWeb.IndexOf("<script id=\"storefront-webpack-json-data\" type=\"application/json\">");
                if (startSub == -1)
                {
                    Logger.Warn($"No Humble wishlist?");
                    return wishlists;
                }
                resultWeb = resultWeb.Substring(startSub, resultWeb.Length - startSub);

                int endSub = resultWeb.IndexOf("</script>");
                resultWeb = resultWeb.Substring(0, endSub);

                resultWeb = resultWeb.Replace("<script id=\"storefront-webpack-json-data\" type=\"application/json\">", string.Empty);

                dynamic dataObj = Serialization.FromJson<dynamic>(resultWeb);

                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                foreach (dynamic gameWish in dataObj["products_json"])
                {
                    string storeId = string.Empty;
                    string storeUrl = string.Empty;
                    string name = string.Empty;
                    DateTime releaseDate = default;
                    string capsule = string.Empty;

                    storeId = (string)gameWish["machine_name"];
                    storeUrl = "https://www.humblebundle.com/store/" + gameWish["human_url"];
                    name = WebUtility.HtmlDecode((string)gameWish["human_name"]);
                    capsule = (string)gameWish["standard_carousel_image"];

                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(name).GetAwaiter().GetResult();

                    wishlists.Add(new Wishlist
                    {
                        StoreId = storeId,
                        StoreName = "Humble",
                        ShopColor = GetShopColor(),
                        StoreUrl = storeUrl,
                        Name = name,
                        SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                        ReleaseDate = releaseDate.ToUniversalTime(),
                        Capsule = capsule,
                        Game = gamesLookup.Found ? gamesLookup.Game : null,
                        IsActive = true
                    });
                }
            }

            wishlists = SetCurrentPrice(wishlists, false);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string storeId)
        {
            IWebView view = API.Instance.WebViews.CreateOffscreenView();
            HumbleAccountClientExtend humbleAccountClient = new HumbleAccountClientExtend(view);
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
