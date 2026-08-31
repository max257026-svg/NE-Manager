using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Editor;

namespace NEManager.App.Views;

public partial class DiffPage : UserControl, IRefreshable
{
    private string _fileA = "";
    private string _fileB = "";

    public DiffPage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void SelectFileA_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "选择文件 A" };
        if (dlg.ShowDialog() == true)
        {
            _fileA = dlg.FileName;
            FileAName.Text = System.IO.Path.GetFileName(_fileA);
        }
    }

    private void SelectFileB_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "选择文件 B" };
        if (dlg.ShowDialog() == true)
        {
            _fileB = dlg.FileName;
            FileBName.Text = System.IO.Path.GetFileName(_fileB);
        }
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_fileA) || string.IsNullOrEmpty(_fileB))
        {
            MessageBox.Show("请选择两个文件进行比较。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var engine = new DiffEngine();
            var result = engine.DiffFiles(_fileA, _fileB);
            
            var leftSb = new System.Text.StringBuilder();
            var rightSb = new System.Text.StringBuilder();
            int additions = 0, deletions = 0, modifications = 0;

            foreach (var line in result.Lines)
            {
                switch (line.DiffType)
                {
                    case DiffType.Same:
                        leftSb.AppendLine(line.Text);
                        rightSb.AppendLine(line.Text);
                        break;
                    case DiffType.Added:
                        leftSb.AppendLine();
                        rightSb.AppendLine($"+ {line.Text}");
                        additions++;
                        break;
                    case DiffType.Removed:
                        leftSb.AppendLine($"- {line.Text}");
                        rightSb.AppendLine();
                        deletions++;
                        break;
                    case DiffType.Modified:
                        leftSb.AppendLine($"~ {line.Text}");
                        rightSb.AppendLine($"~ {line.Text}");
                        modifications++;
                        break;
                }
            }

            DiffLeft.Text = leftSb.ToString();
            DiffRight.Text = rightSb.ToString();
            StatusText.Text = $"比较完成: {additions} 处新增, {deletions} 处删除, {modifications} 处修改";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"比较失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
