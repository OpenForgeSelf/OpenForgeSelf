using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_WorkflowUsageRecord_WorkflowId", false, "WorkflowId")]
[BindIndex("IX_WorkflowUsageRecord_ExecutionId", false, "ExecutionId")]
[BindIndex("IX_WorkflowUsageRecord_StartTime", false, "StartTime")]
[BindIndex("IX_WorkflowUsageRecord_Status", false, "Status")]
[BindIndex("IX_WorkflowUsageRecord_WorkflowId_StartTime", false, "WorkflowId,StartTime")]
[BindIndex("IX_WorkflowUsageRecord_Status_StartTime", false, "Status,StartTime")]
[BindTable("WorkflowUsageRecord", Description = "工作流使用记录", ConnName = "OpenForgeSelf", DbType = DatabaseType.None)]
public partial class WorkflowUsageRecord : IWorkflowUsageRecord, IEntity<IWorkflowUsageRecord>
{
    #region 属性
    private Int64 _Id;
    /// <summary>记录ID</summary>
    [DisplayName("记录ID")]
    [Description("记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _WorkflowId;
    /// <summary>工作流ID</summary>
    [DisplayName("工作流ID")]
    [Description("工作流ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("WorkflowId", "工作流ID", "")]
    public Int64 WorkflowId { get => _WorkflowId; set { if (OnPropertyChanging("WorkflowId", value)) { _WorkflowId = value; OnPropertyChanged("WorkflowId"); } } }

    private String _WorkflowName;
    /// <summary>工作流名称</summary>
    [DisplayName("工作流名称")]
    [Description("工作流名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("WorkflowName", "工作流名称", "")]
    public String WorkflowName { get => _WorkflowName; set { if (OnPropertyChanging("WorkflowName", value)) { _WorkflowName = value; OnPropertyChanged("WorkflowName"); } } }

    private Int64 _ExecutionId;
    /// <summary>执行ID</summary>
    [DisplayName("执行ID")]
    [Description("执行ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExecutionId", "执行ID", "")]
    public Int64 ExecutionId { get => _ExecutionId; set { if (OnPropertyChanging("ExecutionId", value)) { _ExecutionId = value; OnPropertyChanged("ExecutionId"); } } }

    private DateTime _StartTime;
    /// <summary>开始时间</summary>
    [DisplayName("开始时间")]
    [Description("开始时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartTime", "开始时间", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private DateTime _EndTime;
    /// <summary>结束时间</summary>
    [DisplayName("结束时间")]
    [Description("结束时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("EndTime", "结束时间", "")]
    public DateTime EndTime { get => _EndTime; set { if (OnPropertyChanging("EndTime", value)) { _EndTime = value; OnPropertyChanged("EndTime"); } } }

    private Int32 _Status;
    /// <summary>状态（0成功 1失败 2取消）</summary>
    [DisplayName("状态（0成功1失败2取消）")]
    [Description("状态（0成功 1失败 2取消）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态（0成功 1失败 2取消）", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Double _DurationSeconds;
    /// <summary>持续时间（秒）</summary>
    [DisplayName("持续时间（秒）")]
    [Description("持续时间（秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationSeconds", "持续时间（秒）", "")]
    public Double DurationSeconds { get => _DurationSeconds; set { if (OnPropertyChanging("DurationSeconds", value)) { _DurationSeconds = value; OnPropertyChanged("DurationSeconds"); } } }

    private String _InputVariablesJson;
    /// <summary>输入变量JSON</summary>
    [DisplayName("输入变量JSON")]
    [Description("输入变量JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("InputVariablesJson", "输入变量JSON", "")]
    public String InputVariablesJson { get => _InputVariablesJson; set { if (OnPropertyChanging("InputVariablesJson", value)) { _InputVariablesJson = value; OnPropertyChanged("InputVariablesJson"); } } }

    private String _OutputResultJson;
    /// <summary>输出结果JSON</summary>
    [DisplayName("输出结果JSON")]
    [Description("输出结果JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("OutputResultJson", "输出结果JSON", "")]
    public String OutputResultJson { get => _OutputResultJson; set { if (OnPropertyChanging("OutputResultJson", value)) { _OutputResultJson = value; OnPropertyChanged("OutputResultJson"); } } }

    private Int32 _ToolCallCount;
    /// <summary>工具调用次数</summary>
    [DisplayName("工具调用次数")]
    [Description("工具调用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ToolCallCount", "工具调用次数", "")]
    public Int32 ToolCallCount { get => _ToolCallCount; set { if (OnPropertyChanging("ToolCallCount", value)) { _ToolCallCount = value; OnPropertyChanged("ToolCallCount"); } } }

    private Int32 _StepCount;
    /// <summary>步骤数</summary>
    [DisplayName("步骤数")]
    [Description("步骤数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StepCount", "步骤数", "")]
    public Int32 StepCount { get => _StepCount; set { if (OnPropertyChanging("StepCount", value)) { _StepCount = value; OnPropertyChanged("StepCount"); } } }

    private String _TriggeredBy;
    /// <summary>触发者</summary>
    [DisplayName("触发者")]
    [Description("触发者")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("TriggeredBy", "触发者", "")]
    public String TriggeredBy { get => _TriggeredBy; set { if (OnPropertyChanging("TriggeredBy", value)) { _TriggeredBy = value; OnPropertyChanged("TriggeredBy"); } } }

    private String _IpAddress;
    /// <summary>IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IpAddress", "IP地址", "")]
    public String IpAddress { get => _IpAddress; set { if (OnPropertyChanging("IpAddress", value)) { _IpAddress = value; OnPropertyChanged("IpAddress"); } } }

    private String _ErrorMessage;
    /// <summary>错误信息</summary>
    [DisplayName("错误信息")]
    [Description("错误信息")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ErrorMessage", "错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IWorkflowUsageRecord model)
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

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "WorkflowId" => _WorkflowId,
            "WorkflowName" => _WorkflowName,
            "ExecutionId" => _ExecutionId,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "Status" => _Status,
            "DurationSeconds" => _DurationSeconds,
            "InputVariablesJson" => _InputVariablesJson,
            "OutputResultJson" => _OutputResultJson,
            "ToolCallCount" => _ToolCallCount,
            "StepCount" => _StepCount,
            "TriggeredBy" => _TriggeredBy,
            "IpAddress" => _IpAddress,
            "ErrorMessage" => _ErrorMessage,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "WorkflowId": _WorkflowId = value.ToLong(); break;
                case "WorkflowName": _WorkflowName = Convert.ToString(value); break;
                case "ExecutionId": _ExecutionId = value.ToLong(); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "Status": _Status = value.ToInt(); break;
                case "DurationSeconds": _DurationSeconds = value.ToDouble(); break;
                case "InputVariablesJson": _InputVariablesJson = Convert.ToString(value); break;
                case "OutputResultJson": _OutputResultJson = Convert.ToString(value); break;
                case "ToolCallCount": _ToolCallCount = value.ToInt(); break;
                case "StepCount": _StepCount = value.ToInt(); break;
                case "TriggeredBy": _TriggeredBy = Convert.ToString(value); break;
                case "IpAddress": _IpAddress = Convert.ToString(value); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据记录ID查找</summary>
    /// <param name="id">记录ID</param>
    /// <returns>实体对象</returns>
    public static WorkflowUsageRecord FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据工作流ID查找</summary>
    /// <param name="workflowId">工作流ID</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowUsageRecord> FindAllByWorkflowId(Int64 workflowId)
    {
        if (workflowId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.WorkflowId == workflowId);

        return FindAll(_.WorkflowId == workflowId);
    }

    /// <summary>根据执行ID查找</summary>
    /// <param name="executionId">执行ID</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowUsageRecord> FindAllByExecutionId(Int64 executionId)
    {
        if (executionId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ExecutionId == executionId);

        return FindAll(_.ExecutionId == executionId);
    }

    /// <summary>根据状态（0成功1失败2取消）查找</summary>
    /// <param name="status">状态（0成功1失败2取消）</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowUsageRecord> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="executionId">执行ID</param>
    /// <param name="status">状态（0成功 1失败 2取消）</param>
    /// <param name="start">开始时间开始</param>
    /// <param name="end">开始时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowUsageRecord> Search(Int64 workflowId, Int64 executionId, Int32 status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (workflowId >= 0) exp &= _.WorkflowId == workflowId;
        if (executionId >= 0) exp &= _.ExecutionId == executionId;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.StartTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得工作流使用记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>工作流ID</summary>
        public static readonly Field WorkflowId = FindByName("WorkflowId");

        /// <summary>工作流名称</summary>
        public static readonly Field WorkflowName = FindByName("WorkflowName");

        /// <summary>执行ID</summary>
        public static readonly Field ExecutionId = FindByName("ExecutionId");

        /// <summary>开始时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>状态（0成功 1失败 2取消）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>持续时间（秒）</summary>
        public static readonly Field DurationSeconds = FindByName("DurationSeconds");

        /// <summary>输入变量JSON</summary>
        public static readonly Field InputVariablesJson = FindByName("InputVariablesJson");

        /// <summary>输出结果JSON</summary>
        public static readonly Field OutputResultJson = FindByName("OutputResultJson");

        /// <summary>工具调用次数</summary>
        public static readonly Field ToolCallCount = FindByName("ToolCallCount");

        /// <summary>步骤数</summary>
        public static readonly Field StepCount = FindByName("StepCount");

        /// <summary>触发者</summary>
        public static readonly Field TriggeredBy = FindByName("TriggeredBy");

        /// <summary>IP地址</summary>
        public static readonly Field IpAddress = FindByName("IpAddress");

        /// <summary>错误信息</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得工作流使用记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>记录ID</summary>
        public const String Id = "Id";

        /// <summary>工作流ID</summary>
        public const String WorkflowId = "WorkflowId";

        /// <summary>工作流名称</summary>
        public const String WorkflowName = "WorkflowName";

        /// <summary>执行ID</summary>
        public const String ExecutionId = "ExecutionId";

        /// <summary>开始时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>状态（0成功 1失败 2取消）</summary>
        public const String Status = "Status";

        /// <summary>持续时间（秒）</summary>
        public const String DurationSeconds = "DurationSeconds";

        /// <summary>输入变量JSON</summary>
        public const String InputVariablesJson = "InputVariablesJson";

        /// <summary>输出结果JSON</summary>
        public const String OutputResultJson = "OutputResultJson";

        /// <summary>工具调用次数</summary>
        public const String ToolCallCount = "ToolCallCount";

        /// <summary>步骤数</summary>
        public const String StepCount = "StepCount";

        /// <summary>触发者</summary>
        public const String TriggeredBy = "TriggeredBy";

        /// <summary>IP地址</summary>
        public const String IpAddress = "IpAddress";

        /// <summary>错误信息</summary>
        public const String ErrorMessage = "ErrorMessage";
    }
    #endregion
}
