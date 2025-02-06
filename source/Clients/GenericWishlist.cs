using IsThereAnyDeal.Models;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using CommonPlayniteShared.Common;
using Playnite.SDK.Data;

namespace IsThereAnyDeal.Services
{
    public abstract class GenericWishlist
    {
        internal static ILogger Logger => LogManager.GetLogger();

        internal IsThereAnyDeal Plugin { get; set; }
        internal IsThereAnyDealSettings Settings { get; set; }
        internal static string PluginUserDataPath { get; set; }

        internal static string ClientName { get; set; }
        internal static PlayniteTools.ExternalPlugin ExternalPlugin { get; set; }


        public GenericWishlist(IsThereAnyDeal plugin, string clientName)
        {
            Plugin = plugin;
            Settings = plugin.PluginSettings.Settings;
            PluginUserDataPath = plugin.GetPluginUserDataPath();
            ClientName = clientName;
        }

        /// <summary>
        /// Load local data with max 1 day
        /// </summary>
        /// <param name="cacheOnly">Force load for expired data</param>
        /// <param name="forcePrice">Force load price data data</param>
        /// <returns></returns>
        internal List<Wishlist> LoadWishlists(bool cacheOnly = false, bool forcePrice = false)
        {
            try
            {
                string DirPath = Path.Combine(PluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{ClientName}.json");

                FileSystem.CreateDirectory(DirPath, false);

                if (!File.Exists(FilePath) || (File.GetLastWriteTime(FilePath).AddDays(1) < DateTime.Now && !cacheOnly))
                {
                    return null;
                }

                Logger.Info($"Load wishlists from local for {ClientName}");
                if (Serialization.TryFromJsonFile(FilePath, out List<Wishlist> wishlists))
                {
                    if (forcePrice)
                    {
                        wishlists = SetCurrentPrice(wishlists, forcePrice);
                        SaveWishlist(wishlists);
                    }
                    return wishlists;
                }
                else
                {
                    Logger.Error($"Failed to load wishlists from local for {ClientName}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error for load {ClientName} wishlists", true, "IsThereAnyDeal");
            }

            return null;
        }

        /// <summary>
        /// Saved data
        /// </summary>
        /// <param name="wishlists"></param>
        public void SaveWishlist(List<Wishlist> wishlists)
        {
            try
            {
                string DirPath = Path.Combine(PluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{ClientName}.json");

                FileSystem.WriteStringToFile(FilePath, Serialization.ToJson(wishlists));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error for save {ClientName} wishlists", true, "IsThereAnyDeal");
            }
        }

        /// <summary>
        /// Download data from store
        /// </summary>
        /// <param name="CacheOnly"></param>
        /// <param name="ForcePrice"></param>
        /// <returns></returns>
        public List<Wishlist> GetWishlist(bool cacheOnly = false, bool forcePrice = false)
        {
            List<Wishlist> cachedData = LoadWishlists(cacheOnly, forcePrice);
            try
            {
                return cacheOnly ? cachedData : GetStoreWishlist(cachedData);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, $"Error on GetStoreWishlist({ClientName})");
                return cachedData;
            }
        }
        internal abstract List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData);

        public abstract bool RemoveWishlist(string storeId);

        public List<Wishlist> SetCurrentPrice(List<Wishlist> wishlists, bool force)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            return isThereAnyDealApi.GetCurrentPrice(wishlists, Settings, force).GetAwaiter().GetResult();
        }

        internal string GetShopColor()
        {
            return Settings.Stores?.Find(x => x.Title.ToLower().IndexOf(ClientName) > -1)?.Color ?? string.Empty;
        }
    }
}
