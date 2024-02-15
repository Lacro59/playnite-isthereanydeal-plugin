using IsThereAnyDeal.Models;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using IsThereAnyDeal.Models.Api;
using CommonPluginsStores.Gog;
using CommonPluginsStores.Models;
using System.Collections.ObjectModel;

namespace IsThereAnyDeal.Services
{
    public class GogWishlist : GenericWishlist
    {
        protected static GogApi _GogApi;
        internal static GogApi GogApi
        {
            get
            {
                if (_GogApi == null)
                {
                    _GogApi = new GogApi("IsThereAnyDeal");
                }
                return _GogApi;
            }

            set => _GogApi = value;
        }


        public GogWishlist(IsThereAnyDeal plugin) : base(plugin, "GOG")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.GogLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!GogApi.IsUserLoggedIn)
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
            ObservableCollection<AccountWishlist> accountWishlist = GogApi.GetWishlist(GogApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(x.Name).GetAwaiter().GetResult();
                wishlists.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "GOG",
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

            wishlists = SetCurrentPrice(wishlists);
            SaveWishlist(wishlists);
            return wishlists;
        }

        public override bool RemoveWishlist(string StoreId)
        {
            return GogApi.RemoveWishlist(StoreId);
        }
    }
}
