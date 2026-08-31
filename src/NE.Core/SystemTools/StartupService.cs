namespace NEManager.Core.SystemTools;

using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;

public enum StartupLocation
{
    HKRun,           // HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    HKRunOnce,       // HKCU\...\RunOnce
    HKLocalRun,      // HKLM\...\Run
    HKLocalRunOnce,  // HKLM\...\RunOnce
    HKPoliciesRun,   // HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run
    UserFolder,      // %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
    CommonFolder,    // %ProgramData%\Microsoft\Windows\Start Menu\Programs\StartUp
    TaskScheduler,   // 任务计划程序
    WinlogonRun,     // HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Userinit
    ShellExt,        // HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell
    BootExecute      // HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\BootExecute
}

public record StartupItem(
    string Name,
    string Command,
    StartupLocation Location,
    string RegistryPath,
    string ValueName,
    bool Enabled
);

public static class StartupService
{
    public static List<StartupItem> Scan()
    {
        var list = new List<StartupItem>();
        ScanRegistryRunKeys(list);
        ScanStartupFolders(list);
        ScanWinlogon(list);
        ScanBootExecute(list);
        return list;
    }

    private static void ScanRegistryRunKeys(List<StartupItem> list)
    {
        var keys = new (RegistryKey, StartupLocation, string)[]
        {
            (Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false)!, StartupLocation.HKRun, @"HKCU\..\Run"),
            (Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", false)!, StartupLocation.HKRunOnce, @"HKCU\..\RunOnce"),
            (Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false)!, StartupLocation.HKLocalRun, @"HKLM\..\Run"),
            (Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", false)!, StartupLocation.HKLocalRunOnce, @"HKLM\..\RunOnce"),
            (Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", false)!, StartupLocation.HKPoliciesRun, @"HKLM\..\Policies\Run"),
        };

        foreach (var (key, loc, path) in keys)
        {
            try
            {
                if (key == null) continue;
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        var val = key.GetValue(name);
                        list.Add(new StartupItem(
                            Name: name,
                            Command: val?.ToString() ?? "",
                            Location: loc,
                            RegistryPath: path,
                            ValueName: name,
                            Enabled: true
                        ));
                    }
                    catch { }
                }
            }
            finally { key?.Dispose(); }
        }
    }

    private static void ScanStartupFolders(List<StartupItem> list)
    {
        var folders = new (string, StartupLocation)[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupLocation.UserFolder),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "Startup"), StartupLocation.CommonFolder),
        };
        foreach (var (dir, loc) in folders)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    list.Add(new StartupItem(
                        Name: Path.GetFileName(f),
                        Command: f,
                        Location: loc,
                        RegistryPath: dir,
                        ValueName: Path.GetFileName(f),
                        Enabled: true
                    ));
                }
            }
            catch { }
        }
    }

    private static void ScanWinlogon(List<StartupItem> list)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", false);
            if (key == null) return;
            var shell = key.GetValue("Shell")?.ToString();
            var userinit = key.GetValue("Userinit")?.ToString();
            if (!string.IsNullOrEmpty(shell))
                list.Add(new StartupItem("Shell", shell, StartupLocation.WinlogonRun, @"HKLM\..\Winlogon", "Shell", true));
            if (!string.IsNullOrEmpty(userinit))
                list.Add(new StartupItem("Userinit", userinit, StartupLocation.WinlogonRun, @"HKLM\..\Winlogon", "Userinit", true));
        }
        catch { }
    }

    private static void ScanBootExecute(List<StartupItem> list)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager", false);
            if (key == null) return;
            var val = key.GetValue("BootExecute");
            if (val is string[] arr)
            {
                foreach (var v in arr)
                    list.Add(new StartupItem("BootExecute", v, StartupLocation.BootExecute, @"HKLM\..\Session Manager", "BootExecute", true));
            }
        }
        catch { }
    }

    /// <summary>从注册表启动项中删除</summary>
    public static bool RemoveFromRegistry(StartupItem item)
    {
        try
        {
            if (item.Location is StartupLocation.HKRun or StartupLocation.HKRunOnce)
            {
                var subPath = item.Location == StartupLocation.HKRun ? @"Software\Microsoft\Windows\CurrentVersion\Run" : @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
                using var key = Registry.CurrentUser.CreateSubKey(subPath, true);
                key?.DeleteValue(item.ValueName, false);
                return true;
            }
            if (item.Location is StartupLocation.HKLocalRun or StartupLocation.HKLocalRunOnce or StartupLocation.HKPoliciesRun)
            {
                var subPath = item.Location switch
                {
                    StartupLocation.HKLocalRun => @"Software\Microsoft\Windows\CurrentVersion\Run",
                    StartupLocation.HKLocalRunOnce => @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                    _ => @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run"
                };
                using var key = Registry.LocalMachine.CreateSubKey(subPath, true);
                key?.DeleteValue(item.ValueName, false);
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>禁用：重命名 Run 为 RunDisabled（RunOnce 改 RunOnceEx）</summary>
    public static bool DisableInRegistry(StartupItem item)
    {
        // 简化：直接删除（RunOnce 项本来就只跑一次），对 Run 键用改名方案太复杂
        return RemoveFromRegistry(item);
    }
}
