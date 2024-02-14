using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models.Api
{
    public class GamePrices
    {
        [SerializationPropertyName("id")]
        public string Id { get; set; }

        [SerializationPropertyName("deals")]
        public List<Deal> Deals { get; set; }
    }

    public class Deal
    {
        [SerializationPropertyName("shop")]
        public Element Shop { get; set; }

        [SerializationPropertyName("price")]
        public Price Price { get; set; }

        [SerializationPropertyName("regular")]
        public Price Regular { get; set; }

        [SerializationPropertyName("cut")]
        public int Cut { get; set; }

        [SerializationPropertyName("voucher")]
        public object Voucher { get; set; }

        [SerializationPropertyName("storeLow")]
        public Price StoreLow { get; set; }

        [SerializationPropertyName("historyLow")]
        public Price HistoryLow { get; set; }

        [SerializationPropertyName("flag")]
        public object Flag { get; set; }

        [SerializationPropertyName("drm")]
        public List<Element> Drm { get; set; }

        [SerializationPropertyName("platforms")]
        public List<Element> Platforms { get; set; }

        [SerializationPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [SerializationPropertyName("expiry")]
        public object Expiry { get; set; }

        [SerializationPropertyName("url")]
        public string Url { get; set; }
    }

    public class Element
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }
    }

    public class Price
    {
        [SerializationPropertyName("amount")]
        public double Amount { get; set; }

        [SerializationPropertyName("amountInt")]
        public int AmountInt { get; set; }

        [SerializationPropertyName("currency")]
        public string Currency { get; set; }
    }
}
