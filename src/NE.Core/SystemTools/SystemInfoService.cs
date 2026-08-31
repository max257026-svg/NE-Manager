using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace NEManager.Core.SystemTools;

/// <summary>
/// 一键导出 MSInfo 风格的完整系统信息 — WMI 查询零依赖。
/// </summary>
public static class SystemInfoService
{
    public class SystemInfo
    {
        public Dictionary<string, string> Hardware { get; set; } = new();
        public Dictionary<string, string> OS { get; set; } = new();
        public List<(string name, string version, string publisher)> InstalledApps { get; set; } = new();
        public List<(string name, string version, string status)> Drivers { get; set; } = new();
        public List<(string name, string adapter, string ip, string mac, string speed)> Network { get; set; } = new();
    }

    public static SystemInfo Collect()
    {
        var info = new SystemInfo();

        // OS + 硬件
        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in search.Get())
            {
                info.OS["名称"] = mo["Caption"]?.ToString() ?? "";
                info.OS["版本"] = mo["Version"]?.ToString() ?? "";
                info.OS["Build"] = mo["BuildNumber"]?.ToString() ?? "";
                info.OS["架构"] = mo["OSArchitecture"]?.ToString() ?? "";
                info.OS["序列号"] = mo["SerialNumber"]?.ToString() ?? "";
                info.OS["最后启动"] = mo["LastBootUpTime"]?.ToString() ?? "";
            }
        }
        catch { /* ignore */ }

        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject mo in search.Get())
            {
                info.Hardware["电脑名"] = mo["Name"]?.ToString() ?? "";
                info.Hardware["制造商"] = mo["Manufacturer"]?.ToString() ?? "";
                info.Hardware["型号"] = mo["Model"]?.ToString() ?? "";
                info.Hardware["主板"] = mo["BaseBoard"]?.ToString() ?? "";
            }
        }
        catch { }

        try
        {
            using var search = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject mo in search.Get())
            {
                info.Hardware["CPU"] = $"{mo["Name"]} ({mo["NumberOfCores"]}C/{mo["NumberOfLogicalProcessors"]}T)";
            }
        }
        catch { }

        try
        {
            using var search = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            long total = 0;
            foreach (ManagementObject mo in search.Get())
            {
                if (long.TryParse(mo["Capacity"]?.ToString(), out var cap)) total += cap;
            }
            info.Hardware["内存"] = $"{total / 1024 / 1024 / 1024} GB";
        }
        catch { }

        try
        {
            using var search = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject mo in search.Get())
            {
                info.Hardware["显卡"] = mo["Name"]?.ToString() ?? "";
                break;
            }
        }
        catch { }

        // 已装软件（64 位 + 32 位）
        var uninstallPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };
        foreach (var path in uninstallPaths)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;
                foreach (var subName in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(subName);
                    if (sub == null) continue;
                    var name = sub.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    var ver = sub.GetValue("DisplayVersion")?.ToString() ?? "";
                    var pub = sub.GetValue("Publisher")?.ToString() ?? "";
                    info.InstalledApps.Add((name, ver, pub));
                }
            }
            catch { }
        }
        info.InstalledApps.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        // 驱动
        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, State FROM Win32_SystemDriver WHERE State != 'Stopped'");
            foreach (ManagementObject mo in search.Get())
            {
                info.Drivers.Add((
                    mo["Name"]?.ToString() ?? "",
                    mo["DriverVersion"]?.ToString() ?? "",
                    mo["State"]?.ToString() ?? ""));
            }
        }
        catch { }

        // 网络
        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL");
            foreach (ManagementObject mo in search.Get())
            {
                try
                {
                    var cfg = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_NetworkAdapter.DeviceID='{mo["DeviceID"]}'}} WHERE AssocClass=Win32_NetworkAdapterConfiguration");
                    string ip = "", mac = "";
                    foreach (ManagementObject c in cfg.Get())
                    {
                        mac = c["MACAddress"]?.ToString() ?? "";
                        var ips = c["IPAddress"] as string[];
                        ip = ips != null && ips.Length > 0 ? ips[0] : "";
                    }
                    info.Network.Add((
                        mo["Name"]?.ToString() ?? "",
                        mo["NetConnectionID"]?.ToString() ?? "",
                        ip, mac,
                        mo["Speed"]?.ToString() ?? ""));
                }
                catch { }
            }
        }
        catch { }

        return info;
    }

    public static string ExportMarkdown()
    {
        var sb = new StringBuilder();
        var i = Collect();

        sb.AppendLine("# 系统信息导出");
        sb.AppendLine();
        sb.AppendLine("## 操作系统");
        foreach (var kv in i.OS) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## 硬件");
        foreach (var kv in i.Hardware) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine();
        sb.AppendLine($"## 已装软件 ({i.InstalledApps.Count})");
        foreach (var a in i.InstalledApps) sb.AppendLine($"- {a.name} · v{a.version} · {a.publisher}");
        sb.AppendLine();
        sb.AppendLine($"## 驱动 ({i.Drivers.Count})");
        foreach (var d in i.Drivers) sb.AppendLine($"- {d.name} · v{d.version} · {d.status}");
        sb.AppendLine();
        sb.AppendLine($"## 网络适配器 ({i.Network.Count})");
        foreach (var n in i.Network) sb.AppendLine($"- {n.adapter} / {n.name} · {n.ip} · MAC {n.mac}");

        return sb.ToString();
    }
}
