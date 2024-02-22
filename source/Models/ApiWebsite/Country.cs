using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models.ApiWebsite
{
    public class Country
    {
        [SerializationPropertyName("alpha2")]
        public string Alpha2 { get; set; }
        [SerializationPropertyName("name")]
        public string Name { get; set; }
        [SerializationPropertyName("currency")]
        public string Currency { get; set; }
    }
}
