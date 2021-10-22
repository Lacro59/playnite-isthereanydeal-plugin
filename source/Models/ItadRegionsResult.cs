using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models
{
    public class ItadRegionsResult
    {
        public Data data { get; set; }
    }

    public class Currency
    {
        public string code { get; set; }
        public string sign { get; set; }
        public string delimiter { get; set; }
        public bool left { get; set; }
        public string name { get; set; }
        public string html { get; set; }
    }

    public class Region
    {
        public List<string> countries { get; set; }
        public Currency currency { get; set; }
    }

    public class Data
    {
        public Region eu1 { get; set; }
        public Region eu2 { get; set; }
        public Region uk { get; set; }
        public Region us { get; set; }
        public Region br { get; set; }
        public Region au { get; set; }
    }
}
