using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using NEManager.App.Views;

namespace SmokeTest;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        // 加载 Theme.xaml 资源字典，确保页面里用到的 StaticResource 都能找到
        LoadThemeResources(app);

        var results = new StringBuilder();
        results.AppendLine("=== NE Manager 冒烟测试 ===");
        results.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        results.AppendLine();

        var pages = new[]
        {
            typeof(FilesPage), typeof(SecurityPage), typeof(RegistryPage),
            typeof(ProcessPage), typeof(ServicePage), typeof(DiskPage),
            typeof(WmiPage), typeof(PePage), typeof(HexEditorPage),
            typeof(TextEditorPage), typeof(MemoryPage), typeof(ArchivePage),
            typeof(DiffPage), typeof(NetworkPage), typeof(ScriptPage),
            typeof(ToolboxPage), typeof(DiskSectorPage), typeof(BatchRenamePage),
            typeof(DataFormatPage), typeof(LinuxFileSystemPage), typeof(MacFileSystemPage),
            typeof(StartupPage), typeof(LogPage), typeof(AboutPage),
            typeof(MemoryEditorPage),
        };

        int pass = 0, fail = 0;
        var sbLock = new object();

        foreach (var type in pages)
        {
            app.Dispatcher.Invoke(() =>
            {
                try
                {
                    var page = Activator.CreateInstance(type);
                    results.AppendLine($"✅ {type.Name} - OK");
                    Interlocked.Increment(ref pass);
                }
                catch (Exception ex)
                {
                    results.AppendLine($"❌ {type.Name} - FAIL");
                    results.AppendLine($"   消息: {ex.Message}");
                    results.AppendLine($"   堆栈: {(ex.StackTrace?.Split('\n').FirstOrDefault() ?? "")}");
                    if (ex.InnerException != null)
                    {
                        results.AppendLine($"   内部: {ex.InnerException.Message}");
                        results.AppendLine($"   内部堆栈: {(ex.InnerException.StackTrace?.Split('\n').FirstOrDefault() ?? "")}");
                    }
                    Interlocked.Increment(ref fail);
                }
            });
        }

        results.AppendLine();
        results.AppendLine($"=== 结果: {pass} 通过 / {fail} 失败 ===");

        var path = Path.Combine(Path.GetTempPath(), "nemanager_smoke_test.txt");
        File.WriteAllText(path, results.ToString());

        Console.WriteLine(results.ToString());
        Console.WriteLine($"\n报告已保存: {path}");

        app.Shutdown();
    }

    /// <summary>
    /// 手动加载 Theme.xaml 到 Application.Resources，避免每个页面因找不到 StaticResource 而崩。
    /// </summary>
    private static void LoadThemeResources(Application app)
    {
        // Theme.xaml 在 NE.Manager 程序集的 Themes 文件夹下
        var themeUri = new Uri("pack://application:,,,/NE.Manager;component/Themes/Theme.xaml", UriKind.Absolute);
        try
        {
            var themeDict = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(themeDict);
            Console.WriteLine("[OK] Theme.xaml 加载成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Theme.xaml 加载失败: {ex.Message}");
            // 尝试相对路径
            try
            {
                var fallbackUri = new Uri("Themes/Theme.xaml", UriKind.Relative);
                var themeDict = new ResourceDictionary { Source = fallbackUri };
                app.Resources.MergedDictionaries.Add(themeDict);
                Console.WriteLine("[OK] Theme.xaml（相对路径）加载成功");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[ERROR] Theme.xaml 相对路径也失败: {ex2.Message}");
            }
        }
    }
}
