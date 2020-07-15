using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Playnite.SDK;
using System.Collections.Generic;


namespace IsThereAnyDeal
{
    public class IsThereAnyDealSettings : ISettings
    {
        private readonly IsThereAnyDeal plugin;

        public string Region { get; set; } = "us";
        public string Country { get; set; } = "US";
        public string CurrencySign { get; set; } = "$";
        public List<ItadStore> Stores { get; set; } = new List<ItadStore>();

        public bool EnableSteam { get; set; } = false;
        public bool EnableGog { get; set; } = false;
        public bool EnableHumble { get; set; } = false;

        public bool EnableNotification { get; set; } = false;
        public int LimitNotification { get; set; } = 50;

        public string HumbleKey { get; set; } = "";


        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonIgnore` ignore attribute.
        [JsonIgnore]
        public bool OptionThatWontBeSaved { get; set; } = false;

        // Parameterless constructor must exist if you want to use LoadPluginSettings method.
        public IsThereAnyDealSettings()
        {
        }

        public IsThereAnyDealSettings(IsThereAnyDeal plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<IsThereAnyDealSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Region = savedSettings.Region;
                Country = savedSettings.Country;
                CurrencySign = savedSettings.CurrencySign;
                Stores = savedSettings.Stores;

                EnableSteam = savedSettings.EnableSteam;
                EnableGog = savedSettings.EnableGog;
                EnableHumble = savedSettings.EnableHumble;

                EnableNotification = savedSettings.EnableNotification;
                LimitNotification = savedSettings.LimitNotification;

                HumbleKey = savedSettings.HumbleKey;
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}
