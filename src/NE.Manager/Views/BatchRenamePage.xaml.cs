using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Tools;

namespace NEManager.App.Views;

public partial class BatchRenamePage : UserControl, IRefreshable
{
    private List<string> _filePaths = new();
    private List<(string Original, string New)> _previewResults = new();

    public BatchRenamePage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择文件",
            Multiselect = true,
            Filter = "所有文件|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            _filePaths.AddRange(dlg.FileNames);
            UpdateCount();
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择文件夹"
        };

        if (dlg.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dlg.FolderName);
            _filePaths.AddRange(files);
            UpdateCount();
        }
    }

    private void ClearList_Click(object sender, RoutedEventArgs e)
    {
        _filePaths.Clear();
        PreviewList.ItemsSource = null;
        UpdateCount();
    }

    private void RenameModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RegexPanel == null) return;

        var mode = (RenameModeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
        RegexPanel.Visibility = mode == "正则替换" ? Visibility.Visible : Visibility.Collapsed;
        TemplatePanel.Visibility = mode == "模板替换" ? Visibility.Visible : Visibility.Collapsed;
        NumberingPanel.Visibility = mode == "添加序号" ? Visibility.Visible : Visibility.Collapsed;
        CasePanel.Visibility = mode == "大小写转换" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_filePaths.Count == 0)
        {
            MessageBox.Show("请先添加文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mode = (RenameModeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
        _previewResults.Clear();

        try
        {
            switch (mode)
            {
                case "正则替换":
                    var rule = new BatchRenameService.RenameRule
                    {
                        Pattern = PatternBox.Text,
                        Replacement = ReplacementBox.Text,
                        UseRegex = true,
                        CaseSensitive = CaseSensitiveBox.IsChecked ?? false,
                        IncludeExtension = IncludeExtensionBox.IsChecked ?? false
                    };
                    _previewResults = BatchRenameService.PreviewRename(_filePaths, rule).ToList();
                    break;

                case "模板替换":
                    var template = TemplateBox.Text;
                    var startIndex = int.TryParse(StartIndexBox.Text, out var idx) ? idx : 1;
                    var tempResults = BatchRenameService.BatchRenameWithTemplate(_filePaths, template, startIndex);
                    _previewResults = tempResults.Select(r => (r.OriginalName, r.NewName)).ToList();
                    break;

                case "添加序号":
                    var prefix = PrefixBox.Text;
                    var suffix = SuffixBox.Text;
                    var separator = SeparatorBox.Text;
                    var numStart = int.TryParse(NumberStartBox.Text, out var ns) ? ns : 1;
                    var numResults = BatchRenameService.AddNumbering(_filePaths, prefix, suffix, numStart, separator);
                    _previewResults = numResults.Select(r => (r.OriginalName, r.NewName)).ToList();
                    break;

                case "大小写转换":
                    var caseMode = (CaseModeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "lower";
                    var modeKey = caseMode.Contains("upper") ? "upper" : caseMode.Contains("lower") ? "lower" : caseMode.Contains("title") ? "title" : "camel";
                    var caseResults = BatchRenameService.ChangeCase(_filePaths, modeKey);
                    _previewResults = caseResults.Select(r => (r.OriginalName, r.NewName)).ToList();
                    break;
            }

            PreviewList.ItemsSource = _previewResults.Select(r => new { Original = r.Original, New = r.New }).ToList();
            StatusText.Text = $"预览完成，共 {_previewResults.Count} 个文件";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"预览失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "预览失败";
        }
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_filePaths.Count == 0)
        {
            MessageBox.Show("请先添加文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_previewResults.Count == 0)
        {
            MessageBox.Show("请先预览重命名结果", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"确认要重命名 {_previewResults.Count} 个文件吗？", "确认", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var mode = (RenameModeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
        List<BatchRenameService.RenameResult> results = new();

        try
        {
            switch (mode)
            {
                case "正则替换":
                    var rule = new BatchRenameService.RenameRule
                    {
                        Pattern = PatternBox.Text,
                        Replacement = ReplacementBox.Text,
                        UseRegex = true,
                        CaseSensitive = CaseSensitiveBox.IsChecked ?? false,
                        IncludeExtension = IncludeExtensionBox.IsChecked ?? false
                    };
                    results = BatchRenameService.BatchRename(_filePaths, rule);
                    break;

                case "模板替换":
                    var template = TemplateBox.Text;
                    var startIndex = int.TryParse(StartIndexBox.Text, out var idx) ? idx : 1;
                    results = BatchRenameService.BatchRenameWithTemplate(_filePaths, template, startIndex);
                    break;

                case "添加序号":
                    var prefix = PrefixBox.Text;
                    var suffix = SuffixBox.Text;
                    var separator = SeparatorBox.Text;
                    var numStart = int.TryParse(NumberStartBox.Text, out var ns) ? ns : 1;
                    results = BatchRenameService.AddNumbering(_filePaths, prefix, suffix, numStart, separator);
                    break;

                case "大小写转换":
                    var caseMode = (CaseModeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "lower";
                    var modeKey = caseMode.Contains("upper") ? "upper" : caseMode.Contains("lower") ? "lower" : caseMode.Contains("title") ? "title" : "camel";
                    results = BatchRenameService.ChangeCase(_filePaths, modeKey);
                    break;
            }

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;
            StatusText.Text = $"重命名完成：成功 {successCount} 个，失败 {failCount} 个";

            if (failCount > 0)
            {
                var errors = results.Where(r => !r.Success).Select(r => $"{r.OriginalName}: {r.Error}");
                MessageBox.Show($"部分文件重命名失败:\n{string.Join("\n", errors)}", "警告", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _filePaths.Clear();
            _previewResults.Clear();
            PreviewList.ItemsSource = null;
            UpdateCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"执行失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "执行失败";
        }
    }

    private void UpdateCount()
    {
        CountText.Text = $"已添加 {_filePaths.Count} 个文件";
    }
}
