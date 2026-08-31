using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using NEManager.Core.Risk;

namespace NEManager.App;

/// <summary>
/// 运行时错误条目（供日志页/状态栏展示）。
/// </summary>
public sealed class ErrorEntry
{
    public int Id { get; init; }
    public string Time { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// NE 管理器应用程序入口 —— NewEra Studio。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// CLR 级别最早能执行的代码 —— 确保 GB2312/GBK 在任何类型初始化前就注册好。
    /// </summary>
    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>已捕获的运行时错误（异常被全局处理器兜住后进入这里，不弹窗刷屏）。</summary>
    public static ObservableCollection<ErrorEntry> ErrorLog { get; } = new();

    /// <summary>发生错误时触发，主窗口订阅以更新状态栏。</summary>
    public static event Action<ErrorEntry>? ErrorOccurred;

    private static int _errorSeq;
    private static readonly object _logLock = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 尽早注册 CodePages 编码提供者，确保 GB2312/GBK 等在任何页面加载前就可用
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 全局异常兜底：系统级工具崩一次代价太大。
        // 改为「记录 + 状态栏提示」，不再每个异常弹一个阻塞 MessageBox，避免报错海啸。
        DispatcherUnhandledException += (_, args) =>
        {
            ReportError(args.Exception, "UI 线程未处理异常");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                ReportError(ex, "非 UI 线程未处理异常");
        };

        // 默认安全模式：普通用户
        RiskFramework.CurrentMode = SafetyMode.Normal;
    }

    /// <summary>
    /// 统一记录一个错误：写临时日志文件、加入错误集合、通知状态栏。
    /// 业务代码也可显式调用它来把友好的错误信息送进日志。
    /// </summary>
    public static void ReportError(Exception ex, string? source = null)
    {
        if (ex == null) return;
        var entry = new ErrorEntry
        {
            Id = System.Threading.Interlocked.Increment(ref _errorSeq),
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Message = ex.Message,
            Source = source ?? ex.Source ?? string.Empty
        };

        lock (_logLock)
        {
            ErrorLog.Add(entry);
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "nemanager_runtime.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{entry.Source}] {ex}\n\n");
            }
            catch { /* 忽略日志失败 */ }
        }

        ErrorOccurred?.Invoke(entry);
    }

    /// <summary>记录一条友好错误信息（不携带异常对象时使用）。</summary>
    public static void ReportMessage(string message, string? source = null)
    {
        ReportError(new Exception(message), source);
    }
}
