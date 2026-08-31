using System.Text;

namespace NEManager.Core.Apk;

/// <summary>
/// Android resources.arsc 资源表解析器。
/// 用于读取 APK 中 res/values/strings.xml 等编译后的二进制资源表。
/// </summary>
public static class ArscParser
{
    private const int RES_TABLE_TYPE = 0x0002;
    private const int RES_STRING_POOL_TYPE = 0x0001;
    private const int RES_XML_RESOURCE_MAP_TYPE = 0x0180;

    public class ArscInfo
    {
        public List<string> StringPool { get; set; } = new();
        public List<ArscPackage> Packages { get; set; } = new();
    }

    public class ArscPackage
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ArscType> Types { get; set; } = new();
    }

    public class ArscType
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ArscResource> Resources { get; set; } = new();
    }

    public class ArscResource
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public static ArscInfo Parse(byte[] data)
    {
        var info = new ArscInfo();
        if (data == null || data.Length < 12) return info;

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var type = reader.ReadUInt16();
        if (type != RES_TABLE_TYPE) return info;

        var headerSize = reader.ReadUInt16();
        reader.ReadUInt32(); // size
        var packageCount = reader.ReadUInt32();

        // 全局字符串池
        var gpOff = reader.ReadUInt32();
        if (gpOff < data.Length)
        {
            info.StringPool = ReadStringPool(data, (int)gpOff);
        }

        // 跳过 header
        stream.Position = headerSize;

        // 读取 package chunks
        for (int i = 0; i < packageCount; i++)
        {
            if (stream.Position >= data.Length - 8) break;

            var pkgType = reader.ReadUInt16();
            var pkgHeaderSize = reader.ReadUInt16();
            var pkgSize = reader.ReadUInt32();

            if (pkgType != 0x0200) { stream.Position += pkgSize; continue; }

            var pkgId = reader.ReadUInt32();
            // 读取包名（64 字节 UTF-16）
            var nameBytes = reader.ReadBytes(128);
            var pkgName = Encoding.Unicode.GetString(nameBytes).TrimEnd('\0', '\u0000');

            var pkg = new ArscPackage { Id = (int)pkgId, Name = pkgName };
            info.Packages.Add(pkg);

            // 简化：只读取包头，不完整解析类型/资源表
            stream.Position += (int)pkgSize - 0x120;
        }

        return info;
    }

    private static List<string> ReadStringPool(byte[] data, int offset)
    {
        var result = new List<string>();
        if (offset + 40 > data.Length) return result;

        // 验证 chunk type
        var type = BitConverter.ToUInt16(data, offset);
        if (type != RES_STRING_POOL_TYPE) return result;

        var headerSize = BitConverter.ToUInt16(data, offset + 2);
        var chunkSize = BitConverter.ToInt32(data, offset + 4);
        var stringCount = BitConverter.ToInt32(data, offset + 8);
        var flags = BitConverter.ToInt32(data, offset + 16);
        var stringsStart = BitConverter.ToInt32(data, offset + 20);

        bool isUtf8 = (flags & 0x100) == 0;
        var stringTableStart = offset + stringsStart;

        for (int i = 0; i < stringCount; i++)
        {
            var offPos = offset + headerSize + i * 4;
            if (offPos + 4 > data.Length) break;
            var strOff = BitConverter.ToInt32(data, offPos);
            var absOff = stringTableStart + strOff;
            if (absOff < 0 || absOff >= data.Length) { result.Add(""); continue; }

            try
            {
                if (isUtf8)
                {
                    int pos = absOff;
                    int len = data[pos++];
                    if ((len & 0x80) != 0) len = (len & 0x7F) << 8 | data[pos++];
                    var s = Encoding.UTF8.GetString(data, pos, Math.Min(len, data.Length - pos));
                    result.Add(s);
                }
                else
                {
                    int pos = absOff;
                    int len = BitConverter.ToInt16(data, pos); pos += 2;
                    var s = Encoding.Unicode.GetString(data, pos, Math.Min(len * 2, data.Length - pos));
                    result.Add(s);
                }
            }
            catch { result.Add(""); }
        }

        return result;
    }

    /// <summary>
    /// 快速提取所有字符串资源。
    /// </summary>
    public static List<string> QuickStrings(byte[] data)
    {
        return Parse(data).StringPool;
    }
}
