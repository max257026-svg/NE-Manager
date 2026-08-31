using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Memory;

namespace NEManager.App.Views;

public partial class MemoryEditorPage : UserControl, IRefreshable
{
    private ProcessMemory? _memory;
    private MemorySearch? _search;

    public MemoryEditorPage()
    {
        InitializeComponent();
        RefreshProcesses();
    }

    public void OnEnter() { }
    public void OnLeave()
    {
        _memory?.Detach();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        try
        {
            var processes = NEManager.Core.SystemTools.ProcessManager.Enumerate()
                .OrderBy(p => p.Name)
                .Select(p => new { ProcessName = $"{p.Name} (PID: {p.Id})", ProcessId = p.Id })
                .ToList();
            ProcessSelector.ItemsSource = processes;
            ProcessSelector.DisplayMemberPath = "ProcessName";
            ProcessSelector.SelectedValuePath = "ProcessId";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新进程列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessSelector.SelectedValue is not int pid)
        {
            MessageBox.Show("请选择一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _memory = new ProcessMemory();
            if (_memory.Attach(pid))
            {
                AttachStatus.Text = $"已附加: {_memory.ProcessName}";
                StatusText.Text = $"已附加到进程 {_memory.ProcessName} (PID: {pid})";
                
                var regions = _memory.GetMemoryRegions();
                MemoryRegionGrid.ItemsSource = regions;
                
                _search = new MemorySearch(_memory);
            }
            else
            {
                MessageBox.Show("附加进程失败。可能需要管理员权限。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"附加进程失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Detach_Click(object sender, RoutedEventArgs e)
    {
        _memory?.Detach();
        _memory = null;
        _search = null;
        AttachStatus.Text = "";
        StatusText.Text = "未附加进程";
        MemoryRegionGrid.ItemsSource = null;
        SearchResultGrid.ItemsSource = null;
    }

    private void Region_Selected(object sender, SelectionChangedEventArgs e)
    {
        // 可以在这里显示选中区域的详细内容
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (_search == null)
        {
            MessageBox.Show("请先附加到进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var valueText = SearchValueBox.Text;
        if (string.IsNullOrEmpty(valueText))
        {
            MessageBox.Show("请输入搜索值。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var valueType = (ValueTypeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Int32";
            List<SearchResult> results;

            switch (valueType)
            {
                case "Byte":
                    if (byte.TryParse(valueText, out var byteVal))
                        results = _search.SearchExact(new[] { byteVal });
                    else
                        throw new Exception("无效的字节值");
                    break;
                case "Int16":
                    if (short.TryParse(valueText, out var shortVal))
                        results = _search.SearchInt32(shortVal);
                    else
                        throw new Exception("无效的 Int16 值");
                    break;
                case "Int32":
                    if (int.TryParse(valueText, out var intVal))
                        results = _search.SearchInt32(intVal);
                    else
                        throw new Exception("无效的 Int32 值");
                    break;
                case "Int64":
                    if (long.TryParse(valueText, out var longVal))
                        results = _search.SearchInt64(longVal);
                    else
                        throw new Exception("无效的 Int64 值");
                    break;
                case "Float":
                    if (float.TryParse(valueText, out var floatVal))
                        results = _search.SearchFloat(floatVal, 0.01f);
                    else
                        throw new Exception("无效的浮点值");
                    break;
                case "Double":
                    if (double.TryParse(valueText, out var doubleVal))
                        results = _search.SearchFloat((float)doubleVal, 0.01f);
                    else
                        throw new Exception("无效的双精度值");
                    break;
                case "String":
                    results = _search.SearchString(valueText, System.Text.Encoding.UTF8);
                    break;
                default:
                    throw new Exception("未知的值类型");
            }

            SearchResultGrid.ItemsSource = results;
            ResultCountText.Text = $"找到 {results.Count} 个结果";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        if (_search == null || SearchResultGrid.SelectedItem is not SearchResult result)
        {
            MessageBox.Show("请选择一个搜索结果。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _search.LockValue(result.Address, result.CurrentValue);
            result.IsLocked = true;
            SearchResultGrid.Items.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"锁定失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (_search == null || SearchResultGrid.SelectedItem is not SearchResult result)
        {
            MessageBox.Show("请选择一个搜索结果。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _search.UnlockValue(result.Address);
            result.IsLocked = false;
            SearchResultGrid.Items.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解锁失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}


