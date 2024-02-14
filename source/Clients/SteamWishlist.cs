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
using CommonPluginsStores.Steam;
using IsThereAnyDeal.Models.Api;
using System.Collections.ObjectModel;
using CommonPluginsStores.Models;

namespace IsThereAnyDeal.Services
{
    public class SteamWishlist : GenericWishlist
    {
        protected static SteamApi _SteamApi;
        internal static SteamApi SteamApi
        {
            get
            {
                if (_SteamApi == null)
                {
                    _SteamApi = new SteamApi("IsThereAnyDeal");
                }
                return _SteamApi;
            }

            set => _SteamApi = value;
        }

        private string UrlAppData => @"https://store.steampowered.com/api/appdetails?appids={0}";


        public SteamWishlist(IsThereAnyDeal plugin) : base(plugin)
        {
        }

        public List<Wishlist> GetWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Steam", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                }
                SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for Steam");

            if (!SteamApi.IsUserLoggedIn)
            {
                logger.Error($"No Steam configuration.");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Steam-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resourceProvider.GetString("LOCItadNotificationsSteamBadConfig"), "Steam"),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                // Load in cache
                ResultLoad = LoadWishlists("Steam", PluginUserDataPath, true);
                if (ResultLoad != null)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                    SaveWishlist("Steam", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }

            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("steam") > -1);

            ObservableCollection<AccountWishlist> accountWishlist = SteamApi.GetWishlist(SteamApi.CurrentAccountInfos);
            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(x.Id)).GetAwaiter().GetResult();
                Result.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "Steam",
                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                    StoreUrl = x.Link,
                    Name = x.Name,
                    SourceId = SourceId,
                    ReleaseDate = x.Released,
                    Added = x.Added,
                    Capsule = x.Image,
                    Game = gamesLookup.Found ? gamesLookup.Game : null,
                    IsActive = true
                });
            });

            Result = SetCurrentPrice(Result, settings);
            SaveWishlist("Steam", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            //string Url = @"https://store.steampowered.com/wishlist/profiles/{0}/remove/";
            return false;
        }

        public bool ImportWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, string FilePath)
        {
            List<Wishlist> Result = new List<Wishlist>();

            if (File.Exists(FilePath) && Serialization.TryFromJsonFile(FilePath, out dynamic jObject))
            {
                try
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    dynamic rgWishlist = jObject["rgWishlist"];

                    foreach(dynamic el in rgWishlist)
                    {
                        // Respect API limitation
                        Thread.Sleep(1000);

                        string response = string.Empty;
                        try
                        {
                            response = Web.DownloadStringData(string.Format(UrlAppData, (string)el)).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, $"Error download Steam app data - {el.ToString()}", true, "IsThereAnyDeal");
                            return false;
                        }

                        if (!response.IsNullOrEmpty())
                        {
                            string StoreId = string.Empty;
                            try
                            {
                                StoreId = (string)el;

                                Dictionary<string, StoreAppDetailsResult> parsedData = Serialization.FromJson<Dictionary<string, StoreAppDetailsResult>>(response);
                                dynamic AppDetails = parsedData[el.ToString()].data;

                                if (AppDetails == null)
                                {
                                    continue;
                                }
                                
                                string Name = WebUtility.HtmlDecode(AppDetails.name);
                                string Capsule = AppDetails.header_image;

                                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(StoreId)).GetAwaiter().GetResult();
                                ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("steam") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Steam",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = DateTime.TryParse(AppDetails?.release_date?.date, out DateTime ReleaseDate),
                                    Capsule = Capsule,
                                    Game = gamesLookup.Game,
                                    IsActive = true
                                });
                            }
                            catch(Exception ex)
                            {
                                Common.LogError(ex, false, $"Error for import Steam game {StoreId}", true, "IsThereAnyDeal");
                            }
                        }
                    }

                    Result = SetCurrentPrice(Result, settings);
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
