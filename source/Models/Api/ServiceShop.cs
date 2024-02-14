using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models.Api
{
    public class ServiceShop
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("deals")]
        public int Deals { get; set; }

        [SerializationPropertyName("games")]
        public int Games { get; set; }

        [SerializationPropertyName("update")]
        public DateTime Update { get; set; }
    }
}
