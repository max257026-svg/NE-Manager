using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NEManager.Core.Risk;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class ProcessPage : UserControl, IRefreshable
{
    private readonly ObservableCollection<ProcessManager.ProcessItem> _allProcesses = new();
    private readonly DispatcherTimer _autoRefreshTimer = new();

    public ProcessPage()
    {
        InitializeComponent();
        ProcessGrid.ItemsSource = _allProcesses;

        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(3);
        _autoRefreshTimer.Tick += (_, _) => Refresh();

        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _autoRefreshTimer.Stop();
    }

    public void OnEnter() => Refresh();
    public void OnLeave() => _autoRefreshTimer.Stop();

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    private void Refresh()
    {
        SetStatus("正在枚举进程…");
        var previousSelection = (ProcessGrid.SelectedItem as ProcessManager.ProcessItem)?.Id;

        Task.Run(() =>
        {
            try
            {
                var items = ProcessManager.Enumerate();
                Dispatcher.InvokeAsync(() =>
                {
                    _allProcesses.Clear();
                    foreach (var p in items) _allProcesses.Add(p);

                    ApplyFilter();
                    if (previousSelection.HasValue)
                    {
                        var restored = _allProcesses.FirstOrDefault(p => p.Id == previousSelection.Value);
                        if (restored != null) ProcessGrid.SelectedItem = restored;
                    }
                    SetStatus($"共 {_allProcesses.Count} 个进程");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() => SetStatus($"枚举进程失败：{ex.Message}"));
            }
        });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = FilterBox?.Text?.Trim() ?? string.Empty;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_allProcesses);
        if (string.IsNullOrEmpty(filter))
        {
            view.Filter = null;
            CountText.Text = $"{_allProcesses.Count} 个进程";
        }
        else
        {
            view.Filter = obj => obj is ProcessManager.ProcessItem p &&
                                 (p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                  p.Id.ToString().Contains(filter) ||
                                  (p.UserName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                  (p.Path?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
            CountText.Text = $"{view.Cast<object>().Count()} / {_allProcesses.Count} 个进程";
        }
    }

    private void AutoRefresh_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshBox.IsChecked == true) _autoRefreshTimer.Start();
        else _autoRefreshTimer.Stop();
    }

    private ProcessManager.ProcessItem? Selected => ProcessGrid.SelectedItem as ProcessManager.ProcessItem;

    // ==================== 操作 ====================

    private void Kill_Click(object sender, RoutedEventArgs e) => Kill(Selected, false);
    private void KillTree_Click(object sender, RoutedEventArgs e) => Kill(Selected, true);

    private void Kill(ProcessManager.ProcessItem? item, bool tree)
    {
        if (item == null)
        {
            MessageBox.Show("请先选择一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (item.Id == Environment.ProcessId)
        {
            MessageBox.Show("不能结束 NE 管理器自身进程。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var level = item.IsCritical ? RiskLevel.Critical : RiskLevel.Dangerous;

        var warning = tree
            ? $"确定结束进程树吗？\n\n{item.Name} (PID {item.Id})\n\n所有子进程也会被一并结束。"
            : $"确定结束进程吗？\n\n{item.Name} (PID {item.Id})\n{item.Path}";

        if (item.IsCritical)
        {
            warning += "\n\n⚠️⚠️ 这是 Windows 系统关键进程！\n" +
                       "结束它极有可能导致系统立即蓝屏 (BSOD) 或强制注销。\n" +
                       "所有未保存的工作都会丢失。";
        }
        else if (item.UserName.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            warning += "\n\n⚠️ 该进程以 SYSTEM 身份运行，结束它可能影响系统稳定性。";
        }

        var confirm = MessageBox.Show(warning,
            item.IsCritical ? "⚠️⚠️ 结束系统关键进程" : "确认结束进程",
            MessageBoxButton.YesNo,
            item.IsCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        string? error;
        if (tree)
        {
            var errors = ProcessManager.TerminateTree(item.Id);
            error = errors.Count == 0 ? null : string.Join("; ", errors);
        }
        else
        {
            error = ProcessManager.Terminate(item.Id);
        }

        RiskFramework.Log(level, tree ? "结束进程树" : "结束进程",
            $"{item.Name} (PID {item.Id})", error == null, string.Empty, error);

        if (error != null)
        {
            MessageBox.Show($"结束失败：{error}\n\n" +
                            "提示：受保护进程需要 SeDebugPrivilege，请以管理员身份运行本程序。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            SetStatus($"已结束 {item.Name} (PID {item.Id})。");
        }

        Refresh();
    }

    private void Suspend_Click(object sender, RoutedEventArgs e) => ToggleSuspend(true);
    private void Resume_Click(object sender, RoutedEventArgs e) => ToggleSuspend(false);

    private void ToggleSuspend(bool suspend)
    {
        if (Selected == null)
        {
            MessageBox.Show("请先选择一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var error = suspend ? ProcessManager.Suspend(Selected.Id) : ProcessManager.Resume(Selected.Id);

        RiskFramework.Log(RiskLevel.Dangerous, suspend ? "挂起进程" : "恢复进程",
            $"{Selected.Name} (PID {Selected.Id})", error == null, string.Empty, error);

        if (error != null)
        {
            MessageBox.Show($"操作失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            SetStatus($"已{(suspend ? "挂起" : "恢复")} {Selected.Name}。");
            MessageBox.Show(
                suspend
                    ? $"进程 {Selected.Name} 已挂起（冻结）。\n\n" +
                      "挂起的进程不会占用 CPU，但仍持有内存与句柄。\n" +
                      "常用于暂时冻结恶意程序以便分析。"
                    : $"进程 {Selected.Name} 已恢复运行。",
                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Refresh();
    }

    private void Modules_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请先选择一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var modules = ProcessManager.EnumerateModules(Selected.Id);

        var sb = new StringBuilder();
        sb.AppendLine($"进程：{Selected.Name} (PID {Selected.Id})");
        sb.AppendLine($"路径：{Selected.Path}");
        sb.AppendLine($"加载模块数：{modules.Count}");
        sb.AppendLine();
        sb.AppendLine($"{"模块名",-40}{"基址",-20}{"大小",-12}路径");
        sb.AppendLine(new string('─', 130));

        foreach (var m in modules)
            sb.AppendLine($"{m.Name,-40}{m.BaseText,-20}{m.SizeText,-12}{m.Path}");

        if (modules.Count == 0)
            sb.AppendLine("(无法枚举模块，可能需要 SeDebugPrivilege 或进程已退出)");

        new TextViewerDialog($"模块列表 · {Selected.Name}", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }

    private void Dump_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请先选择一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{Selected.Name}_{Selected.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp",
            Filter = "内存转储 (*.dmp)|*.dmp|所有文件 (*.*)|*.*",
            DefaultExt = ".dmp"
        };

        if (dialog.ShowDialog() != true) return;

        SetStatus("正在转储进程内存…");
        var error = ProcessManager.DumpProcessMemory(Selected.Id, dialog.FileName);

        RiskFramework.Log(RiskLevel.Dangerous, "转储进程内存",
            $"{Selected.Name} (PID {Selected.Id})", error == null, dialog.FileName, error);

        if (error != null)
        {
            MessageBox.Show($"转储失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("转储失败。");
        }
        else
        {
            var size = new FileInfo(dialog.FileName).Length;
            MessageBox.Show($"已转储到：\n{dialog.FileName}\n\n大小：{FileItemFormat(size)}",
                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("转储完成。");
        }
    }

    private static string FileItemFormat(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{bytes} B" : $"{size:0.##} {units[unit]}";
    }

    private void FindLocker_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要检查被哪个进程占用的文件",
            Filter = "所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        var lockers = ProcessManager.FindLockingProcesses(dialog.FileName);

        var sb = new StringBuilder();
        sb.AppendLine($"文件：{dialog.FileName}");
        sb.AppendLine();

        if (lockers.Count == 0)
        {
            sb.AppendLine("✅ 没有进程占用该文件。");
        }
        else
        {
            sb.AppendLine($"⚠️ 以下 {lockers.Count} 个进程正在占用：");
            sb.AppendLine();
            foreach (var p in lockers)
            {
                sb.AppendLine($"  PID {p.Id,-8} {p.Name}");
                if (!string.IsNullOrEmpty(p.Path))
                    sb.AppendLine($"         {p.Path}");
                if (!string.IsNullOrEmpty(p.UserName))
                    sb.AppendLine($"         用户：{p.UserName}{(p.IsElevated ? "（已提权）" : "")}");
                sb.AppendLine();
            }
            sb.AppendLine("提示：结束这些进程后即可删除或修改该文件，");
            sb.AppendLine("或者使用「文件管理 → 注册重启后替换」。");
        }

        new TextViewerDialog("文件占用查询", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null || string.IsNullOrEmpty(Selected.Path)) return;
        try
        {
            Process.Start("explorer.exe", $"/select,\"{Selected.Path}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyCommandLine_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        Clipboard.SetText(string.IsNullOrEmpty(Selected.CommandLine) ? Selected.Path : Selected.CommandLine);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NE 管理器 · 进程清单");
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('═', 120));
        sb.AppendLine();

        foreach (var p in _allProcesses.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"进程名    : {p.Name}");
            sb.AppendLine($"PID       : {p.Id}    父进程: {p.ParentId}");
            sb.AppendLine($"状态      : {p.StatusText}");
            sb.AppendLine($"用户      : {p.UserName}    提权: {p.ElevatedText}");
            sb.AppendLine($"内存      : {p.WorkingSetText}    线程: {p.ThreadCount}    句柄: {p.HandleCount}");
            sb.AppendLine($"路径      : {p.Path}");
            if (!string.IsNullOrEmpty(p.CommandLine))
                sb.AppendLine($"命令行    : {p.CommandLine}");
            sb.AppendLine(new string('─', 120));
            sb.AppendLine();
        }

        new TextViewerDialog("进程清单", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }
}
