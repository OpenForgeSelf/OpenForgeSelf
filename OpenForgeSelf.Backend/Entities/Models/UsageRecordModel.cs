using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class UsageRecordModel
{
    #region 属性
    /// <summary>记录ID</summary>
    public Int64 Id { get; set; }

    /// <summary>插件ID</summary>
    public String PluginId { get; set; }

    /// <summary>工具ID</summary>
    public String ToolId { get; set; }

    /// <summary>操作类型</summary>
    public String ActionType { get; set; }

    /// <summary>用户代理</summary>
    public String UserAgent { get; set; }

    /// <summary>IP地址</summary>
    public String IpAddress { get; set; }

    /// <summary>持续时间（毫秒）</summary>
    public Int64 DurationMs { get; set; }

    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>元数据JSON</summary>
    public String MetadataJson { get; set; }

    /// <summary>工作流执行ID</summary>
    public Int64 WorkflowExecutionId { get; set; }

    /// <summary>工作流步骤ID</summary>
    public String StepId { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUsageRecordModel model)
    {
        Id = model.Id;
        PluginId = model.PluginId;
        ToolId = model.ToolId;
        ActionType = model.ActionType;
        UserAgent = model.UserAgent;
        IpAddress = model.IpAddress;
        DurationMs = model.DurationMs;
        Timestamp = model.Timestamp;
        MetadataJson = model.MetadataJson;
        WorkflowExecutionId = model.WorkflowExecutionId;
        StepId = model.StepId;
    }
    #endregion
}
