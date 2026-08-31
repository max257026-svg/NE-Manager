using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using NEManager.Native;

namespace NEManager.Core.SystemTools;

/// <summary>
/// Windows 服务管理器 —— 浏览、启停、改类型、改二进制路径、创建/删除、导出清单。
/// </summary>
public static class ServiceManager
{
    public sealed class ServiceItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
        public string BinaryPath { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string[] Dependencies { get; set; } = Array.Empty<string>();
        public bool CanStop { get; set; }
        public bool CanPause { get; set; }
        public bool IsDriver { get; set; }

        public string StatusText
        {
            get
            {
                return Status switch
                {
                    "Running" => "正在运行",
                    "Stopped" => "已停止",
                    "StartPending" => "正在启动",
                    "StopPending" => "正在停止",
                    "Paused" => "已暂停",
                    "PausePending" => "正在暂停",
                    "ContinuePending" => "正在继续",
                    _ => Status
                };
            }
        }

        public string StartTypeText
        {
            get
            {
                return StartType switch
                {
                    "Automatic" => "自动",
                    "Manual" => "手动",
                    "Disabled" => "禁用",
                    "Boot" => "引导启动",
                    "System" => "系统启动",
                    _ => StartType
                };
            }
        }

        public string TypeText => IsDriver ? "驱动程序" : "Win32 服务";
    }

    private const uint SERVICE_CONFIG_DESCRIPTION = 1;

    /// <summary>
    /// 枚举系统全部服务（含驱动）。对 WMI 做一次性批量查询，避免 150+ 次单独查询卡死 UI。
    /// </summary>
    public static List<ServiceItem> Enumerate(bool includeDrivers = true)
    {
        var result = new List<ServiceItem>();

        ServiceController[] services;
        try
        {
            services = ServiceController.GetServices();
        }
        catch
        {
            return result; // SCM 不可用时返回空列表，避免冒泡崩溃
        }

        // ===== 一次性批量 WMI 查询，替代逐个查询 =====
        var wmiMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var batchQuery = "SELECT Name, PathName, StartName, Description, ProcessId, " +
                             "ServiceType, StartMode, DisplayName FROM Win32_Service";
            foreach (var mo in WmiService.Query(batchQuery))
            {
                var name = mo["Name"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (System.Management.PropertyData prop in mo.Properties)
                    row[prop.Name] = prop.Value?.ToString() ?? string.Empty;

                wmiMap[name] = row;
            }
        }
        catch { /* WMI 不可用时降级运行 */ }

        foreach (var sc in services)
        {
            if (!includeDrivers && sc.ServiceName.StartsWith("Win", StringComparison.Ordinal))
                continue;

            var item = new ServiceItem
            {
                Name = sc.ServiceName,
                DisplayName = sc.DisplayName,
                Status = sc.Status.ToString(),
                CanStop = sc.CanStop,
                CanPause = sc.CanPauseAndContinue
            };

            try { item.StartType = sc.StartType.ToString(); } catch { item.StartType = "未知"; }

            // 从批量查询结果中取数据（O(1) 字典查找）
            if (wmiMap.TryGetValue(sc.ServiceName, out var wmi))
            {
                item.BinaryPath = wmi.TryGetValue("PathName", out var p) ? p : string.Empty;
                item.Account = wmi.TryGetValue("StartName", out var a) ? a : string.Empty;
                item.Description = wmi.TryGetValue("Description", out var d) ? d : string.Empty;
                if (wmi.TryGetValue("ProcessId", out var pidStr) && int.TryParse(pidStr, out var pid))
                    item.ProcessId = pid;
                item.ServiceType = wmi.TryGetValue("ServiceType", out var st) ? st : string.Empty;
                item.IsDriver = item.ServiceType.Contains("Kernel", StringComparison.OrdinalIgnoreCase)
                                || item.ServiceType.Contains("Driver", StringComparison.OrdinalIgnoreCase);
            }

            result.Add(item);
        }

        return result.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static ServiceItem? Refresh(string serviceName)
    {
        return Enumerate().FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    // ==================== 控制 ====================

    public static string? Start(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running) return null;
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? Stop(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped) return null;
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? Pause(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            sc.Pause();
            sc.WaitForStatus(ServiceControllerStatus.Paused, TimeSpan.FromSeconds(10));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? Continue(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            sc.Continue();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public static string? Restart(string serviceName)
    {
        var err = Stop(serviceName);
        if (err != null) return err;
        System.Threading.Thread.Sleep(500);
        return Start(serviceName);
    }

    // ==================== 配置 ====================

    public static string? ChangeStartType(string serviceName, ServiceStartMode mode)
    {
        var scm = Advapi32.OpenSCManager(null, null, WinConst.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return LastError();

        try
        {
            var service = Advapi32.OpenService(scm, serviceName, WinConst.SERVICE_CHANGE_CONFIG);
            if (service == IntPtr.Zero) return LastError();

            try
            {
                uint startType = mode switch
                {
                    ServiceStartMode.Automatic => WinConst.SERVICE_AUTO_START,
                    ServiceStartMode.Manual => WinConst.SERVICE_DEMAND_START,
                    ServiceStartMode.Disabled => WinConst.SERVICE_DISABLED,
                    ServiceStartMode.Boot => WinConst.SERVICE_BOOT_START,
                    ServiceStartMode.System => WinConst.SERVICE_SYSTEM_START,
                    _ => WinConst.SERVICE_NO_CHANGE
                };

                return Advapi32.ChangeServiceConfig(
                    service, WinConst.SERVICE_NO_CHANGE, startType, WinConst.SERVICE_NO_CHANGE,
                    null, null, IntPtr.Zero, null, null, null, null)
                    ? null
                    : LastError();
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

    /// <summary>
    /// 修改服务可执行文件路径（⚠️高危：攻击者常用来持久化）。
    /// </summary>
    public static string? ChangeBinaryPath(string serviceName, string newPath)
    {
        var scm = Advapi32.OpenSCManager(null, null, WinConst.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return LastError();

        try
        {
            var service = Advapi32.OpenService(scm, serviceName, WinConst.SERVICE_CHANGE_CONFIG);
            if (service == IntPtr.Zero) return LastError();

            try
            {
                return Advapi32.ChangeServiceConfig(
                    service, WinConst.SERVICE_NO_CHANGE, WinConst.SERVICE_NO_CHANGE, WinConst.SERVICE_NO_CHANGE,
                    newPath, null, IntPtr.Zero, null, null, null, null)
                    ? null
                    : LastError();
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

    public static string? CreateService(string name, string displayName, string binaryPath,
        ServiceStartMode startMode = ServiceStartMode.Manual)
    {
        var scm = Advapi32.OpenSCManager(null, null, WinConst.SC_MANAGER_CREATE_SERVICE);
        if (scm == IntPtr.Zero) return LastError();

        try
        {
            uint startType = startMode switch
            {
                ServiceStartMode.Automatic => WinConst.SERVICE_AUTO_START,
                ServiceStartMode.Manual => WinConst.SERVICE_DEMAND_START,
                ServiceStartMode.Disabled => WinConst.SERVICE_DISABLED,
                _ => WinConst.SERVICE_DEMAND_START
            };

            var handle = Advapi32.CreateService(
                scm, name, displayName, WinConst.SERVICE_ALL_ACCESS,
                WinConst.SERVICE_WIN32_OWN_PROCESS, startType, 1,
                binaryPath, null, IntPtr.Zero, null, null, null);

            if (handle == IntPtr.Zero) return LastError();
            Advapi32.CloseServiceHandle(handle);
            return null;
        }
        finally
        {
            Advapi32.CloseServiceHandle(scm);
        }
    }

    public static string? DeleteService(string serviceName)
    {
        var scm = Advapi32.OpenSCManager(null, null, WinConst.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return LastError();

        try
        {
            var service = Advapi32.OpenService(scm, serviceName, WinConst.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero) return LastError();

            try
            {
                return Advapi32.DeleteService(service) ? null : LastError();
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

    // ==================== 导出 ====================

    public static string ExportInventory()
    {
        var sb = new StringBuilder();
        sb.AppendLine("NE 管理器 · 服务配置清单");
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('═', 100));
        sb.AppendLine();

        foreach (var s in Enumerate())
        {
            sb.AppendLine($"服务名称    : {s.Name}");
            sb.AppendLine($"显示名称    : {s.DisplayName}");
            sb.AppendLine($"状态        : {s.StatusText}");
            sb.AppendLine($"启动类型    : {s.StartTypeText}");
            sb.AppendLine($"可执行路径  : {s.BinaryPath}");
            sb.AppendLine($"登录账户    : {s.Account}");
            sb.AppendLine($"进程 PID    : {(s.ProcessId > 0 ? s.ProcessId.ToString() : "—")}");
            sb.AppendLine($"类型        : {s.TypeText}");
            if (!string.IsNullOrEmpty(s.Description))
                sb.AppendLine($"描述        : {s.Description}");
            sb.AppendLine(new string('─', 100));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("'", "\\'");

    private static string LastError() => new Win32Exception(Marshal.GetLastWin32Error()).Message;
}
