using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SharedUtils.DTOs {
    public class SensorReadingDto {
        [JsonPropertyName("sid")]
        public string SensorId { get; set; } = string.Empty;

        [JsonPropertyName("val")]
        public decimal Value { get; set; }

        [JsonPropertyName("ts")]
        public long Timestamp { get; set; } 

        public bool IsHeartbeat { get; set; } = false;

        [JsonPropertyName("mid")]
        public long MessageId { get; set; }

        [JsonPropertyName("q")]
        public string Quality { get; set; } = "GOOD"; // GOOD, UNCERTAIN, BAD

        [JsonPropertyName("sig")]
        public string Signature { get; set; } = string.Empty;
    }
}
