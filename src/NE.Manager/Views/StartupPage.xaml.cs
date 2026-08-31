using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class StartupPage : UserControl, IRefreshable
{
    private ObservableCollection<StartupItem> _items = new();

    public StartupPage()
    {
        InitializeComponent();
        ItemGrid.ItemsSource = _items;
        Loaded += (_, _) => Refresh_Click(this, new RoutedEventArgs());
    }

    public void OnEnter() => Refresh_Click(this, new RoutedEventArgs());
    public void OnLeave() { }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        var scan = StartupService.Scan();
        foreach (var it in scan) _items.Add(it);
        CountText.Text = $"共 {scan.Count} 个启动项";
        StatusText.Text = "扫描完成。删除 HKLM Run/RunOnce 项需要管理员权限。";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Show("值名称", "新增 HKCU Run 启动项", "NewItem");
        if (string.IsNullOrEmpty(name)) return;
        var cmd = PromptDialog.Show("命令路径", "新增 HKCU Run 启动项", @"C:\Windows\Notepad.exe");
        if (string.IsNullOrEmpty(cmd)) return;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue(name, cmd, Microsoft.Win32.RegistryValueKind.String);
            MessageBox.Show($"已添加到 HKCU\\Run：{name} = {cmd}");
            Refresh_Click(sender, e);
        }
        catch (Exception ex) { MessageBox.Show("失败：" + ex.Message); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemGrid.SelectedItem is not StartupItem item) return;
        if (MessageBox.Show($"确定删除启动项？\n{item.Name}\n{item.Command}", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        if (item.Location is StartupLocation.UserFolder or StartupLocation.CommonFolder)
        {
            try { File.Delete(item.Command); MessageBox.Show("已删除文件"); Refresh_Click(sender, e); }
            catch (Exception ex) { MessageBox.Show("失败：" + ex.Message); }
        }
        else
        {
            var ok = StartupService.RemoveFromRegistry(item);
            MessageBox.Show(ok ? "已从注册表删除" : "删除失败（可能需要管理员）");
            Refresh_Click(sender, e);
        }
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (ItemGrid.SelectedItem is not StartupItem item) return;
        var ok = StartupService.DisableInRegistry(item);
        MessageBox.Show(ok ? "已禁用（已从启动列表移除）" : "失败");
        Refresh_Click(sender, e);
    }
}
