using System.Collections.Generic;

namespace IsThereAnyDeal.Models
{
    public class ItadRegion
    {
        public string Region { get; set; }
        public string CurrencyName { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySign { get; set; }
        public List<string> Countries { get; set; }

        public override string ToString()
        {
            return Region + " - " + CurrencyName + " - " + CurrencySign;
        }
    }
}
