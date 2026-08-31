using System.Windows;
using Microsoft.Win32;

namespace NEManager.App.Views;

/// <summary>
/// WPF 没有原生文件夹选择对话框，这里用 OpenFileDialog 的文件夹选择技巧替代 WinForms 的 FolderBrowserDialog。
/// </summary>
public static class DialogHelper
{
    public static string? PickFolder(Window? owner, string description, string? defaultPath = null)
    {
        var dialog = new OpenFileDialog
        {
            ValidateNames = false,
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = defaultPath ?? "文件夹选择",
            Title = description
        };

        var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        if (result != true) return null;

        var picked = Path.GetDirectoryName(dialog.FileName);
        return string.IsNullOrEmpty(picked) ? null : picked;
    }
}
