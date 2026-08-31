using System.Text;
using System.Windows;
using System.Windows.Input;
using NEManager.Core.Tools;

namespace NEManager.App.Views;

public partial class HexEditorDialog : Window
{
    private readonly string _filePath;
    private const int MaxDisplay = 64 * 1024;

    public HexEditorDialog(string filePath)
    {
        InitializeComponent();
        _filePath = filePath;
        FileInfoText.Text = $"{Path.GetFileName(filePath)}   |   {new FileInfo(filePath).Length:N0} 字节   |   路径: {filePath}";

        var readLen = (int)Math.Min(MaxDisplay, new FileInfo(filePath).Length);
        var bytes = HexDumpService.ReadRange(filePath, 0, readLen);
        HexDumpBox.Text = HexDumpService.ToHexDump(bytes);
    }

    private void PatchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ApplyPatch_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ApplyPatch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = PatchInput.Text.Trim();
            if (string.IsNullOrEmpty(input)) { MessageBox.Show("请输入 patch。", "提示"); return; }

            // 格式解析：第一行或第一部分是偏移（支持 0x100 和十进制），后面是字节
            var tokens = input.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) { MessageBox.Show("格式错误。\n示例：0x100 90 90 90", "错误"); return; }

            // 解析偏移
            long offset;
            if (tokens[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                offset = Convert.ToInt64(tokens[0], 16);
            else
                offset = Convert.ToInt64(tokens[0]);

            // 解析字节
            var bytes = new byte[tokens.Length - 1];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(tokens[i + 1], 16);

            if (MessageBox.Show(
                $"将在偏移 0x{offset:X} ({offset}) 写入 {bytes.Length} 字节：\n\n{BitConverter.ToString(bytes)}\n\n确定写入？此操作直接修改原文件！",
                "确认 Patch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // 先备份
            var backupPath = _filePath + ".bak";
            try { File.Copy(_filePath, backupPath, true); } catch { /* 备份失败不阻止写入 */ }

            var ok = HexDumpService.PatchBytes(_filePath, offset, bytes);
            if (ok)
            {
                MessageBox.Show($"✅ Patch 成功！\n偏移 0x{offset:X} 写入 {bytes.Length} 字节。\n备份：{backupPath}", "完成");
                // 刷新显示
                var readLen = (int)Math.Min(MaxDisplay, new FileInfo(_filePath).Length);
                var newBytes = HexDumpService.ReadRange(_filePath, 0, readLen);
                HexDumpBox.Text = HexDumpService.ToHexDump(newBytes);
            }
            else
            {
                MessageBox.Show("❌ Patch 失败。文件可能被占用或只读。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"错误：{ex.Message}", "Patch 失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
