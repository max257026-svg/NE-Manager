using System.Windows;
using NEManager.Core.Risk;

namespace NEManager.App.Views;

public partial class SettingsDialog : Window
{
    public SafetyMode SelectedMode { get; private set; } = RiskFramework.CurrentMode;

    public SettingsDialog()
    {
        InitializeComponent();
        ApplyModeToUi(RiskFramework.CurrentMode);
    }

    private void ApplyModeToUi(SafetyMode mode)
    {
        ModeNormal.IsChecked = mode == SafetyMode.Normal;
        ModeAdvanced.IsChecked = mode == SafetyMode.Advanced;
        ModeExpert.IsChecked = mode == SafetyMode.Expert;
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (ModeNormal.IsChecked == true) SelectedMode = SafetyMode.Normal;
        else if (ModeAdvanced.IsChecked == true) SelectedMode = SafetyMode.Advanced;
        else if (ModeExpert.IsChecked == true) SelectedMode = SafetyMode.Expert;
    }

    private void OpenBackup_Click(object sender, RoutedEventArgs e)
        => RiskFramework.OpenBackupFolder();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// 仅切换安全模式的轻量对话框（复用设置窗口的界面）。
/// </summary>
public partial class SafetyModeDialog : SettingsDialog
{
    public SafetyModeDialog() : base()
    {
        Title = "切换安全模式";
    }
}
