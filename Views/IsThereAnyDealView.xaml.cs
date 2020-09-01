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
using System.Windows.Data;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : WindowBase
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();
        private readonly IsThereAnyDealSettings settings;

        private readonly IsThereAnyDeal plugin;

        public string CurrencySign { get; set; }
        public string PlainSelected { get; set; }

        private List<Wishlist> lbWishlistItems = new List<Wishlist>();
        private List<string> SearchStores = new List<string>();
        private int SearchPercentage = 0;

        public IsThereAnyDealView(IsThereAnyDeal plugin, IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();
            this.PlainSelected = PlainSelected;
            this.settings = settings;
            this.plugin = plugin;

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
                                    lbWishlist.ScrollIntoView(lbWishlist.SelectedItem);
                                    break;
                                }
                                index += 1;
                            }
                        }
                        DataLoadWishlist.Visibility = Visibility.Collapsed;
                        dpData.IsEnabled = true;
                    }));
                });

            GetListGiveaways(PlayniteApi, PluginUserDataPath);

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
            if (settings.EnableEpic)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Epic", IsCheck = false });
            }
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName));
            FilterStore.ItemsSource = FilterStoreItems;
        }

        private async Task<List<Wishlist>> LoadData (IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            //logger.Debug("LoadData");
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(plugin, PlayniteApi, settings, PluginUserDataPath);
            return ListWishlist;
        }



        private void GetListGiveaways(IPlayniteAPI PlayniteApi, string PluginUserDataPath)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(PlayniteApi, PluginUserDataPath);

            int row = 0;
            int col = 0;
            foreach (ItadGiveaway itadGiveaway in itadGiveaways)
            {
                if (col > 3)
                {
                    col = 0;
                    row += 2;
                    var rowAuto = new RowDefinition();
                    var rowSep = new RowDefinition();
                    rowAuto.Height = GridLength.Auto;
                    rowSep.Height = new GridLength(20);
                    gGiveaways.RowDefinitions.Add(rowAuto);
                    gGiveaways.RowDefinitions.Add(rowSep);
                }

                var dp = new DockPanel();
                dp.Width = 540;

                var tb = new TextBlock();
                tb.Text = itadGiveaway.TitleAll;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Width = 440;

                var bt = new Button();
                bt.Content = resources.GetString("LOCItadWeb");
                bt.Tag = itadGiveaway.Link;
                bt.Click += new RoutedEventHandler(webGiveaway);
                bt.Height = 30;
                bt.Width = 100;
                DockPanel.SetDock(bt, Dock.Right);

                dp.Children.Add(tb);
                dp.Children.Add(bt);

                Grid.SetRow(dp, row);
                Grid.SetColumn(dp, col);

                col += 2;

                gGiveaways.Children.Add(dp);
            }
        }
        private void webGiveaway(object sender, RoutedEventArgs e)
        {
            Process.Start((string)((Button)sender).Tag);
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
                    x => x.ItadBestPrice.price_cut >= SearchPercentage && x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1 &&
                    (SearchStores.Contains(x.StoreName) || x.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0)
                );
                return;
            }

            if (!TextboxSearch.Text.IsNullOrEmpty())
            {
                lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                    x => x.ItadBestPrice.price_cut >= SearchPercentage && x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1
                );
                return;
            }

            if (SearchStores.Count != 0)
            {
                lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                    x => x.ItadBestPrice.price_cut >= SearchPercentage && 
                    (SearchStores.Contains(x.StoreName) || x.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0)
                );
                return;
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
            lPercentage.Content = SearchPercentage + "%";
            GetListGame();
        }

        // Active store button
        private void LbStorePrice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ItadGameInfo itemSelected = (ItadGameInfo)((ListBox)sender).SelectedItem;
                Button bt = (Button)((Grid)((Grid)((ListBox)sender).Parent).Parent).FindName("btStore");
                bt.IsEnabled = true;
                bt.Tag = itemSelected.url_buy;
            }
            catch(Exception ex)
            {

            }
        }
    }

    public class ListStore
    {
        public string StoreName { get; set; }
        public bool IsCheck { get; set; }
    }
}
