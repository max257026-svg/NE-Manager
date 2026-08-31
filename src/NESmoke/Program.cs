using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NEManager.App.Views;

namespace NESmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // 看门狗：若某页面 OnEnter 弹阻塞对话框或卡死，35s 后强制退出，保证日志可写。
        var watchdog = new Thread(() => { Thread.Sleep(35000); Append("WATCHDOG: 超时强制退出（卡在上一页）"); Environment.Exit(2); })
        {
            IsBackground = true
        };
        watchdog.Start();

        void Log(string s) => Append(s);

        try
        {
            var app = new Application();

            // 合并主题资源，确保页面 XAML 中的 StaticResource 能解析。
            try
            {
                var theme = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/NE.Manager;component/Themes/Theme.xaml")
                };
                app.Resources.MergedDictionaries.Add(theme);
                Log("THEME: 已合并主题资源");
            }
            catch (Exception ex)
            {
                Log($"THEME FAIL: {ex.GetType().Name}: {ex.Message}");
            }

            app.Startup += (_, _) =>
            {
                try
                {
                    RunPages(app, Log);
                }
                catch (Exception ex)
                {
                    Log($"HARNESS THROW: {ex.GetType().Name}: {ex.Message}");
                    Log(ex.StackTrace ?? "");
                }
                finally
                {
                    Log("SMOKE END");
                    app.Shutdown();
                }
            };

            app.Run();
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex.GetType().Name}: {ex.Message}");
            Log(ex.StackTrace ?? "");
            return 1;
        }

        return 0;
    }

    private static void RunPages(Application app, Action<string> log)
    {
        var asm = typeof(NEManager.App.App).Assembly;
        var pages = asm.GetTypes()
            .Where(t => typeof(UserControl).IsAssignableFrom(t) && t.Name.EndsWith("Page"))
            .OrderBy(t => t.Name)
            .ToList();

        log($"PAGES FOUND: {pages.Count}");

        foreach (var t in pages)
        {
            UserControl? page = null;
            try
            {
                page = (UserControl)Activator.CreateInstance(t)!;
                log($"CTOR OK   {t.Name}");
            }
            catch (Exception ex)
            {
                log($"CTOR FAIL {t.Name}: {ex.GetType().Name}: {ex.Message}");
                log(Flatten(ex));
                continue;
            }

            if (page is IRefreshable r)
            {
                try
                {
                    r.OnEnter();
                    log($"ENTER OK   {t.Name}");
                }
                catch (Exception ex)
                {
                    log($"ENTER FAIL {t.Name}: {ex.GetType().Name}: {ex.Message}");
                    log(Flatten(ex));
                }

                try { r.OnLeave(); }
                catch { /* 忽略离开异常 */ }
            }
        }
    }

    private static string Flatten(Exception ex)
    {
        var lines = new List<string>();
        var cur = ex;
        while (cur != null)
        {
            lines.Add($"   -> {cur.GetType().Name}: {cur.Message}");
            cur = cur.InnerException;
        }
        return string.Join("\n", lines);
    }

    private static void Append(string content)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ne_smoke2.log");
            File.AppendAllText(path, content + "\n");
        }
        catch { }
    }
}
