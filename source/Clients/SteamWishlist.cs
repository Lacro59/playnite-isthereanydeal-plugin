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
using IsThereAnyDeal.Models.Api;
using System.Collections.ObjectModel;
using CommonPluginsStores.Models;
using CommonPluginsStores.Steam;

namespace IsThereAnyDeal.Services
{
    public class SteamWishlist : GenericWishlist
    {
        private static SteamApi SteamApi => IsThereAnyDeal.SteamApi;


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
                    () => Plugin.OpenSettingsView()
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
                        GameInfos gameInfos = SteamApi.GetGameInfos((string)el, null);
                        if (gameInfos != null)
                        {
                            GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse((string)el)).GetAwaiter().GetResult();
                            Result.Add(new Wishlist
                            {
                                StoreId = (string)el,
                                StoreName = "Steam",
                                ShopColor = GetShopColor(),
                                StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                                Name = gameInfos.Name,
                                SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                                ReleaseDate = gameInfos.Released?.ToUniversalTime(),
                                Capsule = gameInfos.Image,
                                Game = gamesLookup.Found ? gamesLookup.Game : null,
                                IsActive = true
                            });
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
