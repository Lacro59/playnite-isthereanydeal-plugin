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
using System.Windows.Media;
using CommonPluginsControls.Controls;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private IsThereAnyDealSettings Settings { get; }
        private IsThereAnyDeal Plugin { get; }

        private List<string> SearchStores { get; set; } = new List<string>();

        private IsThereAnyDealApi IsThereAnyDealApi { get; set; } = new IsThereAnyDealApi();
        private ItadGameInfo StorePriceSelected { get; set; }


        private ItadDataContext ItadDataContext { get; set; } = new ItadDataContext();


        public IsThereAnyDealView(IsThereAnyDeal plugin, string id = "")
        {
            InitializeComponent();

            Settings = plugin.PluginSettings.Settings;
            Plugin = plugin;

            // Load data
            RefreshData(id);
            GetListGiveaways(plugin.GetPluginUserDataPath());

            DataContext = ItadDataContext;
            ItadDataContext.MinPrice = Settings.MinPrice;
            ItadDataContext.MaxPrice = Settings.MaxPrice;

            lbWishlist.ItemsSource = new ObservableCollection<Wishlist>();
        }

        private void RefreshData(string id, bool cachOnly = true, bool forcePrice = false)
        {
            DataLoadWishlist.Visibility = Visibility.Visible;
            dpData.IsEnabled = false;

            Task task = Task.Run(() => LoadData(cachOnly, forcePrice))
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        try
                        {
                            lbWishlist.ItemsSource = antecedent.Result;

                            SetInfos();

                            ItadDataContext.CurrencySign = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource)?.Where(x => x.ItadLastPrice != null && x.ItadLastPrice.Where(y => !y.CurrencySign.IsNullOrEmpty()).Count() > 0)?.FirstOrDefault()?.ItadBestPrice.CurrencySign;

                            if (!id.IsNullOrEmpty())
                            {
                                int index = 0;
                                foreach (Wishlist wishlist in lbWishlist.ItemsSource)
                                {
                                    if (wishlist.Game?.Id?.IsEqual(id) ?? false)
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

                            // Order
                            PART_CbOrder_SelectionChanged(null, null);

                            SetFilterStore();
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
            ItadDataContext.FilterStoreItems = FilterStoreItems;
        }

        private void SetInfos()
        {
            tpListBox.ItemsSource = null;
            tpListBox.ItemsSource = IsThereAnyDealApi.CountDatas;
        }


        private ObservableCollection<Wishlist> LoadData(bool cachOnly = true, bool forcePrice = false)
        {
            ObservableCollection<Wishlist> ListWishlist = IsThereAnyDealApi.LoadWishlist(Plugin, cachOnly, forcePrice).ToObservable();
            return ListWishlist;
        }

        private List<ItadGiveaway> LoadDatatGiveaways(string pluginUserDataPath)
        {
            List<ItadGiveaway> itadGiveaways = IsThereAnyDealApi.GetGiveaways(pluginUserDataPath);
            return itadGiveaways;
        }

        private void GetListGiveaways(string pluginUserDataPath)
        {
            _ = Task.Run(() => LoadDatatGiveaways(pluginUserDataPath))
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
                                RowDefinition rowAuto = new RowDefinition
                                {
                                    Height = new GridLength(40)
                                };
                                gGiveaways.RowDefinitions.Add(rowAuto);
                            }

                            DockPanel dp = new DockPanel
                            {
                                Width = 540
                            };

                            TextBlockTrimmed tb = new TextBlockTrimmed
                            {
                                Text = itadGiveaway.Title,
                                VerticalAlignment = VerticalAlignment.Center,
                                MaxWidth = 350
                            };

                            TextBlockTrimmed tbShop = new TextBlockTrimmed
                            {
                                Text = itadGiveaway.ShopName,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(itadGiveaway.ShopColor)),
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            };

                            TextBlock tbWaitlist = new TextBlock
                            {
                                Text = "\uec3f",
                                FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                                ToolTip = ResourceProvider.GetString("LOCItadInWaitlist"),
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#02b65a")),
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            };

                            TextBlock tbCollection = new TextBlock
                            {
                                Text = "\uec5c",
                                FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                                ToolTip = ResourceProvider.GetString("LOCItadInCollection"),
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1494e4")),
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            };

                            LocalDateConverter localDateConverter = new LocalDateConverter();
                            TextBlock tbDate = new TextBlock
                            {
                                Text = "\uf006",
                                FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                                Visibility = itadGiveaway.Time == null ? Visibility.Hidden : Visibility.Visible
                            };
                            TextBlock tbDate2 = new TextBlock
                            {
                                Text = $"{localDateConverter.Convert(itadGiveaway.Time, null, null, CultureInfo.CurrentCulture)}",
                                Margin = new Thickness(10, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                                Visibility = itadGiveaway.Time == null ? Visibility.Hidden : Visibility.Visible
                            };
                            if (itadGiveaway.Time < DateTime.Now)
                            {
                                tbDate.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89363a"));
                                tbDate2.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89363a"));
                            }
                            DockPanel.SetDock(tbDate, Dock.Right);
                            DockPanel.SetDock(tbDate2, Dock.Right);

                            Button bt = new Button
                            {
                                ToolTip = ResourceProvider.GetString("LOCWebsiteLabel"),
                                Content = "\uf028",
                                FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                                Tag = itadGiveaway.Link,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(10, 0, 0, 0),
                            };
                            bt.Click += new RoutedEventHandler(WebGiveaway);
                            DockPanel.SetDock(bt, Dock.Right);

                            _ = dp.Children.Add(bt);
                            _ = dp.Children.Add(tbDate2);
                            _ = dp.Children.Add(tbDate);
                            _ = dp.Children.Add(tb);
                            _ = dp.Children.Add(tbShop);

                            if (itadGiveaway.InWaitlist)
                            {
                                _ = dp.Children.Add(tbWaitlist);
                            }
                            if (itadGiveaway.InCollection)
                            {
                                _ = dp.Children.Add(tbCollection);
                            }

                            Grid.SetRow(dp, row);
                            Grid.SetColumn(dp, col);

                            col += 2;

                            _ = gGiveaways.Children.Add(dp);
                        }
                    }));
                });
        }

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
            StorePriceSelected = (ItadGameInfo)((Button)sender).Tag;

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
            StorePriceSelected = (ItadGameInfo)((Button)sender).Tag;

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
            return ((bool)PART_RbBest.IsChecked ? wishlist.ItadBestPrice.PriceCut >= ItadDataContext.DiscountPercent : wishlist.ItadPriceForWishlistStore.PriceCut >= ItadDataContext.DiscountPercent)
                && ((bool)PART_RbBest.IsChecked ? wishlist.ItadBestPrice.PriceNew <= ItadDataContext.PriceLimit : wishlist.ItadPriceForWishlistStore.PriceNew <= ItadDataContext.PriceLimit)
                && (TextboxSearch.Text.IsNullOrEmpty() || wishlist.Name.Contains(TextboxSearch.Text, StringComparison.InvariantCultureIgnoreCase))
                && (SearchStores.Count == 0 || SearchStores.Contains(wishlist.StoreName) || wishlist.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0)
                && Settings.wishlistIgnores.All(y => y.StoreId != wishlist.StoreId && !y.Name.IsEqual(wishlist.Name))
                && ((bool)PART_TbOnlyInLibrary.IsChecked ? wishlist.InLibrary : (!(bool)PART_TbIncludeInLibrary.IsChecked ? !wishlist.InLibrary : (false && (bool)PART_TbIncludeWithoutData.IsChecked) || wishlist.HasItadData));
        }

        // Get list
        private void GetListGame()
        {
            if (lbWishlist?.ItemsSource != null)
            {
                CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource).Refresh();
            }
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
            if (PART_Order != null)
            {
                PART_Order.Visibility = tc.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PART_CbOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lbWishlist?.ItemsSource != null)
                {
                    if (((ComboBoxItem)PART_CbOrder.SelectedItem).Tag.ToString() == "0")
                    {
                        switch (((ComboBoxItem)PART_CbOrderType.SelectedItem).Tag.ToString())
                        {
                            case "0":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderBy(x => x.Name).ToObservable();
                                break;

                            case "1":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderBy(x => x.ItadBestPrice.PriceCut).ToObservable();
                                break;

                            case "2":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderBy(x => x.ItadBestPrice.PriceNew).ToObservable();
                                break;

                            case "3":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderBy(x => x.ItadPriceForWishlistStore.PriceCut).ToObservable();
                                break;

                            case "4":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderBy(x => x.ItadPriceForWishlistStore.PriceNew).ToObservable();
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
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderByDescending(x => x.Name).ToObservable();
                                break;

                            case "1":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderByDescending(x => x.ItadBestPrice.PriceCut).ToObservable();
                                break;

                            case "2":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderByDescending(x => x.ItadBestPrice.PriceNew).ToObservable();
                                break;

                            case "3":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderByDescending(x => x.ItadPriceForWishlistStore.PriceCut).ToObservable();
                                break;

                            case "4":
                                lbWishlist.ItemsSource = ((ObservableCollection<Wishlist>)lbWishlist.ItemsSource).OrderByDescending(x => x.ItadPriceForWishlistStore.PriceNew).ToObservable();
                                break;

                            default:
                                break;
                        }
                    }

                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource);
                    view.Filter = UserFilter;
                    GetListGame();
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

        private void PART_Rb_Click(object sender, RoutedEventArgs e)
        {
            GetListGame();
        }
    }


    public class ItadDataContext : ObservableObject
    {
        private string _currencySign = "$";
        public string CurrencySign { get => _currencySign; set => SetValue(ref _currencySign, value); }

        private double _minPrice = 0;
        public double MinPrice { get => _minPrice; set => SetValue(ref _minPrice, value); }

        private double _maxPrice = 250;
        public double MaxPrice { get => _maxPrice; set => SetValue(ref _maxPrice, value); }



        private double _discountPercent = 0;
        public double DiscountPercent { get => _discountPercent; set => SetValue(ref _discountPercent, value); }

        private double _priceLimit = 100;
        public double PriceLimit { get => _priceLimit; set => SetValue(ref _priceLimit, value); }

        private List<ListStore> _filterStoreItems = new List<ListStore>();
        public List<ListStore> FilterStoreItems { get => _filterStoreItems; set => SetValue(ref _filterStoreItems, value); }
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public string StoreNameDisplay { get; set; }
        public bool IsCheck { get; set; }
    }
}
