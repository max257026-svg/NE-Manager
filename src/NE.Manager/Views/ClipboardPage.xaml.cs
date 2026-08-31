using System;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class ClipboardPage : UserControl
{
    private ClipboardMonitor? _monitor;

    public ClipboardPage() { InitializeComponent(); }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor == null)
        {
            _monitor = new ClipboardMonitor();
            _monitor.EntryAdded += _ => Dispatcher.Invoke(() => HistoryList.ItemsSource = null);
            _monitor.Start();
            ToggleBtn.Content = "停止监控";
            StatusText.Text = "（监控中）";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            HistoryList.ItemsSource = _monitor.History;
        }
        else
        {
            _monitor.Stop();
            _monitor.Dispose();
            _monitor = null;
            ToggleBtn.Content = "开始监控";
            StatusText.Text = "（已停止）";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _monitor?.Clear();
        HistoryList.ItemsSource = null;
        PreviewBox.Text = "";
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is ClipboardEntry entry)
            PreviewBox.Text = entry.Text;
    }

    private void CopyBack_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PreviewBox.Text))
        {
            try { Clipboard.SetText(PreviewBox.Text); }
            catch { }
        }
    }

    private void EditRewrite_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry) return;
        var dlg = new TextBox
        {
            Text = entry.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 300, Width = 500, FontFamily = (System.Windows.Media.FontFamily)FindResource("MonoFont")
        };
        var win = new Window
        {
            Title = "编辑并重写", Content = dlg,
            Width = 540, Height = 400,
            Owner = Window.GetWindow(this)
        };
        win.ShowDialog();
        entry.Text = dlg.Text;
        PreviewBox.Text = dlg.Text;
        try { Clipboard.SetText(dlg.Text); } catch { }
    }
}
