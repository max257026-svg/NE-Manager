using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NEManager.Core.FileSystem;

namespace NEManager.App.Views;

public partial class MacFileSystemPage : UserControl
{
    private string _currentPath = "";
    private List<Ext4FileEntry> _allFiles = new();

    public MacFileSystemPage()
    {
        InitializeComponent();
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 macOS 文件系统镜像",
            Filter = "镜像文件 (*.dmg;*.sparsebundle;*.img;*.iso)|*.dmg;*.sparsebundle;*.img;*.iso|所有文件 (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            ImagePathBox.Text = dlg.FileName;
            LoadImage(dlg.FileName);
        }
    }

    private void ImagePathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 可选：实时验证路径
    }

    private void AnalyzeFileSystem_Click(object sender, RoutedEventArgs e)
    {
        var imagePath = ImagePathBox.Text.Trim();
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            MessageBox.Show("请先选择有效的镜像文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fsType = MacFileSystemService.DetectMacFileSystem(imagePath);
            var dmgInfo = MacFileSystemService.ReadDmgInfo(imagePath);

            var info = $"文件系统类型: {fsType}\n\n";
            info += $"DMG 信息:\n";
            info += $"  格式: {dmgInfo.Format}\n";
            info += $"  数据大小: {FormatSize(dmgInfo.DataSize)}\n";
            info += $"  卷名: {dmgInfo.VolumeName}\n";

            if (!string.IsNullOrEmpty(dmgInfo.Error))
            {
                info += $"  错误: {dmgInfo.Error}\n";
            }

            MessageBox.Show(info, "文件系统分析", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"分析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadImage(string imagePath)
    {
        try
        {
            StatusText.Text = "正在加载...";
            _currentPath = imagePath;
            PathText.Text = imagePath;

            // macOS 文件系统解析较复杂，这里提供示例数据
            _allFiles = new List<Ext4FileEntry>
            {
                new Ext4FileEntry { Name = "(macOS 镜像 - 完整解析需要专用库)", Size = 0, IsDirectory = true, Mode = "drwxr-xr-x" }
            };

            FileList.ItemsSource = _allFiles.Select(f => new
            {
                Icon = f.IsDirectory ? "\uE8B7" : "\uE8A5",
                f.Name,
                SizeText = f.IsDirectory ? "" : FormatSize(f.Size),
                ModifiedTime = "",
                f.Mode,
                Owner = "",
                Group = ""
            }).ToList();

            CountText.Text = $"{_allFiles.Count} 项";
            StatusText.Text = "就绪";
            BackButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败: {ex.Message}";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath))
        {
            LoadImage(_currentPath);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath))
        {
            LoadImage(_currentPath);
        }
    }

    private void Extract_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("macOS 文件系统完整提取需要专用库支持，当前版本仅提供基本信息读取", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem != null)
        {
            MessageBox.Show("macOS 目录浏览需要完整解析实现，当前版本为演示模式", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = SearchBox.Text.Trim().ToLower();
        if (string.IsNullOrEmpty(keyword))
        {
            FileList.ItemsSource = _allFiles.Select(f => new
            {
                Icon = f.IsDirectory ? "\uE8B7" : "\uE8A5",
                f.Name,
                SizeText = f.IsDirectory ? "" : FormatSize(f.Size),
                ModifiedTime = "",
                f.Mode,
                Owner = "",
                Group = ""
            }).ToList();
        }
        else
        {
            var filtered = _allFiles.Where(f => f.Name.ToLower().Contains(keyword)).ToList();
            FileList.ItemsSource = filtered.Select(f => new
            {
                Icon = f.IsDirectory ? "\uE8B7" : "\uE8A5",
                f.Name,
                SizeText = f.IsDirectory ? "" : FormatSize(f.Size),
                ModifiedTime = "",
                f.Mode,
                Owner = "",
                Group = ""
            }).ToList();
            CountText.Text = $"{filtered.Count} / {_allFiles.Count} 项";
        }
    }

    private static string FormatSize(long size)
    {
        return size switch
        {
            > 1_000_000_000 => $"{size / 1_000_000_000.0:F1} GB",
            > 1_000_000 => $"{size / 1_000_000.0:F1} MB",
            > 1_000 => $"{size / 1_000.0:F1} KB",
            _ => $"{size} B"
        };
    }
}
