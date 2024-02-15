using Playnite.SDK;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using CommonPluginsShared;
using IsThereAnyDeal.Models.Api;
using CommonPluginsStores.Epic;
using System.Collections.ObjectModel;
using CommonPluginsStores.Models;

namespace IsThereAnyDeal.Services
{
    public class EpicWishlist : GenericWishlist
    {
        protected static EpicApi _EpicApi;
        internal static EpicApi EpicApi
        {
            get
            {
                if (_EpicApi == null)
                {
                    _EpicApi = new EpicApi("IsThereAnyDeals");
                }
                return _EpicApi;
            }

            set => _EpicApi = value;
        }


        public EpicWishlist(IsThereAnyDeal plugin) : base(plugin, "Epic")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.EpicLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (!EpicApi.IsUserLoggedIn)
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
            ObservableCollection<AccountWishlist> accountWishlist = EpicApi.GetWishlist(EpicApi.CurrentAccountInfos);

            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(x.Name).GetAwaiter().GetResult();
                wishlists.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "Epic",
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
            return EpicApi.RemoveWishlist(StoreId);
        }
    }
}
