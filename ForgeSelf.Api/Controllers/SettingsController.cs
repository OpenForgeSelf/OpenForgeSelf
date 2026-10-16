using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;

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

    // ===== 网络设置（模型请求代理，2026-10-16-settings-network-proxy）=====
    var enabled = request.ProxyEnabled ?? s.ProxyEnabled;
    var host = request.ProxyHost is null ? s.ProxyHost : request.ProxyHost.Trim();
    var port = request.ProxyPort ?? s.ProxyPort;
    var user = request.ProxyUsername is null ? s.ProxyUsername : request.ProxyUsername.Trim();

    if (port < 0 || port > 65535)
      return BadRequest(new { success = false, message = "代理端口无效：必须为 1-65535" });
    if (enabled)
    {
      if (string.IsNullOrWhiteSpace(host) || !ProxyHostPattern.IsMatch(host))
        return BadRequest(new { success = false, message = "代理地址无效：仅支持域名或 IPv4（不能带 http:// 前缀或端口）" });
      if (port < 1)
        return BadRequest(new { success = false, message = "开启代理时端口必须为 1-65535" });
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

    s.ProxyEnabled = enabled;
    s.ProxyHost = host ?? "";
    s.ProxyPort = port;
    s.ProxyUsername = user ?? "";
    s.ProxyPasswordCipher = cipher ?? "";
    s.Save();

    return Ok(new
    {
      success = true,
      message = "配置已保存",
      data = BuildDto(s)
    });
  }

  /// <summary>出参白名单组装：密码永不下发（明文与密文都不回传，仅回是否已设置）</summary>
  private static AppSettingsDto BuildDto(ForgeSetting s) => new()
  {
    DefaultModel = s.DefaultModel,
    ProxyEnabled = s.ProxyEnabled,
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

  /// <summary>模型请求是否走代理（false=显式直连）</summary>
  public Boolean ProxyEnabled { get; set; }

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

  /// <summary>模型请求是否走代理</summary>
  public Boolean? ProxyEnabled { get; set; }

  /// <summary>代理服务器地址（域名/IPv4）</summary>
  public String? ProxyHost { get; set; }

  /// <summary>代理服务器端口（1-65535）</summary>
  public Int32? ProxyPort { get; set; }

  /// <summary>代理认证用户名（空串=无需认证）</summary>
  public String? ProxyUsername { get; set; }

  /// <summary>代理密码明文入参：null=不修改，空串=清空，非空=加密后落盘</summary>
  public String? ProxyPassword { get; set; }
}
