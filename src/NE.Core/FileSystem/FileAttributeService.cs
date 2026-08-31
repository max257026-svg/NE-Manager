namespace NEManager.Core.FileSystem;

public static class FileAttributeService
{
    /// <summary>设置/清除文件属性（Hidden/ReadOnly/System/Archive）</summary>
    public static (bool Ok, string Msg) SetAttributes(string path, bool? hidden = null, bool? readOnly = null, bool? system = null)
    {
        try
        {
            var fi = new FileInfo(path);
            var attrs = fi.Attributes;
            if (hidden.HasValue) attrs = hidden.Value ? attrs | FileAttributes.Hidden : attrs & ~FileAttributes.Hidden;
            if (readOnly.HasValue) attrs = readOnly.Value ? attrs | FileAttributes.ReadOnly : attrs & ~FileAttributes.ReadOnly;
            if (system.HasValue) attrs = system.Value ? attrs | FileAttributes.System : attrs & ~FileAttributes.System;
            fi.Attributes = attrs;
            return (true, $"已更新 {Path.GetFileName(path)}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>批量对目录下所有文件/子目录设置属性</summary>
    public static (int Ok, int Fail, string[] Errors) BatchSetInDirectory(string dir, bool? hidden = null, bool? readOnly = null, bool? system = null)
    {
        int ok = 0, fail = 0;
        var errors = new List<string>();
        foreach (var f in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories))
        {
            var r = SetAttributes(f, hidden, readOnly, system);
            if (r.Ok) ok++; else { fail++; errors.Add($"{f}: {r.Msg}"); }
        }
        return (ok, fail, errors.ToArray());
    }

    /// <summary>篡改时间戳（创建/访问/修改）</summary>
    public static (bool Ok, string Msg) SetTimestamps(string path, DateTime? creation = null, DateTime? access = null, DateTime? write = null)
    {
        try
        {
            var fi = new FileInfo(path);
            if (creation.HasValue) fi.CreationTime = creation.Value;
            if (access.HasValue) fi.LastAccessTime = access.Value;
            if (write.HasValue) fi.LastWriteTime = write.Value;
            return (true, "时间戳已更新");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
