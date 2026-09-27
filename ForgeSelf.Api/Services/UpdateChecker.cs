using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    // GitHub 分支：最近一次检查命中的资产下载直链（spec 036）
    private string? _githubAssetUrl;
    private string? _githubAssetTag;

    // 本地目录分支：最近一次检查命中的 zip 路径与归一化版本（2026-09-27）
    private string? _localZipPath;
    private string? _localZipTag;

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
            // GitHub Releases provider 分支（spec 036）；stardust 默认路径保持不变
            if (string.Equals(_config.Provider, "github", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckGitHubAsync(result);
            }

            // 本地目录 provider 分支（2026-09-27）：扫本机打包脚本输出的 zip 目录
            // Gitee Releases provider 分支（2026-09-28）：国内网络更稳的镜像发布源
            if (string.Equals(_config.Provider, "gitee", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckGiteeAsync(result);
            }

            if (string.Equals(_config.Provider, "local", StringComparison.OrdinalIgnoreCase))
            {
                return CheckLocalAsync(result);
            }

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

        // GitHub Releases 下载分支（spec 036）
        if (string.Equals(_config.Provider, "github", StringComparison.OrdinalIgnoreCase))
        {
            await DownloadGitHubPackageAsync(version, destPath);
            return;
        }

        // 本地目录下载分支（2026-09-27）：直接拷贝 zip
        // Gitee Releases 下载分支（2026-09-28）
        if (string.Equals(_config.Provider, "gitee", StringComparison.OrdinalIgnoreCase))
        {
            await DownloadGiteePackageAsync(version, destPath);
            return;
        }

        if (string.Equals(_config.Provider, "local", StringComparison.OrdinalIgnoreCase))
        {
            await DownloadLocalPackageAsync(version, destPath);
            return;
        }

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

    // ======================================================================
    // GitHub Releases provider（spec 036）
    // ======================================================================

    /// <summary>
    /// 通过 GitHub Releases API 检查更新。
    /// 拉取 releases 列表（含预发布过滤：channel=stable 时排除 prerelease），
    /// 按 semver 取最新，选择 OpenForgeSelf-*-win-x64.zip 资产。
    /// 网络/解析异常由 <see cref="CheckForUpdateAsync"/> 外层通用 catch 兜底。
    /// </summary>
    private async Task<UpdateCheckResult> CheckGitHubAsync(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(_config.GitHubRepo))
        {
            result.IsSuccess = false;
            result.ErrorMessage = "GitHubRepo 未配置";
            return result;
        }

        var baseUrl = _config.GitHubApiUrl.TrimEnd('/');
        var requestUrl = $"{baseUrl}/repos/{_config.GitHubRepo}/releases?per_page=30";
        XTrace.Log.Info("UpdateChecker(GitHub): 检查更新: {0}", requestUrl);

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.CheckTimeoutSeconds, 5)));

        using (var request = CreateGitHubRequest(requestUrl))
        {
            var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cts.Token);

            using var doc = JsonDocument.Parse(json);

            var channelAllowsPrerelease =
                !string.Equals(_config.Channel, "stable", StringComparison.OrdinalIgnoreCase);

            JsonElement? chosen = null;
            SemVer? chosenSem = null;
            string? chosenTag = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draftEl) &&
                    draftEl.ValueKind == JsonValueKind.True)
                    continue;

                var isPrerelease = release.TryGetProperty("prerelease", out var preEl) &&
                    preEl.ValueKind == JsonValueKind.True;
                if (isPrerelease && !channelAllowsPrerelease)
                    continue;

                var tag = release.TryGetProperty("tag_name", out var tagEl)
                    ? tagEl.GetString() : null;
                var sem = ParseSemVer(tag);
                if (sem == null)
                    continue;

                if (chosenSem == null || CompareSemVer(sem, chosenSem) > 0)
                {
                    chosen = release;
                    chosenSem = sem;
                    chosenTag = tag;
                }
            }

            result.IsSuccess = true;

            if (chosen == null || chosenSem == null)
            {
                result.HasUpdate = false;
                XTrace.Log.Info("UpdateChecker(GitHub): 通道 {0} 下无匹配的 Release。", _config.Channel);
                return result;
            }

            var currentSem = ParseSemVer(_currentVersion);
            result.LatestVersion = chosenSem.ToVersion();
            result.LatestVersionTag = chosenTag;
            result.HasUpdate = currentSem == null || CompareSemVer(chosenSem, currentSem) > 0;

            if (chosen.Value.TryGetProperty("body", out var bodyEl) &&
                bodyEl.ValueKind == JsonValueKind.String)
            {
                result.ReleaseNotes = bodyEl.GetString();
            }

            // 选择 win-x64 zip 资产：OpenForgeSelf-<ver>-win-x64.zip
            string? assetUrl = null;
            if (result.HasUpdate &&
                chosen.Value.TryGetProperty("assets", out var assetsEl) &&
                assetsEl.ValueKind == JsonValueKind.Array)
            {
                assetUrl = ExtractWinX64Asset(assetsEl, result);
            }

            if (result.HasUpdate)
            {
                if (string.IsNullOrEmpty(assetUrl))
                {
                    result.HasUpdate = false;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Release {chosenTag} 中未找到 win-x64 更新包资产";
                    XTrace.Log.Warn("UpdateChecker(GitHub): {0}", result.ErrorMessage);
                    return result;
                }

                // DownloadUrl 已由 ExtractWinX64Asset 填为 browser_download_url（展示用）；
                // 缓存 API 直链供下载复用。
                _githubAssetUrl = assetUrl;
                _githubAssetTag = NormalizeTag(chosenTag);
            }

            XTrace.Log.Info("UpdateChecker(GitHub): 检查完成, HasUpdate={0}, Tag={1}",
                result.HasUpdate, chosenTag);
            return result;
        }
    }

    /// <summary>从 release.assets 中选取 win-x64 更新包，顺带填充 PackageHash / PackageSize。</summary>
    private static string? ExtractWinX64Asset(JsonElement assetsEl, UpdateCheckResult result)
    {
        foreach (var asset in assetsEl.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() : null;
            if (name == null ||
                !name.StartsWith("OpenForgeSelf-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase))
                continue;

            // 下载走资产 API 直链（api.github.com/repos/…/releases/assets/{id} + octet-stream）：
            // 公开/私有仓库均可用，且不依赖 browser_download_url 的重定向行为。
            // browser_download_url 仅作为人读展示链接放 DownloadUrl。
            var apiAssetUrl = asset.TryGetProperty("url", out var apiUrlEl)
                ? apiUrlEl.GetString() : null;
            var browserUrl = asset.TryGetProperty("browser_download_url", out var urlEl)
                ? urlEl.GetString() : null;
            result.DownloadUrl = browserUrl ?? apiAssetUrl;
            var url = apiAssetUrl ?? browserUrl;

            if (asset.TryGetProperty("digest", out var digestEl) &&
                digestEl.ValueKind == JsonValueKind.String)
            {
                result.PackageHash = digestEl.GetString();
            }
            else
            {
                XTrace.Log.Warn("UpdateChecker(GitHub): 资产 {0} 无 digest，下载后将跳过 SHA256 校验。", name);
            }

            if (asset.TryGetProperty("size", out var sizeEl) &&
                sizeEl.ValueKind == JsonValueKind.Number)
            {
                result.PackageSize = sizeEl.GetInt64();
            }

            return url;
        }

        return null;
    }

    /// <summary>从 GitHub Release 资产下载更新包（Accept: application/octet-stream，跟随重定向）。</summary>
    private async Task DownloadGitHubPackageAsync(string version, string destPath)
    {
        if (string.IsNullOrWhiteSpace(_config.GitHubRepo))
            throw new InvalidOperationException("GitHubRepo 未配置，无法下载更新包。");

        var assetUrl = await ResolveGitHubAssetUrlAsync(version);
        XTrace.Log.Info("UpdateChecker(GitHub): 下载更新包: {0} → {1}", assetUrl, destPath);

        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.DownloadTimeoutSeconds, 30)));

        try
        {
            using var request = CreateGitHubRequest(assetUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            using var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream, cts.Token);

            XTrace.Log.Info("UpdateChecker(GitHub): 下载完成: {0} ({1} bytes)", destPath,
                new FileInfo(destPath).Length);
        }
        catch (OperationCanceledException)
        {
            throw new HttpRequestException(
                $"下载更新包超时 ({_config.DownloadTimeoutSeconds}s): {assetUrl}");
        }
    }

    /// <summary>解析资产下载直链：优先用最近检查结果缓存，未命中则按 tag 反查 API。</summary>
    private async Task<string> ResolveGitHubAssetUrlAsync(string version)
    {
        var wanted = NormalizeTag(version);
        if (_githubAssetUrl != null && _githubAssetTag == wanted)
            return _githubAssetUrl;

        var baseUrl = _config.GitHubApiUrl.TrimEnd('/');
        var candidateTags = new List<string> { version };
        var withoutV = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version[1..] : version;
        candidateTags.Add("v" + withoutV);
        candidateTags.Add(withoutV);

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.CheckTimeoutSeconds, 10)));

        foreach (var tag in candidateTags.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = $"{baseUrl}/repos/{_config.GitHubRepo}/releases/tags/{Uri.EscapeDataString(tag)}";
            using var request = CreateGitHubRequest(url);
            var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                continue;

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assetsEl) ||
                assetsEl.ValueKind != JsonValueKind.Array)
                continue;

            var dummy = new UpdateCheckResult();
            var assetUrl = ExtractWinX64Asset(assetsEl, dummy);
            if (!string.IsNullOrEmpty(assetUrl))
            {
                _githubAssetUrl = assetUrl;
                _githubAssetTag = NormalizeTag(tag);
                return assetUrl;
            }
        }

        throw new InvalidOperationException($"未找到版本 {version} 对应的 GitHub Release win-x64 更新包。");
    }

    // ======================================================================
    // Gitee Releases provider（2026-09-28：国内网络更稳的镜像发布源）
    // Gitee API v5 releases 结构与 GitHub 同构：tag_name / body / assets，公开仓库匿名可读。
    // 差异：资产无 digest 字段 → 无哈希时下载后跳过 SHA256 校验（StagedUpdate 既有逻辑）。
    // ======================================================================
    /// <summary>
    /// 通过 Gitee API v5 releases 检查更新（匿名可读公开仓，结构同 GitHub）。
    /// </summary>
    private async Task<UpdateCheckResult> CheckGiteeAsync(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(_config.GiteeRepo))
        {
            result.IsSuccess = false;
            result.ErrorMessage = "GiteeRepo 未配置";
            return result;
        }
        var requestUrl = $"https://gitee.com/api/v5/repos/{_config.GiteeRepo}/releases?per_page=30";
        XTrace.Log.Info("UpdateChecker(Gitee): 检查更新: {0}", requestUrl);
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.CheckTimeoutSeconds, 5)));
        var response = await _httpClient.GetAsync(requestUrl, cts.Token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);
        var channelAllowsPrerelease =
            !string.Equals(_config.Channel, "stable", StringComparison.OrdinalIgnoreCase);
        JsonElement? chosen = null;
        SemVer? chosenSem = null;
        string? chosenTag = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var isPrerelease = release.TryGetProperty("prerelease", out var preEl) &&
                preEl.ValueKind == JsonValueKind.True;
            if (isPrerelease && !channelAllowsPrerelease)
                continue;
            var tag = release.TryGetProperty("tag_name", out var tagEl)
                ? tagEl.GetString() : null;
            var sem = ParseSemVer(tag);
            if (sem == null)
                continue;
            if (chosenSem == null || CompareSemVer(sem, chosenSem) > 0)
            {
                chosen = release;
                chosenSem = sem;
                chosenTag = tag;
            }
        }
        result.IsSuccess = true;
        if (chosen == null || chosenSem == null)
        {
            result.HasUpdate = false;
            XTrace.Log.Info("UpdateChecker(Gitee): 通道 {0} 下无匹配的 Release。", _config.Channel);
            return result;
        }
        var currentSem = ParseSemVer(_currentVersion);
        result.LatestVersion = chosenSem.ToVersion();
        result.LatestVersionTag = chosenTag;
        result.HasUpdate = currentSem == null || CompareSemVer(chosenSem, currentSem) > 0;
        if (chosen.Value.TryGetProperty("body", out var bodyEl) &&
            bodyEl.ValueKind == JsonValueKind.String)
        {
            result.ReleaseNotes = bodyEl.GetString();
        }
        // 资产下载走 browser_download_url 直链（匿名可下）；Gitee 无 digest → 无哈希时下载后跳过校验
        string? assetUrl = null;
        if (result.HasUpdate &&
            chosen.Value.TryGetProperty("assets", out var assetsEl) &&
            assetsEl.ValueKind == JsonValueKind.Array)
        {
            assetUrl = ExtractGiteeAsset(assetsEl, result);
        }
        if (result.HasUpdate)
        {
            if (string.IsNullOrEmpty(assetUrl))
            {
                result.HasUpdate = false;
                result.IsSuccess = false;
                result.ErrorMessage = $"Release {chosenTag} 中未找到 win-x64 更新包资产";
                XTrace.Log.Warn("UpdateChecker(Gitee): {0}", result.ErrorMessage);
                return result;
            }
            // 复用资产缓存字段（语义为"最近一次检查的资产直链/tag"）
            _githubAssetUrl = assetUrl;
            _githubAssetTag = NormalizeTag(chosenTag);
        }
        XTrace.Log.Info("UpdateChecker(Gitee): 检查完成, HasUpdate={0}, Tag={1}",
            result.HasUpdate, chosenTag);
        return result;
    }
    /// <summary>从 Gitee release.assets 中选取 win-x64 更新包，填 PackageSize（Gitee 无 digest）。</summary>
    private static string? ExtractGiteeAsset(JsonElement assetsEl, UpdateCheckResult result)
    {
        foreach (var asset in assetsEl.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() : null;
            if (name == null ||
                !name.StartsWith("OpenForgeSelf-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase))
                continue;
            var browserUrl = asset.TryGetProperty("browser_download_url", out var urlEl)
                ? urlEl.GetString() : null;
            if (string.IsNullOrEmpty(browserUrl))
                continue;
            result.DownloadUrl = browserUrl;
            if (asset.TryGetProperty("size", out var sizeEl) &&
                sizeEl.ValueKind == JsonValueKind.Number)
            {
                result.PackageSize = sizeEl.GetInt64();
            }
            return browserUrl;
        }
        return null;
    }
    /// <summary>从 Gitee Release 资产下载更新包（browser_download_url 直链，跟随重定向）。</summary>
    private async Task DownloadGiteePackageAsync(string version, string destPath)
    {
        if (string.IsNullOrWhiteSpace(_config.GiteeRepo))
            throw new InvalidOperationException("GiteeRepo 未配置，无法下载更新包。");
        var wanted = NormalizeTag(version);
        var assetUrl = (_githubAssetUrl != null && _githubAssetTag == wanted)
            ? _githubAssetUrl
            : await ResolveGiteeAssetUrlAsync(version);
        XTrace.Log.Info("UpdateChecker(Gitee): 下载更新包: {0} → {1}", assetUrl, destPath);
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.DownloadTimeoutSeconds, 30)));
        try
        {
            using var response = await _httpClient.GetAsync(assetUrl,
                HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream, cts.Token);
            XTrace.Log.Info("UpdateChecker(Gitee): 下载完成: {0} ({1} bytes)", destPath,
                new FileInfo(destPath).Length);
        }
        catch (OperationCanceledException)
        {
            throw new HttpRequestException(
                $"下载更新包超时 ({_config.DownloadTimeoutSeconds}s): {assetUrl}");
        }
    }
    /// <summary>按 tag 反查 Gitee Release 解析 win-x64 资产直链。</summary>
    private async Task<string> ResolveGiteeAssetUrlAsync(string version)
    {
        var candidateTags = new List<string> { version };
        var withoutV = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version[1..] : version;
        candidateTags.Add("v" + withoutV);
        candidateTags.Add(withoutV);
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Max(_config.CheckTimeoutSeconds, 10)));
        foreach (var tag in candidateTags.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = $"https://gitee.com/api/v5/repos/{_config.GiteeRepo}/releases/tags/{Uri.EscapeDataString(tag)}";
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                continue;
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assetsEl) ||
                assetsEl.ValueKind != JsonValueKind.Array)
                continue;
            var dummy = new UpdateCheckResult();
            var assetUrl = ExtractGiteeAsset(assetsEl, dummy);
            if (!string.IsNullOrEmpty(assetUrl))
            {
                _githubAssetUrl = assetUrl;
                _githubAssetTag = NormalizeTag(tag);
                return assetUrl;
            }
        }
        throw new InvalidOperationException($"未找到版本 {version} 对应的 Gitee Release win-x64 更新包。");
    }


    // ======================================================================
    // Local directory provider（2026-09-27：更新地址可设置为本地目录）
    // 目录内容 = scripts/release/release-local.ps1 -UpdateDir <目录> 的输出：
    //   OpenForgeSelf-<ver>-win-x64.zip + SHA256SUMS.txt + RELEASE-NOTES-<ver>.md
    // ======================================================================

    /// <summary>
    /// 通过本地目录检查更新：扫描 OpenForgeSelf-*-win-x64.zip 按 semver 取最新，
    /// 读取同目录 SHA256SUMS.txt 作为包哈希、RELEASE-NOTES-&lt;ver&gt;.md 作为更新说明。
    /// </summary>
    private UpdateCheckResult CheckLocalAsync(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(_config.LocalDir))
        {
            result.IsSuccess = false;
            result.ErrorMessage = "LocalDir 未配置";
            return result;
        }

        if (!Directory.Exists(_config.LocalDir))
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"本地更新目录不存在: {_config.LocalDir}";
            return result;
        }

        var zipPath = FindLatestLocalZip(out var latestSem, out var zipVersion);
        result.IsSuccess = true;

        if (zipPath == null || latestSem == null)
        {
            result.HasUpdate = false;
            XTrace.Log.Info("UpdateChecker(Local): 目录 {0} 下无 OpenForgeSelf-*-win-x64.zip 更新包。",
                _config.LocalDir);
            return result;
        }

        var currentSem = ParseSemVer(_currentVersion);
        result.LatestVersion = latestSem.ToVersion();
        result.LatestVersionTag = "v" + zipVersion;
        result.HasUpdate = currentSem == null || CompareSemVer(latestSem, currentSem) > 0;
        result.DownloadUrl = zipPath;
        result.PackageSize = new FileInfo(zipPath).Length;

        // SHA256SUMS.txt："{hash}  <zipName>"（两空格分隔，package-release.ps1 写入格式）
        var sumsPath = Path.Combine(_config.LocalDir, "SHA256SUMS.txt");
        if (File.Exists(sumsPath))
        {
            var hash = FindHashForZip(sumsPath, Path.GetFileName(zipPath));
            if (!string.IsNullOrEmpty(hash))
                result.PackageHash = "sha256:" + hash;
            else
                XTrace.Log.Warn("UpdateChecker(Local): SHA256SUMS.txt 中未找到 {0} 的哈希，下载后将跳过校验。",
                    Path.GetFileName(zipPath));
        }
        else
        {
            XTrace.Log.Warn("UpdateChecker(Local): 目录下无 SHA256SUMS.txt，下载后将跳过 SHA256 校验。");
        }

        // 更新说明：RELEASE-NOTES-<ver>.md
        var notesPath = Path.Combine(_config.LocalDir, $"RELEASE-NOTES-{zipVersion}.md");
        if (File.Exists(notesPath))
            result.ReleaseNotes = File.ReadAllText(notesPath);

        _localZipPath = zipPath;
        _localZipTag = NormalizeTag(zipVersion);

        XTrace.Log.Info("UpdateChecker(Local): 检查完成, HasUpdate={0}, Version={1}",
            result.HasUpdate, result.LatestVersionTag);
        return result;
    }

    /// <summary>扫描目录，返回 semver 最高的 OpenForgeSelf-*-win-x64.zip 完整路径。</summary>
    private string? FindLatestLocalZip(out SemVer? latestSem, out string? zipVersion)
    {
        latestSem = null;
        zipVersion = null;
        string? chosen = null;

        if (!Directory.Exists(_config.LocalDir))
            return null;

        foreach (var file in Directory.EnumerateFiles(_config.LocalDir, "OpenForgeSelf-*-win-x64.zip"))
        {
            var m = Regex.Match(Path.GetFileName(file),
                @"^OpenForgeSelf-(.+)-win-x64\.zip$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            var fileSem = ParseSemVer(m.Groups[1].Value);
            if (fileSem == null)
                continue;

            if (latestSem == null || CompareSemVer(fileSem, latestSem) > 0)
            {
                latestSem = fileSem;
                zipVersion = m.Groups[1].Value;
                chosen = file;
            }
        }

        return chosen;
    }

    /// <summary>从 SHA256SUMS.txt 中按文件名取 64 位 hex 哈希（小写）。</summary>
    private static string? FindHashForZip(string sumsPath, string zipFileName)
    {
        foreach (var line in File.ReadLines(sumsPath))
        {
            var idx = line.IndexOf("  ", StringComparison.Ordinal);
            if (idx <= 0)
                continue;

            var hash = line[..idx].Trim();
            var name = line[(idx + 2)..].Trim();
            if (string.Equals(name, zipFileName, StringComparison.OrdinalIgnoreCase) &&
                hash.Length == 64 &&
                hash.All(c => Uri.IsHexDigit(c)))
            {
                return hash.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>本地目录下载：解析版本对应的 zip 路径后拷贝到 destPath。</summary>
    private async Task DownloadLocalPackageAsync(string version, string destPath)
    {
        var zipPath = ResolveLocalZip(version);
        if (zipPath == null)
            throw new InvalidOperationException($"本地更新目录中未找到版本 {version} 的更新包。");

        XTrace.Log.Info("UpdateChecker(Local): 拷贝更新包: {0} → {1}", zipPath, destPath);

        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var source = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(dest);

        XTrace.Log.Info("UpdateChecker(Local): 拷贝完成: {0} ({1} bytes)", destPath,
            new FileInfo(destPath).Length);
    }

    /// <summary>解析版本对应的本地 zip 路径：优先用最近检查缓存，未命中则按文件名扫描。</summary>
    private string? ResolveLocalZip(string version)
    {
        if (_localZipPath != null && _localZipTag == NormalizeTag(version))
            return _localZipPath;

        var wanted = NormalizeTag(version);
        foreach (var file in Directory.EnumerateFiles(_config.LocalDir, "OpenForgeSelf-*-win-x64.zip"))
        {
            var m = Regex.Match(Path.GetFileName(file),
                @"^OpenForgeSelf-(.+)-win-x64\.zip$", RegexOptions.IgnoreCase);
            if (m.Success && NormalizeTag(m.Groups[1].Value) == wanted)
                return file;
        }

        return null;
    }

    /// <summary>
    /// 构造 GitHub API 请求（仅带 User-Agent）。
    /// 更新源为公开仓库，匿名访问即可；历史版本曾支持 Bearer token（私有仓库），
    /// 仓库转公开后该路径已移除——匿名请求私有仓库会被 GitHub 以 404 掩盖为「不存在」。
    /// </summary>
    private HttpRequestMessage CreateGitHubRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(_appName);
        return request;
    }

    /// <summary>归一化 tag（去 v 前缀、小写），用于比较。</summary>
    private static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var trimmed = tag.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[1..];
        return trimmed.ToLowerInvariant();
    }

    /// <summary>semver 解析结果：4 段 core + 可选预发布标识。</summary>
    internal sealed record SemVer(int[] Core, string? Prerelease)
    {
        public Version ToVersion() => new(Core[0], Core[1], Core[2]);
    }

    /// <summary>
    /// 解析 semver 风格版本号："v1.2.3"、"1.2.3"、"1.2.3.4"、"1.2.3-beta.1+build" 均可；
    /// 解析失败返回 null。
    /// </summary>
    internal static SemVer? ParseSemVer(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];

        var plusIndex = s.IndexOf('+');
        if (plusIndex >= 0)
            s = s[..plusIndex];

        string? prerelease = null;
        var dashIndex = s.IndexOf('-');
        if (dashIndex >= 0)
        {
            prerelease = s[(dashIndex + 1)..];
            s = s[..dashIndex];
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 4)
            return null;

        var core = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var n) || n < 0)
                return null;
            core[i] = n;
        }

        return new SemVer(core, prerelease);
    }

    /// <summary>semver 比较：core 逐段比较，无预发布标识 &gt; 有预发布标识。</summary>
    internal static int CompareSemVer(SemVer a, SemVer b)
    {
        for (var i = 0; i < 4; i++)
        {
            var c = a.Core[i].CompareTo(b.Core[i]);
            if (c != 0) return c;
        }

        if (a.Prerelease == null && b.Prerelease == null) return 0;
        if (a.Prerelease == null) return 1;
        if (b.Prerelease == null) return -1;

        return ComparePrerelease(a.Prerelease, b.Prerelease);
    }

    /// <summary>semver 预发布标识比较（数字段按数值、其余按字典序，短的小于长的）。</summary>
    private static int ComparePrerelease(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var aNumeric = int.TryParse(pa[i], out var x);
            var bNumeric = int.TryParse(pb[i], out var y);
            int c;
            if (aNumeric && bNumeric) c = x.CompareTo(y);
            else if (aNumeric) c = -1;
            else if (bNumeric) c = 1;
            else c = string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
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
