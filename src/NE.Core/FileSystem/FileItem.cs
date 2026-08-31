using System.IO;

namespace NEManager.Core.FileSystem;

/// <summary>
/// 文件列表中的一项。
/// </summary>
public sealed class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime LastWriteTime { get; set; }
    public DateTime LastAccessTime { get; set; }
    public FileAttributes Attributes { get; set; }

    // ---- 派生标志 ----
    public bool IsHidden { get; set; }
    public bool IsSystem { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsReparsePoint { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsSparse { get; set; }
    public bool IsOffline { get; set; }

    /// <summary>是否含有 NTFS 备用数据流。</summary>
    public bool HasAlternateStreams { get; set; }
    public int StreamCount { get; set; }

    /// <summary>Zone.Identifier（从网络下载的文件标记）。</summary>
    public bool IsZoneIdentifier => HasAlternateStreams;

    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name);

    public string SizeText => IsDirectory ? "<目录>" : FormatSize(Size);

    /// <summary>类型描述。</summary>
    public string TypeText
    {
        get
        {
            if (IsDirectory)
            {
                if (IsReparsePoint) return "文件夹链接";
                return IsSystem ? "系统文件夹" : "文件夹";
            }
            if (IsReparsePoint) return "符号链接";
            return string.IsNullOrEmpty(Extension)
                ? "文件"
                : Extension.TrimStart('.').ToUpperInvariant() + " 文件";
        }
    }

    public string AttributeText
    {
        get
        {
            var codes = new List<string>();
            if (IsReadOnly) codes.Add("R");
            if (IsHidden) codes.Add("H");
            if (IsSystem) codes.Add("S");
            if (IsDirectory) codes.Add("D");
            if (IsReparsePoint) codes.Add("L");
            if (IsCompressed) codes.Add("C");
            if (IsEncrypted) codes.Add("E");
            if (HasAlternateStreams) codes.Add($"ADS×{StreamCount}");
            return string.Join("", codes);
        }
    }

    public bool IsDangerous =>
        Name.Equals("bootmgr", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("BCD", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("ntoskrnl.exe", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("winload.efi", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("ntdll.dll", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase) ||
        Name.Equals("boot.ini", StringComparison.OrdinalIgnoreCase);

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return string.Empty;
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{size:0.##} {units[unit]}";
    }
}
