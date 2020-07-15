using System.Collections.Generic;

namespace IsThereAnyDeal.Models
{
    public class ItadRegion
    {
        public string region { get; set; }
        public string currencyName { get; set; }
        public string currencyCode { get; set; }
        public string currencySign { get; set; }
        public List<string> countries { get; set; }

        public override string ToString()
        {
            return region + " - " + currencyName + " - " + currencySign;
        }
    }
}
