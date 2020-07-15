using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using Playnite.Controls;
using Playnite.SDK;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : WindowBase
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public string CurrencySign { get; set; }

        public IsThereAnyDealView(IPlayniteAPI PlayniteApi, string PluginUserDataPath, IsThereAnyDealSettings settings, string PlainSelected = "")
        {
            InitializeComponent();

            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
            List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(PlayniteApi, settings, PluginUserDataPath);
            lbWishlist.ItemsSource = ListWishlist;

            if (!PlainSelected.IsNullOrEmpty())
            {
                int index = 0;
                foreach (Wishlist wishlist in ListWishlist)
                {
                    if (wishlist.Plain == PlainSelected)
                    {
                        lbWishlist.SelectedIndex = index;
                        lbWishlist.UpdateLayout();
                    }
                    index += 1;
                }
            }

            DataContext = this;
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
