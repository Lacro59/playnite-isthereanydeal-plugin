using Playnite.SDK;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using PluginCommon;
using System.Net;

namespace IsThereAnyDeal.Services
{
    class EpicWishlist : GenericWishlist
    {
        public const string GraphQLEndpoint = @"https://graphql.epicgames.com/graphql";


        public async Task<string> QuerySearchWishList(string token)
        {
            string query = @"query wishlistQuery { Wishlist { wishlistItems { elements { offer { title keyImages { type url width height } } } } } }";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                
            var queryObject = new
            {
                query = query,
                variables = new { }
            };
            var content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(GraphQLEndpoint, content).ConfigureAwait(false);
            var str = await response.Content.ReadAsStringAsync();

            return str;
        }


        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool Force = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Epic", PluginUserDataPath);
            if (ResultLoad != null && !Force)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Epic", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (CacheOnly)
            {
                return Result;
            }

            logger.Info($"IsThereAnyDeal - Load from web for Epic");

            // Get Epic configuration if exist.
            string access_token = string.Empty;
            try
            {
                JObject EpicConfig = JObject.Parse(File.ReadAllText(PluginUserDataPath + "\\..\\00000002-DBD1-46C6-B5D0-B1BA559D10E4\\tokens.json"));
                access_token = (string)EpicConfig["access_token"];
            }
            catch
            {
            }

            if (access_token.IsNullOrEmpty())
            {
                logger.Error($"ISThereAnyDeal - No Epic configuration.");
                return Result;
            }

            // Get wishlist
            string ResultWeb = QuerySearchWishList(access_token).GetAwaiter().GetResult();
            if (!ResultWeb.IsNullOrEmpty())
            {
                JObject resultObj = new JObject();

                try
                {
                    resultObj = JObject.Parse(ResultWeb);

                    if (resultObj["data"]["Wishlist"]["wishlistItems"]["elements"] != null) {

                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        foreach (JObject gameWishlist in resultObj["data"]["Wishlist"]["wishlistItems"]["elements"])
                        {
                            int StoreId = 0;
                            string Name = string.Empty;
                            DateTime ReleaseDate = default(DateTime);
                            string Capsule = string.Empty;

                            Name = (string)gameWishlist["offer"]["title"];
                            foreach (var keyImages in gameWishlist["offer"]["keyImages"])
                            {
                                if ((string)keyImages["type"] == "Thumbnail")
                                {
                                    Capsule = (string)keyImages["url"];
                                }
                            }

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId,
                                StoreName = "Epic",
                                StoreUrl = string.Empty,
                                Name = WebUtility.HtmlDecode(Name),
                                SourceId = SourceId,
                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                Capsule = Capsule,
                                Plain = isThereAnyDealApi.GetPlain(Name)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error io parse Epic wishlist");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Epic-Error",
                        resources.GetString("LOCItadNotificationError"),
                        NotificationType.Error
                    ));

                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Epic", PluginUserDataPath, Result);
            return Result;
        }
    }
}
