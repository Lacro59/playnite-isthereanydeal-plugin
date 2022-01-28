using CommonPlayniteShared.PluginLibrary.HumbleLibrary.Services;
using CommonPluginsShared;
using Playnite.SDK;

namespace IsThereAnyDeal.Services
{
    public class HumbleAccountClientExtand : HumbleAccountClient
    {
        public HumbleAccountClientExtand(IWebView webView) : base(webView)
        {
        }

        public bool RemoveWishList(string StoreId)
        {
            Common.LogDebug(true, $"Humble.RemoveWishList({StoreId}) - {webView.GetPageSource()}");
            string url = string.Format(@"https://www.humblebundle.com/wishlist/remove/{0}", StoreId);
            webView.NavigateAndWait(url);
            return true;
        }
    }
}
