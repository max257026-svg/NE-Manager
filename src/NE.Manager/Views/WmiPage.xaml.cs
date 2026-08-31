using System.Data;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Risk;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class WmiPage : UserControl, IRefreshable
{
    public WmiPage()
    {
        InitializeComponent();
        try
        {
            foreach (var q in WmiService.PresetQueries)
                PresetCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{q.Group} / {q.Name}",
                    Tag = q.Query
                });
            if (PresetCombo.Items.Count > 0) PresetCombo.SelectedIndex = 0;
        }
        catch
        {
            // 预设查询加载失败不应阻断页面
        }
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Tag is string query)
            WqlBox.Text = query;
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        var wql = WqlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(wql)) return;

        var result = WmiService.Execute(wql);
        if (!string.IsNullOrEmpty(result.Error))
        {
            StatusText.Text = $"错误：{result.Error}";
            ResultGrid.ItemsSource = null;
            RiskFramework.Log(RiskLevel.Caution, "WMI 查询", wql, false, result.Error);
            return;
        }

        var table = new DataTable();
        foreach (var col in result.Columns)
            table.Columns.Add(col);
        foreach (var row in result.Rows)
        {
            var values = new object[result.Columns.Count];
            for (int i = 0; i < result.Columns.Count; i++)
                values[i] = row.TryGetValue(result.Columns[i], out var v) ? v ?? "" : "";
            table.Rows.Add(values);
        }

        ResultGrid.ItemsSource = table.DefaultView;
        StatusText.Text = $"返回 {result.RowCount} 行，耗时 {result.ElapsedMs} ms";
        RiskFramework.Log(RiskLevel.Safe, "WMI 查询", wql, true, $"{result.RowCount} 行");
    }

    private void Classes_Click(object sender, RoutedEventArgs e)
    {
        var ns = NamespaceBox.Text.Trim();
        var filter = new PromptDialog("浏览 WMI 类", "类名过滤（留空显示全部，如 Win32_）", "Win32_");
        if (filter.ShowDialog() != true) return;
        try
        {
            var classes = WmiService.EnumerateClasses(ns, filter.InputText.Trim());
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"命名空间：{ns}   共 {classes.Count} 个类\n");
            foreach (var c in classes)
                sb.AppendLine($"{c.Name,-40} {c.Description}");
            new TextViewerDialog("WMI 类列表", sb.ToString()) { Owner = Application.Current.MainWindow }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"枚举失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = WmiService.GenerateSystemReport();
            RiskFramework.Log(RiskLevel.Safe, "生成系统报告", "WMI", true);
            new TextViewerDialog("系统信息报告", report) { Owner = Application.Current.MainWindow }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
