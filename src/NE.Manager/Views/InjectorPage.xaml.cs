using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Injection;

namespace NEManager.App.Views;

public partial class InjectorPage : UserControl, IRefreshable
{
    public InjectorPage() => InitializeComponent();

    public void OnEnter() => RefreshProcessList();
    public void OnLeave() { }

    private void RefreshProcessList()
    {
        try
        {
            var procs = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                .OrderBy(p => p.ProcessName)
                .ToList();
            ProcessGrid.ItemsSource = procs;
            ResultBox.Text = $"已加载 {procs.Count} 个进程。双击选择或手动输入 PID。";
        }
        catch (Exception ex)
        {
            ResultBox.Text = $"加载进程失败：{ex.Message}";
        }
    }

    private void RefreshProcs_Click(object sender, RoutedEventArgs e) => RefreshProcessList();

    private void BrowseDll_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*",
            Title = "选择要注入的 DLL"
        };
        if (dlg.ShowDialog() == true) DllPathBox.Text = dlg.FileName;
    }

    private void ProcessGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProcessGrid.SelectedItem is Process p)
        {
            PidBox.Text = p.Id.ToString();
            ResultBox.Text = $"已选中：{p.ProcessName} (PID {p.Id})";
        }
    }

    private void Inject_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PidBox.Text.Trim(), out var pid) || pid <= 0)
        {
            ResultBox.Text = "请输入有效的目标 PID";
            return;
        }
        if (string.IsNullOrWhiteSpace(DllPathBox.Text) || !File.Exists(DllPathBox.Text))
        {
            ResultBox.Text = "请选择一个存在的 DLL 文件";
            return;
        }

        var confirm = MessageBox.Show(
            $"即将向 PID {pid} 注入：\n{DllPathBox.Text}\n\n确定继续吗？",
            "确认注入", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        InjectBtn.IsEnabled = false;
        ResultBox.Text = "注入中...";

        _ = System.Threading.Tasks.Task.Run(() => DllInjector.Inject(pid, DllPathBox.Text))
            .ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    InjectBtn.IsEnabled = true;
                    var r = t.Result;
                    ResultBox.Text = r.Success
                        ? $"[OK] {r.Message}"
                        : $"[FAIL] {r.Message}";
                });
            });
    }
}
