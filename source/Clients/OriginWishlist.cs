using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Collections.Specialized;
using CommonPlayniteShared.PluginLibrary.OriginLibrary.Services;
using CommonPlayniteShared.PluginLibrary.OriginLibrary.Models;
using IsThereAnyDeal.Models.Api;

namespace IsThereAnyDeal.Clients
{
    public class OriginWishlist : GenericWishlist
    {
        protected static OriginAccountClient _OriginAPI;
        internal static OriginAccountClient OriginAPI
        {
            get
            {
                if (_OriginAPI == null)
                {
                    _OriginAPI = new OriginAccountClient(WebViewOffscreen);
                }
                return _OriginAPI;
            }

            set => _OriginAPI = value;
        }

        private string UrlBase => "https://www.origin.com/";
        private string UrlWishlist => "https://api2.origin.com/gifting/users/{0}/wishlist";
        private string UrlWishlistDelete => "https://api2.origin.com/gifting/users/{0}/wishlist?offerId={1}";


        public OriginWishlist(IsThereAnyDeal plugin) : base(plugin, "Origin")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.OriginLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!OriginAPI.GetIsUserLoggedIn())
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

            // Get wishlist
            IWebView view = API.Instance.WebViews.CreateOffscreenView();
            OriginAPI = new OriginAccountClient(view);

            // Get informations from Origin plugin.
            string accessToken = OriginAPI.GetAccessToken().access_token;
            long userId = OriginAPI.GetAccountInfo(OriginAPI.GetAccessToken()).pid.pidId;
            string url = string.Format(UrlWishlist, userId);

            using (WebClient webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                webClient.Headers.Add("authToken", accessToken);
                webClient.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");

                string stringData = webClient.DownloadString(url);
                string data = Serialization.ToJson(Serialization.FromJson<dynamic>(stringData)["wishlist"]);
                List<WishlistData> WishlistData = Serialization.FromJson<List<WishlistData>>(data);

                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                foreach (WishlistData item in WishlistData)
                {
                    string StoreId = string.Empty;
                    string StoreUrl = string.Empty;
                    string Name = string.Empty;
                    DateTime ReleaseDate = default;
                    string Capsule = string.Empty;

                    string offerId = item.offerId;
                    GameStoreDataResponse gameData = GetGameStoreData(offerId);

                    StoreId = gameData.offerId;
                    Capsule = gameData.imageServer + gameData.i18n.packArtLarge;
                    Name = gameData.i18n.displayName;
                    StoreUrl = UrlBase + gameData.offerPath;

                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                    wishlists.Add(new Wishlist
                    {
                        StoreId = StoreId,
                        StoreName = "EA app",
                        ShopColor = GetShopColor(),
                        StoreUrl = string.Empty,
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
            // Only if user is logged. 
            if (OriginAPI.GetIsUserLoggedIn())
            {
                // Get informations from Origin plugin.
                string accessToken = OriginAPI.GetAccessToken().access_token;
                long userId = OriginAPI.GetAccountInfo(OriginAPI.GetAccessToken()).pid.pidId;

                string url = string.Format(UrlWishlistDelete, userId, StoreId);

                using (WebClient webClient = new WebClient { Encoding = Encoding.UTF8 })
                {
                    try
                    {
                        webClient.Headers.Add("authToken", accessToken);
                        webClient.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");

                        string stringData = Encoding.UTF8.GetString(webClient.UploadValues(url, "DELETE", new NameValueCollection()));
                        return stringData.Contains("\"ok\"");
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeal");
                    }
                }
            }

            return false;
        }


        private static GameStoreDataResponse GetGameStoreData(string gameId)
        {
            string lang = CodeLang.GetOriginLang(API.Instance.ApplicationSettings.Language);
            string langShort = CodeLang.GetOriginLangCountry(API.Instance.ApplicationSettings.Language);

            string url = string.Format(@"https://api2.origin.com/ecommerce2/public/supercat/{0}/{1}?country={2}", gameId, lang, langShort);

            string stringData = Web.DownloadStringData(url).GetAwaiter().GetResult();
            return Serialization.FromJson<GameStoreDataResponse>(stringData);
        }
    }

    public class WishlistData
    {
        public string offerId { get; set; }
        public int displayOrder { get; set; }
        //public DateTime addedAt { get; set; }
    }
}
