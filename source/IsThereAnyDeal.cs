using IsThereAnyDeal.Services;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using CommonPluginsShared.PlayniteExtended;
using Playnite.SDK.Events;

namespace IsThereAnyDeal
{
    public class IsThereAnyDeal : PluginExtended<IsThereAnyDealSettingsViewModel>
    {
        public override Guid Id { get; } = Guid.Parse("7d5cbee9-3c86-4389-ac7b-9abe3da4c9cd");

        public IsThereAnyDeal(IPlayniteAPI api) : base(api)
        {

        }

        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
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
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string MenuInExtensions = string.Empty;
            if (PluginSettings.Settings.MenuInExtensions)
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
                        var ViewExtension = new IsThereAnyDealView(this, PlayniteApi, this.GetPluginUserDataPath(), PluginSettings.Settings);
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
                        IsThereAnyDealApi.CheckNotifications(PlayniteApi, PluginSettings.Settings, this);
                    }
                },
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                    Description = resources.GetString("LOCItadUpdateDatas"),
                    Action = (mainMenuItem) =>
                    {
                        IsThereAnyDealApi.UpdateDatas(PlayniteApi, PluginSettings.Settings, this);
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


        #region Game event
        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {

        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {

        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {

        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {

        }
        #endregion


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {

        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {

        }


        #region Settings
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IsThereAnyDealSettingsView(PlayniteApi, PluginSettings.Settings, this.GetPluginUserDataPath());
        }
        #endregion
    }
}
