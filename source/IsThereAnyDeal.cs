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
using System.IO;
using System.Reflection;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsStores.Steam;

namespace IsThereAnyDeal
{
    public class IsThereAnyDeal : PluginExtended<IsThereAnyDealSettingsViewModel>
    {
        public override Guid Id { get; } = Guid.Parse("7d5cbee9-3c86-4389-ac7b-9abe3da4c9cd");

        internal TopPanelItem TopPanelItem { get; set; }
        internal ItadViewSidebar ItadViewSidebar { get; set; }

        public static SteamApi SteamApi { get; set; }


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
                    Assembly DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                TopPanelItem = new TopPanelItem()
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
                            CanBeResizable = false,
                            Width = 1180,
                            Height = 720
                        };

                        IsThereAnyDealView ViewExtension = new IsThereAnyDealView(this, PluginSettings.Settings);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCItad"), ViewExtension, windowOptions);
                        _ = windowExtension.ShowDialog();
                    },
                    Visible = PluginSettings.Settings.EnableIntegrationButtonHeader
                };

                ItadViewSidebar = new ItadViewSidebar(this);
            }
        }


        #region Theme integration
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelItem;
        }

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return null;
        }

        // Sidebar
        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return ItadViewSidebar;
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
                            CanBeResizable = false,
                            Width = 1180,
                            Height = 720
                        };
                        IsThereAnyDealView ViewExtension = new IsThereAnyDealView(this, PluginSettings.Settings);
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
                        _ = IsThereAnyDealApi.CheckNotifications(PluginSettings.Settings, this);
                    }
                },

                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCItad"),
                    Description = resources.GetString("LOCItadUpdateDatas"),
                    Action = (mainMenuItem) =>
                    {
                        ItadViewSidebar.ResetView();
                        IsThereAnyDealApi.UpdateDatas(PluginSettings.Settings, this);
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
            _ = IsThereAnyDealApi.CheckNotifications(PluginSettings.Settings, this);

            SteamApi = new SteamApi("IsThereAnyDeal");
            SteamApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            if (PluginSettings.Settings.EnableSteam)
            {
                _ = SteamApi.CurrentUser;
            }
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
            return new IsThereAnyDealSettingsView(PluginSettings.Settings, this);
        }
        #endregion
    }
}
