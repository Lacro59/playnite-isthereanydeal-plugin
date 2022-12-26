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
using System.Windows.Media;
using CommonPluginsShared.Controls;
using System.IO;
using System.Reflection;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;

namespace IsThereAnyDeal
{
    public class IsThereAnyDeal : PluginExtended<IsThereAnyDealSettingsViewModel>
    {
        public override Guid Id { get; } = Guid.Parse("7d5cbee9-3c86-4389-ac7b-9abe3da4c9cd");

        internal TopPanelItem topPanelItem { get; set; }
        internal ItadViewSidebar itadViewSidebar { get; set; }


        public IsThereAnyDeal(IPlayniteAPI api) : base(api)
        {
            string PluginCachePath = Path.Combine(PlaynitePaths.DataCachePath, "IsThereAnyDeal");
            HttpFileCachePlugin.CacheDirectory = PluginCachePath;
            FileSystem.CreateDirectory(PluginCachePath);

            // Manual dll load
            try
            {
                string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(PluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    var DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
            }

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                topPanelItem = new TopPanelItem()
                {
                    Icon = new TextBlock
                    {
                        Text = "\uea63",
                        FontSize = 22,
                        FontFamily = resources.GetResource("CommonFont") as FontFamily
                    },
                    Title = resources.GetString("LOCItad"),
                    Activated = () =>
                    {
                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = false,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };

                        IsThereAnyDealView ViewExtension = new IsThereAnyDealView(this, this.GetPluginUserDataPath(), PluginSettings.Settings);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension, windowOptions);
                        windowExtension.ShowDialog();
                    },
                    Visible = PluginSettings.Settings.EnableIntegrationButtonHeader
                };

                itadViewSidebar = new ItadViewSidebar(this);
            }
        }


        #region Theme integration
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return topPanelItem;
        }

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return null;
        }

        // Sidebar
        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return new List<SidebarItem>
            {
                itadViewSidebar
            };
        }
        #endregion


        #region Menus
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
                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = false,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };
                        IsThereAnyDealView ViewExtension = new IsThereAnyDealView(this, this.GetPluginUserDataPath(), PluginSettings.Settings);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension, windowOptions);
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
                        itadViewSidebar.ResetView();
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
        #endregion


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
            IsThereAnyDealApi.CheckNotifications(PlayniteApi, PluginSettings.Settings, this);
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
