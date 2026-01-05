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
using CommonPluginsShared.Extensions;
using CommonPluginsStores.Steam.Models.SteamKit;
using CommonPluginsStores.Steam.Models;
using System.Web.UI.WebControls;

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
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoresNoAuthenticate"), ClientName),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                return cachedData;
            }

            List<Models.Wishlist> wishlists = new List<Models.Wishlist>();
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            ObservableCollection<AccountWishlist> accountWishlist = SteamApi.GetWishlist(SteamApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                try
                {
                    GameLookup gamesLookup = gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(x.Id)).GetAwaiter().GetResult();
                    wishlists.Add(new Models.Wishlist
                    {
                        StoreId = x.Id,
                        StoreName = "Steam",
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
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
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

        // TODO Rewrite
        public bool ImportWishlist(string filePath)
        {
            List<Models.Wishlist> wishlists = new List<Models.Wishlist>();

            if (File.Exists(filePath) && Serialization.TryFromJsonFile(filePath, out dynamic jObject))
            {
                try
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    dynamic rgWishlist = jObject["rgWishlist"];

                    foreach(dynamic el in rgWishlist)
                    {
                        string name = SteamApi.GetGameName(uint.Parse((string)el));

                        GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse((string)el)).GetAwaiter().GetResult();
                        wishlists.Add(new Models.Wishlist
                        {
                            StoreId = (string)el,
                            StoreName = "Steam",
                            ShopColor = GetShopColor(),
                            StoreUrl = "https://store.steampowered.com/app/" + (string)el,
                            Name = (name?.IsNullOrEmpty() ?? true) && gamesLookup.Found ? gamesLookup.Game.Title : name,
                            SourceId = PlayniteTools.GetPluginId(ExternalPlugin),
                            ReleaseDate = null,
                            Capsule = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header_292x136.jpg", (string)el),
                            Game = gamesLookup.Found ? gamesLookup.Game : null,
                            IsActive = true
                        });
                    }

                    wishlists = SetCurrentPrice(wishlists, false);
                    SaveWishlist(wishlists);

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
