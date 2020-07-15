using Playnite.SDK;
using IsThereAnyDeal.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace IsThereAnyDeal.Clients
{
    class EpicWishlist
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();


        public EpicWishlist()
        {
            



        }


        internal async Task<string> DonwloadStringData()
        {
            using (var client = new HttpClient())
            {
                string token = "";
                client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                await client.GetStringAsync("https://account-public-service-prod03.ol.epicgames.com/account/api/public/account/f1d782253c2e438a94d9f9fa5acf6ab5").ConfigureAwait(false);

                string result = await client.GetStringAsync("https://www.epicgames.com/store/fr/wishlist").ConfigureAwait(false);

                return result;
            }
        }



        public List<Wishlist> GetWishlist(IPlayniteAPI PlayniteApi, Guid SourceId, string PluginUserDataPath)
        {
            List<Wishlist> Result = new List<Wishlist>();


            var view = PlayniteApi.WebViews.CreateOffscreenView();


            return Result;
        }



    }
}
