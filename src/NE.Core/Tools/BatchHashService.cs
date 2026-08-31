using System.Security.Cryptography;

namespace NEManager.Core.Tools;

public static class BatchHashService
{
    public class HashResult
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Algorithm { get; set; } = "";
        public long FileSize { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; } = "";
    }

    // 批量计算文件哈希
    public static List<HashResult> ComputeBatchHash(
        IEnumerable<string> filePaths,
        string algorithm = "SHA256",
        IProgress<double>? progress = null)
    {
        var results = new List<HashResult>();
        var files = filePaths.ToList();
        int processed = 0;
        
        foreach (var path in files)
        {
            var result = new HashResult
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Algorithm = algorithm,
                FileSize = new FileInfo(path).Length
            };
            
            try
            {
                using var stream = File.OpenRead(path);
                using var hasher = CreateHashAlgorithm(algorithm);
                
                var hashBytes = hasher.ComputeHash(stream);
                result.Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
            }
            
            results.Add(result);
            processed++;
            progress?.Report((double)processed / files.Count);
        }
        
        return results;
    }

    // 验证文件哈希
    public static bool VerifyFileHash(string filePath, string expectedHash, string algorithm = "SHA256")
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var hasher = CreateHashAlgorithm(algorithm);

            var hashBytes = hasher.ComputeHash(stream);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // 哈希算法工厂
    private static HashAlgorithm CreateHashAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => SHA256.Create()
        };
    }

    // 导出哈希结果到文件
    public static void ExportHashResults(List<HashResult> results, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        
        foreach (var result in results)
        {
            if (result.Success)
            {
                writer.WriteLine($"{result.Hash}  {result.FileName}");
            }
            else
            {
                writer.WriteLine($"# ERROR: {result.FileName} - {result.Error}");
            }
        }
    }

    // 从哈希文件验证
    public static List<(string fileName, bool match, string error)> VerifyFromHashFile(
        string hashFilePath,
        string baseDirectory)
    {
        var results = new List<(string, bool, string)>();
        
        try
        {
            using var reader = new StreamReader(hashFilePath);
            string? line;
            
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;
                
                // 格式: hash  filename (两个空格分隔)
                var parts = line.Split("  ", 2);
                if (parts.Length != 2)
                    continue;
                
                var expectedHash = parts[0];
                var fileName = parts[1];
                var filePath = Path.Combine(baseDirectory, fileName);
                
                if (!File.Exists(filePath))
                {
                    results.Add((fileName, false, "文件不存在"));
                    continue;
                }
                
                // 自动检测算法
                var algorithm = expectedHash.Length switch
                {
                    32 => "MD5",
                    40 => "SHA1",
                    64 => "SHA256",
                    96 => "SHA384",
                    128 => "SHA512",
                    _ => "SHA256"
                };
                
                var match = VerifyFileHash(filePath, expectedHash, algorithm);
                results.Add((fileName, match, match ? "" : "哈希不匹配"));
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"读取哈希文件失败: {ex.Message}");
        }
        
        return results;
    }
}
