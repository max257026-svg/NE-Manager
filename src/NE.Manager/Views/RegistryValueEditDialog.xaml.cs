using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Registry;
using NEManager.Core.Risk;

namespace NEManager.App.Views;

/// <summary>
/// 注册表值编辑窗口 —— 支持全部值类型的读写与格式转换。
/// </summary>
public partial class RegistryValueEditDialog : Window
{
    private readonly string _keyPath;
    private readonly Core.Registry.RegistryService.RegistryValueItem? _existing;

    public RegistryValueEditDialog(string keyPath, Core.Registry.RegistryService.RegistryValueItem? existing = null)
    {
        InitializeComponent();
        _keyPath = keyPath;
        _existing = existing;

        KeyPathText.Text = keyPath;
        Title = existing == null ? "新建注册表值" : "编辑注册表值";

        if (existing == null)
        {
            NameBox.Text = "NewValue";
            TypeBox.SelectedIndex = 0;
            NameBox.SelectAll();
        }
        else
        {
            NameBox.Text = existing.Name;
            NameBox.IsEnabled = true;
            SelectType(existing.Kind);
            DataBox.Text = FormatForEdit(existing);
        }

        UpdateHint();
        NameBox.Focus();
    }

    private void SelectType(RegistryValueKind kind)
    {
        for (int i = 0; i < TypeBox.Items.Count; i++)
        {
            if (TypeBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == kind.ToString())
            {
                TypeBox.SelectedIndex = i;
                return;
            }
        }
        TypeBox.SelectedIndex = 0;
    }

    private RegistryValueKind SelectedKind =>
        Enum.TryParse<RegistryValueKind>(((ComboBoxItem)TypeBox.SelectedItem).Tag?.ToString(), out var kind)
            ? kind
            : RegistryValueKind.String;

    private string FormatForEdit(Core.Registry.RegistryService.RegistryValueItem item)
    {
        return item.Data switch
        {
            byte[] bytes => Convert.ToHexString(bytes),
            string[] multi => string.Join("\r\n", multi),
            uint dword => $"0x{dword:X8} ({dword})",
            ulong qword => $"0x{qword:X16} ({qword})",
            string s => s,
            _ => item.Data?.ToString() ?? string.Empty
        };
    }

    private void UpdateHint()
    {
        DataHint.Text = SelectedKind switch
        {
            RegistryValueKind.String => "字符串：直接输入文本",
            RegistryValueKind.ExpandString => "可展开字符串：可包含 %SystemRoot% 之类的环境变量",
            RegistryValueKind.MultiString => "多字符串：每行一个字符串",
            RegistryValueKind.DWord => "32 位数：可输入十进制（如 1）或十六进制（如 0x1）",
            RegistryValueKind.QWord => "64 位数：可输入十进制或十六进制",
            RegistryValueKind.Binary => "二进制：输入十六进制字符，如 00FF1A2B",
            _ => string.Empty
        };
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHint();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ErrorText.Text = "值名称不能为空。";
            return;
        }

        try
        {
            var kind = SelectedKind;
            var text = DataBox.Text;
            object data;

            switch (kind)
            {
                case RegistryValueKind.DWord:
                    data = ParseNumber(text, 32);
                    break;

                case RegistryValueKind.QWord:
                    data = ParseNumber(text, 64);
                    break;

                case RegistryValueKind.MultiString:
                    data = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    break;

                case RegistryValueKind.Binary:
                    data = ParseHex(text);
                    break;

                default:
                    data = text;
                    break;
            }

            var error = Core.Registry.RegistryService.SetValue(_keyPath, name, data, kind);
            if (error != null)
            {
                ErrorText.Text = $"写入失败：{error}\n\nHKLM 下的键值通常需要管理员权限。";
                return;
            }

            bool isSystemPath = _keyPath.Contains(@"HKEY_LOCAL_MACHINE\SYSTEM", StringComparison.OrdinalIgnoreCase);
            RiskFramework.Log(
                isSystemPath ? RiskLevel.Dangerous : RiskLevel.Caution,
                _existing == null ? "新建注册表值" : "修改注册表值",
                $"{_keyPath}\\{name}", true, $"{kind} = {text}");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"数据格式错误：{ex.Message}";
        }
    }

    private static object ParseNumber(string text, int bits)
    {
        text = text.Trim();

        // 支持 "0x1 (1)" 这种显示格式
        if (text.Contains('('))
            text = text[..text.IndexOf('(')].Trim();

        ulong value;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = Convert.ToUInt64(text[2..], 16);
        }
        else
        {
            value = ulong.Parse(text, CultureInfo.InvariantCulture);
        }

        return bits == 32 ? (object)unchecked((uint)value) : value;
    }

    private static byte[] ParseHex(string text)
    {
        var clean = new StringBuilder();
        foreach (var c in text)
        {
            if (Uri.IsHexDigit(c)) clean.Append(c);
        }

        if (clean.Length % 2 != 0)
            throw new FormatException("十六进制字符数必须为偶数。");

        var result = new byte[clean.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = Convert.ToByte(clean.ToString().Substring(i * 2, 2), 16);

        return result;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
