using System.Text;

namespace NEManager.Core.Tools;

public static class HexDumpService
{
    /// <summary>读取文件指定范围的字节（大文件用 Range 读取）</summary>
    public static byte[] ReadRange(string path, long offset, int length)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[Math.Min(length, fs.Length - offset)];
        fs.Read(buffer, 0, buffer.Length);
        return buffer;
    }

    /// <summary>生成经典 hexdump 文本（地址 + 16字节hex + ASCII）</summary>
    public static string ToHexDump(byte[] data, long baseOffset = 0)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            sb.Append($"{baseOffset + i:X8}  ");
            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
            }
            sb.Append(" |");
            for (int j = 0; j < 16 && i + j < data.Length; j++)
            {
                sb.Append(data[i + j] >= 0x20 && data[i + j] < 0x7F ? (char)data[i + j] : '.');
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }

    /// <summary>修改指定偏移的字节（写回文件）</summary>
    public static bool PatchBytes(string path, long offset, byte[] newBytes)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Write(newBytes, 0, newBytes.Length);
            return true;
        }
        catch { return false; }
    }
}
