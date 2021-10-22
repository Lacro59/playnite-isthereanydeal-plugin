using Playnite.SDK;
using Playnite.SDK.Data;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using CommonPluginsShared;
using System.Net;

namespace IsThereAnyDeal.Services
{
    class EpicWishlist : GenericWishlist
    {
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
                dynamic EpicConfig = Serialization.FromJsonFile<dynamic>(PluginUserDataPath + "\\..\\00000002-DBD1-46C6-B5D0-B1BA559D10E4\\tokens.json");
                access_token = (string)EpicConfig["access_token"];

            }
            catch
            {
            }

            if (access_token.IsNullOrEmpty())
            {
                logger.Error($"ISThereAnyDeal - No Epic configuration.");
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

            logger.Info($"IsThereAnyDeal - Load from web for Epic");

            // Get Epic configuration if exist.
            // TODO Check with new Epic plugin
            string access_token = GetToken(PluginUserDataPath);
            if (access_token.IsNullOrEmpty())
            {
                return ResultLoad;
            }


            // Get wishlist
            string query = @"query wishlistQuery { Wishlist { wishlistItems { elements { offerId namespace offer { title keyImages { type url width height } } } } } }";
            dynamic variables = new { };
            string ResultWeb = QuerySearchWishList(query, variables, access_token).GetAwaiter().GetResult();
            if (!ResultWeb.IsNullOrEmpty())
            {
                dynamic resultObj = null;

                try
                {
                    resultObj = Serialization.FromJson<dynamic>(ResultWeb);
#if DEBUG
                    logger.Debug($"IsThereAnyDeal - resultObj: {Serialization.ToJson(resultObj)}");
#endif
                    if (resultObj != null && resultObj["data"] != null && resultObj["data"]["Wishlist"] != null 
                        && resultObj["data"]["Wishlist"]["wishlistItems"] != null && resultObj["data"]["Wishlist"]["wishlistItems"]["elements"] != null) {

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (dynamic gameWishlist in resultObj["data"]["Wishlist"]["wishlistItems"]["elements"])
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
                                StoreId = (string)gameWishlist["offerId"] + "|" + (string)gameWishlist["namespace"];
                                Capsule = string.Empty;

                                Name = WebUtility.HtmlDecode((string)gameWishlist["offer"]["title"]);
                                foreach (var keyImages in gameWishlist["offer"]["keyImages"])
                                {
                                    if ((string)keyImages["type"] == "Thumbnail")
                                    {
                                        Capsule = (string)keyImages["url"];
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
                                logger.Warn($"IsThereAnyDeal - Error in parse Epic wishlist - {Name}");
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
                Common.LogError(ex, false, $"Error on RemoveWishlist()");
            }

            return false;
        }
    }
}
