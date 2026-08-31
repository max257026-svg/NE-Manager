using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NEManager.Core.Decompiler;
using NEManager.Core.FileSystem;
using NEManager.Core.Pe;
using NEManager.Core.Risk;
using NEManager.Core.Security;
using NEManager.Core.SystemTools;
using NEManager.Core.Tools;
using NEManager.Core.Injection;

namespace NEManager.App.Views;

public partial class FilesPage : UserControl, IRefreshable
{
    public ObservableCollection<FileItem> LeftItems { get; } = new();
    public ObservableCollection<FileItem> RightItems { get; } = new();

    private string _leftPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _rightPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private bool _activeIsLeft = true;

    // ===== 路径历史栈（真正的前进/后退）=====
    private readonly List<string> _leftHistory = new();
    private int _leftHistoryIdx = -1;
    private readonly List<string> _rightHistory = new();
    private int _rightHistoryIdx = -1;

    public FilesPage()
    {
        InitializeComponent();
        DataContext = this;
        // 初始化历史栈（让 Back 能回到初始路径）
        _leftHistory.Add(_leftPath); _leftHistoryIdx = 0;
        _rightHistory.Add(_rightPath); _rightHistoryIdx = 0;
        Loaded += (_, _) => RefreshBoth();
    }

    public void OnEnter() => RefreshBoth();
    public void OnLeave() { }

    /// <summary>
    /// 供其他页面（如磁盘页挂载 VHD 后）跳转到指定路径。
    /// </summary>
    public void NavigateToPath(string path, bool isLeft = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Navigate(path, isLeft);
    }

    // ==================== 路径与刷新 ====================

    private string ActivePath => _activeIsLeft ? _leftPath : _rightPath;

    private FileItem? ActiveSelected =>
        _activeIsLeft ? LeftGrid.SelectedItem as FileItem : RightGrid.SelectedItem as FileItem;

    private void RefreshBoth()
    {
        if (!IsLoaded) return; // InitializeComponent 期间 CheckBox 事件会提前触发
        RefreshLeft();
        RefreshRight();
        UpdateDriveInfo();
    }

    private void RefreshLeft()
    {
        if (!IsLoaded) return;
        LeftPathBox.Text = _leftPath;
        LeftItems.Clear();
        foreach (var item in FileSystemService.Enumerate(_leftPath, GetBrowseOptions()))
            LeftItems.Add(item);

        // 应用过滤
        ApplyFilter(LeftItems);

        LeftBreadcrumb.Text = _leftPath;
        LeftStatus.Text = $"{LeftItems.Count} 个项目";
        UpdatePaneHighlight();
        UpdateDriveInfo();
    }

    private void RefreshRight()
    {
        if (!IsLoaded) return;
        RightPathBox.Text = _rightPath;
        RightItems.Clear();
        foreach (var item in FileSystemService.Enumerate(_rightPath, GetBrowseOptions()))
            RightItems.Add(item);

        // 应用过滤
        ApplyFilter(RightItems);

        RightBreadcrumb.Text = _rightPath;
        RightStatus.Text = $"{RightItems.Count} 个项目";
        UpdatePaneHighlight();
        UpdateDriveInfo();
    }

    /// <summary>
    /// 根据 FilterBox 内容过滤 ObservableCollection。
    /// 搜索词支持 * 和 ? 通配符（Glob 语义），纯文本则做大小写不敏感 Contains。
    /// </summary>
    private void ApplyFilter(ObservableCollection<FileItem> collection)
    {
        var keyword = FilterBox.Text?.Trim();
        if (string.IsNullOrEmpty(keyword)) return; // 无关键词时保留全部

        // 简单实现：在 Refresh 里先完整加载，再用 LINQ 过滤
        // 为了避免影响原 ObservableCollection，这里直接原地 RemoveRange 式过滤
        var matches = new List<FileItem>();
        var rest = new List<FileItem>();

        // 把不匹配的先挑出来（目录优先的顺序保持）
        foreach (var item in collection)
        {
            if (MatchesFilter(item, keyword))
                matches.Add(item);
            else
                rest.Add(item);
        }

        // 原地重排：先 Remove 全部 rest，再把 matches 排前面
        foreach (var item in rest) collection.Remove(item);
        // 现在 collection 里就是全部 matches（保持原顺序，目录优先）
    }

    private static bool MatchesFilter(FileItem item, string keyword)
    {
        // 支持 Glob 通配符
        if (keyword.Contains('*') || keyword.Contains('?'))
        {
            return Glob.Match(keyword, item.Name, StringComparison.OrdinalIgnoreCase);
        }
        return item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private FileSystemService.BrowseOptions GetBrowseOptions() => new()
    {
        ShowHidden = ShowHiddenBox?.IsChecked == true,
        ShowSystem = ShowSystemBox?.IsChecked == true,
        DetectAlternateStreams = DetectAdsBox?.IsChecked == true
    };

    private void UpdatePaneHighlight()
    {
        LeftPaneHeader.Background = _activeIsLeft
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("BgElevatedBrush");
        RightPaneHeader.Background = !_activeIsLeft
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("BgElevatedBrush");

        LeftPaneTitle.Text = _activeIsLeft ? "左面板 · 活动" : "左面板";
        RightPaneTitle.Text = !_activeIsLeft ? "右面板 · 活动" : "右面板";

        LeftPaneBorder.BorderBrush = _activeIsLeft
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("BorderBrush");
        RightPaneBorder.BorderBrush = !_activeIsLeft
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("BorderBrush");
    }

    private void UpdateDriveInfo()
    {
        UpdateDriveInfoFor(_leftPath, LeftDriveInfo);
        UpdateDriveInfoFor(_rightPath, RightDriveInfo);
    }

    private static void UpdateDriveInfoFor(string path, TextBlock target)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) { target.Text = string.Empty; return; }
            var drive = new DriveInfo(root);
            if (!drive.IsReady) { target.Text = $"{root} 未就绪"; return; }
            target.Text = $"{root} 可用 {FileItem.FormatSize(drive.AvailableFreeSpace)} / 共 {FileItem.FormatSize(drive.TotalSize)} ({drive.DriveFormat})";
        }
        catch { target.Text = string.Empty; }
    }

    private void Navigate(string path, bool isLeft, bool isHistoryNav = false)
    {
        if (!Directory.Exists(path))
        {
            SetStatus($"目录不存在：{path}");
            return;
        }

        // 历史栈管理：正常导航 push，历史导航不 push
        var history = isLeft ? _leftHistory : _rightHistory;
        var idx = isLeft ? ref _leftHistoryIdx : ref _rightHistoryIdx;

        if (!isHistoryNav)
        {
            // 截断当前位置之后的历史（新开一条路径）
            if (idx < history.Count - 1)
                history.RemoveRange(idx + 1, history.Count - idx - 1);
            history.Add(path);
            idx = history.Count - 1;
        }
        else
        {
            idx = history.IndexOf(path);
        }

        if (isLeft) { _leftPath = path; RefreshLeft(); }
        else { _rightPath = path; RefreshRight(); }
        UpdateDriveInfo();
    }

    // ===== 真正的后退/前进 =====
    private bool GoBack(bool isLeft)
    {
        var history = isLeft ? _leftHistory : _rightHistory;
        var idx = isLeft ? ref _leftHistoryIdx : ref _rightHistoryIdx;
        if (idx <= 0) return false;
        idx--;
        Navigate(history[idx], isLeft, isHistoryNav: true);
        return true;
    }

    private bool GoForward(bool isLeft)
    {
        var history = isLeft ? _leftHistory : _rightHistory;
        var idx = isLeft ? ref _leftHistoryIdx : ref _rightHistoryIdx;
        if (idx >= history.Count - 1) return false;
        idx++;
        Navigate(history[idx], isLeft, isHistoryNav: true);
        return true;
    }

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    // ==================== 事件处理 ====================

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var folder = DialogHelper.PickFolder(Application.Current.MainWindow, "选择要浏览的文件夹", ActivePath);
        if (folder != null) Navigate(folder, _activeIsLeft);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshBoth();

    private void DisplayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        RefreshBoth();
    }

    private void LeftPane_Click(object sender, MouseButtonEventArgs e)
    {
        _activeIsLeft = true;
        UpdatePaneHighlight();
        UpdateDriveInfo();
    }

    private void RightPane_Click(object sender, MouseButtonEventArgs e)
    {
        _activeIsLeft = false;
        UpdatePaneHighlight();
        UpdateDriveInfo();
    }

    private void LeftPath_KeyDown(object sender, KeyEventArgs e) => PathKeyDown(e, true);
    private void RightPath_KeyDown(object sender, KeyEventArgs e) => PathKeyDown(e, false);

    private void PathKeyDown(KeyEventArgs e, bool isLeft)
    {
        if (e.Key != Key.Enter) return;
        var path = (isLeft ? LeftPathBox : RightPathBox).Text.Trim();
        Navigate(path, isLeft);
        e.Handled = true;
    }

    private void LeftUp_Click(object sender, RoutedEventArgs e) => GoUp(true);
    private void RightUp_Click(object sender, RoutedEventArgs e) => GoUp(false);
    private void LeftRefresh_Click(object sender, RoutedEventArgs e) => RefreshLeft();
    private void RightRefresh_Click(object sender, RoutedEventArgs e) => RefreshRight();

    private void GoUp(bool isLeft)
    {
        var current = isLeft ? _leftPath : _rightPath;
        var parent = Directory.GetParent(current);
        if (parent != null)
            Navigate(parent.FullName, isLeft);
        else
            ShowDrives(isLeft);
    }

    private void ShowDrives(bool isLeft)
    {
        var drives = FileSystemService.EnumerateDrives();
        var text = string.Join("\n", drives.Select(d =>
            $"{d.Name,-6} {d.TypeText,-10} {(string.IsNullOrEmpty(d.VolumeLabel) ? "—" : d.VolumeLabel),-20} {d.FreeText} / {d.SizeText}"));

        new TextViewerDialog("驱动器列表", text) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void LeftGrid_DoubleClick(object sender, MouseButtonEventArgs e) => OpenItem(LeftGrid.SelectedItem as FileItem);
    private void RightGrid_DoubleClick(object sender, MouseButtonEventArgs e) => OpenItem(RightGrid.SelectedItem as FileItem);

    private void OpenItem(FileItem? item)
    {
        if (item == null) return;

        if (item.IsDirectory)
        {
            Navigate(item.FullPath, _activeIsLeft);
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = item.FullPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var grid = (DataGrid)sender;
        bool isLeft = grid == LeftGrid;

        // ===== 核心快捷键 =====
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.C) { CopySelected(isLeft); e.Handled = true; return; }
            if (e.Key == Key.X) { CutSelected(isLeft); e.Handled = true; return; }
            if (e.Key == Key.V) { PasteHere(isLeft); e.Handled = true; return; }
            if (e.Key == Key.A) { grid.SelectAll(); e.Handled = true; return; }
            if (e.Key == Key.E) { OpenEdit_Click(sender, e); e.Handled = true; return; }
            if (e.Key == Key.I) { Properties_Click(sender, e); e.Handled = true; return; }
        }

        if (e.Key == Key.Delete) { DeleteSelected(grid.SelectedItem as FileItem); e.Handled = true; return; }
        if (e.Key == Key.F2) { RenameSelected(grid.SelectedItem as FileItem); e.Handled = true; return; }
        if (e.Key == Key.F5) { if (isLeft) RefreshLeft(); else RefreshRight(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { grid.UnselectAll(); e.Handled = true; return; }

        if (e.Key == Key.Enter && grid.SelectedItem is FileItem item)
        {
            OpenItem(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            // 优先后退，没历史了再上一级
            if (!GoBack(isLeft)) GoUp(isLeft);
            e.Handled = true;
        }
    }

    // ===== 剪贴板（复制/剪切/粘贴）=====
    private static FileItem[]? _clipboardItems;
    private static bool _clipboardIsCut;

    private void CopySelected(bool isLeft)
    {
        var grid = isLeft ? LeftGrid : RightGrid;
        var items = grid.SelectedItems.OfType<FileItem>().ToArray();
        if (items.Length == 0) return;
        _clipboardItems = items;
        _clipboardIsCut = false;
        SetStatus($"已复制 {items.Length} 项到剪贴板。Ctrl+V 粘贴。");
    }

    private void CutSelected(bool isLeft)
    {
        var grid = isLeft ? LeftGrid : RightGrid;
        var items = grid.SelectedItems.OfType<FileItem>().ToArray();
        if (items.Length == 0) return;
        _clipboardItems = items;
        _clipboardIsCut = true;
        SetStatus($"已剪切 {items.Length} 项到剪贴板。Ctrl+V 粘贴。");
    }

    private void PasteHere(bool isLeft)
    {
        if (_clipboardItems == null || _clipboardItems.Length == 0)
        {
            SetStatus("剪贴板为空。");
            return;
        }
        var dest = isLeft ? _leftPath : _rightPath;
        int done = 0; int fail = 0;

        foreach (var item in _clipboardItems)
        {
            try
            {
                var target = Path.Combine(dest, item.Name);
                if (_clipboardIsCut)
                {
                    if (item.IsDirectory) Directory.Move(item.FullPath, target);
                    else File.Move(item.FullPath, target);
                }
                else
                {
                    if (item.IsDirectory) CopyDirectory(item.FullPath, target);
                    else File.Copy(item.FullPath, target, true);
                }
                done++;
            }
            catch { fail++; }
        }

        if (isLeft) RefreshLeft(); else RefreshRight();
        SetStatus(_clipboardIsCut
            ? $"移动完成：{done} 成功 / {fail} 失败"
            : $"复制完成：{done} 成功 / {fail} 失败");

        if (_clipboardIsCut) _clipboardItems = null; // 剪切后清空
    }

    private void LeftGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LeftGrid.SelectedItem != null) _activeIsLeft = true;
        UpdatePaneHighlight();
        UpdateSelectionInfo(LeftGrid.SelectedItem as FileItem, true);
    }

    private void RightGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RightGrid.SelectedItem != null) _activeIsLeft = false;
        UpdatePaneHighlight();
        UpdateSelectionInfo(RightGrid.SelectedItem as FileItem, false);
    }

    private void UpdateSelectionInfo(FileItem? item, bool isLeft)
    {
        if (isLeft && _activeIsLeft == false) return;
        if (!isLeft && _activeIsLeft == true) return;

        var mw = Window.GetWindow(this) as MainWindow;

        if (item == null)
        {
            mw?.SetStatus("未选中项目");
            return;
        }

        var parts = new List<string>
        {
            item.Name,
            item.SizeText,
            item.TypeText,
            $"修改于 {item.LastWriteTime:yyyy-MM-dd HH:mm}"
        };
        if (item.HasAlternateStreams) parts.Add($"含 {item.StreamCount} 个备用数据流");
        if (item.IsSystem) parts.Add("系统文件");
        if (FileSystemService.IsSystemPath(item.FullPath)) parts.Add("位于系统目录");

        mw?.SetStatus(string.Join("  ·  ", parts));
    }

    // ==================== 文件操作 ====================

    private void CopyToRight_Click(object sender, RoutedEventArgs e) => CopyBetween(LeftGrid, _rightPath, false);
    private void CopyToLeft_Click(object sender, RoutedEventArgs e) => CopyBetween(RightGrid, _leftPath, true);

    private void CopyBetween(DataGrid source, string destination, bool toLeft)
    {
        var items = source.SelectedItems.OfType<FileItem>().ToList();
        if (items.Count == 0)
        {
            SetStatus("请先在源面板中选择要复制的项目。");
            return;
        }

        var errors = new List<string>();
        int copied = 0;

        foreach (var item in items)
        {
            try
            {
                var dest = Path.Combine(destination, item.Name);
                if (item.IsDirectory)
                {
                    CopyDirectory(item.FullPath, dest);
                }
                else
                {
                    File.Copy(item.FullPath, dest, true);
                }
                copied++;
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Name}: {ex.Message}");
            }
        }

        RiskFramework.Log(RiskLevel.Caution, "复制文件", destination, errors.Count == 0,
            $"复制 {copied} 个项目");

        if (toLeft) RefreshLeft(); else RefreshRight();

        var message = $"已复制 {copied} 个项目。";
        if (errors.Count > 0)
            message += $"\n\n失败 {errors.Count} 项：\n" + string.Join("\n", errors.Take(10));

        MessageBox.Show(message, "复制完成", MessageBoxButton.OK,
            errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Show("新建文件夹", "文件夹名称：", "新建文件夹");
        if (string.IsNullOrWhiteSpace(name)) return;

        var path = Path.Combine(ActivePath, name);
        try
        {
            Directory.CreateDirectory(path);
            if (_activeIsLeft) RefreshLeft(); else RefreshRight();
            SetStatus($"已创建：{path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected(ActiveSelected);

    private void DeleteRight_Click(object sender, RoutedEventArgs e) => DeleteSelected(RightGrid.SelectedItem as FileItem);

    private void DeleteSelected(FileItem? item)
    {
        if (item == null) return;

        bool isSystemPath = FileSystemService.IsSystemPath(item.FullPath);
        var level = isSystemPath ? RiskLevel.Critical : RiskLevel.Dangerous;

        if (!RiskFramework.IsAllowed(level))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(level), "操作被拦截",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"确定要删除吗？\n\n{item.FullPath}\n\n" +
            (item.IsDirectory ? "该目录下的所有内容都会被删除。" : "") +
            (isSystemPath ? "\n⚠️ 这是系统目录中的文件，删除可能导致系统不稳定！" : ""),
            level == RiskLevel.Critical ? "⚠️ 删除系统文件" : "确认删除",
            MessageBoxButton.YesNo,
            isSystemPath ? MessageBoxImage.Warning : MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            // 自动清除只读属性（很多系统文件带 R 属性）
            if (File.Exists(item.FullPath))
            {
                var attrs = File.GetAttributes(item.FullPath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(item.FullPath, attrs & ~FileAttributes.ReadOnly);
                File.Delete(item.FullPath);
            }
            else if (Directory.Exists(item.FullPath))
            {
                Directory.Delete(item.FullPath, true);
            }

            RiskFramework.Log(level, "删除", item.FullPath, true);
            if (_activeIsLeft) RefreshLeft(); else RefreshRight();
            SetStatus($"已删除：{item.Name}");
        }
        catch (Exception ex)
        {
            RiskFramework.Log(level, "删除", item.FullPath, false, string.Empty, ex.Message);
            MessageBox.Show($"删除失败：{ex.Message}\n\n提示：文件可能正被占用，可尝试「查找占用进程」或使用「注册重启后替换」。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameSelected(ActiveSelected);
    private void RenameRight_Click(object sender, RoutedEventArgs e) => RenameSelected(RightGrid.SelectedItem as FileItem);

    private void RenameSelected(FileItem? item)
    {
        if (item == null) return;
        var newName = PromptDialog.Show("重命名", "新名称：", item.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        try
        {
            if (item.IsDirectory)
                Directory.Move(item.FullPath, Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName));
            else
                File.Move(item.FullPath, Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName));

            if (_activeIsLeft) RefreshLeft(); else RefreshRight();
            SetStatus("重命名完成。");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重命名失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 属性与高级信息 ====================

    private void Properties_Click(object sender, RoutedEventArgs e) => ShowProperties(ActiveSelected);
    private void PropertiesRight_Click(object sender, RoutedEventArgs e) => ShowProperties(RightGrid.SelectedItem as FileItem);

    private void ShowProperties(FileItem? item)
    {
        if (item == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══ 文件属性 ═══");
        sb.AppendLine();
        sb.AppendLine($"名称        : {item.Name}");
        sb.AppendLine($"完整路径    : {item.FullPath}");
        sb.AppendLine($"类型        : {item.TypeText}");
        sb.AppendLine($"大小        : {item.SizeText}");
        sb.AppendLine($"创建时间    : {item.CreationTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"修改时间    : {item.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"访问时间    : {item.LastAccessTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"属性        : {item.Attributes} ({item.AttributeText})");
        sb.AppendLine();

        // 备用数据流
        var streams = AlternateDataStreamService.Enumerate(item.FullPath)
            .Where(s => !s.Name.Equals("::$DATA", StringComparison.Ordinal)).ToList();

        sb.AppendLine(item.HasAlternateStreams && streams.Count > 0
            ? $"备用数据流  : {streams.Count} 个"
            : "备用数据流  : 无");

        if (streams.Count > 0)
        {
            foreach (var s in streams)
            {
                sb.AppendLine($"    {s.CleanName}  ({s.SizeText})");

                // 解析 Zone.Identifier（下载来源标记）
                if (s.CleanName.Equals("Zone.Identifier", StringComparison.OrdinalIgnoreCase))
                {
                    var content = AlternateDataStreamService.ReadStream(item.FullPath, s.Name);
                    var fields = AlternateDataStreamService.ParseZoneIdentifier(content);
                    foreach (var (k, v) in fields)
                        sb.AppendLine($"        {k} = {v}");
                }
            }
        }

        sb.AppendLine();

        // 安全描述符
        var sec = SecurityDescriptorService.ReadFileSecurity(item.FullPath);
        sb.AppendLine("═══ 安全描述符 ═══");
        if (!string.IsNullOrEmpty(sec.Error))
        {
            sb.AppendLine($"读取失败：{sec.Error}");
        }
        else
        {
            sb.AppendLine($"所有者      : {sec.Owner}");
            sb.AppendLine($"所有者 SID  : {sec.OwnerSid}");
            sb.AppendLine($"组          : {sec.Group}");
            sb.AppendLine($"DACL 条目数 : {sec.Dacl.Count}");
            sb.AppendLine($"SACL 条目数 : {sec.Sacl.Count}");
            sb.AppendLine($"阻止继承    : {(sec.DaclProtected ? "是" : "否")}");
            sb.AppendLine();
            sb.AppendLine("SDDL:");
            sb.AppendLine(sec.Sddl);
        }

        // 哈希
        if (!item.IsDirectory)
        {
            sb.AppendLine();
            sb.AppendLine("═══ 哈希校验 ═══");
            sb.AppendLine($"SHA256      : {FileSystemService.ComputeHash(item.FullPath, "SHA256")}");
            sb.AppendLine($"MD5         : {FileSystemService.ComputeHash(item.FullPath, "MD5")}");
        }

        new TextViewerDialog($"属性 · {item.Name}", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }

    private void ViewStreams_Click(object sender, RoutedEventArgs e) => ShowStreams(ActiveSelected);
    private void ViewStreamsRight_Click(object sender, RoutedEventArgs e) => ShowStreams(RightGrid.SelectedItem as FileItem);

    private void ShowStreams(FileItem? item)
    {
        if (item == null) return;

        var streams = AlternateDataStreamService.Enumerate(item.FullPath)
            .Where(s => !s.Name.Equals("::$DATA", StringComparison.Ordinal)).ToList();

        if (streams.Count == 0)
        {
            MessageBox.Show("该文件没有备用数据流。\n\n" +
                            "备用数据流 (ADS) 是 NTFS 特有的隐藏存储区域，\n" +
                            "常见于从网络下载的文件（Zone.Identifier 标记）。",
                "无备用数据流", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new StreamsDialog(item.FullPath, streams) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void ViewAcl_Click(object sender, RoutedEventArgs e) => ShowAcl(ActiveSelected);
    private void ViewAclRight_Click(object sender, RoutedEventArgs e) => ShowAcl(RightGrid.SelectedItem as FileItem);

    private void ShowAcl(FileItem? item)
    {
        if (item == null) return;
        var sec = SecurityDescriptorService.ReadFileSecurity(item.FullPath);
        new AclViewerDialog(sec) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void Hash_Click(object sender, RoutedEventArgs e) => ShowHash(ActiveSelected);
    private void HashRight_Click(object sender, RoutedEventArgs e) => ShowHash(RightGrid.SelectedItem as FileItem);

    private void ShowHash(FileItem? item)
    {
        if (item == null || item.IsDirectory) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"文件：{item.FullPath}");
        sb.AppendLine();
        foreach (var algo in new[] { "MD5", "SHA1", "SHA256", "SHA384", "SHA512" })
            sb.AppendLine($"{algo,-8}: {FileSystemService.ComputeHash(item.FullPath, algo)}");

        new TextViewerDialog("哈希校验", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }

    private void FindLocker_Click(object sender, RoutedEventArgs e) => FindLocker(ActiveSelected);
    private void FindLockerRight_Click(object sender, RoutedEventArgs e) => FindLocker(RightGrid.SelectedItem as FileItem);

    private void FindLocker(FileItem? item)
    {
        if (item == null) return;

        SetStatus("正在查询占用进程…");
        var lockers = ProcessManager.FindLockingProcesses(item.FullPath);

        if (lockers.Count == 0)
        {
            MessageBox.Show($"没有进程正在占用该文件。\n\n{item.FullPath}",
                "未被占用", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("未被占用。");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"以下 {lockers.Count} 个进程正在占用：");
        sb.AppendLine($"{item.FullPath}");
        sb.AppendLine();
        foreach (var p in lockers)
            sb.AppendLine($"  PID {p.Id,-8} {p.Name}");

        var dialog = new TextViewerDialog("文件占用情况", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();

        SetStatus($"发现 {lockers.Count} 个占用进程。");
    }

    private void KillLockers_Click(object sender, RoutedEventArgs e) => KillLockers(ActiveSelected);

    private void KillLockers(FileItem? item)
    {
        if (item == null) return;
        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("强制解除占用需要管理员权限。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var lockers = ProcessManager.FindLockingProcesses(item.FullPath);
        if (lockers.Count == 0) { MessageBox.Show("没有进程占用该文件。", "提示"); return; }

        var confirm = MessageBox.Show(
            $"以下 {lockers.Count} 个进程占用该文件：\n\n" +
            string.Join("\n", lockers.Select(p => $"  PID {p.Id}  {p.Name}")) +
            $"\n\n强制结束？（可能导致程序崩溃或数据丢失）",
            "强制解除占用", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        int killed = 0; int failed = 0;
        foreach (var p in lockers)
        {
            try
            {
                var proc = Process.GetProcessById(p.Id);
                proc.Kill();
                proc.WaitForExit(2000);
                killed++;
            }
            catch { failed++; }
        }

        MessageBox.Show($"结束 {killed} 个进程。失败 {failed} 个。", "完成");

        // 尝试删除/操作
        try
        {
            if (File.Exists(item.FullPath)) File.Delete(item.FullPath);
            else if (Directory.Exists(item.FullPath)) Directory.Delete(item.FullPath, true);
            SetStatus($"已强制删除：{item.Name}");
            if (_activeIsLeft) RefreshLeft(); else RefreshRight();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"进程已结束但文件仍无法删除：{ex.Message}", "删除失败");
        }
    }

    // ==================== 高风险操作 ====================

    private void TakeOwn_Click(object sender, RoutedEventArgs e) => TakeOwnership(ActiveSelected);
    private void TakeOwnRight_Click(object sender, RoutedEventArgs e) => TakeOwnership(RightGrid.SelectedItem as FileItem);

    private void TakeOwnership(FileItem? item)
    {
        if (item == null) return;

        if (!RiskFramework.IsAllowed(RiskLevel.Dangerous))
        {
            MessageBox.Show(RiskFramework.GetBlockedMessage(RiskLevel.Dangerous),
                "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show(
                "接管所有权需要管理员权限。\n\n请以管理员身份重启 NE 管理器后重试。",
                "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Dangerous, "接管文件所有权", item.FullPath, new[]
            {
                "文件的所有者将被改为 Administrators 组",
                "会为 Administrators 追加「完全控制」权限",
                "接管后原权限设置将被覆盖（但已自动备份，可回滚）"
            }),
            "⚠️ 确认接管所有权", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        // 备份原有安全描述符
        var originalSddl = SecurityDescriptorService.ReadFileSddl(item.FullPath);
        var backup = RiskFramework.BackupSecurityDescriptor(item.FullPath, originalSddl, "接管所有权");

        var error = TrustedInstallerService.TakeOwnership(item.FullPath);

        RiskFramework.Log(RiskLevel.Dangerous, "接管所有权", item.FullPath, error == null,
            string.Empty, error, backup?.BackupPath);

        MessageBox.Show(
            error == null
                ? $"已成功接管所有权：\n{item.FullPath}\n\n原权限已备份，可在「日志与回滚」中恢复。"
                : $"接管失败：{error}\n\n可能原因：\n  · 未以管理员身份运行\n  · 文件受 Windows 资源保护 (WFP)\n  · 需要 TrustedInstaller 令牌（见「权限与提权」页面）",
            error == null ? "接管成功" : "接管失败",
            MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);

        if (_activeIsLeft) RefreshLeft(); else RefreshRight();
    }

    private void ScheduleReplace_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;

        var source = PromptDialog.Show("注册重启后替换",
            "请输入替换源文件的完整路径：\n（重启后，该源文件将替换当前选中的文件）",
            string.Empty);

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            MessageBox.Show("源文件不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            RiskFramework.BuildWarning(RiskLevel.Critical, "注册重启后文件替换",
                $"{item.FullPath} → 由 {source} 替换", new[]
                {
                    "系统重启后目标文件将被永久替换",
                    "若替换文件不兼容，可能导致系统无法启动",
                    "该操作写入 PendingFileRenameOperations，重启前可撤销"
                }),
            "⚠️ 极高危操作", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var error = FileSystemService.ScheduleReplaceOnReboot(source, item.FullPath);
        RiskFramework.Log(RiskLevel.Critical, "注册重启替换", item.FullPath, error == null,
            $"源文件：{source}", error);

        MessageBox.Show(
            error == null
                ? "已注册。重启计算机后替换操作会自动执行。"
                : $"注册失败：{error}",
            error == null ? "已注册" : "失败",
            MessageBoxButton.OK,
            error == null ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e) => OpenItem(ActiveSelected);
    private void OpenItemRight_Click(object sender, RoutedEventArgs e) => OpenItem(RightGrid.SelectedItem as FileItem);

    private void OpenLocation_Click(object sender, RoutedEventArgs e) => OpenLocation(ActiveSelected);
    private void OpenLocationRight_Click(object sender, RoutedEventArgs e) => OpenLocation(RightGrid.SelectedItem as FileItem);

    private void OpenLocation(FileItem? item)
    {
        if (item == null) return;
        try
        {
            Process.Start("explorer.exe", $"/select,\"{item.FullPath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 缺失的工具栏事件 ====================

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack(_activeIsLeft);
    private void Forward_Click(object sender, RoutedEventArgs e) => GoForward(_activeIsLeft);
    private void Up_Click(object sender, RoutedEventArgs e) => GoUp(_activeIsLeft);
    private void GoDrives_Click(object sender, RoutedEventArgs e) => ShowDrives(_activeIsLeft);

    private void MoveToRight_Click(object sender, RoutedEventArgs e)
    {
        var items = LeftGrid.SelectedItems.OfType<FileItem>().ToList();
        if (items.Count == 0) { SetStatus("请先选择要移动的项目。"); return; }
        int moved = 0;
        foreach (var item in items)
        {
            try
            {
                var dest = Path.Combine(_rightPath, item.Name);
                if (item.IsDirectory) Directory.Move(item.FullPath, dest);
                else File.Move(item.FullPath, dest);
                moved++;
            }
            catch { /* 单项失败继续 */ }
        }
        RefreshBoth();
        SetStatus($"已移动 {moved} 个项目");
    }

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Show("新建文件", "文件名：", "new.txt");
        if (string.IsNullOrWhiteSpace(name)) return;
        var path = Path.Combine(ActivePath, name);
        try
        {
            File.Create(path).Dispose();
            if (_activeIsLeft) RefreshLeft(); else RefreshRight();
            SetStatus($"已创建：{path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // 增量过滤：不重新枚举目录，直接在 ObservableCollection 上做匹配
        FilterInPlace(LeftItems, _leftPath, GetBrowseOptions());
        FilterInPlace(RightItems, _rightPath, GetBrowseOptions());
    }

    /// <summary>
    /// 在不重新枚举磁盘的前提下，对 ObservableCollection 做增量过滤。
    /// 逻辑：先完整枚举一次（如果还没缓存），再根据 FilterBox 筛。
    /// </summary>
    private void FilterInPlace(ObservableCollection<FileItem> collection, string path, FileSystemService.BrowseOptions options)
    {
        // 无关键词 = 全显示
        var keyword = FilterBox.Text?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            // 全显示：如果之前被过滤掉了，需要重新枚举
            RefreshBoth();
            return;
        }

        // 有缓存才做增量过滤，否则 refresh
        if (collection.Count == 0)
        {
            RefreshBoth();
            return;
        }

        var matches = new List<FileItem>();
        var rest = new List<FileItem>();
        foreach (var item in collection)
        {
            if (MatchesFilter(item, keyword))
                matches.Add(item);
            else
                rest.Add(item);
        }

        // 原地移除不匹配的
        foreach (var item in rest) collection.Remove(item);
    }

    /// <summary>
    /// 简单 Glob 匹配：* 匹配任意字符（不含路径分隔符），? 匹配单个字符。
    /// </summary>
    private static class Glob
    {
        public static bool Match(string pattern, string text, StringComparison cmp)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('^');
            foreach (var c in pattern)
            {
                switch (c)
                {
                    case '*': sb.Append(".*"); break;
                    case '?': sb.Append('.'); break;
                    case '.': case '\\': case '^': case '$': case '+': case '(': case ')': case '|':
                        sb.Append('\\'); sb.Append(c); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('$');
            return System.Text.RegularExpressions.Regex.IsMatch(text, sb.ToString(),
                cmp == StringComparison.OrdinalIgnoreCase
                    ? System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    : System.Text.RegularExpressions.RegexOptions.None);
        }
    }

    // ==================== 左面板导航 ====================
    private void LeftBack_Click(object sender, RoutedEventArgs e) => GoBack(true);
    private void LeftForward_Click(object sender, RoutedEventArgs e) => GoForward(true);
    private void LeftDrives_Click(object sender, RoutedEventArgs e) => ShowDrives(true);

    // ==================== 右面板导航 ====================
    private void RightBack_Click(object sender, RoutedEventArgs e) => GoBack(false);
    private void RightForward_Click(object sender, RoutedEventArgs e) => GoForward(false);
    private void RightDrives_Click(object sender, RoutedEventArgs e) => ShowDrives(false);

    // ==================== 拖拽（简单实现） ====================
    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null) return;
        var grid = (DataGrid)sender;
        bool isLeft = grid == LeftGrid;
        var targetPath = isLeft ? _leftPath : _rightPath;
        foreach (var f in files)
        {
            try
            {
                var dest = Path.Combine(targetPath, Path.GetFileName(f));
                if (Directory.Exists(f)) CopyDirectory(f, dest);
                else File.Copy(f, dest, true);
            }
            catch { }
        }
        if (isLeft) RefreshLeft(); else RefreshRight();
    }

    // ==================== 打开编辑 ====================
    private void OpenEdit_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;
        try
        {
            // 使用系统默认编辑器打开
            Process.Start(new ProcessStartInfo { FileName = item.FullPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 复制路径 ====================
    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null) return;
        Clipboard.SetText(item.FullPath);
        SetStatus($"已复制：{item.FullPath}");
    }

    private void CopyPathRight_Click(object sender, RoutedEventArgs e)
    {
        var item = RightGrid.SelectedItem as FileItem;
        if (item == null) return;
        Clipboard.SetText(item.FullPath);
        SetStatus($"已复制：{item.FullPath}");
    }

    // ==================== 面包屑更新 ====================
    private void UpdateBreadcrumbs()
    {
        LeftBreadcrumb.Text = _leftPath;
        RightBreadcrumb.Text = _rightPath;
    }

    // ===== 面包屑点击跳转 =====
    private void LeftBreadcrumb_Click(object sender, MouseButtonEventArgs e) => ShowBreadcrumbMenu((UIElement)sender, _leftPath, true);
    private void RightBreadcrumb_Click(object sender, MouseButtonEventArgs e) => ShowBreadcrumbMenu((UIElement)sender, _rightPath, false);

    private void ShowBreadcrumbMenu(UIElement anchor, string fullPath, bool isLeft)
    {
        var menu = new ContextMenu();
        var segments = SplitPathToSegments(fullPath);

        foreach (var segment in segments)
        {
            var item = new MenuItem
            {
                Header = segment.Display,
                ToolTip = segment.FullPath,
                Tag = segment.FullPath,
                FontWeight = segment.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)
                    ? FontWeights.Bold : FontWeights.Normal
            };
            item.Click += (_, _) => Navigate(segment.FullPath, isLeft);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var copyItem = new MenuItem { Header = "复制完整路径", Tag = fullPath };
        copyItem.Click += (_, _) =>
        {
            Clipboard.SetText(fullPath);
            SetStatus("已复制路径到剪贴板。");
        };
        menu.Items.Add(copyItem);

        menu.Placement = PlacementMode.Bottom;
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    private static List<(string Display, string FullPath)> SplitPathToSegments(string fullPath)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrEmpty(fullPath)) return result;

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root))
            result.Add((root.TrimEnd('\\', '/'), root));

        var rest = root != null && fullPath.Length > root.Length ? fullPath.Substring(root.Length) : string.Empty;
        if (string.IsNullOrEmpty(rest)) return result;

        var parts = rest.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = root ?? string.Empty;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            current = Path.Combine(current, part);
            result.Add((part, current));
        }

        return result;
    }

    // ==================== 快速访问侧边栏 ====================
    private void QuickAccess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        var path = tag switch
        {
            "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Downloads" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "Videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "ThisPC" => "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            "Network" => "::{208D2C60-2AE0-1069-A2DD-08002B30309D}",
            _ => null
        };
        if (string.IsNullOrEmpty(path)) return;

        if (path.StartsWith("::"))
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch { SetStatus("无法打开此位置。"); }
            return;
        }

        if (!Directory.Exists(path)) { SetStatus($"路径不存在：{path}"); return; }
        Navigate(path, _activeIsLeft);
    }

    // ==================== 高权限功能入口 ====================

    private void Decompile_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;

        // 检查是不是 .NET 程序集
        if (!DotNetDecompiler.IsDotNetAssembly(item.FullPath))
        {
            MessageBox.Show("这不是 .NET 程序集（EXE/DLL）。\n\n只能反编译 CLR 程序集，原生 C++ 程序需要用 PE 分析器。",
                "非托管程序集", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetStatus("正在反编译…");
        var result = DotNetDecompiler.Decompile(item.FullPath);

        if (!result.Success)
        {
            MessageBox.Show($"反编译失败：\n{result.Error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("反编译失败。");
            return;
        }

        // 弹出反编译结果
        var dialog = new TextViewerDialog($"反编译 · {item.Name} ({result.Types.Count} 个类型)", result.Code)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        SetStatus($"反编译完成：{result.Types.Count} 个类型");
    }

    private void AnalyzePe_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;

        var pe = PeParser.Parse(item.FullPath);
        if (!pe.IsValid)
        {
            MessageBox.Show(pe.Error, "PE 解析失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══ PE 文件分析 ═══");
        sb.AppendLine();
        sb.AppendLine($"文件        : {item.Name}");
        sb.AppendLine($"路径        : {item.FullPath}");
        sb.AppendLine($"大小        : {item.SizeText}");
        sb.AppendLine();
        sb.AppendLine("── 基本信息 ──");
        sb.AppendLine($"机器类型    : {pe.Machine}");
        sb.AppendLine($"架构        : {(pe.Is64Bit ? "PE32+ (x64/ARM64)" : "PE32 (x86/ARM32)")}");
        sb.AppendLine($"类型        : {(pe.IsDll ? "DLL" : "EXE")}");
        sb.AppendLine($"子系统      : {pe.Subsystem}");
        sb.AppendLine($"节表数量    : {pe.SectionCount}");
        sb.AppendLine($"链接时间戳  : {pe.LinkerTimestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"入口点 RVA  : 0x{pe.EntryPointRva:X}");
        sb.AppendLine($"镜像大小    : 0x{pe.ImageSize:X} ({pe.ImageSize / 1024} KB)");
        sb.AppendLine();
        sb.AppendLine("── 节表 ──");
        foreach (var sec in pe.Sections) sb.AppendLine($"  .{sec}");

        // 尝试签名
        var sig = SignatureService.Verify(item.FullPath);
        sb.AppendLine();
        sb.AppendLine("── 数字签名 ──");
        if (sig.HasSignature)
        {
            sb.AppendLine($"签名者      : {sig.Signer}");
            sb.AppendLine($"颁发者      : {sig.Issuer}");
            sb.AppendLine($"有效期      : {sig.NotBefore:yyyy-MM-dd} → {sig.NotAfter:yyyy-MM-dd}");
            sb.AppendLine($"指纹        : {sig.Thumbprint}");
            sb.AppendLine($"算法        : {sig.Algorithm}");
            sb.AppendLine($"验证状态    : {(sig.IsValid ? "✅ 有效" : "❌ 无效或已篡改")}");
        }
        else
        {
            sb.AppendLine("未签名（Authenticode 签名不存在）");
        }

        sb.AppendLine();
        sb.AppendLine("── 哈希 ──");
        foreach (var algo in new[] { "SHA256", "SHA1", "MD5" })
            sb.AppendLine($"{algo,-8}: {SignatureService.ComputeHash(item.FullPath, algo)}");

        new TextViewerDialog($"PE 分析 · {item.Name}", sb.ToString())
        {
            Owner = Application.Current.MainWindow
        }.ShowDialog();
    }

    private void ViewHex_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;

        SetStatus("正在读取…");
        try
        {
            // 限制前 512KB，避免大文件卡死
            const int maxSize = 512 * 1024;
            var fileInfo = new FileInfo(item.FullPath);
            int readLen = (int)Math.Min(maxSize, fileInfo.Length);
            var bytes = HexDumpService.ReadRange(item.FullPath, 0, readLen);
            var dump = HexDumpService.ToHexDump(bytes);

            var dialog = new TextViewerDialog(
                $"Hex Dump · {item.Name} ({readLen / 1024} KB / {item.SizeText})", dump)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
            SetStatus($"Hex 查看完成：{readLen} 字节");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewPid_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;
        var lockers = ProcessManager.FindLockingProcesses(item.FullPath);
        if (lockers.Count == 0)
        {
            MessageBox.Show("没有进程占用该文件。", "未被占用");
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"占用进程（{lockers.Count}）:");
        foreach (var p in lockers) sb.AppendLine($"  PID {p.Id,-8} {p.Name}");
        new TextViewerDialog("占用情况", sb.ToString()) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void SignFile_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;
        var sig = SignatureService.Verify(item.FullPath);
        var sb = new System.Text.StringBuilder();
        if (sig.HasSignature)
        {
            sb.AppendLine($"✅ 已签名：{(sig.IsValid ? "有效" : "无效/篡改")}");
            sb.AppendLine($"  签名者 : {sig.Signer}");
            sb.AppendLine($"  颁发者 : {sig.Issuer}");
            sb.AppendLine($"  有效期 : {sig.NotBefore:yyyy-MM-dd} → {sig.NotAfter:yyyy-MM-dd}");
            sb.AppendLine($"  指纹   : {sig.Thumbprint}");
        }
        else
        {
            sb.AppendLine("❌ 未签名");
        }
        new TextViewerDialog("签名验证", sb.ToString()) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    // ==================== 高级修改功能 ====================

    private void EditHex_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;
        new HexEditorDialog(item.FullPath) { Owner = Application.Current.MainWindow }.Show();
    }

    private void InjectDll_Click(object sender, RoutedEventArgs e)
    {
        // 选 DLL → 选进程 → 注入
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DLL 文件|*.dll|所有文件|*.*",
            Title = "选择要注入的 DLL"
        };
        if (dlg.ShowDialog() != true) return;

        var dllPath = dlg.FileName;

        // 简单版：让用户输入 PID
        var pidDlg = new PromptDialog("DLL 注入", "必须有管理员权限！\n目标进程 PID：", "")
        { Owner = Application.Current.MainWindow };
        pidDlg.ShowDialog();
        var pidStr = pidDlg.InputText;
        if (!int.TryParse(pidStr, out var pid)) return;

        if (!PrivilegeService.IsElevated())
        {
            MessageBox.Show("必须以管理员身份运行才能注入。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetStatus("正在注入…");
        var result = DllInjector.Inject(pid, dllPath);
        if (result.Success)
        {
            MessageBox.Show(result.Message, "✅ 注入成功");
            SetStatus(result.Message);
        }
        else
        {
            MessageBox.Show(result.Message, "❌ 注入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("注入失败。");
        }
    }

    private void SetAttr_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null) return;
        // 简单版：直接给常用选项
        var choice = MessageBox.Show(
            $"文件：{item.Name}\n路径：{item.FullPath}\n\n选择操作：\n" +
            "   Yes = 设为隐藏\n   No = 清除隐藏\n   Cancel = 只读切换",
            "属性修改", MessageBoxButton.YesNoCancel);

        if (item.IsDirectory)
        {
            var r = choice switch
            {
                MessageBoxResult.Yes => FileAttributeService.SetAttributes(item.FullPath, hidden: true),
                MessageBoxResult.No => FileAttributeService.SetAttributes(item.FullPath, hidden: false),
                MessageBoxResult.Cancel => FileAttributeService.SetAttributes(item.FullPath, readOnly: (item.Attributes & FileAttributes.ReadOnly) == 0),
                _ => (Ok: false, Msg: "")
            };
            MessageBox.Show(r.Ok ? r.Msg : "失败：" + r.Msg);
        }
        else
        {
            var r = choice switch
            {
                MessageBoxResult.Yes => FileAttributeService.SetAttributes(item.FullPath, hidden: true),
                MessageBoxResult.No => FileAttributeService.SetAttributes(item.FullPath, hidden: false),
                MessageBoxResult.Cancel => FileAttributeService.SetAttributes(item.FullPath, readOnly: (item.Attributes & FileAttributes.ReadOnly) == 0),
                _ => (Ok: false, Msg: "")
            };
            MessageBox.Show(r.Ok ? r.Msg : "失败：" + r.Msg);
        }
        if (_activeIsLeft) RefreshLeft(); else RefreshRight();
    }

    private void SetTimestamp_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null) return;
        var now = DateTime.Now;
        var dialog = new PromptDialog("篡改时间戳",
            "输入时间（格式 yyyy-MM-dd HH:mm:ss），留空保持不变\n创建时间:",
            now.ToString("yyyy-MM-dd HH:mm:ss"))
        { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        var input = dialog.InputText;
        if (string.IsNullOrEmpty(input)) return;

        if (!DateTime.TryParse(input, out var dt))
        {
            MessageBox.Show("时间格式错误。", "错误");
            return;
        }

        var r = FileAttributeService.SetTimestamps(item.FullPath, creation: dt, access: dt, write: dt);
        MessageBox.Show(r.Ok ? $"✅ 已设为 {dt:yyyy-MM-dd HH:mm:ss}" : "❌ " + r.Msg);
    }

    private void ReadVer_Click(object sender, RoutedEventArgs e)
    {
        var item = ActiveSelected;
        if (item == null || item.IsDirectory) return;
        var vi = PeResourceService.ReadVersionInfo(item.FullPath) ?? PeResourceService.ReadDotNetInfo(item.FullPath);
        if (vi == null) { MessageBox.Show("无法读取版本信息（可能不是 PE 文件）。"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("── PE 版本资源 ──");
        sb.AppendLine($"文件描述   : {vi.FileDescription}");
        sb.AppendLine($"产品名称   : {vi.ProductName}");
        sb.AppendLine($"公司       : {vi.CompanyName}");
        sb.AppendLine($"原始文件名 : {vi.OriginalFilename}");
        sb.AppendLine($"版权       : {vi.LegalCopyright}");
        sb.AppendLine($"文件版本   : {vi.FileVersion}");
        sb.AppendLine($"产品版本   : {vi.ProductVersion}");
        new TextViewerDialog($"版本信息 · {item.Name}", sb.ToString()) { Owner = Application.Current.MainWindow }.ShowDialog();
    }
}

