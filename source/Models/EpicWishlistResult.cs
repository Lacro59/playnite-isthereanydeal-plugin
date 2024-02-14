using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models
{
    public class EpicWishlistResult
    {
        public EpicData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class KeyImage
    {
        public string type { get; set; }
        public string url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class Offer
    {
        public string title { get; set; }
        public List<KeyImage> keyImages { get; set; }
    }

    public class Element
    {
        public string offerId { get; set; }
        public string @namespace { get; set; }
        public Offer offer { get; set; }
    }

    public class WishlistItems
    {
        public List<Element> elements { get; set; }
    }

    public class EpicWishlistItems
    {
        public WishlistItems wishlistItems { get; set; }
    }

    public class EpicData
    {
        public EpicWishlistItems Wishlist { get; set; }
    }

    public class Extensions
    {
    }
}
