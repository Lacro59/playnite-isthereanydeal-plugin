using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Caching;
using CommonPluginsShared.PlayniteExtended;
using CommonPluginsStores.Epic;
using CommonPluginsStores.Gog;
using CommonPluginsStores.Steam;
using IsThereAnyDeal.Services;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

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


        public IsThereAnyDeal(IPlayniteAPI api) : base(api, "IsThereAnyDeal")
        {
            Logger.Info("[Startup] ctor entered");

            string pluginCachePath = Path.Combine(PlaynitePaths.DataCachePath, PluginName);
            HttpFileCacheService.CacheDirectory = pluginCachePath;
            FileSystem.CreateDirectory(pluginCachePath);
            Logger.Info($"[Startup] cache ready path={pluginCachePath}");

            // Add Event for WindowBase for get the "WindowSettings".
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(WindowBase_LoadedEvent));
            Logger.Info("[Startup] window_loaded handler registered");

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                TopPanelItem = new ItadTopPanelItem(this);
                SidebarItem = new ItadViewSidebar(this);
                Logger.Info("[Startup] desktop chrome ready topPanel=true sidebar=true");
            }
            else
            {
                Logger.Info("[Startup] desktop chrome skipped mode=fullscreen");
            }

            // Timer
            if (PluginSettingsViewModel.Settings.UpdateWishlist.EveryHours)
            {
                Timer timerUpdateWishlist = new Timer(PluginSettingsViewModel.Settings.UpdateWishlist.Hours * 3600000)
                {
                    AutoReset = true
                };
                timerUpdateWishlist.Elapsed += (sender, e) => OnTimedUpdateWishlistEvent(sender, e);
                timerUpdateWishlist.Start();
                Logger.Info($"[Startup] timer started kind=wishlist hours={PluginSettingsViewModel.Settings.UpdateWishlist.Hours}");
            }
            if (PluginSettingsViewModel.Settings.UpdatePrice.EveryHours)
            {
                Timer timerUpdatePrice = new Timer(PluginSettingsViewModel.Settings.UpdatePrice.Hours * 3600000)
                {
                    AutoReset = true
                };
                timerUpdatePrice.Elapsed += (sender, e) => OnTimedUpdatePriceEvent(sender, e);
                timerUpdatePrice.Start();
                Logger.Info($"[Startup] timer started kind=price hours={PluginSettingsViewModel.Settings.UpdatePrice.Hours}");
            }

            Logger.Info("[Startup] ctor completed");
        }

        private void OnTimedUpdatePriceEvent(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Logger.Info("[Timer][price] starting cacheOnly=true forcePrice=true");
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    _ = isThereAnyDealApi.LoadWishlist(this, true, true);
                    _ = IsThereAnyDealApi.CheckNotifications(this);
                    Logger.Info("[Timer][price] completed");
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on timed price update", true, PluginName);
                }
            });
        }

        private void OnTimedUpdateWishlistEvent(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Logger.Info("[Timer][wishlist] starting cacheOnly=false forcePrice=false");
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    _ = isThereAnyDealApi.LoadWishlist(this, false, false);
                    _ = IsThereAnyDealApi.CheckNotifications(this);
                    Logger.Info("[Timer][wishlist] completed");
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on timed wishlist update", true, PluginName);
                }
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
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {winIdProperty}", true, PluginName);
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
            if (PluginSettingsViewModel.Settings.MenuInExtensions)
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
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            CanBeResizable = true,
                            Width = 1180,
                            Height = 720,
                            MinWidth = 900,
                            MinHeight = 500,
                            WindowPersistenceKey = "IsThereAnyDeal.MainView"
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
            Logger.Info("[Startup] OnApplicationStarted entered");

            _ = Task.Run(async () =>
            {
                await PluginSettingsViewModel.MigrateLegacyStoresIdsAsync().ConfigureAwait(false);
            });

            bool updateWishlistOnStart = PluginSettingsViewModel.Settings.UpdateWishlist.OnStart;
            bool updatePriceOnStart = PluginSettingsViewModel.Settings.UpdatePrice.OnStart;

            if (updateWishlistOnStart || updatePriceOnStart)
            {
                // Wishlist OnStart: stores + prices (false, true). Price OnStart only: prices from cache (true, true).
                bool cacheOnly = !updateWishlistOnStart;
                bool forcePrice = true;

                _ = Task.Run(() =>
                {
                    try
                    {
                        Logger.Info($"[Startup][update] starting cacheOnly={cacheOnly} forcePrice={forcePrice} wishlistOnStart={updateWishlistOnStart} priceOnStart={updatePriceOnStart}");
                        IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                        _ = isThereAnyDealApi.LoadWishlist(this, cacheOnly, forcePrice);
                        _ = IsThereAnyDealApi.CheckNotifications(this);
                        Logger.Info("[Startup][update] completed");
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, "Error on startup wishlist/price update", true, PluginName);
                    }
                });
            }
            else
            {
                Logger.Info("[Startup][update] skipped wishlistOnStart=false priceOnStart=false");
                _ = IsThereAnyDealApi.CheckNotifications(this);
            }


            // StoreAPI Initialization
            Task.Run(async () =>
            {
                bool steamLoadUser = PluginSettingsViewModel.Settings.PluginState.SteamIsEnabled && PluginSettingsViewModel.Settings.EnableSteam;
                bool epicLoadUser = PluginSettingsViewModel.Settings.PluginState.EpicIsEnabled && PluginSettingsViewModel.Settings.EnableEpic;
                bool gogLoadUser = PluginSettingsViewModel.Settings.PluginState.GogIsEnabled && PluginSettingsViewModel.Settings.EnableGog;

                Logger.Info($"[Store][init] batch starting steamLoadUser={steamLoadUser} epicLoadUser={epicLoadUser} gogLoadUser={gogLoadUser}");

                var tasks = new[]
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            Logger.Info($"[Store][init] steam starting enable={PluginSettingsViewModel.Settings.EnableSteam} libraryEnabled={PluginSettingsViewModel.Settings.PluginState.SteamIsEnabled}");
                            SteamApi = new SteamApi(PluginName, PlayniteTools.ExternalPlugin.IsThereAnyDeal);
                            SteamApi.Initialization(PluginSettingsViewModel.Settings.SteamStoreSettings, steamLoadUser);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[Store][init] steam failed");
                        }
                    }),
                    Task.Run(() =>
                    {
                        try
                        {
                            Logger.Info($"[Store][init] epic starting enable={PluginSettingsViewModel.Settings.EnableEpic} libraryEnabled={PluginSettingsViewModel.Settings.PluginState.EpicIsEnabled}");
                            EpicApi = new EpicApi(PluginName, PlayniteTools.ExternalPlugin.IsThereAnyDeal);
                            EpicApi.Initialization(PluginSettingsViewModel.Settings.EpicStoreSettings, epicLoadUser);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[Store][init] epic failed");
                        }
                    }),
                    Task.Run(() =>
                    {
                        try
                        {
                            Logger.Info($"[Store][init] gog starting enable={PluginSettingsViewModel.Settings.EnableGog} libraryEnabled={PluginSettingsViewModel.Settings.PluginState.GogIsEnabled}");
                            GogApi = new GogApi(PluginName, PlayniteTools.ExternalPlugin.IsThereAnyDeal);
                            GogApi.Initialization(PluginSettingsViewModel.Settings.GogStoreSettings, gogLoadUser);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[Store][init] gog failed");
                        }
                    })
                };

                await Task.WhenAll(tasks);
                Logger.Info("[Store][init] batch completed");
            });
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
            return PluginSettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IsThereAnyDealSettingsView();
        }

        #endregion
    }
}
