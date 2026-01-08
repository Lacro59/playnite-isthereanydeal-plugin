using CommonPluginsShared;
using CommonPluginsStores.Ea;
using CommonPluginsStores.Models;
using CommonPluginsStores.Xbox;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace IsThereAnyDeal.Clients
{
    public class XboxWishlist : GenericWishlist
    {
        private readonly Lazy<XboxApi> _lazyApi = new Lazy<XboxApi>(() => new XboxApi("IsThereAnyDeal"));
        internal XboxApi XboxApi => _lazyApi.Value;


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

            List<Wishlist> wishlists = new List<Wishlist>();
            ObservableCollection<AccountWishlist> accountWishlist = XboxApi.GetWishlist(new AccountInfos { Link = Settings.XboxLink });

            accountWishlist.ForEach(x =>
            {
                try
                {
                    GameLookup gamesLookup = IsThereAnyDealApi.GetGamesLookup(x.Name).GetAwaiter().GetResult();
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
            return false;
        }
    }
}