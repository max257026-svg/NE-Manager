using System.Windows;
using NEManager.Core.Security;

namespace NEManager.App.Views;

/// <summary>
/// 安全描述符查看窗口 —— DACL / SACL / SDDL 三视图。
/// </summary>
public partial class AclViewerDialog : Window
{
    private readonly SecurityDescriptorService.SecurityInfo _info;

    public AclViewerDialog(SecurityDescriptorService.SecurityInfo info)
    {
        InitializeComponent();
        _info = info;

        PathText.Text = _info.Path;
        OwnerText.Text = string.IsNullOrEmpty(_info.Owner)
            ? $"{_info.OwnerSid}（无法解析名称）"
            : $"{_info.Owner}  [{_info.OwnerSid}]";
        GroupText.Text = _info.Group;

        DaclGrid.ItemsSource = _info.Dacl;
        SaclGrid.ItemsSource = _info.Sacl;
        SddlBox.Text = _info.Sddl;

        if (!string.IsNullOrEmpty(_info.Error))
        {
            SaclNotice.Text = $"读取安全描述符时出现错误：{_info.Error}";
            SaclNotice.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }
        else if (_info.Sacl.Count == 0)
        {
            SaclNotice.Text = "该对象没有配置审计规则 (SACL)。\n" +
                              "读取 SACL 需要 SeSecurityPrivilege「管理审核和安全日志」特权，通常需要管理员权限。";
        }
        else
        {
            SaclNotice.Text = $"共 {_info.Sacl.Count} 条审计规则。";
        }
    }

    private void CopySddl_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_info.Sddl);
        MessageBox.Show("SDDL 已复制到剪贴板。\n\n" +
                        "你可以使用「权限与提权」页面把这段 SDDL 作为权限模板批量应用到其它目录。",
            "已复制", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
