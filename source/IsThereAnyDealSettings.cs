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

namespace IsThereAnyDeal
{
    public class IsThereAnyDealSettings : ObservableObject
    {
        #region Settings variables
        public bool MenuInExtensions { get; set; } = true;
        public bool EnableIntegrationButtonHeader { get; set; } = false;
        public bool EnableIntegrationButtonSide { get; set; } = true;

        public DateTime? LastRefresh { get; set; }

        public List<WishlistIgnore> wishlistIgnores { get; set; } = new List<WishlistIgnore>();

        public List<Country> Countries { get; set; } = new List<Country>();
        public Country CountrySelected { get; set; } = new Country { Alpha2 = "US", Name = "United States of America", Currency = "USD" };
        public List<ItadShops> Stores { get; set; } = new List<ItadShops>();

        public bool EnableSteam { get; set; } = false;
        public bool EnableGog { get; set; } = false;
        public bool EnableHumble { get; set; } = false;
        public bool EnableEpic { get; set; } = false;
        public bool EnableXbox { get; set; } = true;
        public bool EnableUbisoft { get; set; } = true;
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
        public string UbisoftLink { get; set; } = string.Empty;

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
        public IsThereAnyDealSettings Settings { get => _Settings; set => SetValue(ref _Settings, value); }


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
                        Color = Settings.Stores.Where(y => y.Title.IsEqual(x.Title))?.FirstOrDefault()?.Color ?? string.Empty,
                    }).ToList();
                    Settings.Stores = itadShops;
                }
            }

            if (Settings.Countries.Count == 0)
            {
                Settings.Countries = new List<Country>
                {
                    new Country { Alpha2 = "AF", Name = "Afghanistan", Currency = "AFN" },
                    new Country { Alpha2 = "AX", Name = "Åland Islands", Currency = "EUR" },
                    new Country { Alpha2 = "AL", Name = "Albania", Currency = "ALL" },
                    new Country { Alpha2 = "DZ", Name = "Algeria", Currency = "DZD" },
                    new Country { Alpha2 = "AS", Name = "American Samoa", Currency = "USD" },
                    new Country { Alpha2 = "AD", Name = "Andorra", Currency = "EUR" },
                    new Country { Alpha2 = "AO", Name = "Angola", Currency = "AOA" },
                    new Country { Alpha2 = "AI", Name = "Anguilla", Currency = "XCD" },
                    new Country { Alpha2 = "AQ", Name = "Antarctica", Currency = "ARS" },
                    new Country { Alpha2 = "AG", Name = "Antigua and Barbuda", Currency = "XCD" },
                    new Country { Alpha2 = "AR", Name = "Argentina", Currency = "ARS" },
                    new Country { Alpha2 = "AM", Name = "Armenia", Currency = "AMD" },
                    new Country { Alpha2 = "AW", Name = "Aruba", Currency = "AWG" },
                    new Country { Alpha2 = "AU", Name = "Australia", Currency = "AUD" },
                    new Country { Alpha2 = "AT", Name = "Austria", Currency = "EUR" },
                    new Country { Alpha2 = "AZ", Name = "Azerbaijan", Currency = "AZN" },
                    new Country { Alpha2 = "BS", Name = "Bahamas", Currency = "BSD" },
                    new Country { Alpha2 = "BH", Name = "Bahrain", Currency = "BHD" },
                    new Country { Alpha2 = "BD", Name = "Bangladesh", Currency = "BDT" },
                    new Country { Alpha2 = "BB", Name = "Barbados", Currency = "BBD" },
                    new Country { Alpha2 = "BY", Name = "Belarus", Currency = "BYN" },
                    new Country { Alpha2 = "BE", Name = "Belgium", Currency = "EUR" },
                    new Country { Alpha2 = "BZ", Name = "Belize", Currency = "BZD" },
                    new Country { Alpha2 = "BJ", Name = "Benin", Currency = "XOF" },
                    new Country { Alpha2 = "BM", Name = "Bermuda", Currency = "BMD" },
                    new Country { Alpha2 = "BT", Name = "Bhutan", Currency = "BTN" },
                    new Country { Alpha2 = "BO", Name = "Bolivia (Plurinational State of)", Currency = "BOB" },
                    new Country { Alpha2 = "BQ", Name = "Bonaire, Sint Eustatius and Saba", Currency = "USD" },
                    new Country { Alpha2 = "BA", Name = "Bosnia and Herzegovina", Currency = "BAM" },
                    new Country { Alpha2 = "BW", Name = "Botswana", Currency = "BWP" },
                    new Country { Alpha2 = "BV", Name = "Bouvet Island", Currency = "NOK" },
                    new Country { Alpha2 = "BR", Name = "Brazil", Currency = "BRL" },
                    new Country { Alpha2 = "IO", Name = "British Indian Ocean Territory", Currency = "GBP" },
                    new Country { Alpha2 = "BN", Name = "Brunei Darussalam", Currency = "BND" },
                    new Country { Alpha2 = "BG", Name = "Bulgaria", Currency = "BGN" },
                    new Country { Alpha2 = "BF", Name = "Burkina Faso", Currency = "XOF" },
                    new Country { Alpha2 = "BI", Name = "Burundi", Currency = "BIF" },
                    new Country { Alpha2 = "CV", Name = "Cabo Verde", Currency = "CVE" },
                    new Country { Alpha2 = "KH", Name = "Cambodia", Currency = "KHR" },
                    new Country { Alpha2 = "CM", Name = "Cameroon", Currency = "XAF" },
                    new Country { Alpha2 = "CA", Name = "Canada", Currency = "CAD" },
                    new Country { Alpha2 = "KY", Name = "Cayman Islands", Currency = "KYD" },
                    new Country { Alpha2 = "CF", Name = "Central African Republic", Currency = "XAF" },
                    new Country { Alpha2 = "TD", Name = "Chad", Currency = "XAF" },
                    new Country { Alpha2 = "CL", Name = "Chile", Currency = "CLP" },
                    new Country { Alpha2 = "CN", Name = "China", Currency = "CNY" },
                    new Country { Alpha2 = "CX", Name = "Christmas Island", Currency = "AUD" },
                    new Country { Alpha2 = "CC", Name = "Cocos (Keeling) Islands", Currency = "AUD" },
                    new Country { Alpha2 = "CO", Name = "Colombia", Currency = "COP" },
                    new Country { Alpha2 = "KM", Name = "Comoros", Currency = "KMF" },
                    new Country { Alpha2 = "CG", Name = "Congo", Currency = "XAF" },
                    new Country { Alpha2 = "CD", Name = "Congo (Democratic Republic of the)", Currency = "CDF" },
                    new Country { Alpha2 = "CK", Name = "Cook Islands", Currency = "NZD" },
                    new Country { Alpha2 = "CR", Name = "Costa Rica", Currency = "CRC" },
                    new Country { Alpha2 = "CI", Name = "Côte d'Ivoire", Currency = "XOF" },
                    new Country { Alpha2 = "HR", Name = "Croatia", Currency = "HRK" },
                    new Country { Alpha2 = "CU", Name = "Cuba", Currency = "CUC" },
                    new Country { Alpha2 = "CW", Name = "Curaçao", Currency = "ANG" },
                    new Country { Alpha2 = "CY", Name = "Cyprus", Currency = "EUR" },
                    new Country { Alpha2 = "CZ", Name = "Czechia", Currency = "CZK" },
                    new Country { Alpha2 = "DK", Name = "Denmark", Currency = "DKK" },
                    new Country { Alpha2 = "DJ", Name = "Djibouti", Currency = "DJF" },
                    new Country { Alpha2 = "DM", Name = "Dominica", Currency = "XCD" },
                    new Country { Alpha2 = "DO", Name = "Dominican Republic", Currency = "DOP" },
                    new Country { Alpha2 = "EC", Name = "Ecuador", Currency = "USD" },
                    new Country { Alpha2 = "EG", Name = "Egypt", Currency = "EGP" },
                    new Country { Alpha2 = "SV", Name = "El Salvador", Currency = "USD" },
                    new Country { Alpha2 = "GQ", Name = "Equatorial Guinea", Currency = "XAF" },
                    new Country { Alpha2 = "ER", Name = "Eritrea", Currency = "ERN" },
                    new Country { Alpha2 = "EE", Name = "Estonia", Currency = "EUR" },
                    new Country { Alpha2 = "ET", Name = "Ethiopia", Currency = "ETB" },
                    new Country { Alpha2 = "SZ", Name = "Eswatini", Currency = "SZL" },
                    new Country { Alpha2 = "FK", Name = "Falkland Islands (Malvinas)", Currency = "FKP" },
                    new Country { Alpha2 = "FO", Name = "Faroe Islands", Currency = "DKK" },
                    new Country { Alpha2 = "FJ", Name = "Fiji", Currency = "FJD" },
                    new Country { Alpha2 = "FI", Name = "Finland", Currency = "EUR" },
                    new Country { Alpha2 = "FR", Name = "France", Currency = "EUR" },
                    new Country { Alpha2 = "GF", Name = "French Guiana", Currency = "EUR" },
                    new Country { Alpha2 = "PF", Name = "French Polynesia", Currency = "XPF" },
                    new Country { Alpha2 = "TF", Name = "French Southern Territories", Currency = "EUR" },
                    new Country { Alpha2 = "GA", Name = "Gabon", Currency = "XAF" },
                    new Country { Alpha2 = "GM", Name = "Gambia", Currency = "GMD" },
                    new Country { Alpha2 = "GE", Name = "Georgia", Currency = "GEL" },
                    new Country { Alpha2 = "DE", Name = "Germany", Currency = "EUR" },
                    new Country { Alpha2 = "GH", Name = "Ghana", Currency = "GHS" },
                    new Country { Alpha2 = "GI", Name = "Gibraltar", Currency = "GIP" },
                    new Country { Alpha2 = "GR", Name = "Greece", Currency = "EUR" },
                    new Country { Alpha2 = "GL", Name = "Greenland", Currency = "DKK" },
                    new Country { Alpha2 = "GD", Name = "Grenada", Currency = "XCD" },
                    new Country { Alpha2 = "GP", Name = "Guadeloupe", Currency = "EUR" },
                    new Country { Alpha2 = "GU", Name = "Guam", Currency = "USD" },
                    new Country { Alpha2 = "GT", Name = "Guatemala", Currency = "GTQ" },
                    new Country { Alpha2 = "GG", Name = "Guernsey", Currency = "GBP" },
                    new Country { Alpha2 = "GN", Name = "Guinea", Currency = "GNF" },
                    new Country { Alpha2 = "GW", Name = "Guinea-Bissau", Currency = "XOF" },
                    new Country { Alpha2 = "GY", Name = "Guyana", Currency = "GYD" },
                    new Country { Alpha2 = "HT", Name = "Haiti", Currency = "HTG" },
                    new Country { Alpha2 = "HM", Name = "Heard Island and McDonald Islands", Currency = "AUD" },
                    new Country { Alpha2 = "VA", Name = "Holy See", Currency = "EUR" },
                    new Country { Alpha2 = "HN", Name = "Honduras", Currency = "HNL" },
                    new Country { Alpha2 = "HK", Name = "Hong Kong", Currency = "HKD" },
                    new Country { Alpha2 = "HU", Name = "Hungary", Currency = "HUF" },
                    new Country { Alpha2 = "IS", Name = "Iceland", Currency = "ISK" },
                    new Country { Alpha2 = "IN", Name = "India", Currency = "INR" },
                    new Country { Alpha2 = "ID", Name = "Indonesia", Currency = "IDR" },
                    new Country { Alpha2 = "IR", Name = "Iran (Islamic Republic of)", Currency = "IRR" },
                    new Country { Alpha2 = "IQ", Name = "Iraq", Currency = "IQD" },
                    new Country { Alpha2 = "IE", Name = "Ireland", Currency = "EUR" },
                    new Country { Alpha2 = "IM", Name = "Isle of Man", Currency = "GBP" },
                    new Country { Alpha2 = "IL", Name = "Israel", Currency = "ILS" },
                    new Country { Alpha2 = "IT", Name = "Italy", Currency = "EUR" },
                    new Country { Alpha2 = "JM", Name = "Jamaica", Currency = "JMD" },
                    new Country { Alpha2 = "JP", Name = "Japan", Currency = "JPY" },
                    new Country { Alpha2 = "JE", Name = "Jersey", Currency = "GBP" },
                    new Country { Alpha2 = "JO", Name = "Jordan", Currency = "JOD" },
                    new Country { Alpha2 = "KZ", Name = "Kazakhstan", Currency = "KZT" },
                    new Country { Alpha2 = "KE", Name = "Kenya", Currency = "KES" },
                    new Country { Alpha2 = "KI", Name = "Kiribati", Currency = "AUD" },
                    new Country { Alpha2 = "KP", Name = "Korea (Democratic People's Republic of)", Currency = "KPW" },
                    new Country { Alpha2 = "KR", Name = "Korea (Republic of)", Currency = "KRW" },
                    new Country { Alpha2 = "KW", Name = "Kuwait", Currency = "KWD" },
                    new Country { Alpha2 = "KG", Name = "Kyrgyzstan", Currency = "KGS" },
                    new Country { Alpha2 = "LA", Name = "Lao People's Democratic Republic", Currency = "LAK" },
                    new Country { Alpha2 = "LV", Name = "Latvia", Currency = "EUR" },
                    new Country { Alpha2 = "LB", Name = "Lebanon", Currency = "LBP" },
                    new Country { Alpha2 = "LS", Name = "Lesotho", Currency = "LSL" },
                    new Country { Alpha2 = "LR", Name = "Liberia", Currency = "LRD" },
                    new Country { Alpha2 = "LY", Name = "Libya", Currency = "LYD" },
                    new Country { Alpha2 = "LI", Name = "Liechtenstein", Currency = "CHF" },
                    new Country { Alpha2 = "LT", Name = "Lithuania", Currency = "EUR" },
                    new Country { Alpha2 = "LU", Name = "Luxembourg", Currency = "EUR" },
                    new Country { Alpha2 = "MO", Name = "Macao", Currency = "MOP" },
                    new Country { Alpha2 = "MK", Name = "North Macedonia", Currency = "MKD" },
                    new Country { Alpha2 = "MG", Name = "Madagascar", Currency = "MGA" },
                    new Country { Alpha2 = "MW", Name = "Malawi", Currency = "MWK" },
                    new Country { Alpha2 = "MY", Name = "Malaysia", Currency = "MYR" },
                    new Country { Alpha2 = "MV", Name = "Maldives", Currency = "MVR" },
                    new Country { Alpha2 = "ML", Name = "Mali", Currency = "XOF" },
                    new Country { Alpha2 = "MT", Name = "Malta", Currency = "EUR" },
                    new Country { Alpha2 = "MH", Name = "Marshall Islands", Currency = "USD" },
                    new Country { Alpha2 = "MQ", Name = "Martinique", Currency = "EUR" },
                    new Country { Alpha2 = "MR", Name = "Mauritania", Currency = "MRO" },
                    new Country { Alpha2 = "MU", Name = "Mauritius", Currency = "MUR" },
                    new Country { Alpha2 = "YT", Name = "Mayotte", Currency = "EUR" },
                    new Country { Alpha2 = "MX", Name = "Mexico", Currency = "MXN" },
                    new Country { Alpha2 = "FM", Name = "Micronesia (Federated States of)", Currency = "USD" },
                    new Country { Alpha2 = "MD", Name = "Moldova (Republic of)", Currency = "MDL" },
                    new Country { Alpha2 = "MC", Name = "Monaco", Currency = "EUR" },
                    new Country { Alpha2 = "MN", Name = "Mongolia", Currency = "MNT" },
                    new Country { Alpha2 = "ME", Name = "Montenegro", Currency = "EUR" },
                    new Country { Alpha2 = "MS", Name = "Montserrat", Currency = "XCD" },
                    new Country { Alpha2 = "MA", Name = "Morocco", Currency = "MAD" },
                    new Country { Alpha2 = "MZ", Name = "Mozambique", Currency = "MZN" },
                    new Country { Alpha2 = "MM", Name = "Myanmar", Currency = "MMK" },
                    new Country { Alpha2 = "NA", Name = "Namibia", Currency = "NAD" },
                    new Country { Alpha2 = "NR", Name = "Nauru", Currency = "AUD" },
                    new Country { Alpha2 = "NP", Name = "Nepal", Currency = "NPR" },
                    new Country { Alpha2 = "NL", Name = "Netherlands", Currency = "EUR" },
                    new Country { Alpha2 = "NC", Name = "New Caledonia", Currency = "XPF" },
                    new Country { Alpha2 = "NZ", Name = "New Zealand", Currency = "NZD" },
                    new Country { Alpha2 = "NI", Name = "Nicaragua", Currency = "NIO" },
                    new Country { Alpha2 = "NE", Name = "Niger", Currency = "XOF" },
                    new Country { Alpha2 = "NG", Name = "Nigeria", Currency = "NGN" },
                    new Country { Alpha2 = "NU", Name = "Niue", Currency = "NZD" },
                    new Country { Alpha2 = "NF", Name = "Norfolk Island", Currency = "AUD" },
                    new Country { Alpha2 = "MP", Name = "Northern Mariana Islands", Currency = "USD" },
                    new Country { Alpha2 = "NO", Name = "Norway", Currency = "NOK" },
                    new Country { Alpha2 = "OM", Name = "Oman", Currency = "OMR" },
                    new Country { Alpha2 = "PK", Name = "Pakistan", Currency = "PKR" },
                    new Country { Alpha2 = "PW", Name = "Palau", Currency = "USD" },
                    new Country { Alpha2 = "PS", Name = "Palestine, State of", Currency = "ILS" },
                    new Country { Alpha2 = "PA", Name = "Panama", Currency = "PAB" },
                    new Country { Alpha2 = "PG", Name = "Papua New Guinea", Currency = "PGK" },
                    new Country { Alpha2 = "PY", Name = "Paraguay", Currency = "PYG" },
                    new Country { Alpha2 = "PE", Name = "Peru", Currency = "PEN" },
                    new Country { Alpha2 = "PH", Name = "Philippines", Currency = "PHP" },
                    new Country { Alpha2 = "PN", Name = "Pitcairn", Currency = "NZD" },
                    new Country { Alpha2 = "PL", Name = "Poland", Currency = "PLN" },
                    new Country { Alpha2 = "PT", Name = "Portugal", Currency = "EUR" },
                    new Country { Alpha2 = "PR", Name = "Puerto Rico", Currency = "USD" },
                    new Country { Alpha2 = "QA", Name = "Qatar", Currency = "QAR" },
                    new Country { Alpha2 = "RE", Name = "Réunion", Currency = "EUR" },
                    new Country { Alpha2 = "RO", Name = "Romania", Currency = "RON" },
                    new Country { Alpha2 = "RU", Name = "Russian Federation", Currency = "RUB" },
                    new Country { Alpha2 = "RW", Name = "Rwanda", Currency = "RWF" },
                    new Country { Alpha2 = "BL", Name = "Saint Barthélemy", Currency = "EUR" },
                    new Country { Alpha2 = "SH", Name = "Saint Helena, Ascension and Tristan da Cunha", Currency = "SHP" },
                    new Country { Alpha2 = "KN", Name = "Saint Kitts and Nevis", Currency = "XCD" },
                    new Country { Alpha2 = "LC", Name = "Saint Lucia", Currency = "XCD" },
                    new Country { Alpha2 = "MF", Name = "Saint Martin (French part)", Currency = "EUR" },
                    new Country { Alpha2 = "PM", Name = "Saint Pierre and Miquelon", Currency = "EUR" },
                    new Country { Alpha2 = "VC", Name = "Saint Vincent and the Grenadines", Currency = "XCD" },
                    new Country { Alpha2 = "WS", Name = "Samoa", Currency = "WST" },
                    new Country { Alpha2 = "SM", Name = "San Marino", Currency = "EUR" },
                    new Country { Alpha2 = "ST", Name = "Sao Tome and Principe", Currency = "STD" },
                    new Country { Alpha2 = "SA", Name = "Saudi Arabia", Currency = "SAR" },
                    new Country { Alpha2 = "SN", Name = "Senegal", Currency = "XOF" },
                    new Country { Alpha2 = "RS", Name = "Serbia", Currency = "RSD" },
                    new Country { Alpha2 = "SC", Name = "Seychelles", Currency = "SCR" },
                    new Country { Alpha2 = "SL", Name = "Sierra Leone", Currency = "SLL" },
                    new Country { Alpha2 = "SG", Name = "Singapore", Currency = "SGD" },
                    new Country { Alpha2 = "SX", Name = "Sint Maarten (Dutch part)", Currency = "ANG" },
                    new Country { Alpha2 = "SK", Name = "Slovakia", Currency = "EUR" },
                    new Country { Alpha2 = "SI", Name = "Slovenia", Currency = "EUR" },
                    new Country { Alpha2 = "SB", Name = "Solomon Islands", Currency = "SBD" },
                    new Country { Alpha2 = "SO", Name = "Somalia", Currency = "SOS" },
                    new Country { Alpha2 = "ZA", Name = "South Africa", Currency = "ZAR" },
                    new Country { Alpha2 = "GS", Name = "South Georgia and the South Sandwich Islands", Currency = "GBP" },
                    new Country { Alpha2 = "SS", Name = "South Sudan", Currency = "SSP" },
                    new Country { Alpha2 = "ES", Name = "Spain", Currency = "EUR" },
                    new Country { Alpha2 = "LK", Name = "Sri Lanka", Currency = "LKR" },
                    new Country { Alpha2 = "SD", Name = "Sudan", Currency = "SDG" },
                    new Country { Alpha2 = "SR", Name = "Suriname", Currency = "SRD" },
                    new Country { Alpha2 = "SJ", Name = "Svalbard and Jan Mayen", Currency = "NOK" },
                    new Country { Alpha2 = "SE", Name = "Sweden", Currency = "SEK" },
                    new Country { Alpha2 = "CH", Name = "Switzerland", Currency = "CHF" },
                    new Country { Alpha2 = "SY", Name = "Syrian Arab Republic", Currency = "SYP" },
                    new Country { Alpha2 = "TW", Name = "Taiwan", Currency = "TWD" },
                    new Country { Alpha2 = "TJ", Name = "Tajikistan", Currency = "TJS" },
                    new Country { Alpha2 = "TZ", Name = "Tanzania, United Republic of", Currency = "TZS" },
                    new Country { Alpha2 = "TH", Name = "Thailand", Currency = "THB" },
                    new Country { Alpha2 = "TL", Name = "Timor-Leste", Currency = "USD" },
                    new Country { Alpha2 = "TG", Name = "Togo", Currency = "XOF" },
                    new Country { Alpha2 = "TK", Name = "Tokelau", Currency = "NZD" },
                    new Country { Alpha2 = "TO", Name = "Tonga", Currency = "TOP" },
                    new Country { Alpha2 = "TT", Name = "Trinidad and Tobago", Currency = "TTD" },
                    new Country { Alpha2 = "TN", Name = "Tunisia", Currency = "TND" },
                    new Country { Alpha2 = "TR", Name = "Turkey", Currency = "TRY" },
                    new Country { Alpha2 = "TM", Name = "Turkmenistan", Currency = "TMT" },
                    new Country { Alpha2 = "TC", Name = "Turks and Caicos Islands", Currency = "USD" },
                    new Country { Alpha2 = "TV", Name = "Tuvalu", Currency = "AUD" },
                    new Country { Alpha2 = "UG", Name = "Uganda", Currency = "UGX" },
                    new Country { Alpha2 = "UA", Name = "Ukraine", Currency = "UAH" },
                    new Country { Alpha2 = "AE", Name = "United Arab Emirates", Currency = "AED" },
                    new Country { Alpha2 = "GB", Name = "United Kingdom of Great Britain and Northern Ireland", Currency = "GBP" },
                    new Country { Alpha2 = "US", Name = "United States of America", Currency = "USD" },
                    new Country { Alpha2 = "UM", Name = "United States Minor Outlying Islands", Currency = "USD" },
                    new Country { Alpha2 = "UY", Name = "Uruguay", Currency = "UYU" },
                    new Country { Alpha2 = "UZ", Name = "Uzbekistan", Currency = "UZS" },
                    new Country { Alpha2 = "VU", Name = "Vanuatu", Currency = "VUV" },
                    new Country { Alpha2 = "VE", Name = "Venezuela (Bolivarian Republic of)", Currency = "VEF" },
                    new Country { Alpha2 = "VN", Name = "Viet Nam", Currency = "VND" },
                    new Country { Alpha2 = "VG", Name = "Virgin Islands (British)", Currency = "USD" },
                    new Country { Alpha2 = "VI", Name = "Virgin Islands (U.S.)", Currency = "USD" },
                    new Country { Alpha2 = "WF", Name = "Wallis and Futuna", Currency = "XPF" },
                    new Country { Alpha2 = "EH", Name = "Western Sahara", Currency = "MAD" },
                    new Country { Alpha2 = "YE", Name = "Yemen", Currency = "YER" },
                    new Country { Alpha2 = "ZM", Name = "Zambia", Currency = "ZMW" },
                    new Country { Alpha2 = "ZW", Name = "Zimbabwe", Currency = "BWP" }
                };
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

            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                Plugin.TopPanelItem.Visible = Settings.EnableIntegrationButtonHeader;
                Plugin.ItadViewSidebar.Visible = Settings.EnableIntegrationButtonSide;
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
