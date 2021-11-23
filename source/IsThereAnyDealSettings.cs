using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;

namespace IsThereAnyDeal
{
    public class IsThereAnyDealSettings : ObservableObject
    {
        #region Settings variables
        public bool MenuInExtensions { get; set; } = true;
        public bool EnableIntegrationButtonHeader { get; set; } = false;
        public bool EnableIntegrationButtonSide { get; set; } = true;

        public List<WishlistIgnore> wishlistIgnores { get; set; } = new List<WishlistIgnore>();

        public string Region { get; set; } = "us";
        public string Country { get; set; } = "US";
        public string CurrencySign { get; set; } = "$";
        public List<ItadStore> Stores { get; set; } = new List<ItadStore>();

        public bool EnableSteam { get; set; } = false;
        public bool EnableGog { get; set; } = false;
        public bool EnableHumble { get; set; } = false;
        public bool EnableEpic { get; set; } = false;
        public bool EnableXbox { get; set; } = true;
        public bool EnableOrigin { get; set; } = true;

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

        public List<ItadNotificationCriteria> NotificationCriterias { get; set; } = new List<ItadNotificationCriteria>();
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        #endregion  
    }


    public class IsThereAnyDealSettingsViewModel : ObservableObject, ISettings
    {
        private readonly IsThereAnyDeal Plugin;
        private IsThereAnyDealSettings EditingClone { get; set; }

        private IsThereAnyDealSettings _Settings;
        public IsThereAnyDealSettings Settings
        {
            get => _Settings;
            set
            {
                _Settings = value;
                OnPropertyChanged();
            }
        }


        public IsThereAnyDealSettingsViewModel(IsThereAnyDeal plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<IsThereAnyDealSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new IsThereAnyDealSettings();
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
            Plugin.SavePluginSettings(Settings);
            Plugin.topPanelItem.Visible = Settings.EnableIntegrationButtonHeader;
            Plugin.itadViewSidebar.Visible = Settings.EnableIntegrationButtonSide;
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
