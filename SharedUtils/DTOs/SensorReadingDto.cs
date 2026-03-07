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
    }
}
