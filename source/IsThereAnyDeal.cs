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
using CommonPluginsStores.Epic;
using CommonPluginsStores.Gog;
using System.Windows.Automation;
using System.Threading.Tasks;
using System.Timers;

namespace IsThereAnyDeal
{
    public class IsThereAnyDeal : PluginExtended<IsThereAnyDealSettingsViewModel>
    {
        public override Guid Id { get; } = Guid.Parse("7d5cbee9-3c86-4389-ac7b-9abe3da4c9cd");

        public static SteamApi SteamApi { get; set; }
        public static EpicApi EpicApi { get; set; }
        public static GogApi GogApi { get; set; }

        internal TopPanelItem TopPanelItem { get; set; }
        internal ItadViewSidebar SidebarItem { get; set; }

        public IsThereAnyDeal(IPlayniteAPI api) : base(api)
        {
            string pluginCachePath = Path.Combine(PlaynitePaths.DataCachePath, "IsThereAnyDeal");
            HttpFileCachePlugin.CacheDirectory = pluginCachePath;
            FileSystem.CreateDirectory(pluginCachePath);

            // Manual dll load
            try
            {
                string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(pluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    Assembly DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }

            // Add Event for WindowBase for get the "WindowSettings".
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(WindowBase_LoadedEvent));

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                TopPanelItem = new ItadTopPanelItem(this);
                SidebarItem = new ItadViewSidebar(this);
            }

            // Timer
            if (PluginSettings.Settings.UpdateWishlist.EveryHours)
            {
                //Timer timerUpdateWishlist = new Timer(PluginSettings.Settings.UpdateWishlist.Hours * 3600000)
                Timer timerUpdateWishlist = new Timer(PluginSettings.Settings.UpdateWishlist.Hours * 60000)
                {
                    AutoReset = true
                };
                timerUpdateWishlist.Elapsed += (sender, e) => OnTimedUpdateWishlistEvent(sender, e);
                timerUpdateWishlist.Start();
            }
            if (PluginSettings.Settings.UpdatePrice.EveryHours)
            {
                //Timer timerUpdatePrice = new Timer(PluginSettings.Settings.UpdatePrice.Hours * 3600000)
                Timer timerUpdatePrice = new Timer(PluginSettings.Settings.UpdatePrice.Hours * 60000)
                {
                    AutoReset = true
                };
                timerUpdatePrice.Elapsed += (sender, e) => OnTimedUpdatePriceEvent(sender, e);
                timerUpdatePrice.Start();
            }
        }

        private void OnTimedUpdatePriceEvent(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(() =>
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                _ = isThereAnyDealApi.LoadWishlist(this, false, true);
                _ = IsThereAnyDealApi.CheckNotifications(this);
            });
        }

        private void OnTimedUpdateWishlistEvent(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(() =>
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                _ = isThereAnyDealApi.LoadWishlist(this, true, false);
                _ = IsThereAnyDealApi.CheckNotifications(this);
            });
        }


        #region Custom event
        private void WindowBase_LoadedEvent(object sender, System.EventArgs e)
        {
            string winIdProperty = string.Empty;
            try
            {
                winIdProperty = ((Window)sender).GetValue(AutomationProperties.AutomationIdProperty).ToString();

                if (winIdProperty == "WindowSettings" || winIdProperty == "WindowExtensions" || winIdProperty == "WindowLibraryIntegrations")
                {
                    SteamApi.ResetIsUserLoggedIn();
                    EpicApi.ResetIsUserLoggedIn();
                    GogApi.ResetIsUserLoggedIn();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {winIdProperty}", true, "IsThereAnyDeal");
            }
        }
        #endregion


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
            yield return SidebarItem;
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
                MenuSection = ResourceProvider.GetString("LOCItad"),
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
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCItad"),
                    Description = ResourceProvider.GetString("LOCItadPluginView"),
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
                        IsThereAnyDealView viewExtension = new IsThereAnyDealView(this);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCItad"), viewExtension, windowOptions);
                        windowExtension.ShowDialog();
                    }
                },

                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCItad"),
                    Description = ResourceProvider.GetString("LOCItadCheckNotifications"),
                    Action = (mainMenuItem) =>
                    {
                        _ = IsThereAnyDealApi.CheckNotifications(this);
                    }
                },

                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCItad"),
                    Description = ResourceProvider.GetString("LOCItadUpdateDatas"),
                    Action = (mainMenuItem) =>
                    {
                        SidebarItem.ResetView();
                        IsThereAnyDealApi.UpdateDatas(this);
                    }
                }
            };

#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCItad"),
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
            _ = PluginSettings.Settings.UpdateWishlist.OnStart || PluginSettings.Settings.UpdateWishlist.OnStart
                ? Task.Run(() =>
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    _ = isThereAnyDealApi.LoadWishlist(this, PluginSettings.Settings.UpdateWishlist.OnStart, PluginSettings.Settings.UpdateWishlist.OnStart);
                    _ = IsThereAnyDealApi.CheckNotifications(this);
                })
                : IsThereAnyDealApi.CheckNotifications(this);

            // StoreAPI intialization
            SteamApi = new SteamApi("IsThereAnyDeal", PlayniteTools.ExternalPlugin.IsThereAnyDeal);
            SteamApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            SteamApi.StoreSettings = PluginSettings.Settings.SteamStoreSettings;
            if (PluginSettings.Settings.EnableSteam)
            {
                _ = SteamApi.CurrentAccountInfos;
            }

            EpicApi = new EpicApi("IsThereAnyDeal", PlayniteTools.ExternalPlugin.IsThereAnyDeal);
            EpicApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            EpicApi.StoreSettings = PluginSettings.Settings.EpicStoreSettings;
            if (PluginSettings.Settings.EnableEpic)
            {
                _ = EpicApi.CurrentAccountInfos;
            }

            GogApi = new GogApi("IsThereAnyDeal", PlayniteTools.ExternalPlugin.IsThereAnyDeal);
            GogApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            GogApi.StoreSettings = PluginSettings.Settings.GogStoreSettings;
            if (PluginSettings.Settings.EnableGog)
            {
                _ = GogApi.CurrentAccountInfos;
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
