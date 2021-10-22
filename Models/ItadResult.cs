using Playnite.SDK.Data;
using System;

namespace IsThereAnyDeal.Models
{
    public class ItadPlain
    {
        [SerializationPropertyName(".meta")]
        public ItadPlainMeta Meta { get; set; }
        [SerializationPropertyName("data")]
        //[JsonConverter(typeof(ItadPlainDataConverter))]
        public ItadPlainData Data { get; set; }
    }

    public class ItadPlainMeta
    {
        [SerializationPropertyName("match")]
        public string Match { get; set; }
        [SerializationPropertyName("active")]
        public bool Active { get; set; }
    }

    public class ItadPlainData
    {
        [SerializationPropertyName("plain")]
        public string Plain { get; set; }
    }


    //public class ItadPlainDataConverter : JsonConverter
    //{
    //    public override bool CanConvert(Type objectType)
    //    {
    //        return (objectType == typeof(ItadPlainData));
    //    }
    //
    //    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    //    {
    //        JToken token = JToken.Load(reader);
    //        if (token.Type == JTokenType.Object)
    //        {
    //            return token.ToObject<ItadPlainData>();
    //        }
    //        return null;
    //    }
    //
    //    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    //    {
    //        serializer.Serialize(writer, value);
    //    }
    //}
}
