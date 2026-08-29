using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Controllers;

/// <summary>本地配置控制器。读写 ForgeSetting.config（XML 文件），不落库。</summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
  /// <summary>获取当前本地配置（仅暴露前端需要的非敏感字段）</summary>
  [HttpGet]
  public ActionResult<object> GetSettings()
  {
    return Ok(new
    {
      success = true,
      data = new AppSettingsDto
      {
        DefaultModel = ForgeSetting.Current.DefaultModel
      }
    });
  }

  /// <summary>更新本地配置并持久化到 ForgeSetting.config</summary>
  [HttpPost]
  [Authorize("ApiKeyPolicy")]
  public ActionResult<object> UpdateSettings([FromBody] UpdateSettingsRequest request)
  {
    if (request == null)
      return BadRequest(new { success = false, message = "请求体为空" });

    // 仅持久化已知字段；空字符串表示清空默认模型
    ForgeSetting.Current.DefaultModel = request.DefaultModel ?? "";
    ForgeSetting.Current.Save();

    return Ok(new
    {
      success = true,
      message = "配置已保存",
      data = new AppSettingsDto
      {
        DefaultModel = ForgeSetting.Current.DefaultModel
      }
    });
  }
}

/// <summary>本地配置响应 DTO（不含任何敏感字段，如 ApiToken）</summary>
public class AppSettingsDto
{
  /// <summary>默认 AI 推理模型（chatModelId）</summary>
  public String DefaultModel { get; set; } = "";
}

/// <summary>更新本地配置请求</summary>
public class UpdateSettingsRequest
{
  /// <summary>默认 AI 推理模型（chatModelId），可传空字符串清空</summary>
  public String? DefaultModel { get; set; }
}
