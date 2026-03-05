using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace SharedUtils;

public class DhKeyExchange : IDisposable {

    private readonly ECDiffieHellman _ecdh;
    public byte[] PublicKey { get; private set; }

    public DhKeyExchange() {
        _ecdh = ECDiffieHellman.Create(); // Creates new instance
        PublicKey = _ecdh.PublicKey.ExportSubjectPublicKeyInfo(); // exports public key as standard byte array
    }

    public byte[] DeriveSharedSecret(byte[] otherPartyPublicKey) {
        using var otherPartyEcdh = ECDiffieHellman.Create();

        otherPartyEcdh.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);

        return _ecdh.DeriveKeyMaterial(otherPartyEcdh.PublicKey);
    }

    public void Dispose() {
        _ecdh.Dispose();
    }
}