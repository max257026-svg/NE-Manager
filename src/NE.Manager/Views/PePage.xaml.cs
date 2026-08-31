using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NEManager.Core.Binary;
using NEManager.Core.Risk;

namespace NEManager.App.Views;

public partial class PePage : UserControl, IRefreshable
{
    private string SelectedFilePath => PathBox.Text.Trim();

    public PePage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void SetStatus(string message)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.SetStatus(message);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "可执行文件 (*.exe;*.dll;*.sys;*.ocx)|*.exe;*.dll;*.sys;*.ocx|所有文件 (*.*)|*.*",
            Title = "选择 PE 文件"
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FileName;
            Parse_Click(sender, e);
        }
    }

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            MessageBox.Show("请先选择有效的 PE 文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pe = PeParser.Parse(path);
        if (!pe.IsValid)
        {
            KindText.Text = $"解析失败：{pe.Error}";
            Tabs.IsEnabled = false;
            RiskFramework.Log(RiskLevel.Caution, "解析 PE", path, false, pe.Error);
            return;
        }

        KindText.Text = $"类型：{pe.FileKindText}  ·  架构：{pe.MachineName}  ·  " +
                        $"{(pe.Is64Bit ? "64 位" : "32 位")}  ·  子系统：{pe.SubsystemName}  ·  " +
                        $"链接器：{pe.LinkerVersionText}  ·  签名：{(pe.IsSigned ? "已签名" : "未签名")}  ·  " +
                        $"校验和：{(pe.CheckSumValid ? "有效" : "无效/缺失")}";

        Tabs.IsEnabled = true;

        OverviewGrid.ItemsSource = new[]
        {
            new PeField("文件路径", pe.FilePath),
            new PeField("文件种类", pe.FileKindText),
            new PeField("机器架构", pe.MachineName),
            new PeField("节数量", pe.NumberOfSections.ToString()),
            new PeField("时间戳", $"{pe.TimeStamp:yyyy-MM-dd HH:mm:ss} (0x{pe.TimeDateStamp:X8})"),
            new PeField("是否是 DLL", pe.IsDll ? "是" : "否"),
            new PeField("是否驱动", pe.IsDriver ? "是" : "否"),
            new PeField("是否 .NET", pe.IsDotNet ? "是" : "否"),
            new PeField("子系统", pe.SubsystemName),
            new PeField("映像基址", $"0x{pe.ImageBase:X}"),
            new PeField("入口点", $"0x{pe.EntryPoint:X8}"),
            new PeField("映像大小", $"0x{pe.SizeOfImage:X8}"),
            new PeField("文件特征", pe.CharacteristicsText),
            new PeField("DLL 特征", pe.DllCharacteristicsText),
            new PeField("存储校验和", $"0x{pe.StoredCheckSum:X8}"),
            new PeField("计算校验和", $"0x{pe.CalculatedCheckSum:X8}"),
            new PeField("校验和状态", pe.CheckSumValid ? "有效" : "无效或缺失"),
        };

        SectionGrid.ItemsSource = pe.Sections;
        ExportGrid.ItemsSource = pe.Exports;
        ResourceGrid.ItemsSource = pe.Resources;
        DirGrid.ItemsSource = pe.DataDirectories;
        SigGrid.ItemsSource = pe.GetSignatureInfo();

        ImportTree.Items.Clear();
        foreach (var imp in pe.Imports)
        {
            var node = new TreeViewItem
            {
                Header = $"{imp.DllName}  ({imp.FunctionCount} 个函数)",
                Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush")
            };
            foreach (var fn in imp.Functions)
                node.Items.Add(new TreeViewItem { Header = fn });
            ImportTree.Items.Add(node);
        }

        RiskFramework.Log(RiskLevel.Safe, "解析 PE", path, true, pe.FileKindText);
    }

    private async void ExtractStrings_Click(object sender, RoutedEventArgs e)
    {
        var path = PromptDialog.Show("文件路径", "字符串提取器",
            string.IsNullOrEmpty(SelectedFilePath) ? @"C:\Windows\System32\notepad.exe" : SelectedFilePath);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

        SetStatus("提取字符串中…");
        try
        {
            var hits = await System.Threading.Tasks.Task.Run(() => StringExtractor.ExtractFromFile(path));
            var sb = new System.Text.StringBuilder();
            foreach (var h in hits.Take(5000))
                sb.AppendLine($"0x{h.Offset:X8}  [{(h.IsUnicode ? "U" : "A")}]  {h.Text}");
            new TextViewerDialog($"字符串 ({hits.Count} 条，显示前 {Math.Min(5000, hits.Count)})", sb.ToString())
                { Owner = Application.Current.MainWindow }.ShowDialog();
            SetStatus($"字符串提取完成，共 {hits.Count} 条。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "提取失败");
            SetStatus("字符串提取失败。");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
    private static extern int PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon, out IntPtr phicon, out IntPtr piconid, int nIcons, int flags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private void ExtractIcon_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SelectedFilePath) || !System.IO.File.Exists(SelectedFilePath))
        {
            MessageBox.Show("请先选择文件。");
            return;
        }
        try
        {
            int count = PrivateExtractIcons(SelectedFilePath, 0, 256, 256, out var hIcon, out _, 1, 0);
            if (count <= 0 || hIcon == IntPtr.Zero)
            {
                MessageBox.Show("无法提取图标（文件可能没有图标资源）");
                return;
            }

            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png|ICO|*.ico",
                FileName = System.IO.Path.GetFileNameWithoutExtension(SelectedFilePath) + "_icon"
            };
            if (saveDlg.ShowDialog() != true) { DestroyIcon(hIcon); return; }

            var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon, System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(hIcon);

            var path = saveDlg.FileName;
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                bs.Freeze();
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bs));
                using var fs = System.IO.File.Create(path);
                encoder.Save(fs);
            }
            else
            {
                // ICO 格式：手动写文件头
                bs.Freeze();
                var bmp = System.Windows.Media.Imaging.BitmapFrame.Create(bs);
                var encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
                encoder.Frames.Add(bmp);
                using var ms = new System.IO.MemoryStream();
                encoder.Save(ms);
                var bmpBytes = ms.ToArray();

                // ICO header (6 bytes) + ICONDIRENTRY (16 bytes) + BMP data (starting from offset 14)
                using var fs = System.IO.File.Create(path);
                using var bw = new System.IO.BinaryWriter(fs);
                bw.Write((ushort)0);           // reserved
                bw.Write((ushort)1);           // ico type
                bw.Write((ushort)1);           // 1 icon
                int w = (int)bs.Width, h = (int)bs.Height;
                bw.Write((byte)(w > 255 ? 0 : w));
                bw.Write((byte)(h > 255 ? 0 : h));
                bw.Write((byte)0);              // color count
                bw.Write((byte)0);              // reserved
                bw.Write((ushort)1);            // color planes
                bw.Write((ushort)32);           // bits per pixel
                // Calculate BMP size from ICO perspective: BMP file size minus the 14-byte BITMAPFILEHEADER
                int bmpContentSize = bmpBytes.Length - 14;
                bw.Write(bmpContentSize);
                int imageOffset = 6 + 16;       // header + entry
                bw.Write(imageOffset);
                // Write BMP without file header (starting from BITMAPINFOHEADER)
                fs.Write(bmpBytes, 14, bmpContentSize);
            }
            MessageBox.Show($"已保存到 {path}");
            SetStatus($"图标已保存：{path}");
        }
        catch (Exception ex) { MessageBox.Show("提取失败：" + ex.Message); }
    }
}

public class PeField
{
    public string Key { get; }
    public string Value { get; }
    public PeField(string key, string value) { Key = key; Value = value; }
}
