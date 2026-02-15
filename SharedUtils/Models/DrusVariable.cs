using System;
using System.Collections.Generic;
using System.Text;

namespace SharedUtils.Models {
    public class DrusVariable {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g., "TEMP_01"
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal MinSafeValue { get; set; }
        public decimal MaxSafeValue { get; set; }
    }
}
