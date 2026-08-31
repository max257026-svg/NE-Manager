using System.Runtime.InteropServices;
using NEManager.Native;

namespace NEManager.Core.FileSystem;

/// <summary>
/// NTFS 备用数据流 (Alternate Data Stream, ADS) 服务。
/// 病毒、下载标记 (Zone.Identifier)、以及各类隐藏数据都藏在这里。
/// </summary>
public static class AlternateDataStreamService
{
    public sealed class StreamEntry
    {
        public string Name { get; set; } = string.Empty;  // 例如 ":Zone.Identifier:$DATA"
        public long Size { get; set; }

        /// <summary>去掉类型后缀的纯净流名，例如 "Zone.Identifier"。</summary>
        public string CleanName
        {
            get
            {
                var n = Name.TrimStart(':');
                int idx = n.IndexOf(":$DATA", StringComparison.OrdinalIgnoreCase);
                return idx >= 0 ? n[..idx] : n;
            }
        }

        public string SizeText => FileItem.FormatSize(Size);
    }

    /// <summary>
    /// 枚举指定文件/目录的全部备用数据流。
    /// </summary>
    public static List<StreamEntry> Enumerate(string path)
    {
        var list = new List<StreamEntry>();
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = Kernel32.FindFirstStreamW(path, 0, out var data, 0);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return list;

            do
            {
                // 主数据流 "::$DATA" 通常不展示
                if (!data.cStreamName.Equals("::$DATA", StringComparison.Ordinal))
                {
                    list.Add(new StreamEntry { Name = data.cStreamName, Size = data.StreamSize });
                }
                else
                {
                    list.Add(new StreamEntry { Name = data.cStreamName, Size = data.StreamSize });
                }
            }
            while (Kernel32.FindNextStreamW(handle, out data));
        }
        catch
        {
            // 非 NTFS 卷不支持数据流
        }
        finally
        {
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                Kernel32.FindClose(handle);
        }
        return list;
    }

    /// <summary>
    /// 读取某个数据流的内容。
    /// </summary>
    public static byte[] ReadStream(string path, string streamName)
    {
        var fullPath = $"{path}{streamName}";
        var handle = Kernel32.CreateFile(
            fullPath,
            WinConst.GENERIC_READ,
            WinConst.FILE_SHARE_READ,
            IntPtr.Zero,
            WinConst.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return Array.Empty<byte>();

        try
        {
            if (!Kernel32.GetFileSizeEx(handle, out long size) || size <= 0)
                return Array.Empty<byte>();

            if (size > int.MaxValue)
                return Array.Empty<byte>(); // 超大流超出托管缓冲区上限，安全跳过

            IntPtr buffer;
            try
            {
                buffer = Marshal.AllocHGlobal((int)size);
            }
            catch
            {
                return Array.Empty<byte>();
            }

            try
            {
                if (!Kernel32.ReadFile(handle, buffer, (uint)size, out uint read, IntPtr.Zero))
                    return Array.Empty<byte>();

                var result = new byte[read];
                Marshal.Copy(buffer, result, 0, (int)read);
                return result;
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

    /// <summary>
    /// 写入（创建或覆盖）一个数据流。
    /// </summary>
    public static string? WriteStream(string path, string streamName, byte[] content)
    {
        var fullPath = $"{path}{streamName}";
        var handle = Kernel32.CreateFile(
            fullPath,
            WinConst.GENERIC_WRITE,
            0,
            IntPtr.Zero,
            WinConst.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return $"无法打开数据流：{new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";

        try
        {
            var buffer = Marshal.AllocHGlobal(content.Length);
            try
            {
                Marshal.Copy(content, 0, buffer, content.Length);
                if (!Kernel32.WriteFile(handle, buffer, (uint)content.Length, out _, IntPtr.Zero))
                    return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
                Kernel32.FlushFileBuffers(handle);
                return null;
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

    /// <summary>
    /// 创建一个空的数据流。
    /// </summary>
    public static string? CreateStream(string path, string streamName)
    {
        var fullPath = $"{path}:{streamName}";
        var handle = Kernel32.CreateFile(
            fullPath,
            WinConst.GENERIC_WRITE,
            0,
            IntPtr.Zero,
            WinConst.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;

        Kernel32.CloseHandle(handle);
        return null;
    }

    /// <summary>
    /// 删除一个数据流（DeleteFile 支持直接删除命名流）。
    /// </summary>
    public static string? DeleteStream(string path, string streamName)
    {
        var fullPath = $"{path}{streamName}";
        return Kernel32.DeleteFile(fullPath)
            ? null
            : new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
    }

    /// <summary>
    /// 解析 Zone.Identifier 内容（下载来源信息）。
    /// </summary>
    public static Dictionary<string, string> ParseZoneIdentifier(byte[] content)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = System.Text.Encoding.UTF8.GetString(content);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            int eq = trimmed.IndexOf('=');
            if (eq > 0)
                dict[trimmed[..eq]] = trimmed[(eq + 1)..];
        }
        return dict;
    }
}
