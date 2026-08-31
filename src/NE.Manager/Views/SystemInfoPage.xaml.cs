using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class SystemInfoPage : UserControl
{
    public SystemInfoPage() { InitializeComponent(); Loaded += (_, _) => Refresh(); }

    private void Refresh()
    {
        try
        {
            var info = SystemInfoService.Collect();
            OsGrid.ItemsSource = new List<KeyValuePair<string, string>>(info.OS);
            HwGrid.ItemsSource = new List<KeyValuePair<string, string>>(info.Hardware);
            AppsGrid.ItemsSource = info.InstalledApps;
            DrvGrid.ItemsSource = info.Drivers;
            NetGrid.ItemsSource = info.Network;
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取系统信息失败: " + ex.Message, "错误");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = $"NE-SystemInfo-{DateTime.Now:yyyyMMdd-HHmmss}.md", Filter = "Markdown (*.md)|*.md" };
        if (dlg.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(dlg.FileName, SystemInfoService.ExportMarkdown());
            MessageBox.Show("已导出到 " + dlg.FileName, "完成");
        }
    }
}
