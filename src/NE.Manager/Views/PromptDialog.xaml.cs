using System.Windows;

namespace NEManager.App.Views;

/// <summary>
/// 简易输入对话框。
/// </summary>
public partial class PromptDialog : Window
{
    public string InputText => InputBox.Text.Trim();

    public PromptDialog(string title, string message, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    public static string? Show(string title, string message, string defaultValue = "")
    {
        var dialog = new PromptDialog(title, message, defaultValue)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            MessageBox.Show("请输入内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) Ok_Click(sender, e);
        if (e.Key == System.Windows.Input.Key.Escape) Cancel_Click(sender, e);
    }
}
