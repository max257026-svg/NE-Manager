using System.Collections.ObjectModel;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Risk;
using NEManager.Core.Security;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class ServicePage : UserControl, IRefreshable
{
    private readonly ObservableCollection<ServiceManager.ServiceItem> _services = new();

    public ServicePage()
    {
        InitializeComponent();
        ServiceGrid.ItemsSource = _services;
        Loaded += (_, _) => Refresh();
    }

    public void OnEnter() => Refresh();
    public void OnLeave() { }

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    private void Refresh()
    {
        SetStatus("正在枚举服务…");
        var previous = (ServiceGrid.SelectedItem as ServiceManager.ServiceItem)?.Name;

        Task.Run(() =>
        {
            try
            {
                var items = ServiceManager.Enumerate();
                Dispatcher.InvokeAsync(() =>
                {
                    _services.Clear();
                    foreach (var s in items) _services.Add(s);

                    ApplyFilter();
                    if (previous != null)
                    {
                        var restored = _services.FirstOrDefault(s => s.Name == previous);
                        if (restored != null) ServiceGrid.SelectedItem = restored;
                    }

                    SetStatus($"共 {_services.Count} 项服务与驱动");
                    if (!PrivilegeService.IsElevated())
                        SetStatus("⚠️ 当前非管理员权限：启停服务、修改启动类型、删除服务都会失败。");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() => SetStatus($"枚举服务失败：{ex.Message}"));
            }
        });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void IncludeDrivers_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!IsLoaded) return; // IncludeDriversBox 的 IsChecked=True 会在 InitializeComponent 期间触发
        var filter = FilterBox?.Text?.Trim() ?? string.Empty;
        bool includeDrivers = IncludeDriversBox?.IsChecked != false;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_services);
        view.Filter = obj =>
        {
            if (obj is not ServiceManager.ServiceItem s) return false;
            if (!includeDrivers && s.IsDriver) return false;

            return string.IsNullOrEmpty(filter)
                   || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                   || s.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                   || (s.BinaryPath?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
        };

        CountText.Text = $"{view.Cast<object>().Count()} / {_services.Count} 项";
    }

    private ServiceManager.ServiceItem? Selected => ServiceGrid.SelectedItem as ServiceManager.ServiceItem;

    // ==================== 控制 ====================

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var error = ServiceManager.Start(Selected.Name);
        Report("启动服务", Selected, error);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;

        if (Selected.Name.Equals("TrustedInstaller", StringComparison.OrdinalIgnoreCase) ||
            Selected.IsDriver)
        {
            var confirm = MessageBox.Show(
                $"⚠️ 你正在停止一个{(Selected.IsDriver ? "内核驱动" : "系统关键服务")}：\n\n" +
                $"{Selected.DisplayName}\n\n" +
                "停止它可能导致系统功能异常甚至蓝屏。确定继续吗？",
                "⚠️ 高危操作", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;
        }

        var error = ServiceManager.Stop(Selected.Name);
        Report("停止服务", Selected, error);
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var error = ServiceManager.Restart(Selected.Name);
        Report("重启服务", Selected, error);
    }

    private void Report(string operation, ServiceManager.ServiceItem item, string? error)
    {
        RiskFramework.Log(RiskLevel.Caution, operation, item.Name, error == null, string.Empty, error);

        if (error != null)
        {
            MessageBox.Show($"{operation}失败：{error}\n\n服务控制需要管理员权限。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"{operation}失败。");
        }
        else
        {
            SetStatus($"{operation}成功：{item.DisplayName}");
        }

        Refresh();
    }

    // ==================== 启动类型 ====================

    private void ApplyStartType_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请先选择一个服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var tag = ((ComboBoxItem)StartTypeBox.SelectedItem).Tag?.ToString();
        var mode = tag switch
        {
            "Automatic" => ServiceStartMode.Automatic,
            "Manual" => ServiceStartMode.Manual,
            "Disabled" => ServiceStartMode.Disabled,
            _ => ServiceStartMode.Manual
        };

        var error = ServiceManager.ChangeStartType(Selected.Name, mode);
        RiskFramework.Log(RiskLevel.Dangerous, "修改服务启动类型", Selected.Name, error == null,
            $"→ {mode}", error);

        MessageBox.Show(error == null
            ? $"已将 {Selected.DisplayName} 的启动类型改为「{tag}」。"
            : $"修改失败：{error}",
            error == null ? "完成" : "错误",
            MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);

        Refresh();
    }

    private void SetAutomatic_Click(object sender, RoutedEventArgs e) => SetStartType(ServiceStartMode.Automatic, "自动");
    private void SetManual_Click(object sender, RoutedEventArgs e) => SetStartType(ServiceStartMode.Manual, "手动");
    private void SetDisabled_Click(object sender, RoutedEventArgs e) => SetStartType(ServiceStartMode.Disabled, "禁用");

    private void SetStartType(ServiceStartMode mode, string name)
    {
        if (Selected == null) return;

        var error = ServiceManager.ChangeStartType(Selected.Name, mode);
        RiskFramework.Log(RiskLevel.Dangerous, "修改服务启动类型", Selected.Name, error == null, $"→ {name}", error);

        if (error != null)
            MessageBox.Show($"修改失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        else
            SetStatus($"已将 {Selected.DisplayName} 设为「{name}」。");

        Refresh();
    }

    // ==================== 高危配置 ====================

    private void ChangePath_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;

        if (!RiskFramework.IsAllowed(RiskLevel.Critical))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Critical),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newPath = PromptDialog.Show("修改服务可执行路径",
            $"⚠️ 这会改变服务启动时运行的程序。\n\n服务：{Selected.Name}\n当前路径：{Selected.BinaryPath}\n\n新路径：",
            Selected.BinaryPath);

        if (string.IsNullOrWhiteSpace(newPath) || newPath == Selected.BinaryPath) return;

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Critical, "修改服务二进制路径", Selected.Name, new[]
            {
                "服务下次启动时将运行你指定的程序",
                "这是恶意软件常用的持久化手法，杀软会告警",
                "若路径无效，服务将无法启动"
            }),
            "⚠️ 极高危操作", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var error = ServiceManager.ChangeBinaryPath(Selected.Name, newPath);
        RiskFramework.Log(RiskLevel.Critical, "修改服务二进制路径", Selected.Name, error == null,
            $"{Selected.BinaryPath} → {newPath}", error);

        MessageBox.Show(error == null ? "已修改。" : $"修改失败：{error}",
            error == null ? "完成" : "错误", MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);

        Refresh();
    }

    private void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;

        if (!RiskFramework.IsAllowed(RiskLevel.Critical))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Critical),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Critical, "删除服务", Selected.Name, new[]
            {
                "服务注册项将被永久移除",
                "依赖它的其它服务将无法启动",
                "删除关键系统服务会导致系统功能缺失"
            }),
            "⚠️ 极高危操作", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var error = ServiceManager.DeleteService(Selected.Name);
        RiskFramework.Log(RiskLevel.Critical, "删除服务", Selected.Name, error == null, string.Empty, error);

        MessageBox.Show(error == null ? "已删除。" : $"删除失败：{error}",
            error == null ? "完成" : "错误", MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);

        Refresh();
    }

    private void CreateService_Click(object sender, RoutedEventArgs e)
    {
        if (!RiskFramework.IsAllowed(RiskLevel.Critical))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Critical),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var name = PromptDialog.Show("新建服务", "服务名称：", "NewEraService");
        if (string.IsNullOrWhiteSpace(name)) return;

        var display = PromptDialog.Show("新建服务", "显示名称：", name);
        var binary = PromptDialog.Show("新建服务", "可执行文件路径：", string.Empty);

        if (string.IsNullOrWhiteSpace(binary))
        {
            MessageBox.Show("可执行文件路径不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var error = ServiceManager.CreateService(name, display ?? name, binary);
        RiskFramework.Log(RiskLevel.Critical, "创建服务", name, error == null, binary, error);

        MessageBox.Show(error == null ? $"已创建服务 {name}。" : $"创建失败：{error}",
            error == null ? "完成" : "错误", MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);

        Refresh();
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (Selected != null) Clipboard.SetText(Selected.BinaryPath);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("正在生成服务清单…");
        var report = ServiceManager.ExportInventory();

        new TextViewerDialog("服务配置清单", report)
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();

        SetStatus("清单已生成。");
    }
}
