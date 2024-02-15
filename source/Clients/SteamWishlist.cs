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


        public SteamWishlist(IsThereAnyDeal plugin) : base(plugin, "Steam")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.SteamLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!SteamApi.IsUserLoggedIn)
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

            List<Wishlist> Result = new List<Wishlist>();
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            ObservableCollection<AccountWishlist> accountWishlist = SteamApi.GetWishlist(SteamApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(x.Id)).GetAwaiter().GetResult();
                Result.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "Steam",
                    ShopColor = GetShopColor(),
                    StoreUrl = x.Link,
                    Name = x.Name,
                    SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                    ReleaseDate = x.Released,
                    Added = x.Added,
                    Capsule = x.Image,
                    Game = gamesLookup.Found ? gamesLookup.Game : null,
                    IsActive = true
                });
            });

            Result = SetCurrentPrice(Result);
            SaveWishlist(Result);
            return Result;
        }

        public override bool RemoveWishlist(string StoreId)
        {
            return SteamApi.RemoveWishlist(StoreId);
        }


        public bool ImportWishlist(string FilePath)
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
                                DateTime.TryParse(AppDetails?.release_date?.date, out DateTime ReleaseDate);

                                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(StoreId)).GetAwaiter().GetResult();

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Steam",
                                    ShopColor = GetShopColor(),
                                    StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                                    Name = Name,
                                    SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Game = gamesLookup.Found ? gamesLookup.Game : null,
                                    IsActive = true
                                });
                            }
                            catch(Exception ex)
                            {
                                Common.LogError(ex, false, $"Error for import Steam game {StoreId}", true, "IsThereAnyDeal");
                            }
                        }
                    }

                    Result = SetCurrentPrice(Result);
                    SaveWishlist(Result);

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
