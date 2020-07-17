using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Views;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace IsThereAnyDeal
{
    public class IsThereAnyDeal : Plugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IsThereAnyDealSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("7d5cbee9-3c86-4389-ac7b-9abe3da4c9cd");

        public IsThereAnyDeal(IPlayniteAPI api) : base(api)
        {
            settings = new IsThereAnyDealSettings(this);

            // Get plugin's location 
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Add plugin localization in application ressource.
            PluginCommon.Localization.SetPluginLanguage(pluginFolder, api.Paths.ConfigurationPath);
            // Add common in application ressource.
            PluginCommon.Common.Load(pluginFolder);
        }

        public override IEnumerable<ExtensionFunction> GetFunctions()
        {
            return new List<ExtensionFunction>
            {
                new ExtensionFunction(
                    "IsThereAnyDeal",
                    () =>
                    {
                        // Add code to be execute when user invokes this menu entry.

                        new IsThereAnyDealView(PlayniteApi, this.GetPluginUserDataPath(), settings).ShowDialog();
                    })
            };
        }

        public override void OnGameInstalled(Game game)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(Game game)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(Game game)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(Game game, long elapsedSeconds)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(Game game)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted()
        {
            // Add code to be executed when Playnite is initialized.

            if (settings.EnableNotification)
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(PlayniteApi, settings, this.GetPluginUserDataPath(), true);

                foreach (Wishlist wishlist in ListWishlist)
                {
                    //logger.Info($"IsTherAnyDeal - CheckNotification({wishlist.Name})");
                    if (wishlist.GetNotification(settings.LimitNotification))
                    {
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-{wishlist.Plain}",
                            string.Format(resources.GetString("LOCItadNotification"), 
                                wishlist.Name, wishlist.ItadBestPrice.price_new, wishlist.ItadBestPrice.currency_sign, wishlist.ItadBestPrice.price_cut),
                            NotificationType.Info,
                            () => new IsThereAnyDealView(PlayniteApi, this.GetPluginUserDataPath(), settings, wishlist.Plain).ShowDialog()
                        ));
                    }
                }

            }
        }

        public override void OnApplicationStopped()
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated()
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IsThereAnyDealSettingsView(settings);
        }
    }
}
