using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NewLife.Log;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 更新检查器。封装 NewLife.Stardust StarAgent 客户端的版本查询和更新包下载能力，
/// 通过 HTTP 调用 StarServer API 实现版本检查和更新包下载。
/// 读取 <see cref="UpdateConfig"/> 获取服务器地址、通道、超时设置。
/// 超时保护：超时视为无更新，不阻塞启动。
/// </summary>
public class UpdateChecker
{
    private readonly UpdateConfig _config;
    private readonly HttpClient _httpClient;
    private readonly string _appName;
    private readonly string _currentVersion;
    private readonly string _os;
    private readonly string _runtime;

    /// <summary>
    /// 初始化更新检查器。
    /// </summary>
    /// <param name="config">更新配置，包含服务器地址、通道、超时设置</param>
    /// <param name="httpClient">HTTP 客户端，用于调用 StarServer API</param>
    /// <param name="appName">应用名称，如 "ForgeSelf"</param>
    /// <param name="currentVersion">当前版本号，如 "1.0.0.0"</param>
    /// <param name="os">操作系统标识，如 "win-x64"；默认从运行时推断</param>
    /// <param name="runtime">运行时版本，如 "net10.0"；默认从运行时推断</param>
    /// <exception cref="ArgumentNullException">config 或 httpClient 为 null</exception>
    public UpdateChecker(
        UpdateConfig config,
        HttpClient httpClient,
        string appName = "ForgeSelf",
        string currentVersion = "1.0.0.0",
        string? os = null,
        string? runtime = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _appName = appName;
        _currentVersion = currentVersion;

        // 从运行时推断操作系统和运行时版本
        _os = os ?? InferOs();
        _runtime = runtime ?? InferRuntime();
    }

    /// <summary>
    /// 检查版本更新。调用 StarServer API 查询最新版本。
    /// 超时保护：超时视为无更新，不阻塞启动。
    /// </summary>
    /// <returns>版本检查结果</returns>
    public virtual async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = TryParseVersion(_currentVersion),
            CheckTime = DateTime.UtcNow,
        };

        try
        {
            if (string.IsNullOrEmpty(_config.ServerUrl))
            {
                XTrace.Log.Info("UpdateChecker: ServerUrl 未配置，跳过版本检查。");
                result.IsSuccess = false;
                result.ErrorMessage = "ServerUrl 未配置";
                return result;
            }

            // 构建查询参数
            var baseUrl = _config.ServerUrl.TrimEnd('/');
            var queryParams = new Dictionary<string, string>
            {
                ["appName"] = _appName,
                ["currentVersion"] = _currentVersion,
                ["channel"] = _config.Channel,
                ["os"] = _os,
                ["runtime"] = _runtime,
            };

            var queryString = string.Join("&",
                queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            var requestUrl = $"{baseUrl}/api/version/check?{queryString}";

            XTrace.Log.Info("UpdateChecker: 检查更新: {0}", requestUrl);

            // 使用 CancellationTokenSource 实现超时保护
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(_config.CheckTimeoutSeconds, 1)));

            var response = await _httpClient.GetAsync(requestUrl, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            ParseCheckResponse(json, result);

            // 比较版本号确定是否有更新
            if (result.LatestVersion != null && result.CurrentVersion != null)
            {
                result.HasUpdate = result.LatestVersion > result.CurrentVersion;
            }

            result.IsSuccess = true;
            XTrace.Log.Info("UpdateChecker: 检查完成, HasUpdate={0}, LatestVersion={1}",
                result.HasUpdate, result.LatestVersion);
        }
        catch (OperationCanceledException)
        {
            // 超时视为无更新，不阻塞启动
            XTrace.Log.Warn("UpdateChecker: 版本检查超时 ({0}s), 视为无更新。",
                _config.CheckTimeoutSeconds);
            result.IsSuccess = false;
            result.ErrorMessage = $"版本检查超时 ({_config.CheckTimeoutSeconds}s)";
        }
        catch (HttpRequestException ex)
        {
            XTrace.Log.Error("UpdateChecker: 版本检查网络错误: {0}", ex.Message);
            result.IsSuccess = false;
            result.ErrorMessage = $"网络错误: {ex.Message}";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("UpdateChecker: 版本检查异常: {0}", ex.Message);
            result.IsSuccess = false;
            result.ErrorMessage = $"异常: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 下载更新包到指定路径。
    /// </summary>
    /// <param name="version">要下载的版本号</param>
    /// <param name="destPath">目标文件路径（含文件名）</param>
    /// <exception cref="ArgumentNullException">version 或 destPath 为 null 或空</exception>
    /// <exception cref="InvalidOperationException">ServerUrl 未配置</exception>
    /// <exception cref="HttpRequestException">下载失败或超时</exception>
    public virtual async Task DownloadPackageAsync(string version, string destPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(destPath);

        if (string.IsNullOrEmpty(_config.ServerUrl))
            throw new InvalidOperationException("ServerUrl 未配置，无法下载更新包。");

        var baseUrl = _config.ServerUrl.TrimEnd('/');
        var downloadUrl = $"{baseUrl}/api/version/download?appName={Uri.EscapeDataString(_appName)}&version={Uri.EscapeDataString(version)}";

        XTrace.Log.Info("UpdateChecker: 下载更新包: {0} → {1}", downloadUrl, destPath);

        // 确保目标目录存在
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 使用 CancellationTokenSource 实现下载超时
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.DownloadTimeoutSeconds, 30)));

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream, cts.Token);

            XTrace.Log.Info("UpdateChecker: 下载完成: {0} ({1} bytes)", destPath,
                new FileInfo(destPath).Length);
        }
        catch (OperationCanceledException)
        {
            XTrace.Log.Error("UpdateChecker: 下载超时 ({0}s)", _config.DownloadTimeoutSeconds);
            throw new HttpRequestException(
                $"下载更新包超时 ({_config.DownloadTimeoutSeconds}s): {downloadUrl}");
        }
    }

    /// <summary>
    /// 校验更新包 SHA256 哈希。
    /// </summary>
    /// <param name="packagePath">更新包文件路径</param>
    /// <param name="expectedHash">期望的哈希值，格式为 "sha256:&lt;hex&gt;"</param>
    /// <returns>哈希匹配返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">参数为 null 或空</exception>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    /// <exception cref="FormatException">expectedHash 格式不正确</exception>
    public virtual bool VerifyPackage(string packagePath, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("更新包文件不存在", packagePath);

        // 解析 expectedHash 格式："sha256:<hex>"
        const string prefix = "sha256:";
        if (!expectedHash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException(
                $"expectedHash 格式不正确，应为 \"sha256:<hex>\"，实际值: {expectedHash}");

        var expectedHex = expectedHash[prefix.Length..];
        if (string.IsNullOrEmpty(expectedHex))
            throw new FormatException("expectedHash 中缺少 SHA256 十六进制值");

        // 计算文件 SHA256
        XTrace.Log.Info("UpdateChecker: 校验更新包哈希: {0}", packagePath);

        string actualHex;
        using (var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read))
        {
            var hashBytes = SHA256.HashData(stream);
            actualHex = Convert.ToHexStringLower(hashBytes);
        }

        var match = string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
        XTrace.Log.Info("UpdateChecker: 哈希校验 {0} (期望: {1}, 实际: {2})",
            match ? "通过" : "失败",
            expectedHex[..Math.Min(expectedHex.Length, 16)] + "...",
            actualHex[..Math.Min(actualHex.Length, 16)] + "...");

        return match;
    }

    /// <summary>
    /// 从当前进程推断当前版本号。
    /// 优先读取 <see cref="FileVersionInfo.GetVersionInfo"/> 中的 FileVersion 或 ProductVersion。
    /// </summary>
    /// <returns>版本号字符串，如 "1.0.0.0"</returns>
    public static string GetCurrentVersion()
    {
        try
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(
                Environment.ProcessPath ?? typeof(UpdateChecker).Assembly.Location);
            return fileVersion.FileVersion ?? fileVersion.ProductVersion ?? "1.0.0.0";
        }
        catch
        {
            return "1.0.0.0";
        }
    }

    /// <summary>
    /// 解析 StarServer 版本检查响应 JSON 到 <see cref="UpdateCheckResult"/>。
    /// </summary>
    private static void ParseCheckResponse(string json, UpdateCheckResult result)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 检查 success 字段
        if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
        {
            // 错误响应
            if (root.TryGetProperty("error", out var errorProp))
            {
                var code = errorProp.TryGetProperty("code", out var c) ? c.GetString() : "UNKNOWN";
                var message = errorProp.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
                result.ErrorMessage = $"[{code}] {message}";
            }
            else
            {
                result.ErrorMessage = "服务器返回 success=false";
            }
            result.IsSuccess = false;
            return;
        }

        // 解析 data 字段
        if (!root.TryGetProperty("data", out var dataProp))
        {
            result.ErrorMessage = "响应中缺少 data 字段";
            result.IsSuccess = false;
            return;
        }

        // hasUpdate
        if (dataProp.TryGetProperty("hasUpdate", out var hasUpdateProp))
        {
            result.HasUpdate = hasUpdateProp.GetBoolean();
        }

        // latestVersion
        if (dataProp.TryGetProperty("latestVersion", out var versionProp))
        {
            var versionStr = versionProp.GetString();
            if (!string.IsNullOrEmpty(versionStr))
            {
                result.LatestVersion = TryParseVersion(versionStr);
            }
        }

        // downloadUrl
        if (dataProp.TryGetProperty("downloadUrl", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
        {
            result.DownloadUrl = urlProp.GetString();
        }

        // packageHash
        if (dataProp.TryGetProperty("packageHash", out var hashProp) && hashProp.ValueKind == JsonValueKind.String)
        {
            result.PackageHash = hashProp.GetString();
        }

        // packageSize
        if (dataProp.TryGetProperty("packageSize", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number)
        {
            result.PackageSize = sizeProp.GetInt64();
        }

        // releaseNotes
        if (dataProp.TryGetProperty("releaseNotes", out var notesProp) && notesProp.ValueKind == JsonValueKind.String)
        {
            result.ReleaseNotes = notesProp.GetString();
        }

        result.IsSuccess = true;
    }

    /// <summary>
    /// 安全解析版本号字符串，失败时返回 null。
    /// </summary>
    private static Version? TryParseVersion(string versionStr)
    {
        if (string.IsNullOrEmpty(versionStr))
            return null;

        // 尝试解析标准版本号（如 "1.0.0.0"）
        if (Version.TryParse(versionStr, out var version))
            return version;

        // 尝试补全为 4 段（如 "1.0" → "1.0.0.0"）
        var parts = versionStr.Split('.');
        if (parts.Length < 4)
        {
            var padded = string.Join(".", parts.Concat(Enumerable.Repeat("0", 4 - parts.Length)));
            if (Version.TryParse(padded, out version))
                return version;
        }

        XTrace.Log.Warn("UpdateChecker: 无法解析版本号: {0}", versionStr);
        return null;
    }

    /// <summary>
    /// 推断当前操作系统标识。
    /// </summary>
    private static string InferOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                _ => "x64",
            };
            return $"win-{arch}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux-x64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx-x64";

        return "win-x64";
    }

    /// <summary>
    /// 推断当前运行时版本标识。
    /// </summary>
    private static string InferRuntime()
    {
        // 通过 RuntimeInformation.FrameworkDescription 推断
        var desc = RuntimeInformation.FrameworkDescription;
        if (string.IsNullOrEmpty(desc))
            return "net10.0";

        // 如 ".NET 10.0.0" → "net10.0"
        var parts = desc.Split(' ');
        if (parts.Length >= 2)
        {
            var versionPart = parts[1];
            var dotIndex = versionPart.IndexOf('.');
            if (dotIndex > 0)
            {
                var majorMinor = versionPart[..versionPart.LastIndexOf('.')];
                return $"net{majorMinor}";
            }

            // 如 "10.0.0" 直接推断
            if (Version.TryParse(versionPart, out var ver))
                return $"net{ver.Major}.{ver.Minor}";
        }

        return "net10.0";
    }
}