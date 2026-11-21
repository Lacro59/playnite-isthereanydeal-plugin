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
using System.Windows.Data;
using System.Windows.Threading;

namespace IsThereAnyDeal.ViewModels
{
    /// <summary>
    /// Presentation state and commands for the main ITAD wishlist / giveaways view.
    /// Replaces the former <c>ItadDataContext</c> nested in the view code-behind.
    /// </summary>
    public class IsThereAnyDealViewModel : ObservableObject
    {
        private static ILogger Logger => LogManager.GetLogger();

        private readonly IsThereAnyDeal _plugin;
        private readonly IsThereAnyDealSettings _settings;
        private readonly IsThereAnyDealApi _api = new IsThereAnyDealApi();
        private readonly DispatcherTimer _filterPersistTimer;
        private readonly DispatcherTimer _sliderFilterTimer;
        private readonly LocalDateTimeConverter _localDateTimeConverter = new LocalDateTimeConverter();

        private ICollectionView _wishlistView;
        private bool _suppressFilterPersist;
        private bool _notificationAlertsOverride;
        private List<string> _searchStores = new List<string>();

        private string _currencySign = "$";
        private double _minPrice;
        private double _maxPrice = 250;
        private double _discountPercent;
        private double _priceLimit = 100;
        private List<ListStore> _filterStoreItems = new List<ListStore>();
        private string _loadStatus = string.Empty;

        private string _searchText = string.Empty;
        private string _selectedStoresText = string.Empty;
        private string _selectedStoresToolTip = string.Empty;
        private bool _useBestPrice = true;
        private bool _includeInLibrary = true;
        private bool _onlyInLibrary;
        private bool _includeWithoutData;
        private bool _alertsOnly;
        private string _orderTypeTag = "0";
        private string _orderDirectionTag = "0";
        private string _orderDirectionGlyph = "\uEA99";
        private string _orderDirectionToolTip;
        private bool _filtersPanelExpanded = true;

        private ObservableCollection<Wishlist> _wishlistItems = new ObservableCollection<Wishlist>();
        private bool _isWishlistLoading;
        private bool _isWishlistInteractionBlocked;
        private bool _isWishlistEmpty;
        private bool _isWishlistNoResults;
        private int _filteredCount;
        private string _lastRefreshLabel = string.Empty;

        private List<ItadGiveaway> _giveawaysItems = new List<ItadGiveaway>();
        private bool _isGiveawaysBusy;
        private bool _isGiveawaysEmpty = true;
        private string _giveawaysLastRefreshLabel = string.Empty;

        /// <summary>
        /// Creates the view model and seeds price-slider bounds from plugin settings.
        /// </summary>
        /// <param name="plugin">Plugin instance providing settings and save hooks.</param>
        /// <param name="filterAlerts">When true, opens with the alerts-only filter forced (notification open).</param>
        public IsThereAnyDealViewModel(IsThereAnyDeal plugin, bool filterAlerts = false)
        {
            _plugin = plugin;
            _settings = plugin.PluginSettingsViewModel.Settings;
            MinPrice = _settings.MinPrice;
            MaxPrice = _settings.MaxPrice;
            _filtersPanelExpanded = _settings.WishlistFiltersPanelExpanded;
            _notificationAlertsOverride = filterAlerts;
            _alertsOnly = filterAlerts;
            _orderDirectionToolTip = ResourceProvider.GetString("LOCMenuSortAscending");

            if (_settings.WishlistUiFilters == null)
            {
                _settings.WishlistUiFilters = WishlistUiFilters.CreateDefaults();
            }

            if (filterAlerts)
            {
                Logger.Debug("[ITAD][ui] open with filterAlerts=true override=transient");
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

            _sliderFilterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _sliderFilterTimer.Tick += (s, e) =>
            {
                _sliderFilterTimer.Stop();
                RefreshWishlistFilter();
                ScheduleWishlistUiFiltersPersist();
            };

            PropertyChanged += OnSelfPropertyChanged;

            ResetFiltersCommand = new RelayCommand(ResetFilters);
            ToggleOrderDirectionCommand = new RelayCommand(ToggleOrderDirection);
            SelectAllStoresCommand = new RelayCommand(SelectAllStores);
            DeselectAllStoresCommand = new RelayCommand(DeselectAllStores);
            RefreshStoresCommand = new RelayCommand(() => _ = RefreshWishlistAsync(string.Empty, cacheOnly: false));
            RefreshPricesCommand = new RelayCommand(() => _ = RefreshWishlistAsync(string.Empty, cacheOnly: true, forcePrice: true));
            OpenUrlCommand = new RelayCommand<string>(OpenUrl);
            HideWishlistCommand = new RelayCommand<ItadGameInfo>(HideWishlist);
            RemoveWishlistCommand = new RelayCommand<ItadGameInfo>(RemoveWishlist);
            RefreshGiveawaysCommand = new RelayCommand(() => _ = RefreshGiveawaysAsync(cacheOnly: false));

            _wishlistView = CollectionViewSource.GetDefaultView(_wishlistItems);
            ApplySortAndFilter();
            UpdateSelectedStoresPresentation();
        }

        /// <summary>Raised when the view should select and scroll to a wishlist row (visual-only).</summary>
        public event EventHandler<Wishlist> SelectionRequested;

        /// <summary>Plugin instance for settings persistence and save hooks.</summary>
        public IsThereAnyDeal Plugin => _plugin;

        /// <summary>Live plugin settings blob.</summary>
        public IsThereAnyDealSettings Settings => _settings;

        /// <summary>Shared API client for wishlist and giveaways loads.</summary>
        internal IsThereAnyDealApi Api => _api;

        /// <summary>Resets persisted and session filters to defaults.</summary>
        public RelayCommand ResetFiltersCommand { get; }

        /// <summary>Toggles ascending / descending sort.</summary>
        public RelayCommand ToggleOrderDirectionCommand { get; }

        /// <summary>Checks every store in the wishlist source filter.</summary>
        public RelayCommand SelectAllStoresCommand { get; }

        /// <summary>Clears every store check (empty selection = all stores).</summary>
        public RelayCommand DeselectAllStoresCommand { get; }

        /// <summary>Full store-data refresh (blocks interaction).</summary>
        public RelayCommand RefreshStoresCommand { get; }

        /// <summary>Price lookup refresh (blocks interaction).</summary>
        public RelayCommand RefreshPricesCommand { get; }

        /// <summary>Opens a store or ITAD URL in the default browser.</summary>
        public RelayCommand<string> OpenUrlCommand { get; }

        /// <summary>Hides a wishlist row (adds to <c>wishlistIgnores</c>).</summary>
        public RelayCommand<ItadGameInfo> HideWishlistCommand { get; }

        /// <summary>Removes a game from the remote store wishlist after confirmation.</summary>
        public RelayCommand<ItadGameInfo> RemoveWishlistCommand { get; }

        /// <summary>Reloads giveaways from the network (not cache-only).</summary>
        public RelayCommand RefreshGiveawaysCommand { get; }

        /// <summary>Currency symbol shown next to price filters and rows.</summary>
        public string CurrencySign
        {
            get => _currencySign;
            set => SetValue(ref _currencySign, value);
        }

        /// <summary>Minimum bound for the price-limit slider.</summary>
        public double MinPrice
        {
            get => _minPrice;
            set => SetValue(ref _minPrice, value);
        }

        /// <summary>Maximum bound for the price-limit slider.</summary>
        public double MaxPrice
        {
            get => _maxPrice;
            set => SetValue(ref _maxPrice, value);
        }

        /// <summary>Minimum discount percent filter (0–100).</summary>
        public double DiscountPercent
        {
            get => _discountPercent;
            set => SetValue(ref _discountPercent, value);
        }

        /// <summary>Maximum price filter value within <see cref="MinPrice"/>–<see cref="MaxPrice"/>.</summary>
        public double PriceLimit
        {
            get => _priceLimit;
            set => SetValue(ref _priceLimit, value);
        }

        /// <summary>Store checkboxes for the wishlist source filter.</summary>
        public List<ListStore> FilterStoreItems
        {
            get => _filterStoreItems;
            set
            {
                UnhookStoreItems(_filterStoreItems);
                SetValue(ref _filterStoreItems, value ?? new List<ListStore>());
                HookStoreItems(_filterStoreItems);
            }
        }

        /// <summary>Progress label shown while wishlist data is loading.</summary>
        public string LoadStatus
        {
            get => _loadStatus;
            set => SetValue(ref _loadStatus, value);
        }

        /// <summary>Wishlist rows bound to the ListBox (filter/sort via the default view).</summary>
        public ObservableCollection<Wishlist> WishlistItems
        {
            get => _wishlistItems;
            private set => SetValue(ref _wishlistItems, value);
        }

        /// <summary>True while a wishlist load overlay is shown.</summary>
        public bool IsWishlistLoading
        {
            get => _isWishlistLoading;
            set
            {
                if (_isWishlistLoading == value)
                {
                    return;
                }

                SetValue(ref _isWishlistLoading, value);
                UpdateWishlistEmptyState();
            }
        }

        /// <summary>True when store/price refresh must block filter and scroll interaction.</summary>
        public bool IsWishlistInteractionBlocked
        {
            get => _isWishlistInteractionBlocked;
            set
            {
                if (_isWishlistInteractionBlocked == value)
                {
                    return;
                }

                SetValue(ref _isWishlistInteractionBlocked, value);
                OnPropertyChanged(nameof(IsWishlistContentEnabled));
            }
        }

        /// <summary>Inverse of <see cref="IsWishlistInteractionBlocked"/> for the wishlist dock panel.</summary>
        public bool IsWishlistContentEnabled => !_isWishlistInteractionBlocked;

        /// <summary>True when the source wishlist is empty (not loading).</summary>
        public bool IsWishlistEmpty
        {
            get => _isWishlistEmpty;
            private set => SetValue(ref _isWishlistEmpty, value);
        }

        /// <summary>True when filters hide every row while the source is non-empty.</summary>
        public bool IsWishlistNoResults
        {
            get => _isWishlistNoResults;
            private set => SetValue(ref _isWishlistNoResults, value);
        }

        /// <summary>Count of rows visible after filter (toolbar total).</summary>
        public int FilteredCount
        {
            get => _filteredCount;
            private set => SetValue(ref _filteredCount, value);
        }

        /// <summary>Formatted last wishlist refresh timestamp.</summary>
        public string LastRefreshLabel
        {
            get => _lastRefreshLabel;
            private set => SetValue(ref _lastRefreshLabel, value ?? string.Empty);
        }

        /// <summary>Giveaway rows for the Giveaways tab.</summary>
        public List<ItadGiveaway> GiveawaysItems
        {
            get => _giveawaysItems;
            private set => SetValue(ref _giveawaysItems, value ?? new List<ItadGiveaway>());
        }

        /// <summary>True while giveaways are loading (disables list and refresh).</summary>
        public bool IsGiveawaysBusy
        {
            get => _isGiveawaysBusy;
            private set
            {
                if (_isGiveawaysBusy == value)
                {
                    return;
                }

                SetValue(ref _isGiveawaysBusy, value);
                OnPropertyChanged(nameof(IsGiveawaysContentEnabled));
            }
        }

        /// <summary>Inverse of <see cref="IsGiveawaysBusy"/> for the giveaways list.</summary>
        public bool IsGiveawaysContentEnabled => !_isGiveawaysBusy;

        /// <summary>True when there are no giveaways to show.</summary>
        public bool IsGiveawaysEmpty
        {
            get => _isGiveawaysEmpty;
            private set => SetValue(ref _isGiveawaysEmpty, value);
        }

        /// <summary>Formatted last giveaways cache refresh timestamp.</summary>
        public string GiveawaysLastRefreshLabel
        {
            get => _giveawaysLastRefreshLabel;
            private set => SetValue(ref _giveawaysLastRefreshLabel, value ?? string.Empty);
        }

        /// <summary>Session-only name search text.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                SetValue(ref _searchText, value ?? string.Empty);
                if (!_suppressFilterPersist)
                {
                    RefreshWishlistFilter();
                }
            }
        }

        /// <summary>Button label for the store filter: all / single name / count.</summary>
        public string SelectedStoresText
        {
            get => _selectedStoresText;
            private set => SetValue(ref _selectedStoresText, value ?? string.Empty);
        }

        /// <summary>Tooltip listing checked store names (or the same text when empty / single).</summary>
        public string SelectedStoresToolTip
        {
            get => _selectedStoresToolTip;
            private set => SetValue(ref _selectedStoresToolTip, value ?? string.Empty);
        }

        /// <summary>When true, discount/price filters use best price.</summary>
        public bool IsBestPriceMode
        {
            get => _useBestPrice;
            set
            {
                if (!value || _useBestPrice)
                {
                    return;
                }

                _useBestPrice = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStorePriceMode));
                OnPersistedFilterChanged();
            }
        }

        /// <summary>When true, discount/price filters use wishlist-store price.</summary>
        public bool IsStorePriceMode
        {
            get => !_useBestPrice;
            set
            {
                if (!value || !_useBestPrice)
                {
                    return;
                }

                _useBestPrice = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBestPriceMode));
                OnPersistedFilterChanged();
            }
        }

        /// <summary>Exclude games already in the Playnite library.</summary>
        public bool IsLibraryExclude
        {
            get => !_includeInLibrary && !_onlyInLibrary;
            set
            {
                if (!value)
                {
                    return;
                }

                ApplyLibraryMode(includeInLibrary: false, onlyInLibrary: false);
            }
        }

        /// <summary>Include games already in the Playnite library.</summary>
        public bool IsLibraryInclude
        {
            get => _includeInLibrary && !_onlyInLibrary;
            set
            {
                if (!value)
                {
                    return;
                }

                ApplyLibraryMode(includeInLibrary: true, onlyInLibrary: false);
            }
        }

        /// <summary>Show only games already in the Playnite library.</summary>
        public bool IsLibraryOnly
        {
            get => _onlyInLibrary;
            set
            {
                if (!value)
                {
                    return;
                }

                ApplyLibraryMode(includeInLibrary: false, onlyInLibrary: true);
            }
        }

        /// <summary>Session-only: include wishlist rows without ITAD price data.</summary>
        public bool IncludeWithoutData
        {
            get => _includeWithoutData;
            set
            {
                if (_includeWithoutData == value)
                {
                    return;
                }

                SetValue(ref _includeWithoutData, value);
                if (!_suppressFilterPersist)
                {
                    Logger.Debug($"[ITAD][ui] includeWithoutData active={value}");
                    RefreshWishlistFilter();
                }
            }
        }

        /// <summary>Session-only: show only rows matching notification criteria.</summary>
        public bool AlertsOnly
        {
            get => _alertsOnly;
            set
            {
                if (_alertsOnly == value)
                {
                    return;
                }

                SetValue(ref _alertsOnly, value);
                if (_notificationAlertsOverride && !value)
                {
                    _notificationAlertsOverride = false;
                    Logger.Debug("[ITAD][filters] notification alerts override cleared by user");
                }

                if (!_suppressFilterPersist)
                {
                    Logger.Debug($"[ITAD][ui] alertsFilter active={value}");
                    RefreshWishlistFilter();
                }
            }
        }

        /// <summary>Order type combo tag (0–5).</summary>
        public string OrderTypeTag
        {
            get => _orderTypeTag;
            set
            {
                string next = string.IsNullOrEmpty(value) ? "0" : value;
                if (_orderTypeTag == next)
                {
                    return;
                }

                SetValue(ref _orderTypeTag, next);
                if (!_suppressFilterPersist)
                {
                    ApplySortAndFilter();
                    ScheduleWishlistUiFiltersPersist();
                }
            }
        }

        /// <summary>Order direction tag (0 ascending, 1 descending).</summary>
        public string OrderDirectionTag
        {
            get => _orderDirectionTag;
            private set
            {
                string next = string.IsNullOrEmpty(value) ? "0" : value;
                if (_orderDirectionTag == next)
                {
                    return;
                }

                SetValue(ref _orderDirectionTag, next);
                UpdateOrderDirectionPresentation();
            }
        }

        /// <summary>IcoFont glyph for the sort-direction button.</summary>
        public string OrderDirectionGlyph
        {
            get => _orderDirectionGlyph;
            private set => SetValue(ref _orderDirectionGlyph, value);
        }

        /// <summary>Tooltip for the sort-direction button.</summary>
        public string OrderDirectionToolTip
        {
            get => _orderDirectionToolTip;
            private set => SetValue(ref _orderDirectionToolTip, value);
        }

        /// <summary>Whether the filters expander is open (persisted separately from WishlistUiFilters).</summary>
        public bool FiltersPanelExpanded
        {
            get => _filtersPanelExpanded;
            set
            {
                if (_filtersPanelExpanded == value)
                {
                    return;
                }

                SetValue(ref _filtersPanelExpanded, value);
                if (_suppressFilterPersist)
                {
                    return;
                }

                try
                {
                    _settings.WishlistFiltersPanelExpanded = value;
                    _plugin.SavePluginSettings(_settings);
                    Logger.Debug($"[ITAD][filters] panelExpanded={value}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[ITAD][filters] panel expand persist failed");
                }
            }
        }

        /// <summary>
        /// Restores persisted filters then starts the first wishlist and giveaways loads (call from view Loaded).
        /// </summary>
        /// <param name="selectGameId">Optional ITAD / game id to select after load.</param>
        public void Initialize(string selectGameId)
        {
            ApplyWishlistUiFilters(_settings.WishlistUiFilters, applyStores: false);
            ApplyNotificationAlertsOverride();
            _ = RefreshWishlistAsync(selectGameId);
            _ = RefreshGiveawaysAsync(cacheOnly: false);
        }

        /// <summary>
        /// Loads giveaways (cache-first on open; network on refresh command).
        /// </summary>
        /// <param name="cacheOnly">When true, prefer the local giveaways cache.</param>
        public async Task RefreshGiveawaysAsync(bool cacheOnly = false)
        {
            IsGiveawaysBusy = true;
            GiveawaysItems = new List<ItadGiveaway>();
            UpdateGiveawaysEmptyState();

            try
            {
                Logger.Info($"[ITAD][ui][giveaways] refresh start cacheOnly={cacheOnly}");
                List<ItadGiveaway> itadGiveaways = await Task.Run(() => _api.GetGiveaways(cacheOnly));

                UpdateGiveawaysLastRefreshLabel();

                if (itadGiveaways == null || itadGiveaways.Count == 0)
                {
                    Logger.Debug("[ITAD][ui][giveaways] loaded count=0");
                    GiveawaysItems = new List<ItadGiveaway>();
                    UpdateGiveawaysEmptyState();
                    return;
                }

                Logger.Info($"[ITAD][ui][giveaways] loaded count={itadGiveaways.Count}");
                GiveawaysItems = itadGiveaways;
                UpdateGiveawaysEmptyState();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ITAD][ui][giveaways] list failed");
                GiveawaysItems = new List<ItadGiveaway>();
                UpdateGiveawaysEmptyState();
            }
            finally
            {
                IsGiveawaysBusy = false;
            }
        }

        private void UpdateGiveawaysLastRefreshLabel()
        {
            DateTime? lastRefresh = IsThereAnyDealApi.GetGiveawaysLastRefresh();
            if (lastRefresh == null)
            {
                GiveawaysLastRefreshLabel = string.Empty;
                return;
            }

            GiveawaysLastRefreshLabel = _localDateTimeConverter
                .Convert(lastRefresh, null, null, CultureInfo.CurrentCulture)
                ?.ToString() ?? string.Empty;
        }

        private void UpdateGiveawaysEmptyState()
        {
            int count = GiveawaysItems != null ? GiveawaysItems.Count : 0;
            IsGiveawaysEmpty = count == 0;
        }

        /// <summary>
        /// Shows the loading overlay for a long-running wishlist mutation (e.g. remove-from-store).
        /// </summary>
        /// <param name="blockInteraction">When true, disables filter/scroll until <see cref="EndWishlistBusy"/>.</param>
        public void BeginWishlistBusy(bool blockInteraction)
        {
            IsWishlistLoading = true;
            IsWishlistInteractionBlocked = blockInteraction;
            if (string.IsNullOrEmpty(LoadStatus))
            {
                LoadStatus = ResourceProvider.GetString("LOCDownloadingLabel");
            }
        }

        /// <summary>Hides the loading overlay opened by <see cref="BeginWishlistBusy"/>.</summary>
        public void EndWishlistBusy()
        {
            IsWishlistLoading = false;
            IsWishlistInteractionBlocked = false;
            LoadStatus = string.Empty;
        }

        /// <summary>
        /// Reloads wishlist data. Store or price refresh blocks interaction; cache-only keeps the list interactive (SWR).
        /// </summary>
        /// <param name="id">Optional game id to select after load.</param>
        /// <param name="cacheOnly">When true, prefer cache (unless <paramref name="forcePrice"/>).</param>
        /// <param name="forcePrice">When true with cache-only, run price lookup.</param>
        public async Task RefreshWishlistAsync(string id = "", bool cacheOnly = true, bool forcePrice = false)
        {
            bool blockInteraction = !cacheOnly || forcePrice;
            IsWishlistLoading = true;
            IsWishlistInteractionBlocked = blockInteraction;
            string initialPhase = cacheOnly && !forcePrice ? "cache" : (cacheOnly && forcePrice ? "lookup" : "stores");
            LoadStatus = ResolveWishlistProgressLabel(initialPhase);

            Logger.Info($"[ITAD][ui] refresh start cacheOnly={cacheOnly} forcePrice={forcePrice} blockInteraction={blockInteraction}");

            try
            {
                Action<string> progress = phase =>
                {
                    string label = ResolveWishlistProgressLabel(phase);
                    _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LoadStatus = label;
                    }));
                };

                ObservableCollection<Wishlist> result = await Task.Run(() => LoadWishlistData(cacheOnly, forcePrice, progress));

                if (result == null)
                {
                    Logger.Warn($"[ITAD][ui] refresh empty result cacheOnly={cacheOnly} forcePrice={forcePrice}");
                    SetWishlistItems(new ObservableCollection<Wishlist>());
                    return;
                }

                await Task.Yield();

                List<ItadNotificationCriteria> criterias = _settings.NotificationCriterias ?? new List<ItadNotificationCriteria>();
                foreach (Wishlist wishlist in result)
                {
                    wishlist.AttachNotificationCriterias(criterias);
                }

                SetWishlistItems(result);

                Wishlist firstItem = result.FirstOrDefault(x => x.ItadLastPrice?.Any(y => !string.IsNullOrEmpty(y.CurrencySign)) == true);
                if (firstItem != null)
                {
                    CurrencySign = firstItem.ItadBestPrice.CurrencySign;
                }

                await Task.Delay(10);

                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        Wishlist selectedItem = result.FirstOrDefault(x => x.Game?.Id?.IsEqual(id) ?? false);
                        if (selectedItem != null)
                        {
                            SelectionRequested?.Invoke(this, selectedItem);
                        }
                    }

                    LastRefreshLabel = _localDateTimeConverter
                        .Convert(_settings.LastRefresh, null, null, CultureInfo.CurrentCulture)
                        ?.ToString() ?? string.Empty;

                    RebuildStoreFilters();
                    ApplyNotificationAlertsOverride();
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }
            finally
            {
                IsWishlistLoading = false;
                IsWishlistInteractionBlocked = false;
                LoadStatus = string.Empty;
                UpdateWishlistEmptyState();
                Logger.Info($"[ITAD][ui] refresh done cacheOnly={cacheOnly} forcePrice={forcePrice}");
            }
        }

        /// <summary>
        /// Rebuilds store filter checkboxes from enabled stores, then reapplies persisted store selection.
        /// </summary>
        public void RebuildStoreFilters()
        {
            List<ListStore> items = new List<ListStore>();
            if (_settings.EnableSteam)
            {
                items.Add(CreateStoreFilterItem("Steam", "Steam"));
            }

            if (_settings.EnableGog)
            {
                items.Add(CreateStoreFilterItem("GOG", "GOG"));
            }

            if (_settings.EnableHumble)
            {
                items.Add(CreateStoreFilterItem("Humble", "Humble"));
            }

            if (_settings.EnableEpic)
            {
                items.Add(CreateStoreFilterItem("Epic Games Store", "Epic Games Store"));
            }

            if (_settings.EnableXbox)
            {
                items.Add(CreateStoreFilterItem("Microsoft Store", "Xbox"));
            }

            if (_settings.EnableUbisoft)
            {
                items.Add(CreateStoreFilterItem("Ubisoft Connect", "Ubisoft Connect"));
            }

            if (_settings.EnableOrigin)
            {
                items.Add(CreateStoreFilterItem("EA app", "EA app"));
            }

            items.Sort((x, y) => string.Compare(x.StoreName, y.StoreName, true));
            FilterStoreItems = items;
            Logger.Debug($"[ITAD][ui] storeFilter rebuilt items={items.Count}");
            ApplyWishlistUiFilters(_settings.WishlistUiFilters, applyStores: true);
        }

        private ListStore CreateStoreFilterItem(string storeName, string iconKey)
        {
            string icon = TransformIcon.Get(iconKey) ?? string.Empty;
            return new ListStore
            {
                StoreName = storeName,
                StoreIcon = icon,
                Count = CountWishlistItemsForStore(storeName),
                IsCheck = false
            };
        }

        private int CountWishlistItemsForStore(string storeName)
        {
            if (WishlistItems == null || string.IsNullOrEmpty(storeName))
            {
                return 0;
            }

            int count = 0;
            foreach (Wishlist wishlist in WishlistItems)
            {
                if (storeName.IsEqual(wishlist.StoreName))
                {
                    count++;
                    continue;
                }

                if (wishlist.Duplicates != null
                    && wishlist.Duplicates.Exists(d => storeName.IsEqual(d.StoreName)))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Restores wishlist UI filters from settings (without touching store checks unless <paramref name="applyStores"/>).
        /// </summary>
        public void ApplyWishlistUiFilters(WishlistUiFilters filters, bool applyStores)
        {
            if (filters == null)
            {
                filters = WishlistUiFilters.CreateDefaults();
            }

            _sliderFilterTimer.Stop();
            _suppressFilterPersist = true;
            try
            {
                ApplyLibraryMode(filters.IncludeInLibrary, filters.OnlyInLibrary, raiseFilter: false);

                _useBestPrice = filters.UseBestPrice;
                OnPropertyChanged(nameof(IsBestPriceMode));
                OnPropertyChanged(nameof(IsStorePriceMode));

                double minPrice = _settings.MinPrice;
                double maxPrice = _settings.MaxPrice;
                double priceLimit = filters.PriceLimit;
                if (priceLimit < minPrice)
                {
                    priceLimit = minPrice;
                }

                if (priceLimit > maxPrice)
                {
                    priceLimit = maxPrice;
                }

                DiscountPercent = filters.DiscountPercent;
                PriceLimit = priceLimit;

                _orderTypeTag = string.IsNullOrEmpty(filters.OrderTypeTag) ? "0" : filters.OrderTypeTag;
                OnPropertyChanged(nameof(OrderTypeTag));
                OrderDirectionTag = filters.OrderDirectionTag;
                UpdateOrderDirectionPresentation();

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

            ApplySortAndFilter();
        }

        /// <summary>
        /// Forces Alerts-only for a notification-opened view without scheduling a settings save.
        /// </summary>
        public void ApplyNotificationAlertsOverride()
        {
            if (!_notificationAlertsOverride)
            {
                return;
            }

            bool alreadyActive = _alertsOnly;
            _filterPersistTimer.Stop();
            _sliderFilterTimer.Stop();
            _suppressFilterPersist = true;
            try
            {
                _alertsOnly = true;
                OnPropertyChanged(nameof(AlertsOnly));
                if (!alreadyActive)
                {
                    Logger.Debug("[ITAD][filters] notification alerts override applied persist=false");
                }
            }
            finally
            {
                _suppressFilterPersist = false;
            }

            RefreshWishlistFilter();
        }

        /// <summary>Re-applies the current filter predicate without changing sort descriptions.</summary>
        public void RefreshWishlistFilter()
        {
            if (_wishlistView != null)
            {
                try
                {
                    _wishlistView.Filter = MatchesWishlistFilter;
                    _wishlistView.Refresh();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }
            }

            UpdateWishlistEmptyState();
        }

        /// <summary>Applies sort descriptions and filter to the attached wishlist view.</summary>
        public void ApplySortAndFilter()
        {
            if (_wishlistView != null)
            {
                try
                {
                    _wishlistView.SortDescriptions.Clear();

                    bool isAscending = OrderDirectionTag == "0";
                    ListSortDirection direction = isAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

                    switch (OrderTypeTag)
                    {
                        case "0":
                            _wishlistView.SortDescriptions.Add(new SortDescription("Name", direction));
                            break;
                        case "1":
                            _wishlistView.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceCut", direction));
                            break;
                        case "2":
                            _wishlistView.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceNew", direction));
                            break;
                        case "3":
                            _wishlistView.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceCut", direction));
                            break;
                        case "4":
                            _wishlistView.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceNew", direction));
                            break;
                        case "5":
                            _wishlistView.SortDescriptions.Add(new SortDescription("ReleaseDate", direction));
                            break;
                    }

                    _wishlistView.Filter = MatchesWishlistFilter;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }
            }

            UpdateWishlistEmptyState();
        }

        private void SetWishlistItems(ObservableCollection<Wishlist> items)
        {
            WishlistItems = items ?? new ObservableCollection<Wishlist>();
            _wishlistView = CollectionViewSource.GetDefaultView(WishlistItems);
            ApplySortAndFilter();
        }

        private ObservableCollection<Wishlist> LoadWishlistData(bool cacheOnly, bool forcePrice, Action<string> progress)
        {
            List<Wishlist> loaded = _api.LoadWishlist(_plugin, cacheOnly, forcePrice, progress);
            return loaded != null
                ? new ObservableCollection<Wishlist>(loaded)
                : new ObservableCollection<Wishlist>();
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

        private void UpdateWishlistEmptyState()
        {
            int sourceCount = WishlistItems != null ? WishlistItems.Count : 0;
            int filtered = 0;
            if (_wishlistView != null)
            {
                foreach (object unused in _wishlistView)
                {
                    filtered++;
                }
            }

            FilteredCount = filtered;

            if (IsWishlistLoading)
            {
                IsWishlistEmpty = false;
                IsWishlistNoResults = false;
                return;
            }

            if (sourceCount == 0)
            {
                IsWishlistEmpty = true;
                IsWishlistNoResults = false;
                Logger.Debug("[ITAD][ui] wishlistEmptyState mode=empty source=0");
            }
            else if (filtered == 0)
            {
                IsWishlistEmpty = false;
                IsWishlistNoResults = true;
                Logger.Debug($"[ITAD][ui] wishlistEmptyState mode=noResults source={sourceCount} filtered=0");
            }
            else
            {
                IsWishlistEmpty = false;
                IsWishlistNoResults = false;
            }
        }

        private void OpenUrl(string link)
        {
            if (link.IsNullOrEmpty())
            {
                Logger.Warn("[ITAD][ui] website click ignored link=empty");
                return;
            }

            Logger.Debug("[ITAD][ui] openUrl");
            _ = Process.Start(link);
        }

        private void HideWishlist(ItadGameInfo storePriceSelected)
        {
            if (storePriceSelected == null)
            {
                Logger.Warn("[ITAD][ui] hideWishlist ignored tag=invalid");
                return;
            }

            MessageBoxResult resultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadHideOnStoreWishList"), storePriceSelected.Name, storePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (resultDialog != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Logger.Info($"[ITAD][ui] hideWishlist shop={storePriceSelected.ShopName}");
                _settings.wishlistIgnores.Add(new WishlistIgnore
                {
                    StoreId = storePriceSelected.StoreId,
                    StoreName = storePriceSelected.ShopName,
                    Name = storePriceSelected.Name,
                    Id = storePriceSelected.Id,
                    Slug = storePriceSelected.Slug
                });
                _plugin.SavePluginSettings(_settings);
                RefreshWishlistFilter();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }
        }

        private void RemoveWishlist(ItadGameInfo storePriceSelected)
        {
            if (storePriceSelected == null)
            {
                Logger.Warn("[ITAD][ui] removeWishlist ignored tag=invalid");
                return;
            }

            MessageBoxResult resultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadDeleteOnStoreWishList"), storePriceSelected.Name, storePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (resultDialog != MessageBoxResult.Yes)
            {
                return;
            }

            Logger.Info($"[ITAD][ui] removeWishlist start shop={storePriceSelected.ShopName}");
            BeginWishlistBusy(blockInteraction: true);

            bool isDeleted = false;
            _ = Task.Run(() =>
            {
                try
                {
                    if (storePriceSelected.StoreId.IsNullOrEmpty())
                    {
                        Logger.Warn($"[ITAD][ui] removeWishlist skipped storeId=empty shop={storePriceSelected.ShopName}");
                        return;
                    }

                    switch (storePriceSelected.ShopName.ToLower())
                    {
                        case "steam":
                            SteamWishlist steamWishlist = new SteamWishlist(_plugin);
                            isDeleted = steamWishlist.RemoveWishlist(storePriceSelected.StoreId);
                            if (isDeleted)
                            {
                                _ = steamWishlist.GetWishlist(false);
                            }
                            break;

                        case "epic game store":
                            EpicWishlist epicWishlist = new EpicWishlist(_plugin);
                            isDeleted = epicWishlist.RemoveWishlist(storePriceSelected.StoreId);
                            if (isDeleted)
                            {
                                _ = epicWishlist.GetWishlist(false);
                            }
                            break;

                        case "humble store":
                            HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist(_plugin);
                            isDeleted = humbleBundleWishlist.RemoveWishlist(storePriceSelected.StoreId);
                            if (isDeleted)
                            {
                                _ = humbleBundleWishlist.GetWishlist(false);
                            }
                            break;

                        case "gog":
                            GogWishlist gogWishlist = new GogWishlist(_plugin);
                            isDeleted = gogWishlist.RemoveWishlist(storePriceSelected.StoreId);
                            if (isDeleted)
                            {
                                _ = gogWishlist.GetWishlist(false);
                            }
                            break;

                        case "microsoft store":
                        case "xbox":
                            Logger.Warn($"[ITAD][ui] removeWishlist unsupported shop={storePriceSelected.ShopName}");
                            break;

                        case "origin":
                        case "ea app":
                            EaWishlist originWishlist = new EaWishlist(_plugin);
                            isDeleted = originWishlist.RemoveWishlist(storePriceSelected.StoreId);
                            if (isDeleted)
                            {
                                _ = originWishlist.GetWishlist(false);
                            }
                            break;

                        default:
                            Logger.Warn($"[ITAD][ui] removeWishlist unsupported shop={storePriceSelected.ShopName}");
                            break;
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
                    Logger.Debug($"[ITAD][ui] removeWishlist done deleted={isDeleted} shop={storePriceSelected.ShopName}");
                    if (isDeleted)
                    {
                        _ = RefreshWishlistAsync(string.Empty);
                    }
                    else
                    {
                        EndWishlistBusy();
                    }
                }));
            });
        }

        private void ResetFilters()
        {
            WishlistUiFilters defaults = WishlistUiFilters.CreateDefaults();
            ApplyWishlistUiFilters(defaults, applyStores: true);

            _suppressFilterPersist = true;
            try
            {
                IncludeWithoutData = false;
                _alertsOnly = false;
                _notificationAlertsOverride = false;
                OnPropertyChanged(nameof(AlertsOnly));
            }
            finally
            {
                _suppressFilterPersist = false;
            }

            _settings.WishlistUiFilters = defaults.Clone();
            PersistWishlistUiFiltersNow();
            RefreshWishlistFilter();
            Logger.Info(
                $"[ITAD][filters] reset to defaults include={defaults.IncludeInLibrary} only={defaults.OnlyInLibrary}");
        }

        private void ToggleOrderDirection()
        {
            OrderDirectionTag = OrderDirectionTag == "0" ? "1" : "0";
            ApplySortAndFilter();
            ScheduleWishlistUiFiltersPersist();
        }

        private void SelectAllStores()
        {
            SetAllStoreChecks(true);
        }

        private void DeselectAllStores()
        {
            SetAllStoreChecks(false);
        }

        private void SetAllStoreChecks(bool isChecked)
        {
            if (FilterStoreItems == null || FilterStoreItems.Count == 0)
            {
                return;
            }

            _suppressFilterPersist = true;
            try
            {
                foreach (ListStore store in FilterStoreItems)
                {
                    store.IsCheck = isChecked;
                }

                RebuildSelectedStoresFromChecks();
            }
            finally
            {
                _suppressFilterPersist = false;
            }

            Logger.Debug($"[ITAD][ui] storeFilter selectAll={isChecked} count={_searchStores.Count}");
            OnPersistedFilterChanged();
        }

        private void ApplyLibraryMode(bool includeInLibrary, bool onlyInLibrary, bool raiseFilter = true)
        {
            _includeInLibrary = includeInLibrary;
            _onlyInLibrary = onlyInLibrary;
            OnPropertyChanged(nameof(IsLibraryExclude));
            OnPropertyChanged(nameof(IsLibraryInclude));
            OnPropertyChanged(nameof(IsLibraryOnly));

            if (raiseFilter && !_suppressFilterPersist)
            {
                Logger.Debug(
                    $"[ITAD][ui] libraryMode only={_onlyInLibrary} include={_includeInLibrary}");
                OnPersistedFilterChanged();
            }
        }

        private void OnPersistedFilterChanged()
        {
            RefreshWishlistFilter();
            ScheduleWishlistUiFiltersPersist();
        }

        private void OnSelfPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DiscountPercent) && e.PropertyName != nameof(PriceLimit))
            {
                return;
            }

            if (_suppressFilterPersist)
            {
                return;
            }

            _sliderFilterTimer.Stop();
            _sliderFilterTimer.Start();
        }

        private void HookStoreItems(List<ListStore> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (ListStore store in items)
            {
                store.PropertyChanged += Store_PropertyChanged;
            }
        }

        private void UnhookStoreItems(List<ListStore> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (ListStore store in items)
            {
                store.PropertyChanged -= Store_PropertyChanged;
            }
        }

        private void Store_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ListStore.IsCheck) || _suppressFilterPersist)
            {
                return;
            }

            RebuildSelectedStoresFromChecks();
            OnPersistedFilterChanged();
        }

        private void ApplyStoreFilterSelection(List<string> selectedStores)
        {
            _searchStores = new List<string>();
            UpdateSelectedStoresPresentation();

            List<string> selected = selectedStores ?? new List<string>();
            if (FilterStoreItems == null)
            {
                return;
            }

            foreach (ListStore store in FilterStoreItems)
            {
                store.IsCheck = selected.Exists(x => x.IsEqual(store.StoreName));
            }

            RebuildSelectedStoresFromChecks();
        }

        private void RebuildSelectedStoresFromChecks()
        {
            if (FilterStoreItems == null)
            {
                _searchStores = new List<string>();
                UpdateSelectedStoresPresentation();
                return;
            }

            _searchStores = FilterStoreItems
                .Where(x => x.IsCheck)
                .Select(x => x.StoreName)
                .ToList();
            UpdateSelectedStoresPresentation();
        }

        private void UpdateSelectedStoresPresentation()
        {
            if (_searchStores == null || _searchStores.Count == 0)
            {
                string allLabel = ResourceProvider.GetString("LOCItadFilterStoresAll");
                SelectedStoresText = allLabel;
                SelectedStoresToolTip = allLabel;
                return;
            }

            if (_searchStores.Count == 1)
            {
                string single = _searchStores[0];
                SelectedStoresText = single;
                SelectedStoresToolTip = single;
                return;
            }

            SelectedStoresText = string.Format(
                ResourceProvider.GetString("LOCItadFilterStoresCount"),
                _searchStores.Count);
            SelectedStoresToolTip = string.Join(", ", _searchStores);
        }

        private void UpdateOrderDirectionPresentation()
        {
            bool ascending = OrderDirectionTag == "0";
            OrderDirectionGlyph = ascending ? "\uEA99" : "\uEAA1";
            OrderDirectionToolTip = ResourceProvider.GetString(
                ascending ? "LOCMenuSortAscending" : "LOCMenuSortDescending");
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
                _settings.WishlistUiFilters = CaptureWishlistUiFilters();
                _plugin.SavePluginSettings(_settings);
                Logger.Debug(
                    $"[ITAD][filters] persisted include={_settings.WishlistUiFilters.IncludeInLibrary} only={_settings.WishlistUiFilters.OnlyInLibrary} stores={_settings.WishlistUiFilters.SelectedStores?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ITAD][filters] persist failed");
            }
        }

        private WishlistUiFilters CaptureWishlistUiFilters()
        {
            return new WishlistUiFilters
            {
                IncludeInLibrary = _includeInLibrary && !_onlyInLibrary,
                OnlyInLibrary = _onlyInLibrary,
                UseBestPrice = _useBestPrice,
                DiscountPercent = DiscountPercent,
                PriceLimit = PriceLimit,
                SelectedStores = _searchStores != null ? new List<string>(_searchStores) : new List<string>(),
                OrderTypeTag = OrderTypeTag,
                OrderDirectionTag = OrderDirectionTag
            };
        }

        private bool MatchesWishlistFilter(object item)
        {
            Wishlist wishlist = item as Wishlist;
            if (wishlist == null)
            {
                return false;
            }

            bool matchesSearch = SearchText.IsNullOrEmpty()
                || wishlist.Name.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase);
            bool matchesStore = _searchStores.Count == 0
                || _searchStores.Contains(wishlist.StoreName)
                || wishlist.Duplicates.FindAll(y => _searchStores.Contains(y.StoreName)).Count > 0;
            bool notIgnored = _settings.wishlistIgnores.All(y => y.StoreId != wishlist.StoreId && !y.Name.IsEqual(wishlist.Name));
            bool matchesDiscount = _useBestPrice
                ? wishlist.ItadBestPrice.PriceCut >= DiscountPercent
                : wishlist.ItadPriceForWishlistStore.PriceCut >= DiscountPercent;
            bool matchesPriceLimit = _useBestPrice
                ? wishlist.ItadBestPrice.PriceNew <= PriceLimit
                : wishlist.ItadPriceForWishlistStore.PriceNew <= PriceLimit;

            bool matchesLibraryOptions;
            if (_onlyInLibrary)
            {
                matchesLibraryOptions = wishlist.InLibrary;
            }
            else if (!_includeInLibrary)
            {
                matchesLibraryOptions = !wishlist.InLibrary;
            }
            else
            {
                matchesLibraryOptions = true;
            }

            bool matchesItadData = wishlist.HasItadData || IncludeWithoutData;
            bool matchesAlert = !AlertsOnly || wishlist.GetNotification(_settings.NotificationCriterias);

            return matchesDiscount
                && matchesPriceLimit
                && matchesSearch
                && matchesStore
                && notIgnored
                && matchesLibraryOptions
                && matchesItadData
                && matchesAlert;
        }
    }

    /// <summary>
    /// One store row in the wishlist source filter (checkbox, glyph, name, count).
    /// </summary>
    public class ListStore : ObservableObject
    {
        private string _storeName;
        private string _storeIcon;
        private int _count;
        private bool _isCheck;

        /// <summary>Canonical store name used by filter matching (e.g. Steam).</summary>
        public string StoreName
        {
            get => _storeName;
            set => SetValue(ref _storeName, value);
        }

        /// <summary>Store glyph for the filter row (<c>CommonFont</c>).</summary>
        public string StoreIcon
        {
            get => _storeIcon;
            set => SetValue(ref _storeIcon, value ?? string.Empty);
        }

        /// <summary>Wishlist row count for this store (including duplicates).</summary>
        public int Count
        {
            get => _count;
            set
            {
                if (_count == value)
                {
                    return;
                }

                SetValue(ref _count, value);
            }
        }

        /// <summary>Whether this store is included in the active filter.</summary>
        public bool IsCheck
        {
            get => _isCheck;
            set => SetValue(ref _isCheck, value);
        }
    }
}
