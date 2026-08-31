using System.IO.Compression;
using System.Text;

namespace NEManager.Core.FileSystem;

/// <summary>
/// TAR.GZ 归档浏览服务
/// </summary>
public static class TarGzService
{
    /// <summary>
    /// TAR 文件条目
    /// </summary>
    public class TarEntry
    {
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string Mode { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Group { get; set; } = "";
        public long Offset { get; set; }
    }

    /// <summary>
    /// 列出 TAR.GZ 中的文件
    /// </summary>
    public static List<TarEntry> ListTarGzFiles(string archivePath)
    {
        var entries = new List<TarEntry>();

        try
        {
            using var fileStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            
            // 解压到内存（对于大文件可能需要流式处理）
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // 解析 TAR 格式
            while (memoryStream.Position < memoryStream.Length - 512)
            {
                var header = new byte[512];
                var bytesRead = memoryStream.Read(header, 0, 512);
                if (bytesRead < 512) break;

                // 检查是否为空块（结束标记）
                if (header.All(b => b == 0)) break;

                var entry = ParseTarHeader(header, memoryStream.Position);
                if (entry != null)
                {
                    entries.Add(entry);

                    // 跳过文件内容（对齐到 512 字节边界）
                    var sizeInBlocks = (entry.Size + 511) / 512;
                    memoryStream.Seek(sizeInBlocks * 512, SeekOrigin.Current);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            entries.Add(new TarEntry
            {
                Name = $"读取失败: {ex.Message}",
                Size = 0,
                IsDirectory = false,
                Mode = "----------"
            });
        }

        return entries;
    }

    /// <summary>
    /// 列出 TAR 中的文件（未压缩）
    /// </summary>
    public static List<TarEntry> ListTarFiles(string archivePath)
    {
        var entries = new List<TarEntry>();

        try
        {
            using var fileStream = File.OpenRead(archivePath);

            while (fileStream.Position < fileStream.Length - 512)
            {
                var header = new byte[512];
                var bytesRead = fileStream.Read(header, 0, 512);
                if (bytesRead < 512) break;

                if (header.All(b => b == 0)) break;

                var entry = ParseTarHeader(header, fileStream.Position);
                if (entry != null)
                {
                    entries.Add(entry);

                    var sizeInBlocks = (entry.Size + 511) / 512;
                    fileStream.Seek(sizeInBlocks * 512, SeekOrigin.Current);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            entries.Add(new TarEntry
            {
                Name = $"读取失败: {ex.Message}",
                Size = 0,
                IsDirectory = false,
                Mode = "----------"
            });
        }

        return entries;
    }

    /// <summary>
    /// 提取 TAR.GZ 中的单个文件
    /// </summary>
    public static byte[]? ExtractFile(string archivePath, string fileName)
    {
        try
        {
            using var fileStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            while (memoryStream.Position < memoryStream.Length - 512)
            {
                var header = new byte[512];
                var bytesRead = memoryStream.Read(header, 0, 512);
                if (bytesRead < 512) break;

                if (header.All(b => b == 0)) break;

                var entry = ParseTarHeader(header, memoryStream.Position);
                if (entry == null) break;

                var sizeInBlocks = (entry.Size + 511) / 512;

                if (entry.Name == fileName && !entry.IsDirectory)
                {
                    var fileData = new byte[entry.Size];
                    memoryStream.Read(fileData, 0, (int)entry.Size);
                    return fileData;
                }

                memoryStream.Seek(sizeInBlocks * 512, SeekOrigin.Current);
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 解析 TAR 头部
    /// </summary>
    private static TarEntry? ParseTarHeader(byte[] header, long offset)
    {
        try
        {
            // 文件名（0-100 字节）
            var name = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
            if (string.IsNullOrEmpty(name)) return null;

            // 文件模式（100-108 字节）
            var mode = Encoding.ASCII.GetString(header, 100, 8).TrimEnd('\0');

            // 所有者 ID（108-116 字节）
            var owner = Encoding.ASCII.GetString(header, 108, 8).TrimEnd('\0');

            // 组 ID（116-124 字节）
            var group = Encoding.ASCII.GetString(header, 116, 8).TrimEnd('\0');

            // 文件大小（124-136 字节，八进制）
            var sizeStr = Encoding.ASCII.GetString(header, 124, 12).TrimEnd('\0');
            var size = Convert.ToInt64(sizeStr, 8);

            // 修改时间（136-148 字节，八进制）
            var mtimeStr = Encoding.ASCII.GetString(header, 136, 12).TrimEnd('\0');
            var mtime = Convert.ToInt64(mtimeStr, 8);
            var modifiedTime = DateTimeOffset.FromUnixTimeSeconds(mtime).DateTime;

            // 文件类型（156 字节）
            var typeFlag = (char)header[156];
            var isDirectory = typeFlag == '5' || name.EndsWith('/');

            return new TarEntry
            {
                Name = name,
                Size = size,
                IsDirectory = isDirectory,
                ModifiedTime = modifiedTime,
                Mode = mode,
                Owner = owner,
                Group = group,
                Offset = offset
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    public static string FormatSize(long size)
    {
        return size switch
        {
            > 1_000_000_000 => $"{size / 1_000_000_000.0:F1} GB",
            > 1_000_000 => $"{size / 1_000_000.0:F1} MB",
            > 1_000 => $"{size / 1_000.0:F1} KB",
            _ => $"{size} B"
        };
    }
}
