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
using CommonPluginsStores.Xbox;

namespace IsThereAnyDeal.Clients
{
    public class XboxWishlist : GenericWishlist
    {
        protected static XboxApi _XboxApi;
        internal static XboxApi XboxApi
        {
            get
            {
                if (_XboxApi == null)
                {
                    _XboxApi = new XboxApi("IsThereAnyDeals");
                }
                return _XboxApi;
            }

            set => _XboxApi = value;
        }


        public XboxWishlist(IsThereAnyDeal plugin) : base(plugin, "Xbox")
        {
            ExternalPlugin = PlayniteTools.ExternalPlugin.XboxLibrary;
        }

        internal override List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData)
        {
            Logger.Info($"Load data from web for {ClientName}");

            if (Settings.XboxLink.IsNullOrEmpty() 
                && !Settings.XboxLink.StartsWith("https://www.microsoft.com/", StringComparison.InvariantCultureIgnoreCase)
                && !Settings.XboxLink.Contains("wishlist?id=", StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.Error($"{ClientName}: No url");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-{ClientName}-Url",
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(ResourceProvider.GetString("LOCCommonStoreBadConfiguration"), ClientName),
                    NotificationType.Error,
                    () => Plugin.OpenSettingsView()
                ));

                return cachedData;
            }

            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<Wishlist> wishlists = new List<Wishlist>();
            ObservableCollection<AccountWishlist> accountWishlist = XboxApi.GetWishlist(new AccountInfos { Link = Settings.XboxLink });

            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(x.Name).GetAwaiter().GetResult();
                wishlists.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "Microsoft Store",
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
            return false;
        }
    }
}
