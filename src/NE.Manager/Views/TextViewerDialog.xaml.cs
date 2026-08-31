using System.Text;
using System.Windows;

namespace NEManager.App.Views;

/// <summary>
/// 通用文本查看器 —— 属性、报告、导出内容都用它展示。
/// </summary>
public partial class TextViewerDialog : Window
{
    public string TextContent { get; }

    public TextViewerDialog(string title, string content, bool mono = true)
    {
        InitializeComponent();
        TextContent = content;
        Title = title;
        TitleText.Text = title;

        ContentBox.Text = content;
        ContentBox.FontFamily = mono
            ? new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Microsoft YaHei Mono, monospace")
            : new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI");
        ContentBox.FontSize = mono ? 12.5 : 13;

        LineCountText.Text = $"{content.Split('\n').Length} 行 · {content.Length} 字符";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ContentBox.Text);
        StatusText.Text = "已复制到剪贴板。";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{string.Join("_", Title.Split(Path.GetInvalidFileNameChars()))}.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, ContentBox.Text, Encoding.UTF8);
            StatusText.Text = $"已保存到：{dialog.FileName}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
