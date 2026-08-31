using System.Text.RegularExpressions;

namespace NEManager.Core.Tools;

/// <summary>
/// 批量文件重命名服务 - 支持正则表达式和模板
/// </summary>
public static class BatchRenameService
{
    /// <summary>
    /// 重命名结果
    /// </summary>
    public class RenameResult
    {
        public string OriginalPath { get; set; } = "";
        public string NewPath { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public string NewName { get; set; } = "";
        public bool Success { get; set; }
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// 重命名规则
    /// </summary>
    public class RenameRule
    {
        public string Pattern { get; set; } = "";
        public string Replacement { get; set; } = "";
        public bool UseRegex { get; set; } = true;
        public bool CaseSensitive { get; set; } = false;
        public bool IncludeExtension { get; set; } = false;
    }

    /// <summary>
    /// 批量重命名文件
    /// </summary>
    public static List<RenameResult> BatchRename(IEnumerable<string> filePaths, RenameRule rule)
    {
        var results = new List<RenameResult>();
        
        foreach (var path in filePaths)
        {
            var result = new RenameResult
            {
                OriginalPath = path,
                OriginalName = Path.GetFileName(path)
            };

            try
            {
                var fileName = rule.IncludeExtension 
                    ? Path.GetFileName(path) 
                    : Path.GetFileNameWithoutExtension(path);
                
                var extension = Path.GetExtension(path);
                string newName;

                if (rule.UseRegex)
                {
                    var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    newName = Regex.Replace(fileName, rule.Pattern, rule.Replacement, options);
                }
                else
                {
                    var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    newName = fileName.Replace(rule.Pattern, rule.Replacement, comparison);
                }

                if (!rule.IncludeExtension)
                    newName += extension;

                var dir = Path.GetDirectoryName(path) ?? "";
                result.NewName = newName;
                result.NewPath = Path.Combine(dir, newName);

                // 检查是否已存在
                if (File.Exists(result.NewPath))
                {
                    result.Success = false;
                    result.Error = "目标文件已存在";
                }
                else
                {
                    File.Move(path, result.NewPath);
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 使用模板重命名
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    /// <param name="template">模板字符串，支持变量：{name}文件名，{ext}扩展名，{date}日期，{time}时间，{index}序号，{hash}哈希</param>
    /// <param name="startIndex">起始序号</param>
    public static List<RenameResult> BatchRenameWithTemplate(IEnumerable<string> filePaths, string template, int startIndex = 1)
    {
        var results = new List<RenameResult>();
        int index = startIndex;

        foreach (var path in filePaths)
        {
            var result = new RenameResult
            {
                OriginalPath = path,
                OriginalName = Path.GetFileName(path)
            };

            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);
                var now = DateTime.Now;

                // 计算文件哈希（可选，较慢）
                var hash = "";
                if (template.Contains("{hash}"))
                {
                    try
                    {
                        using var md5 = System.Security.Cryptography.MD5.Create();
                        using var stream = File.OpenRead(path);
                        var hashBytes = md5.ComputeHash(stream);
                        hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 8);
                    }
                    catch { hash = "00000000"; }
                }

                // 替换模板变量
                var newName = template
                    .Replace("{name}", name)
                    .Replace("{ext}", ext.TrimStart('.'))
                    .Replace("{date}", now.ToString("yyyyMMdd"))
                    .Replace("{time}", now.ToString("HHmmss"))
                    .Replace("{index}", index.ToString("D4"))
                    .Replace("{hash}", hash);

                if (!newName.Contains('.'))
                    newName += ext;

                var dir = Path.GetDirectoryName(path) ?? "";
                result.NewName = newName;
                result.NewPath = Path.Combine(dir, newName);

                if (File.Exists(result.NewPath))
                {
                    result.Success = false;
                    result.Error = "目标文件已存在";
                }
                else
                {
                    File.Move(path, result.NewPath);
                    result.Success = true;
                }

                index++;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 添加序号前缀/后缀
    /// </summary>
    public static List<RenameResult> AddNumbering(IEnumerable<string> filePaths, string prefix = "", string suffix = "", int startIndex = 1, string separator = "_")
    {
        var results = new List<RenameResult>();
        int index = startIndex;

        foreach (var path in filePaths)
        {
            var result = new RenameResult
            {
                OriginalPath = path,
                OriginalName = Path.GetFileName(path)
            };

            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);
                var newName = $"{prefix}{index}{separator}{name}{suffix}{ext}";

                var dir = Path.GetDirectoryName(path) ?? "";
                result.NewName = newName;
                result.NewPath = Path.Combine(dir, newName);

                if (File.Exists(result.NewPath))
                {
                    result.Success = false;
                    result.Error = "目标文件已存在";
                }
                else
                {
                    File.Move(path, result.NewPath);
                    result.Success = true;
                }

                index++;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 转换文件名大小写
    /// </summary>
    public static List<RenameResult> ChangeCase(IEnumerable<string> filePaths, string mode)
    {
        var results = new List<RenameResult>();

        foreach (var path in filePaths)
        {
            var result = new RenameResult
            {
                OriginalPath = path,
                OriginalName = Path.GetFileName(path)
            };

            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);
                
                var newName = mode switch
                {
                    "upper" => name.ToUpper() + ext.ToLower(),
                    "lower" => name.ToLower() + ext.ToLower(),
                    "title" => ToTitleCase(name) + ext.ToLower(),
                    "camel" => ToCamelCase(name) + ext,
                    _ => name + ext
                };

                var dir = Path.GetDirectoryName(path) ?? "";
                result.NewName = newName;
                result.NewPath = Path.Combine(dir, newName);

                if (File.Exists(result.NewPath))
                {
                    result.Success = false;
                    result.Error = "目标文件已存在";
                }
                else
                {
                    File.Move(path, result.NewPath);
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        return results;
    }

    private static string ToTitleCase(string text)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
    }

    private static string ToCamelCase(string text)
    {
        var words = Regex.Split(text, @"[\s_\-]+");
        if (words.Length == 0) return text;
        
        var result = words[0].ToLower();
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                result += char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
        }
        return result;
    }

    /// <summary>
    /// 预览重命名结果（不执行实际重命名）
    /// </summary>
    public static List<(string Original, string New)> PreviewRename(IEnumerable<string> filePaths, RenameRule rule)
    {
        var previews = new List<(string, string)>();

        foreach (var path in filePaths)
        {
            var fileName = rule.IncludeExtension 
                ? Path.GetFileName(path) 
                : Path.GetFileNameWithoutExtension(path);
            
            var extension = Path.GetExtension(path);
            string newName;

            if (rule.UseRegex)
            {
                var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                newName = Regex.Replace(fileName, rule.Pattern, rule.Replacement, options);
            }
            else
            {
                var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                newName = fileName.Replace(rule.Pattern, rule.Replacement, comparison);
            }

            if (!rule.IncludeExtension)
                newName += extension;

            previews.Add((Path.GetFileName(path), newName));
        }

        return previews;
    }
}
