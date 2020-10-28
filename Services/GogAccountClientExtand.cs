using Playnite.SDK;
using PluginCommon.PlayniteResources.PluginLibrary.Services.GogLibrary;

namespace IsThereAnyDeal.Services
{
    public class GogAccountClientExtand : GogAccountClient
    {
        public GogAccountClientExtand(IWebView webView) : base(webView)
        {
        }

        public string GetWishList()
        {
            string url = string.Format(@"https://embed.gog.com/user/wishlist.json");
            webView.NavigateAndWait(url);
            return webView.GetPageText();
        }

        public bool RemoveWishList(string StoreId)
        {
            string url = string.Format(@"https://embed.gog.com/user/wishlist/remove/{0}", StoreId);
            webView.NavigateAndWait(url);
            return webView.GetPageSource().ToLower().IndexOf("unable to remove product from wishlist") == -1;
        }
    }
}
