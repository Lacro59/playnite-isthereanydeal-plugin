using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IsThereAnyDeal.Models.Api;
using System.Collections.ObjectModel;
using CommonPluginsStores.Models;
using CommonPluginsStores.Ea;

namespace IsThereAnyDeal.Clients
{
    public class EaWishlist : GenericWishlist
    {
        protected static EaApi eaApi;
        internal static EaApi EaApi
        {
            get
            {
                if (eaApi == null)
                {
                    eaApi = new EaApi("IsTeherAnyDeals");
                }
                return eaApi;
            }

            set => eaApi = value;
        }


        public EaWishlist(IsThereAnyDeal plugin) : base(plugin, "EA")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.OriginLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!EaApi.IsUserLoggedIn)
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

            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<Wishlist> wishlists = new List<Wishlist>();
            ObservableCollection<AccountWishlist> accountWishlist = EaApi.GetWishlist(EaApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                try
                {
                    GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(x.Name).GetAwaiter().GetResult();
                    wishlists.Add(new Wishlist
                    {
                        StoreId = x.Id,
                        StoreName = "EA app",
                        ShopColor = GetShopColor(),
                        StoreUrl = x.Link,
                        Name = x.Name,
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
            return EaApi.RemoveWishlist(storeId);
        }
    }
}
