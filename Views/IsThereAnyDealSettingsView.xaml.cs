using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models;
using Playnite.SDK;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace IsThereAnyDeal.Views
{
    public partial class IsThereAnyDealSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private List<ItadRegion> RegionsData;
        private IsThereAnyDealSettings settings;

        private IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
        private bool IsFirst = true;


        public IsThereAnyDealSettingsView(IsThereAnyDealSettings settings)
        {
            this.settings = settings;
            RegionsData = isThereAnyDealApi.GetCoveredRegions();
            InitializeComponent();
            ItadSelectRegion.ItemsSource = RegionsData;
            ItadSelectRegion.Text = GetInfosRegion(settings.Region);
            ItadSelectCountry.Text = settings.Country;
            StoresItems = settings.Stores;
            //logger.Debug(JsonConvert.SerializeObject((object)settings.Stores));
            ListStores.ItemsSource = StoresItems;
            foreach (ItadStore store in StoresItems)
            {
                if (store.IsCheck)
                {
                    if (ListStores.Text == "")
                    {
                        ListStores.Text += store.Title;
                    }
                    else
                    {
                        ListStores.Text += ", " + store.Title;
                    }
                }
            }

            lLimitNotification.Content = settings.LimitNotification + "%";

            DataContext = this;
            IsFirst = false;
        }

        private void ItadSelectRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItadSelectRegion.SelectedItem != null)
            {
                string regionSelected = ((ItadRegion)ItadSelectRegion.SelectedItem).Region;
                settings.Region = regionSelected;

                ItadSelectCountry.ItemsSource = ((ItadRegion)ItadSelectRegion.SelectedItem).Countries;

                ListStores.Text = "";
            }
        }


        private string GetInfosRegion(string RegionName)
        {
            for (int i = 0; i < RegionsData.Count; i++)
            {
                if (RegionName == RegionsData[i].Region)
                {
                    settings.CurrencySign = RegionsData[i].CurrencySign;
                    return RegionsData[i].Region + " - " + RegionsData[i].CurrencyName + " - " + RegionsData[i].CurrencySign;
                }
            }

            return "";
        }

        private void ItadSelectCountry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItadSelectCountry.SelectedItem != null)
            {
                settings.Country = (string)ItadSelectCountry.SelectedItem;
                GetInfosRegion(settings.Region);
                if (!IsFirst)
                {
                    ListStores.Text = "";
                    StoresItems = isThereAnyDealApi.GetRegionStores(settings.Region, settings.Country);
                    ListStores.ItemsSource = StoresItems;
                    ListStores.UpdateLayout();
                }
            }
        }



        List<ItadStore> StoresItems = new List<ItadStore>();

        private void ChkStore_Checked(object sender, RoutedEventArgs e)
        {
            ListStores.Text = "";
            for (int i = 0; i < StoresItems.Count; i++)
            {
                if ((string)((CheckBox)sender).Content == StoresItems[i].Title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
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
                        if (ListStores.Text == "")
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
            settings.Stores = StoresItems;
        }

        private void ChkStore_Unchecked(object sender, RoutedEventArgs e)
        {
            ListStores.Text = "";
            for (int i = 0; i < StoresItems.Count; i++)
            {
                if ((string)((CheckBox)sender).Content == StoresItems[i].Title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
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
                        if (ListStores.Text == "")
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
            settings.Stores = StoresItems;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            lLimitNotification.Content = ((Slider)sender).Value + "%";
        }
    }
}
