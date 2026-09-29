using System.ComponentModel;
using NewLife.Configuration;
using ForgeSelf.Api.Data;
using XCode.Configuration;

namespace ForgeSelf.Api.Models;

/// <summary>铸己匣本地配置</summary>
[Config("ForgeSetting")]
public class ForgeSetting : ForgeConfig<ForgeSetting>
{
  #region 属性
  /// <summary>API令牌。用于API服务器认证的Bearer Token</summary>
  [Description("API令牌。用于API服务器认证的Bearer Token")]
  [Category("安全")]
  public String ApiToken { get; set; } = "";

  /// <summary>是否首次初始化。首次启动生成token后标记为false</summary>
  [Description("是否首次初始化。首次启动生成token后标记为false")]
  [Category("安全")]
  public Boolean IsFirstInit { get; set; } = true;
  /// <summary>应用监听端口号（1024-65535），默认 7102</summary>
  [Description("应用监听端口号（1024-65535），默认 7102")]
  [Category("网络")]
  public Int32 PortNumber { get; set; } = 7102;

  /// <summary>默认 AI 推理模型。存储 chatModelId（格式：提供商:上游模型id），空表示未设置</summary>
  [Description("默认 AI 推理模型（chatModelId）")]
  [Category("AI")]
  public String DefaultModel { get; set; } = "";

  /// <summary>密文迁移版本号。小于 SecretMigrationService.CurrentVersion 时执行 v1→v2 重封装，完成后置为当前版本。
  /// 与 IsFirstInit 无关（后者语义为「是否首次初始化」，不得复用）</summary>
  [Description("密文迁移版本号，用于密文 v1→v2 重封装的一次性触发")]
  [Category("安全")]
  public Int32 SecretMigrationVersion { get; set; } = 0;

  /// <summary>B9-6（R5）：大工具结果 spill 阈值（字节），超过该长度的 tool 结果在模型可见投影中截断为引用。
  /// 默认 32768（32 KiB）；宿主启动时播种到 SessionEventProjection.SpillThresholdBytes</summary>
  [Description("大工具结果 spill 阈值（字节），默认 32768（32 KiB）")]
  [Category("AI")]
  public Int32 SpillThresholdBytes { get; set; } = 32768;
  #endregion
}
