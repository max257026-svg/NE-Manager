using System.Text.RegularExpressions;

namespace NEManager.Core.FileSystem;

public static class BatchRenameService
{
    public class RenamePreview
    {
        public string OriginalPath { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public string NewName { get; set; } = "";
        public bool HasConflict { get; set; }
    }

    // 正则替换重命名
    public static List<RenamePreview> PreviewRegexRename(
        IEnumerable<string> filePaths,
        string pattern,
        string replacement,
        bool caseSensitive = true)
    {
        var previews = new List<RenamePreview>();
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        
        try
        {
            var regex = new Regex(pattern, options);
            
            foreach (var path in filePaths)
            {
                var dir = Path.GetDirectoryName(path) ?? "";
                var name = Path.GetFileName(path);
                var ext = Path.GetExtension(path);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
                
                var newName = regex.Replace(nameWithoutExt, replacement) + ext;
                
                var newPath = Path.Combine(dir, newName);
                var hasConflict = File.Exists(newPath) && newPath != path;
                
                previews.Add(new RenamePreview
                {
                    OriginalPath = path,
                    OriginalName = name,
                    NewName = newName,
                    HasConflict = hasConflict
                });
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"正则表达式错误: {ex.Message}");
        }
        
        return previews;
    }

    // 模板重命名
    public static List<RenamePreview> PreviewTemplateRename(
        IEnumerable<string> filePaths,
        string template)
    {
        var previews = new List<RenamePreview>();
        int counter = 1;
        
        foreach (var path in filePaths)
        {
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(path);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
            var creationTime = File.GetCreationTime(path);
            var modifiedTime = File.GetLastWriteTime(path);
            
            var newName = template
                .Replace("{name}", nameWithoutExt)
                .Replace("{ext}", ext.TrimStart('.'))
                .Replace("{n}", counter.ToString())
                .Replace("{n2}", counter.ToString("D2"))
                .Replace("{n3}", counter.ToString("D3"))
                .Replace("{date}", creationTime.ToString("yyyy-MM-dd"))
                .Replace("{time}", creationTime.ToString("HH-mm-ss"))
                .Replace("{datetime}", creationTime.ToString("yyyy-MM-dd_HH-mm-ss"))
                .Replace("{modified}", modifiedTime.ToString("yyyy-MM-dd"))
                .Replace("{lower}", nameWithoutExt.ToLower())
                .Replace("{upper}", nameWithoutExt.ToUpper())
                .Replace("{title}", char.ToUpper(nameWithoutExt[0]) + nameWithoutExt.Substring(1).ToLower())
                + ext;
            
            var newPath = Path.Combine(dir, newName);
            var hasConflict = File.Exists(newPath) && newPath != path;
            
            previews.Add(new RenamePreview
            {
                OriginalPath = path,
                OriginalName = name,
                NewName = newName,
                HasConflict = hasConflict
            });
            
            counter++;
        }
        
        return previews;
    }

    // 执行重命名
    public static (int success, int failed) ExecuteRename(List<RenamePreview> previews)
    {
        int success = 0;
        int failed = 0;
        
        foreach (var preview in previews)
        {
            if (preview.HasConflict)
            {
                failed++;
                continue;
            }
            
            try
            {
                var dir = Path.GetDirectoryName(preview.OriginalPath) ?? "";
                var newPath = Path.Combine(dir, preview.NewName);
                
                if (File.Exists(preview.OriginalPath))
                    File.Move(preview.OriginalPath, newPath);
                else if (Directory.Exists(preview.OriginalPath))
                    Directory.Move(preview.OriginalPath, newPath);
                
                success++;
            }
            catch
            {
                failed++;
            }
        }
        
        return (success, failed);
    }
}
