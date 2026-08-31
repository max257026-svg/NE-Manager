using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Archive;

namespace NEManager.App.Views;

public partial class ArchivePage : UserControl, IRefreshable
{
    private string _archivePath = "";
    private List<ArchiveEntry> _entries = new();

    public ArchivePage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog 
        { 
            Title = "打开归档文件",
            Filter = "ZIP 文件|*.zip|所有文件|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadArchive(dlg.FileName);
        }
    }

    private void LoadArchive(string path)
    {
        try
        {
            _archivePath = path;
            ArchiveName.Text = System.IO.Path.GetFileName(path);
            _entries = ArchiveService.ListEntries(path);
            
            var displayEntries = _entries.Select(entry => new ArchiveDisplayItem
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                SizeText = FormatSize(entry.Size),
                CompressedSizeText = FormatSize(entry.CompressedSize),
                LastModified = entry.LastModified,
                EntryType = entry.IsDirectory ? "目录" : "文件"
            }).ToList();
            
            ArchiveGrid.ItemsSource = displayEntries;
            
            var totalSize = _entries.Sum(e => e.Size);
            StatusText.Text = $"已加载: {path}";
            EntryCountText.Text = $"{_entries.Count} 项, 总大小: {FormatSize(totalSize)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开归档失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExtractSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ArchiveGrid.SelectedItems.Count == 0)
        {
            MessageBox.Show("请选择要解压的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog { Title = "选择解压目标位置（选择任意文件以获取目录）" };
        if (dlg.ShowDialog() == true)
        {
            var targetDir = System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
            try
            {
                int count = 0;
                foreach (ArchiveDisplayItem item in ArchiveGrid.SelectedItems)
                {
                    var entry = _entries.FirstOrDefault(e => e.FullPath == item.FullPath);
                    if (entry != null && !entry.IsDirectory)
                    {
                        var destPath = System.IO.Path.Combine(targetDir, entry.Name);
                        ArchiveService.ExtractEntry(_archivePath, entry.FullPath, destPath);
                        count++;
                    }
                }
                MessageBox.Show($"成功解压 {count} 个文件。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解压失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_archivePath))
        {
            MessageBox.Show("请先打开归档文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog { Title = "选择解压目标位置（选择任意文件以获取目录）" };
        if (dlg.ShowDialog() == true)
        {
            var targetDir = System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
            try
            {
                ArchiveService.ExtractAll(_archivePath, targetDir);
                MessageBox.Show("解压完成。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解压失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if (ArchiveGrid.SelectedItem is not ArchiveDisplayItem item)
        {
            MessageBox.Show("请选择一个文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var entry = _entries.FirstOrDefault(e => e.FullPath == item.FullPath);
        if (entry == null || entry.IsDirectory)
        {
            MessageBox.Show("无法查看目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), entry.Name);
            ArchiveService.ExtractEntry(_archivePath, entry.FullPath, tempPath);
            
            var previewType = NEManager.Core.Preview.PreviewService.GetPreviewType(tempPath);
            if (previewType == "Text")
            {
                var content = NEManager.Core.Text.LargeTextReader.ReadLines(tempPath, 1000);
                var text = string.Join("\n", content);
                var dlg = new TextViewerDialog(entry.Name, text, true) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
            }
            else
            {
                System.Diagnostics.Process.Start("explorer.exe", tempPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查看失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

public class ArchiveDisplayItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string SizeText { get; set; } = "";
    public string CompressedSizeText { get; set; } = "";
    public DateTime LastModified { get; set; }
    public string EntryType { get; set; } = "";
}
