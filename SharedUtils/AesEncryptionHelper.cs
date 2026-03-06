using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace SharedUtils;

public static class AesEncryptionHelper {
    // Best practice is to pass secret t hrough SHA-256, to ensure  it is 256 bits long(requeired for AES-256)
    // AES = Advanced Encryption Standard
    private static byte[] HashSecret(byte[] dhSharedSecred) {
        return SHA256.HashData(dhSharedSecred);
    }

    public static string Encrypt(string plainText, byte[] dhSharedSecret) {
        byte[] key = HashSecret(dhSharedSecret);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // Generates random 16-byte init vector

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string encrytpedText, byte[] dhSharedSecret) {
        byte[] key = HashSecret(dhSharedSecret);
        byte[] encryptedMessage = Convert.FromBase64String(encrytpedText);

        byte[] iv = new byte[16];
        Buffer.BlockCopy(encryptedMessage, 0, iv, 0, iv.Length);

        byte[] encryptedBytes = new byte[encryptedMessage.Length - iv.Length];
        Buffer.BlockCopy(encryptedMessage, iv.Length, encryptedBytes, 0, encryptedBytes.Length);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}