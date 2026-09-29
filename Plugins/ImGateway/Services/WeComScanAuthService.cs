using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>扫码授权会话状态（供前端轮询展示）。</summary>
public enum ScanAuthState
{
    /// <summary>CLI 已拉起，正在等待输出扫码信息。</summary>
    Starting,

    /// <summary>扫码链接/二维码已拿到，等待用户扫码。</summary>
    WaitingScan,

    /// <summary>扫码成功，凭据已解密并自动回填保存。</summary>
    Succeeded,

    /// <summary>扫码失败（CLI 报错 / 凭据解密失败 / 超时）。</summary>
    Failed,
}

/// <summary>一次扫码授权会话（线程安全：只在后台任务写、轮询读）。</summary>
public sealed class ScanAuthSession
{
    public string Id { get; init; } = string.Empty;
    public ScanAuthState State { get; set; } = ScanAuthState.Starting;
    public string? QrLink { get; set; }
    public string? QrText { get; set; }
    public string? BotId { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 企业微信「智能机器人」扫码授权服务（v2.0.0，方案 A：CLI 仅作扫码取凭据工具）。
/// 流程：spawn 官方 CLI（<c>auth init --noninteractive</c>，隔离配置目录）→ 拿扫码链接/二维码 →
/// 用户手机企微扫码确认 → CLI 写加密凭据 credentials.enc → 本服务解密（AES-256-GCM，
/// key=base64(.encryption_key)，nonce 前 12 / tag 末 16）→ 自动回填 BotId+Secret 保存并重建长连接。
/// 凭据明文只在内存，不回传前端（前端只拿状态与 BotId 回显）；隔离目录保留密文留档。
/// </summary>
public class WeComScanAuthService
{
    /// <summary>扫码链接正则（CLI 输出形如 https://work.weixin.qq.com/ai/qc/gen?source=wecom_cli_external&amp;scode=...）。</summary>
    private static readonly Regex LinkPattern = new(
        @"https://work\.weixin\.qq\.com[^\s""'）)]+", RegexOptions.Compiled);

    /// <summary>等待扫码上限（用户 10 分钟不扫即判超时取消）。</summary>
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(10);

    /// <summary>CLI 配置目录环境变量（官方：不认 HOME/USERPROFILE/XDG 重定向，只认此变量）。</summary>
    private const string EnvConfigDir = "WECOM_CLI_CONFIG_DIR";

    private readonly IConfigStore _store;
    private readonly ImGatewayConnectionManager _connMgr;
    private readonly ConcurrentDictionary<string, ScanAuthSession> _sessions = new();
    private readonly string _baseDir;

    public WeComScanAuthService(IConfigStore store, ImGatewayConnectionManager connMgr)
    {
        _store = store;
        _connMgr = connMgr;
        _baseDir = Path.Combine(FileConfigStore.ResolvePluginDataDir(), "cli-auth");
    }

    /// <summary>
    /// 解析 CLI 可执行文件路径：配置 → 环境变量 WECOM_CLI_PATH → 本机已知路径 → 应用目录相对路径。
    /// 返回 null 表示不可用（前端应提示手填凭据）。
    /// </summary>
    public static string? ResolveCliPath(string? configured)
    {
        var candidates = new List<string?>();
        candidates.Add(configured);
        candidates.Add(Environment.GetEnvironmentVariable("WECOM_CLI_PATH"));
        candidates.Add(@"D:\src\os-proj\MyContext\apps\desktop\resources\bin\wecom-cli-win32-x64.exe"); // 本机开发环境已知路径
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "plugins", "ImGateway", "wecom-cli-win32-x64.exe"));

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return Path.GetFullPath(c);
        }
        return null;
    }

    /// <summary>发起一次扫码授权：拉起 CLI 后立即返回会话（前端轮询 <see cref="Get"/>）。</summary>
    public ScanAuthSession Start()
    {
        var cli = ResolveCliPath(_store.Load().WeCom.CliPath);
        if (cli == null)
            throw new InvalidOperationException("未找到企微 CLI（wecom-cli），无法扫码授权。可配置 cliPath 或环境变量 WECOM_CLI_PATH，或直接手填 BotId+Secret");

        var id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(_baseDir, id);
        Directory.CreateDirectory(dir);

        var session = new ScanAuthSession { Id = id };
        _sessions[id] = session;
        _ = Task.Run(() => RunCliAsync(session, cli, dir));
        return session;
    }

    /// <summary>取会话（不存在返回 null；轮询 404 即会话已失效）。</summary>
    public ScanAuthSession? Get(string id)
        => _sessions.TryGetValue(id, out var s) ? s : null;

    private async Task RunCliAsync(ScanAuthSession s, string cli, string dir)
    {
        try
        {
            // 隔离环境：凭据只写本次会话目录，绝不落默认 ~/.config/wecom（MyContext 的登录态不受影响）
            var psi = new ProcessStartInfo(cli, "auth init --noninteractive")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = dir,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Environment[EnvConfigDir] = dir;
            psi.Environment["WECOM_CLI_LOG_DIR"] = Path.Combine(dir, "logs");
            psi.Environment["WECOM_CLI_TMP_DIR"] = Path.Combine(dir, "tmp");

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("无法启动企微 CLI 进程");

            // 流式读输出（UTF-8 解码；链接/二维码均为 ASCII，不受 CLI 其它中文乱码影响）
            _ = Task.Run(async () =>
            {
                var sb = new StringBuilder();
                string? line;
                try
                {
                    while ((line = await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        AppendOutputLine(s, sb, line);
                    }
                    while ((line = await proc.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        AppendOutputLine(s, sb, line);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[企微扫码] 读取 CLI 输出结束: {0}", ex.Message);
                }
            });

            // 等待扫码（上限 10 分钟）
            using var timeoutCts = new CancellationTokenSource(ScanTimeout);
            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                s.State = ScanAuthState.Failed;
                s.Error = "扫码超时（10 分钟未完成），已取消，请重试";
                return;
            }

            // CLI 退出：复查隔离目录凭据
            var credPath = Path.Combine(dir, "credentials.enc");
            var keyPath = Path.Combine(dir, ".encryption_key");
            if (!File.Exists(credPath) || !File.Exists(keyPath))
            {
                s.State = ScanAuthState.Failed;
                s.Error = "扫码未完成或授权失败（未生成凭据文件），请重试";
                return;
            }

            var cred = DecryptCredentials(credPath, keyPath);
            if (cred == null)
            {
                s.State = ScanAuthState.Failed;
                s.Error = "凭据解密失败（credentials.enc 格式异常或密钥不匹配）";
                return;
            }

            // 自动回填并保存（保留既有绑定/开关状态；凭据更新后立即重建长连接）
            var cfg = _store.Load();
            cfg.WeCom.BotId = cred.Value.BotId;
            cfg.WeCom.Secret = cred.Value.Secret;
            _store.Save(cfg);
            try { _connMgr.Apply(); } catch (Exception ex) { XTrace.Log.Error("[企微扫码] 保存后重建长连接失败: {0}", ex.Message); }

            s.BotId = cred.Value.BotId;
            s.State = ScanAuthState.Succeeded;
            XTrace.Log.Info("[企微扫码] 授权成功并自动回填 botId={0}，长连接已按新凭据重建", cred.Value.BotId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[企微扫码] 授权流程异常: {0}", ex);
            s.State = ScanAuthState.Failed;
            s.Error = $"授权流程异常：{ex.Message}";
        }
    }

    private static void AppendOutputLine(ScanAuthSession s, StringBuilder sb, string line)
    {
        sb.AppendLine(line);
        if (s.QrLink == null)
        {
            var m = LinkPattern.Match(line);
            if (m.Success) s.QrLink = m.Value;
        }
        if (s.State == ScanAuthState.Starting) s.State = ScanAuthState.WaitingScan;
        // 二维码/输出文本整块保留，供前端等宽展示（截断避免超长）
        if (sb.Length <= 16 * 1024) s.QrText = sb.ToString();
    }

    /// <summary>解密 CLI 凭据：AES-256-GCM，key=base64(.encryption_key)，nonce 前 12 / tag 末 16 字节。</summary>
    internal static (string BotId, string Secret)? DecryptCredentials(string credPath, string keyPath)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(File.ReadAllText(keyPath).Trim());
            var cipher = File.ReadAllBytes(credPath);
            if (cipher.Length <= 28) return null; // nonce(12) + tag(16) + 至少 1 字节密文

            var nonce = cipher.AsSpan(0, 12).ToArray();
            var tag = cipher.AsSpan(cipher.Length - 16, 16).ToArray();
            var payload = cipher.AsSpan(12, cipher.Length - 28).ToArray();

            using var aes = new AesGcm(keyBytes, 16); // tag 16 字节（GCM 默认），显式指定避免 SYSLIB0053
            var plain = new byte[payload.Length];
            aes.Decrypt(nonce, payload, tag, plain);

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
            var bot = doc.RootElement.GetProperty("bot");
            var botId = bot.GetProperty("id").GetString();
            var secret = bot.GetProperty("secret").GetString();
            if (string.IsNullOrWhiteSpace(botId) || string.IsNullOrWhiteSpace(secret)) return null;
            return (botId!, secret!);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[企微扫码] 解密凭据失败: {0}", ex.Message);
            return null;
        }
    }
}
