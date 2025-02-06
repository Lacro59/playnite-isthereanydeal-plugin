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

        [SerializationPropertyName("alpha3")]
        public string Alpha3 { get; set; }

        [SerializationPropertyName("m49")]
        public string M49 { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("currency")]
        public string Currency { get; set; }

        [SerializationPropertyName("rCurrency")]
        public string RCurrency { get; set; }
    }
}
