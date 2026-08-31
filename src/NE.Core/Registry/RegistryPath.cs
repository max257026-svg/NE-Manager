using Microsoft.Win32;

namespace NEManager.Core.Registry;

/// <summary>
/// 注册表路径解析工具：把 "HKEY_LOCAL_MACHINE\SOFTWARE\Foo" 拆成根键 + 子路径。
/// </summary>
public static class RegistryPath
{
    public static readonly string[] RootNames =
    {
        "HKEY_CLASSES_ROOT", "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE",
        "HKEY_USERS", "HKEY_CURRENT_CONFIG", "HKCR", "HKCU", "HKLM", "HKU", "HKCC"
    };

    public static RegistryKey ToBaseKey(string fullPath, out string subPath, bool writable)
    {
        subPath = string.Empty;
        fullPath = fullPath.Trim().TrimEnd('\\');

        int slash = fullPath.IndexOf('\\');
        string rootName = slash < 0 ? fullPath : fullPath[..slash];
        subPath = slash < 0 ? string.Empty : fullPath[(slash + 1)..];

        RegistryHive hive = rootName.ToUpperInvariant() switch
        {
            "HKEY_CLASSES_ROOT" or "HKCR" => RegistryHive.ClassesRoot,
            "HKEY_CURRENT_USER" or "HKCU" => RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => RegistryHive.LocalMachine,
            "HKEY_USERS" or "HKU" => RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => RegistryHive.CurrentConfig,
            _ => throw new ArgumentException($"未知的注册表根键：{rootName}")
        };

        return writable
            ? RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            : RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
    }

    public static string GetRootName(string fullPath)
    {
        int slash = fullPath.IndexOf('\\');
        return slash < 0 ? fullPath : fullPath[..slash];
    }

    public static string GetParentPath(string fullPath)
    {
        var trimmed = fullPath.TrimEnd('\\');
        int slash = trimmed.LastIndexOf('\\');
        return slash < 0 ? string.Empty : trimmed[..slash];
    }

    public static string GetLeafName(string fullPath)
    {
        var trimmed = fullPath.TrimEnd('\\');
        int slash = trimmed.LastIndexOf('\\');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    public static string Combine(string parent, string child)
        => string.IsNullOrEmpty(parent) ? child : parent.TrimEnd('\\') + "\\" + child;
}
