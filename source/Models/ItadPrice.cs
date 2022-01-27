using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models
{
    public class ItadPrice
    {
        public double price_new { get; set; }
        public double price_old { get; set; }
        public int price_cut { get; set; }
        public string url { get; set; }
        public Shop shop { get; set; }
        public List<string> drm { get; set; }
    }

    public class Shop
    {
        public string id { get; set; }
        public string name { get; set; }
    }
}
