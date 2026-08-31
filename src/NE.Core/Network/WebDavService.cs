using System.Net.Http;
using System.Net;
using System.Text;

namespace NEManager.Core.Network;

public static class WebDavService
{
    private static readonly HttpClient _client = new();

    public static async Task<List<string>> ListDirectory(string url, string? username = null, string? password = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Depth", "1");
        
        if (!string.IsNullOrEmpty(username))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        // 简单的 XML 解析，提取 href
        var items = new List<string>();
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("<D:href>") || line.Contains("<d:href>"))
            {
                var start = line.IndexOf("href>") + 5;
                var end = line.IndexOf("</", start);
                if (end > start)
                {
                    var href = line.Substring(start, end - start);
                    items.Add(href);
                }
            }
        }
        return items;
    }

    public static async Task DownloadFile(string url, string localPath, string? username = null, string? password = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        if (!string.IsNullOrEmpty(username))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        using var fileStream = File.Create(localPath);
        await response.Content.CopyToAsync(fileStream);
    }

    public static async Task UploadFile(string url, string localPath, string? username = null, string? password = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        
        if (!string.IsNullOrEmpty(username))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        using var fileStream = File.OpenRead(localPath);
        request.Content = new StreamContent(fileStream);
        
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
