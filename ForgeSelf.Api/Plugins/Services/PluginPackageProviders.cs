using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.Services;

public sealed class PluginGitHubProvider
{
    private readonly HttpClient _http;
    private readonly string _apiBase;

    public PluginGitHubProvider(HttpClient? http = null, string? apiBase = null)
    {
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenForgeSelf");
        _apiBase = string.IsNullOrWhiteSpace(apiBase) ? "https://api.github.com" : apiBase.TrimEnd('/');
    }

    public async Task<(byte[] Bytes, string FileName)> DownloadLatestAsync(string repositoryUrl, CancellationToken cancellationToken)
    {
        var (owner, repo) = Parse(repositoryUrl);
        var json = await _http.GetStringAsync($"{_apiBase}/repos/{owner}/{repo}/releases/latest", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("assets", out var assets))
            throw new InvalidDataException("GitHub Release 没有资产");

        string? zipUrl = null, zipName = null, dllUrl = null, dllName = null, sumsUrl = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                continue;
            if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                sumsUrl = url;
            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = url;
                zipName = name;
            }
            else if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                dllUrl = url;
                dllName = name;
            }
        }

        var assetUrl = zipUrl ?? dllUrl;
        var assetName = zipName ?? dllName;
        if (assetUrl == null || assetName == null)
            throw new InvalidDataException("Release 里没有 .zip 或 .dll");

        var bytes = await _http.GetByteArrayAsync(assetUrl, cancellationToken);
        if (sumsUrl != null)
        {
            var sums = await _http.GetStringAsync(sumsUrl, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var matched = sums.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line =>
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return false;
                var file = parts[^1].TrimStart('*');
                return file.Equals(assetName, StringComparison.OrdinalIgnoreCase) &&
                       parts[0].Equals(hash, StringComparison.OrdinalIgnoreCase);
            });
            if (!matched)
                throw new InvalidDataException("SHA256SUMS.txt 与发行物不一致");
        }

        return (bytes, assetName);
    }

    public static (string Owner, string Repo) Parse(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub 地址必须是 https://github.com/owner/repo");

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new InvalidDataException("GitHub 地址缺少 owner/repo");
        var repo = parts[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];
        return (parts[0], repo);
    }
}

public sealed class PluginNuGetProvider
{
    private readonly HttpClient _http;
    private readonly string _flatContainer;

    public PluginNuGetProvider(HttpClient? http = null, string? flatContainer = null)
    {
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenForgeSelf");
        _flatContainer = string.IsNullOrWhiteSpace(flatContainer)
            ? "https://api.nuget.org/v3-flatcontainer"
            : flatContainer.TrimEnd('/');
    }

    public static string NormalizePackageId(string packageName)
    {
        var name = packageName.Trim();
        if (name.StartsWith("OpenForgeSelf.Plugin.", StringComparison.OrdinalIgnoreCase))
            return name;
        return "OpenForgeSelf.Plugin." + name;
    }

    public async Task<(byte[] Bytes, string FileName)> DownloadLatestAsync(string packageName, CancellationToken cancellationToken)
    {
        var id = NormalizePackageId(packageName).ToLowerInvariant();
        var indexJson = await _http.GetStringAsync($"{_flatContainer}/{id}/index.json", cancellationToken);
        using var index = JsonDocument.Parse(indexJson);
        if (!index.RootElement.TryGetProperty("versions", out var versions) || versions.GetArrayLength() == 0)
            throw new InvalidDataException("NuGet 包没有版本");
        var version = versions[versions.GetArrayLength() - 1].GetString()
            ?? throw new InvalidDataException("NuGet 版本为空");
        var nupkg = await _http.GetByteArrayAsync($"{_flatContainer}/{id}/{version}/{id}.{version}.nupkg", cancellationToken);
        using var ms = new MemoryStream(nupkg);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var chosen = zip.Entries.FirstOrDefault(e =>
                         e.FullName.Replace('\\', '/').StartsWith("plugin/", StringComparison.OrdinalIgnoreCase) &&
                         e.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                     ?? zip.Entries.FirstOrDefault(e =>
                         e.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                         e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        if (chosen == null)
            throw new InvalidDataException("nupkg 内没有 ZIP 或 DLL");

        using var entry = chosen.Open();
        using var output = new MemoryStream();
        entry.CopyTo(output);
        return (output.ToArray(), chosen.Name);
    }
}
