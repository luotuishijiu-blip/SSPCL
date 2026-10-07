using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using Sspcl.Desktop.Services;
using Sspcl.Core.Data;
using Sspcl.Core.Install;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class AIFitDialog : Window
{
    private readonly AppSettings _settings;
    private readonly Installation _install;
    private List<string> _ships = new();
    private List<string> _weapons = new();

    public AIFitDialog(AppSettings settings, Installation install)
    {
        _settings = settings;
        _install = install;
        InitializeComponent();
        EndpointBox.Text = _settings.AiEndpoint;
        KeyBox.Text = _settings.AiKey;
        ModelBox.Text = _settings.AiModel;
        Rescan();
    }

    private void Rescan()
    {
        try
        {
            (_ships, _weapons) = ShipWeaponIndex.Scan(_install.Path);
            StatusText.Text = $"已读取 {_ships.Count} 艘舰船 · {_weapons.Count} 个武器";
        }
        catch (Exception ex)
        {
            StatusText.Text = "读取失败：" + ex.Message;
        }
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => Rescan();

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _settings.AiEndpoint = EndpointBox.Text.Trim();
        _settings.AiKey = KeyBox.Text.Trim();
        _settings.AiModel = ModelBox.Text.Trim();
        _settings.Save();
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        string input = InputBox.Text.Trim();
        if (input.Length == 0)
        {
            MessageBox.Show("请先输入配装需求。", "AI 配装", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.AiKey))
        {
            MessageBox.Show("请先配置 API Key。", "AI 配装", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GenerateBtn.IsEnabled = false;
        OutputBox.Text = "生成中…";
        try
        {
            string ships = string.Join(", ", _ships.Take(200));
            string weapons = string.Join(", ", _weapons.Take(300));
            string prompt =
                $"玩家配装需求：{input}\n\n" +
                $"可用舰船（部分）：{ships}\n\n" +
                $"可用武器（部分）：{weapons}\n\n" +
                "请给出具体的舰船配装建议（武器、船插、定位），用中文回答。";

            OutputBox.Text = await CallAiAsync(_settings.AiEndpoint, _settings.AiKey, _settings.AiModel, prompt);
        }
        catch (Exception ex)
        {
            OutputBox.Text = "生成失败：" + ex.Message;
        }
        finally
        {
            GenerateBtn.IsEnabled = true;
        }
    }

    private static async System.Threading.Tasks.Task<string> CallAiAsync(string endpoint, string key, string model, string prompt)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "你是远行星号（Starsector）配装助手。" },
                new { role = "user", content = prompt },
            },
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var resp = await http.SendAsync(req);
        string text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode}: {(text.Length > 300 ? text[..300] : text)}");

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "(空回复)";
    }
}
