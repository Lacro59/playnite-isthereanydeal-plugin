using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using Newtonsoft.Json;
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
                        ListStores.Text += store.title;
                    }
                    else
                    {
                        ListStores.Text += ", " + store.title;
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
                string regionSelected = ((ItadRegion)ItadSelectRegion.SelectedItem).region;
                settings.Region = regionSelected;

                ItadSelectCountry.ItemsSource = ((ItadRegion)ItadSelectRegion.SelectedItem).countries;

                ListStores.Text = "";
            }
        }


        private string GetInfosRegion(string RegionName)
        {
            for (int i = 0; i < RegionsData.Count; i++)
            {
                if (RegionName == RegionsData[i].region)
                {
                    settings.CurrencySign = RegionsData[i].currencySign;
                    return RegionsData[i].region + " - " + RegionsData[i].currencyName + " - " + RegionsData[i].currencySign;
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
                if ((string)((CheckBox)sender).Content == StoresItems[i].title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
                        {
                            ListStores.Text = StoresItems[i].title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].title;
                        }
                    }
                }
                else
                {
                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
                        {
                            ListStores.Text = StoresItems[i].title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].title;
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
                if ((string)((CheckBox)sender).Content == StoresItems[i].title)
                {
                    StoresItems[i].IsCheck = (bool)((CheckBox)sender).IsChecked;

                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
                        {
                            ListStores.Text = StoresItems[i].title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].title;
                        }
                    }
                }
                else
                {
                    if (StoresItems[i].IsCheck)
                    {
                        if (ListStores.Text == "")
                        {
                            ListStores.Text = StoresItems[i].title;
                        }
                        else
                        {
                            ListStores.Text += ", " + StoresItems[i].title;
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
