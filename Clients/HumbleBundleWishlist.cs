using Playnite.SDK;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Playnite.Common.Web;
using System.Net;
using PluginCommon;
using Newtonsoft.Json.Linq;

namespace IsThereAnyDeal.Services
{
    class HumbleBundleWishlist : GenericWishlist
    {
       public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string HumbleBundleId, string PluginUserDataPath, IsThereAnyDealSettings settings, bool CacheOnly = false, bool Force = false)
        {
            List<Wishlist> Result = new List<Wishlist>();

            List<Wishlist> ResultLoad = LoadWishlists("HumbleBundle", PluginUserDataPath);
            if (ResultLoad != null && !Force)
            {
                ResultLoad = SetCurrentPrice(ResultLoad, settings, PlayniteApi);
                SaveWishlist("HumbleBundle", PluginUserDataPath, ResultLoad);
                return ResultLoad;
            }

            if (CacheOnly)
            {
                return Result;
            }

            if (HumbleBundleId.IsNullOrEmpty())
            {
                return Result;
            }

            string ResultWeb = "";
            string url = string.Format(@"https://www.humblebundle.com/store/wishlist/{0}", HumbleBundleId);

            try
            {
                ResultWeb = HttpDownloader.DownloadString(url, Encoding.UTF8);

                if (ResultWeb != "")
                {
                    int startSub = ResultWeb.IndexOf("<script id=\"storefront-webpack-json-data\" type=\"application/json\">");
                    ResultWeb = ResultWeb.Substring(startSub, (ResultWeb.Length - startSub));

                    int endSub = ResultWeb.IndexOf("</script>");
                    ResultWeb = ResultWeb.Substring(0, endSub);

                    ResultWeb = ResultWeb.Replace("<script id=\"storefront-webpack-json-data\" type=\"application/json\">", "");

                    JObject dataObj = JObject.Parse(ResultWeb);

                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    foreach (JObject gameWish in dataObj["products_json"])
                    {
                        int StoreId = 0;
                        string StoreUrl = "https://www.humblebundle.com/store/" + gameWish["human_url"];
                        string Name = (string)gameWish["human_name"];
                        DateTime ReleaseDate = default(DateTime);
                        string Capsule = (string)gameWish["standard_carousel_image"];
                        
                        Result.Add(new Wishlist
                        {
                            StoreId = StoreId,
                            StoreName = "Humble",
                            StoreUrl = StoreUrl,
                            Name = WebUtility.HtmlDecode(Name),
                            SourceId = SourceId,
                            ReleaseDate = ReleaseDate.ToUniversalTime(),
                            Capsule = Capsule,
                            Plain = isThereAnyDealApi.GetPlain(Name)
                        });
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    Common.LogError(ex, "IsThereAnyDeal", "Error in download HumbleBundle wishlist");
                    return Result;
                }
            }

            Result = SetCurrentPrice(Result, settings, PlayniteApi);
            SaveWishlist("HumbleBundle", PluginUserDataPath, Result);
            return Result;
        }
    }
}
