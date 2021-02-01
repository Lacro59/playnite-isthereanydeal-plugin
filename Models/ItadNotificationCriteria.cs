using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models
{
    public class ItadNotificationCriteria
    {
        private static IResourceProvider resources = new ResourceProvider();


        public int PriceCut { get; set; } = -1;
        public int PriceInferior { get; set; } = -1;

        [JsonIgnore]
        public string Criteria {
            get
            {
                string CriteriaString = string.Empty;

                if (PriceCut > -1)
                {
                    CriteriaString = resources.GetString("LOCItadLimitNotificationAt") + " " + PriceCut + " %";
                }

                if (PriceInferior > -1)
                {
                    if (CriteriaString.IsNullOrEmpty())
                    {
                        CriteriaString = resources.GetString("LOCItadLimitNotificationPriceAt") + " " + PriceInferior;
                    }
                    else
                    {
                        CriteriaString += " & " + resources.GetString("LOCItadLimitNotificationPriceAt") + " " + PriceInferior;
                    }
                }

                return CriteriaString;
            }
        }
    }
}
