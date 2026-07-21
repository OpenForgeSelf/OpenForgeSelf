using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class WorkflowUsageRecordModel
{
    #region 属性
    /// <summary>记录ID</summary>
    public Int64 Id { get; set; }

    /// <summary>工作流ID</summary>
    public Int64 WorkflowId { get; set; }

    /// <summary>工作流名称</summary>
    public String WorkflowName { get; set; }

    /// <summary>执行ID</summary>
    public Int64 ExecutionId { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; set; }

    /// <summary>状态（0成功 1失败 2取消）</summary>
    public Int32 Status { get; set; }

    /// <summary>持续时间（秒）</summary>
    public Double DurationSeconds { get; set; }

    /// <summary>输入变量JSON</summary>
    public String InputVariablesJson { get; set; }

    /// <summary>输出结果JSON</summary>
    public String OutputResultJson { get; set; }

    /// <summary>工具调用次数</summary>
    public Int32 ToolCallCount { get; set; }

    /// <summary>步骤数</summary>
    public Int32 StepCount { get; set; }

    /// <summary>触发者</summary>
    public String TriggeredBy { get; set; }

    /// <summary>IP地址</summary>
    public String IpAddress { get; set; }

    /// <summary>错误信息</summary>
    public String ErrorMessage { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IWorkflowUsageRecordModel model)
    {
        Id = model.Id;
        WorkflowId = model.WorkflowId;
        WorkflowName = model.WorkflowName;
        ExecutionId = model.ExecutionId;
        StartTime = model.StartTime;
        EndTime = model.EndTime;
        Status = model.Status;
        DurationSeconds = model.DurationSeconds;
        InputVariablesJson = model.InputVariablesJson;
        OutputResultJson = model.OutputResultJson;
        ToolCallCount = model.ToolCallCount;
        StepCount = model.StepCount;
        TriggeredBy = model.TriggeredBy;
        IpAddress = model.IpAddress;
        ErrorMessage = model.ErrorMessage;
    }
    #endregion
}
