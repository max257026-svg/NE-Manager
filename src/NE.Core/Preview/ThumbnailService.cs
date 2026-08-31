using System.Windows.Media.Imaging;
using System.IO;

namespace NEManager.Core.Preview;

public static class ThumbnailService
{
    // 生成缩略图
    public static BitmapSource? GenerateThumbnail(string filePath, int size = 128)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLower();
            
            // 图片文件
            if (IsImageFile(ext))
            {
                return GenerateImageThumbnail(filePath, size);
            }
            
            // 视频文件
            if (IsVideoFile(ext))
            {
                return GenerateVideoThumbnail(filePath, size);
            }
            
            // 文档文件
            if (IsDocumentFile(ext))
            {
                return GenerateDocumentIcon(ext, size);
            }
            
            // 压缩文件
            if (IsArchiveFile(ext))
            {
                return GenerateArchiveIcon(ext, size);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource GenerateImageThumbnail(string filePath, int size)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath);
        bitmap.DecodePixelWidth = size;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource? GenerateVideoThumbnail(string filePath, int size)
    {
        // 使用 Windows Shell 获取视频缩略图
        // 这里简化处理，返回 null
        // 实际实现需要使用 Windows API Code Pack 或类似库
        return null;
    }

    private static BitmapSource GenerateDocumentIcon(string ext, int size)
    {
        // 生成文档图标（使用 DrawingVisual）
        var visual = new System.Windows.Media.DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // 绘制文档背景
            var bgBrush = ext switch
            {
                ".pdf" => System.Windows.Media.Brushes.Red,
                ".doc" or ".docx" => System.Windows.Media.Brushes.Blue,
                ".xls" or ".xlsx" => System.Windows.Media.Brushes.Green,
                ".ppt" or ".pptx" => System.Windows.Media.Brushes.Orange,
                ".txt" => System.Windows.Media.Brushes.Gray,
                _ => System.Windows.Media.Brushes.LightGray
            };
            
            var rect = new System.Windows.Rect(0, 0, size, size);
            context.DrawRectangle(bgBrush, null, rect);
            
            // 绘制扩展名文字
            var text = ext.TrimStart('.').ToUpper();
            var formattedText = new System.Windows.Media.FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface("Segoe UI"),
                size / 4,
                System.Windows.Media.Brushes.White,
                1);
            
            var textWidth = formattedText.Width;
            var textHeight = formattedText.Height;
            context.DrawText(formattedText, new System.Windows.Point((size - textWidth) / 2, (size - textHeight) / 2));
        }
        
        var renderBitmap = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        renderBitmap.Render(visual);
        renderBitmap.Freeze();
        return renderBitmap;
    }

    private static BitmapSource GenerateArchiveIcon(string ext, int size)
    {
        var visual = new System.Windows.Media.DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var rect = new System.Windows.Rect(0, 0, size, size);
            context.DrawRectangle(System.Windows.Media.Brushes.DarkOrange, null, rect);
            
            var text = ext.TrimStart('.').ToUpper();
            var formattedText = new System.Windows.Media.FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface("Segoe UI"),
                size / 4,
                System.Windows.Media.Brushes.White,
                1);
            
            var textWidth = formattedText.Width;
            var textHeight = formattedText.Height;
            context.DrawText(formattedText, new System.Windows.Point((size - textWidth) / 2, (size - textHeight) / 2));
        }
        
        var renderBitmap = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        renderBitmap.Render(visual);
        renderBitmap.Freeze();
        return renderBitmap;
    }

    private static bool IsImageFile(string ext) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" or ".tiff";

    private static bool IsVideoFile(string ext) =>
        ext is ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm";

    private static bool IsDocumentFile(string ext) =>
        ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt";

    private static bool IsArchiveFile(string ext) =>
        ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz";

    // 清除缩略图缓存
    public static void ClearThumbnailCache()
    {
        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NEManager",
                "Thumbnails");
            
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
        catch
        {
            // 忽略错误
        }
    }
}
