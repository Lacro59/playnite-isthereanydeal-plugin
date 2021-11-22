using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using CommonPlayniteShared.PluginLibrary.SteamLibrary.SteamShared;
using CommonPluginsStores;
using AngleSharp.Parser.Html;
using AngleSharp.Dom.Html;
using System.Text.RegularExpressions;

namespace IsThereAnyDeal.Services
{
    class SteamWishlist : GenericWishlist
    {
        protected static SteamApi _steamApi;
        internal static SteamApi steamApi
        {
            get
            {
                if (_steamApi == null)
                {
                    _steamApi = new SteamApi();
                }
                return _steamApi;
            }

            set
            {
                _steamApi = value;
            }
        }

        private const string UrlProfil = @"https://steamcommunity.com/my/profile";
        public readonly string UrlWishlist = @"https://store.steampowered.com/wishlist/profiles/{0}/wishlistdata/?p={1}&v=";
        private readonly string UrlAppData = @"https://store.steampowered.com/api/appdetails?appids={0}";

        private static string SteamId { get; set; } = string.Empty;
        private static string SteamApiKey { get; set; } = string.Empty;
        private static string SteamUser { get; set; } = string.Empty;


        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Steam", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for Steam");

            // Get Steam configuration if exist.
            try
            {
                if (File.Exists(PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json"))
                {
                    dynamic SteamConfig = Serialization.FromJsonFile<dynamic>(PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json");
                    SteamId = (string)SteamConfig["UserId"];
                    SteamApiKey = (string)SteamConfig["ApiKey"];
                    SteamUser = steamApi.GetSteamUsers()?.First()?.PersonaName;
                }

                SteamUserAndSteamIdByWeb();
            }
            catch
            {
            }

            if (SteamId.IsNullOrEmpty())
            {
                logger.Error($"No Steam configuration.");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Steam-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationsSteamBadConfig"), "Steam"),
                    NotificationType.Error
                ));

                ResultLoad = LoadWishlists("Steam", PluginUserDataPath, true);
                if (ResultLoad != null && CacheOnly)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                    SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }


            string ResultWeb = string.Empty;
            string url = string.Empty;

            for (int iPage = 0; iPage < 10; iPage++)
            {
                url = string.Format(UrlWishlist, SteamId, iPage);

                WebViewOffscreen.NavigateAndWait(url);
                ResultWeb = WebViewOffscreen.GetPageText();

                //try
                //{
                //    ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
                //}
                //catch (WebException ex)
                //{
                //    if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                //    {
                //        Common.LogError(ex, false, $"Error download Steam wishlist for page {iPage}", true, "IsThereAnyDeal");
                //        return Result;
                //    }
                //}
                
                if (ResultWeb.ToLower().Contains("{\"success\":2}"))
                {
                    logger.Warn($"Private wishlist for {SteamId}?");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Steam-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationErrorSteamPrivate"), "Steam"),
                        NotificationType.Error
                    ));
                }


                if (!ResultWeb.IsNullOrEmpty())
                {
                    dynamic resultObj = null;

                    if (ResultWeb == "[]")
                    {
                        logger.Info($"No result after page {iPage} for Steam wishlist");
                        break;
                    }

                    try
                    {
                        resultObj = Serialization.FromJson<dynamic>(ResultWeb);

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (var gameWishlist in resultObj)
                        {
                            string StoreId = string.Empty;
                            string Name = string.Empty;
                            DateTime ReleaseDate = default(DateTime);
                            string Capsule = string.Empty;

                            try
                            {
                                dynamic gameWishlistData = (dynamic)gameWishlist.Value;

                                StoreId = gameWishlist.Name;
                                Name = WebUtility.HtmlDecode((string)gameWishlistData["name"]);

                                string release_date = ((string)gameWishlistData["release_date"])?.Split('.')[0];
                                int.TryParse(release_date, out int release_date_int);
                                ReleaseDate = (release_date_int == 0) ? default(DateTime) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(release_date_int);

                                Capsule = (string)gameWishlistData["capsule"];

                                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("steam") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Steam",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = "https://store.steampowered.com/app/" + StoreId,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Plain = plainData.Plain,
                                    IsActive = plainData.IsActive
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error in parse Steam wishlist - {Name}");
                                logger.Warn($"Error in parse Steam wishlist - {Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, "Error in parse Steam wishlist", true, "IsThereAnyDeal");

                        ResultLoad = LoadWishlists("Steam", PluginUserDataPath, true);
                        if (ResultLoad != null && CacheOnly)
                        {
                            ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                            SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                            return ResultLoad;
                        }
                        return Result;
                    }
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Steam", PluginUserDataPath, Result);
            return Result;
        }


        private void SteamUserAndSteamIdByWeb()
        {
            if (SteamUser.IsNullOrEmpty() || SteamId.IsNullOrEmpty())
            {
                WebViewOffscreen.NavigateAndWait(UrlProfil);
                WebViewOffscreen.NavigateAndWait(WebViewOffscreen.GetCurrentAddress());
                string ResultWeb = WebViewOffscreen.GetPageSource();

                if (SteamUser.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                    var el = htmlDocument.QuerySelector(".actual_persona_name");
                    if (el != null)
                    {
                        SteamUser = el.InnerHtml;
                    }
                }

                if (SteamId.IsNullOrEmpty())
                {
                    int index = ResultWeb.IndexOf("g_steamID = ");
                    if (index > -1)
                    {
                        ResultWeb = ResultWeb.Substring(index + "g_steamID  = ".Length);

                        index = ResultWeb.IndexOf("g_strLanguage =");
                        ResultWeb = ResultWeb.Substring(0, index).Trim();

                        ResultWeb = ResultWeb.Substring(0, ResultWeb.Length - 1).Trim();

                        SteamId = Regex.Replace(ResultWeb, @"[^\d]", string.Empty);
                    }
                }
            }
        }




        public bool RemoveWishlist(string StoreId)
        {
            string Url = @"https://store.steampowered.com/wishlist/profiles/76561198003215440/remove/";
            // formfata : appid=632470&sessionid=8e1207c6343129ee6b8098a2
            return false;
        }

        public bool ImportWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, string FilePath)
        {
            List<Wishlist> Result = new List<Wishlist>();

            if (File.Exists(FilePath))
            {
                try
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    
                    dynamic jObject = Serialization.FromJsonFile<dynamic>(FilePath);

                    var rgWishlist = jObject["rgWishlist"];
                    foreach(var el in rgWishlist)
                    {
                        // Respect API limitation
                        Thread.Sleep(1000);

                        string ResultWeb = string.Empty;
                        try
                        {
                            ResultWeb = Web.DownloadStringData(string.Format(UrlAppData, (string)el)).GetAwaiter().GetResult();
                        }
                        catch (WebException ex)
                        {
                            if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                            {
                                Common.LogError(ex, false, $"Error download Steam app data - {el.ToString()}", true, "IsThereAnyDeal");
                                return false;
                            }
                        }

                        if (!ResultWeb.IsNullOrEmpty())
                        {
                            string StoreId = string.Empty;

                            try
                            {
                                StoreId = (string)el;

                                var parsedData = Serialization.FromJson<Dictionary<string, StoreAppDetailsResult>>(ResultWeb);
                                var AppDetails = parsedData[el.ToString()].data;

                                if (AppDetails == null)
                                {
                                    continue;
                                }
                                
                                string Name = WebUtility.HtmlDecode(AppDetails.name);

                                if (!DateTime.TryParse(AppDetails?.release_date?.date, out DateTime ReleaseDate))
                                {
                                    ReleaseDate = default(DateTime);
                                }

                                string Capsule = AppDetails.header_image;

                                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("steam") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Steam",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Plain = plainData.Plain,
                                    IsActive = plainData.IsActive
                                });
                            }
                            catch(Exception ex)
                            {
                                Common.LogError(ex, false, $"Error for import Steam game {StoreId}", true, "IsThereAnyDeal");
                            }
                        }
                    }

                    Result = SetCurrentPrice(Result, settings, PlayniteApi);
                    SaveWishlist("Steam", PluginUserDataPath, Result);

                    return true;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }
            }

            return false;
        }
    }
}
