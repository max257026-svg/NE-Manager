using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NEManager.App.Views;
using NEManager.Core.Risk;
using NEManager.Core.Security;
using NEManager.Core.Storage;

namespace NEManager.App.Views;

public partial class DiskPage : UserControl, IRefreshable
{
    private IntPtr _vhdHandle = IntPtr.Zero;
    private string _lastVhdPath = string.Empty;
    private string _lastIsoPath = string.Empty;

    public DiskPage()
    {
        InitializeComponent();
    }

    public void OnEnter() => Refresh();
    public void OnLeave() { }

    private void Refresh()
    {
        try { VolumeGrid.ItemsSource = VolumeService.EnumerateVolumes(); }
        catch (Exception ex) { RiskFramework.Log(RiskLevel.Caution, "枚举卷", "卷", false, ex.Message); }

        try { DriveGrid.ItemsSource = VolumeService.EnumeratePhysicalDrives(); }
        catch (Exception ex) { RiskFramework.Log(RiskLevel.Caution, "枚举物理磁盘", "磁盘", false, ex.Message); }
    }

    private void VolumeGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VolumeGrid.SelectedItem is VolumeService.VolumeItem vol && !string.IsNullOrEmpty(vol.MountPoints))
        {
            var mp = vol.MountPoints.Split(';')[0].TrimEnd('\\');
            ((MainWindow)Application.Current.MainWindow).OpenPathInFiles(mp);
        }
    }

    private void MountVhd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "虚拟磁盘 (*.vhd;*.vhdx)|*.vhd;*.vhdx",
            Title = "选择虚拟磁盘文件"
        };
        if (dlg.ShowDialog() != true) return;

        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("挂载 VHD 后修改其内系统文件属于高危离线操作，需要管理员权限。", "需要管理员",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = VolumeService.MountVirtualDisk(dlg.FileName, readOnly: true);
        if (result.Success)
        {
            _vhdHandle = result.Handle;
            _lastVhdPath = dlg.FileName;
            RiskFramework.Log(RiskLevel.Dangerous, "挂载虚拟磁盘", dlg.FileName, true, result.MountedPath);
            var open = MessageBox.Show(
                $"VHD 已挂载到：{result.MountedPath}\n\n是否立即在文件管理器中打开？",
                "挂载成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                ((MainWindow)Application.Current.MainWindow).OpenPathInFiles(result.MountedPath);
        }
        else
        {
            RiskFramework.Log(RiskLevel.Dangerous, "挂载虚拟磁盘", dlg.FileName, false, result.Message);
            MessageBox.Show($"挂载失败：{result.Message}", "挂载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UnmountVhd_Click(object sender, RoutedEventArgs e)
    {
        if (_vhdHandle == IntPtr.Zero)
        {
            MessageBox.Show("当前没有已挂载的 VHD。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var err = VolumeService.UnmountVirtualDisk(_vhdHandle);
        if (err == null)
        {
            RiskFramework.Log(RiskLevel.Dangerous, "卸载虚拟磁盘", _lastVhdPath, true);
            _vhdHandle = IntPtr.Zero;
            MessageBox.Show("VHD 已卸载。", "卸载成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
            MessageBox.Show($"卸载失败：{err}", "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void MountIso_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "光盘镜像 (*.iso)|*.iso", Title = "选择 ISO 文件" };
        if (dlg.ShowDialog() != true) return;

        var result = VolumeService.MountIso(dlg.FileName);
        if (result.Success)
        {
            _lastIsoPath = dlg.FileName;
            RiskFramework.Log(RiskLevel.Caution, "挂载 ISO", dlg.FileName, true, result.MountedPath);
            var open = MessageBox.Show($"ISO 已挂载到：{result.MountedPath}\n\n是否立即在文件管理器中打开？",
                "挂载成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                ((MainWindow)Application.Current.MainWindow).OpenPathInFiles(result.MountedPath);
        }
        else
        {
            RiskFramework.Log(RiskLevel.Caution, "挂载 ISO", dlg.FileName, false, result.Message);
            MessageBox.Show($"挂载失败：{result.Message}", "挂载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UnmountIso_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastIsoPath))
        {
            MessageBox.Show("当前没有已挂载的 ISO。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var err = VolumeService.UnmountIso(_lastIsoPath);
        if (err == null)
        {
            RiskFramework.Log(RiskLevel.Caution, "卸载 ISO", _lastIsoPath, true);
            _lastIsoPath = string.Empty;
            MessageBox.Show("ISO 已卸载。", "卸载成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
            MessageBox.Show($"卸载失败：{err}", "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Shadow_Click(object sender, RoutedEventArgs e)
    {
        var input = new PromptDialog("创建卷影副本", "输入卷盘符（如 C）", "C");
        if (input.ShowDialog() != true) return;

        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("创建卷影副本需要管理员权限（卷影复制服务）。", "需要管理员",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var letter = input.InputText.Trim().TrimEnd(':');
        var (ok, message) = VolumeService.CreateShadowCopy(letter, out string snapshotPath);
        if (ok)
        {
            RiskFramework.Log(RiskLevel.Dangerous, "创建卷影副本", $"{letter}:\\", true, snapshotPath);
            var open = MessageBox.Show($"卷影副本已创建：\n{snapshotPath}\n\n是否浏览快照内文件（用于恢复被篡改的系统文件）？",
                "创建成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                ((MainWindow)Application.Current.MainWindow).OpenPathInFiles(snapshotPath);
        }
        else
        {
            RiskFramework.Log(RiskLevel.Dangerous, "创建卷影副本", $"{letter}:\\", false, message);
            MessageBox.Show($"创建失败：{message}", "创建失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Bcd_Click(object sender, RoutedEventArgs e)
    {
        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("导出 BCD 启动配置需要管理员权限。", "需要管理员",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var text = VolumeService.ExportBcdConfiguration();
        RiskFramework.Log(RiskLevel.Dangerous, "导出 BCD 配置", "BCD", true);
        new TextViewerDialog("BCD 启动配置", text) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void BootTimeout_Click(object sender, RoutedEventArgs e)
    {
        var input = new PromptDialog("设置启动菜单超时", "输入秒数（如 5）", "5");
        if (input.ShowDialog() != true) return;
        if (!int.TryParse(input.InputText.Trim(), out int sec) || sec < 0)
        {
            MessageBox.Show("请输入有效的秒数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("修改 BCD 启动超时需要管理员权限。", "需要管理员",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var err = VolumeService.SetBootTimeout(sec);
        if (err == null)
        {
            RiskFramework.Log(RiskLevel.Dangerous, "设置启动超时", $"{sec} 秒", true);
            MessageBox.Show($"启动超时已设置为 {sec} 秒。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            RiskFramework.Log(RiskLevel.Dangerous, "设置启动超时", $"{sec} 秒", false, err);
            MessageBox.Show($"失败：{err}", "失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Raw_Click(object sender, RoutedEventArgs e)
    {
        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("原始磁盘扇区访问需要管理员权限。写入操作属于最高危操作，可能导致数据永久损坏。",
                "需要管理员", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var path = new PromptDialog("原始扇区读取", "设备路径（如 \\\\.\\PhysicalDrive0 或 \\\\.\\C:）", "\\\\.\\PhysicalDrive0");
        if (path.ShowDialog() != true) return;
        var off = new PromptDialog("起始偏移（扇区）", "从哪个扇区开始（十进制）", "0");
        if (off.ShowDialog() != true) return;
        var len = new PromptDialog("读取扇区数", "读取多少扇区（每扇区 512 字节）", "16");
        if (len.ShowDialog() != true) return;

        if (!long.TryParse(off.InputText.Trim(), out long sector) || sector < 0 ||
            !int.TryParse(len.InputText.Trim(), out int count) || count <= 0 || count > 4096)
        {
            MessageBox.Show("参数无效（扇区数需在 1-4096 之间）。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (success, error, data) = VolumeService.ReadSectors(path.InputText.Trim(), sector * 512L, count * 512);
        if (!success)
        {
            RiskFramework.Log(RiskLevel.Critical, "原始扇区读取", path.InputText, false, error);
            MessageBox.Show($"读取失败：{error}", "读取失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        RiskFramework.Log(RiskLevel.Critical, "原始扇区读取", $"{path.InputText} @sector {sector} x{count}", true);
        new TextViewerDialog($"扇区转储 {path.InputText} (sector {sector})", FormatHex(data, sector * 512L))
            { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private static string FormatHex(byte[] data, long baseOffset)
    {
        var sb = new StringBuilder();
        const int bytesPerLine = 16;
        for (int i = 0; i < data.Length; i += bytesPerLine)
        {
            sb.Append($"{(baseOffset + i):X12}  ");
            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < data.Length) sb.Append($"{data[i + j]:X2} ");
                else sb.Append("   ");
            }
            sb.Append(" ");
            for (int j = 0; j < bytesPerLine && i + j < data.Length; j++)
            {
                var b = data[i + j];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
