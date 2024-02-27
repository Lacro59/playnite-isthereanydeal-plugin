using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using CommonPluginsShared;
using System.Threading.Tasks;
using System;
using Playnite.SDK.Models;
using System.Windows.Documents;
using System.Diagnostics;
using IsThereAnyDeal.Models.ApiWebsite;
using CommonPluginsStores.Steam;

namespace IsThereAnyDeal.Views
{
    public partial class IsThereAnyDealSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly IResourceProvider resources = new ResourceProvider();

        private IsThereAnyDealSettings Settings;
        private IsThereAnyDeal Plugin;

        private List<ItadShops> StoresItems = new List<ItadShops>();
        private readonly IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();


        public IsThereAnyDealSettingsView(IsThereAnyDealSettings settings, IsThereAnyDeal plugin)
        {
            Settings = settings;
            Plugin = plugin;

            InitializeComponent();

            SteamPanel.SteamApi = IsThereAnyDeal.SteamApi;

            Settings.wishlistIgnores = Settings.wishlistIgnores.OrderBy(x => x.StoreName).ThenBy(x => x.Name).ToList();
            lvIgnoreList.ItemsSource = Settings.wishlistIgnores;

            DataContext = this;

            PART_LimitNotificationPrice.LongValue = Settings.LimitNotificationPrice;
            lLimitNotification.Content = PART_sPriceCut.Value + "%";

            StoresItems = Settings.Stores;
            ListStores.ItemsSource = StoresItems;
            ChkStore_Checked(null, null);
        }

        private void PART_SelectCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StoresText.Text = string.Empty;

            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() =>
            {
                StoresItems = isThereAnyDealApi.GetShops(Settings.CountrySelected.Alpha2).GetAwaiter().GetResult();
                StoresItems.ForEach(x => 
                {
                    ItadShops finded = Settings.Stores.Where(y => y.Id == x.Id)?.FirstOrDefault();
                    if (finded != null)
                    {
                        x.IsCheck = finded.IsCheck;
                    }
                });

                _ = (Dispatcher?.BeginInvoke((Action)delegate
                {
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
            string targetPath = API.Instance.Dialogs.SelectFile("json file|*.json");

            if (!targetPath.IsNullOrEmpty())
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"IsThereAnyDeal - " + resources.GetString("LOCImportLabel"),
                    true
                );
                globalProgressOptions.IsIndeterminate = true;

                API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
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
                                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }

                                _ = API.Instance.Dialogs.ShowMessage(resources.GetString("LOCItadImportSuccessful"), "IsThereAnyDeal");
                            }
                            else
                            {
                                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }
                                _ = API.Instance.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
                            }
                        }
                        else
                        {
                            if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                            {
                                return;
                            }
                            _ = API.Instance.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
                        }

                        stopWatch.Stop();
                        TimeSpan ts = stopWatch.Elapsed;
                        logger.Info($"Task GetImportSteam() - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeal");

                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            return;
                        }
                        _ = API.Instance.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
                    }
                }, globalProgressOptions);
            }
        }

        private void PART_BtCountriesRefresh_Click(object sender, RoutedEventArgs e)
        {
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() =>
            {
                List<Country> Countries = isThereAnyDealApi.GetCountries().GetAwaiter().GetResult();
                if (Countries?.Count > 0)
                {
                    Settings.Countries = Countries;
                }

                _ = (Dispatcher?.BeginInvoke((Action)delegate
                {
                    PART_SelectCountry.ItemsSource = null;
                    PART_SelectCountry.ItemsSource = Settings.Countries;

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                }));
            });
        }

        private void PART_Steam_Checked(object sender, RoutedEventArgs e)
        {
            IsThereAnyDeal.SteamApi = new SteamApi("IsThereAnyDeal");
            IsThereAnyDeal.SteamApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            _ = IsThereAnyDeal.SteamApi.CurrentUser;
        }
    }
}
