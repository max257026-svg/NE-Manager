#pragma warning disable SYSLIB0014 // FTP WebRequest 在 .NET 中没有 HttpClient 替代方案

using System.Net;
using System.IO;

namespace NEManager.Core.Network;

public static class FtpService
{
    public static List<string> ListDirectory(string ftpUrl, string username, string password)
    {
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.ListDirectory;
        request.Credentials = new NetworkCredential(username, password);
        
        using var response = (FtpWebResponse)request.GetResponse();
        using var stream = response.GetResponseStream();
        using var reader = new StreamReader(stream);
        
        var content = reader.ReadToEnd();
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static void DownloadFile(string ftpUrl, string localPath, string username, string password)
    {
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.DownloadFile;
        request.Credentials = new NetworkCredential(username, password);
        
        using var response = (FtpWebResponse)request.GetResponse();
        using var responseStream = response.GetResponseStream();
        using var fileStream = File.Create(localPath);
        
        responseStream.CopyTo(fileStream);
    }

    public static void UploadFile(string ftpUrl, string localPath, string username, string password)
    {
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.UploadFile;
        request.Credentials = new NetworkCredential(username, password);
        
        using var fileStream = File.OpenRead(localPath);
        using var requestStream = request.GetRequestStream();
        
        fileStream.CopyTo(requestStream);
    }

    public static void DeleteFile(string ftpUrl, string username, string password)
    {
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.DeleteFile;
        request.Credentials = new NetworkCredential(username, password);
        
        using var response = (FtpWebResponse)request.GetResponse();
    }

    public static void CreateDirectory(string ftpUrl, string username, string password)
    {
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.MakeDirectory;
        request.Credentials = new NetworkCredential(username, password);
        
        using var response = (FtpWebResponse)request.GetResponse();
    }
}
