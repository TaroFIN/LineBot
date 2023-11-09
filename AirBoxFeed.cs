using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace LineBot
{
    public class AirBoxItem
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }
        public string SiteName { get; set; }

        public bool jsonIsBroken { get; set; }

        public AirBoxFeed airbox;

    }

    public class AirBoxFeed
    { 
        public string device_id { get; set; }
        public string source { get; set; }
        public List<Feed> feeds { get; set; }
    }

    public class Feed
    {
        public AirBox AirBox { get; set; }
    }

    public class AirBox
    {
        public DateTime timestamp { get; set; }
        public string siteName { get; set; }
        public string area { get; set; }
        public string device_ID { get; set; }
        public string name { get; set; }
        public double s_d1 { get; set; }
        public double s_h0 { get; set; }
        public double s_t0 { get; set; }

    }
}