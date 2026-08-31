using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NEManager.Core.Security;
using NEManager.Native;
using ThreadState = System.Diagnostics.ThreadState;

namespace NEManager.Core.SystemTools;

/// <summary>
/// 进程与模块工具集 —— 进程树终结、挂起/恢复、模块枚举、文件占用检测。
/// </summary>
public static class ProcessManager
{
    public sealed class ProcessItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int ParentId { get; set; }
        public int ThreadCount { get; set; }
        public long WorkingSet { get; set; }
        public int HandleCount { get; set; }
        public bool IsElevated { get; set; }
        public bool IsCritical { get; set; }
        public bool IsWow64 { get; set; }
        public bool IsSuspended { get; set; }
        public DateTime StartTime { get; set; }

        public string WorkingSetText => FileSystem.FileItem.FormatSize(WorkingSet);

        public string StatusText => IsSuspended ? "已挂起" : "运行中";

        public string ElevatedText => IsElevated ? "是" : "否";
    }

    public sealed class ModuleItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public IntPtr BaseAddress { get; set; }
        public uint Size { get; set; }

        public string BaseText => $"0x{BaseAddress.ToInt64():X}";
        public string SizeText => FileSystem.FileItem.FormatSize(Size);
    }

    /// <summary>
    /// 是否属于系统关键进程（结束会导致系统崩溃/注销）。
    /// </summary>
    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "System Idle Process", "smss.exe", "csrss.exe", "wininit.exe",
        "winlogon.exe", "services.exe", "lsass.exe", "lsaiso.exe", "fontdrvhost.exe",
        "dwm.exe", "sihost.exe", "Registry", "Memory Compression"
    };

    /// <summary>
    /// 枚举全部进程。启用 SeDebugPrivilege 后可获取更多信息。
    /// </summary>
    public static List<ProcessItem> Enumerate()
    {
        PrivilegeService.SetPrivilege(WinConst.SE_DEBUG_NAME, true);

        var result = new List<ProcessItem>();
        var snapshot = Kernel32.CreateToolhelp32Snapshot(WinConst.TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return result;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Kernel32.Process32First(snapshot, ref entry)) return result;

            do
            {
                var item = new ProcessItem
                {
                    Id = (int)entry.th32ProcessID,
                    Name = entry.szExeFile,
                    ParentId = (int)entry.th32ParentProcessID,
                    ThreadCount = (int)entry.cntThreads,
                    IsCritical = CriticalProcesses.Contains(entry.szExeFile)
                };

                Enrich(item);
                result.Add(item);
            }
            while (Kernel32.Process32Next(snapshot, ref entry));
        }
        finally
        {
            Kernel32.CloseHandle(snapshot);
        }

        return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Enrich(ProcessItem item)
    {
        try
        {
            using var proc = Process.GetProcessById(item.Id);
            try { item.Path = proc.MainModule?.FileName ?? string.Empty; } catch { }
            try { item.WorkingSet = proc.WorkingSet64; } catch { }
            try { item.HandleCount = proc.HandleCount; } catch { }
            try { item.StartTime = proc.StartTime; } catch { }
        }
        catch { /* 进程已退出 */ }

        if (item.Id == 0 || item.Id == 4)
        {
            item.UserName = "SYSTEM";
            item.IsElevated = true;
            return;
        }

        // 注意：枚举阶段【不】调用 WMI（每个进程一次 WMI 会拖垮 UI 线程）。
        // 用户名用令牌快速解析；命令行等重信息改为选中进程时按需获取（见 GetProcessDetails）。
        item.UserName = GetProcessUserFast(item.Id);
        item.IsElevated = IsProcessElevated(item.Id);
        item.IsSuspended = IsProcessSuspended(item.Id);
    }

    /// <summary>
    /// 基于进程令牌快速解析用户名（不走 WMI），用于进程列表枚举，避免逐个进程 WMI 查询卡死 UI。
    /// </summary>
    public static string GetProcessUserFast(int pid)
    {
        if (pid == 0 || pid == 4) return "SYSTEM";

        var handle = Kernel32.OpenProcess(WinConst.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (handle == IntPtr.Zero) return string.Empty;
        try
        {
            if (!Advapi32.OpenProcessToken(handle, WinConst.TOKEN_QUERY, out var token))
                return string.Empty;
            try
            {
                int size = 0;
                Advapi32.GetTokenInformation(token, WinConst.TokenUser, IntPtr.Zero, 0, out size);
                if (size <= 0) return string.Empty;

                var ptr = Marshal.AllocHGlobal(size);
                try
                {
                    if (!Advapi32.GetTokenInformation(token, WinConst.TokenUser, ptr, size, out _))
                        return string.Empty;

                    var tu = Marshal.PtrToStructure<TOKEN_USER>(ptr);
                    if (tu.User.Sid == IntPtr.Zero) return string.Empty;

                    var sid = new System.Security.Principal.SecurityIdentifier(tu.User.Sid);
                    return sid.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
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
        catch
        {
            return string.Empty;
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 选中进程时按需获取「命令行 + 用户名」等较重信息（命令行只能走 WMI）。
    /// 调用方应在后台线程执行，避免阻塞 UI。
    /// </summary>
    public static (string CommandLine, string UserName) GetProcessDetails(int pid)
    {
        string commandLine = string.Empty;
        string userName = GetProcessUserFast(pid);

        try
        {
            var query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}";
            foreach (var mo in WmiService.Query(query))
                commandLine = mo["CommandLine"]?.ToString() ?? string.Empty;
        }
        catch { }

        return (commandLine, userName);
    }

    public static string GetProcessUser(int pid)
    {
        try
        {
            var query = $"SELECT * FROM Win32_Process WHERE ProcessId = {pid}";
            foreach (var mo in WmiService.Query(query))
            {
                var owner = new string[2];
                var ret = mo.InvokeMethod("GetOwner", owner);
                if (ret is 0 or 0u)
                    return string.IsNullOrEmpty(owner[1]) ? owner[0] : $"{owner[1]}\\{owner[0]}";
            }
        }
        catch { }
        return string.Empty;
    }

    public static bool IsProcessElevated(int pid)
    {
        var handle = Kernel32.OpenProcess(WinConst.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (handle == IntPtr.Zero) return false;
        try
        {
            if (!Advapi32.OpenProcessToken(handle, WinConst.TOKEN_QUERY, out var token))
                return false;
            try
            {
                int size = Marshal.SizeOf<TOKEN_ELEVATION>();
                var ptr = Marshal.AllocHGlobal(size);
                try
                {
                    if (!Advapi32.GetTokenInformation(token, WinConst.TokenElevation, ptr, size, out _))
                        return false;
                    return Marshal.PtrToStructure<TOKEN_ELEVATION>(ptr).TokenIsElevated != 0;
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
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    public static bool IsProcessSuspended(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            foreach (ProcessThread thread in proc.Threads)
            {
                if (thread.ThreadState == ThreadState.Wait &&
                    thread.WaitReason == ThreadWaitReason.Suspended)
                    return true;
            }
        }
        catch { }
        return false;
    }

    // ==================== 操作 ====================

    public static string? Terminate(int pid, bool force = true)
    {
        var handle = Kernel32.OpenProcess(WinConst.PROCESS_TERMINATE | WinConst.PROCESS_QUERY_LIMITED_INFORMATION,
            false, (uint)pid);
        if (handle == IntPtr.Zero)
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;

        try
        {
            if (!Kernel32.TerminateProcess(handle, force ? 1u : 0u))
                return new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return null;
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 结束进程及其所有子进程。
    /// </summary>
    public static List<string> TerminateTree(int rootPid)
    {
        var errors = new List<string>();
        var processes = Enumerate();
        var children = new Dictionary<int, List<int>>();

        foreach (var p in processes)
        {
            if (!children.TryGetValue(p.ParentId, out var list))
                children[p.ParentId] = list = new List<int>();
            list.Add(p.Id);
        }

        void KillRecursive(int pid)
        {
            if (children.TryGetValue(pid, out var kids))
                foreach (var kid in kids)
                    KillRecursive(kid);

            if (pid == Environment.ProcessId) return;  // 别把自己干掉

            var err = Terminate(pid);
            if (err != null) errors.Add($"PID {pid}: {err}");
        }

        KillRecursive(rootPid);
        return errors;
    }

    public static string? Suspend(int pid)
    {
        var handle = Kernel32.OpenProcess(WinConst.PROCESS_SUSPEND_RESUME, false, (uint)pid);
        if (handle == IntPtr.Zero)
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        try
        {
            uint status = NtDll.NtSuspendProcess(handle);
            return status == 0 ? null : $"NtSuspendProcess 失败 (0x{status:X8})";
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    public static string? Resume(int pid)
    {
        var handle = Kernel32.OpenProcess(WinConst.PROCESS_SUSPEND_RESUME, false, (uint)pid);
        if (handle == IntPtr.Zero)
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        try
        {
            uint status = NtDll.NtResumeProcess(handle);
            return status == 0 ? null : $"NtResumeProcess 失败 (0x{status:X8})";
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 枚举进程加载的模块（DLL）。
    /// </summary>
    public static List<ModuleItem> EnumerateModules(int pid)
    {
        PrivilegeService.SetPrivilege(WinConst.SE_DEBUG_NAME, true);
        var list = new List<ModuleItem>();

        var snapshot = Kernel32.CreateToolhelp32Snapshot(
            WinConst.TH32CS_SNAPMODULE | WinConst.TH32CS_SNAPMODULE32, (uint)pid);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return list;

        try
        {
            var entry = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (!Kernel32.Module32First(snapshot, ref entry)) return list;

            do
            {
                list.Add(new ModuleItem
                {
                    Name = entry.szModule,
                    Path = entry.szExePath,
                    BaseAddress = entry.modBaseAddr,
                    Size = entry.modBaseSize
                });
            }
            while (Kernel32.Module32Next(snapshot, ref entry));
        }
        finally
        {
            Kernel32.CloseHandle(snapshot);
        }

        return list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 查找正在占用指定文件的进程 —— 解决"文件正在使用中"。
    /// </summary>
    public static List<ProcessItem> FindLockingProcesses(string filePath)
    {
        var lockers = new List<ProcessItem>();

        uint session;
        var key = new StringBuilder(Rstrtmgr.CCH_RM_SESSION_KEY + 1);
        if (Rstrtmgr.RmStartSession(out session, 0, key) != 0)
            return lockers;

        try
        {
            var files = new[] { filePath };
            if (Rstrtmgr.RmRegisterResources(session, 1, files, 0, IntPtr.Zero, 0, IntPtr.Zero) != 0)
                return lockers;

            uint procInfoNeeded = 0;
            uint procInfo = 0;
            uint rebootReasons = Rstrtmgr.RmRebootReasonNone;

            uint result = Rstrtmgr.RmGetList(session, out procInfoNeeded, ref procInfo, null!, ref rebootReasons);
            if (result == Rstrtmgr.ERROR_MORE_DATA || result == 0)
            {
                procInfo = procInfoNeeded;
                var infos = new Rstrtmgr.RM_PROCESS_INFO[procInfo];
                result = Rstrtmgr.RmGetList(session, out procInfoNeeded, ref procInfo, infos, ref rebootReasons);

                if (result == 0 || result == Rstrtmgr.ERROR_MORE_DATA)
                {
                    for (int i = 0; i < Math.Min(procInfo, (uint)infos.Length); i++)
                    {
                        var pid = (int)infos[i].Process.dwProcessId;
                        var item = new ProcessItem
                        {
                            Id = pid,
                            Name = infos[i].strAppName,
                            UserName = GetProcessUser(pid),
                            IsElevated = IsProcessElevated(pid)
                        };
                        try
                        {
                            using var p = Process.GetProcessById(pid);
                            item.Path = p.MainModule?.FileName ?? string.Empty;
                        }
                        catch { }
                        lockers.Add(item);
                    }
                }
            }
        }
        finally
        {
            Rstrtmgr.RmEndSession(session);
        }

        return lockers;
    }

    /// <summary>
    /// 转储进程内存到文件。
    /// </summary>
    public static string? DumpProcessMemory(int pid, string outputPath)
    {
        PrivilegeService.SetPrivilege(WinConst.SE_DEBUG_NAME, true);
        var handle = Kernel32.OpenProcess(
            WinConst.PROCESS_VM_READ | WinConst.PROCESS_QUERY_INFORMATION, false, (uint)pid);

        if (handle == IntPtr.Zero)
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;

        try
        {
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            var buffer = Marshal.AllocHGlobal(1024 * 1024);
            try
            {
                // 从低地址扫描可读区域（简化实现：顺序读取可访问页）
                long address = 0x10000;
                long written = 0;
                const long maxSize = 512L * 1024 * 1024; // 上限 512MB

                while (written < maxSize && address < 0x7FFFFFFFFFFF)
                {
                    if (Kernel32.ReadProcessMemory(handle, new IntPtr(address), buffer,
                            (nuint)(1024 * 1024), out nuint bytesRead) && bytesRead > 0)
                    {
                        var chunk = new byte[(int)bytesRead];
                        Marshal.Copy(buffer, chunk, 0, (int)bytesRead);
                        fs.Write(chunk);
                        written += (long)bytesRead;
                        address += (long)bytesRead;
                    }
                    else
                    {
                        address += 4096; // 跳到下一页
                    }
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            Kernel32.CloseHandle(handle);
        }
    }
}
