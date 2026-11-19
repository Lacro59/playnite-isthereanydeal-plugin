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

        [SerializationPropertyName("assets")]
        public Assets Assets { get; set; }

        [SerializationPropertyName("drmfree")]
        public bool DrmFree { get; set; }
    }

    /// <summary>
    /// Image assets returned by the ITAD game / giveaways API.
    /// </summary>
    public class Assets
    {
        [SerializationPropertyName("boxart")]
        public string Boxart { get; set; }

        [SerializationPropertyName("banner145")]
        public string Banner145 { get; set; }

        [SerializationPropertyName("banner300")]
        public string Banner300 { get; set; }

        [SerializationPropertyName("banner400")]
        public string Banner400 { get; set; }

        [SerializationPropertyName("banner600")]
        public string Banner600 { get; set; }
    }
}
