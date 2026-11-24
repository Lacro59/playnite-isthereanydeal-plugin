using CommonPluginsShared.UI;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.ViewModels;
using System.Linq;
using System.Windows.Controls;

namespace IsThereAnyDeal.Views
{
    /// <summary>
    /// Logique d'interaction pour IsThereAnyDealView.xaml
    /// </summary>
    public partial class IsThereAnyDealView : UserControl
    {
        private IsThereAnyDealViewModel ViewModel { get; }


        /// <summary>
        /// Creates the main ITAD wishlist / giveaways view.
        /// </summary>
        /// <param name="plugin">Plugin instance.</param>
        /// <param name="id">Optional ITAD game id to select after load.</param>
        /// <param name="filterAlerts">When true, opens with the alerts-only filter active.</param>
        public IsThereAnyDealView(IsThereAnyDeal plugin, string id = "", bool filterAlerts = false)
        {
            InitializeComponent();

            ViewModel = new IsThereAnyDealViewModel(plugin, filterAlerts);
            ViewModel.SelectionRequested += (s, item) => SelectWishlistItem(item);

            DataContext = ViewModel;

            Loaded += (s, e) => ViewModel.Initialize(id);
        }

        /// <summary>
        /// Selects and scrolls to a wishlist entry; in two-column mode, scrolls to its row and focuses its card.
        /// </summary>
        /// <param name="item">Wishlist entry to bring into view.</param>
        private void SelectWishlistItem(Wishlist item)
        {
            if (!ViewModel.IsMultiColumn)
            {
                lbWishlist.SelectedItem = item;
                lbWishlist.ScrollIntoView(item);
                return;
            }

            WishlistRow row = ViewModel.WishlistRows.FirstOrDefault(x => x.Contains(item));
            if (row == null)
            {
                return;
            }

            lbWishlist.ScrollIntoView(row);
            lbWishlist.UpdateLayout();

            ListBoxItem rowContainer = lbWishlist.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;
            if (rowContainer == null)
            {
                return;
            }

            ListBoxItem card = UIHelper.FindVisualChildren<ListBoxItem>(rowContainer)
                .FirstOrDefault(x => ReferenceEquals(x.Content, item));
            card?.Focus();
        }

        private void PART_BtRefresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button?.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }
}
