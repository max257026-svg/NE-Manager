using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NEManager.Core.Preview;

namespace NEManager.App.Views;

public partial class PreviewDialog : Window
{
    public PreviewDialog()
    {
        InitializeComponent();
    }

    public void ShowPreview(string filePath)
    {
        try
        {
            PreviewTitle.Text = System.IO.Path.GetFileName(filePath);
            var previewType = PreviewService.GetPreviewType(filePath);
            PreviewType.Text = previewType;

            switch (previewType)
            {
                case "Image":
                    ShowImagePreview(filePath);
                    break;
                case "Text":
                    ShowTextPreview(filePath);
                    break;
                case "Video":
                case "Audio":
                    ShowMediaPreview(filePath);
                    break;
                case "Font":
                    ShowFontPreview(filePath);
                    break;
                default:
                    ShowBinaryPreview(filePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"预览失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowImagePreview(string filePath)
    {
        var image = new System.Windows.Controls.Image
        {
            Stretch = Stretch.Uniform,
            Source = new BitmapImage(new Uri(filePath))
        };
        PreviewContainer.Children.Add(image);
    }

    private void ShowTextPreview(string filePath)
    {
        var lines = NEManager.Core.Text.LargeTextReader.ReadLines(filePath, 1000);
        var text = string.Join("\n", lines);
        
        var textBox = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            FontFamily = (FontFamily)FindResource("MonoFont"),
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            Background = (Brush)FindResource("BgPanelBrush"),
            BorderThickness = new Thickness(0),
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(10)
        };
        PreviewContainer.Children.Add(textBox);
    }

    private void ShowMediaPreview(string filePath)
    {
        var mediaElement = new MediaElement
        {
            Source = new Uri(filePath),
            LoadedBehavior = MediaState.Play,
            UnloadedBehavior = MediaState.Close
        };
        PreviewContainer.Children.Add(mediaElement);
    }

    private void ShowFontPreview(string filePath)
    {
        var fontFamily = new FontFamily(new Uri(filePath), "./#");
        var sampleText = new TextBlock
        {
            Text = "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz\n0123456789\n你好世界 Hello World",
            FontFamily = fontFamily,
            FontSize = 24,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        PreviewContainer.Children.Add(sampleText);
    }

    private void ShowBinaryPreview(string filePath)
    {
        var infoText = new TextBlock
        {
            Text = $"二进制文件\n\n路径: {filePath}\n\n大小: {new System.IO.FileInfo(filePath).Length:N0} 字节\n\n无法预览此文件类型。",
            Foreground = (Brush)FindResource("TextBrush"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        PreviewContainer.Children.Add(infoText);
    }
}
