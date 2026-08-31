namespace NEManager.Core.Tools;

/// <summary>
/// 进制转换、时间戳、Base64、URL 编码等工具。
/// </summary>
public static class ConverterService
{
    public static string ToBinary(long value, int bits)
    {
        return Convert.ToString(value, 2).PadLeft(bits, '0');
    }

    public static string ToOctal(long value)
    {
        return Convert.ToString(value, 8);
    }

    public static string ToHex(long value)
    {
        return Convert.ToString(value, 16).ToUpperInvariant();
    }

    public static long ToDecimal(string value, int fromBase)
    {
        return Convert.ToInt64(value, fromBase);
    }

    public static DateTime TimestampToDateTime(long timestamp, bool isUnix)
    {
        if (isUnix)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }
        else
        {
            return DateTime.FromFileTimeUtc(timestamp);
        }
    }

    public static long DateTimeToTimestamp(DateTime dt, bool isUnix)
    {
        if (isUnix)
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }
        else
        {
            return dt.ToFileTimeUtc();
        }
    }

    public static string Base64Encode(byte[] data)
    {
        return Convert.ToBase64String(data);
    }

    public static byte[] Base64Decode(string text)
    {
        return Convert.FromBase64String(text);
    }

    public static string UrlEncode(string text)
    {
        return Uri.EscapeDataString(text);
    }

    public static string UrlDecode(string text)
    {
        return Uri.UnescapeDataString(text);
    }

    public static (byte r, byte g, byte b, byte a) ToColorBytes(string hexColor)
    {
        hexColor = hexColor.TrimStart('#');
        if (hexColor.Length == 6)
        {
            byte r = Convert.ToByte(hexColor[..2], 16);
            byte g = Convert.ToByte(hexColor[2..4], 16);
            byte b = Convert.ToByte(hexColor[4..6], 16);
            return (r, g, b, 0xFF);
        }
        if (hexColor.Length == 8)
        {
            byte r = Convert.ToByte(hexColor[..2], 16);
            byte g = Convert.ToByte(hexColor[2..4], 16);
            byte b = Convert.ToByte(hexColor[4..6], 16);
            byte a = Convert.ToByte(hexColor[6..8], 16);
            return (r, g, b, a);
        }
        throw new FormatException("无效的颜色格式，请使用 #RRGGBB 或 #RRGGBBAA。");
    }
}
