using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using NEManager.Native;

namespace NEManager.Core.Security;

/// <summary>
/// TrustedInstaller 权限接管 —— Windows 世界中的 "Root"。
/// 原理：TrustedInstaller 服务的进程持有 NT SERVICE\TrustedInstaller 令牌，
/// 复制该令牌即可以其身份创建进程 / 修改系统文件。
/// </summary>
public static class TrustedInstallerService
{
    private const string TiServiceName = "TrustedInstaller";

    public sealed class TiLaunchResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public int ProcessId { get; init; }
    }

    /// <summary>
    /// 以 TrustedInstaller 令牌启动指定程序。需要管理员权限。
    /// </summary>
    public static TiLaunchResult LaunchAsTrustedInstaller(string? commandLine = null, bool waitForExit = false)
    {
        if (!PrivilegeService.IsElevated())
            return new TiLaunchResult
            {
                Success = false,
                Message = "需要管理员权限：请右键以管理员身份运行 NE 管理器，或点击「提权重启」。"
            };

        // 1. 确保 TrustedInstaller 服务处于运行状态
        var startError = EnsureTiServiceRunning(out int tiPid);
        if (startError != null)
            return new TiLaunchResult { Success = false, Message = startError };

        if (tiPid <= 0)
            return new TiLaunchResult { Success = false, Message = "无法获取 TrustedInstaller 服务进程 PID。" };

        // 2. 打开服务进程并复制其令牌
        var process = Kernel32.OpenProcess(
            WinConst.PROCESS_QUERY_LIMITED_INFORMATION | WinConst.PROCESS_DUP_HANDLE, false, (uint)tiPid);
        if (process == IntPtr.Zero)
        {
            return new TiLaunchResult
            {
                Success = false,
                Message = $"无法打开 TrustedInstaller 进程 (PID {tiPid})：{LastError()}"
            };
        }

        try
        {
            if (!Advapi32.OpenProcessToken(process, WinConst.TOKEN_DUPLICATE | WinConst.TOKEN_QUERY, out var tiToken)
                || tiToken == IntPtr.Zero)
            {
                return new TiLaunchResult { Success = false, Message = $"无法复制服务令牌：{LastError()}" };
            }

            try
            {
                // 3. 复制为主令牌
                if (!Advapi32.DuplicateTokenEx(
                        tiToken,
                        WinConst.TOKEN_ALL_ACCESS,
                        IntPtr.Zero,
                        WinConst.SecurityImpersonation,
                        WinConst.TokenPrimary,
                        out var primaryToken))
                {
                    return new TiLaunchResult { Success = false, Message = $"DuplicateTokenEx 失败：{LastError()}" };
                }

                try
                {
                    // 4. 使用该令牌创建进程
                    var cmd = string.IsNullOrWhiteSpace(commandLine)
                        ? new StringBuilder($"\"{Environment.ProcessPath}\"")
                        : new StringBuilder(commandLine);

                    var si = new Kernel32.STARTUPINFO { cb = (uint)Marshal.SizeOf<Kernel32.STARTUPINFO>() };

                    bool ok = Advapi32.CreateProcessWithTokenW(
                        primaryToken, 0, null, cmd, 0, IntPtr.Zero, null, ref si, out var pi);

                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        // 1314 = ERROR_PRIVILEGE_NOT_HELD：缺少 SeAssignPrimaryToken / SeImpersonate
                        if (err == 1314)
                        {
                            PrivilegeService.SetPrivilege(WinConst.SE_IMPERSONATE_NAME, true);
                            PrivilegeService.SetPrivilege(WinConst.SE_ASSIGNPRIMARYTOKEN_NAME, true);
                            ok = Advapi32.CreateProcessWithTokenW(
                                primaryToken, 0, null, cmd, 0, IntPtr.Zero, null, ref si, out pi);
                            err = ok ? 0 : Marshal.GetLastWin32Error();
                        }
                        if (!ok)
                        {
                            return new TiLaunchResult
                            {
                                Success = false,
                                Message = $"以 TrustedInstaller 身份创建进程失败：{new Win32Exception(err).Message}"
                            };
                        }
                    }

                    Kernel32.CloseHandle(pi.hThread);
                    if (waitForExit)
                    {
                        Kernel32.CloseHandle(pi.hProcess);
                        return new TiLaunchResult
                        {
                            Success = true,
                            Message = "已启动。"
                        };
                    }
                    Kernel32.CloseHandle(pi.hProcess);

                    return new TiLaunchResult
                    {
                        Success = true,
                        Message = $"已以 NT SERVICE\\TrustedInstaller 身份启动：{cmd}",
                        ProcessId = (int)pi.dwProcessId
                    };
                }
                finally
                {
                    Kernel32.CloseHandle(primaryToken);
                }
            }
            finally
            {
                Kernel32.CloseHandle(tiToken);
            }
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    /// <summary>
    /// 确保 TrustedInstaller 服务正在运行，并返回其进程 PID。
    /// </summary>
    private static string? EnsureTiServiceRunning(out int pid)
    {
        pid = 0;
        var scm = Advapi32.OpenSCManager(null, null,
            WinConst.SC_MANAGER_CONNECT | WinConst.SC_MANAGER_ENUMERATE_SERVICE);
        if (scm == IntPtr.Zero)
            return $"无法打开服务控制管理器：{LastError()}";

        try
        {
            var service = Advapi32.OpenService(scm, TiServiceName,
                WinConst.SERVICE_QUERY_STATUS | WinConst.SERVICE_START);
            if (service == IntPtr.Zero)
                return $"找不到 TrustedInstaller 服务：{LastError()}";

            try
            {
                // 查询当前状态
                QueryPid(service, out pid);
                if (pid > 0) return null;

                // 启动服务
                if (!Advapi32.StartService(service, 0, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    // 1056 = 服务已在运行
                    if (err != 1056)
                        return $"启动 TrustedInstaller 服务失败：{new Win32Exception(err).Message}";
                }

                // 轮询等待服务进程出现（TrustedInstaller 空闲时会自动退出，轮询要快）
                for (int i = 0; i < 40; i++)
                {
                    System.Threading.Thread.Sleep(50);
                    QueryPid(service, out pid);
                    if (pid > 0) return null;
                }
                return "TrustedInstaller 服务已启动，但未能捕获其进程（服务可能立刻退出）。可稍后重试。";
            }
            finally
            {
                Advapi32.CloseServiceHandle(service);
            }
        }
        finally
        {
            Advapi32.CloseServiceHandle(scm);
        }
    }

    private static void QueryPid(IntPtr service, out int pid)
    {
        pid = 0;
        int size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (Advapi32.QueryServiceStatusEx(service, 0, buffer, (uint)size, out _))
            {
                var status = Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buffer);
                pid = (int)status.dwProcessId;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// 接管文件/目录的所有者并授予 Administrators 完全控制。
    /// 这是绕过 "拒绝访问" 的标准工作流第一步。
    /// </summary>
    public static string? TakeOwnership(string path, bool isRegistryKey = false, IntPtr registryKey = 0)
    {
        var type = isRegistryKey ? SE_OBJECT_TYPE.SE_REGISTRY_KEY : SE_OBJECT_TYPE.SE_FILE_OBJECT;

        // 启用取得所有权特权
        PrivilegeService.SetPrivilege(WinConst.SE_TAKE_OWNERSHIP_NAME, true);

        var sid = GetAdministratorsSid();

        uint result;
        if (isRegistryKey)
        {
            result = Advapi32.SetNamedSecurityInfo(
                path, type, WinConst.OWNER_SECURITY_INFORMATION, sid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }
        else
        {
            result = Advapi32.SetNamedSecurityInfo(
                path, type, WinConst.OWNER_SECURITY_INFORMATION, sid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        if (result != WinConst.ERROR_SUCCESS)
        {
            // 非提权进程拿不到该特权时，退化为提示
            return $"接管所有者失败 (0x{result:X8})：{new Win32Exception((int)result).Message}";
        }

        // 再授予 Administrators 完全控制，否则仅改所有者仍然进不去
        var grantError = GrantFullControl(path, "Administrators");
        return grantError;
    }

    /// <summary>
    /// 将所有者还原为 TrustedInstaller（系统文件改完之后收尾用）。
    /// </summary>
    public static string? RestoreToTrustedInstaller(string path)
    {
        if (!Advapi32.ConvertStringSidToSid(WinConst.TRUSTED_INSTALLER_SID, out var tiSid))
            return "无法解析 TrustedInstaller SID。";

        try
        {
            PrivilegeService.SetPrivilege(WinConst.SE_RESTORE_NAME, true);
            PrivilegeService.SetPrivilege(WinConst.SE_TAKE_OWNERSHIP_NAME, true);

            uint result = Advapi32.SetNamedSecurityInfo(
                path, SE_OBJECT_TYPE.SE_FILE_OBJECT,
                WinConst.OWNER_SECURITY_INFORMATION, tiSid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            return result == WinConst.ERROR_SUCCESS
                ? null
                : $"还原所有者失败 (0x{result:X8})：{new Win32Exception((int)result).Message}";
        }
        finally
        {
            if (tiSid != IntPtr.Zero) Marshal.FreeHGlobal(tiSid);
        }
    }

    /// <summary>
    /// 授予指定账户对目标的完全控制权限（在现有 DACL 上追加）。
    /// </summary>
    public static string? GrantFullControl(string path, string accountName)
    {
        // 读取现有 DACL
        uint ret = Advapi32.GetNamedSecurityInfo(
            path, SE_OBJECT_TYPE.SE_FILE_OBJECT,
            WinConst.DACL_SECURITY_INFORMATION,
            out _, out _, out var oldDacl, out _, out var sd);
        if (ret != WinConst.ERROR_SUCCESS)
            return $"读取安全描述符失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}";

        try
        {
            var access = new EXPLICIT_ACCESS
            {
                grfAccessPermissions = WinConst.GENERIC_ALL,
                grfAccessMode = (uint)ACCESS_MODE.GRANT_ACCESS,
                grfInheritance = WinConst.OBJECT_INHERIT_ACE | WinConst.CONTAINER_INHERIT_ACE,
                Trustee = new TRUSTEE
                {
                    TrusteeForm = (int)TRUSTEE_FORM.TRUSTEE_IS_NAME,
                    TrusteeType = (int)TRUSTEE_TYPE.TRUSTEE_IS_GROUP,
                    ptstrName = accountName
                }
            };

            int eaSize = Marshal.SizeOf<EXPLICIT_ACCESS>();
            var eaPtr = Marshal.AllocHGlobal(eaSize);
            try
            {
                Marshal.StructureToPtr(access, eaPtr, false);
                ret = Advapi32.SetEntriesInAcl(1, eaPtr, oldDacl, out var newDacl);
                if (ret != WinConst.ERROR_SUCCESS)
                    return $"构造新 DACL 失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}";

                try
                {
                    ret = Advapi32.SetNamedSecurityInfo(
                        path, SE_OBJECT_TYPE.SE_FILE_OBJECT,
                        WinConst.DACL_SECURITY_INFORMATION,
                        IntPtr.Zero, IntPtr.Zero, newDacl, IntPtr.Zero);
                    return ret == WinConst.ERROR_SUCCESS
                        ? null
                        : $"写入 DACL 失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}";
                }
                finally
                {
                    if (newDacl != IntPtr.Zero) Marshal.FreeHGlobal(newDacl);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(eaPtr);
            }
        }
        finally
        {
            if (sd != IntPtr.Zero) Marshal.FreeHGlobal(sd);
        }
    }

    /// <summary>
    /// 重置为继承父对象的默认权限（修复损坏的系统目录权限）。
    /// </summary>
    public static string? ResetInheritance(string path)
    {
        PrivilegeService.SetPrivilege(WinConst.SE_TAKE_OWNERSHIP_NAME, true);
        PrivilegeService.SetPrivilege(WinConst.SE_RESTORE_NAME, true);

        // 传入 NULL DACL + UNPROTECTED 标志 = 打开继承、清空显式 ACE
        uint ret = Advapi32.SetNamedSecurityInfo(
            path, SE_OBJECT_TYPE.SE_FILE_OBJECT,
            WinConst.DACL_SECURITY_INFORMATION | WinConst.UNPROTECTED_DACL_SECURITY_INFORMATION,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        return ret == WinConst.ERROR_SUCCESS
            ? null
            : $"重置继承权限失败 (0x{ret:X8})：{new Win32Exception((int)ret).Message}";
    }

    /// <summary>
    /// 以管理员身份重启本程序（UAC 提权入口）。
    /// </summary>
    public static bool RestartAsAdministrator(string? arguments = null)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = arguments ?? string.Join(' ', Environment.GetCommandLineArgs().Skip(1))
            };
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception)
        {
            return false; // 用户取消 UAC
        }
    }

    internal static IntPtr GetAdministratorsSid()
    {
        // S-1-5-32-544 = BUILTIN\Administrators
        Advapi32.ConvertStringSidToSid("S-1-5-32-544", out var sid);
        return sid;
    }

    private static string LastError() => new Win32Exception(Marshal.GetLastWin32Error()).Message;
}
