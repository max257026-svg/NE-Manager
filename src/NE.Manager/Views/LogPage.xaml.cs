using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Risk;

namespace NEManager.App.Views;

public partial class LogPage : UserControl, IRefreshable
{
    public LogPage()
    {
        InitializeComponent();
    }

    public void OnEnter() => Refresh();
    public void OnLeave() { }

    private void Refresh()
    {
        LogGrid.ItemsSource = RiskFramework.GetLogs();
        BackupGrid.ItemsSource = RiskFramework.GetBackups();
        ErrorGrid.ItemsSource = App.ErrorLog;

        int total = RiskFramework.GetLogs().Count;
        int failed = RiskFramework.GetLogs().Count(l => !l.Success);
        int backups = RiskFramework.GetBackups().Count;
        SummaryText.Text = $"日志 {total} 条（失败 {failed}） · 备份 {backups} 个 · 运行时错误 {App.ErrorLog.Count} 条";
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确认清空所有操作日志？备份记录不会被删除。", "清空日志",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            RiskFramework.ClearLogs();
            Refresh();
        }
    }

    private void OpenBackup_Click(object sender, RoutedEventArgs e)
        => RiskFramework.OpenBackupFolder();

    private void Rollback_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BackupRecord record })
        {
            if (MessageBox.Show(
                    $"确认将备份恢复到：\n{record.OriginalPath}\n\n操作：{record.Operation}\n备份时间：{record.CreatedAtText}",
                    "回滚确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var error = RiskFramework.Rollback(record);
            if (error == null)
                MessageBox.Show("回滚成功。", "回滚", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"回滚失败：{error}", "回滚", MessageBoxButton.OK, MessageBoxImage.Error);
            Refresh();
        }
    }
}
