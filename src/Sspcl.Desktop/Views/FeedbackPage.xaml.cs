using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Sspcl.Desktop.Services;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class FeedbackPage : UserControl
{
    private readonly AppSettings _settings;

    public FeedbackPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        UrlBox.Text = _settings.FeedbackUrl;
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlBox.Text.Trim();
        string text = FeedbackBox.Text.Trim();
        if (url.Length == 0)
        {
            MessageBox.Show("请先填写接收地址（开发者提供的反馈接口）。", "反馈", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (text.Length == 0)
        {
            MessageBox.Show("请填写反馈内容。", "反馈", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.FeedbackUrl = url;
        _settings.Save();

        var btn = (Button)sender;
        btn.IsEnabled = false;
        StatusText.Text = "发送中…";
        try
        {
            await PostAsync(url, text);
            StatusText.Text = "已发送";
            FeedbackBox.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = "发送失败：" + ex.Message;
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private static async System.Threading.Tasks.Task PostAsync(string url, string text)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string content = $"【sspcl 反馈】\n{text}";
        var body = new { content };
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode}");
    }
}
