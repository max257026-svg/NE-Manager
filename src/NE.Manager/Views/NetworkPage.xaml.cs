using System.Windows;
using System.Windows.Controls;
using NEManager.Core.Network;

namespace NEManager.App.Views;

public partial class NetworkPage : UserControl, IRefreshable
{
    private string _currentProtocol = "FTP";
    private string _baseUrl = "";
    private string _username = "";
    private string _password = "";

    public NetworkPage()
    {
        InitializeComponent();
    }

    public void OnEnter() { }
    public void OnLeave() { }

    private void Protocol_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProtocolSelector.SelectedItem is ComboBoxItem item)
        {
            _currentProtocol = item.Content.ToString() ?? "FTP";
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerInput.Text.Trim();
        var username = UsernameInput.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrEmpty(server))
        {
            MessageBox.Show("请输入服务器地址。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _username = username;
        _password = password;
        _baseUrl = _currentProtocol.ToLower() == "ftp" 
            ? $"ftp://{server}" 
            : $"http://{server}";

        try
        {
            StatusText.Text = "正在连接...";
            
            List<string> items;
            if (_currentProtocol == "FTP")
            {
                items = await Task.Run(() => FtpService.ListDirectory(_baseUrl, _username, _password));
            }
            else
            {
                items = await WebDavService.ListDirectory(_baseUrl, _username, _password);
            }

            var displayItems = items.Select(item => new NetworkFileItem
            {
                Name = item,
                Type = item.EndsWith("/") ? "目录" : "文件",
                Size = "",
                ModifiedTime = ""
            }).ToList();

            FileGrid.ItemsSource = displayItems;
            StatusText.Text = $"已连接，共 {items.Count} 项";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "连接失败";
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not NetworkFileItem item)
        {
            MessageBox.Show("请选择要下载的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = item.Name,
            Title = "保存文件"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "正在下载...";
                
                var remoteUrl = _baseUrl.TrimEnd('/') + "/" + item.Name;
                if (_currentProtocol == "FTP")
                {
                    await Task.Run(() => FtpService.DownloadFile(remoteUrl, dlg.FileName, _username, _password));
                }
                else
                {
                    await WebDavService.DownloadFile(remoteUrl, dlg.FileName, _username, _password);
                }

                StatusText.Text = $"已下载: {item.Name}";
                MessageBox.Show("下载完成。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "下载失败";
            }
        }
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择要上传的文件" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "正在上传...";
                
                var fileName = Path.GetFileName(dlg.FileName);
                var remoteUrl = _baseUrl.TrimEnd('/') + "/" + fileName;
                
                if (_currentProtocol == "FTP")
                {
                    await Task.Run(() => FtpService.UploadFile(remoteUrl, dlg.FileName, _username, _password));
                }
                else
                {
                    await WebDavService.UploadFile(remoteUrl, dlg.FileName, _username, _password);
                }

                StatusText.Text = $"已上传: {fileName}";
                MessageBox.Show("上传完成。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                await Connect_Async(); // 刷新列表（使用 await 而非直接调用 async void）
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上传失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "上传失败";
            }
        }
    }

    /// <summary>
    /// 为非 async 事件处理程序提供 awaitable 刷新入口，避免死锁。
    /// </summary>
    private async Task Connect_Async()
    {
        var server = ServerInput.Text.Trim();
        var username = UsernameInput.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrEmpty(server)) return;

        _username = username;
        _password = password;
        _baseUrl = _currentProtocol.ToLower() == "ftp"
            ? $"ftp://{server}"
            : $"http://{server}";

        try
        {
            StatusText.Text = "正在连接...";

            List<string> items;
            if (_currentProtocol == "FTP")
            {
                items = await Task.Run(() => FtpService.ListDirectory(_baseUrl, _username, _password));
            }
            else
            {
                items = await WebDavService.ListDirectory(_baseUrl, _username, _password);
            }

            var displayItems = items.Select(item => new NetworkFileItem
            {
                Name = item,
                Type = item.EndsWith("/") ? "目录" : "文件",
                Size = "",
                ModifiedTime = ""
            }).ToList();

            FileGrid.ItemsSource = displayItems;
            StatusText.Text = $"已连接，共 {items.Count} 项";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "连接失败";
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not NetworkFileItem item)
        {
            MessageBox.Show("请选择要删除的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"确定要删除 {item.Name} 吗？", "确认删除", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var remoteUrl = _baseUrl.TrimEnd('/') + "/" + item.Name;
                if (_currentProtocol == "FTP")
                {
                    await Task.Run(() => FtpService.DeleteFile(remoteUrl, _username, _password));
                }
                else
                {
                    MessageBox.Show("WebDAV 删除功能暂未实现。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StatusText.Text = $"已删除: {item.Name}";
                await Connect_Async(); // 刷新列表
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void CreateDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PromptDialog("新建目录", "请输入目录名称:", "");
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var dirName = dlg.InputText.Trim();
                var remoteUrl = _baseUrl.TrimEnd('/') + "/" + dirName + "/";
                
                if (_currentProtocol == "FTP")
                {
                    await Task.Run(() => FtpService.CreateDirectory(remoteUrl, _username, _password));
                }
                else
                {
                    MessageBox.Show("WebDAV 创建目录功能暂未实现。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StatusText.Text = $"已创建目录: {dirName}";
                await Connect_Async(); // 刷新列表
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await Connect_Async();
    }
}

public class NetworkFileItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Size { get; set; } = "";
    public string ModifiedTime { get; set; } = "";
}
