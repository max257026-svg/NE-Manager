using System.Text;

namespace NEManager.Core.Binary;

public static class StringExtractor
{
    public record StringHit(string Text, int Offset, bool IsUnicode);

    /// <summary>从二进制中提取 ASCII 字符串（≥ minLen 连续可打印字符）</summary>
    public static List<StringHit> ExtractAnsi(byte[] data, int minLen = 4)
    {
        var hits = new List<StringHit>();
        int i = 0;
        while (i < data.Length)
        {
            int start = i;
            while (i < data.Length && data[i] >= 0x20 && data[i] <= 0x7E) i++;
            int len = i - start;
            if (len >= minLen)
            {
                hits.Add(new StringHit(Encoding.ASCII.GetString(data, start, len), start, false));
            }
            i = start + len + 1;
        }
        return hits;
    }

    /// <summary>提取 UTF-16 LE 字符串（≥ minLen 字符，偶数长度，低字节全可打印）</summary>
    public static List<StringHit> ExtractUnicode(byte[] data, int minLen = 4)
    {
        var hits = new List<StringHit>();
        int i = 0;
        while (i < data.Length - 1)
        {
            int start = i;
            int charCount = 0;
            while (i < data.Length - 1 && data[i] >= 0x20 && data[i] <= 0x7E && data[i + 1] == 0)
            {
                i += 2;
                charCount++;
            }
            if (charCount >= minLen)
            {
                hits.Add(new StringHit(Encoding.Unicode.GetString(data, start, charCount * 2), start, true));
            }
            i = start + Math.Max(2, charCount * 2) + 1;
        }
        return hits;
    }

    public static List<StringHit> ExtractAll(byte[] data, int minLen = 4)
    {
        var all = new List<StringHit>();
        all.AddRange(ExtractAnsi(data, minLen));
        all.AddRange(ExtractUnicode(data, minLen));
        all.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        return all;
    }

    public static List<StringHit> ExtractFromFile(string path, int minLen = 4, long maxSize = 50 * 1024 * 1024)
    {
        var fi = new FileInfo(path);
        if (fi.Length > maxSize) throw new InvalidOperationException($"文件太大（{fi.Length / 1024 / 1024}MB）。最大支持 {maxSize / 1024 / 1024}MB。");
        var bytes = File.ReadAllBytes(path);
        return ExtractAll(bytes, minLen);
    }
}
