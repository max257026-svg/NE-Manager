using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Hex;

namespace NEManager.App.Views;

public partial class HexEditorPage : UserControl, IRefreshable
{
    private HexDocument? _doc;
    private long _currentOffset;
    private const int BytesPerRow = 16;
    private const int VisibleRows = 50;

    public HexEditorPage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "打开文件进行 HEX 编辑" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                _doc = new HexDocument();
                _doc.LoadFromFile(dlg.FileName);
                _currentOffset = 0;
                RenderHexView();
                StatusText.Text = $"已加载: {dlg.FileName}";
                SizeText.Text = $"大小: {_doc.Length:N0} 字节";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        try
        {
            _doc.SaveToFile(_doc.FilePath);
            StatusText.Text = $"已保存: {_doc.FilePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        var dlg = new SaveFileDialog { Title = "另存为" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                _doc.SaveToFile(dlg.FileName);
                StatusText.Text = $"已保存: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _doc?.Undo();
        RenderHexView();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _doc?.Redo();
        RenderHexView();
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        var dlg = new PromptDialog("查找字节", "输入要查找的十六进制字节（如 4F 5A）:", "");
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var hexStr = dlg.InputText.Replace(" ", "");
                var pattern = new byte[hexStr.Length / 2];
                for (int i = 0; i < pattern.Length; i++)
                    pattern[i] = Convert.ToByte(hexStr.Substring(i * 2, 2), 16);

                var found = _doc.FindBytes(pattern, _currentOffset, true);
                if (found >= 0)
                {
                    _currentOffset = found;
                    RenderHexView();
                    OffsetText.Text = found.ToString("X8");
                }
                else
                {
                    MessageBox.Show("未找到匹配的字节序列。", "查找结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查找失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void GoTo_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        var dlg = new PromptDialog("跳转到偏移", "输入十六进制偏移地址:", "");
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var offset = Convert.ToInt64(dlg.InputText.Trim(), 16);
                if (offset >= 0 && offset < _doc.Length)
                {
                    _currentOffset = offset;
                    RenderHexView();
                    OffsetText.Text = offset.ToString("X8");
                }
                else
                {
                    MessageBox.Show("偏移地址超出范围。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"跳转失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RenderHexView()
    {
        if (_doc == null) return;

        var addressLines = new System.Text.StringBuilder();
        var hexLines = new System.Text.StringBuilder();
        var asciiLines = new System.Text.StringBuilder();

        long startRow = _currentOffset / BytesPerRow;
        long endRow = Math.Min(startRow + VisibleRows, (_doc.Length + BytesPerRow - 1) / BytesPerRow);

        for (long row = startRow; row < endRow; row++)
        {
            long rowOffset = row * BytesPerRow;
            addressLines.AppendLine(rowOffset.ToString("X8"));

            var hexPart = new System.Text.StringBuilder();
            var asciiPart = new System.Text.StringBuilder();

            for (int col = 0; col < BytesPerRow; col++)
            {
                long byteOffset = rowOffset + col;
                if (byteOffset < _doc.Length)
                {
                    byte b = _doc.Data[byteOffset];
                    hexPart.Append(b.ToString("X2") + " ");
                    asciiPart.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }
                else
                {
                    hexPart.Append("   ");
                    asciiPart.Append(' ');
                }

                if (col == 7) hexPart.Append(' ');
            }

            hexLines.AppendLine(hexPart.ToString());
            asciiLines.AppendLine(asciiPart.ToString());
        }

        AddressColumn.Text = addressLines.ToString();
        HexColumn.Text = hexLines.ToString();
        AsciiColumn.Text = asciiLines.ToString();
    }
}
