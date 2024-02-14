using Playnite.SDK;
using Playnite.SDK.Data;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using CommonPluginsShared;
using System.Net;
using CommonPlayniteShared.Common;
using System.Security.Principal;
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

        public const string GraphQLEndpoint = @"https://graphql.epicgames.com/graphql";


        public EpicWishlist(IsThereAnyDeal plugin) : base(plugin)
        {
        }

        public List<Wishlist> GetWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Epic", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                }
                SaveWishlist("Epic", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for Epic");

            if (!EpicApi.IsUserLoggedIn)
            {
                logger.Warn($"Epic user is not authenticated");
                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Epic-NoAuthenticate",
                    "IsThereAnyDeal" + Environment.NewLine
                        + string.Format(resourceProvider.GetString("LOCCommonStoresNoAuthenticate"), "Epic"),
                    NotificationType.Error
                ));

                // Load in cache
                ResultLoad = LoadWishlists("Epic", PluginUserDataPath, true);
                if (ResultLoad != null)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                    SaveWishlist("Epic", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }

            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("epic") > -1);

            ObservableCollection<AccountWishlist> accountWishlist = EpicApi.GetWishlist(EpicApi.CurrentAccountInfos);
            accountWishlist.ForEach(x =>
            {
                GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(int.Parse(x.Id)).GetAwaiter().GetResult();
                Result.Add(new Wishlist
                {
                    StoreId = x.Id,
                    StoreName = "Epic",
                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                    StoreUrl = x.Link,
                    Name = x.Name,
                    SourceId = SourceId,
                    ReleaseDate = x.Released,
                    Added = x.Added,
                    Capsule = x.Image,
                    Game = gamesLookup.Found ? gamesLookup.Game : null,
                    IsActive = true
                });
            });

            Result = SetCurrentPrice(Result, settings);
            SaveWishlist("Epic", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId)
        {
            return EpicApi.RemoveWishlist(StoreId);
        }
    }
}
