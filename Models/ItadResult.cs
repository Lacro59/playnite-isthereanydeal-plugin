using Newtonsoft.Json;

namespace IsThereAnyDeal.Models
{
    public class ItadPlain
    {
        [JsonProperty(".meta")]
        public ItadPlainMeta Meta { get; set; }
        [JsonProperty("data")]
        public ItadPlainData Data { get; set; }
    }

    public class ItadPlainMeta
    {
        [JsonProperty("match")]
        public string Match { get; set; }
        [JsonProperty("active")]
        public bool Active { get; set; }
    }

    public class ItadPlainData
    {
        [JsonProperty("plain")]
        public string Plain { get; set; }
    }
}
