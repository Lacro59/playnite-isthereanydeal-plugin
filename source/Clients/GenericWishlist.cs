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
    public class GenericWishlist
    {
        internal static readonly ILogger logger = LogManager.GetLogger();
        internal static readonly IResourceProvider resourceProvider = new ResourceProvider();

        internal IsThereAnyDeal Plugin { get; set; }

        protected static IWebView _WebViewOffscreen;
        internal static IWebView WebViewOffscreen
        {
            get
            {
                if (_WebViewOffscreen == null)
                {
                    _WebViewOffscreen = API.Instance.WebViews.CreateOffscreenView();
                }
                return _WebViewOffscreen;
            }

            set => _WebViewOffscreen = value;
        }

        internal static string PluginUserDataPath { get; set; }


        public GenericWishlist(IsThereAnyDeal plugin)
        {
            Plugin = plugin;
        }

        public List<Wishlist> LoadWishlists(string clientName, string pluginUserDataPath, bool force = false)
        {
            GenericWishlist.PluginUserDataPath = pluginUserDataPath;

            try
            {
                string DirPath = Path.Combine(pluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{clientName}.json");

                FileSystem.CreateDirectory(DirPath, false);

                if (!File.Exists(FilePath))
                {
                    return null;
                }
                else if (File.GetLastWriteTime(FilePath).AddDays(1) < DateTime.Now && !force)
                {
                    return null;
                }

                logger.Info($"Load wishlist from local for {clientName}");
                if (Serialization.TryFromJsonFile(FilePath, out List<Wishlist> wishlists))
                {
                    return wishlists;
                }
                else
                {
                    logger.Error($"Failed to load wishlist from local for {clientName}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error for load {clientName} wishlist", true, "IsThereAnyDeal");
            }

            return null;
        }

        public void SaveWishlist(string clientName, string pluginUserDataPath, List<Wishlist> wishlists)
        {
            try
            {
                string DirPath = Path.Combine(pluginUserDataPath, "IsThereAnyDeal");
                string FilePath = Path.Combine(DirPath, $"{clientName}.json");

                FileSystem.WriteStringToFile(FilePath, Serialization.ToJson(wishlists));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error for save {clientName} wishlist", true, "IsThereAnyDeal");
            }
        }

        public List<Wishlist> SetCurrentPrice(List<Wishlist> wishlists, IsThereAnyDealSettings settings)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            return isThereAnyDealApi.GetCurrentPrice(wishlists, settings).GetAwaiter().GetResult();
        }
    }
}
