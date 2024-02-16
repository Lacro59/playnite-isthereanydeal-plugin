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
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();
        private static IResourceProvider ResourceProvider => new ResourceProvider();

        private readonly IsThereAnyDealSettings Settings;
        private readonly IsThereAnyDeal Plugin;

        private List<string> SearchStores { get; set; } = new List<string>();

        private IsThereAnyDealApi IsThereAnyDealApi { get; set; } = new IsThereAnyDealApi();
        private ItadGameInfo StorePriceSelected { get; set; }


        private readonly ItadDataContext itadDataContext = new ItadDataContext();


        public IsThereAnyDealView(IsThereAnyDeal plugin, IsThereAnyDealSettings settings, string id = "")
        {
            InitializeComponent();

            Settings = settings;
            Plugin = plugin;

            // Load data
            RefreshData(id);

            //GetListGiveaways(PluginUserDataPath);
            SetFilterStore();

            DataContext = itadDataContext;
            itadDataContext.MinPrice = Settings.MinPrice;
            itadDataContext.MaxPrice = Settings.MaxPrice;

            lbWishlist.ItemsSource = new ObservableCollection<Wishlist>();
        }

        private void RefreshData(string id, bool CachOnly = true, bool ForcePrice = false)
        {
            DataLoadWishlist.Visibility = Visibility.Visible;
            dpData.IsEnabled = false;

            Task task = Task.Run(() => LoadData(Plugin.GetPluginUserDataPath(), Settings, CachOnly, ForcePrice))
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        try
                        {
                            lbWishlist.ItemsSource = antecedent.Result;
                            GetListGame();
                            SetInfos();

                            itadDataContext.CurrencySign = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource)?.Where(x => x.ItadLastPrice != null && x.ItadLastPrice.Where(y => !y.CurrencySign.IsNullOrEmpty()).Count() > 0)?.FirstOrDefault()?.ItadBestPrice.CurrencySign;

                            if (!id.IsNullOrEmpty())
                            {
                                int index = 0;
                                foreach (Wishlist wishlist in lbWishlist.ItemsSource)
                                {
                                    if (wishlist.Game.Id.IsEqual(id))
                                    {
                                        index = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).ToList().FindIndex(x => x == wishlist);
                                        lbWishlist.SelectedIndex = index;
                                        lbWishlist.ScrollIntoView(lbWishlist.SelectedItem);
                                        break;
                                    }
                                }
                            }
                            PART_DateData.Content = new LocalDateTimeConverter().Convert(Settings.LastRefresh, null, null, CultureInfo.CurrentCulture);
                            DataLoadWishlist.Visibility = Visibility.Collapsed;
                            dpData.IsEnabled = true;

                            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource);
                            view.Filter = UserFilter;
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
            if (Settings.EnableSteam)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Steam", StoreNameDisplay = (TransformIcon.Get("Steam") + " Steam").Trim(), IsCheck = false });
            }
            if (Settings.EnableGog)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "GOG", StoreNameDisplay = (TransformIcon.Get("GOG") + " GOG").Trim(), IsCheck = false });
            }
            if (Settings.EnableHumble)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Humble", StoreNameDisplay = (TransformIcon.Get("Humble") + " Humble").Trim(), IsCheck = false });
            }
            if (Settings.EnableEpic)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Epic", StoreNameDisplay = (TransformIcon.Get("Epic") + " Epic").Trim(), IsCheck = false });
            }
            if (Settings.EnableXbox)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Microsoft Store", StoreNameDisplay = (TransformIcon.Get("Xbox") + " Microsoft Store").Trim(), IsCheck = false });
            }
            if (Settings.EnableUbisoft)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Ubisoft Connect", StoreNameDisplay = (TransformIcon.Get("Ubisoft Connect") + " Ubisoft Connect").Trim(), IsCheck = false });
            }
            if (Settings.EnableOrigin)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "EA app", StoreNameDisplay = (TransformIcon.Get("EA app") + " EA app").Trim(), IsCheck = false });
            }
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName));
            itadDataContext.FilterStoreItems = FilterStoreItems;
        }

        private void SetInfos()
        {
            tpListBox.ItemsSource = null;
            tpListBox.ItemsSource = IsThereAnyDealApi.CountDatas;
        }


        private ObservableCollection<Wishlist> LoadData(string PluginUserDataPath, IsThereAnyDealSettings settings, bool CachOnly = true, bool ForcePrice = false)
        {
            ObservableCollection<Wishlist> ListWishlist = IsThereAnyDealApi.LoadWishlist(Plugin, settings, PluginUserDataPath, CachOnly, ForcePrice).ToObservable();
            return ListWishlist;
        }

        //private List<ItadGiveaway> LoadDatatGiveaways(string PluginUserDataPath)
        //{
        //    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(PluginUserDataPath);
        //    return itadGiveaways;
        //}

        /*
        private void GetListGiveaways(string PluginUserDataPath)
        {
            _ = Task.Run(() => LoadDatatGiveaways(PluginUserDataPath))
                .ContinueWith(antecedent =>
                {
                    if (antecedent?.Result == null)
                    {
                        return;
                    }

                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        List<ItadGiveaway> itadGiveaways = antecedent.Result;
                        int row = 0;
                        int col = 0;
                        foreach (ItadGiveaway itadGiveaway in itadGiveaways)
                        {
                            if (col > 3)
                            {
                                col = 0;
                                row += 1;
                                RowDefinition rowAuto = new RowDefinition();
                                rowAuto.Height = new GridLength(40);
                                gGiveaways.RowDefinitions.Add(rowAuto);
                            }

                            DockPanel dp = new DockPanel();
                            dp.Width = 540;

                            TextBlock tb = new TextBlock();
                            tb.Text = itadGiveaway.TitleAll;
                            tb.VerticalAlignment = VerticalAlignment.Center;
                            tb.Width = 440;
                            tb.VerticalAlignment = VerticalAlignment.Center;

                            Button bt = new Button();
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
        */

        private void WebGiveaway(object sender, RoutedEventArgs e)
        {
            _ = Process.Start((string)((Button)sender).Tag);
        }


        #region Button for each game in wishlist
        private void ButtonWeb_Click(object sender, RoutedEventArgs e)
        {
            string Tag = (string)((Button)sender).Tag;
            if (!Tag.IsNullOrEmpty())
            {
                _ = Process.Start(Tag);
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

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadDeleteOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName),
                "IsThereAnyDeal", 
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                DataLoadWishlist.Visibility = Visibility.Visible;
                dpData.IsEnabled = false;

                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!StorePriceSelected.StoreId.IsNullOrEmpty())
                        {
                            switch (StorePriceSelected.ShopName.ToLower())
                            {
                                case "steam":
                                    SteamWishlist steamWishlist = new SteamWishlist(Plugin);
                                    IsDeleted = steamWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = steamWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "epic game store":
                                    EpicWishlist epicWishlist = new EpicWishlist(Plugin);
                                    IsDeleted = epicWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = epicWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "humble store":
                                    HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist(Plugin);
                                    IsDeleted = humbleBundleWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = humbleBundleWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "gog":
                                    GogWishlist gogWishlist = new GogWishlist(Plugin);
                                    IsDeleted = gogWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = gogWishlist.GetWishlist(false);
                                    }
                                    break;

                                case "microsoft store":
                                    break;

                                case "origin":
                                case "ea app":
                                    OriginWishlist originWishlist = new OriginWishlist(Plugin);
                                    IsDeleted = originWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        _ = originWishlist.GetWishlist(false);
                                    }
                                    break;

                                default:
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
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        if (IsDeleted)
                        {
                            RefreshData(string.Empty);
                        }

                        DataLoadWishlist.Visibility = Visibility.Collapsed;
                        dpData.IsEnabled = true;
                    }));
                });
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

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCItadHideOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName),
                "IsThereAnyDeal",
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    Settings.wishlistIgnores.Add(new WishlistIgnore
                    {
                        StoreId = StorePriceSelected.StoreId,
                        StoreName = StorePriceSelected.ShopName,
                        Name = StorePriceSelected.Name,
                        Id = StorePriceSelected.Id,
                        Slug = StorePriceSelected.Slug
                    });
                    Plugin.SavePluginSettings(Settings);
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
        private bool UserFilter(object item)
        {
            Wishlist wishlist = item as Wishlist;
            return wishlist.ItadBestPrice.PriceCut >= itadDataContext.DiscountPercent
                && wishlist.ItadBestPrice.PriceNew <= itadDataContext.PriceLimit
                && (TextboxSearch.Text.IsNullOrEmpty() || wishlist.Name.Contains(TextboxSearch.Text, StringComparison.InvariantCultureIgnoreCase))
                && (SearchStores.Count == 0 || SearchStores.Contains(wishlist.StoreName) || wishlist.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0)
                && (wishlist.Game == null || Settings.wishlistIgnores.All(y => y.StoreId != wishlist.StoreId && y.Id != wishlist.Game.Id))
                && ((bool)PART_TbOnlyInLibrary.IsChecked ? wishlist.InLibrary : (!(bool)PART_TbIncludeInLibrary.IsChecked ? !wishlist.InLibrary : (false && (bool)PART_TbIncludeWithoutData.IsChecked) || wishlist.HasItadData));
        }

        // Get list
        private void GetListGame()
        {
            if (lbWishlist?.ItemsSource != null)
            {
                CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource).Refresh();
            }

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
                _ = SearchStores.Remove((string)sender.Tag);
            }

            if (SearchStores.Count != 0)
            {
                FilterStore.Text = string.Join(", ", SearchStores);
            }

            GetListGame();
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
            PART_Order.Visibility = tc.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
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

                            default:
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

                            default:
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


        // Search price
        // Search percentage
        private void On_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            GetListGame();
        }
    }


    public class ItadDataContext : ObservableObject
    {
        private ObservableCollection<Wishlist> _ItemsSource = new ObservableCollection<Wishlist>();
        public ObservableCollection<Wishlist> ItemsSource { get => _ItemsSource; set => SetValue(ref _ItemsSource, value); }
    
        private string _CurrencySign = "$";
        public string CurrencySign { get => _CurrencySign; set => SetValue(ref _CurrencySign, value); }

        private double _MinPrice = 0;
        public double MinPrice { get => _MinPrice; set => SetValue(ref _MinPrice, value); }

        private double _MaxPrice = 250;
        public double MaxPrice { get => _MaxPrice; set => SetValue(ref _MaxPrice, value); }



        private double _DiscountPercent = 0;
        public double DiscountPercent { get => _DiscountPercent; set => SetValue(ref _DiscountPercent, value); }

        private double _PriceLimit = 100;
        public double PriceLimit { get => _PriceLimit; set => SetValue(ref _PriceLimit, value); }

        private List<ListStore> _FilterStoreItems = new List<ListStore>();
        public List<ListStore> FilterStoreItems { get => _FilterStoreItems; set => SetValue(ref _FilterStoreItems, value); }
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public string StoreNameDisplay { get; set; }
        public bool IsCheck { get; set; }
    }
}
