using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Memory;
using NEManager.Core.SystemTools;
using MemoryValueType = NEManager.Core.Memory.ValueType;

namespace NEManager.App.Views;

public partial class MemoryPage : UserControl, IRefreshable
{
    private MemoryModService? _mem;
    private FreezeManager? _freezer;
    private ObservableCollection<ScanResultWrapper> _results = new();
    private ObservableCollection<AddressWrapper> _addressBook = new();

    public MemoryPage()
    {
        InitializeComponent();
        ResultGrid.ItemsSource = _results;
        AddressGrid.ItemsSource = _addressBook;
        Loaded += (_, _) => RefreshProcesses();
    }

    public void OnEnter() { RefreshProcesses(); }

    public void OnLeave()
    {
        _freezer?.Dispose();
        _freezer = null;
        _mem?.Dispose();
        _mem = null;
    }

    // ==================== 进程管理 ====================

    private async void RefreshProcesses()
    {
        try
        {
            StatusText.Text = "正在刷新进程列表…";
            var processes = await System.Threading.Tasks.Task.Run(() => ProcessManager.Enumerate());
            ProcessCombo.ItemsSource = processes;
            ProcessCombo.DisplayMemberPath = "Name";
            ProcessCombo.SelectedValuePath = "Id";
            if (ProcessCombo.Items.Count > 0) ProcessCombo.SelectedIndex = 0;
            StatusText.Text = $"已刷新 {processes.Count} 个进程";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"刷新进程失败: {ex.Message}";
        }
    }

    private void ProcessCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        AttachBtn.IsEnabled = ProcessCombo.SelectedItem != null;
    }

    private void RefreshProcesses_Click(object sender, RoutedEventArgs e) => RefreshProcesses();

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessCombo.SelectedItem is not ProcessManager.ProcessItem proc) return;

        _freezer?.Dispose();
        _mem?.Dispose();
        _mem = new MemoryModService(proc.Id);

        if (!_mem.IsAttached)
        {
            StatusText.Text = "OpenProcess 失败（可能需要管理员权限）";
            MessageBox.Show($"无法附加到进程 {proc.Name} (PID {proc.Id})。\n需要以管理员身份运行。", "附加失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            _mem = null;
            return;
        }

        _freezer = new FreezeManager(_mem);
        StatusText.Text = $"已附加到 {proc.Name} (PID {proc.Id})";
    }

    // ==================== 扫描 ====================

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_mem == null || !_mem.IsAttached)
        {
            MessageBox.Show("请先附加到一个进程。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var typeTag = (ValueTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Int32";
        var type = Enum.Parse<MemoryValueType>(typeTag);
        var val = ScanValueBox.Text;

        if (string.IsNullOrWhiteSpace(val))
        {
            MessageBox.Show("请输入要搜索的值。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText.Text = "扫描中...";
        ResultCountText.Text = "";

        try
        {
            var matches = await System.Threading.Tasks.Task.Run(() => _mem.Scan(type, ScanType.Exact, val));
            _results.Clear();
            foreach (var m in matches) _results.Add(new ScanResultWrapper(m));
            ResultCountText.Text = $"找到 {matches.Count} 个地址";
            StatusText.Text = _mem.Error.Length > 0 ? _mem.Error : $"扫描完成。{matches.Count} 结果。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"扫描失败：{ex.Message}";
        }
    }

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        // RH Editor 的「下一次扫描」= 变化检测。简化版：重新扫同样条件。
        Scan_Click(sender, e);
    }

    private void ResultGrid_DoubleClick(object sender, RoutedEventArgs e)
    {
        if (ResultGrid.SelectedItem is not ScanResultWrapper wr) return;

        _addressBook.Add(new AddressWrapper(wr));
        StatusText.Text = $"已添加 {wr.AddressStr} 到地址簿。";
    }

    // ==================== 地址簿 ====================

    private void AddManual_Click(object sender, RoutedEventArgs e)
    {
        var addr = PromptDialog.Show("输入地址", "手动添加地址 (格式: 0x12345678 或十进制)", "0x");
        if (string.IsNullOrEmpty(addr)) return;

        var val = PromptDialog.Show("输入初始值", "初始值 (留空读取当前值)", "0");
        if (_mem == null || !_mem.IsAttached) return;

        long.TryParse(addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr,
            NumberStyles.HexNumber, null, out long addrVal);

        var intPtr = new IntPtr(addrVal);
        var bytes = _mem.ReadBytes(intPtr, 4);
        var current = BitConverter.ToInt32(bytes, 0).ToString();

        _addressBook.Add(new AddressWrapper(
            Description: "手动", Address: intPtr, Type: "Int32",
            CurrentValue: current, FrozenValue: val ?? current, IsFrozen: false, Module: "-"));
    }

    private void EditValue_Click(object sender, RoutedEventArgs e)
    {
        if (AddressGrid.SelectedItem is not AddressWrapper aw || _mem == null) return;

        var newVal = PromptDialog.Show("写入新值", $"修改内存 (当前: {aw.CurrentValue})", aw.CurrentValue);
        if (string.IsNullOrEmpty(newVal)) return;

        var ok = WriteValue(_mem, aw.Address, aw.Type, newVal);
        if (ok)
        {
            aw.CurrentValue = newVal;
            StatusText.Text = $"已写入 {aw.AddressStr} = {newVal}";
        }
        else
        {
            MessageBox.Show("写入失败（可能地址已无效或权限不足）。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveAddress_Click(object sender, RoutedEventArgs e)
    {
        if (AddressGrid.SelectedItems.Count > 0)
        {
            foreach (var item in AddressGrid.SelectedItems.OfType<AddressWrapper>().ToList())
            {
                if (item.IsFrozen && _freezer != null)
                {
                    _freezer.Remove(new AddressEntry(item.Description, item.Address, item.ValueType,
                        "", item.CurrentValue, item.FrozenValue, true, item.Module));
                }
                _addressBook.Remove(item);
            }
        }
        else if (AddressGrid.SelectedItem is AddressWrapper aw)
        {
            if (aw.IsFrozen && _freezer != null)
            {
                _freezer.Remove(new AddressEntry(aw.Description, aw.Address, aw.ValueType,
                    "", aw.CurrentValue, aw.FrozenValue, true, aw.Module));
            }
            _addressBook.Remove(aw);
        }
    }

    private void ToggleFreeze_Click(object sender, RoutedEventArgs e)
    {
        if (_freezer == null) return;

        bool anyUnfrozen = _addressBook.Any(a => !a.IsFrozen);

        foreach (var aw in _addressBook)
        {
            if (anyUnfrozen && !aw.IsFrozen)
            {
                aw.IsFrozen = true;
                aw.OnPropertyChanged(nameof(aw.IsFrozen));
                _freezer.Add(new AddressEntry(aw.Description, aw.Address, aw.ValueType,
                    "", aw.CurrentValue, aw.FrozenValue, true, aw.Module));
            }
            else if (!anyUnfrozen && aw.IsFrozen)
            {
                aw.IsFrozen = false;
                aw.OnPropertyChanged(nameof(aw.IsFrozen));
                _freezer.Remove(new AddressEntry(aw.Description, aw.Address, aw.ValueType,
                    "", aw.CurrentValue, aw.FrozenValue, true, aw.Module));
            }
        }

        FreezeToggleBtn.Content = anyUnfrozen ? "停止冻结" : "开始冻结";
        FrozenCountText.Text = _freezer.IsRunning ? $"({_freezer.Frozen.Count} 冻结中)" : "";
    }

    // ==================== 辅助 ====================

    private void SetStatus(string msg) { Dispatcher.InvokeAsync(() => StatusText.Text = msg); }

    private static bool WriteValue(MemoryModService mem, IntPtr addr, string typeName, string val)
    {
        try
        {
            var bytes = typeName switch
            {
                "Int32" => BitConverter.GetBytes(int.Parse(val)),
                "Int64" => BitConverter.GetBytes(long.Parse(val)),
                "Float" => BitConverter.GetBytes(float.Parse(val)),
                "Double" => BitConverter.GetBytes(double.Parse(val)),
                _ => null
            };
            if (bytes == null) return false;
            return mem.WriteBytes(addr, bytes);
        }
        catch { return false; }
    }

    // ==================== WPF 绑定包装类 ====================

    private class ScanResultWrapper
    {
        public ScanResult Result { get; }
        public IntPtr Address => Result.Address;
        public string AddressStr => $"0x{Result.Address.ToInt64():X}";
        public string Display => Result.Display;
        public int Size => Result.Size;
        public MemoryValueType ValueType => Result.ValueType;

        public ScanResultWrapper(ScanResult r) { Result = r; }
    }

    private class AddressWrapper : INotifyPropertyChanged
    {
        private string _description = "";
        private IntPtr _address;
        private string _type = "Int32";
        private string _currentValue = "";
        private string _frozenValue = "";
        private bool _isFrozen;
        private string _module = "-";

        public string Description
        {
            get => _description; set { _description = value; OnPropertyChanged(nameof(Description)); }
        }
        public IntPtr Address
        {
            get => _address; set { _address = value; OnPropertyChanged(nameof(AddressStr)); }
        }
        public string AddressStr => $"0x{Address.ToInt64():X}";
        public string Type
        {
            get => _type; set { _type = value; OnPropertyChanged(nameof(Type)); OnPropertyChanged(nameof(ValueType)); }
        }
        public MemoryValueType ValueType => Enum.Parse<MemoryValueType>(Type);
        public string CurrentValue
        {
            get => _currentValue; set { _currentValue = value; OnPropertyChanged(nameof(CurrentValue)); }
        }
        public string FrozenValue
        {
            get => _frozenValue; set { _frozenValue = value; OnPropertyChanged(nameof(FrozenValue)); }
        }
        public bool IsFrozen
        {
            get => _isFrozen; set { _isFrozen = value; OnPropertyChanged(nameof(IsFrozen)); }
        }
        public string Module
        {
            get => _module; set { _module = value; OnPropertyChanged(nameof(Module)); }
        }

        public AddressWrapper() { }

        public AddressWrapper(ScanResultWrapper wr)
        {
            Description = "扫描结果";
            Address = wr.Address;
            Type = wr.ValueType.ToString();
            CurrentValue = wr.Display;
            FrozenValue = wr.Display;
        }

        public AddressWrapper(string Description, IntPtr Address, string Type,
            string CurrentValue, string FrozenValue, bool IsFrozen, string Module)
        {
            this._description = Description;
            this._address = Address;
            this._type = Type;
            this._currentValue = CurrentValue;
            this._frozenValue = FrozenValue;
            this._isFrozen = IsFrozen;
            this._module = Module;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
