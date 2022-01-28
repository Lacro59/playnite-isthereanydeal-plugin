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

namespace IsThereAnyDeal.Views
{
    public partial class IsThereAnyDealSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IPlayniteAPI _PlayniteApi;
        private IsThereAnyDealSettings _settings;
        private string _PluginUserDataPath;

        private List<ItadRegion> RegionsData;
        private List<ItadStore> StoresItems = new List<ItadStore>();
        private IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
        private bool IsFirst = true;


        public IsThereAnyDealSettingsView(IPlayniteAPI PlayniteApi, IsThereAnyDealSettings settings, string PluginUserDataPath)
        {
            _PlayniteApi = PlayniteApi;
            _settings = settings;
            _PluginUserDataPath = PluginUserDataPath;
            
            InitializeComponent();

            GetDataRegion();

            _settings.wishlistIgnores = _settings.wishlistIgnores.OrderBy(x => x.StoreName).ThenBy(x => x.Name).ToList();
            lvIgnoreList.ItemsSource = _settings.wishlistIgnores;

            DataContext = this;
            IsFirst = false;

            PART_LimitNotificationPrice.LongValue = _settings.LimitNotificationPrice;


            lLimitNotification.Content = PART_sPriceCut.Value + "%";
        }

        private void ItadSelectRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItadSelectRegion.SelectedItem != null)
            {
                string regionSelected = ((ItadRegion)ItadSelectRegion.SelectedItem).Region;
                _settings.Region = regionSelected;

                ItadSelectCountry.ItemsSource = ((ItadRegion)ItadSelectRegion.SelectedItem).Countries;

                ListStores.Text = string.Empty;


                lLimitNotificationPrice.Content = GetInfosRegion(_settings.Region, true);
            }
        }


        private string GetInfosRegion(string RegionName, bool CurrencySignOnly = false)
        {
            for (int i = 0; i < RegionsData.Count; i++)
            {
                if (RegionName == RegionsData[i].Region)
                {
                    _settings.CurrencySign = RegionsData[i].CurrencySign;

                    if (!CurrencySignOnly)
                    {
                        return RegionsData[i].Region + " - " + RegionsData[i].CurrencyName + " - " + RegionsData[i].CurrencySign;
                    }
                    else
                    {
                        return RegionsData[i].CurrencySign;
                    }
                }
            }

            return string.Empty;
        }

        private void ItadSelectCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItadSelectCountry.SelectedItem != null)
            {
                _settings.Country = (string)ItadSelectCountry.SelectedItem;
                GetInfosRegion(_settings.Region);
                if (!IsFirst)
                {
                    ListStores.Text = string.Empty;


                    PART_DataLoad.Visibility = Visibility.Visible;
                    PART_Data.Visibility = Visibility.Hidden;

                    var TaskView = Task.Run(() =>
                    {
                        StoresItems = isThereAnyDealApi.GetRegionStores(_settings.Region, _settings.Country);
                        Common.LogDebug(true, $"StoresItems: {Serialization.ToJson(StoresItems)}");

                        this.Dispatcher.BeginInvoke((Action)delegate
                        {
                            ListStores.ItemsSource = StoresItems;
                            ListStores.UpdateLayout();

                            PART_DataLoad.Visibility = Visibility.Hidden;
                            PART_Data.Visibility = Visibility.Visible;
                        });
                    });
                }
            }
        }


        private void ChkStore_Checked(object sender, RoutedEventArgs e)
        {
            ListStores.Text = string.Empty;
            for (int i = 0; i < StoresItems.Count; i++)
            {
                if ((string)((CheckBox)sender).Content == StoresItems[i].Title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == string.Empty)
                        {
                            ListStores.Text = StoresItems[i].Title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].Title;
                        }
                    }
                }
                else
                {
                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == string.Empty)
                        {
                            ListStores.Text = StoresItems[i].Title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].Title;
                        }
                    }
                }
            }
            _settings.Stores = StoresItems;
        }

        private void ChkStore_Unchecked(object sender, RoutedEventArgs e)
        {
            ListStores.Text = string.Empty;
            for (int i = 0; i < StoresItems.Count; i++)
            {
                if ((string)((CheckBox)sender).Content == StoresItems[i].Title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == string.Empty)
                        {
                            ListStores.Text = StoresItems[i].Title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].Title;
                        }
                    }
                }
                else
                {
                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == string.Empty)
                        {
                            ListStores.Text = StoresItems[i].Title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].Title;
                        }
                    }
                }
            }
            _settings.Stores = StoresItems;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                lLimitNotification.Content = ((Slider)sender).Value + "%";
            }
            catch
            {
            }
        }


        private void BtShow_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            _settings.wishlistIgnores.RemoveAt(index);
            lvIgnoreList.ItemsSource = null;
            lvIgnoreList.ItemsSource = _settings.wishlistIgnores;
        }


        private void GetDataRegion()
        {
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            var TaskView = Task.Run(() =>
            {
                RegionsData = isThereAnyDealApi.GetCoveredRegions();
                Common.LogDebug(true, $"RegionsData: {Serialization.ToJson(RegionsData)}");

                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    ItadSelectRegion.ItemsSource = RegionsData;
                    ItadSelectRegion.Text = GetInfosRegion(_settings.Region);

                    ItadSelectCountry.Text = _settings.Country;
                    StoresItems = _settings.Stores;
                    ListStores.ItemsSource = StoresItems;

                    foreach (ItadStore store in StoresItems)
                    {
                        if (store.IsCheck)
                        {
                            if (ListStores.Text == string.Empty)
                            {
                                ListStores.Text += store.Title;
                            }
                            else
                            {
                                ListStores.Text += ", " + store.Title;
                            }
                        }
                    }

                    lLimitNotificationPrice.Content = GetInfosRegion(_settings.Region, true);

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                });
            });
        }


        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            if (!((string)((Button)sender).Tag).IsNullOrEmpty())
            {
                int.TryParse((string)((Button)sender).Tag, out int index);

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
            Hyperlink link = (Hyperlink)sender;
            Process.Start((string)link.Tag);
        }

        private void ButtonImportSteam_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = _PlayniteApi.Dialogs.SelectFile("json file|*.json");

            if (!targetPath.IsNullOrEmpty())
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"IsThereAnyDeal - " + resources.GetString("LOCImportLabel"),
                    true
                );
                globalProgressOptions.IsIndeterminate = true;

                _PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    try
                    {
                        Stopwatch stopWatch = new Stopwatch();
                        stopWatch.Start();


                        GameSource gameSource = _PlayniteApi.Database.Sources.Where(x => x.Name.ToLower().IndexOf("steam") > -1).FirstOrDefault();

                        if (gameSource != null)
                        {
                            Guid SteamID = gameSource.Id;

                            SteamWishlist steamWishlist = new SteamWishlist();

                            if (steamWishlist.ImportWishlist(_PlayniteApi, SteamID, _PluginUserDataPath, _settings, targetPath))
                            {
                                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }

                                _PlayniteApi.Dialogs.ShowMessage(resources.GetString("LOCItadImportSuccessful"), "IsThereAnyDeal");
                            }
                            else
                            {
                                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                {
                                    return;
                                }

                                _PlayniteApi.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
                            }
                        }
                        else
                        {
                            if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                            {
                                return;
                            }

                            _PlayniteApi.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
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
                        _PlayniteApi.Dialogs.ShowErrorMessage(resources.GetString("LOCItadImportError"), "IsThereAnyDeal");
                    }
                }, globalProgressOptions);
            }
        }
    }
}
