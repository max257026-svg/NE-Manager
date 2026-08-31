using System.Text;

namespace NEManager.Core.Apk;

/// <summary>
/// 二进制 Android XML (AXML) 解析器。
/// 支持解析 AndroidManifest.xml 的字符串池、命名空间、节点和属性。
/// 完整的 AXML 规范见：https://android.googlesource.com/platform/frameworks/base/+/refs/heads/master/libs/androidfw/include/androidfw/ResourceTypes.h
/// </summary>
public static class AXmlParser
{
    // Chunk 类型常量
    private const int RES_XML_TYPE = 0x0003;
    private const int RES_XML_RESOURCE_MAP_TYPE = 0x0180;
    private const int RES_XML_STRING_POOL_TYPE = 0x0001;
    private const int RES_XML_NAMESPACE_START_TYPE = 0x0102;
    private const int RES_XML_NAMESPACE_END_TYPE = 0x0103;
    private const int RES_XML_ELEMENT_START_TYPE = 0x0102;
    private const int RES_XML_ELEMENT_END_TYPE = 0x0103;
    private const int RES_XML_CDATA_TYPE = 0x0104;

    /// <summary>
    /// 解析二进制 AXML 数据，返回可读的 XML 字符串。
    /// </summary>
    public static string Parse(byte[] data)
    {
        if (data == null || data.Length < 8) return string.Empty;

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        // 检查 AXML 魔数
        var type = reader.ReadUInt16();
        var headerSize = reader.ReadUInt16();
        if (type != RES_XML_TYPE) return "<error>不是有效的 AXML 文件</error>";

        reader.ReadUInt32(); // 文件总大小

        var sb = new StringBuilder();
        var stringPool = new List<string>();
        var resourceMap = new Dictionary<int, string>();
        var elementStack = new Stack<string>();
        var indent = 0;

        // 遍历所有 chunk
        while (stream.Position < stream.Length - 8)
        {
            var chunkType = reader.ReadUInt16();
            var chunkHeaderSize = reader.ReadUInt16();
            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;

            if (chunkType == RES_XML_STRING_POOL_TYPE)
            {
                stringPool = ParseStringPool(reader, chunkSize);
            }
            else if (chunkType == RES_XML_RESOURCE_MAP_TYPE)
            {
                resourceMap = ParseResourceMap(reader, chunkSize);
            }
            else if (chunkType == RES_XML_ELEMENT_START_TYPE)
            {
                // 跳过 4 字节 (namespace uri index)
                reader.ReadUInt32();
                var nameIdx = reader.ReadUInt32();
                // 跳过 4 字节 (start line + 4 byte unknown)
                reader.ReadUInt32();
                reader.ReadUInt32();

                var attrStart = reader.ReadUInt16();
                var attrSize = reader.ReadUInt16();
                var attrCount = reader.ReadUInt16();

                var elementName = nameIdx < stringPool.Count ? stringPool[(int)nameIdx] : $"element_{nameIdx}";
                elementStack.Push(elementName);

                sb.Append(' ', indent * 2);
                sb.Append('<');
                sb.Append(elementName);

                // 解析属性
                for (int i = 0; i < attrCount; i++)
                {
                    var attrNsIdx = reader.ReadUInt32();
                    var attrNameIdx = reader.ReadUInt32();
                    var attrRawValueIdx = reader.ReadUInt32();
                    var attrDataType = reader.ReadByte();
                    var attrData = reader.ReadByte();
                    reader.ReadUInt16(); // skip
                    reader.ReadUInt16(); // skip
                    var typedValue = reader.ReadUInt32();

                    var attrName = attrNameIdx < stringPool.Count ? stringPool[(int)attrNameIdx] : $"attr_{attrNameIdx}";
                    var attrValue = attrRawValueIdx < stringPool.Count ? stringPool[(int)attrRawValueIdx] : FormatTypedValue(attrDataType, typedValue);

                    sb.Append(' ');
                    sb.Append(attrName);
                    sb.Append("=\"");
                    sb.Append(EscapeXml(attrValue));
                    sb.Append('"');
                }

                sb.AppendLine(">");
                indent++;
            }
            else if (chunkType == RES_XML_ELEMENT_END_TYPE)
            {
                indent = Math.Max(0, indent - 1);
                reader.ReadUInt32(); // namespace uri index
                var nameIdx = reader.ReadUInt32();

                var elementName = nameIdx < stringPool.Count ? stringPool[(int)nameIdx] : $"element_{nameIdx}";
                if (elementStack.Count > 0) elementStack.Pop();

                sb.Append(' ', indent * 2);
                sb.Append("</");
                sb.Append(elementName);
                sb.AppendLine(">");
            }

            // 跳到下一个 chunk
            stream.Position = chunkStart + chunkSize;
        }

        return sb.ToString();
    }

    private static List<string> ParseStringPool(BinaryReader reader, uint chunkSize)
    {
        var result = new List<string>();
        var startPos = reader.BaseStream.Position;

        reader.ReadUInt32(); // stringCount
        var flags = reader.ReadUInt32();
        var stringsStart = reader.ReadUInt32();
        // 跳过 styleCount + stylesStart
        reader.ReadUInt32();
        reader.ReadUInt32();

        // 读取字符串偏移表（简化：跳过）
        // 直接尝试读取字符串区域
        var stringRegionStart = startPos + stringsStart;
        var endPos = startPos + chunkSize;

        reader.BaseStream.Position = stringRegionStart;

        while (reader.BaseStream.Position < endPos - 1)
        {
            try
            {
                if ((flags & 0x100) != 0)
                {
                    // UTF-16
                    var len = reader.ReadUInt16();
                    var bytes = reader.ReadBytes(len * 2);
                    var s = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(s)) result.Add(s);
                }
                else
                {
                    // UTF-8
                    var len = reader.ReadByte();
                    if (len == 0) len = reader.ReadByte();
                    var bytes = reader.ReadBytes(len);
                    var s = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(s)) result.Add(s);
                }
            }
            catch { break; }
        }

        return result;
    }

    private static Dictionary<int, string> ParseResourceMap(BinaryReader reader, uint chunkSize)
    {
        var result = new Dictionary<int, string>();
        // 简化：resource map 是 int 数组，这里跳过
        return result;
    }

    private static string FormatTypedValue(byte dataType, uint data)
    {
        return dataType switch
        {
            0x00 => "null",
            0x01 => data == 0 ? "false" : "true",  // bool
            0x10 => data.ToString(),                // int dec
            0x11 => "0x" + data.ToString("X"),     // int hex
            0x12 => "0x" + data.ToString("X8"),    // int color argb
            0x13 => "0x" + data.ToString("X6"),    // int color rgb
            0x20 => data.ToString(),                // float
            _ => $"0x{data.ToString("X")}"
        };
    }

    private static string EscapeXml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");

    /// <summary>
    /// 简化解析：直接提取 manifest 中的关键信息（包名、版本、权限、Activity）。
    /// </summary>
    public static Dictionary<string, List<string>> QuickExtract(byte[] data)
    {
        var result = new Dictionary<string, List<string>>
        {
            ["Permissions"] = new(),
            ["Activities"] = new(),
            ["Services"] = new(),
            ["Receivers"] = new(),
            ["Providers"] = new(),
            ["Package"] = new(),
            ["Version"] = new()
        };

        var xml = Parse(data);
        ExtractTag(xml, "package", result["Package"]);
        ExtractTag(xml, "versionName", result["Version"]);
        ExtractAttributeValues(xml, "android.permission", result["Permissions"]);
        ExtractAttributeValues(xml, ".activity", result["Activities"]);
        ExtractAttributeValues(xml, ".service", result["Services"]);
        ExtractAttributeValues(xml, ".receiver", result["Receivers"]);
        ExtractAttributeValues(xml, ".provider", result["Providers"]);

        return result;
    }

    private static void ExtractTag(string xml, string tag, List<string> list)
    {
        // 简单正则匹配
        var match = System.Text.RegularExpressions.Regex.Match(xml, $"{tag}=\"([^\"]+)\"");
        if (match.Success) list.Add(match.Groups[1].Value);
    }

    private static void ExtractAttributeValues(string xml, string keyword, List<string> list)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(xml, "name=\"([^\"]+)\"");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var val = m.Groups[1].Value;
            if (val.Contains(keyword) || val.Contains(".") && val.Length > 5)
                list.Add(val);
        }
    }
}
