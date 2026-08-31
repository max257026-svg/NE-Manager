using System.Management;

namespace NEManager.Core.SystemTools;

/// <summary>
/// WMI 控制台服务 —— 执行 WQL、浏览类与属性、导出系统报告。
/// </summary>
public static class WmiService
{
    public sealed class WmiClassInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class WmiPropertyInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsArray { get; set; }
        public bool IsLocal { get; set; }
    }

    // ==================== 查询 ====================

    /// <summary>WMI 单次操作的最长等待时间，避免命名空间不可达时无限阻塞 UI 线程。</summary>
    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 构造一个带超时与「立即返回」选项的 ManagementObjectSearcher。
    /// 这样即使 WMI 服务无响应，单次查询最多阻塞 QueryTimeout 而非永久挂起。
    /// </summary>
    private static ManagementObjectSearcher CreateSearcher(string wql, string? scope)
    {
        var options = new System.Management.EnumerationOptions
        {
            ReturnImmediately = true,
            Timeout = QueryTimeout
        };

        if (string.IsNullOrEmpty(scope))
            return new ManagementObjectSearcher(new ManagementScope(), new ObjectQuery(wql), options);

        var mgmtScope = new ManagementScope(scope, new ConnectionOptions { Timeout = QueryTimeout });
        return new ManagementObjectSearcher(mgmtScope, new ObjectQuery(wql), options);
    }

    /// <summary>
    /// 执行 WQL 查询，返回原始 ManagementObject 集合。
    /// 坏 WQL / 命名空间不可达 / 超时 时返回空集合，不抛出。
    /// </summary>
    public static List<ManagementObject> Query(string wql, string? scope = null)
    {
        var list = new List<ManagementObject>();
        try
        {
            using var searcher = CreateSearcher(wql, scope);
            foreach (ManagementObject mo in searcher.Get())
                list.Add(mo);
        }
        catch
        {
            // 查询失败时返回空集合，由调用方决定如何提示
        }
        return list;
    }

    /// <summary>
    /// 执行查询并把结果整理成表格（列名 + 行数据）。
    /// </summary>
    public sealed class QueryResult
    {
        public List<string> Columns { get; } = new();
        public List<Dictionary<string, string>> Rows { get; } = new();
        public string Error { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
        public int RowCount => Rows.Count;
    }

    public static QueryResult Execute(string wql)
    {
        var result = new QueryResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var searcher = CreateSearcher(wql, null);
            using var collection = searcher.Get();

            foreach (ManagementObject mo in collection)
            {
                var row = new Dictionary<string, string>();
                foreach (PropertyData prop in mo.Properties)
                {
                    if (!result.Columns.Contains(prop.Name))
                        result.Columns.Add(prop.Name);

                    row[prop.Name] = FormatValue(prop.Value);
                }
                result.Rows.Add(row);
            }
        }
        catch (ManagementException ex)
        {
            result.Error = ex.Message;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
        }

        result.Columns.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return string.Empty;
        if (value is string s) return s;
        if (value is Array array)
            return string.Join(", ", array.Cast<object>().Select(o => o?.ToString() ?? string.Empty));
        return value.ToString() ?? string.Empty;
    }

    // ==================== 类浏览 ====================

    public static List<WmiClassInfo> EnumerateClasses(string namespacePath = @"root\cimv2", string filter = "")
    {
        var list = new List<WmiClassInfo>();
        try
        {
            var options = new System.Management.EnumerationOptions
            {
                EnumerateDeep = false,
                ReturnImmediately = true,
                UseAmendedQualifiers = true
            };

            var scope = new ManagementScope(namespacePath, new ConnectionOptions { Timeout = QueryTimeout });
            scope.Connect();

            // 从根类出发枚举所有直接子类
            var rootClass = new ManagementClass(scope, new ManagementPath(), null);
            foreach (ManagementClass cls in rootClass.GetSubclasses(options))
            {
                using (cls)
                {
                    var name = cls.ClassPath.ClassName;
                    if (!string.IsNullOrEmpty(filter) &&
                        !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string description = string.Empty;
                    try
                    {
                        foreach (QualifierData q in cls.Qualifiers)
                            if (q.Name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                                description = q.Value?.ToString() ?? string.Empty;
                    }
                    catch { }

                    list.Add(new WmiClassInfo { Name = name, Description = description });
                }
            }
        }
        catch { }
        return list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<WmiPropertyInfo> EnumerateProperties(string className, string namespacePath = @"root\cimv2")
    {
        var list = new List<WmiPropertyInfo>();
        try
        {
            var path = new ManagementPath(className) { NamespacePath = namespacePath };
            using var cls = new ManagementClass(path);
            foreach (PropertyData prop in cls.Properties)
            {
                list.Add(new WmiPropertyInfo
                {
                    Name = prop.Name,
                    Type = prop.Type.ToString(),
                    IsArray = prop.IsArray,
                    IsLocal = prop.IsLocal
                });
            }
        }
        catch { }
        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ==================== 预置查询库 ====================

    public static readonly (string Group, string Name, string Query)[] PresetQueries =
    {
        ("硬件", "BIOS 信息", "SELECT Manufacturer, Name, SerialNumber, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"),
        ("硬件", "主板信息", "SELECT Manufacturer, Product, SerialNumber, Version FROM Win32_BaseBoard"),
        ("硬件", "处理器", "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"),
        ("硬件", "物理内存", "SELECT Manufacturer, Capacity, Speed, PartNumber, SerialNumber FROM Win32_PhysicalMemory"),
        ("硬件", "磁盘驱动器", "SELECT Model, InterfaceType, Size, SerialNumber, MediaType FROM Win32_DiskDrive"),
        ("硬件", "显卡", "SELECT Name, DriverVersion, AdapterRAM, VideoProcessor FROM Win32_VideoController"),
        ("硬件", "显示器", "SELECT Name, ScreenWidth, ScreenHeight FROM Win32_DesktopMonitor"),
        ("硬件", "网卡", "SELECT Name, MACAddress, Speed, NetConnectionStatus FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True"),
        ("系统", "操作系统", "SELECT Caption, Version, BuildNumber, InstallDate, LastBootUpTime, OSArchitecture FROM Win32_OperatingSystem"),
        ("系统", "计算机信息", "SELECT Name, Domain, Manufacturer, Model, NumberOfProcessors, TotalPhysicalMemory FROM Win32_ComputerSystem"),
        ("系统", "逻辑磁盘", "SELECT DeviceID, VolumeName, FileSystem, Size, FreeSpace, DriveType FROM Win32_LogicalDisk"),
        ("系统", "已安装更新", "SELECT HotFixID, InstalledOn, Description FROM Win32_QuickFixEngineering"),
        ("系统", "系统服务", "SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service"),
        ("系统", "启动项", "SELECT Name, Command, Location, User FROM Win32_StartupCommand"),
        ("系统", "环境变量", "SELECT Name, VariableValue, UserName FROM Win32_Environment"),
        ("系统", "共享文件夹", "SELECT Name, Path, Description FROM Win32_Share"),
        ("系统", "用户账户", "SELECT Name, FullName, Disabled, Lockout, SID FROM Win32_UserAccount"),
        ("系统", "本地组", "SELECT Name, Description, SID FROM Win32_Group"),
        ("系统", "时区", "SELECT Caption, StandardName, Bias FROM Win32_TimeZone"),
        ("软件", "已安装程序", "SELECT Name, Version, Vendor, InstallDate FROM Win32_Product"),
        ("软件", "正在运行的进程", "SELECT Name, ProcessId, ExecutablePath, CommandLine FROM Win32_Process"),
        ("软件", "系统驱动", "SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_SystemDriver"),
        ("安全", "防病毒产品", "SELECT displayName, productState, pathToSignedProductExe FROM AntiVirusProduct"),
        ("安全", "防火墙配置", "SELECT * FROM Win32_ComputerSystemProduct"),
        ("事件", "最近系统错误", "SELECT TimeGenerated, Source, EventCode, Message FROM Win32_NTLogEvent WHERE Logfile = 'System' AND Type = 'Error'"),
        ("性能", "内存使用", "SELECT TotalVisibleMemorySize, FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem"),
        ("性能", "CPU 负载", "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor")
    };

    /// <summary>
    /// 生成一份完整系统报告（文本格式）。
    /// </summary>
    public static string GenerateSystemReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════");
        sb.AppendLine("  NE 管理器 · 系统信息报告");
        sb.AppendLine($"  生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  生成工具：NE 管理器 V1.0 (NewEra Studio)");
        sb.AppendLine("══════════════════════════════════════════════════");
        sb.AppendLine();

        var groups = PresetQueries.GroupBy(q => q.Group);
        foreach (var group in groups)
        {
            sb.AppendLine($"【{group.Key}】");
            foreach (var (_, name, query) in group)
            {
                sb.AppendLine();
                sb.AppendLine($"  ▸ {name}");
                try
                {
                    var res = Execute(query);
                    if (!string.IsNullOrEmpty(res.Error))
                    {
                        sb.AppendLine($"    (查询失败: {res.Error})");
                        continue;
                    }
                    if (res.Rows.Count == 0)
                    {
                        sb.AppendLine("    (无数据)");
                        continue;
                    }
                    foreach (var row in res.Rows)
                    {
                        foreach (var col in res.Columns)
                        {
                            if (!row.TryGetValue(col, out var v) || string.IsNullOrEmpty(v)) continue;
                            sb.AppendLine($"    {col,-24}: {v}");
                        }
                        sb.AppendLine("    " + new string('─', 40));
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    (异常: {ex.Message})");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
