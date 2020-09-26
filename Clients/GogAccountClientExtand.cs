using Playnite.SDK;

namespace IsThereAnyDeal.Services
{
    public class GogAccountClientExtand
    {
        private ILogger logger = LogManager.GetLogger();
        private IWebView webView;

        public GogAccountClientExtand(IWebView webView)
        {
            this.webView = webView;
        }

        public string GetWishList()
        {
            string url = string.Format(@"https://embed.gog.com/user/wishlist.json");
            webView.NavigateAndWait(url);
            return webView.GetPageText();
        }

        public bool GetIsUserLoggedIn()
        {
            webView.NavigateAndWait(@"https://www.gog.com/account/getFilteredProducts?hiddenFlag=0&mediaType=1&page=1&sortBy=title");
            return webView.GetCurrentAddress().Contains("getFilteredProducts");
        }
    }
}
