using CommonPluginsControls.Stores;
using CommonPluginsControls.Stores.Models;
using CommonPluginsShared;
using CommonPluginsStores.Steam;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Models.ApiWebsite;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Settings view: wishlist options plus Libraries tab via <see cref="StoresSettingsView"/> (Steam / Epic / Gog).
    /// </summary>
    public partial class IsThereAnyDealSettingsView : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private IsThereAnyDealSettings Settings => GetEditingSettings();
        private IsThereAnyDeal Plugin => (DataContext as IsThereAnyDealSettingsViewModel)?.Plugin;

        private List<ItadShops> StoresItems { get; set; } = new List<ItadShops>();
        private IsThereAnyDealApi IsThereAnyDealApi { get; set; } = new IsThereAnyDealApi();

        private bool _storeUiInitialized;
        private bool _wishlistUiInitialized;
        private IsThereAnyDealSettings _subscribedSettings;
        private bool? _priceOnStartBeforeWishlist;

        public IsThereAnyDealSettingsView()
        {
            InitializeComponent();
            Loaded += IsThereAnyDealSettingsView_Loaded;
            Unloaded += IsThereAnyDealSettingsView_Unloaded;
            DataContextChanged += IsThereAnyDealSettingsView_DataContextChanged;
        }

        private void IsThereAnyDealSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            EnsureWishlistUiInitialized();
            AttachWishlistEnableListeners();

            if (_storeUiInitialized)
            {
                StoreSettingsLog.Debug("IsThereAnyDeal settings view re-opened; refreshing store panels");
                RefreshStorePanels();
                RequestStorePanelsBackgroundAuthRefresh();
                return;
            }

            _storeUiInitialized = true;
            StoreSettingsLog.Debug("IsThereAnyDeal settings view initializing store panels");
            RegisterStorePanels();
            RefreshStorePanels();
        }

        private void IsThereAnyDealSettingsView_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachWishlistEnableListeners();
        }

        private void IsThereAnyDealSettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            EnsureWishlistUiInitialized();
            AttachWishlistEnableListeners();

            if (_storeUiInitialized)
            {
                RefreshStorePanels();
            }
        }

        private void EnsureWishlistUiInitialized()
        {
            IsThereAnyDealSettings settings = GetEditingSettings();
            if (settings == null || _wishlistUiInitialized)
            {
                return;
            }

            _wishlistUiInitialized = true;

            PART_DataLoad.Visibility = Visibility.Hidden;
            PART_Data.Visibility = Visibility.Visible;

            settings.wishlistIgnores = settings.wishlistIgnores.OrderBy(x => x.StoreName).ThenBy(x => x.Name).ToList();
            lvIgnoreList.ItemsSource = settings.wishlistIgnores;

            PART_LimitNotificationPrice.LongValue = settings.LimitNotificationPrice;
            lLimitNotification.Content = PART_sPriceCut.Value + "%";

            StoresItems = settings.Stores;
            ListStores.ItemsSource = StoresItems;
            ChkStore_Checked(null, null);

            PART_SelectCountry.ItemsSource = settings.Countries;
            int idx = ((List<Country>)PART_SelectCountry.ItemsSource).FindIndex(x => x.Alpha3 == settings.CountrySelected.Alpha3);
            PART_SelectCountry.SelectedIndex = idx;

            SyncUpdateOnStartUi();
        }

        private void PART_UpdateWishlistOnStart_Changed(object sender, RoutedEventArgs e)
        {
            SyncUpdateOnStartUi();
        }

        /// <summary>
        /// When wishlist update on start is enabled, prices are included: disable the price-only checkbox and show the hint.
        /// </summary>
        private void SyncUpdateOnStartUi()
        {
            if (PART_UpdateWishlistOnStart == null || PART_UpdatePriceOnStart == null)
            {
                return;
            }

            bool wishlistOnStart = PART_UpdateWishlistOnStart.IsChecked == true;

            if (wishlistOnStart)
            {
                if (_priceOnStartBeforeWishlist == null)
                {
                    _priceOnStartBeforeWishlist = PART_UpdatePriceOnStart.IsChecked == true;
                }

                PART_UpdatePriceOnStart.IsChecked = true;
                PART_UpdatePriceOnStart.IsEnabled = false;
            }
            else
            {
                PART_UpdatePriceOnStart.IsEnabled = true;
                if (_priceOnStartBeforeWishlist != null)
                {
                    PART_UpdatePriceOnStart.IsChecked = _priceOnStartBeforeWishlist;
                    _priceOnStartBeforeWishlist = null;
                }
            }

            if (PART_PriceOnStartIncludedHint != null)
            {
                PART_PriceOnStartIncludedHint.Visibility = wishlistOnStart ? Visibility.Visible : Visibility.Collapsed;
            }

            Common.LogDebug($"[Settings][update-on-start] sync wishlistOnStart={wishlistOnStart} priceOnStartEnabled={PART_UpdatePriceOnStart.IsEnabled} priceOnStartChecked={PART_UpdatePriceOnStart.IsChecked}");
        }

        private void AttachWishlistEnableListeners()
        {
            IsThereAnyDealSettings settings = GetEditingSettings();
            if (ReferenceEquals(_subscribedSettings, settings))
            {
                return;
            }

            DetachWishlistEnableListeners();

            if (settings == null)
            {
                return;
            }

            _subscribedSettings = settings;
            _subscribedSettings.PropertyChanged += Settings_PropertyChanged;
        }

        private void DetachWishlistEnableListeners()
        {
            if (_subscribedSettings == null)
            {
                return;
            }

            _subscribedSettings.PropertyChanged -= Settings_PropertyChanged;
            _subscribedSettings = null;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_storeUiInitialized)
            {
                return;
            }

            if (!IsWishlistStoreVisibilityProperty(e.PropertyName))
            {
                return;
            }

            StoreSettingsLog.Debug($"Wishlist store visibility changed: {e.PropertyName ?? "(all)"}");
            RefreshStorePanels();
            RequestStorePanelsBackgroundAuthRefresh();
        }

        private static bool IsWishlistStoreVisibilityProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return true;
            }

            return propertyName == nameof(IsThereAnyDealSettings.EnableSteam)
                || propertyName == nameof(IsThereAnyDealSettings.EnableEpic)
                || propertyName == nameof(IsThereAnyDealSettings.EnableGog);
        }

        private void RefreshStorePanels()
        {
            IsThereAnyDealSettings settings = GetEditingSettings();

            SteamPanel.StoreApi = IsSteamStoreEnabled(settings) ? IsThereAnyDeal.SteamApi : null;
            EpicPanel.StoreApi = IsEpicStoreEnabled(settings) ? IsThereAnyDeal.EpicApi : null;
            GogPanel.StoreApi = IsGogStoreEnabled(settings) ? IsThereAnyDeal.GogApi : null;

            foreach (StoreSettingsEntry entry in StoresSettings.Stores)
            {
                if (entry.Id == "Steam")
                {
                    entry.IsVisible = IsSteamStoreEnabled(settings);
                }
                else if (entry.Id == "Epic")
                {
                    entry.IsVisible = IsEpicStoreEnabled(settings);
                }
                else if (entry.Id == "Gog")
                {
                    entry.IsVisible = IsGogStoreEnabled(settings);
                }
            }

            StoresSettings.RefreshStores();
        }

        private void RequestStorePanelsBackgroundAuthRefresh()
        {
            IsThereAnyDealSettings settings = GetEditingSettings();

            if (IsSteamStoreEnabled(settings))
            {
                (SteamPanel.DataContext as IStorePanelViewModel)?.RequestBackgroundAuthRefresh();
            }

            if (IsEpicStoreEnabled(settings))
            {
                (EpicPanel.DataContext as IStorePanelViewModel)?.RequestBackgroundAuthRefresh();
            }

            if (IsGogStoreEnabled(settings))
            {
                (GogPanel.DataContext as IStorePanelViewModel)?.RequestBackgroundAuthRefresh();
            }
        }

        private void RegisterStorePanels()
        {
            IsThereAnyDealSettings settings = GetEditingSettings();

            StoresSettings.RegisterStore(new StoreSettingsEntry
            {
                Id = "Steam",
                NameResourceKey = "LOCCommonStoreSteam",
                CategoryResourceKey = "LOCCommonStoresLaunchers",
                Panel = SteamPanel,
                IsVisible = IsSteamStoreEnabled(settings),
                SortOrder = 0
            });

            StoresSettings.RegisterStore(new StoreSettingsEntry
            {
                Id = "Epic",
                NameResourceKey = "LOCCommonStoreEpic",
                CategoryResourceKey = "LOCCommonStoresLaunchers",
                Panel = EpicPanel,
                IsVisible = IsEpicStoreEnabled(settings),
                SortOrder = 1
            });

            StoresSettings.RegisterStore(new StoreSettingsEntry
            {
                Id = "Gog",
                NameResourceKey = "LOCCommonStoreGog",
                CategoryResourceKey = "LOCCommonStoresLaunchers",
                Panel = GogPanel,
                IsVisible = IsGogStoreEnabled(settings),
                SortOrder = 2
            });
        }

        private IsThereAnyDealSettings GetEditingSettings()
        {
            IsThereAnyDealSettingsViewModel viewModel = DataContext as IsThereAnyDealSettingsViewModel;
            return viewModel?.Settings;
        }

        private static bool IsSteamStoreEnabled(IsThereAnyDealSettings settings)
        {
            return settings != null && settings.EnableSteam;
        }

        private static bool IsEpicStoreEnabled(IsThereAnyDealSettings settings)
        {
            return settings != null && settings.EnableEpic;
        }

        private static bool IsGogStoreEnabled(IsThereAnyDealSettings settings)
        {
            return settings != null && settings.EnableGog;
        }

        private void PART_SelectCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Capture on UI thread — Settings/DataContext must not be read from Task.Run.
            IsThereAnyDealSettings settings = GetEditingSettings();
            if (settings?.CountrySelected == null)
            {
                return;
            }

            string countryAlpha2 = settings.CountrySelected.Alpha2;
            List<ItadShops> previousStores = settings.Stores;

            StoresText.Text = string.Empty;

            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() =>
            {
                List<ItadShops> shops = IsThereAnyDealApi.GetShops(countryAlpha2).GetAwaiter().GetResult();
                shops?.ForEach(x =>
                {
                    ItadShops finded = previousStores?.Where(y => y.Id == x.Id)?.FirstOrDefault();
                    if (finded != null)
                    {
                        x.IsCheck = finded.IsCheck;
                    }
                });

                _ = (Dispatcher?.BeginInvoke((Action)delegate
                {
                    StoresItems = shops;
                    ListStores.ItemsSource = null;
                    ListStores.ItemsSource = StoresItems;

                    ChkStore_Checked(null, null);

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                }));
            });
        }

        private void ChkStore_Checked(object sender, RoutedEventArgs e)
        {
            if (Settings == null)
            {
                return;
            }

            StoresText.Text = string.Join(", ", ((List<ItadShops>)ListStores.ItemsSource)?.Where(x => x.IsCheck).Select(x => x.Title));
            StoresItems = (List<ItadShops>)ListStores.ItemsSource;
            Settings.Stores = StoresItems;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lLimitNotification != null)
            {
                lLimitNotification.Content = ((Slider)sender).Value + "%";
            }
        }

        private void BtShow_Click(object sender, RoutedEventArgs e)
        {
            if (Settings == null)
            {
                return;
            }

            int index = int.Parse(((Button)sender).Tag.ToString());
            Settings.wishlistIgnores.RemoveAt(index);
            lvIgnoreList.ItemsSource = null;
            lvIgnoreList.ItemsSource = Settings.wishlistIgnores;
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            if (!((string)((Button)sender).Tag).IsNullOrEmpty())
            {
                _ = int.TryParse((string)((Button)sender).Tag, out int index);
                ((List<ItadNotificationCriteria>)PART_LbNotifications.ItemsSource).RemoveAt(index);
                PART_LbNotifications.Items.Refresh();
            }
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)PART_CbPriceCut.IsChecked || (bool)PART_CbPriceInferior.IsChecked)
            {
                int PriceCut = -1;
                int PriceInferior = -1;

                if ((bool)PART_CbPriceCut.IsChecked)
                {
                    PriceCut = (int)PART_sPriceCut.Value;
                }

                if ((bool)PART_CbPriceInferior.IsChecked)
                {
                    PriceInferior = (int)PART_LimitNotificationPrice.LongValue;
                }

                ((List<ItadNotificationCriteria>)PART_LbNotifications.ItemsSource).Add(new ItadNotificationCriteria
                {
                    PriceCut = PriceCut,
                    PriceInferior = PriceInferior
                });

                PART_LbNotifications.Items.Refresh();
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink)
            {
                Hyperlink link = (Hyperlink)sender;
                _ = Process.Start((string)link.Tag);
            }
            if (sender is FrameworkElement)
            {
                FrameworkElement link = (FrameworkElement)sender;
                _ = Process.Start((string)link.Tag);
            }
        }

        private void ButtonImportSteam_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin == null)
            {
                return;
            }

            string targetPath = API.Instance.Dialogs.SelectFile("json file|*.json");

            if (!targetPath.IsNullOrEmpty())
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"IsThereAnyDeal - " + ResourceProvider.GetString("LOCImportLabel"),
                    true
                );
                globalProgressOptions.IsIndeterminate = true;

                _ = API.Instance.Dialogs.ActivateGlobalProgress((a) =>
                {
                    try
                    {
                        Stopwatch stopWatch = new Stopwatch();
                        stopWatch.Start();

                        GameSource gameSource = API.Instance.Database.Sources.FirstOrDefault(x => x.Name.ToLower().IndexOf("steam") > -1);
                        if (gameSource != null)
                        {
                            Guid SteamID = gameSource.Id;

                            SteamWishlist steamWishlist = new SteamWishlist(Plugin);

                            if (steamWishlist.ImportWishlist(targetPath))
                            {
                                if (a.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }

                                _ = API.Instance.Dialogs.ShowMessage(ResourceProvider.GetString("LOCItadImportSuccessful"), "IsThereAnyDeal");
                            }
                            else
                            {
                                if (a.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }
                                _ = API.Instance.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCItadImportError"), "IsThereAnyDeal");
                            }
                        }
                        else
                        {
                            if (a.CancelToken.IsCancellationRequested)
                            {
                                return;
                            }
                            _ = API.Instance.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCItadImportError"), "IsThereAnyDeal");
                        }

                        stopWatch.Stop();
                        TimeSpan ts = stopWatch.Elapsed;
                        Logger.Info($"Task GetImportSteam() - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeal");

                        if (a.CancelToken.IsCancellationRequested)
                        {
                            return;
                        }
                        _ = API.Instance.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCItadImportError"), "IsThereAnyDeal");
                    }
                }, globalProgressOptions);
            }
        }
    }
}
