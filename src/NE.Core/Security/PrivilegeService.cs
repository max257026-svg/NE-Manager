using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NEManager.Native;

namespace NEManager.Core.Security;

/// <summary>
/// 当前进程令牌中的一项 Windows 特权。
/// </summary>
public sealed class PrivilegeEntry
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; set; }
    public bool EnabledByDefault { get; init; }
    public bool Removed { get; init; }
    public long Luid { get; init; }

    /// <summary>是否是 NE 管理器重点关注的高危/关键特权。</summary>
    public bool IsCritical =>
        Name is WinConst.SE_TAKE_OWNERSHIP_NAME or WinConst.SE_RESTORE_NAME
            or WinConst.SE_DEBUG_NAME or WinConst.SE_SECURITY_NAME
            or WinConst.SE_TCB_NAME or WinConst.SE_LOAD_DRIVER_NAME;

    public string Description => Name switch
    {
        WinConst.SE_TAKE_OWNERSHIP_NAME => "取得文件或其他对象的所有权（接管系统文件必备）",
        WinConst.SE_RESTORE_NAME => "还原文件和目录（绕过权限检查写入受保护文件）",
        WinConst.SE_BACKUP_NAME => "备份文件和目录（绕过权限检查读取任意文件）",
        WinConst.SE_DEBUG_NAME => "调试程序（打开任意进程、读写内存、查看模块）",
        WinConst.SE_SECURITY_NAME => "管理审核和安全日志（读取/修改 SACL）",
        WinConst.SE_TCB_NAME => "作为操作系统的一部分（等价于 SYSTEM 的核心特权）",
        WinConst.SE_LOAD_DRIVER_NAME => "加载和卸载设备驱动程序",
        WinConst.SE_IMPERSONATE_NAME => "身份验证后模拟客户端",
        WinConst.SE_SYSTEM_ENVIRONMENT_NAME => "修改固件环境变量",
        WinConst.SE_MANAGE_VOLUME_NAME => "执行卷维护任务",
        WinConst.SE_CREATE_SYMBOLIC_LINK_NAME => "创建符号链接",
        WinConst.SE_SHUTDOWN_NAME => "关闭系统",
        WinConst.SE_INCREASE_QUOTA_NAME => "为进程调整内存配额",
        WinConst.SE_SYSTEMTIME_NAME => "更改系统时间",
        WinConst.SE_RELABEL_NAME => "修改对象标签（完整性级别）",
        WinConst.SE_LOCK_MEMORY_NAME => "锁定内存页",
        WinConst.SE_CREATE_TOKEN_NAME => "创建令牌对象",
        WinConst.SE_ASSIGNPRIMARYTOKEN_NAME => "替换进程级令牌",
        WinConst.SE_AUDIT_NAME => "生成安全审核",
        WinConst.SE_PROFILE_SINGLE_PROCESS_NAME => "分析单个进程",
        WinConst.SE_INC_BASE_PRIORITY_NAME => "提高调度优先级",
        WinConst.SE_CREATE_PAGEFILE_NAME => "创建页面文件",
        WinConst.SE_CREATE_GLOBAL_NAME => "创建全局对象",
        WinConst.SE_CREATE_PERMANENT_NAME => "创建永久共享对象",
        WinConst.SE_UNDOCK_NAME => "从扩展坞上取下计算机",
        WinConst.SE_TIME_ZONE_NAME => "更改时区",
        WinConst.SE_INCREASE_WORKING_SET_NAME => "增加进程工作集",
        WinConst.SE_TRUSTED_CREDMAN_ACCESS_NAME => "作为受信任的调用方访问凭据管理器",
        WinConst.SE_DELEGATE_SESSION_USER_IMPERSONATE_NAME => "获取其他用户模拟令牌",
        WinConst.SE_SYNC_AGENT_NAME => "同步目录服务数据",
        WinConst.SE_ENABLE_DELEGATION_NAME => "启用计算机和用户账户的信任委派",
        WinConst.SE_MACHINE_ACCOUNT_NAME => "将工作站添加到域",
        WinConst.SE_REMOTE_SHUTDOWN_NAME => "从远程系统强制关机",
        WinConst.SE_CREATE_SYMBOLIC_LINK_NAME + "x" => "",
        _ => string.Empty
    };
}

/// <summary>
/// Windows 特权 (SePrivilege) 管理服务 —— Windows 世界里的 "Root 能力开关"。
/// </summary>
public static class PrivilegeService
{
    /// <summary>
    /// 枚举当前进程令牌中的全部特权。
    /// </summary>
    public static IReadOnlyList<PrivilegeEntry> Enumerate()
    {
        var list = new List<PrivilegeEntry>();
        if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(),
                WinConst.TOKEN_QUERY | WinConst.TOKEN_ADJUST_PRIVILEGES, out var token))
            return list;

        try
        {
            // 先探测缓冲区大小
            Advapi32.GetTokenInformation(token, WinConst.TokenPrivileges, IntPtr.Zero, 0, out var length);
            if (length <= 0) return list;

            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!Advapi32.GetTokenInformation(token, WinConst.TokenPrivileges, buffer, length, out _))
                    return list;

                int count = Marshal.ReadInt32(buffer);
                // TOKEN_PRIVILEGES = uint Count + LUID_AND_ATTRIBUTES[1]
                // LUID_AND_ATTRIBUTES = LUID(8) + uint(4) = 12 字节
                int baseOffset = IntPtr.Size == 8 ? 8 : 4; // 结构体对齐后数组起始位置

                for (int i = 0; i < count; i++)
                {
                    int offset = baseOffset + i * 12;
                    var luid = Marshal.PtrToStructure<LUID>(buffer + offset);
                    uint attrs = (uint)Marshal.ReadInt32(buffer + offset + 8);

                    string name = LookupName(luid);
                    list.Add(new PrivilegeEntry
                    {
                        Name = name,
                        DisplayName = LookupDisplayName(name),
                        Enabled = (attrs & WinConst.SE_PRIVILEGE_ENABLED) != 0,
                        EnabledByDefault = (attrs & WinConst.SE_PRIVILEGE_ENABLED_BY_DEFAULT) != 0,
                        Removed = (attrs & WinConst.SE_PRIVILEGE_REMOVED) != 0,
                        Luid = luid.Value
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Kernel32.CloseHandle(token);
        }

        return list.OrderByDescending(p => p.IsCritical).ThenBy(p => p.Name).ToList();
    }

    /// <summary>
    /// 启用或禁用指定特权。返回错误信息，成功返回 null。
    /// </summary>
    public static string? SetPrivilege(string privilegeName, bool enable)
    {
        if (!Advapi32.LookupPrivilegeValue(null, privilegeName, out var luid))
            return $"无法定位特权 {privilegeName}：{new Win32Exception(Marshal.GetLastWin32Error()).Message}";

        if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(),
                WinConst.TOKEN_QUERY | WinConst.TOKEN_ADJUST_PRIVILEGES, out var token))
            return $"无法打开进程令牌：{new Win32Exception(Marshal.GetLastWin32Error()).Message}";

        try
        {
            // 手工布局 TOKEN_PRIVILEGES：uint Count + LUID(8) + uint Attributes(4)
            var buffer = Marshal.AllocHGlobal(16);
            try
            {
                Marshal.WriteInt32(buffer, 1);
                Marshal.WriteInt32(buffer, 4, (int)luid.LowPart);
                Marshal.WriteInt32(buffer, 8, luid.HighPart);
                Marshal.WriteInt32(buffer, 12, enable ? (int)WinConst.SE_PRIVILEGE_ENABLED : 0);

                if (!Advapi32.AdjustTokenPrivileges(token, false, buffer, 16, IntPtr.Zero, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    // 1300 表示"并非所有特权都被分配"——部分特权在令牌里根本不存在，属正常情况
                    if (err == 1300)
                        return "当前令牌未持有该特权（通常需要以管理员/系统身份运行）";
                    return new Win32Exception(err).Message;
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Kernel32.CloseHandle(token);
        }
    }

    /// <summary>
    /// 一次性启用 NE 管理器运行所需的关键特权组合。
    /// </summary>
    public static string EnableEssentialPrivileges()
    {
        var errors = new List<string>();
        foreach (var p in new[]
                 {
                     WinConst.SE_BACKUP_NAME, WinConst.SE_RESTORE_NAME,
                     WinConst.SE_TAKE_OWNERSHIP_NAME, WinConst.SE_DEBUG_NAME,
                     WinConst.SE_SECURITY_NAME, WinConst.SE_IMPERSONATE_NAME,
                     WinConst.SE_MANAGE_VOLUME_NAME, WinConst.SE_CREATE_SYMBOLIC_LINK_NAME,
                     WinConst.SE_INCREASE_QUOTA_NAME
                 })
        {
            var err = SetPrivilege(p, true);
            if (err != null && !err.Contains("未持有"))
                errors.Add($"{p}: {err}");
        }
        return errors.Count == 0 ? string.Empty : string.Join("; ", errors);
    }

    public static bool IsPrivilegeEnabled(string privilegeName)
    {
        return Enumerate().Any(p => p.Name == privilegeName && p.Enabled);
    }

    private static string LookupName(LUID luid)
    {
        uint size = 0;
        Advapi32.LookupPrivilegeName(null, in luid, null, ref size);
        if (size == 0) return string.Empty;

        var sb = new System.Text.StringBuilder((int)size + 1);
        return Advapi32.LookupPrivilegeName(null, in luid, sb, ref size) ? sb.ToString() : string.Empty;
    }

    private static string LookupDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        uint size = 0;
        Advapi32.LookupPrivilegeDisplayName(null, name, null, ref size, out _);
        if (size == 0) return name;

        var sb = new System.Text.StringBuilder((int)size + 1);
        return Advapi32.LookupPrivilegeDisplayName(null, name, sb, ref size, out _) ? sb.ToString() : name;
    }

    /// <summary>
    /// 当前进程完整性级别：低/中/高/系统。
    /// </summary>
    public static string GetIntegrityLevel()
    {
        if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(), WinConst.TOKEN_QUERY, out var token))
            return "未知";
        try
        {
            uint size = 0;
            Advapi32.GetTokenInformation(token, WinConst.TokenIntegrityLevel, IntPtr.Zero, 0, out var len);
            if (len <= 0) return "未知";

            var buffer = Marshal.AllocHGlobal(len);
            try
            {
                if (!Advapi32.GetTokenInformation(token, WinConst.TokenIntegrityLevel, buffer, len, out _))
                    return "未知";

                var sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero)
                    return "未知";
                if (!Advapi32.GetLengthSid(sid).Equals(0))
                {
                    // 完整性 RID 位于 SID 的最后一个子授权
                    uint sidLength = Advapi32.GetLengthSid(sid);
                    byte subAuthCount = Marshal.ReadByte(sid, 1);
                    uint rid = (uint)Marshal.ReadInt32(sid, (int)(8 + (subAuthCount - 1) * 4));
                    size = sidLength;
                    return rid switch
                    {
                        WinConst.SECURITY_MANDATORY_UNTRUSTED_RID => "不受信任",
                        WinConst.SECURITY_MANDATORY_LOW_RID => "低",
                        WinConst.SECURITY_MANDATORY_MEDIUM_RID => "中（标准用户）",
                        WinConst.SECURITY_MANDATORY_MEDIUM_PLUS_RID => "中+",
                        WinConst.SECURITY_MANDATORY_HIGH_RID => "高（管理员）",
                        WinConst.SECURITY_MANDATORY_SYSTEM_RID => "系统",
                        WinConst.SECURITY_MANDATORY_PROTECTED_PROCESS_RID => "受保护进程",
                        _ => $"RID 0x{rid:X}"
                    };
                }
                return "未知";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Kernel32.CloseHandle(token);
        }
    }

    /// <summary>进程是否已提升至管理员。</summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>进程完整性级别是否为"高"及以上（已过 UAC）。</summary>
    public static bool IsHighIntegrity()
    {
        if (!Advapi32.OpenProcessToken(Kernel32.GetCurrentProcess(), WinConst.TOKEN_QUERY, out var token))
            return false;
        try
        {
            var elevation = new TOKEN_ELEVATION();
            int size = Marshal.SizeOf<TOKEN_ELEVATION>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                if (!Advapi32.GetTokenInformation(token, WinConst.TokenElevation, ptr, size, out _))
                    return false;
                elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(ptr);
                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        finally
        {
            Kernel32.CloseHandle(token);
        }
    }
}
