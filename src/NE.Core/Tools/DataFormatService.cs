using System.Text.RegularExpressions;

namespace NEManager.Core.Tools;

/// <summary>
/// 数据格式化工具 - JSON/XML/YAML/CSV 格式化与转换
/// </summary>
public static class DataFormatService
{
    /// <summary>
    /// JSON 压缩（最小化）
    /// </summary>
    public static string MinifyJson(string json)
    {
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
            return System.Text.Json.JsonSerializer.Serialize(obj);
        }
        catch { return json; }
    }

    /// <summary>
    /// JSON 格式化
    /// </summary>
    public static string FormatJson(string json)
    {
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
            return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch { return json; }
    }

    /// <summary>
    /// XML 转 JSON
    /// </summary>
    public static string XmlToJson(string xml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var dict = new Dictionary<string, object>();
            
            if (doc.Root != null)
            {
                dict = XmlElementToDict(doc.Root);
            }
            
            return System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// JSON 转 XML
    /// </summary>
    public static string JsonToXml(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var rootElement = new System.Xml.Linq.XElement("root");
            
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    rootElement.Add(JsonElementToXml(prop.Name, prop.Value));
                }
            }
            
            return rootElement.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static Dictionary<string, object> XmlElementToDict(System.Xml.Linq.XElement element)
    {
        var dict = new Dictionary<string, object>();
        
        foreach (var child in element.Elements())
        {
            var key = child.Name.LocalName;
            var value = XmlElementToValue(child);
            
            if (dict.ContainsKey(key))
            {
                // 如果键已存在，转换为数组
                if (dict[key] is List<object> list)
                {
                    list.Add(value);
                }
                else
                {
                    dict[key] = new List<object> { dict[key], value };
                }
            }
            else
            {
                dict[key] = value;
            }
        }
        
        return dict;
    }

    private static object XmlElementToValue(System.Xml.Linq.XElement element)
    {
        if (!element.HasElements)
        {
            var text = element.Value.Trim();
            if (int.TryParse(text, out int i)) return i;
            if (double.TryParse(text, out double d)) return d;
            if (bool.TryParse(text, out bool b)) return b;
            return text;
        }
        
        return XmlElementToDict(element);
    }

    private static System.Xml.Linq.XElement JsonElementToXml(string name, System.Text.Json.JsonElement element)
    {
        var xmlElement = new System.Xml.Linq.XElement(name);
        
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    xmlElement.Add(JsonElementToXml(prop.Name, prop.Value));
                }
                break;
                
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    xmlElement.Add(JsonElementToXml("item", item));
                }
                break;
                
            case System.Text.Json.JsonValueKind.String:
                xmlElement.Value = element.GetString() ?? "";
                break;
                
            case System.Text.Json.JsonValueKind.Number:
                xmlElement.Value = element.GetRawText();
                break;
                
            case System.Text.Json.JsonValueKind.True:
                xmlElement.Value = "true";
                break;
                
            case System.Text.Json.JsonValueKind.False:
                xmlElement.Value = "false";
                break;
                
            case System.Text.Json.JsonValueKind.Null:
                xmlElement.Value = "";
                break;
        }
        
        return xmlElement;
    }

    /// <summary>
    /// JSON 验证
    /// </summary>
    public static (bool Valid, string Error) ValidateJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// XML 格式化
    /// </summary>
    public static string FormatXml(string xml, bool indent = true)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            return doc.ToString();
        }
        catch { return xml; }
    }

    /// <summary>
    /// XML 压缩
    /// </summary>
    public static string MinifyXml(string xml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            return doc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        catch { return xml; }
    }

    /// <summary>
    /// XML 验证
    /// </summary>
    public static (bool Valid, string Error) ValidateXml(string xml)
    {
        try
        {
            System.Xml.Linq.XDocument.Parse(xml);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// YAML 转 JSON（简单实现）
    /// </summary>
    public static string YamlToJson(string yaml)
    {
        try
        {
            var dict = ParseSimpleYaml(yaml);
            return System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// JSON 转 YAML（简单实现）
    /// </summary>
    public static string JsonToYaml(string json)
    {
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
            return ConvertToYaml(obj, 0);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// CSV 解析
    /// </summary>
    public static List<List<string>> ParseCsv(string csv, char delimiter = ',')
    {
        var rows = new List<List<string>>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var row = ParseCsvLine(line.TrimEnd('\r'), delimiter);
            if (row.Count > 0)
                rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// CSV 生成
    /// </summary>
    public static string GenerateCsv(List<List<string>> rows, char delimiter = ',')
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            var line = string.Join(delimiter.ToString(), 
                row.Select(cell => EscapeCsvCell(cell, delimiter)));
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// CSV 转 JSON
    /// </summary>
    public static string CsvToJson(string csv, bool useFirstRowAsHeader = true, char delimiter = ',')
    {
        try
        {
            var rows = ParseCsv(csv, delimiter);
            if (rows.Count == 0) return "[]";

            if (useFirstRowAsHeader && rows.Count > 1)
            {
                var headers = rows[0];
                var data = rows.Skip(1).Select(row =>
                {
                    var dict = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count && i < row.Count; i++)
                        dict[headers[i]] = row[i];
                    return dict;
                }).ToList();

                return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            else
            {
                return System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// JSON 转 CSV
    /// </summary>
    public static string JsonToCsv(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var sb = new StringBuilder();

            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var elements = doc.RootElement.EnumerateArray().ToList();
                if (elements.Count == 0) return "";

                // 获取所有键
                var headers = new List<string>();
                foreach (var el in elements)
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var prop in el.EnumerateObject())
                        {
                            if (!headers.Contains(prop.Name))
                                headers.Add(prop.Name);
                        }
                    }
                }

                // 写表头
                sb.AppendLine(string.Join(",", headers));

                // 写数据
                foreach (var el in elements)
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var values = headers.Select(h =>
                        {
                            if (el.TryGetProperty(h, out var val))
                                return EscapeCsvCell(val.ToString(), ',');
                            return "";
                        });
                        sb.AppendLine(string.Join(",", values));
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// 文本编码转换
    /// </summary>
    public static byte[] ConvertEncoding(byte[] data, string fromEncoding, string toEncoding)
    {
        var srcEnc = Encoding.GetEncoding(fromEncoding);
        var dstEnc = Encoding.GetEncoding(toEncoding);
        var text = srcEnc.GetString(data);
        return dstEnc.GetBytes(text);
    }

    /// <summary>
    /// 获取常见编码列表
    /// </summary>
    public static List<(string Name, string DisplayName)> GetCommonEncodings()
    {
        return new List<(string, string)>
        {
            ("utf-8", "UTF-8"),
            ("utf-16", "UTF-16"),
            ("utf-32", "UTF-32"),
            ("ascii", "ASCII"),
            ("gb2312", "简体中文 (GB2312)"),
            ("gbk", "简体中文 (GBK)"),
            ("big5", "繁体中文 (Big5)"),
            ("shift_jis", "日语 (Shift-JIS)"),
            ("euc-jp", "日语 (EUC-JP)"),
            ("euc-kr", "韩语 (EUC-KR)"),
            ("iso-8859-1", "西欧 (ISO-8859-1)"),
            ("windows-1252", "西欧 (Windows-1252)")
        };
    }

    // 解析简单 YAML
    private static Dictionary<string, object> ParseSimpleYaml(string yaml)
    {
        var result = new Dictionary<string, object>();
        var lines = yaml.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx > 0)
            {
                var key = trimmed.Substring(0, colonIdx).Trim();
                var value = trimmed.Substring(colonIdx + 1).Trim();
                result[key] = value;
            }
        }

        return result;
    }

    // 转换为 YAML 格式
    private static string ConvertToYaml(object? obj, int indent)
    {
        if (obj == null) return "null";
        var prefix = new string(' ', indent * 2);

        if (obj is System.Text.Json.JsonElement el)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var sb = new StringBuilder();
                    foreach (var prop in el.EnumerateObject())
                    {
                        sb.AppendLine($"{prefix}{prop.Name}: {ConvertToYaml(prop.Value, indent + 1).TrimStart()}");
                    }
                    return sb.ToString();

                case System.Text.Json.JsonValueKind.Array:
                    var arrSb = new StringBuilder();
                    foreach (var item in el.EnumerateArray())
                    {
                        arrSb.AppendLine($"{prefix}- {ConvertToYaml(item, indent + 1).TrimStart()}");
                    }
                    return arrSb.ToString();

                case System.Text.Json.JsonValueKind.String:
                    return el.GetString() ?? "";

                case System.Text.Json.JsonValueKind.Number:
                    return el.GetRawText();

                case System.Text.Json.JsonValueKind.True:
                    return "true";

                case System.Text.Json.JsonValueKind.False:
                    return "false";

                case System.Text.Json.JsonValueKind.Null:
                    return "null";
            }
        }

        return obj.ToString() ?? "";
    }

    // 解析 CSV 行
    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    cells.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    // CSV 单元格转义
    private static string EscapeCsvCell(string cell, char delimiter)
    {
        if (cell.Contains(delimiter) || cell.Contains('"') || cell.Contains('\n'))
        {
            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }
        return cell;
    }
}
