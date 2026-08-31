using System.Text;

namespace NEManager.Core.Tools;

/// <summary>
/// 编码转换服务。
/// </summary>
public static class EncodingService
{
    private static readonly List<(string name, Encoding encoding)> _encodings = new();

    static EncodingService()
    {
        // .NET Core 默认不包含 GB2312/GBK 等编码，需要注册 CodePages 提供者
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }

        // 预加载所有编码（失败就跳过，不让静态构造函数崩）
        SafeAdd("UTF-8", () => Encoding.UTF8);
        SafeAdd("UTF-16 LE", () => Encoding.Unicode);
        SafeAdd("UTF-16 BE", () => Encoding.BigEndianUnicode);
        SafeAdd("UTF-32", () => Encoding.UTF32);
        SafeAdd("ASCII", () => Encoding.ASCII);
        SafeAdd("GB2312", () => Encoding.GetEncoding("GB2312"));
        SafeAdd("GBK", () => Encoding.GetEncoding("GBK"));
        SafeAdd("Big5", () => Encoding.GetEncoding("Big5"));
        SafeAdd("ISO-8859-1", () => Encoding.Latin1);
        SafeAdd("Shift-JIS", () => Encoding.GetEncoding("Shift_JIS"));
    }

    private static void SafeAdd(string name, Func<Encoding> factory)
    {
        try { _encodings.Add((name, factory())); }
        catch { /* 编码不存在就跳过，不让整个类炸 */ }
    }

    public static byte[] Convert(byte[] data, Encoding from, Encoding to)
    {
        if (from.Equals(to)) return data;
        var text = from.GetString(data);
        return to.GetBytes(text);
    }

    public static string ConvertText(string text, Encoding from, Encoding to)
    {
        if (from.Equals(to)) return text;
        var bytes = from.GetBytes(text);
        return to.GetString(bytes);
    }

    public static Encoding DetectEncoding(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return Encoding.UTF8;
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode;
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0xFE && data[3] == 0xFF)
            return Encoding.UTF32;

        return Encoding.UTF8;
    }

    public static List<(string name, Encoding encoding)> SupportedEncodings => _encodings;
}