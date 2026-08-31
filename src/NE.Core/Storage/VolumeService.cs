using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NEManager.Core.FileSystem;
using NEManager.Native;

namespace NEManager.Core.Storage;

/// <summary>
/// 卷、物理磁盘、虚拟磁盘底层服务。
/// </summary>
public static class VolumeService
{
    // ==================== 卷信息 ====================

    public sealed class VolumeItem
    {
        public string GuidPath { get; set; } = string.Empty;   // \\?\Volume{...}
        public string MountPoints { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public uint MaxComponentLength { get; set; }
        public uint FileSystemFlags { get; set; }
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }

        public string TotalText => FileItem.FormatSize(TotalSize);
        public string FreeText => FileItem.FormatSize(FreeSpace);

        public string FlagsText
        {
            get
            {
                var flags = new List<string>();
                const uint FILE_CASE_SENSITIVE_SEARCH = 0x00000001;
                const uint FILE_UNICODE_ON_DISK = 0x00000004;
                const uint FILE_PERSISTENT_ACLS = 0x00000008;
                const uint FILE_FILE_COMPRESSION = 0x00000010;
#pragma warning disable CS0219 // 这些常量保留用于将来扩展开关显示
                const uint FILE_VOLUME_QUOTAS = 0x00000020;
                const uint FILE_SUPPORTS_SPARSE_FILES = 0x00000040;
                const uint FILE_SUPPORTS_REPARSE_POINTS = 0x00000080;
                const uint FILE_VOLUME_IS_COMPRESSED = 0x00008000;
                const uint FILE_SUPPORTS_OBJECT_IDS = 0x00010000;
#pragma warning restore CS0219
                const uint FILE_SUPPORTS_ENCRYPTION = 0x00020000;
                const uint FILE_NAMED_STREAMS = 0x00040000;
                const uint FILE_READ_ONLY_VOLUME = 0x00080000;

                if ((FileSystemFlags & FILE_CASE_SENSITIVE_SEARCH) != 0) flags.Add("区分大小写");
                if ((FileSystemFlags & FILE_UNICODE_ON_DISK) != 0) flags.Add("Unicode");
                if ((FileSystemFlags & FILE_PERSISTENT_ACLS) != 0) flags.Add("ACL");
                if ((FileSystemFlags & FILE_FILE_COMPRESSION) != 0) flags.Add("压缩");
                if ((FileSystemFlags & FILE_SUPPORTS_SPARSE_FILES) != 0) flags.Add("稀疏文件");
                if ((FileSystemFlags & FILE_SUPPORTS_REPARSE_POINTS) != 0) flags.Add("重解析点");
                if ((FileSystemFlags & FILE_SUPPORTS_ENCRYPTION) != 0) flags.Add("EFS 加密");
                if ((FileSystemFlags & FILE_NAMED_STREAMS) != 0) flags.Add("备用数据流");
                if ((FileSystemFlags & FILE_READ_ONLY_VOLUME) != 0) flags.Add("只读");

                return string.Join(", ", flags);
            }
        }
    }

    public static List<VolumeItem> EnumerateVolumes()
    {
        var list = new List<VolumeItem>();
        var buffer = new StringBuilder(1024);

        var handle = Kernel32.FindFirstVolume(buffer, (uint)buffer.Capacity);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return list;

        try
        {
            do
            {
                var guidPath = buffer.ToString();
                var item = new VolumeItem { GuidPath = guidPath };

                // 挂载点（盘符）
                uint returnLength;
                Kernel32.GetVolumePathNamesForVolumeName(guidPath, buffer, (uint)buffer.Capacity, out returnLength);
                item.MountPoints = buffer.ToString().TrimEnd('\0').Replace("\0", " ");

                // 卷信息
                var label = new StringBuilder(261);
                var fs = new StringBuilder(261);
                if (Kernel32.GetVolumeInformation(guidPath, label, 261,
                        out uint serial, out uint maxComp, out uint flags, fs, 261))
                {
                    item.Label = label.ToString();
                    item.FileSystem = fs.ToString();
                    item.SerialNumber = serial.ToString("X8");
                    item.MaxComponentLength = maxComp;
                    item.FileSystemFlags = flags;
                }

                // 容量
                if (Kernel32.GetDiskFreeSpaceEx(guidPath, out ulong free, out ulong total, out _))
                {
                    item.TotalSize = (long)total;
                    item.FreeSpace = (long)free;
                }

                list.Add(item);
            }
            while (Kernel32.FindNextVolume(handle, buffer, (uint)buffer.Capacity));
        }
        finally
        {
            Kernel32.FindVolumeClose(handle);
        }

        return list;
    }

    // ==================== 物理磁盘 ====================

    public sealed class PhysicalDriveItem
    {
        public string DevicePath { get; set; } = string.Empty;
        public int Index { get; set; }
        public string Model { get; set; } = string.Empty;
        public long Size { get; set; }
        public uint BytesPerSector { get; set; }
        public string InterfaceType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int PartitionCount { get; set; }

        public string SizeText => FileItem.FormatSize(Size);
    }

    public static List<PhysicalDriveItem> EnumeratePhysicalDrives()
    {
        var list = new List<PhysicalDriveItem>();
        try
        {
            foreach (var mo in SystemTools.WmiService.Query(
                         "SELECT DeviceID, Model, Size, BytesPerSector, InterfaceType, SerialNumber, Partitions FROM Win32_DiskDrive"))
            {
                var deviceId = mo["DeviceID"]?.ToString() ?? string.Empty;
                list.Add(new PhysicalDriveItem
                {
                    DevicePath = deviceId,
                    Index = ExtractIndex(deviceId),
                    Model = mo["Model"]?.ToString() ?? string.Empty,
                    Size = Convert.ToInt64(mo["Size"] ?? 0L),
                    BytesPerSector = Convert.ToUInt32(mo["BytesPerSector"] ?? 512u),
                    InterfaceType = mo["InterfaceType"]?.ToString() ?? string.Empty,
                    SerialNumber = mo["SerialNumber"]?.ToString()?.Trim() ?? string.Empty,
                    PartitionCount = Convert.ToInt32(mo["Partitions"] ?? 0)
                });
            }
        }
        catch { }
        return list;
    }

    private static int ExtractIndex(string deviceId)
    {
        var digits = new string(deviceId.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int idx) ? idx : -1;
    }

    // ==================== 原始扇区访问（只读） ====================

    /// <summary>
    /// 以扇区为单位读取原始卷/物理磁盘。⚠️ 只读实现，写盘功能不开放。
    /// </summary>
    public static (bool success, string error, byte[] data) ReadSectors(string devicePath, long offset, int length)
    {
        var handle = Kernel32.CreateFile(
            devicePath,
            WinConst.GENERIC_READ,
            WinConst.FILE_SHARE_READ | WinConst.FILE_SHARE_WRITE,
            IntPtr.Zero,
            WinConst.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return (false, new Win32Exception(Marshal.GetLastWin32Error()).Message, Array.Empty<byte>());

        try
        {
            if (!Kernel32.SetFilePointerEx(handle, offset, out _, 0))
                return (false, new Win32Exception(Marshal.GetLastWin32Error()).Message, Array.Empty<byte>());

            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!Kernel32.ReadFile(handle, buffer, (uint)length, out uint read, IntPtr.Zero))
                    return (false, new Win32Exception(Marshal.GetLastWin32Error()).Message, Array.Empty<byte>());

                var data = new byte[read];
                Marshal.Copy(buffer, data, 0, (int)read);
                return (true, string.Empty, data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    // ==================== VHD / VHDX / ISO ====================

    public sealed class MountResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string MountedPath { get; init; } = string.Empty;
        public IntPtr Handle { get; init; }
    }

    /// <summary>
    /// 挂载 VHD/VHDX 虚拟磁盘。
    /// </summary>
    public static MountResult MountVirtualDisk(string vhdPath, bool readOnly = true)
    {
        if (string.IsNullOrEmpty(vhdPath))
            return new MountResult { Success = false, Message = "虚拟磁盘路径为空。" };

        var storageType = new VIRTUAL_STORAGE_TYPE
        {
            DeviceId = vhdPath.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) ? 3u : 2u,
            VendorId = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B") // Microsoft 虚拟磁盘
        };

        uint ret = VirtDisk.OpenVirtualDisk(
            ref storageType, vhdPath,
            readOnly ? WinConst.VIRTUAL_DISK_ACCESS_ATTACH_RO : WinConst.VIRTUAL_DISK_ACCESS_ATTACH_RW,
            WinConst.OPEN_VIRTUAL_DISK_FLAG_NONE,
            IntPtr.Zero,
            out var handle);

        if (ret != 0)
            return new MountResult { Success = false, Message = $"打开虚拟磁盘失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}" };

        try
        {
            uint attachFlags = readOnly
                ? WinConst.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY
                : WinConst.ATTACH_VIRTUAL_DISK_FLAG_NONE;

            ret = VirtDisk.AttachVirtualDisk(handle, IntPtr.Zero, attachFlags, 0, IntPtr.Zero, IntPtr.Zero);
            if (ret != 0 && ret != 87) // 87 = 参数错误，某些版本对 NULL 参数敏感
                return new MountResult
                {
                    Success = false,
                    Message = $"附加虚拟磁盘失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}"
                };

            // 查询挂载后的物理路径
            uint pathSize = 1024;
            var path = new StringBuilder((int)pathSize);
            VirtDisk.GetVirtualDiskPhysicalPath(handle, ref pathSize, path);

            return new MountResult
            {
                Success = true,
                Message = "已挂载。可通过磁盘管理或刷新驱动器列表看到新卷。",
                MountedPath = path.ToString(),
                Handle = handle
            };
        }
        catch (Exception ex)
        {
            Kernel32.CloseHandle(handle);
            return new MountResult { Success = false, Message = ex.Message };
        }
    }

    public static string? UnmountVirtualDisk(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return "无效的虚拟磁盘句柄。";
        try
        {
            uint ret = VirtDisk.DetachVirtualDisk(handle, WinConst.DETACH_VIRTUAL_DISK_FLAG_NONE, 0);
            return ret == 0 ? null : $"卸载失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}";
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 挂载 / 卸载 ISO 镜像（调用 Windows 内置 PowerShell 命令）。
    /// </summary>
    public static MountResult MountIso(string isoPath)
    {
        var (ok, output) = RunPowerShell($"Mount-DiskImage -ImagePath \"{isoPath}\" -PassThru | Get-Volume | Select-Object -ExpandProperty DriveLetter");
        var letter = output.Trim();
        if (ok && letter.Length == 1)
        {
            return new MountResult
            {
                Success = true,
                Message = "ISO 已挂载。",
                MountedPath = letter + ":\\"
            };
        }
        return new MountResult
        {
            Success = ok,
            Message = ok ? "ISO 已挂载，但未能确定盘符。" : $"挂载失败：{output}"
        };
    }

    public static string? UnmountIso(string isoPath)
    {
        var (ok, output) = RunPowerShell($"Dismount-DiskImage -ImagePath \"{isoPath}\"");
        return ok ? null : output;
    }

    private static (bool ok, string output) RunPowerShell(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, "无法启动 PowerShell。");

            // 并发读取 stdout/stderr，避免单一流缓冲区写满导致死锁
            var errTask = System.Threading.Tasks.Task.Run(() => process.StandardError.ReadToEnd());
            var output = process.StandardOutput.ReadToEnd();
            var error = errTask.Result;
            process.WaitForExit(30000);

            return (process.ExitCode == 0, string.IsNullOrEmpty(output) ? error : output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ==================== 卷快照 (Shadow Copy) ====================

    /// <summary>
    /// 创建卷影副本快照，返回快照路径。用于恢复被占用/被篡改的系统文件。
    /// </summary>
    public static (bool ok, string message) CreateShadowCopy(string volumeLetter, out string snapshotPath)
    {
        snapshotPath = string.Empty;
        var script = $"(Get-WmiObject -List Win32_ShadowCopy).Create('{volumeLetter}:\\', 'ClientAccessible').ShadowID";
        var (ok, output) = RunPowerShell(script);

        if (!ok || string.IsNullOrWhiteSpace(output))
            return (false, string.IsNullOrWhiteSpace(output) ? "创建快照失败。" : output.Trim());

        var shadowId = output.Trim().ToUpperInvariant();
        var (ok2, path) = RunPowerShell($"(Get-WmiObject Win32_ShadowCopy | Where-Object {{ $_.ID -eq '{shadowId}' }}).DeviceObject");

        snapshotPath = path.Trim();
        return (ok2 && !string.IsNullOrEmpty(snapshotPath), snapshotPath);
    }

    public static List<(string Id, string DeviceObject, DateTime InstallDate)> EnumerateShadowCopies()
    {
        var list = new List<(string, string, DateTime)>();
        try
        {
            var (ok, output) = RunPowerShell(
                "Get-WmiObject Win32_ShadowCopy | ForEach-Object { $_.ID + '|' + $_.DeviceObject + '|' + $_.InstallDate }");

            if (!ok) return list;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split('|');
                if (parts.Length < 2) continue;

                // WMI 日期格式 yyyymmddHHMMSS.ffffff+mmm
                DateTime.TryParseExact(
                    parts.Length > 2 ? parts[2].Split('.')[0] : string.Empty,
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date);

                list.Add((parts[0], parts[1], date));
            }
        }
        catch { }
        return list;
    }

    // ==================== BCD 启动配置 ====================

    /// <summary>
    /// 导出当前 BCD 启动配置（bcdedit 输出）。
    /// </summary>
    public static string ExportBcdConfiguration()
    {
        var (ok, output) = RunPowerShell("bcdedit /enum all");
        return ok ? output : $"读取 BCD 失败（需要管理员权限）：{output}";
    }

    /// <summary>
    /// 修改 BCD 启动项超时时间。⚠️高危
    /// </summary>
    public static string? SetBootTimeout(int seconds)
    {
        var (ok, output) = RunPowerShell($"bcdedit /timeout {seconds}");
        return ok ? null : output;
    }
}
