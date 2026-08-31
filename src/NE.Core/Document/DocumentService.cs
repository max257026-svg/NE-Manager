using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace NEManager.Core.Document;

public static class DocumentService
{
    // PDF 基础信息读取（不依赖第三方库）
    public static PdfInfo ReadPdfInfo(string path)
    {
        var info = new PdfInfo();
        try
        {
            using var fs = File.OpenRead(path);
            using var reader = new StreamReader(fs);
            
            // 读取文件头
            var header = reader.ReadLine();
            if (header?.StartsWith("%PDF-") == true)
                info.Version = header.Substring(5);
            
            // 简单统计页数（查找 /Type /Page）
            fs.Position = 0;
            var content = reader.ReadToEnd();
            info.PageCount = System.Text.RegularExpressions.Regex.Matches(content, @"/Type\s*/Page[^s]").Count;
            
            // 读取文件大小
            info.FileSize = new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    // Office 文档（DOCX/XLSX/PPTX）读取
    public static OfficeInfo ReadOfficeInfo(string path)
    {
        var info = new OfficeInfo();
        try
        {
            using var archive = ZipFile.OpenRead(path);
            
            // 读取 core.xml
            var coreEntry = archive.GetEntry("docProps/core.xml");
            if (coreEntry != null)
            {
                using var stream = coreEntry.Open();
                var doc = XDocument.Load(stream);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                
                info.Title = doc.Root?.Element(ns + "title")?.Value ?? "";
                info.Creator = doc.Root?.Element(ns + "creator")?.Value ?? "";
                info.Description = doc.Root?.Element(ns + "description")?.Value ?? "";
            }
            
            // 读取 app.xml
            var appEntry = archive.GetEntry("docProps/app.xml");
            if (appEntry != null)
            {
                using var stream = appEntry.Open();
                var doc = XDocument.Load(stream);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                
                if (int.TryParse(doc.Root?.Element(ns + "Pages")?.Value, out var pages))
                    info.Pages = pages;
                if (int.TryParse(doc.Root?.Element(ns + "Words")?.Value, out var words))
                    info.Words = words;
            }
            
            info.FileSize = new FileInfo(path).Length;
            info.Type = Path.GetExtension(path).ToLower() switch
            {
                ".docx" => "Word 文档",
                ".xlsx" => "Excel 工作簿",
                ".pptx" => "PowerPoint 演示文稿",
                _ => "Office 文档"
            };
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    // EPUB 电子书读取
    public static EpubInfo ReadEpubInfo(string path)
    {
        var info = new EpubInfo();
        try
        {
            using var archive = ZipFile.OpenRead(path);
            
            // 读取 container.xml
            var containerEntry = archive.GetEntry("META-INF/container.xml");
            if (containerEntry != null)
            {
                using var stream = containerEntry.Open();
                var doc = XDocument.Load(stream);
                var rootFile = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile");
                var opfPath = rootFile?.Attribute("full-path")?.Value;
                
                if (!string.IsNullOrEmpty(opfPath))
                {
                    var opfEntry = archive.GetEntry(opfPath);
                    if (opfEntry != null)
                    {
                        using var opfStream = opfEntry.Open();
                        var opfDoc = XDocument.Load(opfStream);
                        var metadata = opfDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "metadata");
                        
                        if (metadata != null)
                        {
                            info.Title = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? "";
                            info.Creator = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "creator")?.Value ?? "";
                            info.Language = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "language")?.Value ?? "";
                        }
                    }
                }
            }
            
            info.FileSize = new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    // SVG 矢量图读取
    public static SvgInfo ReadSvgInfo(string path)
    {
        var info = new SvgInfo();
        try
        {
            var doc = XDocument.Load(path);
            var svg = doc.Root;
            
            if (svg?.Name.LocalName == "svg")
            {
                info.Width = svg.Attribute("width")?.Value ?? "auto";
                info.Height = svg.Attribute("height")?.Value ?? "auto";
                info.ViewBox = svg.Attribute("viewBox")?.Value ?? "";
                
                // 统计元素数量
                info.ElementCount = svg.Descendants().Count();
            }
            
            info.FileSize = new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        return info;
    }

    // JSON 格式化
    public static string FormatJson(string json, bool indent = true)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indent
            };
            var obj = JsonSerializer.Deserialize<object>(json);
            return JsonSerializer.Serialize(obj, options);
        }
        catch
        {
            return json; // 返回原始内容
        }
    }

    // XML 格式化
    public static string FormatXml(string xml, bool indent = true)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.ToString(indent ? SaveOptions.None : SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml;
        }
    }

    // CSV 解析
    public static List<List<string>> ParseCsv(string path, char delimiter = ',')
    {
        var result = new List<List<string>>();
        try
        {
            using var reader = new StreamReader(path);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var fields = ParseCsvLine(line, delimiter);
                result.Add(fields);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"CSV 解析失败: {ex.Message}");
        }
        return result;
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        fields.Add(current.ToString());
        return fields;
    }
}

public class PdfInfo
{
    public string Version { get; set; } = "";
    public int PageCount { get; set; }
    public long FileSize { get; set; }
    public string Error { get; set; } = "";
}

public class OfficeInfo
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Creator { get; set; } = "";
    public string Description { get; set; } = "";
    public int Pages { get; set; }
    public int Words { get; set; }
    public long FileSize { get; set; }
    public string Error { get; set; } = "";
}

public class EpubInfo
{
    public string Title { get; set; } = "";
    public string Creator { get; set; } = "";
    public string Language { get; set; } = "";
    public long FileSize { get; set; }
    public string Error { get; set; } = "";
}

public class SvgInfo
{
    public string Width { get; set; } = "";
    public string Height { get; set; } = "";
    public string ViewBox { get; set; } = "";
    public int ElementCount { get; set; }
    public long FileSize { get; set; }
    public string Error { get; set; } = "";
}
