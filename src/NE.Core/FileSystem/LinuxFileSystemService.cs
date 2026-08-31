using System.IO;
using System.Text;

namespace NEManager.Core.FileSystem;

public static class LinuxFileSystemService
{
    // EXT4 超级块结构
    public class Ext4SuperBlock
    {
        public uint InodesCount { get; set; }
        public uint BlocksCount { get; set; }
        public uint BlockSize { get; set; }
        public uint FragSize { get; set; }
        public uint BlocksPerGroup { get; set; }
        public uint InodesPerGroup { get; set; }
        public string VolumeName { get; set; } = "";
        public string Magic { get; set; } = "";
    }

    // 读取 EXT4 超级块
    public static Ext4SuperBlock? ReadExt4SuperBlock(string imagePath)
    {
        try
        {
            using var fs = File.OpenRead(imagePath);
            using var reader = new BinaryReader(fs);
            
            // EXT4 超级块在偏移 1024 字节
            fs.Seek(1024, SeekOrigin.Begin);
            
            var sb = new Ext4SuperBlock();
            
            sb.InodesCount = reader.ReadUInt32();
            sb.BlocksCount = reader.ReadUInt32();
            reader.ReadUInt32(); // r_blocks_count
            reader.ReadUInt32(); // free_blocks_count
            reader.ReadUInt32(); // free_inodes_count
            reader.ReadUInt32(); // first_data_block
            
            var logBlockSize = reader.ReadUInt32();
            sb.BlockSize = (uint)(1024 << (int)logBlockSize);
            
            var logFragSize = reader.ReadUInt32();
            sb.FragSize = (uint)(1024 << (int)logFragSize);
            
            sb.BlocksPerGroup = reader.ReadUInt32();
            reader.ReadUInt32(); // frags_per_group
            sb.InodesPerGroup = reader.ReadUInt32();
            
            // 跳过一些字段
            fs.Seek(1024 + 56, SeekOrigin.Begin);
            var magic = reader.ReadUInt16();
            sb.Magic = magic.ToString("X4");
            
            // 读取卷名（在偏移 1194）
            fs.Seek(1024 + 1194 - 1024, SeekOrigin.Begin);
            var volumeNameBytes = reader.ReadBytes(16);
            sb.VolumeName = Encoding.ASCII.GetString(volumeNameBytes).TrimEnd('\0');
            
            return sb;
        }
        catch
        {
            return null;
        }
    }

    // 检测文件系统类型
    public static string DetectFileSystemType(string imagePath)
    {
        try
        {
            using var fs = File.OpenRead(imagePath);
            using var reader = new BinaryReader(fs);
            
            // 检查 EXT4 magic (0xEF53 at offset 0x438)
            fs.Seek(0x438, SeekOrigin.Begin);
            var magic = reader.ReadUInt16();
            if (magic == 0xEF53)
                return "EXT4";
            
            // 检查 BTRFS magic ("_BHRfS_M" at offset 0x10040)
            fs.Seek(0x10040, SeekOrigin.Begin);
            var btrfsMagic = Encoding.ASCII.GetString(reader.ReadBytes(8));
            if (btrfsMagic == "_BHRfS_M")
                return "BTRFS";
            
            // 检查 XFS magic ("XFSB" at offset 0)
            fs.Seek(0, SeekOrigin.Begin);
            var xfsMagic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (xfsMagic == "XFSB")
                return "XFS";
            
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    // 列出镜像中的文件（简化版，仅支持读取根目录）
    public static List<Ext4FileEntry> ListExt4Files(string imagePath)
    {
        var files = new List<Ext4FileEntry>();
        
        try
        {
            using var fs = File.OpenRead(imagePath);
            using var reader = new BinaryReader(fs);
            
            // 读取超级块获取块大小
            fs.Seek(1024 + 24, SeekOrigin.Begin);
            var logBlockSize = reader.ReadUInt32();
            var blockSize = 1024u << (int)logBlockSize;
            
            // 读取根 inode (inode 2)
            // 这里简化处理，实际 EXT4 解析非常复杂
            // 真实实现需要解析 block group descriptor table 和 inode table
            
            // 返回一个示例条目
            files.Add(new Ext4FileEntry
            {
                Name = "(EXT4 镜像 - 完整解析需要专用库)",
                Size = 0,
                IsDirectory = true,
                Mode = "drwxr-xr-x"
            });
        }
        catch (Exception ex)
        {
            files.Add(new Ext4FileEntry
            {
                Name = $"读取失败: {ex.Message}",
                Size = 0,
                IsDirectory = false,
                Mode = "----------"
            });
        }
        
        return files;
    }
}

public class Ext4FileEntry
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public string Mode { get; set; } = "";
    public uint Inode { get; set; }
}
