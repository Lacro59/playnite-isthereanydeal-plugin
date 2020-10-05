using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            PluginCommon.Localization.SetPluginLanguage(pluginFolder, api.ApplicationSettings.Language);
            // Add common in application ressource.
            PluginCommon.Common.Load(pluginFolder);

            // Check version
            if (settings.EnableCheckVersion)
            {
                CheckVersion cv = new CheckVersion();

                if (cv.Check("IsThereAnyDeal", pluginFolder))
                {
                    cv.ShowNotification(api, "IsThereAnyDeal - " + resources.GetString("LOCUpdaterWindowTitle"));
                }
            }
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

                        new IsThereAnyDealView(this, PlayniteApi, this.GetPluginUserDataPath(), settings).ShowDialog();
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

            Task taskNotifications = Task.Run(() => 
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                if (settings.EnableNotification)
                {
                    if (settings.EnableNotificationPercentage)
                    {
                        List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(this, PlayniteApi, settings, this.GetPluginUserDataPath(), true);
                        foreach (Wishlist wishlist in ListWishlist)
                        {
                            if (wishlist.GetNotification(settings.LimitNotification))
                            {
                                PlayniteApi.Notifications.Add(new NotificationMessage(
                                    $"IsThereAnyDeal-{wishlist.Plain}",
                                    string.Format(resources.GetString("LOCItadNotification"),
                                        wishlist.Name, wishlist.ItadBestPrice.PriceNew, wishlist.ItadBestPrice.CurrencySign, wishlist.ItadBestPrice.PriceCut),
                                    NotificationType.Info,
                                    () => new IsThereAnyDealView(this, PlayniteApi, this.GetPluginUserDataPath(), settings, wishlist.Plain).ShowDialog()
                                ));
                            }
                        }
                    }

                    if (settings.EnableNotificationPrice)
                    {
                        List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(this, PlayniteApi, settings, this.GetPluginUserDataPath(), true);
                        foreach (Wishlist wishlist in ListWishlist)
                        {
                            if (wishlist.GetNotificationPrice(settings.LimitNotificationPrice))
                            {
                                PlayniteApi.Notifications.Add(new NotificationMessage(
                                    $"IsThereAnyDeal-{wishlist.Plain}",
                                    string.Format(resources.GetString("LOCItadNotification"),
                                        wishlist.Name, wishlist.ItadBestPrice.PriceNew, wishlist.ItadBestPrice.CurrencySign, wishlist.ItadBestPrice.PriceCut),
                                    NotificationType.Info,
                                    () => new IsThereAnyDealView(this, PlayniteApi, this.GetPluginUserDataPath(), settings, wishlist.Plain).ShowDialog()
                                ));
                            }
                        }
                    }
                }

                if (settings.EnableNotificationGiveaways)
                {
                    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(PlayniteApi, this.GetPluginUserDataPath());
                    foreach (ItadGiveaway itadGiveaway in itadGiveaways)
                    {
                        if (!itadGiveaway.HasSeen)
                        {
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                $"IsThereAnyDeal-{itadGiveaway.Title}",
                                string.Format(resources.GetString("LOCItadNotificationGiveaway"), itadGiveaway.TitleAll, itadGiveaway.Count),
                                NotificationType.Info,
                                () => Process.Start(itadGiveaway.Link)
                            ));
                        }
                    }
                }
            });
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
