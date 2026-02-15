using System;
using System.Collections.Generic;
using System.Text;

namespace SharedUtils.DTOs {
    public class EncryptedPacketDto {
        public byte[]? Payload { get; set; }   // The encrypted JSON bytes
        public byte[]? Hmac { get; set; }      // To verify nobody tampered with it
        public byte[]? Iv { get; set; }        // Initialization Vector
    }
}
