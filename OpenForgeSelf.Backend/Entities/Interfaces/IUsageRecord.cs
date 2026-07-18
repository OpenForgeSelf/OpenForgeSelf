using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial interface IUsageRecord
{
    #region 属性
    /// <summary>记录ID</summary>
    Int64 Id { get; set; }

    /// <summary>插件ID</summary>
    String PluginId { get; set; }

    /// <summary>工具ID</summary>
    String ToolId { get; set; }

    /// <summary>操作类型</summary>
    String ActionType { get; set; }

    /// <summary>用户代理</summary>
    String UserAgent { get; set; }

    /// <summary>IP地址</summary>
    String IpAddress { get; set; }

    /// <summary>持续时间（毫秒）</summary>
    Int64 DurationMs { get; set; }

    /// <summary>时间戳</summary>
    DateTime Timestamp { get; set; }

    /// <summary>元数据JSON</summary>
    String MetadataJson { get; set; }

    /// <summary>工作流执行ID</summary>
    Int64 WorkflowExecutionId { get; set; }

    /// <summary>工作流步骤ID</summary>
    String StepId { get; set; }
    #endregion
}
