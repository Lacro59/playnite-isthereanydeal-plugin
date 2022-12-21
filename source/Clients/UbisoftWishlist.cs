using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPluginsShared;
using IsThereAnyDeal.Models;
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
        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool ForcePrice = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                if (ForcePrice)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
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

                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Ubisoft-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationErrorUbisoftNoLink"), "Ubisoft"),
                    NotificationType.Error
                ));

                ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath, true);
                if (ResultLoad != null)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
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

                            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                            PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                            var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("ubisoft") > -1 || x.Id.ToLower().IndexOf("ubisoft connect") > -1 || x.Id.ToLower().IndexOf("uplay") > -1);

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
                                Plain = plainData.Plain.Trim(),
                                IsActive = plainData.IsActive
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
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Ubisoft-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationError"), "Ubisoft"),
                        NotificationType.Error
                    ));

                    ResultLoad = LoadWishlists("Ubisoft", PluginUserDataPath, true);
                    if (ResultLoad != null)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                        SaveWishlist("Ubisoft", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Ubisoft", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId, string PluginUserDataPath)
        {
            return false;
        }
    }
}
