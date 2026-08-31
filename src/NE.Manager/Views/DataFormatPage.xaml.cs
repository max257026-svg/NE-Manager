using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Tools;

namespace NEManager.App.Views;

public partial class DataFormatPage : UserControl, IRefreshable
{
    private string _currentFile = "";

    public DataFormatPage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void Convert_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("请输入要转换的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var inputFormat = (InputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
        var outputFormat = (OutputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";

        try
        {
            string result;
            if (inputFormat == outputFormat)
            {
                result = input;
            }
            else if (inputFormat == "JSON" && outputFormat == "XML")
            {
                result = DataFormatService.JsonToXml(input);
            }
            else if (inputFormat == "XML" && outputFormat == "JSON")
            {
                result = DataFormatService.XmlToJson(input);
            }
            else if (inputFormat == "JSON" && outputFormat == "YAML")
            {
                result = DataFormatService.JsonToYaml(input);
            }
            else if (inputFormat == "YAML" && outputFormat == "JSON")
            {
                result = DataFormatService.YamlToJson(input);
            }
            else if (inputFormat == "CSV" && outputFormat == "JSON")
            {
                result = DataFormatService.CsvToJson(input);
            }
            else if (inputFormat == "JSON" && outputFormat == "CSV")
            {
                result = DataFormatService.JsonToCsv(input);
            }
            else
            {
                // 先转为 JSON，再转为目标格式
                var json = inputFormat switch
                {
                    "XML" => DataFormatService.XmlToJson(input),
                    "YAML" => DataFormatService.YamlToJson(input),
                    "CSV" => DataFormatService.CsvToJson(input),
                    _ => input
                };

                result = outputFormat switch
                {
                    "XML" => DataFormatService.JsonToXml(json),
                    "YAML" => DataFormatService.JsonToYaml(json),
                    "CSV" => DataFormatService.JsonToCsv(json),
                    _ => json
                };
            }

            OutputBox.Text = result;
            StatusText.Text = $"转换成功：{inputFormat} → {outputFormat}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"转换失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "转换失败";
        }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("请输入要验证的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var format = (InputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";

        try
        {
            var (isValid, error) = format switch
            {
                "JSON" => DataFormatService.ValidateJson(input),
                "XML" => DataFormatService.ValidateXml(input),
                _ => (true, "")
            };

            if (isValid)
            {
                StatusText.Text = $"验证通过：{format} 格式正确";
                MessageBox.Show($"{format} 格式验证通过", "验证成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText.Text = $"验证失败：{error}";
                MessageBox.Show($"{format} 格式验证失败：{error}", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"验证出错：{ex.Message}";
            MessageBox.Show($"验证出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("请输入要格式化的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var format = (InputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";

        try
        {
            var result = format switch
            {
                "JSON" => DataFormatService.FormatJson(input),
                "XML" => DataFormatService.FormatXml(input),
                _ => input
            };

            InputBox.Text = result;
            StatusText.Text = $"格式化完成：{format}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"格式化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "格式化失败";
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Clear();
        OutputBox.Clear();
        StatusText.Text = "就绪";
        InfoText.Text = "";
        _currentFile = "";
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var format = (InputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
        var filter = format switch
        {
            "JSON" => "JSON 文件|*.json|所有文件|*.*",
            "XML" => "XML 文件|*.xml|所有文件|*.*",
            "YAML" => "YAML 文件|*.yaml;*.yml|所有文件|*.*",
            "CSV" => "CSV 文件|*.csv|所有文件|*.*",
            _ => "所有文件|*.*"
        };

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开文件",
            Filter = filter
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                InputBox.Text = File.ReadAllText(dlg.FileName);
                _currentFile = dlg.FileName;
                StatusText.Text = $"已打开：{Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        var output = OutputBox.Text;
        if (string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show("没有可保存的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var format = (OutputFormatSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
        var filter = format switch
        {
            "JSON" => "JSON 文件|*.json|所有文件|*.*",
            "XML" => "XML 文件|*.xml|所有文件|*.*",
            "YAML" => "YAML 文件|*.yaml|所有文件|*.*",
            "CSV" => "CSV 文件|*.csv|所有文件|*.*",
            _ => "所有文件|*.*"
        };

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存文件",
            Filter = filter,
            DefaultExt = $".{format.ToLower()}"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, output);
                StatusText.Text = $"已保存：{Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
