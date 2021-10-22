using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IsThereAnyDeal.Clients
{
    class XboxWishlist : GenericWishlist
    {
        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("Xbox", PluginUserDataPath);
            if (ResultLoad != null && CacheOnly)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("Xbox", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            logger.Info($"IsThereAnyDeal - Load from web for Xbox");

            // Get wishlist
            string url = settings.XboxLink;
            string baseUrl = "https://www.microsoft.com";

            if (url.IsNullOrEmpty())
            {
                logger.Error($"IsThereAnyDeal - No Xbox wish list link");

                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"IsThereAnyDeal-Xbox-Error",
                    "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationErrorXboxNoLink"), "Xbox"),
                    NotificationType.Error
                ));

                ResultLoad = LoadWishlists("Xbox", PluginUserDataPath, true);
                if (ResultLoad != null && CacheOnly)
                {
                    ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                    SaveWishlist("Xbox", PluginUserDataPath, ResultLoad);
                    return ResultLoad;
                }
                return Result;
            }

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            view.NavigateAndWait(url);
            string ResultWeb = view.GetPageSource();

            if (!ResultWeb.IsNullOrEmpty())
            {
                try
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument HtmlRequirement = parser.Parse(ResultWeb);

                    foreach (var SearchElement in HtmlRequirement.QuerySelectorAll("li.product-wishlist-item"))
                    {
                        string StoreId = string.Empty;
                        string StoreUrl = string.Empty;
                        string Name = string.Empty;
                        DateTime ReleaseDate = default(DateTime);
                        string Capsule = string.Empty;

                        try
                        {
                            StoreId = SearchElement.GetAttribute("data-product-id");
                            Capsule = SearchElement.QuerySelector("img.c-image").GetAttribute("data-src");
                            Name = SearchElement.QuerySelector("h3.c-heading").InnerHtml.Trim();
                            StoreUrl = SearchElement.QuerySelector("a.c-button").GetAttribute("href");

                            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                            PlainData plainData = isThereAnyDealApi.GetPlain(Name);

                            var tempShopColor = settings.Stores.Find(x => x.Id.ToLower().IndexOf("microsoft") > -1);

                            Result.Add(new Wishlist
                            {
                                StoreId = StoreId.Trim(),
                                StoreName = "Microsoft Store",
                                ShopColor = (tempShopColor == null) ? string.Empty : tempShopColor.Color,
                                StoreUrl = baseUrl + StoreUrl.Trim(),
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
                            Common.LogError(ex, true, $"Error in parse Microsoft Store wishlist - {Name}");
                            logger.Warn($"IsThereAnyDeal - Error in parse Microsoft Store wishlist - {Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true, "Error in parse Xbox wishlist");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"IsThereAnyDeal-Xbox-Error",
                        "IsThereAnyDeal\r\n" + string.Format(resources.GetString("LOCItadNotificationError"), "Xbox"),
                        NotificationType.Error
                    ));

                    ResultLoad = LoadWishlists("Xbox", PluginUserDataPath, true);
                    if (ResultLoad != null && CacheOnly)
                    {
                        ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                        SaveWishlist("Xbox", PluginUserDataPath, ResultLoad);
                        return ResultLoad;
                    }
                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("Xbox", PluginUserDataPath, Result);
            return Result;
        }

        public bool RemoveWishlist(string StoreId, string PluginUserDataPath)
        {
            return false;
        }
    }
}
