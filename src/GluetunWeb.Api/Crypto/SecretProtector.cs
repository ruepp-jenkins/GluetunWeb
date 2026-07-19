using System.Security.Cryptography;
using System.Text;

namespace GluetunWeb.Api.Crypto;

/// <summary>
/// Encrypts/decrypts sensitive values (provider credentials, WireGuard keys, tokens) at rest
/// using AES-256-GCM. The 256-bit key is derived (SHA-256) from the GLUETUNWEB_MASTER_KEY
/// environment variable so any passphrase length is accepted.
///
/// Wire format (base64): [12-byte nonce][16-byte tag][ciphertext].
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts plaintext; returns null when input is null/empty.</summary>
    string? Encrypt(string? plaintext);

    /// <summary>Decrypts a value produced by <see cref="Encrypt"/>; returns null when input is null/empty.</summary>
    string? Decrypt(string? ciphertext);
}

public sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public SecretProtector(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES-256 key must be 32 bytes.", nameof(key));
        _key = key;
    }

    /// <summary>Builds a protector from a passphrase (env var). SHA-256 yields the 32-byte key.</summary>
    public static SecretProtector FromPassphrase(string passphrase)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(passphrase)));

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        var data = Convert.FromBase64String(ciphertext);
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext is too short.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[data.Length - NonceSize - TagSize];
        Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(data, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
