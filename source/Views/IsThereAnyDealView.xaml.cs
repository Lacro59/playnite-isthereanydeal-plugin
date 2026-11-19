using CommonPluginsControls.Controls;
using CommonPluginsShared;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Extensions;
using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private IsThereAnyDealSettings Settings { get; }
        private IsThereAnyDeal Plugin { get; }

        private List<string> SearchStores { get; set; } = new List<string>();

        private IsThereAnyDealApi IsThereAnyDealApi { get; set; } = new IsThereAnyDealApi();
        private ItadGameInfo StorePriceSelected { get; set; }

        /// <summary>
        /// When true, <see cref="UserFilter"/> additionally requires a notification-criteria match (AND with other filters).
        /// </summary>
        private bool FilterAlertsOnly { get; set; }

        /// <summary>
        /// When true, the view was opened from a price-alert notification: Alerts-only is forced for this instance and must not be written to <see cref="WishlistUiFilters"/>.
        /// </summary>
        private bool _notificationAlertsOverride;

        private bool _suppressFilterPersist;
        private readonly DispatcherTimer _filterPersistTimer;

        private ItadDataContext ItadDataContext { get; set; } = new ItadDataContext();


        /// <summary>
        /// Creates the main ITAD wishlist / giveaways view.
        /// </summary>
        /// <param name="plugin">Plugin instance.</param>
        /// <param name="id">Optional ITAD game id to select after load.</param>
        /// <param name="filterAlerts">When true, opens with the alerts-only filter active.</param>
        public IsThereAnyDealView(IsThereAnyDeal plugin, string id = "", bool filterAlerts = false)
        {
            InitializeComponent();

            Plugin = plugin;
            Settings = plugin.PluginSettingsViewModel.Settings;
            FilterAlertsOnly = filterAlerts;
            _notificationAlertsOverride = filterAlerts;
            if (filterAlerts)
            {
                Logger.Debug("[ITAD][ui] open with filterAlerts=true override=transient");
            }

            if (Settings.WishlistUiFilters == null)
            {
                Settings.WishlistUiFilters = WishlistUiFilters.CreateDefaults();
            }

            _filterPersistTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _filterPersistTimer.Tick += (s, e) =>
            {
                _filterPersistTimer.Stop();
                PersistWishlistUiFiltersNow();
            };

            DataContext = ItadDataContext;
            ItadDataContext.MinPrice = Settings.MinPrice;
            ItadDataContext.MaxPrice = Settings.MaxPrice;

            PART_DateData.Content = "";

            lbWishlist.ItemsSource = new ObservableCollection<Wishlist>();

            // Subscribe to the Loaded event instead of calling methods here
            Loaded += (s, e) =>
            {
                ApplyWishlistUiFilters(Settings.WishlistUiFilters, applyStores: false);
                ApplyNotificationAlertsOverride();

                // Now the UI is ready to display the loading bar
                RefreshData(id);
                GetListGiveaways();
            };
        }


        /// <summary>
        /// Reloads wishlist data. Store and price refresh block filter/scroll; cache-only keeps the list interactive (SWR).
        /// </summary>
        private async void RefreshData(string id, bool cachOnly = true, bool forcePrice = false)
        {
            // Store or price refresh → block interaction. Cache-only open → keep filter & scroll.
            bool blockInteraction = !cachOnly || forcePrice;

            DataLoadWishlist.Visibility = Visibility.Visible;
            DataLoadWishlist.IsHitTestVisible = blockInteraction;
            dpData.IsEnabled = !blockInteraction;
            string initialPhase = cachOnly && !forcePrice ? "cache" : (cachOnly && forcePrice ? "lookup" : "stores");
            ItadDataContext.LoadStatus = ResolveWishlistProgressLabel(initialPhase);

            Logger.Info($"[ITAD][ui] refresh start cacheOnly={cachOnly} forcePrice={forcePrice} blockInteraction={blockInteraction}");

            try
            {
                Action<string> progress = phase =>
                {
                    var label = ResolveWishlistProgressLabel(phase);
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ItadDataContext.LoadStatus = label;
                    }));
                };

                var result = await Task.Run(() => LoadData(cachOnly, forcePrice, progress));

                if (result == null)
                {
                    Logger.Warn($"[ITAD][ui] refresh empty result cacheOnly={cachOnly} forcePrice={forcePrice}");
                    return;
                }

                await Task.Yield();

                List<ItadNotificationCriteria> criterias = Settings.NotificationCriterias ?? new List<ItadNotificationCriteria>();
                foreach (Wishlist wishlist in result)
                {
                    wishlist.AttachNotificationCriterias(criterias);
                }

                lbWishlist.ItemsSource = result;

                SetInfos();

                var firstItem = result.FirstOrDefault(x => x.ItadLastPrice?.Any(y => !string.IsNullOrEmpty(y.CurrencySign)) == true);
                if (firstItem != null)
                {
                    ItadDataContext.CurrencySign = firstItem.ItadBestPrice.CurrencySign;
                }

                await Task.Delay(10);

                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        var selectedItem = result.FirstOrDefault(x => x.Game?.Id?.IsEqual(id) ?? false);
                        if (selectedItem != null)
                        {
                            lbWishlist.SelectedItem = selectedItem;
                            lbWishlist.ScrollIntoView(selectedItem);
                        }
                    }

                    PART_DateData.Content = new LocalDateTimeConverter().Convert(Settings.LastRefresh, null, null, CultureInfo.CurrentCulture);

                    PART_CbOrder_SelectionChanged(null, null);
                    SetFilterStore();
                    ApplyNotificationAlertsOverride();

                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }
            finally
            {
                DataLoadWishlist.Visibility = Visibility.Collapsed;
                DataLoadWishlist.IsHitTestVisible = true;
                dpData.IsEnabled = true;
                ItadDataContext.LoadStatus = string.Empty;
                Logger.Info($"[ITAD][ui] refresh done cacheOnly={cachOnly} forcePrice={forcePrice}");
            }
        }

        private static string ResolveWishlistProgressLabel(string phase)
        {
            switch (phase)
            {
                case "cache":
                    return ResourceProvider.GetString("LOCItadProgressCache");
                case "stores":
                    return ResourceProvider.GetString("LOCItadProgressStores");
                case "lookup":
                    return ResourceProvider.GetString("LOCItadProgressLookup");
                case "price":
                    return ResourceProvider.GetString("LOCItadProgressPrice");
                case "save":
                    return ResourceProvider.GetString("LOCItadProgressSave");
                default:
                    return ResourceProvider.GetString("LOCDownloadingLabel");
            }
        }

        private void SetFilterStore()
        {
            List<ListStore> FilterStoreItems = new List<ListStore>();
            if (Settings.EnableSteam)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Steam", StoreNameDisplay = (TransformIcon.Get("Steam") + " Steam").Trim(), IsCheck = false });
            }
            if (Settings.EnableGog)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "GOG", StoreNameDisplay = (TransformIcon.Get("GOG") + " GOG").Trim(), IsCheck = false });
            }
            if (Settings.EnableHumble)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Humble", StoreNameDisplay = (TransformIcon.Get("Humble") + " Humble").Trim(), IsCheck = false });
            }
            if (Settings.EnableEpic)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Epic Games Store", StoreNameDisplay = (TransformIcon.Get("Epic Games Store") + " Epic Games Store").Trim(), IsCheck = false });
            }
            if (Settings.EnableXbox)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Microsoft Store", StoreNameDisplay = (TransformIcon.Get("Xbox") + " Microsoft Store").Trim(), IsCheck = false });
            }
            if (Settings.EnableUbisoft)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Ubisoft Connect", StoreNameDisplay = (TransformIcon.Get("Ubisoft Connect") + " Ubisoft Connect").Trim(), IsCheck = false });
            }
            if (Settings.EnableOrigin)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "EA app", StoreNameDisplay = (TransformIcon.Get("EA app") + " EA app").Trim(), IsCheck = false });
            }
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName, true));
            ItadDataContext.FilterStoreItems = FilterStoreItems;
            ApplyWishlistUiFilters(Settings.WishlistUiFilters, applyStores: true);
        }

        private void SetInfos()
        {
            tpListBox.ItemsSource = null;
            tpListBox.ItemsSource = IsThereAnyDealApi.CountDatas;
        }


        private ObservableCollection<Wishlist> LoadData(bool cachOnly = true, bool forcePrice = false, Action<string> progress = null)
        {
            ObservableCollection<Wishlist> ListWishlist = IsThereAnyDealApi.LoadWishlist(Plugin, cachOnly, forcePrice, progress).ToObservable();
            return ListWishlist;
        }

        private List<ItadGiveaway> LoadDatatGiveaways(bool cacheOnly = false)
        {
            return IsThereAnyDealApi.GetGiveaways(cacheOnly);
        }

        private void UpdateGiveawaysLastRefreshLabel()
        {
            DateTime? lastRefresh = IsThereAnyDealApi.GetGiveawaysLastRefresh();
            if (lastRefresh == null)
            {
                PART_GiveawaysDateData.Text = string.Empty;
                return;
            }

            PART_GiveawaysDateData.Text = new LocalDateTimeConverter()
                .Convert(lastRefresh, null, null, CultureInfo.CurrentCulture)
                ?.ToString() ?? string.Empty;
        }

        private async void GetListGiveaways(bool cacheOnly = false)
        {
            PART_GiveawaysList.IsEnabled = false;
            PART_GiveawaysRefresh.IsEnabled = false;
            PART_GiveawaysList.ItemsSource = null;

            try
            {
                Logger.Info($"[ITAD][ui][giveaways] refresh start cacheOnly={cacheOnly}");
                var itadGiveaways = await Task.Run(() => LoadDatatGiveaways(cacheOnly));

                UpdateGiveawaysLastRefreshLabel();

                if (itadGiveaways == null || itadGiveaways.Count == 0)
                {
                    Logger.Debug("[ITAD][ui][giveaways] loaded count=0");
                    return;
                }

                Logger.Info($"[ITAD][ui][giveaways] loaded count={itadGiveaways.Count}");
                PART_GiveawaysList.ItemsSource = itadGiveaways;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ITAD][ui][giveaways] list failed");
            }
            finally
            {
                PART_GiveawaysList.IsEnabled = true;
                PART_GiveawaysRefresh.IsEnabled = true;
            }
        }

        private void PART_GiveawaysRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetListGiveaways(cacheOnly: false);
        }

        private void WebGiveaway(object sender, RoutedEventArgs e)
        {
            string link = (sender as Button)?.Tag as string;
            if (link.IsNullOrEmpty())
            {
                Logger.Warn("[ITAD][ui][giveaways] website click ignored link=empty");
                return;
            }

            _ = Process.Start(link);
        }


        #region Button for each game in wishlist

        private void ButtonWeb_Click(object sender, RoutedEventArgs e)
        {
            string Tag = (string)((Button)sender).Tag;
            if (!Tag.IsNullOrEmpty())
            {
                _ = Process.Start(Tag);
            }
        }

        // Resolve btStore under PART_Infos and arm it with the selected shop buy URL.
        private void LbStorePrice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox?.SelectedItem == null)
            {
                return;
            }

            StorePriceSelected = listBox.SelectedItem as ItadGameInfo;
            if (StorePriceSelected == null)
            {
                Logger.Debug("[ITAD][ui] LbStorePrice_SelectionChanged: SelectedItem is not ItadGameInfo");
                return;
            }

            FrameworkElement current = listBox.Parent as FrameworkElement;
            while (current != null && current.Name != "PART_Infos")
            {
                current = current.Parent as FrameworkElement;
            }

            if (current == null)
            {
                Logger.Warn("[ITAD][ui] LbStorePrice_SelectionChanged: PART_Infos not found");
                return;
            }

            Button bt = current.FindName("btStore") as Button;
            if (bt == null)
            {
                Logger.Warn("[ITAD][ui] LbStorePrice_SelectionChanged: btStore not found");
                return;
            }

            bt.IsEnabled = true;
            bt.Tag = StorePriceSelected.UrlBuy;
            Logger.Debug($"[ITAD][ui] LbStorePrice_SelectionChanged: btStore enabled shop={StorePriceSelected.ShopName} hasUrl={!StorePriceSelected.UrlBuy.IsNullOrEmpty()}");
        }

        // Remove game
        private void BtRemoveWishList_Click(object sender, RoutedEventArgs e)
        {
            bool IsDeleted = false;
            StorePriceSelected = (ItadGameInfo)((Button)sender).Tag;

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadDeleteOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                DataLoadWishlist.Visibility = Visibility.Visible;
                dpData.IsEnabled = false;

                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!StorePriceSelected.StoreId.IsNullOrEmpty())
                        {
                            switch (StorePriceSelected.ShopName.ToLower())
                            {
                                case "steam":
                                    SteamWishlist steamWishlist = new SteamWishlist(Plugin);
                                    IsDeleted = steamWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = steamWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "epic game store":
                                    EpicWishlist epicWishlist = new EpicWishlist(Plugin);
                                    IsDeleted = epicWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = epicWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "humble store":
                                    HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist(Plugin);
                                    IsDeleted = humbleBundleWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = humbleBundleWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "gog":
                                    GogWishlist gogWishlist = new GogWishlist(Plugin);
                                    IsDeleted = gogWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = gogWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "microsoft store":
                                    break;

                                case "origin":
                                case "ea app":
                                    EaWishlist originWishlist = new EaWishlist(Plugin);
                                    IsDeleted = originWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = originWishlist.GetWishlist(false);
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeal");
                    }
                })
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        if (IsDeleted)
                        {
                            RefreshData(string.Empty);
                        }

                        DataLoadWishlist.Visibility = Visibility.Collapsed;
                        dpData.IsEnabled = true;
                    }));
                });
            }
        }

        // Hide game
        private void BtHideWishList_Click(object sender, RoutedEventArgs e)
        {
            StorePriceSelected = (ItadGameInfo)((Button)sender).Tag;

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadHideOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    Settings.wishlistIgnores.Add(new WishlistIgnore
                    {
                        StoreId = StorePriceSelected.StoreId,
                        StoreName = StorePriceSelected.ShopName,
                        Name = StorePriceSelected.Name,
                        Id = StorePriceSelected.Id,
                        Slug = StorePriceSelected.Slug
                    });
                    Plugin.SavePluginSettings(Settings);
                    GetListGame();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }
            }
        }

        #endregion

        #region Search

        private bool UserFilter(object item)
        {
            Wishlist wishlist = item as Wishlist;
            if (wishlist == null)
            {
                return false;
            }

            bool matchesSearch = TextboxSearch.Text.IsNullOrEmpty()
                || wishlist.Name.Contains(TextboxSearch.Text, StringComparison.InvariantCultureIgnoreCase);
            bool matchesStore = SearchStores.Count == 0
                || SearchStores.Contains(wishlist.StoreName)
                || wishlist.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0;
            bool notIgnored = Settings.wishlistIgnores.All(y => y.StoreId != wishlist.StoreId && !y.Name.IsEqual(wishlist.Name));
            bool matchesDiscount = (bool)PART_RbBest.IsChecked
                ? wishlist.ItadBestPrice.PriceCut >= ItadDataContext.DiscountPercent
                : wishlist.ItadPriceForWishlistStore.PriceCut >= ItadDataContext.DiscountPercent;
            bool matchesPriceLimit = (bool)PART_RbBest.IsChecked
                ? wishlist.ItadBestPrice.PriceNew <= ItadDataContext.PriceLimit
                : wishlist.ItadPriceForWishlistStore.PriceNew <= ItadDataContext.PriceLimit;
            bool matchesLibraryOptions = (bool)PART_TbOnlyInLibrary.IsChecked
                ? wishlist.InLibrary
                : (!(bool)PART_TbIncludeInLibrary.IsChecked
                    ? !wishlist.InLibrary
                    : (false && (bool)PART_TbIncludeWithoutData.IsChecked) || wishlist.HasItadData);
            bool matchesAlert = !FilterAlertsOnly || wishlist.GetNotification(Settings.NotificationCriterias);

            return matchesDiscount
                && matchesPriceLimit
                && matchesSearch
                && matchesStore
                && notIgnored
                && matchesLibraryOptions
                && matchesAlert;
        }

        // Get list
        private void GetListGame()
        {
            if (lbWishlist?.ItemsSource != null)
            {
                CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource).Refresh();
            }
        }

        // Search by name
        private void TextboxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetListGame();
        }

        // Search by store
        private void ChkStore_Checked(object sender, RoutedEventArgs e)
        {
            FilterCbStore((CheckBox)sender);
        }
        private void ChkStore_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterCbStore((CheckBox)sender);
        }
        private void FilterCbStore(CheckBox sender)
        {
            FilterStore.Text = string.Empty;

            if ((bool)sender.IsChecked)
            {
                SearchStores.Add((string)sender.Tag);
            }
            else
            {
                _ = SearchStores.Remove((string)sender.Tag);
            }

            if (SearchStores.Count != 0)
            {
                FilterStore.Text = string.Join(", ", SearchStores);
            }

            GetListGame();
            ScheduleWishlistUiFiltersPersist();
        }

        private void PART_Tb_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb == null)
            {
                return;
            }

            if (tb.Name == "PART_TbAlerts")
            {
                // Alerts filter is session-only (never written to WishlistUiFilters)
                FilterAlertsOnly = tb.IsChecked == true;
                if (_notificationAlertsOverride && FilterAlertsOnly == false)
                {
                    _notificationAlertsOverride = false;
                    Logger.Debug("[ITAD][filters] notification alerts override cleared by user");
                }

                Logger.Debug($"[ITAD][ui] alertsFilter active={FilterAlertsOnly}");
                GetListGame();
                return;
            }

            if (tb.Name == "PART_TbOnlyInLibrary")
            {
                PART_TbIncludeInLibrary.IsChecked = false;
                PART_TbIncludeWithoutData.IsChecked = false;
            }
            else
            {
                PART_TbOnlyInLibrary.IsChecked = false;
            }

            GetListGame();
            ScheduleWishlistUiFiltersPersist();
        }

        private void PART_BtResetFilters_Click(object sender, RoutedEventArgs e)
        {
            WishlistUiFilters defaults = WishlistUiFilters.CreateDefaults();
            ApplyWishlistUiFilters(defaults, applyStores: true);

            // Include / Only already applied from defaults; clear session-only toggles
            _suppressFilterPersist = true;
            try
            {
                if (PART_TbIncludeWithoutData != null)
                {
                    PART_TbIncludeWithoutData.IsChecked = false;
                }

                if (PART_TbAlerts != null)
                {
                    PART_TbAlerts.IsChecked = false;
                }

                FilterAlertsOnly = false;
                _notificationAlertsOverride = false;
            }
            finally
            {
                _suppressFilterPersist = false;
            }

            Settings.WishlistUiFilters = defaults.Clone();
            PersistWishlistUiFiltersNow();
            GetListGame();
            Logger.Info(
                $"[ITAD][filters] reset to defaults include={defaults.IncludeInLibrary} only={defaults.OnlyInLibrary}");
        }

        #endregion

        #region Order

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (PART_Order != null)
            {
                PART_Order.Visibility = tc.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

		private void PART_CbOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				if (lbWishlist?.ItemsSource == null) return;

				var view = CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource);
				view.SortDescriptions.Clear();

				string orderType = ((ComboBoxItem)PART_CbOrderType.SelectedItem).Tag.ToString();
				bool isAscending = ((ComboBoxItem)PART_CbOrder.SelectedItem).Tag.ToString() == "0";

				ListSortDirection direction = isAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

				switch (orderType)
				{
					case "0":
						view.SortDescriptions.Add(new SortDescription("Name", direction));
						break;
					case "1":
						view.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceCut", direction));
						break;
					case "2":
						view.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceNew", direction));
						break;
					case "3":
						view.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceCut", direction));
						break;
					case "4":
						view.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceNew", direction));
						break;
					case "5":
						view.SortDescriptions.Add(new SortDescription("ReleaseDate", direction));
						break;
				}

				view.Filter = UserFilter;
                if (sender != null)
                {
                    ScheduleWishlistUiFiltersPersist();
                }
			}
			catch (Exception ex)
			{
				Common.LogError(ex, false);
			}
		}
		#endregion

		#region Data refresh

		private void ButtonData_Click(object sender, RoutedEventArgs e)
        {
            RefreshData(string.Empty, false);
        }

        private void ButtonPrice_Click(object sender, RoutedEventArgs e)
        {
            RefreshData(string.Empty, true, true);
        }

        #endregion


        // Search price
        // Search percentage
        private void On_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            GetListGame();
            ScheduleWishlistUiFiltersPersist();
        }

        private void PART_Rb_Click(object sender, RoutedEventArgs e)
        {
            GetListGame();
            ScheduleWishlistUiFiltersPersist();
        }

        #region Wishlist UI filter persistence

        /// <summary>
        /// Forces Alerts-only for a notification-opened view without scheduling a settings save.
        /// </summary>
        private void ApplyNotificationAlertsOverride()
        {
            if (!_notificationAlertsOverride)
            {
                return;
            }

            bool alreadyActive = FilterAlertsOnly && PART_TbAlerts?.IsChecked == true;
            _filterPersistTimer.Stop();
            _suppressFilterPersist = true;
            try
            {
                FilterAlertsOnly = true;
                if (PART_TbAlerts != null)
                {
                    PART_TbAlerts.IsChecked = true;
                }

                if (!alreadyActive)
                {
                    Logger.Debug("[ITAD][filters] notification alerts override applied persist=false");
                }
            }
            finally
            {
                _suppressFilterPersist = false;
            }
        }

        private void ScheduleWishlistUiFiltersPersist()
        {
            if (_suppressFilterPersist)
            {
                return;
            }

            _filterPersistTimer.Stop();
            _filterPersistTimer.Start();
        }

        private void PersistWishlistUiFiltersNow()
        {
            if (_suppressFilterPersist)
            {
                return;
            }

            try
            {
                Settings.WishlistUiFilters = CaptureWishlistUiFilters();
                Plugin.SavePluginSettings(Settings);
                Logger.Debug(
                    $"[ITAD][filters] persisted include={Settings.WishlistUiFilters.IncludeInLibrary} only={Settings.WishlistUiFilters.OnlyInLibrary} stores={Settings.WishlistUiFilters.SelectedStores?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ITAD][filters] persist failed");
            }
        }

        private WishlistUiFilters CaptureWishlistUiFilters()
        {
            string orderType = "0";
            string orderDirection = "0";
            if (PART_CbOrderType?.SelectedItem is ComboBoxItem orderTypeItem && orderTypeItem.Tag != null)
            {
                orderType = orderTypeItem.Tag.ToString();
            }

            if (PART_CbOrder?.SelectedItem is ComboBoxItem orderDirItem && orderDirItem.Tag != null)
            {
                orderDirection = orderDirItem.Tag.ToString();
            }

            return new WishlistUiFilters
            {
                IncludeInLibrary = PART_TbIncludeInLibrary?.IsChecked == true,
                OnlyInLibrary = PART_TbOnlyInLibrary?.IsChecked == true,
                UseBestPrice = PART_RbBest?.IsChecked != false,
                DiscountPercent = ItadDataContext.DiscountPercent,
                PriceLimit = ItadDataContext.PriceLimit,
                SelectedStores = SearchStores != null ? new List<string>(SearchStores) : new List<string>(),
                OrderTypeTag = orderType,
                OrderDirectionTag = orderDirection
            };
        }

        private void ApplyWishlistUiFilters(WishlistUiFilters filters, bool applyStores)
        {
            if (filters == null)
            {
                filters = WishlistUiFilters.CreateDefaults();
            }

            _suppressFilterPersist = true;
            try
            {
                if (PART_TbOnlyInLibrary != null)
                {
                    PART_TbOnlyInLibrary.IsChecked = filters.OnlyInLibrary;
                }

                if (PART_TbIncludeInLibrary != null)
                {
                    PART_TbIncludeInLibrary.IsChecked = filters.IncludeInLibrary && !filters.OnlyInLibrary;
                }

                if (PART_RbBest != null && PART_RbStore != null)
                {
                    PART_RbBest.IsChecked = filters.UseBestPrice;
                    PART_RbStore.IsChecked = !filters.UseBestPrice;
                }

                double minPrice = Settings.MinPrice;
                double maxPrice = Settings.MaxPrice;
                double priceLimit = filters.PriceLimit;
                if (priceLimit < minPrice)
                {
                    priceLimit = minPrice;
                }

                if (priceLimit > maxPrice)
                {
                    priceLimit = maxPrice;
                }

                ItadDataContext.DiscountPercent = filters.DiscountPercent;
                ItadDataContext.PriceLimit = priceLimit;

                SelectComboByTag(PART_CbOrderType, filters.OrderTypeTag);
                SelectComboByTag(PART_CbOrder, filters.OrderDirectionTag);

                if (applyStores)
                {
                    ApplyStoreFilterSelection(filters.SelectedStores);
                }

                int storeCount = filters.SelectedStores?.Count ?? 0;
                if (!applyStores)
                {
                    Logger.Info(
                        $"[ITAD][filters] restored include={filters.IncludeInLibrary} only={filters.OnlyInLibrary} best={filters.UseBestPrice} discount={filters.DiscountPercent} priceLimit={priceLimit} order={filters.OrderTypeTag}/{filters.OrderDirectionTag}");
                }
                else
                {
                    Logger.Debug($"[ITAD][filters] stores reapplied count={storeCount}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ITAD][filters] apply failed");
            }
            finally
            {
                _suppressFilterPersist = false;
            }
        }

        private void ApplyStoreFilterSelection(List<string> selectedStores)
        {
            SearchStores = new List<string>();
            FilterStore.Text = string.Empty;

            List<string> selected = selectedStores ?? new List<string>();
            if (ItadDataContext.FilterStoreItems == null)
            {
                return;
            }

            foreach (ListStore store in ItadDataContext.FilterStoreItems)
            {
                store.IsCheck = selected.Exists(x => x.IsEqual(store.StoreName));
            }

            // Rebuild after CheckBox events that may have mutated SearchStores during IsCheck assign
            SearchStores = ItadDataContext.FilterStoreItems
                .Where(x => x.IsCheck)
                .Select(x => x.StoreName)
                .ToList();

            if (SearchStores.Count != 0)
            {
                FilterStore.Text = string.Join(", ", SearchStores);
            }
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            if (combo == null)
            {
                return;
            }

            string target = string.IsNullOrEmpty(tag) ? "0" : tag;
            foreach (object item in combo.Items)
            {
                ComboBoxItem comboItem = item as ComboBoxItem;
                if (comboItem?.Tag != null && comboItem.Tag.ToString() == target)
                {
                    combo.SelectedItem = comboItem;
                    return;
                }
            }
        }

        #endregion
    }


    public class ItadDataContext : ObservableObject
    {
        private string _currencySign = "$";
        public string CurrencySign { get => _currencySign; set => SetValue(ref _currencySign, value); }

        private double _minPrice = 0;
        public double MinPrice { get => _minPrice; set => SetValue(ref _minPrice, value); }

        private double _maxPrice = 250;
        public double MaxPrice { get => _maxPrice; set => SetValue(ref _maxPrice, value); }



        private double _discountPercent = 0;
        public double DiscountPercent { get => _discountPercent; set => SetValue(ref _discountPercent, value); }

        private double _priceLimit = 100;
        public double PriceLimit { get => _priceLimit; set => SetValue(ref _priceLimit, value); }

        private List<ListStore> _filterStoreItems = new List<ListStore>();
        public List<ListStore> FilterStoreItems { get => _filterStoreItems; set => SetValue(ref _filterStoreItems, value); }

        private string _loadStatus = string.Empty;
        public string LoadStatus { get => _loadStatus; set => SetValue(ref _loadStatus, value); }
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public string StoreNameDisplay { get; set; }
        public bool IsCheck { get; set; }
    }
}
