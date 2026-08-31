using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace NEManager.Core.SystemTools;

/// <summary>
/// WPF 剪贴板历史监控 — 使用 DispatcherTimer 轮询（无窗口消息钩子依赖）。
/// </summary>
public class ClipboardMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private string _lastText = "";

    public List<ClipboardEntry> History { get; } = new();
    public int MaxHistory { get; set; } = 100;

    public event Action<ClipboardEntry>? EntryAdded;

    public ClipboardMonitor()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text) || text == _lastText) return;
            _lastText = text;

            var entry = new ClipboardEntry
            {
                Text = text,
                Timestamp = DateTime.Now,
                Length = text.Length
            };
            History.Insert(0, entry);
            while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
            EntryAdded?.Invoke(entry);
        }
        catch { /* clipboard may be locked by other app */ }
    }

    public void Clear() { History.Clear(); _lastText = ""; }

    public void Dispose() { _timer.Stop(); }
}

public class ClipboardEntry
{
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int Length { get; set; }

    public string Preview => Text.Length > 120 ? Text[..120] + "..." : Text;
}
