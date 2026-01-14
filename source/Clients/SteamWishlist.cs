using CommonPlayniteShared.PluginLibrary.SteamLibrary;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using CommonPluginsStores.Models;
using CommonPluginsStores.Steam;
using CommonPluginsStores.Steam.Models;
using IsThereAnyDeal.Models.Api;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace IsThereAnyDeal.Services
{
    public class SteamWishlist : GenericWishlist
    {
        private static SteamApi SteamApi => IsThereAnyDeal.SteamApi;


        public SteamWishlist(IsThereAnyDeal plugin) : base(plugin, "Steam")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.SteamLibrary;
        }

        internal override List<Models.Wishlist> GetStoreWishlist(List<Models.Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!SteamApi.IsUserLoggedIn)
            {
                Logger.Warn($"{ClientName}: Not authenticated");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-{ClientName}-NotAuthenticate",
                    IsThereAnyDeal.PluginName + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoresNoAuthenticate"), ClientName),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                return cachedData;
            }

            List<Models.Wishlist> wishlists = new List<Models.Wishlist>();
            ObservableCollection<AccountWishlist> accountWishlist = SteamApi.GetWishlist(SteamApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                try
                {
                    uint.TryParse(x.Id, out uint appId);
                    if (appId > 0)
                    {
                        GameLookup gamesLookup = IsThereAnyDealApi.GetGamesLookup(appId).GetAwaiter().GetResult();
                        wishlists.Add(new Models.Wishlist
                        {
                            StoreId = x.Id,
                            StoreName = ClientName,
                            ShopColor = GetShopColor(),
                            StoreUrl = x.Link,
                            Name = x.Name.IsEqual($"SteamApp? - {x.Id}") && (gamesLookup?.Found ?? false) ? gamesLookup.Game.Title : x.Name,
                            SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                            ReleaseDate = x.Released,
                            Added = x.Added,
                            Capsule = x.Image,
                            Game = (gamesLookup?.Found ?? false) ? gamesLookup.Game : null,
                            IsActive = true
                        });
                    }
                    else
                    {
                        Logger.Warn($"{ClientName}: Invalid AppID {x.Id} for {x.Name}");
					}
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
                }
            });

			wishlists = SetCurrentPrice(wishlists, false);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string storeId)
        {
            return SteamApi.RemoveWishlist(storeId);
        }

        public bool ImportWishlist(string filePath)
        {
            List<Models.Wishlist> wishlists = new List<Models.Wishlist>();

            if (File.Exists(filePath) && Serialization.TryFromJsonFile(filePath, out SteamUserData steamUserData))
            {
                try
                {
					steamUserData?.RgWishlist?.ForEach(appId =>
                    {
						var gameData = SteamApi.GetAppDetails(appId, 1);
						GameLookup gamesLookup = IsThereAnyDealApi.GetGamesLookup(appId).GetAwaiter().GetResult();

                        wishlists.Add(new Models.Wishlist
                        {
                            StoreId = appId.ToString(),
                            StoreName = ClientName,
                            ShopColor = GetShopColor(),
                            StoreUrl = $"https://store.steampowered.com/app/{appId}",
                            Name = (gameData?.data?.name.IsNullOrEmpty() ?? true) && (gamesLookup?.Found ?? false) ? gamesLookup.Game.Title : gameData?.data?.name ?? $"SteamApp - {appId}",
                            SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                            ReleaseDate = DateHelper.ParseReleaseDate(gameData?.data?.release_date?.date)?.Date,
                            Capsule = gameData?.data?.header_image ?? string.Empty,

							Game = gamesLookup != null ? gamesLookup.Found ? gamesLookup.Game : null : null,
                            IsActive = true
                        });
                    });

                    wishlists = SetCurrentPrice(wishlists, false);
                    SaveWishlist(wishlists);

                    return true;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
                }
            }

            return false;
        }
    }
}