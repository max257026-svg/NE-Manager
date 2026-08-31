using System.IO;
using System.Runtime.InteropServices;
using NEManager.Core.Security;
using NEManager.Native;

namespace NEManager.Core.FileSystem;

/// <summary>
/// 文件系统浏览服务 —— 强制显示受保护系统文件、隐藏项、$Recycle.Bin 等。
/// </summary>
public static class FileSystemService
{
    /// <summary>
    /// Windows 中默认隐藏、但 NE 管理器必须能看到的受保护目录。
    /// </summary>
    public static readonly string[] ProtectedDirectories =
    {
        "$Recycle.Bin", "System Volume Information", "Recovery", "$WINDOWS.~BT",
        "$Windows.~WS", "Windows.old", "ProgramData", "Documents and Settings",
        "MSOCache", "PerfLogs", "Program Files", "Program Files (x86)",
        "System32", "SysWOW64", "WinSxS", "Boot", "EFI"
    };

    public sealed class BrowseOptions
    {
        public bool ShowHidden { get; set; } = true;
        public bool ShowSystem { get; set; } = true;
        public bool ShowProtected { get; set; } = true;
        public bool DetectAlternateStreams { get; set; } = true;
        public bool FollowSymlinks { get; set; } = true;
    }

    /// <summary>
    /// 枚举目录内容。受权限拒绝的项目会被忽略（不抛异常）。
    /// </summary>
    public static List<FileItem> Enumerate(string path, BrowseOptions? options = null)
    {
        options ??= new BrowseOptions();
        var items = new List<FileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists) return items;

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,          // 关键：不跳过任何属性，隐藏/系统文件全部列出
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            foreach (var info in dirInfo.EnumerateFileSystemInfos("*", enumOptions))
            {
                try
                {
                    items.Add(FromInfo(info, options));
                }
                catch
                {
                    // 单项读取失败不影响整体
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            /* 拒绝访问：交由上层提示接管权限 */
        }
        catch (Exception)
        {
            /* 忽略其它异常 */
        }

        return items
            .OrderByDescending(i => i.IsDirectory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static FileItem? FromPath(string path, BrowseOptions? options = null)
    {
        options ??= new BrowseOptions();
        try
        {
            if (Directory.Exists(path))
                return FromInfo(new DirectoryInfo(path), options);
            if (File.Exists(path))
                return FromInfo(new FileInfo(path), options);
        }
        catch { }
        return null;
    }

    private static FileItem FromInfo(FileSystemInfo info, BrowseOptions options)
    {
        var item = new FileItem
        {
            Name = info.Name.Length == 0 ? info.FullName : info.Name,
            FullPath = info.FullName,
            IsDirectory = (info.Attributes & FileAttributes.Directory) != 0,
            Attributes = info.Attributes,
            CreationTime = info.CreationTime,
            LastWriteTime = info.LastWriteTime,
            LastAccessTime = info.LastAccessTime
        };

        item.IsHidden = (info.Attributes & FileAttributes.Hidden) != 0;
        item.IsSystem = (info.Attributes & FileAttributes.System) != 0;
        item.IsReadOnly = (info.Attributes & FileAttributes.ReadOnly) != 0;
        item.IsReparsePoint = (info.Attributes & FileAttributes.ReparsePoint) != 0;
        item.IsCompressed = (info.Attributes & FileAttributes.Compressed) != 0;
        item.IsEncrypted = (info.Attributes & FileAttributes.Encrypted) != 0;
        item.IsSparse = (info.Attributes & FileAttributes.SparseFile) != 0;
        item.IsOffline = (info.Attributes & FileAttributes.Offline) != 0;

        if (!item.IsDirectory)
        {
            try { item.Size = ((FileInfo)info).Length; } catch { }
        }

        // 检测备用数据流（NTFS）
        if (options.DetectAlternateStreams)
        {
            try
            {
                var streams = AlternateDataStreamService.Enumerate(item.FullPath);
                // "::$DATA" 是主数据流，不计入
                var extra = streams.Where(s => !s.Name.Equals("::$DATA", StringComparison.Ordinal)).ToList();
                if (extra.Count > 0)
                {
                    item.HasAlternateStreams = true;
                    item.StreamCount = extra.Count;
                }
            }
            catch { }
        }

        return item;
    }

    /// <summary>
    /// 判断路径是否指向受保护的敏感目录。
    /// </summary>
    public static bool IsProtectedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => ProtectedDirectories.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断是否为 Windows 系统目录（改动前必须警告）。
    /// </summary>
    public static bool IsSystemPath(string path)
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(systemRoot)) return false;

        return path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrEmpty(programFiles) && path.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase));
    }

    // ==================== 驱动器 ====================

    public sealed class DriveItem
    {
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public DriveType Type { get; set; }
        public string VolumeLabel { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public string SerialNumber { get; set; } = string.Empty;

        public string TypeText => Type switch
        {
            DriveType.Fixed => "本地磁盘",
            DriveType.Removable => "可移动磁盘",
            DriveType.Network => "网络驱动器",
            DriveType.CDRom => "光盘",
            DriveType.Ram => "RAM 磁盘",
            _ => "未知"
        };

        public string UsageText
        {
            get
            {
                if (TotalSize <= 0) return string.Empty;
                double used = (TotalSize - FreeSpace) / (double)TotalSize * 100;
                return $"{used:0.#}% 已用";
            }
        }

        public string SizeText => FileItem.FormatSize(TotalSize);
        public string FreeText => FileItem.FormatSize(FreeSpace);
    }

    public static List<DriveItem> EnumerateDrives()
    {
        var list = new List<DriveItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            var item = new DriveItem
            {
                Name = drive.Name,
                RootPath = drive.RootDirectory.FullName,
                Type = drive.DriveType
            };
            try
            {
                item.VolumeLabel = drive.VolumeLabel;
                item.FileSystem = drive.DriveFormat;
                if (drive.IsReady)
                {
                    item.TotalSize = drive.TotalSize;
                    item.FreeSpace = drive.AvailableFreeSpace;
                }
            }
            catch { /* 光驱无盘等情况 */ }

            // 通过原生 API 补齐卷序列号
            try
            {
                var sb = new System.Text.StringBuilder(261);
                var fs = new System.Text.StringBuilder(261);
                if (Kernel32.GetVolumeInformation(
                        drive.Name.TrimEnd('\\') + "\\", sb, 261,
                        out uint serial, out _, out _, fs, 261))
                {
                    item.SerialNumber = serial.ToString("X8");
                    if (string.IsNullOrEmpty(item.FileSystem)) item.FileSystem = fs.ToString();
                }
            }
            catch { }

            list.Add(item);
        }
        return list;
    }

    // ==================== 占用与替换 ====================

    /// <summary>
    /// 注册"下次启动时替换文件"（MoveFileEx 重启替换机制）。
    /// 用于替换被进程占用、当前无法覆盖的系统文件。
    /// </summary>
    public static string? ScheduleReplaceOnReboot(string sourceFile, string targetFile)
    {
        bool ok = Kernel32.MoveFileEx(
            sourceFile, targetFile,
            WinConst.MOVEFILE_REPLACE_EXISTING | WinConst.MOVEFILE_DELAY_UNTIL_REBOOT);

        return ok ? null : new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
    }

    /// <summary>
    /// 检测文件是否被其它进程占用。
    /// </summary>
    public static bool IsFileLocked(string path)
    {
        var handle = Kernel32.CreateFile(
            path,
            WinConst.GENERIC_READ | WinConst.GENERIC_WRITE,
            0,                                  // 不共享 = 若被占用则失败
            IntPtr.Zero,
            WinConst.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return true;

        Kernel32.CloseHandle(handle);
        return false;
    }

    /// <summary>
    /// 绕过访问拒绝的读取工作流：失败 → 尝试接管所有者 → 读取 → （可选）还原。
    /// </summary>
    public static (bool success, string message, byte[] data) ReadProtectedFile(string path, bool restoreAfter = false)
    {
        try
        {
            if (File.Exists(path))
                return (true, string.Empty, File.ReadAllBytes(path));
        }
        catch (UnauthorizedAccessException)
        {
            /* 继续尝试提权 */
        }
        catch (Exception ex)
        {
            return (false, ex.Message, Array.Empty<byte>());
        }

        // 保存原权限用于回滚
        var originalSddl = SecurityDescriptorService.ReadFileSddl(path);

        var takeError = TrustedInstallerService.TakeOwnership(path);
        if (takeError != null)
            return (false, $"接管权限失败：{takeError}", Array.Empty<byte>());

        try
        {
            var data = File.ReadAllBytes(path);

            if (restoreAfter && !string.IsNullOrEmpty(originalSddl))
                SecurityDescriptorService.SetFileSddl(path, originalSddl);

            return (true, string.Empty, data);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, Array.Empty<byte>());
        }
    }

    /// <summary>
    /// 计算文件哈希（用于校验系统文件是否被篡改）。
    /// </summary>
    public static string ComputeHash(string path, string algorithm = "SHA256")
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var hasher = algorithm.ToUpperInvariant() switch
            {
                "MD5" => (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.MD5.Create(),
                "SHA1" => System.Security.Cryptography.SHA1.Create(),
                "SHA384" => System.Security.Cryptography.SHA384.Create(),
                "SHA512" => System.Security.Cryptography.SHA512.Create(),
                _ => System.Security.Cryptography.SHA256.Create()
            };
            var hash = hasher.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
