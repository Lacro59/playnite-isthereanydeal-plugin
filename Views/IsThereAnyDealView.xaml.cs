using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
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

        public string CurrencySign { get; set; }
        public string PlainSelected { get; set; }

        public IsThereAnyDealView(IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();
            this.PlainSelected = PlainSelected;

            // Load data
            var task = Task.Run(() => LoadData(PlayniteApi, PluginUserDataPath, settings, PluginUserDataPath))
                .ContinueWith(antecedent => 
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        lbWishlist.ItemsSource = antecedent.Result;
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
                    }));
                });

            DataContext = this;
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
    }
}
