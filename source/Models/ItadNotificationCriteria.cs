using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IsThereAnyDeal.Models
{
    public class ItadNotificationCriteria
    {
        public int PriceCut { get; set; } = -1;
        public int PriceInferior { get; set; } = -1;

        [DontSerialize]
        public string Criteria
        {
            get
            {
                string CriteriaString = string.Empty;

                if (PriceCut > -1)
                {
                    CriteriaString = ResourceProvider.GetString("LOCItadLimitNotificationAt") + " " + PriceCut + " %";
                }

                if (PriceInferior > -1)
                {
                    if (CriteriaString.IsNullOrEmpty())
                    {
                        CriteriaString = ResourceProvider.GetString("LOCItadLimitNotificationPriceAt") + " " + PriceInferior;
                    }
                    else
                    {
                        CriteriaString += " & " + ResourceProvider.GetString("LOCItadLimitNotificationPriceAt") + " " + PriceInferior;
                    }
                }

                return CriteriaString;
            }
        }
    }
}
