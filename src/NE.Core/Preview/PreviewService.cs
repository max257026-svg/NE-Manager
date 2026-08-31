namespace NEManager.Core.Preview;

/// <summary>
/// 文件预览类型检测与内容读取。
/// </summary>
public static class PreviewService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".tif", ".webp", ".svg"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".py", ".java", ".c", ".cpp", ".h", ".hpp", ".rs", ".go", ".rb", ".php",
        ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".sh", ".bat", ".ps1", ".sql",
        ".xaml", ".config", ".sln", ".csproj"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mpg", ".mpeg"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus", ".aiff"
    };

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".woff", ".woff2", ".eot", ".ttc"
    };

    public static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path));
    public static bool IsTextFile(string path) => TextExtensions.Contains(Path.GetExtension(path));
    public static bool IsVideoFile(string path) => VideoExtensions.Contains(Path.GetExtension(path));
    public static bool IsAudioFile(string path) => AudioExtensions.Contains(Path.GetExtension(path));
    public static bool IsFontFile(string path) => FontExtensions.Contains(Path.GetExtension(path));

    public static string GetPreviewType(string path)
    {
        if (IsImageFile(path)) return "Image";
        if (IsTextFile(path)) return "Text";
        if (IsVideoFile(path)) return "Video";
        if (IsAudioFile(path)) return "Audio";
        if (IsFontFile(path)) return "Font";

        try
        {
            var buffer = new byte[512];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read > 0 && IsTextContent(buffer, read))
                return "Text";
        }
        catch { }

        if (new FileInfo(path).Length == 0) return "None";
        return "Binary";
    }

    public static string ReadTextPreview(string path, int maxLines = 1000)
    {
        var lines = new List<string>();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fs);
            string? line;
            int count = 0;
            while ((line = reader.ReadLine()) != null && count < maxLines)
            {
                lines.Add(line);
                count++;
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static (int width, int height, string format) GetImageInfo(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => ReadPngInfo(fs),
                ".jpg" or ".jpeg" => ReadJpegInfo(fs),
                ".gif" => ReadGifInfo(fs),
                ".bmp" => ReadBmpInfo(fs),
                _ => (0, 0, "Unknown")
            };
        }
        catch
        {
            return (0, 0, "Unknown");
        }
    }

    private static (int width, int height, string format) ReadPngInfo(Stream fs)
    {
        var header = new byte[24];
        if (fs.Read(header, 0, 24) < 24) return (0, 0, "Unknown");
        if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
            return (0, 0, "Unknown");
        int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        return (width, height, "PNG");
    }

    private static (int width, int height, string format) ReadJpegInfo(Stream fs)
    {
        var b = fs.ReadByte();
        if (b != 0xFF) return (0, 0, "Unknown");
        b = fs.ReadByte();
        if (b != 0xD8) return (0, 0, "Unknown");

        while (true)
        {
            b = fs.ReadByte();
            if (b == -1) return (0, 0, "Unknown");
            if (b != 0xFF) continue;
            b = fs.ReadByte();
            if (b == -1) return (0, 0, "Unknown");
            if (b == 0xC0 || b == 0xC1 || b == 0xC2)
            {
                var buf = new byte[7];
                if (fs.Read(buf, 0, 7) < 7) return (0, 0, "Unknown");
                int height = (buf[1] << 8) | buf[2];
                int width = (buf[3] << 8) | buf[4];
                return (width, height, "JPEG");
            }
            var lenBuf = new byte[2];
            if (fs.Read(lenBuf, 0, 2) < 2) return (0, 0, "Unknown");
            int len = (lenBuf[0] << 8) | lenBuf[1];
            fs.Seek(len - 2, SeekOrigin.Current);
        }
    }

    private static (int width, int height, string format) ReadGifInfo(Stream fs)
    {
        var header = new byte[10];
        if (fs.Read(header, 0, 10) < 10) return (0, 0, "Unknown");
        if (header[0] != 'G' || header[1] != 'I' || header[2] != 'F')
            return (0, 0, "Unknown");
        int width = header[6] | (header[7] << 8);
        int height = header[8] | (header[9] << 8);
        return (width, height, "GIF");
    }

    private static (int width, int height, string format) ReadBmpInfo(Stream fs)
    {
        var header = new byte[26];
        if (fs.Read(header, 0, 26) < 26) return (0, 0, "Unknown");
        if (header[0] != 'B' || header[1] != 'M') return (0, 0, "Unknown");
        int width = BitConverter.ToInt32(header, 18);
        int height = Math.Abs(BitConverter.ToInt32(header, 22));
        return (width, height, "BMP");
    }

    private static bool IsTextContent(byte[] buffer, int length)
    {
        int nullCount = 0;
        for (int i = 0; i < length; i++)
        {
            byte b = buffer[i];
            if (b == 0) nullCount++;
            if (nullCount > 2) return false;
        }
        return true;
    }
}
