using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NEManager.Core.Security;

public static class SignatureService
{
    public record SigResult(
        bool HasSignature,
        bool IsValid,
        string Signer,
        string Issuer,
        DateTime NotBefore,
        DateTime NotAfter,
        string Thumbprint,
        string Algorithm,
        string Error = ""
    );

    /// <summary>验证 PE 文件 Authenticode 签名</summary>
    public static SigResult Verify(string path)
    {
        try
        {
            // 用 WinVerifyTrust 的简化方式：读取证书
            var certs = new X509Certificate2Collection();
            certs.Import(path);

            if (certs.Count == 0)
                return new SigResult(false, false, string.Empty, string.Empty, DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty);

            var cert = certs[0]; // 第一张是签名者
            var chain = new X509Chain();
            var chainOk = chain.Build(cert);

            return new SigResult(
                true, chainOk,
                cert.Subject,
                cert.Issuer,
                cert.NotBefore,
                cert.NotAfter,
                cert.Thumbprint,
                cert.SignatureAlgorithm.FriendlyName ?? string.Empty
            );
        }
        catch (Exception ex)
        {
            return new SigResult(false, false, string.Empty, string.Empty, DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty, ex.Message);
        }
    }

    /// <summary>简单哈希（SHA256/SHA1/MD5 自选）</summary>
    public static string ComputeHash(string path, string algo = "SHA256")
    {
        using var stream = File.OpenRead(path);
        return algo.ToUpper() switch
        {
            "MD5" => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(stream)).ToLower(),
            "SHA1" => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(stream)).ToLower(),
            "SHA256" => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLower(),
            "SHA384" => Convert.ToHexString(System.Security.Cryptography.SHA384.HashData(stream)).ToLower(),
            "SHA512" => Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(stream)).ToLower(),
            _ => throw new NotSupportedException(algo)
        };
    }
}
