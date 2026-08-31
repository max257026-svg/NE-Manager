namespace NEManager.Core.Risk;

/// <summary>
/// 危险等级。
/// </summary>
public enum RiskLevel
{
    /// <summary>普通操作，无风险。</summary>
    Safe,

    /// <summary>需要注意，失败可恢复。</summary>
    Caution,

    /// <summary>高危：可能影响系统稳定性。</summary>
    Dangerous,

    /// <summary>极高危：操作失误会导致系统无法启动或数据永久丢失。</summary>
    Critical
}

/// <summary>
/// 一条操作日志。
/// </summary>
public sealed class OperationLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public RiskLevel Level { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupPath { get; set; }

    public string LevelText => Level switch
    {
        RiskLevel.Safe => "安全",
        RiskLevel.Caution => "注意",
        RiskLevel.Dangerous => "高危",
        RiskLevel.Critical => "极高危",
        _ => "未知"
    };

    public string ResultText => Success ? "成功" : "失败";
    public string TimeText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// 可回滚操作的备份记录。
/// </summary>
public sealed class BackupRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Operation { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public BackupKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsRolledBack { get; set; }

    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string KindText => Kind switch
    {
        BackupKind.File => "文件",
        BackupKind.DirectoryTree => "目录树",
        BackupKind.RegistryKey => "注册表项",
        BackupKind.SecurityDescriptor => "安全描述符",
        BackupKind.ServiceConfiguration => "服务配置",
        _ => Kind.ToString()
    };
    public string StateText => IsRolledBack ? "已回滚" : "可用";
    public bool CanRollback => !IsRolledBack && !string.IsNullOrEmpty(BackupPath) && File.Exists(BackupPath);
}

public enum BackupKind
{
    File,
    DirectoryTree,
    RegistryKey,
    SecurityDescriptor,
    ServiceConfiguration
}

/// <summary>
/// 安全模式等级 —— 普通用户模式下禁用一切底层修改能力。
/// </summary>
public enum SafetyMode
{
    /// <summary>普通用户模式：只读浏览，禁用所有高危功能。</summary>
    Normal,

    /// <summary>高级模式：启用系统文件修改、特权操作。</summary>
    Advanced,

    /// <summary>专家模式：启用全部能力，包括原始磁盘写入、BCD 修改。</summary>
    Expert
}

/// <summary>
/// NE 管理器风险框架 —— 所有高危操作的统一入口。
/// 强制四件事：显式警告、自动备份、完整日志、可回滚。
/// </summary>
public static class RiskFramework
{
    private static readonly List<OperationLogEntry> Logs = new();
    private static readonly List<BackupRecord> Backups = new();

    public static SafetyMode CurrentMode { get; set; } = SafetyMode.Normal;

    /// <summary>是否允许执行指定等级的操作。</summary>
    public static bool IsAllowed(RiskLevel level) => level switch
    {
        RiskLevel.Safe => true,
        RiskLevel.Caution => CurrentMode >= SafetyMode.Normal,
        RiskLevel.Dangerous => CurrentMode >= SafetyMode.Advanced,
        RiskLevel.Critical => CurrentMode >= SafetyMode.Expert,
        _ => false
    };

    /// <summary>
    /// 在安全模式不足时给出的提示。
    /// </summary>
    public static string GetBlockedMessage(RiskLevel level) =>
        $"当前处于「{GetModeName(CurrentMode)}」模式，无法执行{GetLevelName(level)}操作。\n\n" +
        $"请在「安全模式」设置中切换到「{GetModeName(level switch
        {
            RiskLevel.Dangerous => SafetyMode.Advanced,
            RiskLevel.Critical => SafetyMode.Expert,
            _ => SafetyMode.Normal
        })}」或更高等级。";

    public static string GetModeName(SafetyMode mode) => mode switch
    {
        SafetyMode.Normal => "普通用户",
        SafetyMode.Advanced => "高级",
        SafetyMode.Expert => "专家",
        _ => "未知"
    };

    public static string GetLevelName(RiskLevel level) => level switch
    {
        RiskLevel.Safe => "安全",
        RiskLevel.Caution => "需注意",
        RiskLevel.Dangerous => "高危",
        RiskLevel.Critical => "极高危",
        _ => "未知"
    };

    /// <summary>
    /// 生成高危操作的警告文案。
    /// </summary>
    public static string BuildWarning(RiskLevel level, string operation, string target, string[]? consequences = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⚠️ {GetLevelName(level)}操作确认");
        sb.AppendLine();
        sb.AppendLine($"操作：{operation}");
        sb.AppendLine($"对象：{target}");
        sb.AppendLine();

        if (consequences is { Length: > 0 })
        {
            sb.AppendLine("可能造成的后果：");
            foreach (var c in consequences)
                sb.AppendLine($"  · {c}");
            sb.AppendLine();
        }

        sb.AppendLine(level switch
        {
            RiskLevel.Critical =>
                "此操作一旦执行错误，可能导致操作系统无法启动或数据永久丢失。\n" +
                "NE 管理器会自动创建备份，但你仍应确认已了解操作后果。",
            RiskLevel.Dangerous =>
                "此操作会影响系统配置或受保护文件。\n" +
                "NE 管理器会自动创建备份，可通过「回滚中心」恢复。",
            RiskLevel.Caution =>
                "此操作会修改系统状态，但通常可恢复。",
            _ => "此操作是安全的。"
        });

        return sb.ToString();
    }

    // ==================== 日志 ====================

    public static void Log(RiskLevel level, string operation, string target, bool success,
        string detail = "", string? error = null, string? backupPath = null)
    {
        lock (Logs)
        {
            Logs.Add(new OperationLogEntry
            {
                Level = level,
                Operation = operation,
                Target = target,
                Success = success,
                Detail = detail,
                ErrorMessage = error,
                BackupPath = backupPath
            });

            // 内存上限保护
            if (Logs.Count > 5000)
                Logs.RemoveRange(0, Logs.Count - 5000);
        }
    }

    public static IReadOnlyList<OperationLogEntry> GetLogs() => Logs.AsReadOnly();

    public static IReadOnlyList<OperationLogEntry> GetLogs(RiskLevel minimumLevel)
        => Logs.Where(l => l.Level >= minimumLevel).ToList().AsReadOnly();

    public static void ClearLogs() => Logs.Clear();

    // ==================== 备份 ====================

    public static string BackupRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NewEraStudio", "NEManager", "Backups");

    /// <summary>
    /// 备份一个文件，返回备份记录。
    /// </summary>
    public static BackupRecord? BackupFile(string sourcePath, string operation, string description = "")
    {
        try
        {
            var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dir);

            var name = Path.GetFileName(sourcePath);
            var backupPath = Path.Combine(dir, $"{DateTime.Now:HHmmss}_{name}");

            int counter = 1;
            while (File.Exists(backupPath))
                backupPath = Path.Combine(dir, $"{DateTime.Now:HHmmss}_{counter++}_{name}");

            File.Copy(sourcePath, backupPath, true);

            var record = new BackupRecord
            {
                Operation = operation,
                OriginalPath = sourcePath,
                BackupPath = backupPath,
                Kind = BackupKind.File,
                Description = description
            };

            lock (Backups) { Backups.Add(record); }
            return record;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 备份一段注册表（导出为 .reg 文件）。
    /// </summary>
    public static BackupRecord? BackupRegistry(string keyPath, string operation)
    {
        try
        {
            var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dir);

            var safe = string.Join("_", keyPath.Split(Path.GetInvalidFileNameChars()));
            var backupPath = Path.Combine(dir, $"{DateTime.Now:HHmmss}_{safe}.reg");

            var content = NEManager.Core.Registry.RegistryService.ExportBranch(keyPath);
            File.WriteAllText(backupPath, content, System.Text.Encoding.Unicode);

            var record = new BackupRecord
            {
                Operation = operation,
                OriginalPath = keyPath,
                BackupPath = backupPath,
                Kind = BackupKind.RegistryKey,
                Description = $"注册表分支 {keyPath}"
            };

            lock (Backups) { Backups.Add(record); }
            return record;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 备份安全描述符（SDDL 文本）。
    /// </summary>
    public static BackupRecord? BackupSecurityDescriptor(string path, string sddl, string operation)
    {
        try
        {
            var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dir);

            var safe = string.Join("_", path.Split(Path.GetInvalidFileNameChars()));
            var backupPath = Path.Combine(dir, $"{DateTime.Now:HHmmss}_{safe}.sddl");

            File.WriteAllText(backupPath, sddl);

            var record = new BackupRecord
            {
                Operation = operation,
                OriginalPath = path,
                BackupPath = backupPath,
                Kind = BackupKind.SecurityDescriptor,
                Description = $"安全描述符 {path}"
            };

            lock (Backups) { Backups.Add(record); }
            return record;
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<BackupRecord> GetBackups()
    {
        lock (Backups) { return Backups.AsReadOnly(); }
    }

    // ==================== 回滚 ====================

    public static string? Rollback(BackupRecord record)
    {
        try
        {
            switch (record.Kind)
            {
                case BackupKind.File:
                    if (!File.Exists(record.BackupPath))
                        return "备份文件已不存在，无法回滚。";
                    File.Copy(record.BackupPath, record.OriginalPath, true);
                    break;

                case BackupKind.SecurityDescriptor:
                    var sddl = File.ReadAllText(record.BackupPath);
                    var err = NEManager.Core.Security.SecurityDescriptorService.SetFileSddl(record.OriginalPath, sddl);
                    if (err != null) return err;
                    break;

                case BackupKind.RegistryKey:
                    return "注册表回滚请手动导入备份的 .reg 文件：" + record.BackupPath;

                default:
                    return "该备份类型暂不支持自动回滚，请手动处理。";
            }

            record.IsRolledBack = true;
            Log(RiskLevel.Caution, "回滚操作", record.OriginalPath, true,
                $"从备份 {record.BackupPath} 恢复");
            return null;
        }
        catch (Exception ex)
        {
            Log(RiskLevel.Caution, "回滚操作", record.OriginalPath, false, string.Empty, ex.Message);
            return ex.Message;
        }
    }

    /// <summary>
    /// 受保护的危险操作执行器：检查安全模式 → 备份 → 执行 → 记录日志。
    /// </summary>
    public static (bool executed, string? error, BackupRecord? backup) ExecuteGuarded(
        RiskLevel level,
        string operation,
        string target,
        Func<string>? backupAction,
        Func<string?> action)
    {
        if (!IsAllowed(level))
            return (false, GetBlockedMessage(level), null);

        BackupRecord? backup = null;
        if (backupAction != null)
        {
            var path = backupAction();
            if (!string.IsNullOrEmpty(path))
            {
                backup = new BackupRecord
                {
                    Operation = operation,
                    OriginalPath = target,
                    BackupPath = path,
                    Kind = BackupKind.File
                };
                lock (Backups) { Backups.Add(backup); }
            }
        }

        try
        {
            var error = action();
            Log(level, operation, target, error == null, string.Empty, error, backup?.BackupPath);
            return (error == null, error, backup);
        }
        catch (Exception ex)
        {
            Log(level, operation, target, false, string.Empty, ex.Message, backup?.BackupPath);
            return (false, ex.Message, backup);
        }
    }

    /// <summary>
    /// 打开备份目录。
    /// </summary>
    public static void OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(BackupRoot);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = BackupRoot,
                UseShellExecute = true
            });
        }
        catch
        {
            // 忽略
        }
    }
}
