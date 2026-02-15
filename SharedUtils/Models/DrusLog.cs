using System;
using System.Collections.Generic;
using System.Text;

namespace SharedUtils.Models {
    public class DrusLog {
        public long Id { get; set; }
        public int VarId { get; set; }
        public decimal Val { get; set; }
        public DateTime LogTimestamp { get; set; }
    }
}
