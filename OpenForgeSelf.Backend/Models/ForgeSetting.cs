using System.ComponentModel;
using NewLife.Configuration;
using XCode.Configuration;

namespace OpenForgeSelf.Backend.Models;

/// <summary>铸己匣本地配置</summary>
[Config("ForgeSetting")]
public class ForgeSetting : Config<ForgeSetting>
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
  #endregion
}
