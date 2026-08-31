using System.IO.Compression;

namespace NEManager.Core.Archive;

/// <summary>
/// ZIP 压缩文件浏览与解压服务。
/// </summary>
public static class ArchiveService
{
    public static List<ArchiveEntry> ListEntries(string archivePath)
    {
        var entries = new List<ArchiveEntry>();
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            entries.Add(new ArchiveEntry
            {
                Name = entry.Name,
                FullPath = entry.FullName,
                Size = entry.Length,
                CompressedSize = entry.CompressedLength,
                IsDirectory = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'),
                LastModified = entry.LastWriteTime.DateTime,
                CompressionMethod = "Deflate"
            });
        }
        return entries;
    }

    public static bool ExtractEntry(string archivePath, string entryPath, string destPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(entryPath);
            if (entry == null) return false;

            var destFile = Path.Combine(destPath, entry.FullName);
            var destDir = Path.GetDirectoryName(destFile);
            if (destDir != null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            entry.ExtractToFile(destFile, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ExtractAll(string archivePath, string destPath)
    {
        try
        {
            ZipFile.ExtractToDirectory(archivePath, destPath, overwriteFiles: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSupported(string path)
    {
        return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    public static Stream GetEntryStream(string archivePath, string entryPath)
    {
        var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry(entryPath);
        if (entry == null)
        {
            archive.Dispose();
            throw new FileNotFoundException($"压缩包中未找到条目: {entryPath}");
        }
        return new ArchiveEntryStream(entry.Open(), archive);
    }

    private class ArchiveEntryStream : Stream
    {
        private readonly Stream _inner;
        private readonly ZipArchive _archive;

        public ArchiveEntryStream(Stream inner, ZipArchive archive)
        {
            _inner = inner;
            _archive = archive;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _archive.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

public class ArchiveEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public string CompressionMethod { get; set; } = string.Empty;
}
