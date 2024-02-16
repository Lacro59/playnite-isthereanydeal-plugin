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
using CommonPluginsStores.Origin;

namespace IsThereAnyDeal.Clients
{
    public class OriginWishlist : GenericWishlist
    {
        protected static OriginApi _OriginApi;
        internal static OriginApi OriginApi
        {
            get
            {
                if (_OriginApi == null)
                {
                    _OriginApi = new OriginApi("IsTeherAnyDeals");
                }
                return _OriginApi;
            }

            set => _OriginApi = value;
        }


        public OriginWishlist(IsThereAnyDeal plugin) : base(plugin, "Origin")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.OriginLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!OriginApi.IsUserLoggedIn)
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
            ObservableCollection<AccountWishlist> accountWishlist = OriginApi.GetWishlist(OriginApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
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
            return OriginApi.RemoveWishlist(StoreId);
        }
    }
}
