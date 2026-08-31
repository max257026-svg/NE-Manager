using System.Text;
using Microsoft.Win32;
using NEManager.Core.Security;
using NEManager.Native;

namespace NEManager.Core.Registry;

/// <summary>
/// 注册表完整编辑服务 —— 全部值类型、批量搜索、离线 hive、权限接管。
/// </summary>
public static class RegistryService
{
    // ==================== 模型 ====================

    public sealed class RegistryValueItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrEmpty(Name) ? "(默认)" : Name;
        public RegistryValueKind Kind { get; set; }
        public object? Data { get; set; }

        public string KindText => Kind switch
        {
            RegistryValueKind.String => "REG_SZ",
            RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
            RegistryValueKind.Binary => "REG_BINARY",
            RegistryValueKind.DWord => "REG_DWORD",
            RegistryValueKind.QWord => "REG_QWORD",
            RegistryValueKind.MultiString => "REG_MULTI_SZ",
            RegistryValueKind.None => "REG_NONE",
            RegistryValueKind.Unknown => "REG_UNKNOWN",
            _ => Kind.ToString()
        };

        public string DisplayText
        {
            get
            {
                try
                {
                    return Data switch
                    {
                        null => "(值未设置)",
                        byte[] bytes => $"({bytes.Length} 字节) {Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 32)))}{(bytes.Length > 32 ? "…" : "")}",
                        string[] multi => string.Join("  ⏎  ", multi),
                        uint dword => $"0x{dword:X8} ({dword})",
                        ulong qword => $"0x{qword:X16} ({qword})",
                        _ => Data.ToString() ?? string.Empty
                    };
                }
                catch
                {
                    return "(无法读取)";
                }
            }
        }
    }

    public sealed class RegistryKeyItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public int SubKeyCount { get; set; }
        public int ValueCount { get; set; }
        public DateTime LastWriteTime { get; set; }
    }

    public sealed class SearchHit
    {
        public string KeyPath { get; set; } = string.Empty;
        public string? ValueName { get; set; }
        public string MatchedText { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    // ==================== 枚举 ====================

    public static List<RegistryKeyItem> EnumerateSubKeys(string parentPath)
    {
        var result = new List<RegistryKeyItem>();
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(parentPath, out string subPath, writable: false);
            using var key = baseKey.OpenSubKey(subPath, false);
            if (key == null) return result;

            foreach (var name in key.GetSubKeyNames())
            {
                var full = RegistryPath.Combine(parentPath, name);
                int subCount = 0, valCount = 0;
                try
                {
                    using var sub = key.OpenSubKey(name, false);
                    if (sub != null)
                    {
                        subCount = sub.SubKeyCount;
                        valCount = sub.ValueCount;
                    }
                }
                catch { /* 无权限时仅显示名称 */ }

                result.Add(new RegistryKeyItem
                {
                    Name = name,
                    FullPath = full,
                    SubKeyCount = subCount,
                    ValueCount = valCount
                });
            }
        }
        catch { /* 无访问权限 */ }
        return result.OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<RegistryValueItem> EnumerateValues(string keyPath)
    {
        var result = new List<RegistryValueItem>();
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(keyPath, out string subPath, writable: false);
            using var key = baseKey.OpenSubKey(subPath, false);
            if (key == null) return result;

            foreach (var name in key.GetValueNames())
            {
                var kind = key.GetValueKind(name);
                object? data = kind switch
                {
                    RegistryValueKind.Binary => key.GetValue(name) as byte[],
                    RegistryValueKind.MultiString => key.GetValue(name) as string[],
                    RegistryValueKind.DWord => key.GetValue(name),
                    RegistryValueKind.QWord => key.GetValue(name),
                    _ => key.GetValue(name)
                };

                result.Add(new RegistryValueItem { Name = name, Kind = kind, Data = data });
            }

            // 某些键存在未命名默认值但 GetValueNames 不返回
            if (key.GetValue(string.Empty) != null && !result.Any(v => v.Name.Length == 0))
            {
                result.Insert(0, new RegistryValueItem
                {
                    Name = string.Empty,
                    Kind = key.GetValueKind(string.Empty),
                    Data = key.GetValue(string.Empty)
                });
            }
        }
        catch { /* 无访问权限 */ }
        return result.OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ==================== 增改删 ====================

    public static string? CreateKey(string parentPath, string name)
    {
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(parentPath, out string subPath, writable: true);
            using var parent = baseKey.OpenSubKey(subPath, true);
            if (parent == null) return "无法以写入方式打开父项（权限不足？）。";
            parent.CreateSubKey(name);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? DeleteKey(string keyPath)
    {
        try
        {
            var parentPath = RegistryPath.GetParentPath(keyPath);
            var leaf = RegistryPath.GetLeafName(keyPath);
            if (string.IsNullOrEmpty(parentPath)) return "不能删除根键。";

            using var parentBase = RegistryPath.ToBaseKey(parentPath, out string parentSub, writable: true);
            using var p = parentBase.OpenSubKey(parentSub, true) ?? parentBase.CreateSubKey(parentSub);
            if (p == null) return "无法以写入方式打开父项（权限不足？）。";

            p.DeleteSubKeyTree(leaf, false);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? RenameKey(string keyPath, string newName)
    {
        var err = CreateKey(RegistryPath.GetParentPath(keyPath), newName);
        if (err != null) return err;

        var copyErr = CopyKeyRecursive(keyPath, RegistryPath.Combine(RegistryPath.GetParentPath(keyPath), newName));
        if (copyErr != null) return copyErr;

        return DeleteKey(keyPath);
    }

    public static string? SetValue(string keyPath, string valueName, object data, RegistryValueKind kind)
    {
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(keyPath, out string subPath, writable: true);
            using var key = baseKey.OpenSubKey(subPath, true) ?? baseKey.CreateSubKey(subPath);
            key.SetValue(valueName, data, kind);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? DeleteValue(string keyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(keyPath, out string subPath, writable: true);
            using var key = baseKey.OpenSubKey(subPath, true);
            if (key == null) return "无法以写入方式打开注册表项。";
            key.DeleteValue(valueName, false);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? RenameValue(string keyPath, string oldName, string newName)
    {
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(keyPath, out string subPath, writable: true);
            using var key = baseKey.OpenSubKey(subPath, true);
            if (key == null) return "无法以写入方式打开注册表项。";

            var data = key.GetValue(oldName);
            var kind = key.GetValueKind(oldName);
            if (data != null) key.SetValue(newName, data, kind);
            key.DeleteValue(oldName, false);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static string? CopyKeyRecursive(string sourcePath, string destPath)
    {
        try
        {
            var err = CreateKey(RegistryPath.GetParentPath(destPath), RegistryPath.GetLeafName(destPath));
            if (err != null) return err;

            foreach (var v in EnumerateValues(sourcePath))
            {
                if (v.Data == null) continue;
                SetValue(destPath, v.Name, v.Data, v.Kind);
            }
            foreach (var sub in EnumerateSubKeys(sourcePath))
            {
                var e = CopyKeyRecursive(sub.FullPath, RegistryPath.Combine(destPath, sub.Name));
                if (e != null) return e;
            }
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ==================== 搜索 ====================

    public sealed class SearchOptions
    {
        public string Pattern { get; set; } = string.Empty;
        public bool SearchKeyNames { get; set; } = true;
        public bool SearchValueNames { get; set; } = true;
        public bool SearchValueData { get; set; } = true;
        public bool UseRegex { get; set; }
        public bool MatchCase { get; set; }
        public int MaxResults { get; set; } = 500;
    }

    public static List<SearchHit> Search(string rootPath, SearchOptions options)
    {
        var hits = new List<SearchHit>();
        var regexOpts = options.MatchCase
            ? System.Text.RegularExpressions.RegexOptions.None
            : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        System.Text.RegularExpressions.Regex? regex = null;
        if (options.UseRegex)
        {
            try
            {
                regex = new System.Text.RegularExpressions.Regex(
                    options.Pattern, regexOpts | System.Text.RegularExpressions.RegexOptions.Compiled);
            }
            catch
            {
                regex = null; // 非法正则：降级为无匹配，不抛出崩溃
            }
        }

        bool Match(string? text)
        {
            if (text == null) return false;
            if (regex != null) return regex.IsMatch(text);
            return options.MatchCase
                ? text.Contains(options.Pattern, StringComparison.Ordinal)
                : text.Contains(options.Pattern, StringComparison.OrdinalIgnoreCase);
        }

        void Walk(string path, int depth)
        {
            if (hits.Count >= options.MaxResults || depth > 24) return;

            if (options.SearchKeyNames && Match(RegistryPath.GetLeafName(path)))
            {
                hits.Add(new SearchHit { KeyPath = path, MatchedText = RegistryPath.GetLeafName(path), Data = "(键名)" });
                if (hits.Count >= options.MaxResults) return;
            }

            foreach (var v in EnumerateValues(path))
            {
                if (options.SearchValueNames && Match(v.DisplayName))
                    hits.Add(new SearchHit
                    {
                        KeyPath = path,
                        ValueName = v.Name,
                        MatchedText = v.DisplayName,
                        Data = v.DisplayText
                    });

                if (options.SearchValueData)
                {
                    var text = v.Data switch
                    {
                        byte[] b => Convert.ToHexString(b),
                        string[] m => string.Join(" ", m),
                        _ => v.Data?.ToString() ?? string.Empty
                    };
                    if (Match(text))
                        hits.Add(new SearchHit
                        {
                            KeyPath = path,
                            ValueName = v.Name,
                            MatchedText = text.Length > 100 ? text[..100] : text,
                            Data = v.DisplayText
                        });
                }
                if (hits.Count >= options.MaxResults) return;
            }

            foreach (var sub in EnumerateSubKeys(path))
            {
                Walk(sub.FullPath, depth + 1);
                if (hits.Count >= options.MaxResults) return;
            }
        }

        try
        {
            Walk(rootPath, 0);
        }
        catch
        {
            // 深层键访问被拒/损坏：返回已收集的部分结果，不向上抛
        }
        return hits;
    }

    // ==================== 导出 / 导入 ====================

    /// <summary>把指定分支导出为 .reg 格式文本。</summary>
    public static string ExportBranch(string keyPath, bool includeSubKeys = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        AppendKey(keyPath, sb, includeSubKeys, 0);
        return sb.ToString();
    }

    private static void AppendKey(string keyPath, StringBuilder sb, bool includeSubKeys, int depth)
    {
        if (depth > 24) return;
        sb.AppendLine($"[{keyPath}]");

        foreach (var v in EnumerateValues(keyPath))
        {
            sb.AppendLine($"{EscapeValueName(v.Name)}={FormatValue(v)}");
        }
        sb.AppendLine();

        if (!includeSubKeys) return;
        foreach (var sub in EnumerateSubKeys(keyPath))
            AppendKey(sub.FullPath, sb, true, depth + 1);
    }

    private static string EscapeValueName(string name)
    {
        if (name.Length == 0) return "@";
        var escaped = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    private static string FormatValue(RegistryValueItem v)
    {
        return v.Kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString =>
                $"\"{FormatRegString(v.Data?.ToString() ?? string.Empty)}\"",
            RegistryValueKind.DWord =>
                $"dword:{unchecked((uint)Convert.ToInt64(v.Data)):x8}",
            RegistryValueKind.QWord =>
                $"qword:{unchecked((ulong)Convert.ToInt64(v.Data)):x16}",
            RegistryValueKind.MultiString when v.Data is string[] multi =>
                "hex(7):" + string.Join(",", MultiStringToBytes(multi).Select(b => b.ToString("x2"))),
            RegistryValueKind.Binary when v.Data is byte[] bytes =>
                "hex:" + FormatHexLines(bytes),
            _ => $"\"{FormatRegString(v.Data?.ToString() ?? string.Empty)}\""
        };
    }

    private static byte[] MultiStringToBytes(string[] values)
    {
        var bytes = new List<byte>();
        foreach (var s in values)
        {
            bytes.AddRange(Encoding.Unicode.GetBytes(s));
            bytes.Add(0); bytes.Add(0);
        }
        bytes.Add(0); bytes.Add(0);
        return bytes.ToArray();
    }

    private static string FormatHexLines(byte[] bytes)
    {
        var parts = bytes.Select(b => b.ToString("x2"));
        return string.Join(",", parts);
    }

    private static string FormatRegString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");

    // ==================== 离线 Hive ====================

    /// <summary>
    /// 加载离线注册表 hive 文件（例如 D:\Windows\System32\config\SOFTWARE）。
    /// 需要 SeRestorePrivilege / SeBackupPrivilege。
    /// </summary>
    public static string? LoadHive(string hiveFilePath, string mountName)
    {
        PrivilegeServiceEnabled();

        var hklm = new IntPtr(unchecked((int)WinConst.HKEY_LOCAL_MACHINE));
        uint ret = Advapi32.RegLoadKey(hklm, mountName, hiveFilePath);
        if (ret != WinConst.ERROR_SUCCESS)
            return $"加载 hive 失败 (0x{ret:X8})：{new System.ComponentModel.Win32Exception((int)ret).Message}";
        return null;
    }

    public static string? UnloadHive(string mountName)
    {
        PrivilegeServiceEnabled();
        var hklm = new IntPtr(unchecked((int)WinConst.HKEY_LOCAL_MACHINE));
        uint ret = Advapi32.RegUnLoadKey(hklm, mountName);
        if (ret != WinConst.ERROR_SUCCESS)
            return $"卸载 hive 失败 (0x{ret:X8})：{new System.ComponentModel.Win32Exception((int)ret).Message}";
        return null;
    }

    /// <summary>
    /// 以只读方式挂载 hive 文件（RegLoadAppKey 不需要特权，也不污染注册表命名空间）。
    /// </summary>
    public static IntPtr LoadAppKey(string hiveFilePath, out string? error)
    {
        error = null;
        uint ret = Advapi32.RegLoadAppKey(hiveFilePath, out var handle,
            WinConst.KEY_ALL_ACCESS, WinConst.REG_OPTION_NON_VOLATILE, 0);
        if (ret != WinConst.ERROR_SUCCESS)
        {
            error = $"挂载 hive 失败 (0x{ret:X8})：{new System.ComponentModel.Win32Exception((int)ret).Message}";
            return IntPtr.Zero;
        }
        return handle;
    }

    private static void PrivilegeServiceEnabled()
    {
        PrivilegeService.SetPrivilege(WinConst.SE_RESTORE_NAME, true);
        PrivilegeService.SetPrivilege(WinConst.SE_BACKUP_NAME, true);
    }
}
