using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;

namespace IsThereAnyDeal.Services
{
    public class GenericWishlist
    {
        internal static readonly ILogger logger = LogManager.GetLogger();
        internal static readonly IResourceProvider resources = new ResourceProvider();


        public List<Wishlist> LoadWishlists(string clientName, string PluginUserDataPath, bool force = false)
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
                else if (File.GetLastWriteTime(PluginUserDataPath + $"\\IsThereAnyDeal\\{clientName}.json").AddDays(1) < DateTime.Now)
                {
                    if (!force)
                    {
                        return null;
                    }
                }

                logger.Info($"IsThereAnyDeal - Load from local for {clientName}");

                string fileData = File.ReadAllText(PluginUserDataPath + $"\\IsThereAnyDeal\\{clientName}.json");
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

        public List<Wishlist> SetCurrentPrice(List<Wishlist> ListWishlist, IsThereAnyDealSettings settings, IPlayniteAPI PlayniteApi)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            return isThereAnyDealApi.GetCurrentPrice(ListWishlist, settings, PlayniteApi);
        }
    }
}
