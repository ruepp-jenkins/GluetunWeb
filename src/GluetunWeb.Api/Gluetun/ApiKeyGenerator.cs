using System.Numerics;
using System.Security.Cryptography;

namespace GluetunWeb.Api.Gluetun;

/// <summary>
/// Generates control-server API keys the same way <c>docker run --rm qmcgaw/gluetun genkey</c> does:
/// a 22-character Base58 value derived from 16 random bytes.
/// </summary>
public static class ApiKeyGenerator
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Base58Encode(bytes);
    }

    internal static string Base58Encode(byte[] data)
    {
        // Interpret bytes as a big-endian unsigned integer.
        var value = new BigInteger(data, isUnsigned: true, isBigEndian: true);
        var chars = new Stack<char>();
        var fifty8 = new BigInteger(58);
        while (value > 0)
        {
            value = BigInteger.DivRem(value, fifty8, out var rem);
            chars.Push(Alphabet[(int)rem]);
        }

        // Preserve leading zero bytes as leading '1' characters (Base58 convention).
        foreach (var b in data)
        {
            if (b == 0) chars.Push(Alphabet[0]);
            else break;
        }

        return new string(chars.ToArray());
    }
}
