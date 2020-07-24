using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using Newtonsoft.Json;
using Playnite.Controls;
using Playnite.SDK;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : WindowBase
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IsThereAnyDealSettings settings;

        public string CurrencySign { get; set; }
        public string PlainSelected { get; set; }

        private List<Wishlist> lbWishlistItems = new List<Wishlist>();
        private List<string> SearchStores = new List<string>();
        private int SearchPercentage = 0;

        public IsThereAnyDealView(IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();
            this.PlainSelected = PlainSelected;
            this.settings = settings;

            // Load data
            dpData.IsEnabled = false;
            var task = Task.Run(() => LoadData(PlayniteApi, PluginUserDataPath, settings, PluginUserDataPath))
                .ContinueWith(antecedent => 
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        lbWishlistItems = antecedent.Result;
                        lbWishlist.ItemsSource = lbWishlistItems;
                        if (!PlainSelected.IsNullOrEmpty())
                        {
                            int index = 0;
                            foreach (Wishlist wishlist in antecedent.Result)
                            {
                                if (wishlist.Plain == PlainSelected)
                                {
                                    lbWishlist.SelectedIndex = index;
                                    lbWishlist.UpdateLayout();
                                    break;
                                }
                                index += 1;
                            }
                        }
                        DataLoadWishlist.Visibility = Visibility.Collapsed;
                        dpData.IsEnabled = true;
                    }));
                });

            SetFilterStore();

            DataContext = this;
        }

        private void SetFilterStore()
        {
            List<ListStore> FilterStoreItems = new List<ListStore>();
            if (settings.EnableSteam)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Steam", IsCheck = false });
            }
            if (settings.EnableGog)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "GOG", IsCheck = false });
            }
            if (settings.EnableHumble)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Humble", IsCheck = false });
            }
            FilterStore.ItemsSource = FilterStoreItems;
        }

        private async Task<List<Wishlist>> LoadData (IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            logger.Debug("LoadData");
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(PlayniteApi, settings, PluginUserDataPath);
            return ListWishlist;
        }


        #region Button for each game in wishlist
        private void ButtonWeb_Click(object sender, RoutedEventArgs e)
        {
            string Tag = (string)((Button)sender).Tag;
            if (!Tag.IsNullOrEmpty())
            {
                Process.Start(Tag);
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            string Tag = (string)((Button)sender).Tag;
            if (!Tag.IsNullOrEmpty())
            {
                Process.Start(Tag);
            }
        }
        #endregion


        private void LbWishlist_Loaded(object sender, RoutedEventArgs e)
        {
            Tools.DesactivePlayniteWindowControl(this);
            lbWishlist.ScrollIntoView(lbWishlist.SelectedItem);
        }


        // Get list
        private void GetListGame()
        {
            lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                x => x.ItadBestPrice.price_cut >= SearchPercentage
            );

            if (!TextboxSearch.Text.IsNullOrEmpty() && SearchStores.Count != 0)
            {
                lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                    x => x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1 && SearchStores.Contains(x.StoreName)
                );
            }

            if (!TextboxSearch.Text.IsNullOrEmpty())
            {
                lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                    x => x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1
                );
            }

            if (SearchStores.Count != 0)
            {
                lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                    x => SearchStores.Contains(x.StoreName)
                );
            }
        }

        // Search by name
        private void TextboxSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            GetListGame();
        }

        // Search by store
        private void ChkStore_Checked(object sender, RoutedEventArgs e)
        {
            FilterCbStore((CheckBox)sender);
        }
        private void ChkStore_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterCbStore((CheckBox)sender);
        }
        private void FilterCbStore(CheckBox sender)
        {
            FilterStore.Text = "";

            if ((bool)sender.IsChecked)
            {
                SearchStores.Add((string)sender.Content);
            }
            else
            {
                SearchStores.Remove((string)sender.Content);
            }

            if (SearchStores.Count != 0)
            {
                FilterStore.Text = String.Join(", ", SearchStores);
            }

            GetListGame();
        }

        // Search percentage
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SearchPercentage = (int)((Slider)sender).Value;
            GetListGame();
        }
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public bool IsCheck { get; set; }
    }
}
