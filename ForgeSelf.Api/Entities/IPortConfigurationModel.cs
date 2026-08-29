using System;
using NewLife.Data;

namespace ForgeSelf.Api.Entities;

/// <summary>应用监听端口配置接口</summary>
public interface IPortConfigurationModel
{
  /// <summary>配置 ID</summary>
  Int64 Id { get; set; }

  /// <summary>监听端口号（1024-65535）</summary>
  Int32 PortNumber { get; set; }

  /// <summary>最后修改时间（UTC）</summary>
  DateTime UpdateTime { get; set; }
}
