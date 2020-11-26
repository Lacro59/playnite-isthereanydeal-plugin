using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Specialized;
using PluginCommon.PlayniteResources.PluginLibrary.OriginLibrary.Services;
using PluginCommon.PlayniteResources.PluginLibrary.OriginLibrary.Models;

namespace IsThereAnyDeal.Clients
{
    class OriginWishlist : GenericWishlist
    {
        private OriginAccountClient originAPI;

        private string urlBase = "https://www.origin.com/";
        private string urlWishlist = "https://api2.origin.com/gifting/users/{0}/wishlist";
        private string urlWishlistDelete = "https://api2.origin.com/gifting/users/{0}/wishlist?offerId={1}";


        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Origin", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Origin", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"IsThereAnyDeal - Load from web for Origin");

            // Get wishlist
            var view = PlayniteApi.WebViews.CreateOffscreenView();
            originAPI = new OriginAccountClient(view);

            // Only if user is logged. 
            if (originAPI.GetIsUserLoggedIn())
            {
                // Get informations from Origin plugin.
                string accessToken = originAPI.GetAccessToken().access_token;
                var userId = originAPI.GetAccountInfo(originAPI.GetAccessToken()).pid.pidId;

                string url = string.Format(urlWishlist, userId);

                using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
                {
                    try
                    {
                        webClient.Headers.Add("authToken", accessToken);
                        webClient.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");

                        var stringData = webClient.DownloadString(url);
                        string data = JsonConvert.SerializeObject(JObject.Parse(stringData)["wishlist"]);
                        List<WishlistData> WishlistData = JsonConvert.DeserializeObject<List<WishlistData>>(data);

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                        foreach (var item in WishlistData)
                        {
                            string offerId = item.offerId;
                            var gameData = GetGameStoreData(offerId, PlayniteApi);

                            string StoreId = gameData.offerId;
                            string Capsule = gameData.imageServer + gameData.i18n.packArtLarge;
                            DateTime ReleaseDate = default(DateTime);
                            string Name = gameData.i18n.displayName;
                            string StoreUrl = urlBase + gameData.offerPath;

                            PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId,
                                StoreName = "Origin",
                                ShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("origin") > -1).Color,
                                StoreUrl = string.Empty,
                                Name = Name,
                                SourceId = SourceId,
                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                Capsule = Capsule,
                                Plain = plainData.Plain,
                                IsActive = plainData.IsActive
                            });
                        }
                    }
                    catch (WebException ex)
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                        {
                            var resp = (HttpWebResponse)ex.Response;
                            switch (resp.StatusCode)
                            {
                                case HttpStatusCode.NotFound: // HTTP 404
                                    break;
                                default:
                                    Common.LogError(ex, "IsThereAnyDeal", $"Failed to load from {url}. ");
                                    break;
                            }
                            return Result;
                        }
                    }
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Origin", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId, IPlayniteAPI PlayniteApi)
        {
            var view = PlayniteApi.WebViews.CreateOffscreenView();
            originAPI = new OriginAccountClient(view);
    
            // Only if user is logged. 
            if (originAPI.GetIsUserLoggedIn())
            {
                // Get informations from Origin plugin.
                string accessToken = originAPI.GetAccessToken().access_token;
                var userId = originAPI.GetAccountInfo(originAPI.GetAccessToken()).pid.pidId;

                string url = string.Format(urlWishlistDelete, userId, StoreId);

                using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
                {
                    try
                    {
                        webClient.Headers.Add("authToken", accessToken);
                        webClient.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");
                        
                        var stringData = System.Text.Encoding.UTF8.GetString(webClient.UploadValues(url, "DELETE", new NameValueCollection()));
        
                        return stringData.Contains("\"ok\"");
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, "IsThereAnyDeal", $"Error on RemoveWishlist()");
                    }
                }
            }

            return false;
        }


        private static GameStoreDataResponse GetGameStoreData(string gameId, IPlayniteAPI PlayniteApi)
        {
            string lang = CodeLang.GetOriginLang(PlayniteApi.ApplicationSettings.Language);
            string langShort = CodeLang.GetOriginLangCountry(PlayniteApi.ApplicationSettings.Language);

            var url = string.Format(@"https://api2.origin.com/ecommerce2/public/supercat/{0}/{1}?country={2}", gameId, lang, langShort);

            string stringData = Web.DownloadStringData(url).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<GameStoreDataResponse>(stringData);
        }
    }

    public class WishlistData
    {
        public string offerId { get; set; }
        public int displayOrder { get; set; }
        //public DateTime addedAt { get; set; }
    }
}
