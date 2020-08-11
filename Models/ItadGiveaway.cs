using System;

namespace IsThereAnyDeal.Models
{
    public class ItadGiveaway
    {
        public string TitleAll { get; set; }
        public string Title { get; set; }
        public DateTime? Time { get; set; }
        public string Link { get; set; }
        public string Count { get; set; }
        public string ShopName { get; set; }
        public bool HasSeen { get; set; }
    }
}
