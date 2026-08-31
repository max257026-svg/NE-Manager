using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Risk;
using NEManager.Core.Security;

namespace NEManager.App.Views;

public partial class SecurityPage : UserControl, IRefreshable
{
    public ObservableCollection<PrivilegeEntry> Privileges { get; } = new();

    public SecurityPage()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => RefreshPrivileges();
    }

    public void OnEnter() => RefreshPrivileges();
    public void OnLeave() { }

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    // ==================== 特权管理 ====================

    private async void RefreshPrivileges()
    {
        try
        {
            var (privileges, elevated, integrity) = await System.Threading.Tasks.Task.Run(() =>
            {
                var privs = PrivilegeService.Enumerate();
                return (privs, PrivilegeService.IsElevated(), PrivilegeService.GetIntegrityLevel());
            });

            Privileges.Clear();
            foreach (var p in privileges)
                Privileges.Add(p);

            int enabled = Privileges.Count(p => p.Enabled);
            int critical = Privileges.Count(p => p.IsCritical && p.Enabled);

            PrivilegeSummary.Text = elevated
                ? $"当前进程：管理员 · 完整性「{integrity}」 · " +
                  $"已启用 {enabled}/{Privileges.Count} 项特权（其中关键特权 {critical} 项）"
                : $"⚠️ 当前为普通权限进程，仅能启用 {enabled} 项已有特权。" +
                  "要以管理员身份启用 SeTakeOwnership / SeDebug 等关键特权，请点击左下角「以管理员身份重启」。";

            TiStatusText.Text = elevated
                ? "当前状态：✅ 已具备管理员权限，可以执行接管与提权操作。"
                : "当前状态：⚠️ 非管理员权限。接管所有权、启动 TrustedInstaller 进程都会失败，" +
                  "请先点击左下角「以管理员身份重启」。";
        }
        catch (Exception ex)
        {
            PrivilegeSummary.Text = $"读取特权信息失败：{ex.Message}";
        }
    }

    private void RefreshPrivileges_Click(object sender, RoutedEventArgs e) => RefreshPrivileges();

    private void EnableEssential_Click(object sender, RoutedEventArgs e)
    {
        var error = PrivilegeService.EnableEssentialPrivileges();
        RefreshPrivileges();

        MessageBox.Show(
            string.IsNullOrEmpty(error)
                ? "关键特权启用完成。\n\n若部分特权仍未启用，说明当前进程令牌未持有它们——请以管理员身份重启程序。"
                : $"部分特权启用失败：\n{error}",
            "完成", MessageBoxButton.OK,
            string.IsNullOrEmpty(error) ? MessageBoxImage.Information : MessageBoxImage.Warning);

        SetStatus(string.IsNullOrEmpty(error) ? "关键特权已启用。" : "部分特权启用失败。");
    }

    private void EnableSelected_Click(object sender, RoutedEventArgs e) => ToggleSelected(true);
    private void DisableSelected_Click(object sender, RoutedEventArgs e) => ToggleSelected(false);

    private void ToggleSelected(bool enable)
    {
        if (PrivilegeGrid.SelectedItem is not PrivilegeEntry entry)
        {
            MessageBox.Show("请先选择一项特权。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var error = PrivilegeService.SetPrivilege(entry.Name, enable);
        RefreshPrivileges();

        if (error != null)
        {
            MessageBox.Show($"操作失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"切换特权失败：{error}");
        }
        else
        {
            SetStatus($"已{(enable ? "启用" : "禁用")}特权 {entry.Name}。");
        }
    }

    // ==================== TrustedInstaller ====================

    private void LaunchTiCmd_Click(object sender, RoutedEventArgs e)
        => LaunchAsTi("cmd.exe", "命令行");

    private void LaunchTiSelf_Click(object sender, RoutedEventArgs e)
        => LaunchAsTi(null, "NE 管理器");

    private void LaunchAsTi(string? command, string description)
    {
        if (!RiskFramework.IsAllowed(RiskLevel.Dangerous))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Dangerous),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Dangerous, $"以 TrustedInstaller 身份启动{description}",
                "NT SERVICE\\TrustedInstaller 令牌", new[]
                {
                    "新进程将拥有对全部系统文件的完全控制权",
                    "Windows 资源保护 (WFP) 将不再阻止你修改系统文件",
                    "误删或误改系统文件会导致系统无法启动"
                }),
            "⚠️ 确认启动", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetStatus("正在启动 TrustedInstaller 服务并复制令牌…");

        var result = TrustedInstallerService.LaunchAsTrustedInstaller(command);

        RiskFramework.Log(RiskLevel.Dangerous, "TrustedInstaller 提权",
            command ?? Environment.ProcessPath ?? string.Empty, result.Success,
            string.Empty, result.Message);

        MessageBox.Show(
            result.Success
                ? $"已以 NT SERVICE\\TrustedInstaller 身份启动{description}。\n\nPID: {result.ProcessId}"
                : $"启动失败：{result.Message}",
            result.Success ? "已启动" : "失败",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);

        SetStatus(result.Success ? "TrustedInstaller 进程已启动。" : result.Message);
    }

    private void BrowseTakeOwn_Click(object sender, RoutedEventArgs e)
    {
        var folder = DialogHelper.PickFolder(Application.Current.MainWindow, "选择要接管的目录", TakeOwnPath.Text);
        if (folder != null) TakeOwnPath.Text = folder;
    }

    private void TakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        var path = TakeOwnPath.Text.Trim();
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            MessageBox.Show("路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!RiskFramework.IsAllowed(RiskLevel.Dangerous))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Dangerous),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool recursive = TakeOwnRecursive.IsChecked == true;

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Dangerous, "接管所有权", path, new[]
            {
                "所有者将改为 BUILTIN\\Administrators",
                "会为 Administrators 追加「完全控制」权限",
                recursive ? "将递归处理整个目录树，耗时可能很长" : "仅处理当前对象",
                "原始权限已自动备份，可通过「日志与回滚」恢复"
            }),
            "⚠️ 确认接管", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        // 备份原权限
        var sddl = SecurityDescriptorService.ReadFileSddl(path);
        var backup = RiskFramework.BackupSecurityDescriptor(path, sddl, "接管所有权");

        var error = TrustedInstallerService.TakeOwnership(path);

        if (error == null && recursive && Directory.Exists(path))
        {
            var failures = ApplyRecursively(path, takeOwn: true, sddl: null);
            RiskFramework.Log(RiskLevel.Dangerous, "递归接管所有权", path, failures.Count == 0,
                $"失败 {failures.Count} 项", failures.Count > 0 ? string.Join("; ", failures.Take(5)) : null);

            MessageBox.Show(
                failures.Count == 0
                    ? $"已递归接管：{path}"
                    : $"接管完成，但有 {failures.Count} 项失败：\n{string.Join("\n", failures.Take(10))}",
                "完成", MessageBoxButton.OK,
                failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        else
        {
            RiskFramework.Log(RiskLevel.Dangerous, "接管所有权", path, error == null, string.Empty, error,
                backup?.BackupPath);

            MessageBox.Show(
                error == null
                    ? $"已接管：{path}\n\n原权限已备份。"
                    : $"接管失败：{error}\n\n提示：需要管理员权限并启用 SeTakeOwnershipPrivilege。",
                error == null ? "成功" : "失败",
                MessageBoxButton.OK,
                error == null ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        SetStatus(error == null ? "接管完成。" : error);
    }

    private List<string> ApplyRecursively(string root, bool takeOwn, string? sddl)
    {
        var failures = new List<string>();

        void Walk(string dir, int depth)
        {
            if (depth > 20) return;

            if (takeOwn)
            {
                var err = TrustedInstallerService.TakeOwnership(dir);
                if (err != null) failures.Add($"{dir}: {err}");
            }
            else if (sddl != null)
            {
                var err = SecurityDescriptorService.SetFileSddl(dir, sddl);
                if (err != null) failures.Add($"{dir}: {err}");
            }

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir, "*", new EnumerationOptions
                         {
                             RecurseSubdirectories = false,
                             IgnoreInaccessible = true,
                             AttributesToSkip = 0
                         }))
                {
                    Walk(sub, depth + 1);
                }
            }
            catch { /* 忽略无法枚举的目录 */ }
        }

        Walk(root, 0);
        return failures;
    }

    private void RestoreTi_Click(object sender, RoutedEventArgs e)
    {
        var path = TakeOwnPath.Text.Trim();
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            MessageBox.Show("路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            $"把所有者还原给 TrustedInstaller？\n\n{path}\n\n" +
            "还原后你将失去对该对象的直接修改权限（这是系统文件的正常状态）。",
            "确认还原", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var error = TrustedInstallerService.RestoreToTrustedInstaller(path);
        RiskFramework.Log(RiskLevel.Caution, "还原 TrustedInstaller 所有者", path, error == null, string.Empty, error);

        MessageBox.Show(error == null ? "已还原。" : $"还原失败：{error}",
            error == null ? "完成" : "失败", MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void ResetInheritance_Click(object sender, RoutedEventArgs e)
    {
        var path = TakeOwnPath.Text.Trim();
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            MessageBox.Show("路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            $"重置为继承权限？\n\n{path}\n\n" +
            "这会清空该对象上的所有显式权限条目，改为从父对象继承。\n" +
            "适用于系统目录权限被错误修改导致无法访问的情况。",
            "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var sddl = SecurityDescriptorService.ReadFileSddl(path);
        RiskFramework.BackupSecurityDescriptor(path, sddl, "重置继承权限");

        var error = TrustedInstallerService.ResetInheritance(path);
        RiskFramework.Log(RiskLevel.Dangerous, "重置继承权限", path, error == null, string.Empty, error);

        MessageBox.Show(error == null ? "已重置。" : $"重置失败：{error}",
            error == null ? "完成" : "失败", MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    // ==================== 权限模板 ====================

    private void BrowseTemplateSource_Click(object sender, RoutedEventArgs e)
    {
        var folder = DialogHelper.PickFolder(Application.Current.MainWindow, "选择模板来源目录");
        if (folder != null) TemplateSourcePath.Text = folder;
    }

    private void BrowseTemplateTarget_Click(object sender, RoutedEventArgs e)
    {
        var folder = DialogHelper.PickFolder(Application.Current.MainWindow, "选择要应用模板的目标目录");
        if (folder != null) TemplateTargetPath.Text = folder;
    }

    private void ReadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var path = TemplateSourcePath.Text.Trim();
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            MessageBox.Show("模板来源路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var sddl = SecurityDescriptorService.ReadFileSddl(path);
        if (string.IsNullOrEmpty(sddl))
        {
            MessageBox.Show("读取安全描述符失败（权限不足？）。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        TemplateSddl.Text = sddl;
        SetStatus($"已从 {path} 读取权限模板。");
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        var target = TemplateTargetPath.Text.Trim();
        var sddl = TemplateSddl.Text.Trim();

        if (!Directory.Exists(target))
        {
            MessageBox.Show("目标目录不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (string.IsNullOrEmpty(sddl))
        {
            MessageBox.Show("请先读取模板 SDDL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!RiskFramework.IsAllowed(RiskLevel.Dangerous))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Dangerous),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Dangerous, "批量应用权限模板", target, new[]
            {
                "目标目录树中所有子目录的权限都会被覆盖",
                "若模板错误，可能导致连管理员都无法访问这些目录",
                "操作过程中无法中断"
            }),
            "⚠️ 确认批量应用", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        RiskFramework.BackupSecurityDescriptor(target,
            SecurityDescriptorService.ReadFileSddl(target), "批量应用权限模板");

        SetStatus("正在应用权限模板…");
        var failures = SecurityDescriptorService.ApplyTemplateToTree(target, sddl);

        RiskFramework.Log(RiskLevel.Dangerous, "批量应用权限模板", target, failures.Count == 0,
            $"失败 {failures.Count} 项");

        MessageBox.Show(
            failures.Count == 0
                ? $"权限模板已应用到：{target}"
                : $"完成，但有 {failures.Count} 项失败：\n{string.Join("\n", failures.Take(10))}",
            "完成", MessageBoxButton.OK,
            failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

        SetStatus(failures.Count == 0 ? "权限模板已应用。" : $"{failures.Count} 项失败。");
    }

    // ==================== 预设模板 ====================

    private void PresetAdminFull_Click(object sender, RoutedEventArgs e)
        => TemplateSddl.Text = "O:BAG:BAD:PAI(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;0x1200a9;;;BU)";

    private void PresetEveryoneRead_Click(object sender, RoutedEventArgs e)
        => TemplateSddl.Text = "O:BAG:BAD:PAI(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;0x1200a9;;;WD)";

    private void PresetInherit_Click(object sender, RoutedEventArgs e)
        => TemplateSddl.Text = "O:BAG:BAD:AI(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)";
}
