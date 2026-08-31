using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Tools;

namespace NEManager.App.Views;

public partial class ToolboxPage : UserControl, IRefreshable
{
    public ToolboxPage()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeEncodingSelectors();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void InitializeEncodingSelectors()
    {
        var encodings = EncodingService.SupportedEncodings;
        foreach (var (name, _) in encodings)
        {
            FromEncoding.Items.Add(name);
            ToEncoding.Items.Add(name);
        }
        if (encodings.Count > 0)
        {
            FromEncoding.SelectedIndex = 0;
            ToEncoding.SelectedIndex = Math.Min(1, encodings.Count - 1);
        }
    }

    // 进制转换
    private void ConverterInput_Changed(object sender, TextChangedEventArgs e)
    {
        var input = ConverterInput.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            BinaryOutput.Text = "";
            OctalOutput.Text = "";
            DecimalOutput.Text = "";
            HexOutput.Text = "";
            return;
        }

        try
        {
            long value;
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(input.Substring(2), 16);
            else if (input.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(input.Substring(2), 2);
            else if (input.StartsWith("0", StringComparison.OrdinalIgnoreCase) && input.Length > 1 && input.All(c => c >= '0' && c <= '7'))
                value = Convert.ToInt64(input.Substring(1), 8);
            else
                value = Convert.ToInt64(input);

            BinaryOutput.Text = "0b" + Convert.ToString(value, 2);
            OctalOutput.Text = "0" + Convert.ToString(value, 8);
            DecimalOutput.Text = value.ToString();
            HexOutput.Text = "0x" + value.ToString("X");
        }
        catch
        {
            BinaryOutput.Text = "无效输入";
            OctalOutput.Text = "无效输入";
            DecimalOutput.Text = "无效输入";
            HexOutput.Text = "无效输入";
        }
    }

    // 哈希计算
    private void BrowseHashFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "选择文件" };
        if (dlg.ShowDialog() == true)
        {
            HashFilePath.Text = dlg.FileName;
        }
    }

    private void ComputeHash_Click(object sender, RoutedEventArgs e)
    {
        var path = HashFilePath.Text;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            MessageBox.Show("请选择有效的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var algorithm = (HashAlgorithmSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SHA256";
        try
        {
            var hash = HashService.ComputeFileHash(path, algorithm);
            HashResult.Text = hash;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"计算哈希失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 编码转换
    private void ConvertEncoding_Click(object sender, RoutedEventArgs e)
    {
        var input = EncodingInput.Text;
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show("请输入文本。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var encodings = EncodingService.SupportedEncodings;
        var fromIndex = FromEncoding.SelectedIndex;
        var toIndex = ToEncoding.SelectedIndex;
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= encodings.Count || toIndex >= encodings.Count)
            return;

        try
        {
            var fromEnc = encodings[fromIndex].encoding;
            var toEnc = encodings[toIndex].encoding;
            var result = EncodingService.ConvertText(input, fromEnc, toEnc);
            EncodingOutput.Text = result;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编码转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 时间戳转换
    private void TimestampToDate_Click(object sender, RoutedEventArgs e)
    {
        if (long.TryParse(TimestampInput.Text, out var timestamp))
        {
            try
            {
                var dt = ConverterService.TimestampToDateTime(timestamp, true);
                TimestampToDateResult.Text = dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("请输入有效的时间戳。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DateToTimestamp_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.TryParse(DateTimeInput.Text, out var dt))
        {
            try
            {
                var timestamp = ConverterService.DateTimeToTimestamp(dt, true);
                DateToTimestampResult.Text = timestamp.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("请输入有效的日期时间。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Base64
    private void Base64Encode_Click(object sender, RoutedEventArgs e)
    {
        var input = Base64Input.Text;
        try
        {
            var encoded = ConverterService.Base64Encode(System.Text.Encoding.UTF8.GetBytes(input));
            Base64Output.Text = encoded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Base64Decode_Click(object sender, RoutedEventArgs e)
    {
        var input = Base64Input.Text;
        try
        {
            var decoded = ConverterService.Base64Decode(input);
            Base64Output.Text = System.Text.Encoding.UTF8.GetString(decoded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // URL 编解码
    private void UrlEncode_Click(object sender, RoutedEventArgs e)
    {
        var input = UrlInput.Text;
        try
        {
            var encoded = ConverterService.UrlEncode(input);
            UrlOutput.Text = encoded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UrlDecode_Click(object sender, RoutedEventArgs e)
    {
        var input = UrlInput.Text;
        try
        {
            var decoded = ConverterService.UrlDecode(input);
            UrlOutput.Text = decoded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 正则测试
    private void RegexTest_Click(object sender, RoutedEventArgs e)
    {
        var pattern = RegexPattern.Text;
        var input = RegexInput.Text;
        if (string.IsNullOrEmpty(pattern))
        {
            MessageBox.Show("请输入正则表达式。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var matches = regex.Matches(input);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"找到 {matches.Count} 个匹配:");
            sb.AppendLine();
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                sb.AppendLine($"位置 {match.Index}: \"{match.Value}\"");
            }
            RegexResult.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"正则表达式错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 颜色拾取
    private void ColorHex_Changed(object sender, RoutedEventArgs e)
    {
        var hex = ColorHexInput.Text;
        try
        {
            var (r, g, b, a) = ConverterService.ToColorBytes(hex);
            ColorPreview.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(a, r, g, b));
            ColorRgbText.Text = $"RGB({r}, {g}, {b}) / A={a}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无效的颜色值: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
