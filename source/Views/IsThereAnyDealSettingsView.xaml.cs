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
        private static readonly IResourceProvider resources = new ResourceProvider();

        private IsThereAnyDealSettings Settings;
        private IsThereAnyDeal Plugin;
        private string PluginUserDataPath;

        private List<ItadShops> StoresItems = new List<ItadShops>();
        private readonly IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
        private bool IsFirst = true;


        public IsThereAnyDealSettingsView(IsThereAnyDealSettings settings, IsThereAnyDeal plugin, string PluginUserDataPath)
        {
            Settings = settings;
            Plugin = plugin;
            this.PluginUserDataPath = PluginUserDataPath;

            InitializeComponent();

            Settings.wishlistIgnores = Settings.wishlistIgnores.OrderBy(x => x.StoreName).ThenBy(x => x.Name).ToList();
            lvIgnoreList.ItemsSource = Settings.wishlistIgnores;

            DataContext = this;
            IsFirst = false;

            PART_LimitNotificationPrice.LongValue = Settings.LimitNotificationPrice;


            lLimitNotification.Content = PART_sPriceCut.Value + "%";
        }

        private void PART_SelectCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_SelectCountry.SelectedItem != null)
            {
                Settings.Country = ((ComboBoxItem)PART_SelectCountry.SelectedItem).Content.ToString();
                if (!IsFirst)
                {
                    ListStores.Text = string.Empty;

                    PART_DataLoad.Visibility = Visibility.Visible;
                    PART_Data.Visibility = Visibility.Hidden;

                    Task.Run(() =>
                    {
                        StoresItems = isThereAnyDealApi.GetShops(Settings.Country).GetAwaiter().GetResult();

                        this.Dispatcher?.BeginInvoke((Action)delegate
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
                        if (ListStores.Text.IsNullOrEmpty())
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
                        if (ListStores.Text.IsNullOrEmpty())
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
            Settings.Stores = StoresItems;
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
                        if (ListStores.Text.IsNullOrEmpty())
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
                        if (ListStores.Text.IsNullOrEmpty())
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

                            if (steamWishlist.ImportWishlist(SteamID, PluginUserDataPath, Settings, targetPath))
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
    }
}
