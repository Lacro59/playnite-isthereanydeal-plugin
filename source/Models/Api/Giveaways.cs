using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models.Api
{
    public class Counts
    {
        [SerializationPropertyName("games")]
        public int Games { get; set; }

        [SerializationPropertyName("waitlist")]
        public int Waitlist { get; set; }

        [SerializationPropertyName("collection")]
        public int Collection { get; set; }
    }

    public class Datum
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("url")]
        public string Url { get; set; }

        [SerializationPropertyName("expiry")]
        public int? Expiry { get; set; }

        [SerializationPropertyName("publishAt")]
        public int PublishAt { get; set; }

        [SerializationPropertyName("isPending")]
        public bool IsPending { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("isMature")]
        public bool IsMature { get; set; }

        [SerializationPropertyName("shop")]
        public int? Shop { get; set; }

        [SerializationPropertyName("counts")]
        public Counts Counts { get; set; }

        [SerializationPropertyName("games")]
        public List<Game> Games { get; set; }
    }

    public class Giveaways
    {
        [SerializationPropertyName("_id")]
        public int Id { get; set; }

        [SerializationPropertyName("offset")]
        public int Offset { get; set; }

        [SerializationPropertyName("done")]
        public bool Done { get; set; }

        [SerializationPropertyName("data")]
        public List<Datum> Data { get; set; }
    }
}
