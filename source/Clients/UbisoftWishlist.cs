using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPluginsShared;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Clients
{
    class UbisoftWishlist : GenericWishlist
    {
        public List<Wishlist> GetWishlist(Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                }
                SaveWishlist("Ubisoft", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"Load from web for Ubisoft");

            // Get wishlist
            string url = settings.UbisoftLink;

            if (url.IsNullOrEmpty())
            {
                logger.Error($"No Ubisoft wish list link");

                API.Instance.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Ubisoft-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resourceProvider.GetString("LOCItadNotificationErrorUbisoftNoLink"), "Ubisoft"),
                    NotificationType.Error
                ));

                ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath, true);
                if (ResultLoad != null)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings);
                    SaveWishlist("Ubisoft", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }

            string ResultWeb = Web.DownloadStringData(settings.UbisoftLink).GetAwaiter().GetResult();

            if (!ResultWeb.IsNullOrEmpty())
            {
                try
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument HtmlRequirement = parser.Parse(ResultWeb);

                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    foreach (IElement SearchElement in HtmlRequirement.QuerySelectorAll(".wishlist-items-list li"))
                    {
                        string StoreId = string.Empty;
                        string Name = string.Empty;
                        DateTime ReleaseDate = default(DateTime);
                        string Capsule = string.Empty;

                        try
                        {
                            StoreId = SearchElement.QuerySelector("div.wishlist-product-tile.product-tile").GetAttribute("data-itemid");
                            Capsule = SearchElement.QuerySelector("img").GetAttribute("data-src");
                            Name = SearchElement.QuerySelector("div.wishlist-product-tile.product-tile .prod-title").InnerHtml.Trim();
                            
                            GameLookup gamesLookup = isThereAnyDealApi.GetGamesLookup(Name).GetAwaiter().GetResult();

                            ItadShops tempShopColor = settings.Stores.Find(x => x.Title.ToLower().IndexOf("ubisoft") > -1 || x.Title.ToLower().IndexOf("ubisoft connect") > -1 || x.Title.ToLower().IndexOf("uplay") > -1);

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId.Trim(),
                                StoreName = "Ubisoft Connect",
                                ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                StoreUrl = @"https://store.ubi.com/fr/game?pid=" + StoreId.Trim(),
                                Name = Name.Trim(),
                                SourceId = SourceId,
                                ReleaseDate = ReleaseDate.ToUniversalTime(),
                                Capsule = Capsule.Trim(),
                                Game = gamesLookup.Game,
                                IsActive = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, true, $"Error in parse Ubisoft Store wishlist - {Name}");
                            logger.Warn($"Error in parse Ubisoft Store wishlist - {Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true, "Error in parse Ubisoft wishlist");
                    API.Instance.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Ubisoft-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resourceProvider.GetString("LOCItadNotificationError"), "Ubisoft"),
                        NotificationType.Error
                    ));

                    ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath, true);
                    if (ResultLoad != null)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings);
                        SaveWishlist("Ubisoft", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings);
            SaveWishlist("Ubisoft", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId, string PluginUserDataPath)
        {
            return false;
        }
    }
}
