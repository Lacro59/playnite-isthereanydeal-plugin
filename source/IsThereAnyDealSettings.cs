using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models.Api;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using IsThereAnyDeal.Models.ApiWebsite;
using CommonPluginsShared.Plugins;
using CommonPluginsStores.Models;

namespace IsThereAnyDeal
{
    public class IsThereAnyDealSettings : PluginSettings
    {
        #region Settings variables
        public bool EnableIntegrationButtonHeader { get; set; } = false;
        public bool EnableIntegrationButtonSide { get; set; } = true;

        public DateTime? LastRefresh { get; set; }

        public List<WishlistIgnore> wishlistIgnores { get; set; } = new List<WishlistIgnore>();

        /// <summary>Wishlist panel filter UI state (not exposed in Settings view).</summary>
        public WishlistUiFilters WishlistUiFilters { get; set; } = WishlistUiFilters.CreateDefaults();

        /// <summary>Whether the wishlist filters Expander is expanded (independent of WishlistUiFilters reset).</summary>
        public bool WishlistFiltersPanelExpanded { get; set; } = true;

        [DontSerialize]
        public List<Country> Countries => IsThereAnyDealApi.GetCountries();
        public Country CountrySelected { get; set; } = new Country { Alpha2 = "US", Alpha3 = "USA", M49 = "003", Name = "United States of America", Currency = "USD", RCurrency = "USD" };
        public List<ItadShops> Stores { get; set; } = new List<ItadShops>();

        private bool _enableSteam;
        public bool EnableSteam { get => _enableSteam; set => SetValue(ref _enableSteam, value); }

        private bool _enableGog;
        public bool EnableGog { get => _enableGog; set => SetValue(ref _enableGog, value); }

        public bool EnableHumble { get; set; } = false;

        private bool _enableEpic;
        public bool EnableEpic { get => _enableEpic; set => SetValue(ref _enableEpic, value); }

        public bool EnableXbox { get; set; } = true;
        public bool EnableUbisoft { get; set; } = true;
        [DontSerialize]
        public bool EnableOrigin => false;

        public bool EnableNotificationGiveaways { get; set; } = false;
        public bool EnableNotification { get; set; } = false;
        public bool EnableNotificationPercentage { get; set; } = false;
        public int LimitNotification { get; set; } = 50;
        public bool EnableNotificationPrice { get; set; } = false;
        public int LimitNotificationPrice { get; set; } = 5;

        public int MinPrice { get; set; } = 1;
        public int MaxPrice { get; set; } = 100;

        public string HumbleKey { get; set; } = string.Empty;
        public string XboxLink { get; set; } = string.Empty;
        public string UbisoftLink { get; set; } = string.Empty;

        public List<ItadNotificationCriteria> NotificationCriterias { get; set; } = new List<ItadNotificationCriteria>();

        public PluginUpdate UpdateWishlist { get; set; } = new PluginUpdate();
        public PluginUpdate UpdatePrice { get; set; } = new PluginUpdate();

        /// <summary>Steam auth options — ITAD defaults (ForceAuth off, UseAuth on); not aligned on PA ForceAuth=true.</summary>
        public StoreSettings SteamStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = false };

        public StoreSettings EpicStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = true };

        public StoreSettings GogStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = true };
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        #endregion
    }


    public class IsThereAnyDealSettingsViewModel : PluginSettingsViewModel, IPluginSettingsViewModel
    {
        public IsThereAnyDeal Plugin { get; }
        private IsThereAnyDealSettings EditingClone { get; set; }

        private IsThereAnyDealSettings _settings;
        public IsThereAnyDealSettings Settings { get => _settings; set => SetValue(ref _settings, value); }

        IPluginSettings IPluginSettingsViewModel.Settings => Settings;


        public IsThereAnyDealSettingsViewModel(IsThereAnyDeal plugin)
        {
            Logger.Info("[Settings][startup] ctor entered");

            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            IsThereAnyDealSettings savedSettings = plugin.LoadPluginSettings<IsThereAnyDealSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new IsThereAnyDealSettings();
            EnsureStoreSettingsDefaults(Settings);
            EnsureWishlistUiFiltersDefaults(Settings);
            Logger.Info($"[Settings][startup] settings loaded hasSaved={(savedSettings != null)} storesCount={Settings.Stores?.Count ?? 0}");

            // Legacy store Id migration must not block plugin load (sync-over-async hang risk).
            if (NeedsLegacyStoresIdMigration())
            {
                Logger.Info($"[Settings][startup] legacy_stores_migration deferred country={Settings.CountrySelected?.Alpha2} storesCount={Settings.Stores.Count}");
            }

            Logger.Info("[Settings][startup] ctor completed");
        }

        /// <summary>
        /// Fills missing <see cref="StoreSettings"/> after load (e.g. former <c>DontSerialize</c> Epic/Gog).
        /// </summary>
        private static void EnsureStoreSettingsDefaults(IsThereAnyDealSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.SteamStoreSettings == null)
            {
                settings.SteamStoreSettings = new StoreSettings { UseAuth = true, ForceAuth = false };
                Logger.Debug("[Settings][stores] SteamStoreSettings defaulted after load");
            }

            if (settings.EpicStoreSettings == null)
            {
                settings.EpicStoreSettings = new StoreSettings { UseAuth = true, ForceAuth = true };
                Logger.Debug("[Settings][stores] EpicStoreSettings defaulted after load");
            }

            if (settings.GogStoreSettings == null)
            {
                settings.GogStoreSettings = new StoreSettings { UseAuth = true, ForceAuth = true };
                Logger.Debug("[Settings][stores] GogStoreSettings defaulted after load");
            }
        }

        /// <summary>
        /// Ensures <see cref="WishlistUiFilters"/> exists after load (older settings JSON).
        /// </summary>
        private static void EnsureWishlistUiFiltersDefaults(IsThereAnyDealSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.WishlistUiFilters == null)
            {
                settings.WishlistUiFilters = WishlistUiFilters.CreateDefaults();
                Logger.Debug("[Settings][filters] WishlistUiFilters defaulted after load");
                return;
            }

            if (settings.WishlistUiFilters.SelectedStores == null)
            {
                settings.WishlistUiFilters.SelectedStores = new List<string>();
            }

            if (string.IsNullOrEmpty(settings.WishlistUiFilters.OrderTypeTag))
            {
                settings.WishlistUiFilters.OrderTypeTag = "0";
            }

            if (string.IsNullOrEmpty(settings.WishlistUiFilters.OrderDirectionTag))
            {
                settings.WishlistUiFilters.OrderDirectionTag = "0";
            }
        }

        /// <summary>
        /// Returns true when persisted shop entries still use non-numeric Ids and need remapping against the ITAD shops API.
        /// </summary>
        internal bool NeedsLegacyStoresIdMigration()
        {
            return Settings?.Stores != null
                && Settings.Stores.Count > 0
                && !int.TryParse(Settings.Stores.First().Id, out _);
        }

        /// <summary>
        /// Remaps legacy shop Ids asynchronously. Call only from a background path (for example <c>OnApplicationStarted</c>), never from a constructor.
        /// </summary>
        internal async Task MigrateLegacyStoresIdsAsync()
        {
            if (!NeedsLegacyStoresIdMigration())
            {
                return;
            }

            string country = Settings.CountrySelected?.Alpha2 ?? "US";
            Logger.Info($"[Settings][migrate] legacy_stores started country={country} storesCount={Settings.Stores.Count}");

            try
            {
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                List<ServiceShop> serviceShops = await isThereAnyDealApi.GetServiceShops(country).ConfigureAwait(false);
                if (serviceShops == null)
                {
                    Logger.Warn($"[Settings][migrate] legacy_stores no shops from API country={country}");
                    return;
                }

                List<ItadShops> itadShops = serviceShops.Select(x => new ItadShops
                {
                    Id = x.Id.ToString(),
                    Title = x.Title,
                    IsCheck = Settings.Stores.Where(y => y.Title.IsEqual(x.Title))?.FirstOrDefault()?.IsCheck ?? false,
                    Color = Settings.Stores.Where(y => y.Title.IsEqual(x.Title))?.FirstOrDefault()?.Color ?? ResourceProvider.GetResource("TextBrush").ToString(),
                }).ToList();

                Settings.Stores = itadShops;
                Plugin.SavePluginSettings(Settings);
                Logger.Info($"[Settings][migrate] legacy_stores completed shopsCount={Settings.Stores.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[Settings][migrate] legacy_stores failed");
            }
        }

        // Code executed when settings view is opened and user starts editing values.
        public void BeginEdit()
        {
            EditingClone = Serialization.GetClone(Settings);
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            CopySettingsValues(EditingClone, Settings);
            OnPropertyChanged(nameof(Settings));
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            EnsureStoreSettingsDefaults(Settings);

            // Persist store auth flags into live StoreApi instances when already constructed.
            if (IsThereAnyDeal.SteamApi != null)
            {
                IsThereAnyDeal.SteamApi.SaveSettings(Settings.SteamStoreSettings, Settings.PluginState.SteamIsEnabled && Settings.EnableSteam);
            }
            else
            {
                Logger.Debug("[Settings][stores] SteamApi SaveSettings skipped reason=not_ready");
            }

            if (IsThereAnyDeal.EpicApi != null)
            {
                IsThereAnyDeal.EpicApi.SaveSettings(Settings.EpicStoreSettings, Settings.PluginState.EpicIsEnabled && Settings.EnableEpic);
            }
            else
            {
                Logger.Debug("[Settings][stores] EpicApi SaveSettings skipped reason=not_ready");
            }

            if (IsThereAnyDeal.GogApi != null)
            {
                IsThereAnyDeal.GogApi.SaveSettings(Settings.GogStoreSettings, Settings.PluginState.GogIsEnabled && Settings.EnableGog);
            }
            else
            {
                Logger.Debug("[Settings][stores] GogApi SaveSettings skipped reason=not_ready");
            }

            PersistSettings(Plugin, Settings);

            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                Plugin.TopPanelItem.Visible = Settings.EnableIntegrationButtonHeader;
                Plugin.SidebarItem.Visible = Settings.EnableIntegrationButtonSide;
            }
        }

        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
