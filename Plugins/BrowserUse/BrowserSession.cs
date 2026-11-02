using System.Text.Json;
using Microsoft.Playwright;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.BrowserUse;

/// <summary>
/// 本机 Playwright Chromium 会话。单实例串行；卸载时关闭。
/// 下载只落盘，不启动文件。CAPTCHA/SSO 直接返回人工接管。
/// </summary>
public sealed class BrowserSession
{
    private static readonly string[] CaptchaSelectors =
    [
        "iframe[src*='recaptcha']",
        "iframe[src*='hcaptcha']",
        "iframe[src*='challenges.cloudflare.com']",
        "iframe[src*='captcha']",
        ".g-recaptcha",
        ".h-captcha",
        "#captcha",
        "[data-sitekey]"
    ];

    private readonly string _dataDir;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _headless;

    public BrowserSession(string dataDir) => _dataDir = dataDir;

    public Task<string> OpenAsync(string parameters) => Gate(() => OpenCoreAsync(parameters));
    public Task<string> NavigateAsync(string parameters) => Gate(() => NavigateToolAsync(parameters));
    public Task<string> SnapshotAsync(string parameters) => Gate(() => SnapshotCoreAsync(parameters));
    public Task<string> ClickAsync(string parameters) => Gate(() => ClickCoreAsync(parameters));
    public Task<string> FillAsync(string parameters) => Gate(() => FillCoreAsync(parameters));
    public Task<string> ExtractAsync(string parameters) => Gate(() => ExtractCoreAsync(parameters));
    public Task<string> ScreenshotAsync(string parameters) => Gate(() => ScreenshotCoreAsync(parameters));
    public Task<string> DownloadAsync(string parameters) => Gate(() => DownloadCoreAsync(parameters));

    public void Shutdown()
    {
        _gate.Wait();
        try
        {
            CloseCoreAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[BrowserUse] 关闭浏览器失败: {0}", ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> Gate(Func<Task<string>> action)
    {
        await _gate.WaitAsync();
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[BrowserUse] 工具执行失败: {0}", ex.Message);
            return BrowserResults.Fail("browser_error", ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> OpenCoreAsync(string parameters)
    {
        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var url = doc == null ? null : GetString(doc.RootElement, "url");
        var cfg = BrowserUseConfig.Load(_dataDir);

        if (_browser == null || !_browser.IsConnected || _page == null || _page.IsClosed)
        {
            await CloseCoreAsync();
            _playwright = await Playwright.CreateAsync();
            _headless = cfg.Headless;
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _headless
            });
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true
            });
            _page = await _context.NewPageAsync();
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return BrowserResults.Ok(new
            {
                success = true,
                opened = true,
                headless = _headless,
                url = _page!.Url
            });
        }

        return await NavigateCoreAsync(url, cfg);
    }

    private async Task<string> NavigateToolAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var url = doc == null ? null : GetString(doc.RootElement, "url");
        if (string.IsNullOrWhiteSpace(url))
            return BrowserResults.Fail("invalid_args", "缺少 url");
        return await NavigateCoreAsync(url, BrowserUseConfig.Load(_dataDir));
    }

    private async Task<string> NavigateCoreAsync(string url, BrowserUseConfig cfg)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return BrowserResults.Fail("invalid_url", "url 必须是绝对 http(s) 地址", url);
        if (!BrowserGuard.IsHostAllowed(uri, cfg.AllowedDomains))
            return BrowserResults.DomainDenied(url);

        var page = _page!;
        var before = page.Url;
        await page.GotoAsync(url, new PageGotoOptions
        {
            Timeout = cfg.NavigationTimeoutMs,
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        if (!BrowserGuard.IsUrlAllowed(page.Url, cfg.AllowedDomains))
        {
            var blocked = page.Url;
            await SafeGotoAsync(page, BackTarget(before, cfg), cfg);
            return BrowserResults.DomainDenied(blocked);
        }

        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null)
            return BrowserResults.Takeover(takeover, page.Url);

        return BrowserResults.Ok(new { success = true, url = page.Url, title = await page.TitleAsync() });
    }

    private async Task<string> SnapshotCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);

        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        var target = string.IsNullOrWhiteSpace(selector) ? page.Locator("body") : page.Locator(selector);
        string text;
        try
        {
            text = await target.AriaSnapshotAsync();
        }
        catch (Exception)
        {
            text = await target.InnerTextAsync();
        }

        return BrowserResults.Ok(new
        {
            success = true,
            url = page.Url,
            title = await page.TitleAsync(),
            snapshot = Truncate(text, 20_000)
        });
    }

    private async Task<string> ClickCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var cfg = BrowserUseConfig.Load(_dataDir);
        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);

        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        if (string.IsNullOrWhiteSpace(selector))
            return BrowserResults.Fail("invalid_args", "缺少 selector");
        if (selector.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            return BrowserResults.Takeover("captcha", page.Url);

        var before = page.Url;
        var popups = new List<IPage>();
        void OnPopup(object? _, IPage popup) => popups.Add(popup);
        page.Popup += OnPopup;
        try
        {
            var options = new LocatorClickOptions { Timeout = cfg.NavigationTimeoutMs };
            var button = doc == null ? null : GetString(doc.RootElement, "button");
            if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase)) options.Button = MouseButton.Right;
            else if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase)) options.Button = MouseButton.Middle;
            await page.Locator(selector).ClickAsync(options);
        }
        finally
        {
            page.Popup -= OnPopup;
        }

        var jump = await RejectDisallowedJumpAsync(page, before, cfg);
        if (jump != null) return jump;

        foreach (var popup in popups)
        {
            var popUrl = popup.Url;
            try
            {
                await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 5000 });
                popUrl = popup.Url;
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[BrowserUse] 弹窗未完成加载: {0}", ex.Message);
            }

            if (!BrowserGuard.IsUrlAllowed(popUrl, cfg.AllowedDomains))
            {
                try { await popup.CloseAsync(); } catch (Exception ex) { XTrace.Log.Debug("[BrowserUse] 关闭弹窗失败: {0}", ex.Message); }
                return BrowserResults.DomainDenied(popUrl);
            }

            _page = popup;
            page = popup;
        }

        takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);
        return BrowserResults.Ok(new { success = true, url = page.Url, title = await page.TitleAsync() });
    }

    private async Task<string> FillCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var cfg = BrowserUseConfig.Load(_dataDir);
        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);

        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        if (string.IsNullOrWhiteSpace(selector))
            return BrowserResults.Fail("invalid_args", "缺少 selector");
        if (selector.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            return BrowserResults.Takeover("captcha", page.Url);
        if (!TryGetString(doc!.RootElement, "value", out var value))
            return BrowserResults.Fail("invalid_args", "缺少 value");

        var before = page.Url;
        await page.Locator(selector).FillAsync(value ?? "", new LocatorFillOptions { Timeout = cfg.NavigationTimeoutMs });
        var jump = await RejectDisallowedJumpAsync(page, before, cfg);
        if (jump != null) return jump;
        takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);
        return BrowserResults.Ok(new { success = true, url = page.Url });
    }

    private async Task<string> ExtractCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);

        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        if (string.IsNullOrWhiteSpace(selector))
            return BrowserResults.Fail("invalid_args", "缺少 selector");
        var attribute = GetString(doc!.RootElement, "attribute");
        var locator = page.Locator(selector);
        var value = string.IsNullOrEmpty(attribute)
            ? await locator.InnerTextAsync()
            : await locator.GetAttributeAsync(attribute) ?? "";
        return BrowserResults.Ok(new
        {
            success = true,
            url = page.Url,
            text = Truncate(value, 20_000)
        });
    }

    private async Task<string> ScreenshotCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);

        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        var fullPage = doc != null && GetBool(doc.RootElement, "fullPage");
        var dir = Path.Combine(_dataDir, "screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"shot-{DateTime.Now:yyyyMMddHHmmssfff}.png");
        if (string.IsNullOrWhiteSpace(selector))
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = fullPage });
        }
        else
        {
            await page.Locator(selector).ScreenshotAsync(new LocatorScreenshotOptions { Path = path });
        }

        return BrowserResults.Ok(new { success = true, path, url = page.Url });
    }

    private async Task<string> DownloadCoreAsync(string parameters)
    {
        var closed = RequirePage();
        if (closed != null) return closed;
        var page = _page!;
        var cfg = BrowserUseConfig.Load(_dataDir);
        using var doc = Parse(parameters, out var error);
        if (error != null) return error;
        var url = doc == null ? null : GetString(doc.RootElement, "url");
        var selector = doc == null ? null : GetString(doc.RootElement, "selector");
        var filename = doc == null ? null : GetString(doc.RootElement, "filename");
        var dir = Path.Combine(_dataDir, "downloads");
        Directory.CreateDirectory(dir);

        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return BrowserResults.Fail("invalid_url", "url 必须是绝对 http(s) 地址", url);
            if (!BrowserGuard.IsHostAllowed(uri, cfg.AllowedDomains))
                return BrowserResults.DomainDenied(url);
            if (BrowserGuard.IsSso(url))
                return BrowserResults.Takeover("sso", url);

            var response = await _context!.APIRequest.GetAsync(url, new APIRequestContextOptions
            {
                Timeout = cfg.NavigationTimeoutMs
            });
            if (!response.Ok)
                return BrowserResults.Fail("download_failed", $"HTTP {response.Status}", url);
            var bytes = await response.BodyAsync();
            var path = UniquePath(dir, SafeFileName(filename, GuessName(url)));
            await File.WriteAllBytesAsync(path, bytes);
            return BrowserResults.Ok(new { success = true, path, bytes = bytes.Length, executed = false, url });
        }

        if (string.IsNullOrWhiteSpace(selector))
            return BrowserResults.Fail("invalid_args", "需要 url 或 selector");

        var takeover = await DetectTakeoverAsync(page);
        if (takeover != null) return BrowserResults.Takeover(takeover, page.Url);
        if (selector.Contains("captcha", StringComparison.OrdinalIgnoreCase))
            return BrowserResults.Takeover("captcha", page.Url);

        var before = page.Url;
        var download = await page.RunAndWaitForDownloadAsync(async () =>
        {
            await page.Locator(selector).ClickAsync(new LocatorClickOptions { Timeout = cfg.NavigationTimeoutMs });
        }, new PageRunAndWaitForDownloadOptions { Timeout = cfg.NavigationTimeoutMs });

        var jump = await RejectDisallowedJumpAsync(page, before, cfg);
        if (jump != null)
        {
            try { await download.CancelAsync(); } catch (Exception ex) { XTrace.Log.Debug("[BrowserUse] 取消下载失败: {0}", ex.Message); }
            return jump;
        }

        if (!BrowserGuard.IsUrlAllowed(download.Url, cfg.AllowedDomains))
        {
            try { await download.CancelAsync(); } catch (Exception ex) { XTrace.Log.Debug("[BrowserUse] 取消下载失败: {0}", ex.Message); }
            return BrowserResults.DomainDenied(download.Url);
        }

        var saved = UniquePath(dir, SafeFileName(filename, download.SuggestedFilename));
        await download.SaveAsAsync(saved);
        return BrowserResults.Ok(new { success = true, path = saved, executed = false, url = download.Url });
    }

    private async Task<string?> RejectDisallowedJumpAsync(IPage page, string before, BrowserUseConfig cfg)
    {
        if (BrowserGuard.IsUrlAllowed(page.Url, cfg.AllowedDomains)) return null;
        var blocked = page.Url;
        await SafeGotoAsync(page, BackTarget(before, cfg), cfg);
        return BrowserResults.DomainDenied(blocked);
    }

    private static string BackTarget(string before, BrowserUseConfig cfg)
        => BrowserGuard.IsUrlAllowed(before, cfg.AllowedDomains) && !string.IsNullOrWhiteSpace(before)
            ? before
            : "about:blank";

    private async Task SafeGotoAsync(IPage page, string url, BrowserUseConfig cfg)
    {
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = cfg.NavigationTimeoutMs,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[BrowserUse] 回退页面失败: {0}", ex.Message);
        }
    }

    private async Task<string?> DetectTakeoverAsync(IPage page)
    {
        if (BrowserGuard.IsSso(page.Url)) return "sso";
        try
        {
            var title = await page.TitleAsync();
            if (title.Contains("验证码", StringComparison.Ordinal)
                || title.Contains("captcha", StringComparison.OrdinalIgnoreCase))
                return "captcha";
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[BrowserUse] 读取标题失败: {0}", ex.Message);
        }

        foreach (var selector in CaptchaSelectors)
        {
            try
            {
                if (await page.Locator(selector).CountAsync() > 0) return "captcha";
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[BrowserUse] CAPTCHA 探测跳过 {0}: {1}", selector, ex.Message);
            }
        }

        return null;
    }

    private string? RequirePage()
    {
        if (_page == null || _page.IsClosed)
            return BrowserResults.Fail("browser_not_open", "浏览器未打开，请先调用 browser_open");
        return null;
    }

    private async Task CloseCoreAsync()
    {
        try { if (_context != null) await _context.CloseAsync(); } catch (Exception ex) { XTrace.Log.Debug("[BrowserUse] 关闭上下文: {0}", ex.Message); }
        try { if (_browser != null) await _browser.CloseAsync(); } catch (Exception ex) { XTrace.Log.Debug("[BrowserUse] 关闭浏览器: {0}", ex.Message); }
        _page = null;
        _context = null;
        _browser = null;
        _playwright?.Dispose();
        _playwright = null;
    }

    private static JsonDocument? Parse(string? parameters, out string? error)
    {
        error = null;
        var json = string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                doc.Dispose();
                error = BrowserResults.Fail("invalid_json", "参数必须是 JSON 对象");
                return null;
            }
            return doc;
        }
        catch (JsonException ex)
        {
            error = BrowserResults.Fail("invalid_json", ex.Message);
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => TryGetString(root, name, out var value) ? value : null;

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                value = prop.Value.GetString();
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool GetBool(JsonElement root, string name)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Value.ValueKind == JsonValueKind.True) return true;
            if (prop.Value.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
        return text[..max] + "…(truncated)";
    }

    private static string GuessName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return "download.bin";
    }

    private static string SafeFileName(string? name, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(name) ? fallback : name;
        raw = Path.GetFileName(raw);
        foreach (var c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c, '_');
        return string.IsNullOrWhiteSpace(raw) ? "download.bin" : raw;
    }

    private static string UniquePath(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return Path.Combine(dir, $"{stem}-{DateTime.Now:yyyyMMddHHmmssfff}{ext}");
    }
}
