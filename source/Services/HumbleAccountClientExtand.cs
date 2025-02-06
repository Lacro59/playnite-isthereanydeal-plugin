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

        public bool RemoveWishList(string storeId)
        {
            Common.LogDebug(true, $"Humble.RemoveWishList({storeId}) - {webView.GetPageSource()}");
            string url = string.Format(@"https://www.humblebundle.com/wishlist/remove/{0}", storeId);
            webView.NavigateAndWait(url);
            return true;
        }
    }
}
