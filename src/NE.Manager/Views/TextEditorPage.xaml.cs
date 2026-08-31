using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NEManager.Core.Text;

namespace NEManager.App.Views;

public partial class TextEditorPage : UserControl, IRefreshable
{
    private string _currentFile = "";
    private Encoding _currentEncoding = Encoding.UTF8;
    private List<string> _lines = new();
    private int _lastSearchIndex = -1;

    public TextEditorPage()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeEncodingSelector();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void InitializeEncodingSelector()
    {
        var encodings = NEManager.Core.Tools.EncodingService.SupportedEncodings;
        EncodingSelector.ItemsSource = encodings.Select(e => e.name).ToList();
        if (encodings.Count > 0)
            EncodingSelector.SelectedIndex = 0;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "打开文本文件" };
        if (dlg.ShowDialog() == true)
        {
            LoadFile(dlg.FileName);
        }
    }

    private void LoadFile(string path)
    {
        try
        {
            _currentFile = path;
            _currentEncoding = LargeTextReader.DetectEncoding(path);
            _lines = LargeTextReader.ReadLines(path, 10000);

            var text = string.Join("\n", _lines);
            TextContent.Text = text;

            UpdateLineNumbers();
            UpdateStatus();

            StatusText.Text = $"已加载: {Path.GetFileName(path)}";
            EncodingText.Text = $"编码: {_currentEncoding.EncodingName}";
            LineCountText.Text = $"行数: {_lines.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateLineNumbers()
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= _lines.Count; i++)
        {
            sb.AppendLine(i.ToString());
        }
        LineNumbers.Text = sb.ToString();
    }

    private void UpdateStatus()
    {
        var pos = TextContent.CaretIndex;
        var text = TextContent.Text;
        var line = text.Substring(0, pos).Count(c => c == '\n') + 1;
        var col = pos - text.LastIndexOf('\n', pos > 0 ? pos - 1 : 0);
        CursorText.Text = $"行 {line}, 列 {col}";
    }

    private void TextContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void Encoding_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EncodingSelector.SelectedIndex < 0) return;
        var encodings = NEManager.Core.Tools.EncodingService.SupportedEncodings;
        if (EncodingSelector.SelectedIndex < encodings.Count)
        {
            _currentEncoding = encodings[EncodingSelector.SelectedIndex].encoding;
            if (!string.IsNullOrEmpty(_currentFile))
            {
                LoadFile(_currentFile);
            }
        }
    }

    private void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Find_Click(sender, e);
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        _lastSearchIndex = -1;
        FindNext_Click(sender, e);
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var searchText = SearchBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = TextContent.Text;
        var startIndex = _lastSearchIndex + 1;

        var index = text.IndexOf(searchText, startIndex, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            TextContent.Select(index, searchText.Length);
            TextContent.ScrollToLine(text.Substring(0, index).Count(c => c == '\n'));
            _lastSearchIndex = index;
        }
        else
        {
            MessageBox.Show("未找到匹配项。", "查找结果", MessageBoxButton.OK, MessageBoxImage.Information);
            _lastSearchIndex = -1;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFile))
        {
            var dlg = new SaveFileDialog { Title = "保存文件" };
            if (dlg.ShowDialog() == true)
            {
                _currentFile = dlg.FileName;
            }
            else
            {
                return;
            }
        }

        try
        {
            File.WriteAllText(_currentFile, TextContent.Text, _currentEncoding);
            StatusText.Text = $"已保存: {Path.GetFileName(_currentFile)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
