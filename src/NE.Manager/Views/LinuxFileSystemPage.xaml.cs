using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NEManager.Core.FileSystem;

namespace NEManager.App.Views;

public partial class LinuxFileSystemPage : UserControl
{
    private string _currentPath = "";
    private List<Ext4FileEntry> _allFiles = new();

    public LinuxFileSystemPage()
    {
        InitializeComponent();
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 Linux 文件系统镜像",
            Filter = "镜像文件 (*.img;*.iso;*.ext4)|*.img;*.iso;*.ext4|所有文件 (*.*)|*.*"
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

    private void AnalyzeSuperBlock_Click(object sender, RoutedEventArgs e)
    {
        var imagePath = ImagePathBox.Text.Trim();
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            MessageBox.Show("请先选择有效的镜像文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fsType = LinuxFileSystemService.DetectFileSystemType(imagePath);
            var superBlock = LinuxFileSystemService.ReadExt4SuperBlock(imagePath);

            var info = $"文件系统类型: {fsType}\n\n";
            if (superBlock != null)
            {
                info += $"EXT4 超级块信息:\n";
                info += $"  Inode 总数: {superBlock.InodesCount}\n";
                info += $"  Block 总数: {superBlock.BlocksCount}\n";
                info += $"  Block 大小: {superBlock.BlockSize} bytes\n";
                info += $"  Frag 大小: {superBlock.FragSize} bytes\n";
                info += $"  每组 Blocks: {superBlock.BlocksPerGroup}\n";
                info += $"  每组 Inodes: {superBlock.InodesPerGroup}\n";
                info += $"  卷名: {superBlock.VolumeName}\n";
                info += $"  Magic: {superBlock.Magic}\n";
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

            _allFiles = LinuxFileSystemService.ListExt4Files(imagePath);
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
        // 返回上一级（简化处理）
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
        MessageBox.Show("EXT4 完整文件提取需要专用库支持，当前版本仅提供基本信息读取", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem != null)
        {
            MessageBox.Show("EXT4 目录浏览需要完整解析实现，当前版本为演示模式", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
