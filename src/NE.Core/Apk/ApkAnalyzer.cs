using System.IO.Compression;
using System.Text;

namespace NEManager.Core.Apk;

/// <summary>
/// APK 完整分析器 —— 集成 AXML/DEX/ARSC 解析。
/// </summary>
public class ApkInfo
{
    public string PackageName { get; set; } = "";
    public string VersionName { get; set; } = "";
    public int VersionCode { get; set; }
    public string MinSdkVersion { get; set; } = "";
    public string TargetSdkVersion { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    public List<string> Activities { get; set; } = new();
    public List<string> Services { get; set; } = new();
    public List<string> Receivers { get; set; } = new();
    public List<string> Providers { get; set; } = new();
    public string IconPath { get; set; } = "";
    public string ManifestXml { get; set; } = "";
    public DexParser.DexInfo? DexInfo { get; set; }
    public ArscParser.ArscInfo? ArscInfo { get; set; }
    public List<ApkFileEntry> Files { get; set; } = new();
    public long TotalSize { get; set; }
    public int DexCount { get; set; }
    public bool IsSigned { get; set; }
    public bool IsZipAlign { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

public class ApkFileEntry
{
    public string FullName { get; set; } = "";
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public bool IsDirectory { get; set; }
    public string Extension { get; set; } = "";
}

public static class ApkAnalyzer
{
    public static ApkInfo Analyze(string apkPath)
    {
        var info = new ApkInfo { AnalyzedAt = DateTime.Now };

        if (!File.Exists(apkPath)) return info;

        using var archive = ZipFile.OpenRead(apkPath);
        info.TotalSize = archive.Entries.Sum(e => e.Length);

        // 收集所有文件
        foreach (var entry in archive.Entries)
        {
            info.Files.Add(new ApkFileEntry
            {
                FullName = entry.FullName,
                Size = entry.Length,
                CompressedSize = entry.CompressedLength,
                IsDirectory = entry.FullName.EndsWith("/"),
                Extension = Path.GetExtension(entry.FullName).ToLowerInvariant()
            });
        }

        // 签名检测
        info.IsSigned = archive.Entries.Any(e =>
            e.FullName.StartsWith("META-INF/") &&
            (e.FullName.EndsWith(".RSA") || e.FullName.EndsWith(".DSA") || e.FullName.EndsWith(".EC") ||
             e.FullName.EndsWith(".SF") || e.FullName.EndsWith(".MANIFEST.MF")));

        // 图标路径
        info.IconPath = archive.Entries
            .FirstOrDefault(e => e.FullName.StartsWith("res/mipmap") || e.FullName.StartsWith("res/drawable"))?.FullName ?? "";

        // DEX 计数
        info.DexCount = archive.Entries.Count(e => e.Name.EndsWith(".dex"));

        // 解析 AndroidManifest.xml
        var manifestEntry = archive.GetEntry("AndroidManifest.xml");
        if (manifestEntry != null)
        {
            using var stream = manifestEntry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var manifestData = ms.ToArray();

            // 解析 AXML
            info.ManifestXml = AXmlParser.Parse(manifestData);

            // 提取关键信息
            var quick = AXmlParser.QuickExtract(manifestData);
            info.PackageName = quick["Package"].FirstOrDefault() ?? "";
            info.VersionName = quick["Version"].FirstOrDefault() ?? "";
            info.Permissions = quick["Permissions"].Distinct().ToList();
            info.Activities = quick["Activities"].Distinct().ToList();
            info.Services = quick["Services"].Distinct().ToList();
            info.Receivers = quick["Receivers"].Distinct().ToList();
            info.Providers = quick["Providers"].Distinct().ToList();
        }

        // 解析 classes.dex
        var dexEntry = archive.GetEntry("classes.dex");
        if (dexEntry != null)
        {
            using var stream = dexEntry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            info.DexInfo = DexParser.Parse(ms.ToArray());
        }

        // 解析 resources.arsc
        var arscEntry = archive.GetEntry("resources.arsc");
        if (arscEntry != null)
        {
            using var stream = arscEntry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            info.ArscInfo = ArscParser.Parse(ms.ToArray());
        }

        return info;
    }

    public static List<string> ListFiles(string apkPath)
    {
        using var archive = ZipFile.OpenRead(apkPath);
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    public static byte[] ExtractFileBytes(string apkPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(apkPath);
        var entry = archive.GetEntry(entryPath);
        if (entry == null) return Array.Empty<byte>();

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static void ExtractFile(string apkPath, string entryPath, string outputPath)
    {
        var bytes = ExtractFileBytes(apkPath, entryPath);
        File.WriteAllBytes(outputPath, bytes);
    }

    public static void ExtractAll(string apkPath, string outputDir)
    {
        using var archive = ZipFile.OpenRead(apkPath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.Combine(outputDir, entry.FullName);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (destinationDir != null && !Directory.Exists(destinationDir))
                Directory.CreateDirectory(destinationDir);

            if (!string.IsNullOrEmpty(entry.Name))
                entry.ExtractToFile(destinationPath, true);
        }
    }

    /// <summary>
    /// 生成 Smali 代码骨架（基于 DEX 解析结果）。
    /// </summary>
    public static string GenerateSmaliSkeleton(DexParser.DexClass dexClass)
    {
        var sb = new StringBuilder();
        var className = DexParser.TypeDescriptorToName(dexClass.Name).Replace('.', '/');
        var superClass = DexParser.TypeDescriptorToName(dexClass.SuperClass).Replace('.', '/');

        sb.AppendLine(".class " + (string.IsNullOrEmpty(dexClass.AccessFlags) ? "" : dexClass.AccessFlags + " ") + "L" + className + ";");
        sb.AppendLine(".super L" + (string.IsNullOrEmpty(superClass) ? "java/lang/Object" : superClass) + ";");
        if (!string.IsNullOrEmpty(dexClass.SourceFile))
            sb.AppendLine(".source \"" + dexClass.SourceFile + "\"");
        sb.AppendLine();
        sb.AppendLine("# virtual methods");
        sb.AppendLine();
        sb.AppendLine(".method public <clinit>()V");
        sb.AppendLine("    sget-object v0, Ljava/lang/System;->out:Ljava/io/PrintStream;");
        sb.AppendLine("    const-string v1, \"Hello from NE Manager!\"");
        sb.AppendLine("    invoke-virtual {v0, v1}, Ljava/io/PrintStream;->println(Ljava/lang/String;)V");
        sb.AppendLine("    return-void");
        sb.AppendLine(".end method");
        sb.AppendLine();
        sb.AppendLine(".method public <init>()V");
        sb.AppendLine("    invoke-direct {p0}, L" + className + ";-><init>()V");
        sb.AppendLine("    return-void");
        sb.AppendLine(".end method");

        return sb.ToString();
    }
}
