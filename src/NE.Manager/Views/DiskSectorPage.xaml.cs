using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Storage;

namespace NEManager.App.Views;

public partial class DiskSectorPage : UserControl, IRefreshable
{
    private List<DiskSectorService.DiskInfo> _disks = new();

    public DiskSectorPage()
    {
        InitializeComponent();
    }

    public void OnEnter()
    {
        LoadDisks();
    }

    public void OnLeave() { }

    private void LoadDisks()
    {
        try
        {
            _disks = DiskSectorService.EnumerateDisks();
            DiskSelector.Items.Clear();
            foreach (var disk in _disks)
            {
                DiskSelector.Items.Add(new ComboBoxItem
                {
                    Content = $"PhysicalDrive{disk.Index} - {disk.SizeText} ({disk.MediaType})",
                    Tag = disk.DevicePath
                });
            }
            if (_disks.Count > 0)
            {
                DiskSelector.SelectedIndex = 0;
                StatusText.Text = $"找到 {_disks.Count} 个磁盘";
            }
            else
            {
                StatusText.Text = "未找到磁盘";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载磁盘列表失败: {ex.Message}";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadDisks();
    }

    private void DiskSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiskSelector.SelectedItem is ComboBoxItem item && item.Tag is string devicePath)
        {
            var disk = _disks.FirstOrDefault(d => d.DevicePath == devicePath);
            if (disk != null)
            {
                var geo = DiskSectorService.GetDiskGeometry(devicePath);
                if (geo != null)
                {
                    DiskInfoText.Text = $"柱面数: {geo.Cylinders} | 每磁道扇区: {geo.SectorsPerTrack} | 每扇区字节: {geo.BytesPerSector}";
                }
            }
        }
    }

    private void ReadSector_Click(object sender, RoutedEventArgs e)
    {
        if (DiskSelector.SelectedItem is not ComboBoxItem item || item.Tag is not string devicePath)
        {
            MessageBox.Show("请先选择磁盘", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!long.TryParse(SectorNumberBox.Text, out var sectorNumber))
        {
            MessageBox.Show("请输入有效的扇区号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sectorSize = int.Parse((SectorSizeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "512");

        try
        {
            StatusText.Text = "正在读取...";
            var sector = DiskSectorService.ReadSector(devicePath, sectorNumber, sectorSize);
            if (sector != null)
            {
                HexView.Text = sector.HexView;
                AsciiView.Text = sector.AsciiView;
                StatusText.Text = $"已读取扇区 {sectorNumber}，大小 {sector.SectorSize} 字节";
            }
            else
            {
                StatusText.Text = "读取失败";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"读取错误: {ex.Message}";
            MessageBox.Show($"读取扇区失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BatchRead_Click(object sender, RoutedEventArgs e)
    {
        if (DiskSelector.SelectedItem is not ComboBoxItem item || item.Tag is not string devicePath)
        {
            MessageBox.Show("请先选择磁盘", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!long.TryParse(SectorNumberBox.Text, out var startSector))
        {
            MessageBox.Show("请输入有效的起始扇区号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sectorSize = int.Parse((SectorSizeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "512");
        var count = 16; // 默认读取 16 个扇区

        try
        {
            StatusText.Text = "正在批量读取...";
            var sectors = DiskSectorService.ReadSectors(devicePath, startSector, count, sectorSize);
            
            var hexSb = new System.Text.StringBuilder();
            var asciiSb = new System.Text.StringBuilder();
            
            foreach (var sector in sectors)
            {
                hexSb.AppendLine($"=== 扇区 {sector.SectorNumber} ===");
                hexSb.AppendLine(sector.HexView);
                asciiSb.AppendLine($"=== 扇区 {sector.SectorNumber} ===");
                asciiSb.AppendLine(sector.AsciiView);
            }

            HexView.Text = hexSb.ToString();
            AsciiView.Text = asciiSb.ToString();
            StatusText.Text = $"已读取 {sectors.Count} 个扇区";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"批量读取错误: {ex.Message}";
            MessageBox.Show($"批量读取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Window
        {
            Title = "搜索字节模式",
            Width = 400,
            Height = 200,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock { Text = "输入十六进制模式 (例如: 4D 5A 90 00):", Margin = new Thickness(0,0,0,5) },
                    new TextBox { Name = "PatternBox", Margin = new Thickness(0,0,0,10) },
                    new Button 
                    { 
                        Content = "搜索", 
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Tag = "Search"
                    }
                }
            }
        };

        dialog.ShowDialog();
    }
}
