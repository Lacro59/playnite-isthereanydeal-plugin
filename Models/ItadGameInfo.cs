using Newtonsoft.Json;


namespace IsThereAnyDeal.Models
{
    public class ItadGameInfo
    {
        public string plain { get; set; }
        //public string title { get; set; }
        public double price_new { get; set; }
        public double price_old { get; set; }
        public double price_cut { get; set; }
        public string currency_sign { get; set; }
        //public DateTime added { get; set; }
        public string shop_name { get; set; }
        public string shop_color { get; set; }
        public string url_buy { get; set; }
        //public string url_game { get; set; }

        [JsonIgnore]
        public string url_game
        {
            get
            {
                return string.Format("https://isthereanydeal.com/game/{0}/info/", plain);
            }
        }
    }
}
