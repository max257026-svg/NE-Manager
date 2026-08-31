using System.Text;
using System.Text.RegularExpressions;

namespace NEManager.Core.Text;

/// <summary>
/// 大文本文件读取工具类。
/// </summary>
public static class LargeTextReader
{
    public static List<string> ReadLines(string path, int maxLines)
    {
        var lines = new List<string>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fs, DetectEncoding(path));
        string? line;
        int count = 0;
        while ((line = reader.ReadLine()) != null && count < maxLines)
        {
            lines.Add(line);
            count++;
        }
        return lines;
    }

    public static string ReadRange(string path, long startOffset, int byteCount, Encoding encoding)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(startOffset, SeekOrigin.Begin);
        var buffer = new byte[byteCount];
        int read = fs.Read(buffer, 0, byteCount);
        if (read < byteCount)
            Array.Resize(ref buffer, read);
        return encoding.GetString(buffer);
    }

    public static Encoding DetectEncoding(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bom = new byte[4];
        int read = fs.Read(bom, 0, 4);

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return Encoding.UTF8;
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        if (read >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
            return Encoding.UTF32;

        return Encoding.UTF8;
    }

    public static long GetLineCount(string path)
    {
        long count = 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fs);
        while (reader.ReadLine() != null)
        {
            count++;
        }
        return count;
    }

    public static List<(long line, string text)> SearchInFile(string path, string pattern, bool regex, bool caseSensitive)
    {
        var results = new List<(long line, string text)>();
        var encoding = DetectEncoding(path);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fs, encoding);

        Regex? rx = null;
        if (regex)
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            rx = new Regex(pattern, options);
        }

        string? line;
        long lineNum = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            bool matched;
            if (rx != null)
            {
                matched = rx.IsMatch(line);
            }
            else
            {
                matched = caseSensitive
                    ? line.Contains(pattern)
                    : line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }

            if (matched)
                results.Add((lineNum, line));
        }

        return results;
    }
}
