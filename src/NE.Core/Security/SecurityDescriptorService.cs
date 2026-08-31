using System.Security.AccessControl;
using System.Security.Principal;
using NEManager.Core.Registry;
using WinConst = NEManager.Native.WinConst;

namespace NEManager.Core.Security;

/// <summary>
/// 安全描述符（ACL）读取与编辑服务 —— 支持文件、目录、注册表项。
/// </summary>
public static class SecurityDescriptorService
{
    // ==================== 模型 ====================

    public enum AceKind { Allow, Deny, Audit, Unknown }

    public sealed class AceEntry
    {
        public AceKind Kind { get; set; }
        public string Trustee { get; set; } = string.Empty;
        public string Sid { get; set; } = string.Empty;
        public uint AccessMask { get; set; }
        public string Rights { get; set; } = string.Empty;

        public string AccessMaskText => $"0x{AccessMask:X8}";

        public bool ContainerInherit { get; set; }
        public bool ObjectInherit { get; set; }
        public bool InheritOnly { get; set; }
        public bool NoPropagate { get; set; }
        public bool IsInherited { get; set; }
        public bool SuccessAudit { get; set; }
        public bool FailureAudit { get; set; }

        public string KindText => Kind switch
        {
            AceKind.Allow => "允许",
            AceKind.Deny => "拒绝",
            AceKind.Audit => "审计",
            _ => "未知"
        };

        public string InheritanceText
        {
            get
            {
                var parts = new List<string>();
                if (ContainerInherit) parts.Add("容器继承");
                if (ObjectInherit) parts.Add("对象继承");
                if (InheritOnly) parts.Add("仅继承");
                if (NoPropagate) parts.Add("不传播");
                if (IsInherited) parts.Add("已继承");
                return parts.Count == 0 ? "仅此对象" : string.Join(", ", parts);
            }
        }
    }

    public sealed class SecurityInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string OwnerSid { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public bool DaclProtected { get; set; }
        public bool SaclPresent { get; set; }
        public string Sddl { get; set; } = string.Empty;
        public List<AceEntry> Dacl { get; set; } = new();
        public List<AceEntry> Sacl { get; set; } = new();
        public string Error { get; set; } = string.Empty;
    }

    // ==================== 读取 ====================

    public static SecurityInfo ReadFileSecurity(string path)
    {
        var info = new SecurityInfo { Path = path };
        try
        {
            // 打开继承后，用 FileInfo/DirectoryInfo 的 ACL 扩展方法读取（.NET 无 File.GetAccessControl 静态方法）
            FileSystemSecurity sec = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access)
                : new FileInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access);

            // 额外请求 SACL 需要 SeSecurityPrivilege
            try
            {
                PrivilegeService.SetPrivilege(WinConst.SE_SECURITY_NAME, true);
                sec = Directory.Exists(path)
                    ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.All)
                    : new FileInfo(path).GetAccessControl(AccessControlSections.All);
            }
            catch
            {
                /* 无 SeSecurityPrivilege 时忽略 SACL */
            }

            var owner = sec.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner != null)
            {
                info.OwnerSid = owner.Value;
                info.Owner = TranslateSid(owner);
            }

            var group = sec.GetGroup(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (group != null) info.Group = TranslateSid(group);

            try { info.Sddl = sec.GetSecurityDescriptorSddlForm(AccessControlSections.All); }
            catch { try { info.Sddl = sec.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access); } catch { } }

            info.DaclProtected = sec.AreAccessRulesProtected;

            foreach (FileSystemAccessRule rule in sec.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                info.Dacl.Add(new AceEntry
                {
                    Kind = rule.AccessControlType == AccessControlType.Allow ? AceKind.Allow : AceKind.Deny,
                    Trustee = TranslateSid(rule.IdentityReference as SecurityIdentifier),
                    Sid = (rule.IdentityReference as SecurityIdentifier)?.Value ?? string.Empty,
                    AccessMask = (uint)rule.FileSystemRights,
                    Rights = DescribeFileRights((uint)rule.FileSystemRights),
                    ContainerInherit = (rule.InheritanceFlags & InheritanceFlags.ContainerInherit) != 0,
                    ObjectInherit = (rule.InheritanceFlags & InheritanceFlags.ObjectInherit) != 0,
                    InheritOnly = (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0,
                    NoPropagate = (rule.PropagationFlags & PropagationFlags.NoPropagateInherit) != 0,
                    IsInherited = rule.IsInherited
                });
            }

            try
            {
                foreach (FileSystemAuditRule rule in sec.GetAuditRules(true, true, typeof(SecurityIdentifier)))
                {
                    info.Sacl.Add(new AceEntry
                    {
                        Kind = AceKind.Audit,
                        Trustee = TranslateSid(rule.IdentityReference as SecurityIdentifier),
                        Sid = (rule.IdentityReference as SecurityIdentifier)?.Value ?? string.Empty,
                        AccessMask = (uint)rule.FileSystemRights,
                        Rights = DescribeFileRights((uint)rule.FileSystemRights),
                        ContainerInherit = (rule.InheritanceFlags & InheritanceFlags.ContainerInherit) != 0,
                        ObjectInherit = (rule.InheritanceFlags & InheritanceFlags.ObjectInherit) != 0,
                        SuccessAudit = (rule.AuditFlags & AuditFlags.Success) != 0,
                        FailureAudit = (rule.AuditFlags & AuditFlags.Failure) != 0
                    });
                }
                info.SaclPresent = info.Sacl.Count > 0;
            }
            catch { info.SaclPresent = false; }
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    public static SecurityInfo ReadRegistrySecurity(string keyPath)
    {
        var info = new SecurityInfo { Path = keyPath };
        try
        {
            using var baseKey = RegistryPath.ToBaseKey(keyPath, out string subPath, writable: false);
            using var key = baseKey.OpenSubKey(subPath, false);
            if (key == null)
            {
                info.Error = "无法打开注册表项。";
                return info;
            }

            var sec = key.GetAccessControl();
            var owner = sec.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner != null)
            {
                info.OwnerSid = owner.Value;
                info.Owner = TranslateSid(owner);
            }
            info.Sddl = sec.GetSecurityDescriptorSddlForm(AccessControlSections.All);

            foreach (RegistryAccessRule rule in sec.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                info.Dacl.Add(new AceEntry
                {
                    Kind = rule.AccessControlType == AccessControlType.Allow ? AceKind.Allow : AceKind.Deny,
                    Trustee = TranslateSid(rule.IdentityReference as SecurityIdentifier),
                    Sid = (rule.IdentityReference as SecurityIdentifier)?.Value ?? string.Empty,
                    AccessMask = (uint)rule.RegistryRights,
                    Rights = DescribeRegistryRights((uint)rule.RegistryRights),
                    ContainerInherit = (rule.InheritanceFlags & InheritanceFlags.ContainerInherit) != 0,
                    IsInherited = rule.IsInherited
                });
            }
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    public static string ReadFileSddl(string path)
    {
        try
        {
            FileSystemSecurity sec = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access)
                : new FileInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access);
            return sec.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access);
        }
        catch
        {
            return string.Empty;
        }
    }

    // ==================== 写入 ====================

    public static string? SetFileSddl(string path, string sddl)
    {
        try
        {
            PrivilegeService.SetPrivilege(WinConst.SE_RESTORE_NAME, true);
            PrivilegeService.SetPrivilege(WinConst.SE_TAKE_OWNERSHIP_NAME, true);

            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                var sec = dirInfo.GetAccessControl(AccessControlSections.All);
                sec.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.All);
                dirInfo.SetAccessControl((DirectorySecurity)sec);
            }
            else
            {
                var fileInfo = new FileInfo(path);
                var sec = fileInfo.GetAccessControl(AccessControlSections.All);
                sec.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.All);
                fileInfo.SetAccessControl((FileSecurity)sec);
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string? SetOwner(string path, string sidOrAccount, bool isDirectory)
    {
        try
        {
            PrivilegeService.SetPrivilege(WinConst.SE_TAKE_OWNERSHIP_NAME, true);
            var account = ResolveIdentity(sidOrAccount);

            if (isDirectory)
            {
                var dirInfo = new DirectoryInfo(path);
                var sec = dirInfo.GetAccessControl();
                sec.SetOwner(account);
                dirInfo.SetAccessControl((DirectorySecurity)sec);
            }
            else
            {
                var fileInfo = new FileInfo(path);
                var sec = fileInfo.GetAccessControl();
                sec.SetOwner(account);
                fileInfo.SetAccessControl((FileSecurity)sec);
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// 批量将一套 SDDL 权限模板应用到目录树。返回失败项。
    /// </summary>
    public static List<string> ApplyTemplateToTree(string rootPath, string sddl)
    {
        var failures = new List<string>();
        var directories = new List<string> { rootPath };
        try
        {
            directories.AddRange(Directory.EnumerateDirectories(rootPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            }));
        }
        catch { /* 枚举失败不影响已有部分 */ }

        foreach (var dir in directories)
        {
            var err = SetFileSddl(dir, sddl);
            if (err != null) failures.Add($"{dir}: {err}");
        }
        return failures;
    }

    // ==================== 工具 ====================

    public static string TranslateSid(SecurityIdentifier? sid)
    {
        if (sid == null) return string.Empty;
        try
        {
            var account = sid.Translate(typeof(NTAccount));
            return account.Value;
        }
        catch
        {
            return WellKnownSidNames.TryGetValue(sid.Value, out var name) ? name : sid.Value;
        }
    }

    public static IdentityReference ResolveIdentity(string sidOrAccount)
    {
        if (sidOrAccount.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
            return new SecurityIdentifier(sidOrAccount);
        return new NTAccount(sidOrAccount);
    }

    public static string DescribeFileRights(uint mask)
    {
        var rights = new List<string>();

        if ((mask & 0x001F01FF) == 0x001F01FF) return "完全控制";
        if ((mask & 0x001200A9) == 0x001200A9 && (mask & ~0x001200A9u) == 0) return "读取和执行";

        if ((mask & 0x80000000) != 0) rights.Add("通用读取");
        if ((mask & 0x40000000) != 0) rights.Add("通用写入");
        if ((mask & 0x20000000) != 0) rights.Add("通用执行");
        if ((mask & 0x10000000) != 0) rights.Add("通用全部");

        if ((mask & 0x0001) != 0) rights.Add("读取数据");
        if ((mask & 0x0002) != 0) rights.Add("写入数据");
        if ((mask & 0x0004) != 0) rights.Add("追加数据");
        if ((mask & 0x0008) != 0) rights.Add("读取扩展属性");
        if ((mask & 0x0010) != 0) rights.Add("写入扩展属性");
        if ((mask & 0x0020) != 0) rights.Add("执行文件");
        if ((mask & 0x0040) != 0) rights.Add("删除子文件夹和文件");
        if ((mask & 0x0080) != 0) rights.Add("读取属性");
        if ((mask & 0x0100) != 0) rights.Add("写入属性");
        if ((mask & 0x10000) != 0) rights.Add("删除");
        if ((mask & 0x20000) != 0) rights.Add("读取权限");
        if ((mask & 0x40000) != 0) rights.Add("更改权限");
        if ((mask & 0x80000) != 0) rights.Add("取得所有权");
        if ((mask & 0x100000) != 0) rights.Add("同步");

        return rights.Count == 0 ? $"0x{mask:X8}" : string.Join(", ", rights);
    }

    public static string DescribeRegistryRights(uint mask)
    {
        var rights = new List<string>();
        if ((mask & 0xF003F) == 0xF003F) return "完全控制";

        if ((mask & 0x0001) != 0) rights.Add("查询值");
        if ((mask & 0x0002) != 0) rights.Add("设置值");
        if ((mask & 0x0004) != 0) rights.Add("创建子项");
        if ((mask & 0x0008) != 0) rights.Add("枚举子项");
        if ((mask & 0x0010) != 0) rights.Add("通知");
        if ((mask & 0x0020) != 0) rights.Add("创建链接");
        if ((mask & 0x0200) != 0) rights.Add("64 位视图");
        if ((mask & 0x0100) != 0) rights.Add("32 位视图");
        if ((mask & 0x10000) != 0) rights.Add("删除");
        if ((mask & 0x20000) != 0) rights.Add("读取权限");
        if ((mask & 0x40000) != 0) rights.Add("更改权限");
        if ((mask & 0x80000) != 0) rights.Add("取得所有权");

        return rights.Count == 0 ? $"0x{mask:X8}" : string.Join(", ", rights);
    }

    private static readonly Dictionary<string, string> WellKnownSidNames = new()
    {
        ["S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"] = @"NT SERVICE\TrustedInstaller",
        ["S-1-5-18"] = "SYSTEM",
        ["S-1-5-19"] = @"NT AUTHORITY\LOCAL SERVICE",
        ["S-1-5-20"] = @"NT AUTHORITY\NETWORK SERVICE",
        ["S-1-5-32-544"] = @"BUILTIN\Administrators",
        ["S-1-5-32-545"] = @"BUILTIN\Users",
        ["S-1-5-32-555"] = @"BUILTIN\Remote Desktop Users",
        ["S-1-1-0"] = "Everyone",
        ["S-1-5-11"] = @"NT AUTHORITY\Authenticated Users",
        ["S-1-5-4"] = @"NT AUTHORITY\INTERACTIVE",
        ["S-1-5-6"] = @"NT AUTHORITY\SERVICE",
        ["S-1-5-32-551"] = @"BUILTIN\Backup Operators",
        ["S-1-5-32-549"] = @"BUILTIN\Server Operators",
        ["S-1-5-32-548"] = @"BUILTIN\Account Operators",
        ["S-1-5-32-550"] = @"BUILTIN\Print Operators",
        ["S-1-5-32-546"] = @"BUILTIN\Guests",
        ["S-1-5-32-547"] = @"BUILTIN\Power Users",
        ["S-1-5-32-556"] = @"BUILTIN\Network Configuration Operators",
        ["S-1-5-32-558"] = @"BUILTIN\Performance Monitor Users",
        ["S-1-5-32-559"] = @"BUILTIN\Performance Log Users",
        ["S-1-5-32-562"] = @"BUILTIN\Distributed COM Users",
        ["S-1-5-32-573"] = @"BUILTIN\Event Log Readers",
        ["S-1-5-32-580"] = @"BUILTIN\Remote Management Users",
        ["S-1-15-2-1"] = @"APPLICATION PACKAGE AUTHORITY\ALL APPLICATION PACKAGES",
        ["S-1-5-21-0"] = "(未知)"
    };
}
