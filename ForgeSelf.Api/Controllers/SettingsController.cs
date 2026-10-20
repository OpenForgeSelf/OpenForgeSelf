using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.Net;

namespace ForgeSelf.Api.Controllers;

/// <summary>本地配置控制器。读写 ForgeSetting.config（XML 文件），不落库。</summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
  private readonly ISecretEncryptionService _encryption;

  public SettingsController(ISecretEncryptionService encryption)
  {
    _encryption = encryption;
  }

  /// <summary>代理地址宿主校验：仅域名或 IPv4（不含 scheme/端口；IPv6 暂不支持——见 02-spec Unknown）</summary>
  private static readonly Regex ProxyHostPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

  /// <summary>获取当前本地配置（仅暴露前端需要的非敏感字段；代理密码只回报是否已设置）</summary>
  [HttpGet]
  public ActionResult<object> GetSettings()
  {
    return Ok(new
    {
      success = true,
      data = BuildDto(ForgeSetting.Current)
    });
  }

  /// <summary>更新本地配置并持久化到 ForgeSetting.config。
  /// 语义：网络字段 null=不修改；proxyPassword 空串=清空、非空=加密覆盖。
  /// DefaultModel 于 2026-10-16 由「null→清空」改为「null=不修改」（防止网络面板保存误清默认模型；
  /// 清空仍走空字符串，GeneralPanel/AiAgentPanel 均传值，不受影响）。</summary>
  [HttpPost]
  [Authorize("ApiKeyPolicy")]
  public ActionResult<object> UpdateSettings([FromBody] UpdateSettingsRequest request)
  {
    if (request == null)
      return BadRequest(new { success = false, message = "请求体为空" });

    var s = ForgeSetting.Current;

    // 默认模型：仅显式提供时更新；空字符串表示清空默认模型
    if (request.DefaultModel != null)
      s.DefaultModel = request.DefaultModel;

    // ===== 网络设置（模型请求代理，2026-10-16-settings-proxy-mode-bypass）=====
    var mode = request.ProxyMode is null ? s.ProxyMode : request.ProxyMode.Trim().ToLowerInvariant();
    if (mode is not (ModelProxy.ModeSystem or ModelProxy.ModeManual or ModelProxy.ModeDirect))
      return BadRequest(new { success = false, message = "代理模式无效：仅支持 system / manual / direct" });

    var host = request.ProxyHost is null ? s.ProxyHost : request.ProxyHost.Trim();
    var port = request.ProxyPort ?? s.ProxyPort;
    var user = request.ProxyUsername is null ? s.ProxyUsername : request.ProxyUsername.Trim();

    if (port < 0 || port > 65535)
      return BadRequest(new { success = false, message = "代理端口无效：必须为 1-65535" });
    if (mode == ModelProxy.ModeManual)
    {
      if (string.IsNullOrWhiteSpace(host) || !ProxyHostPattern.IsMatch(host))
        return BadRequest(new { success = false, message = "代理地址无效：仅支持域名或 IPv4（不能带 http:// 前缀或端口）" });
      if (port < 1)
        return BadRequest(new { success = false, message = "手动配置代理时端口必须为 1-65535" });
    }

    // 旁路条目（BR-5）：null=不修改；string[] 逐条校验（字符集/长度/条数），违规 400 带行号；落盘 \n join
    List<string>? bypassEntries = null;
    if (request.ProxyBypass != null)
    {
      bypassEntries = [];
      var lineNo = 0;
      foreach (var rawEntry in request.ProxyBypass)
      {
        lineNo++;
        var entry = (rawEntry ?? "").Trim();
        if (entry.Length == 0) continue; // 空行忽略
        if (bypassEntries.Count >= 50)
          return BadRequest(new { success = false, message = $"不走代理的条目超过上限 50 条（第 {lineNo} 行起）" });
        if (!BypassMatcher.IsValidEntry(entry))
          return BadRequest(new { success = false, message = $"不走代理的条目无效（第 {lineNo} 行）：仅支持域名/IPv4/通配符（* . - _ :），单条≤128 字符" });
        bypassEntries.Add(entry);
      }
    }

    var cipher = s.ProxyPasswordCipher;
    if (request.ProxyPassword != null)
    {
      if (request.ProxyPassword.Length == 0)
      {
        cipher = "";
      }
      else
      {
        try
        {
          cipher = _encryption.Encrypt(request.ProxyPassword);
        }
        catch (Exception ex)
        {
          return StatusCode(500, new { success = false, message = $"代理密码加密失败：{ex.Message}" });
        }
      }
    }

    s.ProxyMode = mode;
    s.ProxyHost = host ?? "";
    s.ProxyPort = port;
    s.ProxyUsername = user ?? "";
    s.ProxyPasswordCipher = cipher ?? "";
    if (bypassEntries != null)
      s.ProxyBypass = string.Join("\n", bypassEntries);
    s.Save();

    return Ok(new
    {
      success = true,
      message = "配置已保存",
      data = BuildDto(s)
    });
  }

  /// <summary>测试代理可用性（2026-10-16-settings-proxy-mode-bypass FR-3/BR-4）：
  /// 接收未保存的表单值，经指定代理向探测目标发一次性 GET；配置不落盘、密码不写日志。
  /// 判据：收到任何 HTTP 响应（含 4xx/5xx）= 代理可用（转发成功）；仅网络层失败=不可用。</summary>
  [HttpPost("test-proxy")]
  [Authorize("ApiKeyPolicy")]
  public async Task<ActionResult<object>> TestProxy([FromBody] TestProxyRequest? request)
  {
    if (request == null)
      return BadRequest(new { success = false, message = "请求体为空" });

    var host = (request.ProxyHost ?? "").Trim();
    if (host.Length == 0 || !ProxyHostPattern.IsMatch(host))
      return BadRequest(new { success = false, message = "代理地址无效：仅支持域名或 IPv4（不能带 http:// 前缀或端口）" });
    if (request.ProxyPort is < 1 or > 65535)
      return BadRequest(new { success = false, message = "代理端口无效：必须为 1-65535" });

    var url = string.IsNullOrWhiteSpace(request.ProxyTestUrl) ? DefaultProbeUrl : request.ProxyTestUrl.Trim();
    if (!ProbeUrlPattern.IsMatch(url))
      return BadRequest(new { success = false, message = "探测地址无效：仅支持 http(s):// 绝对地址" });

    // 临时 handler：显式指定入参代理，不受已保存配置影响（BR-4）；密码仅存内存，不入任何日志
    var proxy = new WebProxy($"http://{host}:{request.ProxyPort}");
    var user = (request.ProxyUsername ?? "").Trim();
    if (user.Length > 0)
      proxy.Credentials = new NetworkCredential(user, request.ProxyPassword ?? "");

    using var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

    var sw = Stopwatch.StartNew();
    try
    {
      using var resp = await client.GetAsync(url);
      sw.Stop();
      return Ok(new
      {
        success = true,
        data = new
        {
          reachable = true,
          statusCode = (int)resp.StatusCode,
          latencyMs = sw.ElapsedMilliseconds,
          message = $"代理可用（上游返回 HTTP {(int)resp.StatusCode}）"
        }
      });
    }
    catch (Exception ex)
    {
      sw.Stop();
      // 透出最内层原因（如 SocketException 的「积极拒绝」），供 UI 直显错误原文；不含任何凭据信息
      var root = ex;
      while (root.InnerException != null) root = root.InnerException;
      return Ok(new
      {
        success = true,
        data = new
        {
          reachable = false,
          statusCode = (int?)null,
          latencyMs = sw.ElapsedMilliseconds,
          message = root.Message
        }
      });
    }
  }

  /// <summary>默认探针（国内可达性实测优先；gstatic 在墙内不可达故不作默认）</summary>
  private const string DefaultProbeUrl = "https://www.baidu.com";
  private static readonly Regex ProbeUrlPattern = new(@"^https?://[A-Za-z0-9._\-]+(:\d{1,5})?(/.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

  /// <summary>出参白名单组装：密码永不下发（明文与密文都不回传，仅回是否已设置）</summary>
  private static AppSettingsDto BuildDto(ForgeSetting s) => new()
  {
    DefaultModel = s.DefaultModel,
    ProxyMode = s.ProxyMode,
    ProxyBypass = BypassMatcher.Parse(s.ProxyBypass).ToArray(),
    ProxyHost = s.ProxyHost,
    ProxyPort = s.ProxyPort,
    ProxyUsername = s.ProxyUsername,
    ProxyPasswordSet = !string.IsNullOrEmpty(s.ProxyPasswordCipher)
  };
}

/// <summary>本地配置响应 DTO（不含任何敏感字段，如 ApiToken / 代理密码）</summary>
public class AppSettingsDto
{
  /// <summary>默认 AI 推理模型（chatModelId）</summary>
  public String DefaultModel { get; set; } = "";

  /// <summary>代理模式：system=跟随系统 / manual=手动配置 / direct=直连</summary>
  public String ProxyMode { get; set; } = ModelProxy.ModeSystem;

  /// <summary>不走代理的条目（一行一条）</summary>
  public String[] ProxyBypass { get; set; } = [];

  /// <summary>代理服务器地址（域名/IPv4）</summary>
  public String ProxyHost { get; set; } = "";

  /// <summary>代理服务器端口（1-65535，0=未填）</summary>
  public Int32 ProxyPort { get; set; }

  /// <summary>代理认证用户名（空=无需认证）</summary>
  public String ProxyUsername { get; set; } = "";

  /// <summary>是否已设置代理密码（只报存在性，永不回传密码本身）</summary>
  public Boolean ProxyPasswordSet { get; set; }
}

/// <summary>更新本地配置请求。除 DefaultModel 外，null=不修改。</summary>
public class UpdateSettingsRequest
{
  /// <summary>默认 AI 推理模型（chatModelId），空字符串清空，null 不修改（2026-10-16 语义调整）</summary>
  public String? DefaultModel { get; set; }

  /// <summary>代理模式（system/manual/direct），null=不修改</summary>
  public String? ProxyMode { get; set; }

  /// <summary>不走代理的条目（一行一条），null=不修改，空数组=清空</summary>
  public String[]? ProxyBypass { get; set; }

  /// <summary>代理服务器地址（域名/IPv4）</summary>
  public String? ProxyHost { get; set; }

  /// <summary>代理服务器端口（1-65535）</summary>
  public Int32? ProxyPort { get; set; }

  /// <summary>代理认证用户名（空串=无需认证）</summary>
  public String? ProxyUsername { get; set; }

  /// <summary>代理密码明文入参：null=不修改，空串=清空，非空=加密后落盘</summary>
  public String? ProxyPassword { get; set; }
}

/// <summary>测试代理请求（2026-10-16-settings-proxy-mode-bypass）：接受未保存的表单值，仅内存使用</summary>
public class TestProxyRequest
{
  /// <summary>代理地址（域名/IPv4）</summary>
  public String ProxyHost { get; set; } = "";

  /// <summary>代理端口（1-65535）</summary>
  public Int32 ProxyPort { get; set; }

  /// <summary>认证用户名（可选）</summary>
  public String? ProxyUsername { get; set; }

  /// <summary>认证密码明文（可选；不落盘、不写日志）</summary>
  public String? ProxyPassword { get; set; }

  /// <summary>探测目标地址（可选，空=内置探针 https://www.baidu.com）</summary>
  public String? ProxyTestUrl { get; set; }
}
