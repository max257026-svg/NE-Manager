using System.Security.Cryptography;
using System.Text;

namespace NEManager.Core.Tools;

/// <summary>
/// 哈希计算服务。
/// </summary>
public static class HashService
{
    private static readonly string[] _supportedAlgorithms = { "MD5", "SHA1", "SHA256", "SHA512" };

    public static string[] SupportedAlgorithms => _supportedAlgorithms;

    public static string ComputeFileHash(string path, string algorithm)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ComputeHash(stream, algorithm);
    }

    public static string ComputeTextHash(string text, string algorithm)
    {
        var data = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream(data);
        return ComputeHash(ms, algorithm);
    }

    private static string ComputeHash(Stream stream, string algorithm)
    {
        using var hasher = CreateHashAlgorithm(algorithm);
        var hash = hasher.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HashAlgorithm CreateHashAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA512" => SHA512.Create(),
            _ => throw new NotSupportedException($"不支持的哈希算法: {algorithm}")
        };
    }
}
