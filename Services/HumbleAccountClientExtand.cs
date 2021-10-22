using CommonPlayniteShared.PluginLibrary.HumbleLibrary.Services;
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
            string url = string.Format(@"https://www.humblebundle.com/wishlist/remove/{0}", StoreId);
            webView.NavigateAndWait(url);
#if DEBUG
            logger.Debug($"IsThereAnyDeal - Humble.RemoveWishList({StoreId}) - {webView.GetPageSource()}");
#endif
            return true;
        }
    }
}
