using System.Text;

namespace NEManager.Core.Apk;

/// <summary>
/// DEX 文件解析器 —— 解析 classes.dex 的字符串池、类型池、类定义。
/// DEX 文件格式规范见：https://source.android.com/docs/core/runtime/dex-format
/// </summary>
public static class DexParser
{
    // DEX 魔数: "dex\n035\0"
    private static readonly byte[] DexMagic = { 0x64, 0x65, 0x78, 0x0A, 0x30, 0x33, 0x35, 0x00 };

    public class DexInfo
    {
        public int StringCount { get; set; }
        public List<string> Strings { get; set; } = new();
        public int TypeCount { get; set; }
        public List<string> Types { get; set; } = new();
        public int ClassCount { get; set; }
        public List<DexClass> Classes { get; set; } = new();
        public int MethodCount { get; set; }
        public int FieldCount { get; set; }
    }

    public class DexClass
    {
        public string Name { get; set; } = "";
        public string SuperClass { get; set; } = "";
        public string AccessFlags { get; set; } = "";
        public List<string> Methods { get; set; } = new();
        public List<string> Fields { get; set; } = new();
        public string SourceFile { get; set; } = "";
    }

    private const uint ACC_PUBLIC = 0x0001;
    private const uint ACC_PRIVATE = 0x0002;
    private const uint ACC_PROTECTED = 0x0004;
    private const uint ACC_STATIC = 0x0008;
    private const uint ACC_FINAL = 0x0010;
    private const uint ACC_INTERFACE = 0x0200;
    private const uint ACC_ABSTRACT = 0x0400;
    private const uint ACC_SYNTHETIC = 0x1000;
    private const uint ACC_ANNOTATION = 0x2000;
    private const uint ACC_ENUM = 0x4000;

    public static DexInfo Parse(byte[] data)
    {
        var info = new DexInfo();
        if (data == null || data.Length < 0x70) return info;

        // 检查魔数
        for (int i = 0; i < 8; i++)
            if (data[i] != DexMagic[i]) return info;

        using var stream = new MemoryStream(data);
        using var br = new BinaryReader(stream);

        // 跳过魔数 + version + checksum + signature
        stream.Position = 0x08;
        br.ReadUInt32(); // fileSize
        br.ReadUInt32(); // headerSize (0x70)
        br.ReadUInt32(); // endianTag
        br.ReadUInt32(); // linkSize
        br.ReadUInt32(); // linkOff
        br.ReadUInt32(); // mapOff

        // String pool
        var stringIdsSize = br.ReadUInt32();
        var stringIdsOff = br.ReadUInt32();

        // Type pool
        var typeIdsSize = br.ReadUInt32();
        var typeIdsOff = br.ReadUInt32();

        // 跳过 protoIds, fieldIds, methodIds
        br.ReadUInt32(); br.ReadUInt32();
        br.ReadUInt32(); br.ReadUInt32();
        br.ReadUInt32(); br.ReadUInt32();

        // Class definitions
        var classDefsSize = br.ReadUInt32();
        var classDefsOff = br.ReadUInt32();

        info.StringCount = (int)stringIdsSize;
        info.TypeCount = (int)typeIdsSize;
        info.ClassCount = (int)classDefsSize;

        // 读取字符串池
        info.Strings = ReadStringPool(data, (int)stringIdsOff, (int)stringIdsSize);

        // 读取类型名
        info.Types = ReadTypePool(data, (int)typeIdsOff, (int)typeIdsSize, info.Strings);

        // 读取类定义
        info.Classes = ReadClasses(data, (int)classDefsOff, (int)classDefsSize, info);

        return info;
    }

    private static List<string> ReadStringPool(byte[] data, int stringIdsOff, int count)
    {
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            if (stringIdsOff + i * 4 + 4 > data.Length) break;
            var offset = BitConverter.ToInt32(data, stringIdsOff + i * 4);
            if (offset < 0 || offset >= data.Length)
            {
                result.Add("");
                continue;
            }

            // 跳过 uleb128 size
            int pos = offset;
            int size = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                var b = data[pos++];
                size |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) == 0) break;
            }

            // 读取 UTF-8 字符串
            var end = Array.IndexOf(data, (byte)0, pos);
            if (end < 0 || end > data.Length) end = data.Length;
            var s = Encoding.UTF8.GetString(data, pos, end - pos);
            result.Add(s);
        }
        return result;
    }

    private static List<string> ReadTypePool(byte[] data, int typeIdsOff, int count, List<string> strings)
    {
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            if (typeIdsOff + i * 4 + 4 > data.Length) break;
            var idx = BitConverter.ToInt32(data, typeIdsOff + i * 4);
            result.Add(idx >= 0 && idx < strings.Count ? strings[idx] : $"?{idx}");
        }
        return result;
    }

    private static List<DexClass> ReadClasses(byte[] data, int classDefsOff, int count, DexInfo info)
    {
        var result = new List<DexClass>();
        for (int i = 0; i < count; i++)
        {
            var off = classDefsOff + i * 0x20; // ClassDefItem = 32 bytes
            if (off + 0x20 > data.Length) break;

            var classIdx = BitConverter.ToInt32(data, off + 0);
            var accessFlags = BitConverter.ToUInt32(data, off + 4);
            var superclassIdx = BitConverter.ToInt32(data, off + 8);
            var interfacesOff = BitConverter.ToInt32(data, off + 12);
            var sourceFileIdx = BitConverter.ToInt32(data, off + 16);
            var annotationsOff = BitConverter.ToInt32(data, off + 20);
            var classDataOff = BitConverter.ToInt32(data, off + 24);
            var staticValuesOff = BitConverter.ToInt32(data, off + 28);

            var cls = new DexClass
            {
                Name = classIdx >= 0 && classIdx < info.Types.Count ? info.Types[classIdx] : $"?{classIdx}",
                SuperClass = superclassIdx >= 0 && superclassIdx < info.Types.Count ? info.Types[superclassIdx] : "",
                AccessFlags = FormatAccessFlags(accessFlags),
                SourceFile = sourceFileIdx >= 0 && sourceFileIdx < info.Strings.Count ? info.Strings[sourceFileIdx] : ""
            };

            // 读取 classData （简化）
            if (classDataOff > 0 && classDataOff < data.Length)
            {
                var methods = ParseClassDataMethods(data, classDataOff, info.Strings);
                cls.Methods = methods;
            }

            result.Add(cls);
        }
        return result;
    }

    private static List<string> ParseClassDataMethods(byte[] data, int offset, List<string> strings)
    {
        var result = new List<string>();
        if (offset >= data.Length) return result;

        // 简化：只读取 uleb128 计数然后尝试读取
        int pos = offset;

        // 跳过 staticFieldsSize + instanceFieldsSize + directMethodsSize + virtualMethodsSize
        // 这些是 uleb128，简化跳过
        for (int i = 0; i < 4; i++)
            pos = SkipUleb128(data, pos);

        // 跳过 fields 区
        pos = SkipUleb128(data, pos); // directMethodsSize
        int directMethodsSize = ReadUleb128(data, ref pos);

        // 简化：不完整解析 method_item，只返回占位符
        // 完整实现需要解析 method_item = method_idx(uleb128) + access_flags(uleb128) + code_off(uleb128)
        for (int i = 0; i < Math.Min(directMethodsSize, 50); i++)
        {
            pos = SkipUleb128(data, pos);
            pos = SkipUleb128(data, pos);
            pos = SkipUleb128(data, pos);
        }

        return result;
    }

    private static int ReadUleb128(byte[] data, ref int pos)
    {
        int result = 0, shift = 0;
        while (pos < data.Length)
        {
            var b = data[pos++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return result;
    }

    private static int SkipUleb128(byte[] data, int pos)
    {
        while (pos < data.Length)
        {
            var b = data[pos++];
            if ((b & 0x80) == 0) break;
        }
        return pos;
    }

    private static string FormatAccessFlags(uint flags)
    {
        var parts = new List<string>();
        if ((flags & ACC_PUBLIC) != 0) parts.Add("public");
        if ((flags & ACC_PRIVATE) != 0) parts.Add("private");
        if ((flags & ACC_PROTECTED) != 0) parts.Add("protected");
        if ((flags & ACC_STATIC) != 0) parts.Add("static");
        if ((flags & ACC_FINAL) != 0) parts.Add("final");
        if ((flags & ACC_INTERFACE) != 0) parts.Add("interface");
        if ((flags & ACC_ABSTRACT) != 0) parts.Add("abstract");
        if ((flags & ACC_SYNTHETIC) != 0) parts.Add("synthetic");
        if ((flags & ACC_ANNOTATION) != 0) parts.Add("@annotation");
        if ((flags & ACC_ENUM) != 0) parts.Add("enum");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// 快速检查是否为 DEX 文件。
    /// </summary>
    public static bool IsDex(byte[] data)
    {
        if (data == null || data.Length < 8) return false;
        for (int i = 0; i < 8; i++)
            if (data[i] != DexMagic[i]) return false;
        return true;
    }

    /// <summary>
    /// 将类型描述符转换为可读类名。
    /// </summary>
    public static string TypeDescriptorToName(string desc)
    {
        if (string.IsNullOrEmpty(desc)) return desc;
        if (desc.StartsWith("L") && desc.EndsWith(";"))
            return desc.Substring(1, desc.Length - 2).Replace('/', '.');
        if (desc.StartsWith('['))
            return desc + "[]";
        return desc switch
        {
            "Z" => "boolean",
            "B" => "byte",
            "C" => "char",
            "S" => "short",
            "I" => "int",
            "J" => "long",
            "F" => "float",
            "D" => "double",
            "V" => "void",
            _ => desc
        };
    }
}
