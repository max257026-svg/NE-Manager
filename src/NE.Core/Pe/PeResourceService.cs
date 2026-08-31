using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Core.Pe;

/// <summary>PE 版本信息资源读写（简化版 VS_VERSIONINFO）</summary>
public static class PeResourceService
{
    [DllImport("version.dll", CharSet = CharSet.Unicode)] private static extern uint GetFileVersionInfoSizeW(string lptstrFilename, out uint lpdwHandle);
    [DllImport("version.dll", CharSet = CharSet.Unicode)] private static extern bool GetFileVersionInfoW(string lptstrFilename, uint dwHandle, uint dwLen, byte[] lpData);
    [DllImport("version.dll", CharSet = CharSet.Unicode)] private static extern bool VerQueryValueW(byte[] pBlock, string lpSubBlock, out IntPtr lplpBuffer, out uint puLen);

    public record VersionInfo(
        string FileDescription, string ProductName, string CompanyName,
        string OriginalFilename, string LegalCopyright, string FileVersion, string ProductVersion
    );

    /// <summary>读取 PE 的版本资源</summary>
    public static VersionInfo? ReadVersionInfo(string path)
    {
        try
        {
            var size = GetFileVersionInfoSizeW(path, out var handle);
            if (size == 0) return null;

            var data = new byte[size];
            if (!GetFileVersionInfoW(path, handle, size, data)) return null;

            string Query(string key)
            {
                if (!VerQueryValueW(data, "\\StringFileInfo\\040904B0\\" + key, out var buf, out var len)) return string.Empty;
                return Marshal.PtrToStringUni(buf, (int)(len / 2)) ?? string.Empty;
            }

            return new VersionInfo(
                Query("FileDescription"), Query("ProductName"), Query("CompanyName"),
                Query("OriginalFilename"), Query("LegalCopyright"),
                Query("FileVersion"), Query("ProductVersion")
            );
        }
        catch { return null; }
    }

    /// <summary>读取 .NET 程序集元数据（比 PE 版本信息更准）</summary>
    public static VersionInfo? ReadDotNetInfo(string path)
    {
        try
        {
            var asm = AssemblyName.GetAssemblyName(path);
            var va = asm.Version;
            return new VersionInfo(
                FileDescription: string.Empty, ProductName: asm.Name ?? string.Empty, CompanyName: string.Empty,
                OriginalFilename: Path.GetFileName(path), LegalCopyright: string.Empty,
                FileVersion: va?.ToString() ?? string.Empty, ProductVersion: va?.ToString() ?? string.Empty
            );
        }
        catch { return null; }
    }
}
