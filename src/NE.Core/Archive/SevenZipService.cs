using System.Diagnostics;

namespace NEManager.Core.Archive;

public static class SevenZipService
{
    private static readonly string SevenZipPath = @"C:\Program Files\7-Zip\7z.exe";

    public static bool IsAvailable()
    {
        return File.Exists(SevenZipPath);
    }

    public static List<string> ListEntries(string archivePath)
    {
        if (!IsAvailable())
            throw new Exception("7-Zip 未安装或路径不正确");
        
        var psi = new ProcessStartInfo
        {
            FileName = SevenZipPath,
            Arguments = $"l \"{archivePath}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null) return new List<string>();
        
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        
        var entries = new List<string>();
        var lines = output.Split('\n');
        bool inList = false;
        
        foreach (var line in lines)
        {
            if (line.Contains("----------") && !inList)
            {
                inList = true;
                continue;
            }
            if (inList && line.Contains("----------"))
                break;
            
            if (inList && line.Length > 53)
            {
                var entry = line.Substring(53).Trim();
                if (!string.IsNullOrEmpty(entry))
                    entries.Add(entry);
            }
        }
        
        return entries;
    }

    public static void Extract(string archivePath, string outputDir)
    {
        if (!IsAvailable())
            throw new Exception("7-Zip 未安装或路径不正确");
        
        var psi = new ProcessStartInfo
        {
            FileName = SevenZipPath,
            Arguments = $"x \"{archivePath}\" -o\"{outputDir}\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("无法启动 7-Zip 进程");
        
        process.WaitForExit();
        
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new Exception($"解压失败: {error}");
        }
    }

    public static void Create(string archivePath, string sourceDir, string format = "7z")
    {
        if (!IsAvailable())
            throw new Exception("7-Zip 未安装或路径不正确");
        
        var psi = new ProcessStartInfo
        {
            FileName = SevenZipPath,
            Arguments = $"a -t{format} \"{archivePath}\" \"{sourceDir}\\*\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("无法启动 7-Zip 进程");
        
        process.WaitForExit();
        
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new Exception($"创建压缩文件失败: {error}");
        }
    }
}
