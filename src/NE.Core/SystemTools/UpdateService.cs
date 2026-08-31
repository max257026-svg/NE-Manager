using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NEManager.Core.SystemTools;

/// <summary>
/// 轻量 GitHub Release 自动检查器 — 零第三方依赖，直接用 HttpClient + System.Text.Json。
/// </summary>
public static class UpdateService
{
    private const string RepoOwner = "max257026-svg";
    private const string RepoName = "NE-Manager";
    private const string ApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    public static string CurrentVersion { get; } =
        System.Diagnostics.FileVersionInfo.GetVersionInfo(
            System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion ?? "0.0.0.0";

    public static async Task<(bool hasUpdate, string latestTag, string downloadUrl, string body)> CheckAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NE-Manager-UpdateChecker/3.0");
            client.Timeout = TimeSpan.FromSeconds(8);

            var json = await client.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string body = root.GetProperty("body").GetString() ?? "";
            string download = "";
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        download = a.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            string cleanTag = tag.TrimStart('v');
            bool newer = Version.TryParse(cleanTag, out var latestV)
                         && Version.TryParse(CurrentVersion, out var curV)
                         && latestV > curV;

            return (newer, tag, download, body);
        }
        catch
        {
            return (false, "", "", "");
        }
    }

    /// <summary>
    /// 把 zip 下载到临时目录，返回文件路径。
    /// </summary>
    public static async Task<string> DownloadAsync(string downloadUrl, IProgress<(long bytes, long total)>? progress = null)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NE-Manager-UpdateChecker/3.0");
        client.Timeout = TimeSpan.FromMinutes(5);

        using var resp = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;

        string tmp = Path.Combine(Path.GetTempPath(), $"NE-Manager-update-{Guid.NewGuid():N}.zip");
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var file = File.Create(tmp);

        var buffer = new byte[65536];
        long done = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await file.WriteAsync(buffer, 0, read);
            done += read;
            progress?.Report((done, total));
        }
        return tmp;
    }
}

