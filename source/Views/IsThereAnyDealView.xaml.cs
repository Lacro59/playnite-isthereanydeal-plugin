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

        public string CurrencySign { get; set; }
        public string _PlainSelected { get; set; }

        private List<Wishlist> lbWishlistItems = new List<Wishlist>();
        private List<string> SearchStores = new List<string>();
        private int SearchPercentage = 0;
        private int SearchPrice = 100;

        private IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
        private ItadGameInfo StorePriceSelected;


        public IsThereAnyDealView(IsThereAnyDeal plugin, IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();

            _PlainSelected = PlainSelected;
            _settings = settings;
            _plugin = plugin;
            _PlayniteApi = PlayniteApi;

            // Load data
            RefreshData();

            GetListGiveaways(PlayniteApi, PluginUserDataPath);

            SetFilterStore();

            lPrice.Content = settings.MaxPrice + _settings.CurrencySign;
            sPrice.Minimum = settings.MinPrice;
            sPrice.Maximum = settings.MaxPrice;

            DataContext = this;
        }

        private void RefreshData()
        {
            dpData.IsEnabled = false;
            var task = Task.Run(() => LoadData(_PlayniteApi, _plugin.GetPluginUserDataPath(), _settings, _PlainSelected))
                .ContinueWith(antecedent =>
                {
                    this.Dispatcher?.Invoke(new Action(() => {
                        try
                        {
                            lbWishlistItems = antecedent.Result;
                            GetListGame();
                            SetInfos();

                            if (!_PlainSelected.IsNullOrEmpty())
                            {
                                int index = 0;
                                foreach (Wishlist wishlist in lbWishlist.ItemsSource)
                                {
                                    if (wishlist.Plain.IsEqual(_PlainSelected))
                                    {
                                        index = ((List<Wishlist>)lbWishlist.ItemsSource).FindIndex(x => x == wishlist);
                                        lbWishlist.SelectedIndex = index;
                                        lbWishlist.ScrollIntoView(lbWishlist.SelectedItem);
                                        break;
                                    }
                                }
                            }
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
            if (_settings.EnableOrigin)
            {
                FilterStoreItems.Add(new ListStore { StoreName = "Origin", StoreNameDisplay = (TransformIcon.Get("Origin") + " Origin").Trim(), IsCheck = false });
            }
            FilterStoreItems.Sort((x, y) => string.Compare(x.StoreName, y.StoreName));
            FilterStore.ItemsSource = FilterStoreItems;
        }

        private void SetInfos()
        {
            tpListBox.ItemsSource = null;
            tpListBox.ItemsSource = isThereAnyDealApi.countDatas;
        }


        private async Task<List<Wishlist>> LoadData (IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(_plugin, PlayniteApi, settings, PluginUserDataPath, true);
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
            try
            {
                StorePriceSelected = (ItadGameInfo)((ListBox)sender).SelectedItem;

                Button bt = (Button)((Grid)((Grid)((ListBox)sender).Parent).Parent).FindName("btStore");
                bt.IsEnabled = true;
                bt.Tag = StorePriceSelected.UrlBuy;
            }
            catch (Exception ex)
            {

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
#if DEBUG
            logger.Debug($"IsThereAnyDeal - BtRemoveWishList_Click() - StorePriceSelected: {Serialization.ToJson(StorePriceSelected)}");
#endif
            var RessultDialog = _PlayniteApi.Dialogs.ShowMessage(
                string.Format(resources.GetString("LOCItadDeleteOnStoreWishList"), StorePriceSelected.Name, StorePriceSelected.ShopName), 
                "IsThereAnyDeal", 
                MessageBoxButton.YesNo
            );
            if (RessultDialog == MessageBoxResult.Yes)
            {
                DataLoadWishlist.Visibility = Visibility.Visible;
                dpData.IsEnabled = false;

                var task = Task.Run(() =>
                {
                    try
                    {
                        if (!StorePriceSelected.StoreId.IsNullOrEmpty())
                        {
                            switch (StorePriceSelected.ShopName.ToLower())
                            {
                                case "steam":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is Steam");
                                    SteamWishlist steamWishlist = new SteamWishlist();
                                    IsDeleted = steamWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        steamWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
#endif
                                    break;
                                case "epic game store":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is Epic");
#endif
                                    EpicWishlist epicWishlist = new EpicWishlist();
                                    IsDeleted = epicWishlist.RemoveWishlist(StorePriceSelected.StoreId, _plugin.GetPluginUserDataPath());
                                    if (IsDeleted)
                                    {
                                        epicWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;
                                case "humble store":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is Humble Store");
#endif
                                    HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist();
                                    IsDeleted = humbleBundleWishlist.RemoveWishlist(_PlayniteApi, StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        humbleBundleWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _settings.HumbleKey, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;
                                case "gog":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is GOG");
#endif
                                    GogWishlist gogWishlist = new GogWishlist(_PlayniteApi);
                                    IsDeleted = gogWishlist.RemoveWishlist(StorePriceSelected.StoreId);
                                    if (IsDeleted)
                                    {
                                        gogWishlist.GetWishlist(_PlayniteApi, StorePriceSelected.SourceId, _plugin.GetPluginUserDataPath(), _settings, false);
                                    }
                                    break;
                                case "microsoft store":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is xbox");
#endif
                                    break;
                                case "origin":
#if DEBUG
                                    logger.Debug($"IsThereAnyDeal - Is origin");
#endif
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
#if DEBUG
                            logger.Debug($"IsThereAnyDeal - IsDeleted");
#endif
                            RefreshData();
                        }
                        else
                        {
#if DEBUG
                            logger.Debug($"IsThereAnyDeal - IsNotDeleted");
#endif
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
#if DEBUG
            logger.Debug($"IsThereAnyDeal - BtHideWishList_Click() - StorePriceSelected: {Serialization.ToJson(StorePriceSelected)}");
#endif
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
            lbWishlistItems = lbWishlistItems.Where(x => _settings.wishlistIgnores.All(y => y.StoreId != x.StoreId && y.Plain != x.Plain)).ToList();
            List<Wishlist> tempList = lbWishlistItems;

            // List options
            if ((bool)PART_TbOnlyInLibrary.IsChecked)
            {
                tempList = tempList.FindAll(x => x.InLibrary).ToList();
            }
            else
            {
                if (!(bool)PART_TbIncludeInLibrary.IsChecked)
                {
                    tempList = tempList.FindAll(x => !x.InLibrary).ToList();
                }
                if (!(bool)PART_TbIncludeWithoutData.IsChecked)
                {
                    tempList = tempList.FindAll(x => x.HasItadData).ToList();
                }
            }


            lbWishlist.ItemsSource = tempList.FindAll(
                x => x.ItadBestPrice.PriceCut >= SearchPercentage && x.ItadBestPrice.PriceNew <= SearchPrice
            );

            if (!TextboxSearch.Text.IsNullOrEmpty() && SearchStores.Count != 0)
            {
                lbWishlist.ItemsSource = tempList.FindAll(
                    x => x.ItadBestPrice.PriceCut >= SearchPercentage && x.ItadBestPrice.PriceNew <= SearchPrice && 
                    x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1 &&
                    (SearchStores.Contains(x.StoreName) || x.Duplicates.FindAll(y => SearchStores.Contains(y.StoreName)).Count > 0)
                );
                return;
            }

            if (!TextboxSearch.Text.IsNullOrEmpty())
            {
                lbWishlist.ItemsSource = tempList.FindAll(
                    x => x.ItadBestPrice.PriceCut >= SearchPercentage && x.ItadBestPrice.PriceNew <= SearchPrice &&
                    x.Name.ToLower().IndexOf(TextboxSearch.Text) > -1
                );
                return;
            }

            if (SearchStores.Count != 0)
            {
                lbWishlist.ItemsSource = tempList.FindAll(
                    x => x.ItadBestPrice.PriceCut >= SearchPercentage && x.ItadBestPrice.PriceNew <= SearchPrice && 
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
            try
            {
                SearchPrice = (int)((Slider)sender).Value;
                lPrice.Content = SearchPrice + _settings.CurrencySign;
                GetListGame();
            }
            catch
            {
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
    }


    public class ListStore
    {
        public string StoreName { get; set; }
        public string StoreNameDisplay { get; set; }
        public bool IsCheck { get; set; }
    }
}
