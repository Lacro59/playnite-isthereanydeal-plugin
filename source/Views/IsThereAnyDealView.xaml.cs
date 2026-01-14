using CommonPluginsControls.Controls;
using CommonPluginsShared;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Extensions;
using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

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

            DataContext = ItadDataContext;
            ItadDataContext.MinPrice = Settings.MinPrice;
            ItadDataContext.MaxPrice = Settings.MaxPrice;

            PART_DateData.Content = "";

            lbWishlist.ItemsSource = new ObservableCollection<Wishlist>();

            // Subscribe to the Loaded event instead of calling methods here
            Loaded += (s, e) =>
            {
                // Now the UI is ready to display the loading bar
                RefreshData(id);
                GetListGiveaways();
            };
        }


        // Optimized RefreshData method to prevent UI blocking
        private async void RefreshData(string id, bool cachOnly = true, bool forcePrice = false)
        {
            DataLoadWishlist.Visibility = Visibility.Visible;
            dpData.IsEnabled = false;

            try
            {
                // 1. Heavy background loading
                var result = await Task.Run(() => LoadData(cachOnly, forcePrice));

                if (result == null) return;

                // 2. IMPORTANT: Give the UI a moment to refresh the ProgressBar animation
                // before starting the heavy UI attachment
                await Task.Yield();

                // 3. Assign data
                lbWishlist.ItemsSource = result;

                // 4. Update Infos (Ensure this method isn't doing too much heavy math)
                SetInfos();

                // Safe currency sign retrieval
                var firstItem = result.FirstOrDefault(x => x.ItadLastPrice?.Any(y => !string.IsNullOrEmpty(y.CurrencySign)) == true);
                if (firstItem != null)
                {
                    ItadDataContext.CurrencySign = firstItem.ItadBestPrice.CurrencySign;
                }

                // 5. Yield again before heavy Filtering/Sorting
                await Task.Delay(10);

                // 6. Execute Sorting and Filtering with lower priority
                // This prevents the UI from freezing while the CollectionView is being rebuilt
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        var selectedItem = result.FirstOrDefault(x => x.Game?.Id?.IsEqual(id) ?? false);
                        if (selectedItem != null)
                        {
                            lbWishlist.SelectedItem = selectedItem;
                            lbWishlist.ScrollIntoView(selectedItem);
                        }
                    }

                    PART_DateData.Content = new LocalDateTimeConverter().Convert(Settings.LastRefresh, null, null, CultureInfo.CurrentCulture);

                    // These two are the real "UI blockers" as they trigger re-rendering of the whole list
                    PART_CbOrder_SelectionChanged(null, null);
                    SetFilterStore();

                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "IsThereAnyDeal");
            }
            finally
            {
                // We only hide the loader once everything (including Background priority tasks) is done
                DataLoadWishlist.Visibility = Visibility.Collapsed;
                dpData.IsEnabled = true;
            }
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
                FilterStoreItems.Add(new ListStore { StoreName = "Epic Games Store", StoreNameDisplay = (TransformIcon.Get("Epic Games Store") + " Epic Games Store").Trim(), IsCheck = false });
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
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName, true));
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

        private List<ItadGiveaway> LoadDatatGiveaways()
        {
            List<ItadGiveaway> itadGiveaways = IsThereAnyDealApi.GetGiveaways();
            return itadGiveaways;
        }

        private async void GetListGiveaways()
        {
            // Initialize UI state: disable the grid and clear previous items
            gGiveaways.IsEnabled = false;
            gGiveaways.Children.Clear();
            gGiveaways.RowDefinitions.Clear();

            // Add the first row definition (default height)
            gGiveaways.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            try
            {
                // Offload data loading to a background thread to keep UI responsive
                var itadGiveaways = await Task.Run(() => LoadDatatGiveaways());

                if (itadGiveaways == null || itadGiveaways.Count == 0)
                {
                    return;
                }

                // Execution resumes here on the UI Thread automatically
                int row = 0;
                int col = 0;
                LocalDateConverter localDateConverter = new LocalDateConverter();
                FontFamily icoFont = ResourceProvider.GetResource("FontIcoFont") as FontFamily;

                foreach (ItadGiveaway itadGiveaway in itadGiveaways)
                {
                    // Grid layout logic: 4 columns per row (stepping by 2 for spacing/layout)
                    if (col > 3)
                    {
                        col = 0;
                        row += 1;
                        gGiveaways.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
                    }

                    // Create the main container for the giveaway item
                    DockPanel dp = new DockPanel { Width = 540 };

                    // Website Button - Docked Right
                    Button bt = new Button
                    {
                        ToolTip = ResourceProvider.GetString("LOCWebsiteLabel"),
                        Content = "\uf028",
                        FontFamily = icoFont,
                        Tag = itadGiveaway.Link,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 0, 0, 0),
                    };
                    bt.Click += WebGiveaway;
                    DockPanel.SetDock(bt, Dock.Right);
                    dp.Children.Add(bt);

                    // Expiration Date handling - Docked Right
                    if (itadGiveaway.Time != null)
                    {
                        bool isExpired = itadGiveaway.Time < DateTime.Now;

                        TextBlock tbDateText = new TextBlock
                        {
                            Text = $"{localDateConverter.Convert(itadGiveaway.Time, null, null, CultureInfo.CurrentCulture)}",
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        if (isExpired)
                        {
                            tbDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89363a"));
                        }
                        DockPanel.SetDock(tbDateText, Dock.Right);

                        TextBlock tbDateIcon = new TextBlock
                        {
                            Text = "\uf006",
                            FontFamily = icoFont,
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        if (isExpired)
                        {
							tbDateIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89363a"));
                        }
                        DockPanel.SetDock(tbDateIcon, Dock.Right);

                        dp.Children.Add(tbDateText);
                        dp.Children.Add(tbDateIcon);
                    }

                    // Title - Main content
                    TextBlockTrimmed tbTitle = new TextBlockTrimmed
                    {
                        Text = itadGiveaway.Title,
                        VerticalAlignment = VerticalAlignment.Center,
                        MaxWidth = 350
                    };
                    dp.Children.Add(tbTitle);

                    // Store Name
                    TextBlockTrimmed tbShop = new TextBlockTrimmed
                    {
                        Text = itadGiveaway.ShopName,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(itadGiveaway.ShopColor)),
                        Margin = new Thickness(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    dp.Children.Add(tbShop);

                    // Status Badges (Waitlist / Collection)
                    if (itadGiveaway.InWaitlist)
                    {
                        dp.Children.Add(new TextBlock
                        {
                            Text = "\uec3f",
                            FontFamily = icoFont,
                            ToolTip = ResourceProvider.GetString("LOCItadInWaitlist"),
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#02b65a")),
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    if (itadGiveaway.InCollection)
                    {
                        dp.Children.Add(new TextBlock
                        {
                            Text = "\uec5c",
                            FontFamily = icoFont,
                            ToolTip = ResourceProvider.GetString("LOCItadInCollection"),
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1494e4")),
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }

                    // Set Grid position and add to the container
                    Grid.SetRow(dp, row);
                    Grid.SetColumn(dp, col);
                    gGiveaways.Children.Add(dp);

                    col += 2;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while getting giveaways list");
            }
            finally
            {
                // Ensure the UI is re-enabled even if an error occurs
                gGiveaways.IsEnabled = true;
            }
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
                                    EaWishlist originWishlist = new EaWishlist(Plugin);
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
				if (lbWishlist?.ItemsSource == null) return;

				var view = CollectionViewSource.GetDefaultView(lbWishlist.ItemsSource);
				view.SortDescriptions.Clear();

				string orderType = ((ComboBoxItem)PART_CbOrderType.SelectedItem).Tag.ToString();
				bool isAscending = ((ComboBoxItem)PART_CbOrder.SelectedItem).Tag.ToString() == "0";

				ListSortDirection direction = isAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

				switch (orderType)
				{
					case "0":
						view.SortDescriptions.Add(new SortDescription("Name", direction));
						break;
					case "1":
						view.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceCut", direction));
						break;
					case "2":
						view.SortDescriptions.Add(new SortDescription("ItadBestPrice.PriceNew", direction));
						break;
					case "3":
						view.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceCut", direction));
						break;
					case "4":
						view.SortDescriptions.Add(new SortDescription("ItadPriceForWishlistStore.PriceNew", direction));
						break;
					case "5":
						view.SortDescriptions.Add(new SortDescription("ReleaseDate", direction));
						break;
				}

				view.Filter = UserFilter;
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