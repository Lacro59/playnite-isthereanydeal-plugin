using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Playnite.SDK;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.IO;

namespace IsThereAnyDeal.Clients
{
    public class GenericWishlist
    {
        private static readonly ILogger logger = LogManager.GetLogger();


        public List<Wishlist> LoadWishlists(string clientName, string PluginUserDataPath)
        {
            try
            {
                if (!Directory.Exists(PluginUserDataPath + "\\IsThereAnyDeal"))
                {
                    Directory.CreateDirectory(PluginUserDataPath + "\\IsThereAnyDeal");
                    return null;
                }

                if (!File.Exists(PluginUserDataPath + $"\\IsThereAnyDeal\\{clientName}.json"))
                {
                    return null;
                }

                string fileData = File.ReadAllText(PluginUserDataPath + $"\\IsThereAnyDeal\\{clientName}.json");
                logger.Info($"IsThereAnyDeal - Load from local for {clientName}");
                return JsonConvert.DeserializeObject<List<Wishlist>>(fileData);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"Error for save {clientName} wishlist");
                return null;
            }
        }

        public void SaveWishlist(string clientName, string PluginUserDataPath, List<Wishlist> Wishlist)
        {
            try
            {
                if (!Directory.Exists(PluginUserDataPath + "\\IsThereAnyDeal"))
                {
                    Directory.CreateDirectory(PluginUserDataPath + "\\IsThereAnyDeal");
                }

                File.WriteAllText(PluginUserDataPath + $"\\IsThereAnyDeal\\{clientName}.json", JsonConvert.SerializeObject(Wishlist));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"Error for save {clientName} wishlist");
            }
        }

        public List<Wishlist> SetCurrentPrice(List<Wishlist> ListWishlist, IsThereAnyDealSettings settings)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            return isThereAnyDealApi.GetCurrentPrice(ListWishlist, settings);
        }

    }
}
