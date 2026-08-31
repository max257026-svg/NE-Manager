using System.Diagnostics;

namespace NEManager.Core.Script;

public class ScriptResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
}

public static class ScriptEngine
{
    public static ScriptResult RunPython(string script, string? workingDir = null)
    {
        return RunScript("python", "-c", script, workingDir);
    }

    public static ScriptResult RunLua(string script, string? workingDir = null)
    {
        return RunScript("lua", "-e", script, workingDir);
    }

    public static ScriptResult RunPowerShell(string script, string? workingDir = null)
    {
        return RunScript("powershell", "-Command", script, workingDir);
    }

    public static ScriptResult RunBatch(string script, string? workingDir = null)
    {
        return RunScript("cmd", "/c", script, workingDir);
    }

    private static ScriptResult RunScript(string executable, string argPrefix, string script, string? workingDir)
    {
        var result = new ScriptResult();
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"{argPrefix} \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            if (!string.IsNullOrEmpty(workingDir))
                psi.WorkingDirectory = workingDir;
            
            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Success = false;
                result.Error = $"无法启动进程: {executable}";
                return result;
            }
            
            result.Output = process.StandardOutput.ReadToEnd();
            result.Error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }

    public static ScriptResult RunScriptFile(string filePath, string? workingDir = null)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        var executable = ext switch
        {
            ".py" => "python",
            ".lua" => "lua",
            ".ps1" => "powershell",
            ".bat" or ".cmd" => "cmd",
            _ => throw new NotSupportedException($"不支持的脚本类型: {ext}")
        };
        
        var argPrefix = ext switch
        {
            ".py" => "",
            ".lua" => "",
            ".ps1" => "-File",
            ".bat" or ".cmd" => "/c",
            _ => ""
        };
        
        return RunScript(executable, argPrefix, filePath, workingDir);
    }
}
