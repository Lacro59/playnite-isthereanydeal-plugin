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
using CommonPlayniteShared.PluginLibrary.EpicLibrary;

namespace IsThereAnyDeal.Services
{
    class EpicWishlist : GenericWishlist
    {
        protected static EpicAccountClient _EpicAPI;
        internal static EpicAccountClient EpicAPI
        {
            get
            {
                if (_EpicAPI == null)
                {
                    _EpicAPI = new EpicAccountClient(
                        API.Instance,
                        PluginUserDataPath + "\\..\\00000002-DBD1-46C6-B5D0-B1BA559D10E4\\tokens.json"
                    );
                }
                return _EpicAPI;
            }

            set
            {
                _EpicAPI = value;
            }
        }

        public const string GraphQLEndpoint = @"https://graphql.epicgames.com/graphql";


        public async Task<string> QuerySearchWishList(string query, dynamic variables, string token)
        {
            

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                
            var queryObject = new
            {
                query = query,
                variables = variables
            };
            var content = new StringContent(Serialization.ToJson(queryObject), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(GraphQLEndpoint, content).ConfigureAwait(false);
            var str = await response.Content.ReadAsStringAsync();

            return str;
        }

        private string GetToken(string PluginUserDataPath)
        {
            string access_token = string.Empty;
            try
            {
                dynamic EpicConfig = Serialization.FromJson<dynamic>(
                    Encryption.DecryptFromFile(
                        PluginUserDataPath + "\\..\\00000002-DBD1-46C6-B5D0-B1BA559D10E4\\tokens.json",
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User.Value));

                access_token = (string)EpicConfig["access_token"];
            }
            catch
            {
            }

            return access_token;
        }


        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Epic", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Epic", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for Epic");


            if (!EpicAPI.GetIsUserLoggedIn())
            {
                logger.Warn($"Epic user is not authenticated");

                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"isthereanydeal-epic-noauthenticate",
                    $"IsThereAnyDeal\r\n Epic - {resources.GetString("LOCLoginRequired")}",
                    NotificationType.Error
                ));

                return ResultLoad;
            }

            var tokens = EpicAPI.loadTokens();
            if (tokens.access_token.IsNullOrEmpty())
            {
                logger.Warn($"Epic user is not authenticated");

                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"isthereanydeal-epic-noauthenticate",
                    $"IsThereAnyDeal\r\n Epic - {resources.GetString("LOCLoginRequired")}",
                    NotificationType.Error
                ));

                return ResultLoad;
            }


            // Get wishlist
            string query = @"query wishlistQuery { Wishlist { wishlistItems { elements { offerId namespace offer { title keyImages { type url width height } } } } } }";
            dynamic variables = new { };
            string ResultWeb = QuerySearchWishList(query, variables, tokens.access_token).GetAwaiter().GetResult();
            if (!ResultWeb.IsNullOrEmpty())
            {
                EpicWishlistResult resultObj = null;

                try
                {
                    resultObj = Serialization.FromJson<EpicWishlistResult>(ResultWeb);
#if DEBUG
                    logger.Debug($"IsThereAnyDeal - resultObj: {Serialization.ToJson(resultObj)}");
#endif
                    if (resultObj != null && resultObj.data != null && resultObj.data.Wishlist != null 
                        && resultObj.data.Wishlist.wishlistItems != null && resultObj.data.Wishlist.wishlistItems.elements != null) {

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (Element gameWishlist in resultObj.data.Wishlist.wishlistItems.elements)
                        {
                            string StoreId = string.Empty;
                            string Name = string.Empty;
                            DateTime ReleaseDate = default(DateTime);
                            string Capsule = string.Empty;

                            try
                            {
#if DEBUG
                                logger.Debug($"IsThereAnyDeal - gameWishlist: {Serialization.ToJson(gameWishlist)}");
#endif
                                StoreId = gameWishlist.offerId + "|" + gameWishlist.@namespace;
                                Capsule = string.Empty;

                                Name = WebUtility.HtmlDecode(gameWishlist.offer.title);
                                foreach (var keyImages in gameWishlist.offer.keyImages)
                                {
                                    if (keyImages.type == "Thumbnail")
                                    {
                                        Capsule = keyImages.url;
                                    }
                                }

                                PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                                var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("epic") > -1);

                                Result.Add(new Wishlist
                                {
                                    StoreId = StoreId,
                                    StoreName = "Epic",
                                    ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                    StoreUrl = string.Empty,
                                    Name = Name,
                                    SourceId = SourceId,
                                    ReleaseDate = ReleaseDate.ToUniversalTime(),
                                    Capsule = Capsule,
                                    Plain = plainData.Plain,
                                    IsActive = plainData.IsActive
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error in parse Epic wishlist - {Name}");
                                logger.Warn($"Error in parse Epic wishlist - {Name}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true, "Error in parse Epic wishlist");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Epic-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationError"), "Epic Game Store"),
                        NotificationType.Error
                    ));

                    ResultLoad = LoadWishlists("Epic", PluginUserDataPath, true);
                    if (ResultLoad != null && CacheOnly)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                        SaveWishlist("Epic", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }

                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Epic", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId, string PluginUserDataPath)
        {
            try
            {
                // Get Epic configuration if exist.
                string access_token = GetToken(PluginUserDataPath);
                if (access_token.IsNullOrEmpty())
                {
                    return false;
                }

                string EpicOfferId = StoreId.Split('|')[0];
                string EpicNamespace = StoreId.Split('|')[1];


                string query = @"mutation removeFromWishlistMutation($namespace: String!, $offerId: String!, $operation: RemoveOperation!) { Wishlist { removeFromWishlist(namespace: $namespace, offerId: $offerId, operation: $operation) { success } } }";
                dynamic variables = new
                {
                    @namespace = EpicNamespace,
                    offerId = EpicOfferId,
                    operation = "REMOVE"
                };
                string ResultWeb = QuerySearchWishList(query, variables, access_token).GetAwaiter().GetResult();
#if DEBUG
                logger.Debug($"IsThereAnyDeal - Epic.RemoveWishlist() - {ResultWeb.Trim()}");
#endif
                return ResultWeb.IndexOf("\"success\":true") > -1;
            }
            catch(Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }

            return false;
        }
    }
}
