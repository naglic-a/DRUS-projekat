using System;
using System.Collections.Generic;
using System.Text;

namespace SharedUtils.Models {
    public class DrusAlarm {
        public int Id { get; set; }
        public int VarId { get; set; }
        public string AlarmType { get; set; } = string.Empty; // HIGH, LOW
        public string Severity { get; set; } = string.Empty;  // WARNING, CRITICAL
        public decimal TriggeredVal { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime AlarmTimestamp { get; set; }
    }
}
