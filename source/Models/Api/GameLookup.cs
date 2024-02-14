using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Models.Api
{
    public class GameLookup
    {
        [SerializationPropertyName("found")]
        public bool Found { get; set; }

        [SerializationPropertyName("game")]
        public Game Game { get; set; }
    }

    public class Game
    {
        [SerializationPropertyName("id")]
        public string Id { get; set; }

        [SerializationPropertyName("slug")]
        public string Slug { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("type")]
        public string Type { get; set; }

        [SerializationPropertyName("mature")]
        public bool Mature { get; set; }
    }
}
