using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NEManager.Core.Storage;

/// <summary>
/// 磁盘扇区编辑器 - 直接读写物理磁盘和逻辑卷的原始扇区
/// </summary>
public static class DiskSectorService
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
    private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
    private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

    /// <summary>
    /// 磁盘扇区数据
    /// </summary>
    public class SectorData
    {
        public long SectorNumber { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string HexView { get; set; } = "";
        public string AsciiView { get; set; } = "";
        public int SectorSize { get; set; } = 512;
    }

    /// <summary>
    /// 磁盘几何信息
    /// </summary>
    public class DiskGeometry
    {
        public long Cylinders { get; set; }
        public uint MediaType { get; set; }
        public uint TracksPerCylinder { get; set; }
        public uint SectorsPerTrack { get; set; }
        public uint BytesPerSector { get; set; }
        public long TotalSize => Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector;
    }

    /// <summary>
    /// 获取磁盘几何信息
    /// </summary>
    public static DiskGeometry? GetDiskGeometry(string devicePath)
    {
        try
        {
            var handle = CreateFile(devicePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_NO_BUFFERING, IntPtr.Zero);

            if (handle.IsInvalid) return null;

            try
            {
                var output = new byte[40];
                if (!DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_GEOMETRY,
                    IntPtr.Zero, 0, output, (uint)output.Length, out _, IntPtr.Zero))
                    return null;

                return new DiskGeometry
                {
                    Cylinders = BitConverter.ToInt64(output, 0),
                    MediaType = BitConverter.ToUInt32(output, 8),
                    TracksPerCylinder = BitConverter.ToUInt32(output, 12),
                    SectorsPerTrack = BitConverter.ToUInt32(output, 16),
                    BytesPerSector = BitConverter.ToUInt32(output, 20)
                };
            }
            finally { handle.Close(); }
        }
        catch { return null; }
    }

    /// <summary>
    /// 读取扇区数据
    /// </summary>
    public static SectorData? ReadSector(string devicePath, long sectorNumber, int sectorSize = 512)
    {
        try
        {
            var handle = CreateFile(devicePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_NO_BUFFERING | FILE_FLAG_RANDOM_ACCESS, IntPtr.Zero);

            if (handle.IsInvalid) return null;

            try
            {
                var data = new byte[sectorSize];
                long offset = sectorNumber * sectorSize;

                // 使用 SetFilePointerEx 定位
                var distance = BitConverter.GetBytes(offset);
                var moveMethod = BitConverter.GetBytes(0); // FILE_BEGIN
                var input = new byte[16];
                Array.Copy(distance, 0, input, 0, 8);
                Array.Copy(moveMethod, 0, input, 8, 4);

                // 简化：直接用 FileStream 读取
                using var fs = new FileStream(handle, FileAccess.Read, sectorSize, false);
                fs.Seek(offset, SeekOrigin.Begin);
                var bytesRead = fs.Read(data, 0, sectorSize);

                if (bytesRead < sectorSize)
                    Array.Resize(ref data, bytesRead);

                return new SectorData
                {
                    SectorNumber = sectorNumber,
                    Data = data,
                    SectorSize = bytesRead,
                    HexView = FormatHexView(data),
                    AsciiView = FormatAsciiView(data)
                };
            }
            finally { handle.Close(); }
        }
        catch { return null; }
    }

    /// <summary>
    /// 批量读取扇区
    /// </summary>
    public static List<SectorData> ReadSectors(string devicePath, long startSector, int count, int sectorSize = 512)
    {
        var sectors = new List<SectorData>();
        for (int i = 0; i < count; i++)
        {
            var sector = ReadSector(devicePath, startSector + i, sectorSize);
            if (sector != null)
                sectors.Add(sector);
        }
        return sectors;
    }

    /// <summary>
    /// 写入扇区数据（危险操作）
    /// </summary>
    public static bool WriteSector(string devicePath, long sectorNumber, byte[] data)
    {
        try
        {
            var handle = CreateFile(devicePath, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_NO_BUFFERING | FILE_FLAG_RANDOM_ACCESS, IntPtr.Zero);

            if (handle.IsInvalid) return false;

            try
            {
                using var fs = new FileStream(handle, FileAccess.Write, data.Length, false);
                long offset = sectorNumber * data.Length;
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Write(data, 0, data.Length);
                fs.Flush();
                return true;
            }
            finally { handle.Close(); }
        }
        catch { return false; }
    }

    /// <summary>
    /// 搜索扇区中的字节模式
    /// </summary>
    public static List<long> SearchPattern(string devicePath, byte[] pattern, long startSector, long endSector, int sectorSize = 512)
    {
        var results = new List<long>();
        for (long s = startSector; s <= endSector; s++)
        {
            var sector = ReadSector(devicePath, s, sectorSize);
            if (sector?.Data == null) continue;

            int idx = IndexOf(sector.Data, pattern);
            if (idx >= 0)
                results.Add(s);
        }
        return results;
    }

    /// <summary>
    /// 枚举物理磁盘
    /// </summary>
    public static List<DiskInfo> EnumerateDisks()
    {
        var disks = new List<DiskInfo>();
        for (int i = 0; i < 20; i++)
        {
            var path = $@"\\.\PhysicalDrive{i}";
            var geo = GetDiskGeometry(path);
            if (geo != null)
            {
                disks.Add(new DiskInfo
                {
                    Index = i,
                    DevicePath = path,
                    TotalSize = geo.TotalSize,
                    BytesPerSector = geo.BytesPerSector,
                    MediaType = geo.MediaType == 0 ? "可移动" : "固定"
                });
            }
        }
        return disks;
    }

    public class DiskInfo
    {
        public int Index { get; set; }
        public string DevicePath { get; set; } = "";
        public long TotalSize { get; set; }
        public uint BytesPerSector { get; set; }
        public string MediaType { get; set; } = "";
        public string SizeText => TotalSize switch
        {
            > 1_000_000_000_000 => $"{TotalSize / 1_000_000_000_000.0:F1} TB",
            > 1_000_000_000 => $"{TotalSize / 1_000_000_000.0:F1} GB",
            > 1_000_000 => $"{TotalSize / 1_000_000.0:F1} MB",
            _ => $"{TotalSize} B"
        };
    }

    // 格式化十六进制视图
    private static string FormatHexView(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            sb.Append($"{i:X8}  ");
            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
                if (j == 7) sb.Append(' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // 格式化 ASCII 视图
    private static string FormatAsciiView(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                {
                    var b = data[i + j];
                    sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        if (pattern.Length == 0) return -1;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { found = false; break; }
            }
            if (found) return i;
        }
        return -1;
    }
}
