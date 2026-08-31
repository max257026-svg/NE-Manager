using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Script;

namespace NEManager.App.Views;

public partial class ScriptPage : UserControl, IRefreshable
{
    private string _currentFile = "";

    public ScriptPage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        var script = ScriptEditor.Text;
        if (string.IsNullOrWhiteSpace(script))
        {
            MessageBox.Show("请输入脚本内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var scriptType = (ScriptTypeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Python";
        
        try
        {
            StatusText.Text = "正在运行...";
            OutputWindow.Clear();
            
            ScriptResult result;
            switch (scriptType)
            {
                case "Python":
                    result = ScriptEngine.RunPython(script);
                    break;
                case "PowerShell":
                    result = ScriptEngine.RunPowerShell(script);
                    break;
                case "Batch":
                    result = ScriptEngine.RunBatch(script);
                    break;
                case "Lua":
                    result = ScriptEngine.RunLua(script);
                    break;
                default:
                    throw new Exception($"不支持的脚本类型: {scriptType}");
            }

            if (result.Success)
            {
                OutputWindow.Text = result.Output;
                StatusText.Text = "运行成功";
                ExitCodeText.Text = $"退出码: {result.ExitCode}";
            }
            else
            {
                OutputWindow.Text = $"错误:\n{result.Error}\n\n输出:\n{result.Output}";
                StatusText.Text = "运行失败";
                ExitCodeText.Text = $"退出码: {result.ExitCode}";
            }
        }
        catch (Exception ex)
        {
            OutputWindow.Text = $"执行错误: {ex.Message}";
            StatusText.Text = "执行错误";
            ExitCodeText.Text = "";
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开脚本文件",
            Filter = "脚本文件|*.py;*.ps1;*.bat;*.cmd;*.lua|Python|*.py|PowerShell|*.ps1|Batch|*.bat;*.cmd|Lua|*.lua|所有文件|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                ScriptEditor.Text = File.ReadAllText(dlg.FileName);
                _currentFile = dlg.FileName;
                StatusText.Text = $"已打开: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFile))
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存脚本文件",
                Filter = "Python|*.py|PowerShell|*.ps1|Batch|*.bat|Lua|*.lua|所有文件|*.*"
            };

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
            File.WriteAllText(_currentFile, ScriptEditor.Text);
            StatusText.Text = $"已保存: {Path.GetFileName(_currentFile)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ScriptEditor.Clear();
        OutputWindow.Clear();
        _currentFile = "";
        StatusText.Text = "就绪";
        ExitCodeText.Text = "";
    }
}
