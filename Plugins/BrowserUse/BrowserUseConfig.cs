using System.Text.Encodings.Web;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.BrowserUse;

/// <summary>插件数据目录 config.json。缺文件时按默认白名单（仅本机）生成。</summary>
public sealed class BrowserUseConfig
{
    public string Comment { get; set; } =
        "本机个人浏览器，不做团队共享。allowedDomains 为允许访问的域名（自动包含其子域）。未列出的 http(s) 导航与点击/填写引起的跳转一律拒绝，并在工具结果标记 needsApproval。环境变量 FORGESELF_BROWSER_ALLOWED_DOMAINS 可追加域名（逗号或分号分隔）。";

    public string[] AllowedDomains { get; set; } = ["localhost", "127.0.0.1", "::1"];

    /// <summary>默认有界面，便于 CAPTCHA/SSO 人工接管。不提供绕过开关。</summary>
    public bool Headless { get; set; }

    public int NavigationTimeoutMs { get; set; } = 30000;

    public static BrowserUseConfig CreateDefault() => new();

    public static BrowserUseConfig Load(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, "config.json");
        BrowserUseConfig cfg;
        try
        {
            if (!File.Exists(path))
            {
                cfg = CreateDefault();
                var json = JsonSerializer.Serialize(cfg, WriteOptions);
                File.WriteAllText(path, json);
            }
            else
            {
                cfg = JsonSerializer.Deserialize<BrowserUseConfig>(File.ReadAllText(path), ReadOptions) ?? CreateDefault();
            }
        }
        catch
        {
            cfg = CreateDefault();
        }

        if (cfg.AllowedDomains == null || cfg.AllowedDomains.Length == 0)
            cfg.AllowedDomains = [];
        if (cfg.NavigationTimeoutMs is < 1000 or > 120_000)
            cfg.NavigationTimeoutMs = 30000;

        var env = Environment.GetEnvironmentVariable("FORGESELF_BROWSER_ALLOWED_DOMAINS");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var extra = env.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            cfg.AllowedDomains = cfg.AllowedDomains.Concat(extra).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        return cfg;
    }

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
