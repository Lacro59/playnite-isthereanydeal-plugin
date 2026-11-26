namespace IsThereAnyDeal.Models
{
    /// <summary>
    /// Two wishlist cards shown side by side in the multi-column layout,
    /// so the list keeps a virtualized one-row-per-item panel.
    /// </summary>
    public class WishlistRow
    {
        /// <summary>Creates a row with its left card.</summary>
        /// <param name="left">Card shown in the left column.</param>
        public WishlistRow(Wishlist left)
        {
            Left = left;
        }

        /// <summary>Card shown in the left column.</summary>
        public Wishlist Left { get; }

        /// <summary>Card shown in the right column; null on the last row of an odd count.</summary>
        public Wishlist Right { get; set; }

        /// <summary>True when <paramref name="item"/> is one of the two cards of this row.</summary>
        /// <param name="item">Wishlist entry to look for.</param>
        public bool Contains(Wishlist item)
        {
            return item != null && (ReferenceEquals(Left, item) || ReferenceEquals(Right, item));
        }
    }
}
