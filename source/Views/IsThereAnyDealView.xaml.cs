using IsThereAnyDeal.ViewModels;
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
            ViewModel.SelectionRequested += (s, item) =>
            {
                lbWishlist.SelectedItem = item;
                lbWishlist.ScrollIntoView(item);
            };

            DataContext = ViewModel;

            Loaded += (s, e) => ViewModel.Initialize(id);
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
