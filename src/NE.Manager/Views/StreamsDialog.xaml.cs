using System.Text;
using System.Windows;
using NEManager.Core.FileSystem;

namespace NEManager.App.Views;

/// <summary>
/// 备用数据流 (ADS) 管理窗口 —— 查看、读取、新建、导出、删除。
/// </summary>
public partial class StreamsDialog : Window
{
    private readonly string _filePath;
    private List<AlternateDataStreamService.StreamEntry> _streams;

    public StreamsDialog(string filePath, List<AlternateDataStreamService.StreamEntry> streams)
    {
        InitializeComponent();
        _filePath = filePath;
        _streams = streams;
        FilePathText.Text = filePath;
        StreamGrid.ItemsSource = _streams;
    }

    private AlternateDataStreamService.StreamEntry? Selected => StreamGrid.SelectedItem as AlternateDataStreamService.StreamEntry;

    private void StreamGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Selected != null) View_Click(sender, e);
    }

    private void View_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请选择一个数据流。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var data = AlternateDataStreamService.ReadStream(_filePath, Selected.Name);
        if (data.Length == 0)
        {
            ContentBox.Text = "(流为空或无法读取)";
            return;
        }

        // 尝试按 UTF-8 解码，失败则显示十六进制
        try
        {
            var text = Encoding.UTF8.GetString(data);
            var replacementCount = text.Count(c => c == '\uFFFD');
            ContentBox.Text = replacementCount > data.Length / 10
                ? FormatHex(data)
                : text;
        }
        catch
        {
            ContentBox.Text = FormatHex(data);
        }
    }

    private static string FormatHex(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Math.Min(data.Length, 8192); i += 16)
        {
            sb.Append($"{i:X8}  ");
            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
            }
            sb.Append("  ");
            for (int j = 0; j < 16 && i + j < data.Length; j++)
            {
                var b = data[i + j];
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            }
            sb.AppendLine();
        }
        if (data.Length > 8192) sb.AppendLine($"\n… 仅显示前 8192 字节（共 {data.Length} 字节）");
        return sb.ToString();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Show("新建数据流", "数据流名称（不需要冒号）：", "MyStream");
        if (string.IsNullOrWhiteSpace(name)) return;

        var error = AlternateDataStreamService.CreateStream(_filePath, name.TrimStart(':'));
        if (error != null)
        {
            MessageBox.Show($"创建失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Refresh();
        MessageBox.Show($"已创建数据流：{name}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请选择一个数据流。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = Selected.CleanName,
            Filter = "所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        var data = AlternateDataStreamService.ReadStream(_filePath, Selected.Name);
        File.WriteAllBytes(dialog.FileName, data);
        MessageBox.Show($"已导出 {data.Length} 字节到：\n{dialog.FileName}", "完成",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null)
        {
            MessageBox.Show("请选择一个数据流。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确定删除数据流「{Selected.CleanName}」吗？\n\n" +
            $"{_filePath}\n\n" +
            "⚠️ 删除后数据无法恢复。若是 Zone.Identifier，删除将解除文件的「来自互联网」封锁标记。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var error = AlternateDataStreamService.DeleteStream(_filePath, Selected.Name);
        if (error != null)
        {
            MessageBox.Show($"删除失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Refresh();
        ContentBox.Text = string.Empty;
        MessageBox.Show("已删除。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Refresh()
    {
        _streams = AlternateDataStreamService.Enumerate(_filePath)
            .Where(s => !s.Name.Equals("::$DATA", StringComparison.Ordinal)).ToList();
        StreamGrid.ItemsSource = _streams;
        StreamGrid.Items.Refresh();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
