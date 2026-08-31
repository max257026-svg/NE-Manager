using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NEManager.Core.Registry;
using NEManager.Core.Risk;
using NEManager.Core.Security;

namespace NEManager.App.Views;

public partial class RegistryPage : UserControl, IRefreshable
{
    /// <summary>树视图节点模型。</summary>
    public sealed class KeyNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public ObservableCollection<KeyNode> Children { get; } = new();
        public bool HasLoaded { get; set; }
        public bool IsPlaceholder { get; set; }
    }

    public ObservableCollection<Core.Registry.RegistryService.RegistryValueItem> Values { get; } = new();

    private string _currentPath = @"HKEY_LOCAL_MACHINE\SOFTWARE";

    public RegistryPage()
    {
        InitializeComponent();
        DataContext = this;
        ValueGrid.ItemsSource = Values;
        Loaded += (_, _) => BuildRootTree();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
        StatusHint.Text = message;
    }

    // ==================== 树构建 ====================

    private void BuildRootTree()
    {
        KeyTree.Items.Clear();

        foreach (var root in new[]
                 {
                     "HKEY_CLASSES_ROOT", "HKEY_CURRENT_USER",
                     "HKEY_LOCAL_MACHINE", "HKEY_USERS", "HKEY_CURRENT_CONFIG"
                 })
        {
            var node = new KeyNode { Name = root, FullPath = root };
            node.Children.Add(new KeyNode { Name = "(加载中…)", IsPlaceholder = true });
            KeyTree.Items.Add(node);
        }
    }

    private void KeyTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item) return;
        if (item.DataContext is not KeyNode node) return;
        if (node.HasLoaded) return;

        LoadChildren(node);
    }

    private void LoadChildren(KeyNode node)
    {
        try
        {
            node.Children.Clear();
            node.HasLoaded = true;

            var subKeys = Core.Registry.RegistryService.EnumerateSubKeys(node.FullPath);
            if (subKeys.Count == 0)
            {
                node.Children.Add(new KeyNode { Name = "(无子项)", IsPlaceholder = true });
                return;
            }

            foreach (var sub in subKeys)
            {
                var child = new KeyNode { Name = sub.Name, FullPath = sub.FullPath };
                try
                {
                    if (sub.SubKeyCount > 0)
                        child.Children.Add(new KeyNode { Name = "(加载中…)", IsPlaceholder = true });
                }
                catch { }
                node.Children.Add(child);
            }
        }
        catch (Exception ex)
        {
            node.Children.Clear();
            node.Children.Add(new KeyNode { Name = $"(访问被拒：{ex.Message})", IsPlaceholder = true });
        }
    }

    private void KeyTree_SelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (KeyTree.SelectedItem is not KeyNode node) return;
        if (node.IsPlaceholder) return;

        _currentPath = node.FullPath;
        PathBox.Text = node.FullPath;
        LoadValues();
    }

    // ==================== 值列表 ====================

    private void LoadValues()
    {
        try
        {
            Values.Clear();
            foreach (var v in Core.Registry.RegistryService.EnumerateValues(_currentPath))
                Values.Add(v);

            SetStatus($"「{_currentPath}」共 {Values.Count} 个值");
        }
        catch (Exception ex)
        {
            Values.Clear();
            SetStatus($"读取注册表值失败：{ex.Message}");
        }
    }

    /// <summary>供搜索结果跳转调用。</summary>
    public void NavigateTo(string path)
    {
        _currentPath = path;
        PathBox.Text = path;
        LoadValues();
        ExpandToPath(path);
        SetStatus($"已跳转到 {path}");
    }

    private void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Go_Click(sender, e);
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        _currentPath = path;
        LoadValues();
        ExpandToPath(path);
    }

    private void ExpandToPath(string path)
    {
        // 在树中定位并展开到指定路径
        foreach (KeyNode root in KeyTree.Items)
        {
            if (!path.StartsWith(root.FullPath, StringComparison.OrdinalIgnoreCase)) continue;

            var remaining = path.Length > root.FullPath.Length
                ? path[(root.FullPath.Length + 1)..]
                : string.Empty;

            if (string.IsNullOrEmpty(remaining))
            {
                root.HasLoaded = false;
                LoadChildren(root);
                SetSelected(root);
                return;
            }

            var segments = remaining.Split('\\');
            var current = root;

            foreach (var segment in segments)
            {
                if (!current.HasLoaded) LoadChildren(current);

                var next = current.Children.FirstOrDefault(c =>
                    c.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));

                if (next == null) break;
                current = next;
            }

            if (!current.HasLoaded) LoadChildren(current);
            SetSelected(current);
            return;
        }
    }

    private void SetSelected(KeyNode node)
    {
        // 展开所有父节点后选中
        var container = KeyTree.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
        container?.BringIntoView();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        BuildRootTree();
        LoadValues();
    }

    // ==================== 键值操作 ====================

    private void NewKey_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Show("新建注册表项", "项名称：", "NewKey");
        if (string.IsNullOrWhiteSpace(name)) return;

        var error = Core.Registry.RegistryService.CreateKey(_currentPath, name);
        if (error != null)
        {
            MessageBox.Show($"创建失败：{error}\n\n提示：HKLM 下的项需要管理员权限。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RiskFramework.Log(RiskLevel.Caution, "新建注册表项", _currentPath + "\\" + name, true);
        BuildRootTree();
        SetStatus($"已创建项：{name}");
    }

    private void RenameKey_Click(object sender, RoutedEventArgs e)
    {
        var oldName = RegistryPath.GetLeafName(_currentPath);
        var newName = PromptDialog.Show("重命名注册表项", "新名称：", oldName);
        if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

        var error = Core.Registry.RegistryService.RenameKey(_currentPath, newName);
        if (error != null)
        {
            MessageBox.Show($"重命名失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RiskFramework.Log(RiskLevel.Dangerous, "重命名注册表项", _currentPath, true, $"→ {newName}");
        _currentPath = RegistryPath.Combine(RegistryPath.GetParentPath(_currentPath), newName);
        PathBox.Text = _currentPath;
        BuildRootTree();
        LoadValues();
        SetStatus("已重命名。");
    }

    private void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        var isSystemKey = _currentPath.Contains(@"HKEY_LOCAL_MACHINE\SYSTEM", StringComparison.OrdinalIgnoreCase)
                          || _currentPath.Contains(@"HKEY_LOCAL_MACHINE\SOFTWARE", StringComparison.OrdinalIgnoreCase);
        var level = isSystemKey ? RiskLevel.Critical : RiskLevel.Dangerous;

        if (!RiskFramework.IsAllowed(level))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(level), "操作被拦截",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(level, "删除注册表项", _currentPath, new[]
            {
                "该项及其所有子项、所有键值都会被删除",
                isSystemKey ? "这是系统关键分支，删除可能导致 Windows 无法启动" : "删除后无法恢复",
                "将自动导出备份到备份目录"
            }),
            "⚠️ 确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        // 先备份
        RiskFramework.BackupRegistry(_currentPath, "删除注册表项");

        var error = Core.Registry.RegistryService.DeleteKey(_currentPath);
        RiskFramework.Log(level, "删除注册表项", _currentPath, error == null, string.Empty, error);

        if (error != null)
        {
            MessageBox.Show($"删除失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _currentPath = RegistryPath.GetParentPath(_currentPath);
        PathBox.Text = _currentPath;
        BuildRootTree();
        LoadValues();
        SetStatus("已删除。");
    }

    private void NewValue_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RegistryValueEditDialog(_currentPath) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            LoadValues();
            SetStatus("已创建值。");
        }
    }

    private void EditValue_Click(object sender, RoutedEventArgs e)
    {
        if (ValueGrid.SelectedItem is not Core.Registry.RegistryService.RegistryValueItem item)
        {
            MessageBox.Show("请先选择一个值。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RegistryValueEditDialog(_currentPath, item) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            LoadValues();
            SetStatus("已保存。");
        }
    }

    private void ValueGrid_DoubleClick(object sender, MouseButtonEventArgs e) => EditValue_Click(sender, e);

    private void RenameValue_Click(object sender, RoutedEventArgs e)
    {
        if (ValueGrid.SelectedItem is not Core.Registry.RegistryService.RegistryValueItem item) return;

        var newName = PromptDialog.Show("重命名值", "新名称：", item.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        var error = Core.Registry.RegistryService.RenameValue(_currentPath, item.Name, newName);
        if (error != null)
            MessageBox.Show($"重命名失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        else
        {
            RiskFramework.Log(RiskLevel.Caution, "重命名注册表值", $"{_currentPath}\\{newName}", true);
            LoadValues();
        }
    }

    private void DeleteValue_Click(object sender, RoutedEventArgs e)
    {
        if (ValueGrid.SelectedItem is not Core.Registry.RegistryService.RegistryValueItem item)
        {
            MessageBox.Show("请先选择一个值。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"删除注册表值？\n\n键：{_currentPath}\n值：{item.DisplayName}\n数据：{item.DisplayText}",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var error = Core.Registry.RegistryService.DeleteValue(_currentPath, item.Name);
        if (error != null)
        {
            MessageBox.Show($"删除失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RiskFramework.Log(RiskLevel.Caution, "删除注册表值", $"{_currentPath}\\{item.DisplayName}", true);
        LoadValues();
        SetStatus("已删除值。");
    }

    private void CopyValueName_Click(object sender, RoutedEventArgs e)
    {
        if (ValueGrid.SelectedItem is Core.Registry.RegistryService.RegistryValueItem item)
            Clipboard.SetText(item.Name);
    }

    private void CopyValueData_Click(object sender, RoutedEventArgs e)
    {
        if (ValueGrid.SelectedItem is Core.Registry.RegistryService.RegistryValueItem item)
            Clipboard.SetText(item.DisplayText);
    }

    // ==================== 导出与搜索 ====================

    private void ExportBranch_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("正在导出…");
        var content = Core.Registry.RegistryService.ExportBranch(_currentPath);

        new TextViewerDialog($"导出 · {_currentPath}", content)
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();

        SetStatus("导出完成。");
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RegistrySearchDialog(_currentPath) { Owner = Application.Current.MainWindow };
        dialog.Show();
    }

    // ==================== 权限与离线 Hive ====================

    private void ViewAcl_Click(object sender, RoutedEventArgs e)
    {
        var info = SecurityDescriptorService.ReadRegistrySecurity(_currentPath);
        new AclViewerDialog(info) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void LoadHive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择离线注册表 Hive 文件",
            Filter = "注册表 Hive 文件|SOFTWARE;SYSTEM;SAM;SECURITY;NTUSER.DAT;*.dat|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        if (!RiskFramework.IsAllowed(RiskLevel.Dangerous))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Dangerous),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mountName = PromptDialog.Show("加载离线 Hive",
            "挂载到 HKEY_LOCAL_MACHINE 下的临时项名称：", "NE_OfflineHive");

        if (string.IsNullOrWhiteSpace(mountName)) return;

        var error = Core.Registry.RegistryService.LoadHive(dialog.FileName, mountName);
        if (error != null)
        {
            MessageBox.Show(
                $"加载失败：{error}\n\n" +
                "提示：RegLoadKey 需要管理员权限并启用 SeRestorePrivilege / SeBackupPrivilege。\n" +
                "若仍失败，可尝试以 TrustedInstaller 身份运行本程序。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            RiskFramework.Log(RiskLevel.Dangerous, "加载离线 Hive", dialog.FileName, false, string.Empty, error);
            return;
        }

        RiskFramework.Log(RiskLevel.Dangerous, "加载离线 Hive", dialog.FileName, true, $"挂载为 {mountName}");

        _currentPath = $@"HKEY_LOCAL_MACHINE\{mountName}";
        PathBox.Text = _currentPath;
        BuildRootTree();
        LoadValues();

        MessageBox.Show(
            $"已挂载到：{_currentPath}\n\n" +
            "现在可以像操作本机注册表一样修改离线系统的配置。\n" +
            $"用完后请通过「权限与提权」或 bcdedit 卸载：reg unload HKLM\\{mountName}",
            "挂载成功", MessageBoxButton.OK, MessageBoxImage.Information);

        SetStatus($"已挂载离线 Hive 到 {_currentPath}");
    }
}
