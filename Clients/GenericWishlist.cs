using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using CommonPluginsPlaynite.Common;

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
                string DirPath = Path.Combine(PluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{clientName}.json");

                if (!Directory.Exists(DirPath))
                {
                    Directory.CreateDirectory(DirPath);
                    return null;
                }

                if (!File.Exists(FilePath))
                {
                    return null;
                }
                else if (File.GetLastWriteTime(FilePath).AddDays(1) < DateTime.Now)
                {
                    if (!force)
                    {
                        return null;
                    }
                }

                logger.Info($"IsThereAnyDeal - Load wishlist from local for {clientName}");

                string FileData = FileSystem.ReadFileAsStringSafe(FilePath);
                return JsonConvert.DeserializeObject<List<Wishlist>>(FileData);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IsThereAnyDeal", $"Error for load {clientName} wishlist");
                return null;
            }
        }

        public void SaveWishlist(string clientName, string PluginUserDataPath, List<Wishlist> Wishlist)
        {
            try
            {
                string DirPath = Path.Combine(PluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{clientName}.json");

                FileSystem.WriteStringToFile(FilePath, JsonConvert.SerializeObject(Wishlist));
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
