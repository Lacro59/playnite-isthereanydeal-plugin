using IsThereAnyDeal.Services;
using IsThereAnyDeal.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IsThereAnyDeal.Clients;
using CommonPluginsShared.Extensions;
using System.Windows.Controls.Primitives;
using CommonPluginsShared.Converters;
using System.Globalization;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();
        private IPlayniteAPI _PlayniteApi;
        private readonly IsThereAnyDealSettings _settings;
        private readonly IsThereAnyDeal _plugin;

        private List<Wishlist> lbWishlistItems { get; set; } = new List<Wishlist>();
        private List<string> SearchStores { get; set; } = new List<string>();
        private int SearchPercentage { get; set; } = 0;
        private int SearchPrice { get; set; } = 100;

        private IsThereAnyDealApi isThereAnyDealApi { get; set; } = new IsThereAnyDealApi();
        private ItadGameInfo StorePriceSelected { get; set; }


        public IsThereAnyDealView(IsThereAnyDeal plugin, IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();
            
            _settings = settings;
            _plugin = plugin;
            _PlayniteApi = PlayniteApi;

            // Load data
            RefreshData(PlainSelected);

            GetListGiveaways(PlayniteApi, PluginUserDataPath);

            SetFilterStore();

            lPrice.Content = settings.MaxPrice + _settings.CurrencySign;
            sPrice.Minimum = settings.MinPrice;
            sPrice.Maximum = settings.MaxPrice;

            DataContext = this;
        }

        private void RefreshData(string PlainSelected, bool CachOnly = true, bool ForcePrice = false)
        {
            DataLoadWishlist.Visibility = Visibility.Visible;
            lbWishlist.ItemsSource = null;
            dpData.IsEnabled = false;
            var task = Task.Run(() => LoadData(_PlayniteApi, _plugin.GetPluginUserDataPath(), _settings, PlainSelected, CachOnly, ForcePrice))
                .ContinueWith(antecedent =>
                {
                    this.Dispatcher?.Invoke(new Action(() => {
                        try
                        {
                            lbWishlistItems = antecedent.Result;
                            GetListGame();
                            SetInfos();

                            if (!PlainSelected.IsNullOrEmpty())
                            {
                                int index = 0;
                                foreach (Wishlist wishlist in lbWishlist.ItemsSource)
                                {
                                    if (wishlist.Plain.IsEqual(PlainSelected))
                                    {
                                        index = ((List<Wishlist>)lbWishlist.ItemsSource).FindIndex(x => x == wishlist);
                                        lbWishlist.SelectedIndex = index;
                                        lbWishlist.ScrollIntoView(lbWishlist.SelectedItem);
                                        break;
                                    }
                                }
                            }
                            PART_DateData.Content = new LocalDateTimeConverter().Convert(_settings.LastRefresh, null, null, CultureInfo.CurrentCulture);
                            DataLoadWishlist.Visibility = Visibility.Collapsed;
                            dpData.IsEnabled = true;
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, true, "IsThereAnyDeal");
                        }
                    }));
                });
        }

        private void SetFilterStore()
        {
            List<ListStore> FilterStoreItems = new List<ListStore>();
            if (_settings.EnableSteam)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Steam", StoreNameDisplay = (TransformIcon.Get("Steam") + " Steam").Trim(), IsCheck = false });
                }
            if (_settings.EnableGog)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "GOG", StoreNameDisplay = (TransformIcon.Get("GOG") + " GOG").Trim(), IsCheck = false });
            }
            if (_settings.EnableHumble)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Humble", StoreNameDisplay = (TransformIcon.Get("Humble") + " Humble").Trim(), IsCheck = false });
            }
            if (_settings.EnableEpic)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Epic", StoreNameDisplay = (TransformIcon.Get("Epic") + " Epic").Trim(), IsCheck = false });
            }
            if (_settings.EnableXbox)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Microsoft Store", StoreNameDisplay = (TransformIcon.Get("Xbox") + " Microsoft Store").Trim(), IsCheck = false });
            }
            if (_settings.EnableUbisoft)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Ubisoft Connect", StoreNameDisplay = (TransformIcon.Get("Ubisoft Connect") + " Ubisoft Connect").Trim(), IsCheck = false });
            }
            if (_settings.EnableOrigin)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "EA app", StoreNameDisplay = (TransformIcon.Get("EA app") + " EA app").Trim(), IsCheck = false });
            }
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName));
            FilterStore.ItemsSource = FilterStoreItems;
        }

        private void SetInfos()
        {
            tpListBox.ItemsSource = null;
            tpListBox.ItemsSource = isThereAnyDealApi.countDatas;
        }


        private async Task<List<Wishlist>> LoadData (IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "", bool CachOnly = true, bool ForcePrice = false)
        {
            List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(_plugin, settings, PluginUserDataPath, CachOnly, ForcePrice);
            return ListWishlist;
        }

        private async Task<List<ItadGiveaway>> LoadDatatGiveaways(IPlayniteAPI PlayniteApi, string PluginUserDataPath)
        {
            List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(PlayniteApi, PluginUserDataPath);
            return itadGiveaways;
        }


        private void GetListGiveaways(IPlayniteAPI PlayniteApi, string PluginUserDataPath)
        {
            var task = Task.Run(() => LoadDatatGiveaways(PlayniteApi, PluginUserDataPath))
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        List<ItadGiveaway> itadGiveaways = antecedent.Result;
                        int row = 0;
                        int col = 0;
                        foreach (ItadGiveaway itadGiveaway in itadGiveaways)
                        {
                            if (col > 3)
                            {
                                col = 0;
                                row += 1;
                                var rowAuto = new RowDefinition();
                                rowAuto.Height = new GridLength(40);
                                gGiveaways.RowDefinitions.Add(rowAuto);
                            }

                            var dp = new DockPanel();
                            dp.Width = 540;

                            var tb = new TextBlock();
                            tb.Text = itadGiveaway.TitleAll;
                            tb.VerticalAlignment = VerticalAlignment.Center;
                            tb.Width = 440;
                            tb.VerticalAlignment = VerticalAlignment.Center;

                            var bt = new Button();
                            bt.Content = resources.GetString("LOCWebsiteLabel");
                            bt.Tag = itadGiveaway.Link;
                            bt.Click += new RoutedEventHandler(webGiveaway);
                            bt.Height = 30;
                            bt.Width = 100;
                            bt.VerticalAlignment = VerticalAlignment.Center;
                            DockPanel.SetDock(bt, Dock.Right);

                            dp.Children.Add(tb);
                            dp.Children.Add(bt);

                            Grid.SetRow(dp, row);
                            Grid.SetColumn(dp, col);

                            col += 2;

                            gGiveaways.Children.Add(dp);
                        }
                    }));
                });
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

        // Active store button
        private void LbStorePrice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListBox)sender).SelectedItem != null)
            {
                StorePriceSelected = (ItadGameInfo)((ListBox)sender).SelectedItem;

                Button bt = (Button)((Grid)((Grid)((ListBox)sender).Parent).Parent).FindName("btStore");
                bt.IsEnabled = true;
                bt.Tag = StorePriceSelected.UrlBuy;
            }
        }

        // Remove game
        private void BtRemoveWishList_Click(object sender, RoutedEventArgs e)
        {
            bool IsDeleted = false;
            int index = int.Parse(((Button)sender).Tag.ToString());

            ListBox elParent = UI.FindParent<ListBox>(sender as Button);
            StorePriceSelected = (ItadGameInfo)elParent.Items[index];
            lbWishlist.ItemsSource = null;
            Common.LogDebug(true, $"BtRemoveWishList_Click() - StorePriceSelected: {Serialization.ToJson(StorePriceSelected)}");

            MessageBoxResult RessultDialog = _PlayniteApi.Dialogs.ShowMessage(
                string.Format(resources.GetString("LOCItadDeleteOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName), 
                "IsThereAnyDeal", 
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                DataLoadWishlist.Visibility = Visibility.Visible;
                dpData.IsEnabled = false;

                Task.Run(() =>
                {
                    try
                    {
                        if (!StorePriceSelected.StoreId.IsNullOrEmpty())
                        {
                            switch (StorePriceSelected.ShopName.ToLower())
                            {
                                case "steam":
                                    Common.LogDebug(true, $"Is Steam");
#if DEBUG
                                    SteamWishlist steamWishlist = new SteamWishlist();
                                    IsDeleted = steamWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        steamWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
#endif
                                    break;

                                case "epic game store":
                                    Common.LogDebug(true, $"Is Epic");

                                    EpicWishlist epicWishlist = new EpicWishlist();
                                    IsDeleted = epicWishlist.RemoveWishlist(StorePriceSelected.StoreId, _plugin.GetPluginUserDataPath());
                                    if (IsDeleted)
                                    {
                                        epicWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;

                                case "humble store":
                                    Common.LogDebug(true, $"Is Humble Store");

                                    HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                                    IsDeleted = humbleBundleWishlist.RemoveWishlist(_PlayniteApi, StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        humbleBundleWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _settings.HumbleKey, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;

                                case "gog":
                                    Common.LogDebug(true, $"Is GOG");

                                    GogWishlist gogWishlist = new GogWishlist(_PlayniteApi);
                                    IsDeleted = gogWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        gogWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;

                                case "microsoft store":
                                    Common.LogDebug(true, $" Is xbox");
                                    break;

                                case "origin":
                                    Common.LogDebug(true, $"Is origin");

                                    OriginWishlist originWishlist = new OriginWishlist();
                                    IsDeleted = originWishlist.RemoveWishlist(StorePriceSelected.StoreId, _PlayniteApi);
                                    if (IsDeleted)
                                    {
                                        originWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeal");
                    }
                })
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (IsDeleted)
                        {
                            Common.LogDebug(true, $"IsDeleted");
                            RefreshData(string.Empty);
                        }
                        else
                        {
                            Common.LogDebug(true, $"IsNotDeleted");
                            GetListGame();
                        }
                        
                        DataLoadWishlist.Visibility = Visibility.Collapsed;
                        dpData.IsEnabled = true;
                    }));
                });
            }
            else
            {
                GetListGame();
                DataLoadWishlist.Visibility = Visibility.Collapsed;
                dpData.IsEnabled = true;
            }
        }

        // Hide game
        private void BtHideWishList_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            ListBox elParent = UI.FindParent<ListBox>(sender as Button);
            StorePriceSelected = (ItadGameInfo)elParent.Items[index];
            lbWishlist.ItemsSource = null;
            Common.LogDebug(true, $"BtHideWishList_Click() - StorePriceSelected: {Serialization.ToJson(StorePriceSelected)}");

            var RessultDialog = _PlayniteApi.Dialogs.ShowMessage(
                string.Format(resources.GetString("LOCItadHideOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    _settings.wishlistIgnores.Add(new WishlistIgnore
                    {
                        StoreId = StorePriceSelected.StoreId,
                        StoreName = StorePriceSelected.ShopName,
                        Name = StorePriceSelected.Name,
                        Plain = StorePriceSelected.Plain
                    });
                    _plugin.SavePluginSettings(_settings);
                    GetListGame();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }
            }
        }
        #endregion


        #region Search
        // Get list
        private void GetListGame()
        {
            lbWishlist.ItemsSource = lbWishlistItems.FindAll(
                x => x.ItadBestPrice.PriceCut >= SearchPercentage && x.ItadBestPrice.PriceNew <= SearchPrice &&
                (TextboxSearch.Text.IsNullOrEmpty() ? true : x.Name.Contains(TextboxSearch.Text, StringComparison.InvariantCultureIgnoreCase)) &&
                (SearchStores.Count == 0 ? true : ((SearchStores.Contains(x.StoreName) || x.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0))) &&
                _settings.wishlistIgnores.All(y => y.StoreId != x.StoreId && y.Plain != x.Plain) &&
                ((bool)PART_TbOnlyInLibrary.IsChecked ? x.InLibrary : ((!(bool)PART_TbIncludeInLibrary.IsChecked ? !x.InLibrary : true || !(bool)PART_TbIncludeWithoutData.IsChecked ? x.HasItadData : true)))
            );

            // Order
            PART_CbOrder_SelectionChanged(null, null);
        }

        // Search by name
        private void TextboxSearch_TextChanged(object sender, TextChangedEventArgs e)
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
            FilterStore.Text = string.Empty;

            if ((bool)sender.IsChecked)
            {
                SearchStores.Add((string)sender.Tag);
            }
            else
            {
                SearchStores.Remove((string)sender.Tag);
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
            try
            {
                SearchPercentage = (int)((Slider)sender).Value;
                lPercentage.Content = SearchPercentage + "%";
                GetListGame();
            }
            catch
            {
            }
        }

        // Search price
        private void Slider_ValueChangedPrice(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lPrice != null)
            {
                SearchPrice = (int)((Slider)sender).Value;
                lPrice.Content = SearchPrice + _settings.CurrencySign;
                GetListGame();
            }
        }


        private void PART_Tb_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tb = sender as ToggleButton;
            if (tb.Name == "PART_TbOnlyInLibrary")
            {
                PART_TbIncludeInLibrary.IsChecked = false;
                PART_TbIncludeWithoutData.IsChecked = false;
            }
            else
            {
                PART_TbOnlyInLibrary.IsChecked = false;
            }

            GetListGame();
        }
        #endregion


        #region Order
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (tc.SelectedIndex == 0)
            {
                PART_Order.Visibility = Visibility.Visible;
            }
            else
            {
                PART_Order.Visibility = Visibility.Collapsed;
            }
        }

        private void PART_CbOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lbWishlist?.ItemsSource != null)
                {
                    List<Wishlist> wishlists = (List<Wishlist>)lbWishlist.ItemsSource;
                    lbWishlist.ItemsSource = null;

                    if (((ComboBoxItem)PART_CbOrder.SelectedItem).Tag.ToString() == "0")
                    {
                        switch (((ComboBoxItem)PART_CbOrderType.SelectedItem).Tag.ToString())
                        {
                            case "0":
                                wishlists = wishlists.OrderBy(x => x.Name).ToList();
                                break;

                            case "1":
                                wishlists = wishlists.OrderBy(x => x.ItadBestPrice.PriceCut).ToList();
                                break;

                            case "2":
                                wishlists = wishlists.OrderBy(x => x.ItadBestPrice.PriceNew).ToList();
                                break;
                        }
                    }
                    else
                    {
                        switch (((ComboBoxItem)PART_CbOrderType.SelectedItem).Tag.ToString())
                        {
                            case "0":
                                wishlists = wishlists.OrderByDescending(x => x.Name).ToList();
                                break;

                            case "1":
                                wishlists = wishlists.OrderByDescending(x => x.ItadBestPrice.PriceCut).ToList();
                                break;

                            case "2":
                                wishlists = wishlists.OrderByDescending(x => x.ItadBestPrice.PriceNew).ToList();
                                break;
                        }
                    }

                    lbWishlist.ItemsSource = wishlists;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }
        #endregion


        #region Data refresh
        private void ButtonData_Click(object sender, RoutedEventArgs e)
        {
            RefreshData(string.Empty, false);
        }

        private void ButtonPrice_Click(object sender, RoutedEventArgs e)
        {
            RefreshData(string.Empty, true, true);
        }
        #endregion
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public string StoreNameDisplay { get; set; }
        public bool IsCheck { get; set; }
    }
}
