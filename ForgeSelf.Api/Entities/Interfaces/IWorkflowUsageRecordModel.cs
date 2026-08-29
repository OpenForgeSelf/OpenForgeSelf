using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
public partial interface IWorkflowUsageRecordModel
{
    #region 属性
    /// <summary>记录ID</summary>
    Int64 Id { get; set; }

    /// <summary>工作流ID</summary>
    Int64 WorkflowId { get; set; }

    /// <summary>工作流名称</summary>
    String WorkflowName { get; set; }

    /// <summary>执行ID</summary>
    Int64 ExecutionId { get; set; }

    /// <summary>开始时间</summary>
    DateTime StartTime { get; set; }

    /// <summary>结束时间</summary>
    DateTime EndTime { get; set; }

    /// <summary>状态（0成功 1失败 2取消）</summary>
    Int32 Status { get; set; }

    /// <summary>持续时间（秒）</summary>
    Double DurationSeconds { get; set; }

    /// <summary>输入变量JSON</summary>
    String InputVariablesJson { get; set; }

    /// <summary>输出结果JSON</summary>
    String OutputResultJson { get; set; }

    /// <summary>工具调用次数</summary>
    Int32 ToolCallCount { get; set; }

    /// <summary>步骤数</summary>
    Int32 StepCount { get; set; }

    /// <summary>触发者</summary>
    String TriggeredBy { get; set; }

    /// <summary>IP地址</summary>
    String IpAddress { get; set; }

    /// <summary>错误信息</summary>
    String ErrorMessage { get; set; }
    #endregion
}
