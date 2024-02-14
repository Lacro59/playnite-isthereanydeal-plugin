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
    class OriginWishlist : GenericWishlist
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

        private string urlBase = "https://www.origin.com/";
        private string urlWishlist = "https://api2.origin.com/gifting/users/{0}/wishlist";
        private string urlWishlistDelete = "https://api2.origin.com/gifting/users/{0}/wishlist?offerId={1}";


        public List<Wishlist> GetWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Origin", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                }
                SaveWishlist("Origin", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for EA app");

            // Get wishlist
            IWebView view = API.Instance.WebViews.CreateOffscreenView();
            OriginAPI = new OriginAccountClient(view);

            // Only if user is logged. 
            if (OriginAPI.GetIsUserLoggedIn())
            {
                // Get informations from Origin plugin.
                string accessToken = OriginAPI.GetAccessToken().access_token;
                long userId = OriginAPI.GetAccountInfo(OriginAPI.GetAccessToken()).pid.pidId;
                string url = string.Format(urlWishlist, userId);

                using (WebClient webClient = new WebClient { Encoding = Encoding.UTF8 })
                {
                    try
                    {
                        webClient.Headers.Add("authToken", accessToken);
                        webClient.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");

                        var stringData = webClient.DownloadString(url);
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

                            try
                            {
                                string offerId = item.offerId;
                                GameStoreDataResponse gameData = GetGameStoreData(offerId);

                                StoreId = gameData.offerId;
                                Capsule = gameData.imageServer + gameData.i18n.packArtLarge;
                                Name = gameData.i18n.displayName;
                                StoreUrl = urlBase + gameData.offerPath;

                                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                                ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("origin") > -1 || x.Title.ToLower().IndexOf("ea app") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "EA app",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = string.Empty,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Game = gamesLookup.Game,
                                    IsActive = true
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error in parse EA app wishlist - {Name}");
                                logger.Warn($"Error in parse EA app wishlist - {Name}");
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                        {
                            HttpWebResponse resp = (HttpWebResponse)ex.Response;
                            switch (resp.StatusCode)
                            {
                                case HttpStatusCode.NotFound: // HTTP 404
                                    break;
                                default:
                                    Common.LogError(ex, false, $"Failed to load from {url}", true, "IsThereAnyDeal");
                                    break;
                            }
                            return Result;
                        }
                    }
                }
            }
            else
            {
                logger.Warn($"EA app user is not authenticated");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"isthereanydeal-origin-noauthenticate",
                    $"IsThereAnyDeal\r\nEA app - {resourceProvider.GetString("LOCLoginRequired")}",
                    NotificationType.Error
                ));
            }

            Result = SetCurrentPrice(Result, settings);
            SaveWishlist("Origin", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            // Only if user is logged. 
            if (OriginAPI.GetIsUserLoggedIn())
            {
                // Get informations from Origin plugin.
                string accessToken = OriginAPI.GetAccessToken().access_token;
                long userId = OriginAPI.GetAccountInfo(OriginAPI.GetAccessToken()).pid.pidId;

                string url = string.Format(urlWishlistDelete, userId, StoreId);

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
