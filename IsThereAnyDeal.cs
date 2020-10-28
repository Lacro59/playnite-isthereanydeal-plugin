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
using System.Windows;
using IsThereAnyDeal.Clients;

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

        // To add new game menu items override GetGameMenuItems
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>
            {
            };

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCItad"),
                Description = "Test",
                Action = (mainMenuItem) => { }
            });
#endif

            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override List<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string MenuInExtensions = string.Empty;
            if (settings.MenuInExtensions)
            {
                MenuInExtensions = "@";
            }

            List<MainMenuItem> mainMenuItems = new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                    Description = resources.GetString("LOCItadPluginView"),
                    Action = (mainMenuItem) =>
                    {
                        var ViewExtension = new IsThereAnyDealView(this, PlayniteApi, this.GetPluginUserDataPath(), settings);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension);
                        windowExtension.ShowDialog();
                    }
                },
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                    Description = resources.GetString("LOCItadCheckNotifications"),
                    Action = (mainMenuItem) =>
                    {
                        IsThereAnyDealApi.CheckNotifications(PlayniteApi, settings, this);
                    }
                },
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                    Description = resources.GetString("LOCItadUpdateDatas"),
                    Action = (mainMenuItem) =>
                    {
                        IsThereAnyDealApi.UpdateDatas(PlayniteApi, settings, this);
                    }
                }
            };

#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                Description = "Test",
                Action = (mainMenuItem) => { }
            });
#endif

            return mainMenuItems;
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

            IsThereAnyDealApi.CheckNotifications(PlayniteApi, settings, this);
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
