using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NEManager.Core.Risk;
using NEManager.Core.Security;
using NEManager.Core.SystemTools;
using NEManager.Core.Tools;

namespace NEManager.App.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, UserControl> _pages = new();
    private readonly List<TabInfo> _tabs = new();
    private string _currentPage = "Files";
    private string _activeTabKey = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        App.ErrorOccurred += OnAppError;
    }

    private class TabInfo
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
    }

    private int _errorCount;

    private void OnAppError(ErrorEntry entry)
    {
        _errorCount++;
        ErrorBadge.Visibility = Visibility.Visible;
        ErrorCountText.Text = _errorCount.ToString();
        StatusBarText.Text = $"⚠️ 运行时错误：{entry.Message}";
        StatusBarText.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
    }

    private void ErrorBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => Navigate("Log");

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 自动冒烟测试：遍历所有页面并写结果到 %TEMP%\ne_smoke.log（仅当设置了环境变量）。
        if (Environment.GetEnvironmentVariable("NE_SMOKE") == "1")
        {
            RunSmokeTest();
            return;
        }

        Navigate("Files"); // 先让 UI 出现
            // 启动后后台检查更新（不阻塞 UI）
            _ = Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(2000); // 等 UI 稳定
                    var (hasUpdate, tag, url, _) = await NEManager.Core.SystemTools.UpdateService.CheckAsync();
                    if (hasUpdate && !string.IsNullOrEmpty(url))
                    {
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            string msg = "发现新版本 " + tag
                                + "（当前 v" + NEManager.Core.SystemTools.UpdateService.CurrentVersion + "）。\n\n"
                                + "是否跳转到 GitHub 下载？";
                            var result = System.Windows.MessageBox.Show(
                                msg,
                                "NE Manager 有更新",
                                System.Windows.MessageBoxButton.YesNo,
                                System.Windows.MessageBoxImage.Information);
                            if (result == System.Windows.MessageBoxResult.Yes)
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                        });
                    }
                }
                catch { /* 更新失败不影响主流程 */ }
            });

        try
        {
            // 特权枚举是 P/Invoke 重活，扔后台
            var (elevated, highInt, integrity, privileges) = await System.Threading.Tasks.Task.Run(() =>
            {
                var privs = PrivilegeService.Enumerate();
                return (
                    PrivilegeService.IsElevated(),
                    PrivilegeService.IsHighIntegrity(),
                    PrivilegeService.GetIntegrityLevel(),
                    privs
                );
            });

            ElevationText.Text = elevated ? "管理员" : "标准用户";
            ElevationBadge.Background = elevated
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : (System.Windows.Media.Brush)FindResource("BgElevatedBrush");
            ElevateButton.Visibility = elevated ? Visibility.Collapsed : Visibility.Visible;
            ElevateButton.Content = highInt ? "已提升（仍非管理员）" : "以管理员身份重启";
            IntegrityText.Text = $"完整性：{integrity}";
            int enabled = privileges.Count(p => p.Enabled);
            PrivilegeText.Text = $"特权：{enabled}/{privileges.Count} 已启用";
            UpdateSafeModeBadge();
        }
        catch (Exception ex) { App.ReportError(ex, "初始化状态栏"); }

        try
        {
            if (!PrivilegeService.IsElevated())
                SetStatus("当前为标准用户权限。部分系统级功能（接管 TrustedInstaller、修改系统文件、读取 SACL）需要管理员权限。");
        }
        catch { }
    }

    // ==================== 导航 ====================

    private static readonly Dictionary<string, string> PageTitles = new()
    {
        ["Dashboard"] = "实时仪表盘",
        ["Files"] = "文件管理",
        ["Security"] = "权限与提权",
        ["Registry"] = "注册表编辑器",
        ["Process"] = "进程管理器",
        ["Service"] = "服务管理器",
        ["Disk"] = "磁盘与卷",
        ["Wmi"] = "WMI 控制台",
        ["Pe"] = "PE 文件分析",
        ["HexEditor"] = "HEX 编辑器",
        ["TextEditor"] = "文本编辑器",
        ["MemoryEditor"] = "内存修改",
        ["Injector"] = "DLL 注入器",
        ["Toolbox"] = "工具箱",
        ["Diff"] = "Diff 对比",
        ["Archive"] = "归档浏览",
        ["Network"] = "网络文件",
        ["Script"] = "脚本引擎",
        ["DiskSector"] = "磁盘扇区编辑",
        ["BatchRename"] = "批量重命名",
        ["DataFormat"] = "数据格式化",
        ["LinuxFS"] = "Linux 文件系统",
        ["MacFS"] = "macOS 文件系统",
        ["Log"] = "日志与回滚",
        ["Startup"] = "启动项管理",
        ["About"] = "关于"
    };

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string page)
            Navigate(page);
    }

    public void Navigate(string pageKey)
    {
        // 更新导航按钮状态
        foreach (var child in NavPanel.Children)
        {
            if (child is Button navButton)
                navButton.Tag = navButton.CommandParameter?.ToString() == pageKey ? "Active" : null;
        }

        try
        {
            if (!_pages.TryGetValue(pageKey, out var page))
            {
                page = pageKey switch
                {
                    "Dashboard" => new DashboardPage(),
                    "Files" => new FilesPage(),
                    "Security" => new SecurityPage(),
                    "Registry" => new RegistryPage(),
                    "Process" => new ProcessPage(),
                    "Service" => new ServicePage(),
                    "Disk" => new DiskPage(),
                    "Wmi" => new WmiPage(),
                    "Pe" => new PePage(),
                    "HexEditor" => new HexEditorPage(),
                    "TextEditor" => new TextEditorPage(),
                    "MemoryEditor" => new MemoryPage(),
                    "Injector" => new InjectorPage(),
                    "Toolbox" => new ToolboxPage(),
                    "Diff" => new DiffPage(),
                    "Archive" => new ArchivePage(),
                    "Network" => new NetworkPage(),
                    "Script" => new ScriptPage(),
                    "DiskSector" => new DiskSectorPage(),
                    "BatchRename" => new BatchRenamePage(),
                    "DataFormat" => new DataFormatPage(),
                    "LinuxFS" => new LinuxFileSystemPage(),
                    "MacFS" => new MacFileSystemPage(),
                                                            "Log" => new LogPage(),
                    "SystemInfo" => new SystemInfoPage(),
                    "Clipboard" => new ClipboardPage(),
                    "ProcessTree" => new ProcessTreePage(),
                    "Startup" => new StartupPage(),
                    "About" => new AboutPage(),
                    _ => new FilesPage()
                };
                _pages[pageKey] = page;
            }

            // 离开页面时保存状态
            if (PageHost.Content is IRefreshable previous)
                previous.OnLeave();

            PageHost.Content = page;
            _currentPage = pageKey;

            // 进入页面时刷新
            if (page is IRefreshable refreshable)
                refreshable.OnEnter();

            // 更新标签页
            ActivateTab(pageKey);

            // 重置全局状态栏文字为当前页面标题
            SetStatus($"已切换到「{PageTitles.GetValueOrDefault(pageKey, pageKey)}」");
        }
        catch (Exception ex)
        {
            App.ReportError(ex, $"打开页面失败：{pageKey}");
            ShowFatalError($"打开「{pageKey}」页面时出错：{ex.Message}");
        }
    }

    // ==================== 标签页系统 ====================

    private void ActivateTab(string pageKey)
    {
        // 查找是否已有标签
        var existing = _tabs.FirstOrDefault(t => t.Key == pageKey);
        if (existing == null)
        {
            existing = new TabInfo { Key = pageKey, Title = PageTitles.GetValueOrDefault(pageKey, pageKey) };
            _tabs.Add(existing);
            AddTabButton(existing);
        }

        _activeTabKey = pageKey;
        RefreshTabButtons();
    }

    private void AddTabButton(TabInfo tab)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 2, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = tab.Key
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        var title = new TextBlock
        {
            Text = tab.Title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(title);

        // 关闭按钮
        var closeBtn = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        closeBtn.MouseLeftButtonDown += (s, e) => CloseTab(tab.Key);
        sp.Children.Add(closeBtn);

        border.Child = sp;
        border.MouseLeftButtonDown += (s, e) => Navigate(tab.Key);

        TabBar.Children.Add(border);
    }

    private void RefreshTabButtons()
    {
        foreach (var child in TabBar.Children)
        {
            if (child is Border border && border.Tag is string key)
            {
                bool isActive = key == _activeTabKey;
                border.Background = isActive
                    ? (Brush)FindResource("BgPanelBrush")
                    : Brushes.Transparent;

                if (border.Child is StackPanel sp)
                {
                    foreach (var spChild in sp.Children)
                    {
                        if (spChild is TextBlock tb)
                        {
                            tb.Foreground = isActive
                                ? (Brush)FindResource("TextBrush")
                                : (Brush)FindResource("TextMutedBrush");
                        }
                    }
                }
            }
        }
    }

    private void CloseTab(string pageKey)
    {
        if (_tabs.Count <= 1) return; // 至少保留一个标签

        var tab = _tabs.FirstOrDefault(t => t.Key == pageKey);
        if (tab == null) return;

        _tabs.Remove(tab);
        _pages.Remove(pageKey);

        // 移除标签按钮
        var toRemove = TabBar.Children.OfType<Border>()
            .FirstOrDefault(b => b.Tag as string == pageKey);
        if (toRemove != null)
            TabBar.Children.Remove(toRemove);

        // 如果关闭的是当前活动标签，切换到第一个
        if (_activeTabKey == pageKey && _tabs.Count > 0)
        {
            Navigate(_tabs[0].Key);
        }
    }

    public void ToggleToolbox()
    {
        ToolboxPanel.Visibility = ToolboxPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// 当某个页面初始化/刷新失败时，在内容区显示可读的错误信息，而不是留下白屏。
    /// </summary>
    private void ShowFatalError(string message)
    {
        try
        {
            PageHost.Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = "⚠️ 页面加载失败\n\n" + message +
                           "\n\n详细信息已记录到：%TEMP%\\nemanager_runtime.log",
                    Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                    FontFamily = (System.Windows.Media.FontFamily)FindResource("UIFont"),
                    FontSize = 14,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(20)
                }
            };
        }
        catch { /* 极端情况下连错误页都无法构建，放弃 */ }
    }

    // ==================== 状态栏 ====================

    public void SetStatus(string message)
    {
        StatusBarText.Text = message;
    }

    /// <summary>
    /// 跳转到文件管理器并定位到指定路径（如挂载 VHD 后直接浏览）。
    /// </summary>
    public void OpenPathInFiles(string path, bool isLeft = true)
    {
        Navigate("Files");
        if (PageHost.Content is FilesPage fp)
            fp.NavigateToPath(path, isLeft);
    }

    private void RefreshStatusBar()
    {
        // 管理员状态
        bool elevated = PrivilegeService.IsElevated();
        ElevationText.Text = elevated ? "管理员" : "标准用户";
        ElevationBadge.Background = elevated
            ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
            : (System.Windows.Media.Brush)FindResource("BgElevatedBrush");

        ElevateButton.Visibility = elevated ? Visibility.Collapsed : Visibility.Visible;
        ElevateButton.Content = PrivilegeService.IsHighIntegrity()
            ? "已提升（仍非管理员）" : "以管理员身份重启";

        // 完整性级别
        IntegrityText.Text = $"完整性：{PrivilegeService.GetIntegrityLevel()}";

        // 特权数量
        var privileges = PrivilegeService.Enumerate();
        int enabled = privileges.Count(p => p.Enabled);
        PrivilegeText.Text = $"特权：{enabled}/{privileges.Count} 已启用";

        // 安全模式
        UpdateSafeModeBadge();
    }

    private void UpdateSafeModeBadge()
    {
        var (text, brush) = RiskFramework.CurrentMode switch
        {
            SafetyMode.Normal => ("普通模式", "WarningBrush"),
            SafetyMode.Advanced => ("高级模式", "InfoBrush"),
            SafetyMode.Expert => ("专家模式", "DangerBrush"),
            _ => ("未知", "WarningBrush")
        };

        SafeModeText.Text = text;
        SafeModeBadge.Background = (System.Windows.Media.Brush)FindResource(brush);
    }

    // ==================== 事件 ====================

    private void ElevationBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => PromptElevate();

    private void Elevate_Click(object sender, RoutedEventArgs e)
        => PromptElevate();

    private void PromptElevate()
    {
        if (PrivilegeService.IsElevated())
        {
            MessageBox.Show(
                "当前已以管理员身份运行，完整性级别为「高」。\n\n" +
                "若需操作受 TrustedInstaller 保护的系统文件，请前往「权限与提权」页面使用接管工具。",
                "已具备管理员权限", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            "NE 管理器需要管理员权限才能执行以下操作：\n\n" +
            "  · 接管 TrustedInstaller 权限并修改系统文件\n" +
            "  · 启用 SeDebug / SeTakeOwnership 等关键特权\n" +
            "  · 读取安全审计日志 (SACL)\n" +
            "  · 管理系统服务与驱动\n\n" +
            "是否现在以管理员身份重启？",
            "请求提升权限", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (TrustedInstallerService.RestartAsAdministrator())
                Application.Current.Shutdown();
            else
                MessageBox.Show("提权已取消或失败。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SafeModeBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dialog = new SafetyModeDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            RiskFramework.CurrentMode = dialog.SelectedMode;
            UpdateSafeModeBadge();
            SetStatus($"安全模式已切换为「{RiskFramework.GetModeName(dialog.SelectedMode)}」。");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            RiskFramework.CurrentMode = dialog.SelectedMode;
            UpdateSafeModeBadge();
            RefreshStatusBar();
        }
    }

    // ==================== Win11 窗口控制 ====================

    private void TitleBar_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (sender is Button btn) btn.Content = "\xE922";
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (sender is Button btn) btn.Content = "\xE923";
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ==================== 快速工具箱事件 ====================

    private void QuickConverter_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var input = QuickConverterInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                QuickConverterResult.Text = "";
                return;
            }

            long value;
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(input.Substring(2), 16);
            else if (input.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(input.Substring(2), 2);
            else if (long.TryParse(input, out var dec))
                value = dec;
            else
            {
                QuickConverterResult.Text = "无效输入";
                return;
            }

            QuickConverterResult.Text = $"十进制: {value}\n十六进制: 0x{value:X}\n二进制: 0b{Convert.ToString(value, 2)}";
        }
        catch
        {
            QuickConverterResult.Text = "转换错误";
        }
    }

    private void QuickHash_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择文件" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var hash = HashService.ComputeFileHash(dlg.FileName, "SHA256");
                QuickHashResult.Text = $"SHA256:\n{hash}";
            }
            catch (Exception ex)
            {
                QuickHashResult.Text = $"错误: {ex.Message}";
            }
        }
    }

    private void QuickRegexTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pattern = QuickRegexPattern.Text;
            var input = QuickRegexInput.Text;
            if (string.IsNullOrEmpty(pattern))
            {
                QuickRegexResult.Text = "请输入正则表达式";
                return;
            }

            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var matches = regex.Matches(input);
            QuickRegexResult.Text = $"找到 {matches.Count} 个匹配";
        }
        catch (Exception ex)
        {
            QuickRegexResult.Text = $"错误: {ex.Message}";
        }
    }

    private void QuickBase64Encode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = QuickEncodingInput.Text;
            var encoded = ConverterService.Base64Encode(System.Text.Encoding.UTF8.GetBytes(input));
            QuickEncodingResult.Text = encoded;
        }
        catch (Exception ex)
        {
            QuickEncodingResult.Text = $"错误: {ex.Message}";
        }
    }

    private void QuickBase64Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = QuickEncodingInput.Text;
            var decoded = ConverterService.Base64Decode(input);
            QuickEncodingResult.Text = System.Text.Encoding.UTF8.GetString(decoded);
        }
        catch (Exception ex)
        {
            QuickEncodingResult.Text = $"错误: {ex.Message}";
        }
    }

    // ==================== 自动冒烟测试（仅调试用） ====================

    private void RunSmokeTest()
    {
        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ne_smoke.log");
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("SMOKE START " + DateTime.Now);

        var keys = new[] { "Dashboard", "Files", "Security", "Registry", "Process", "Service", "Disk", "Wmi", "Pe", "HexEditor", "TextEditor", "MemoryEditor", "Injector", "Toolbox", "Diff", "Archive", "Network", "Script", "DiskSector", "BatchRename", "DataFormat", "LinuxFS", "MacFS", "Startup", "Log", "About" };
        foreach (var k in keys)
        {
            try
            {
                Navigate(k);
                if (PageHost.Content is ScrollViewer sv)
                {
                    var tb = sv.Content as TextBlock;
                    lines.Add($"FAIL {k}: {(tb?.Text ?? "(无文本)")}");
                }
                else
                {
                    lines.Add($"OK   {k} -> {PageHost.Content?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                lines.Add($"THROW {k}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        lines.Add("SMOKE END");
        try { System.IO.File.WriteAllText(logPath, string.Join("\n", lines)); } catch { }
        Application.Current.Shutdown();
    }
}

/// <summary>
/// 支持进入/离开刷新的页面接口。
/// </summary>
public interface IRefreshable
{
    void OnEnter();
    void OnLeave();
}




