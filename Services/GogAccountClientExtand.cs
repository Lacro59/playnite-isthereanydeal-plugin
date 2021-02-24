using CommonPluginsPlaynite.PluginLibrary.GogLibrary.Models;
using CommonPluginsPlaynite.PluginLibrary.Services.GogLibrary;
using CommonPluginsShared;
using Playnite.SDK;
using System;

namespace IsThereAnyDeal.Services
{
    public class GogAccountClientExtand : GogAccountClient
    {
        public GogAccountClientExtand(IWebView webView) : base(webView)
        {
        }

        public string GetWishList()
        {
            try
            {
                string url = string.Format(@"https://embed.gog.com/user/wishlist.json");
                webView.NavigateAndWait(url);
                return webView.GetPageText();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ISThereAnyDeal");
                return string.Empty;
            }
        }

        public string GetWishListWithoutAPI()
        {
            try
            {
                AccountBasicRespose AccountInfo = GetAccountInfo();

                string url = string.Format(@"https://www.gog.com/u/{0}/wishlist", AccountInfo.username);
                webView.NavigateAndWait(url);
                return webView.GetPageSource();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ISThereAnyDeal");
                return string.Empty;
            }
        }

        public bool RemoveWishList(string StoreId)
        {
            string url = string.Format(@"https://embed.gog.com/user/wishlist/remove/{0}", StoreId);
            webView.NavigateAndWait(url);
            return webView.GetPageSource().ToLower().IndexOf("unable to remove product from wishlist") == -1;
        }
    }
}
