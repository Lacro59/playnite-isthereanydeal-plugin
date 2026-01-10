using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models.Api;
using CommonPluginsShared.Extensions;
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

        [DontSerialize]
        public List<Country> Countries => IsThereAnyDealApi.GetCountries();
        public Country CountrySelected { get; set; } = new Country { Alpha2 = "US", Alpha3 = "USA", M49 = "003", Name = "United States of America", Currency = "USD", RCurrency = "USD" };
        public List<ItadShops> Stores { get; set; } = new List<ItadShops>();

        public bool EnableSteam { get; set; } = false;
        public bool EnableGog { get; set; } = false;
        public bool EnableHumble { get; set; } = false;
        public bool EnableEpic { get; set; } = false;
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

        public StoreSettings SteamStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = false };
        [DontSerialize]
        public StoreSettings EpicStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = true };
        [DontSerialize]
        public StoreSettings GogStoreSettings { get; set; } = new StoreSettings { UseAuth = true, ForceAuth = true };
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        #endregion  
    }


    public class IsThereAnyDealSettingsViewModel : ObservableObject, ISettings
    {
        private IsThereAnyDeal Plugin { get; }
        private IsThereAnyDealSettings EditingClone { get; set; }

        private IsThereAnyDealSettings _settings;
        public IsThereAnyDealSettings Settings { get => _settings; set => SetValue(ref _settings, value); }


        public IsThereAnyDealSettingsViewModel(IsThereAnyDeal plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            IsThereAnyDealSettings savedSettings = plugin.LoadPluginSettings<IsThereAnyDealSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new IsThereAnyDealSettings();

            // TEMP
            if (Settings.Stores.Count > 0)
            {
                if (!int.TryParse(Settings.Stores.First().Id, out int i))
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    List<ServiceShop> serviceShops = isThereAnyDealApi.GetServiceShops(Settings.CountrySelected.Alpha2).GetAwaiter().GetResult();
                    List<ItadShops> itadShops = serviceShops?.Select(x => new ItadShops
                    {
                        Id = x.Id.ToString(),
                        Title = x.Title,
                        IsCheck = Settings.Stores.Where(y => y.Title.IsEqual(x.Title))?.FirstOrDefault()?.IsCheck ?? false,
                        Color = Settings.Stores.Where(y => y.Title.IsEqual(x.Title))?.FirstOrDefault()?.Color ?? ResourceProvider.GetResource("TextBrush").ToString(),
                    }).ToList();
                    Settings.Stores = itadShops;
                }
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
            Settings = EditingClone;
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
			// StoreAPI intialization
			IsThereAnyDeal.SteamApi.SaveSettings(Settings.SteamStoreSettings, Settings.PluginState.SteamIsEnabled && Settings.EnableSteam);
			IsThereAnyDeal.EpicApi.SaveSettings(Settings.EpicStoreSettings, Settings.PluginState.EpicIsEnabled && Settings.EnableEpic);
			IsThereAnyDeal.GogApi.SaveSettings(Settings.GogStoreSettings, Settings.PluginState.GogIsEnabled && Settings.EnableGog);

            Plugin.SavePluginSettings(Settings);

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
