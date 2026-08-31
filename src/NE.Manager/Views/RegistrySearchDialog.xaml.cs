using System.Windows;
using System.Windows.Input;
using NEManager.Core.Registry;

namespace NEManager.App.Views;

public partial class RegistrySearchDialog : Window
{
    private readonly string _rootPath;
    private List<Core.Registry.RegistryService.SearchHit> _hits = new();

    public RegistrySearchDialog(string rootPath)
    {
        InitializeComponent();
        _rootPath = rootPath;
        RootPathText.Text = rootPath;
        PatternBox.Focus();
    }

    private void PatternBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Search_Click(sender, e);
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var pattern = PatternBox.Text.Trim();
        if (string.IsNullOrEmpty(pattern))
        {
            ResultStatus.Text = "请输入搜索关键词。";
            return;
        }

        var options = new Core.Registry.RegistryService.SearchOptions
        {
            Pattern = pattern,
            SearchKeyNames = SearchKeysBox.IsChecked == true,
            SearchValueNames = SearchValueNamesBox.IsChecked == true,
            SearchValueData = SearchValueDataBox.IsChecked == true,
            UseRegex = UseRegexBox.IsChecked == true,
            MatchCase = MatchCaseBox.IsChecked == true
        };

        SearchProgress.Visibility = Visibility.Visible;
        ResultStatus.Text = "正在搜索…（注册表分支较大时可能需要几十秒）";
        ResultGrid.ItemsSource = null;

        var root = _rootPath;

        try
        {
            _hits = await Task.Run(() => Core.Registry.RegistryService.Search(root, options));

            ResultGrid.ItemsSource = _hits;
            ResultStatus.Text = _hits.Count == 0
                ? "未找到匹配项。"
                : $"找到 {_hits.Count} 个匹配项" +
                  (_hits.Count >= options.MaxResults ? $"（已达上限 {options.MaxResults}，请缩小搜索范围）" : "");
        }
        catch (Exception ex)
        {
            ResultStatus.Text = $"搜索出错：{ex.Message}";
        }
        finally
        {
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void ResultGrid_DoubleClick(object sender, MouseButtonEventArgs e) => Jump_Click(sender, e);

    private void Jump_Click(object sender, RoutedEventArgs e)
    {
        if (ResultGrid.SelectedItem is not Core.Registry.RegistryService.SearchHit hit)
        {
            MessageBox.Show("请先选择一个结果。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 把路径写回主窗口的注册表页面
        if (Owner is MainWindow main)
        {
            main.Navigate("Registry");
            if (main.PageHost.Content is RegistryPage page)
                page.NavigateTo(hit.KeyPath);
        }
        else if (Application.Current.MainWindow is MainWindow main2)
        {
            main2.Navigate("Registry");
            if (main2.PageHost.Content is RegistryPage page2)
                page2.NavigateTo(hit.KeyPath);
        }

        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
