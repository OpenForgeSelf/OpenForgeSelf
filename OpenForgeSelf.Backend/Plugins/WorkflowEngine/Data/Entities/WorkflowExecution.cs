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

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_WorkflowExecution_WorkflowId", false, "WorkflowId")]
[BindIndex("IX_WorkflowExecution_Status", false, "Status")]
[BindIndex("IX_WorkflowExecution_StartTime", false, "StartTime")]
[BindTable("WorkflowExecution", Description = "工作流执行记录", ConnName = "WorkflowEngine", DbType = DatabaseType.None)]
public partial class WorkflowExecution
{
    #region 属性
    private Int64 _Id;
    /// <summary>执行ID</summary>
    [DisplayName("执行ID")]
    [Description("执行ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "执行ID", "")]
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
    [DataObjectField(false, false, true, 200)]
    [BindColumn("WorkflowName", "工作流名称", "")]
    public String WorkflowName { get => _WorkflowName; set { if (OnPropertyChanging("WorkflowName", value)) { _WorkflowName = value; OnPropertyChanged("WorkflowName"); } } }

    private Int32 _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

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

    private String _CurrentStepId;
    /// <summary>当前步骤ID</summary>
    [DisplayName("当前步骤ID")]
    [Description("当前步骤ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CurrentStepId", "当前步骤ID", "")]
    public String CurrentStepId { get => _CurrentStepId; set { if (OnPropertyChanging("CurrentStepId", value)) { _CurrentStepId = value; OnPropertyChanged("CurrentStepId"); } } }

    private String _LogsJson;
    /// <summary>日志JSON</summary>
    [DisplayName("日志JSON")]
    [Description("日志JSON")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("LogsJson", "日志JSON", "")]
    public String LogsJson { get => _LogsJson; set { if (OnPropertyChanging("LogsJson", value)) { _LogsJson = value; OnPropertyChanged("LogsJson"); } } }

    private String _ResultsJson;
    /// <summary>结果JSON</summary>
    [DisplayName("结果JSON")]
    [Description("结果JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ResultsJson", "结果JSON", "")]
    public String ResultsJson { get => _ResultsJson; set { if (OnPropertyChanging("ResultsJson", value)) { _ResultsJson = value; OnPropertyChanged("ResultsJson"); } } }

    private String _ErrorMessage;
    /// <summary>错误信息</summary>
    [DisplayName("错误信息")]
    [Description("错误信息")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ErrorMessage", "错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

    private String _VariablesJson;
    /// <summary>变量JSON</summary>
    [DisplayName("变量JSON")]
    [Description("变量JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("VariablesJson", "变量JSON", "")]
    public String VariablesJson { get => _VariablesJson; set { if (OnPropertyChanging("VariablesJson", value)) { _VariablesJson = value; OnPropertyChanged("VariablesJson"); } } }

    private String _StepResultsJson;
    /// <summary>步骤结果JSON</summary>
    [DisplayName("步骤结果JSON")]
    [Description("步骤结果JSON")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("StepResultsJson", "步骤结果JSON", "")]
    public String StepResultsJson { get => _StepResultsJson; set { if (OnPropertyChanging("StepResultsJson", value)) { _StepResultsJson = value; OnPropertyChanged("StepResultsJson"); } } }

    private Double _Progress;
    /// <summary>进度</summary>
    [DisplayName("进度")]
    [Description("进度")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Progress", "进度", "")]
    public Double Progress { get => _Progress; set { if (OnPropertyChanging("Progress", value)) { _Progress = value; OnPropertyChanged("Progress"); } } }

    private String _TriggeredBy;
    /// <summary>触发者</summary>
    [DisplayName("触发者")]
    [Description("触发者")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TriggeredBy", "触发者", "")]
    public String TriggeredBy { get => _TriggeredBy; set { if (OnPropertyChanging("TriggeredBy", value)) { _TriggeredBy = value; OnPropertyChanged("TriggeredBy"); } } }
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
            "Status" => _Status,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "CurrentStepId" => _CurrentStepId,
            "LogsJson" => _LogsJson,
            "ResultsJson" => _ResultsJson,
            "ErrorMessage" => _ErrorMessage,
            "VariablesJson" => _VariablesJson,
            "StepResultsJson" => _StepResultsJson,
            "Progress" => _Progress,
            "TriggeredBy" => _TriggeredBy,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "WorkflowId": _WorkflowId = value.ToLong(); break;
                case "WorkflowName": _WorkflowName = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "CurrentStepId": _CurrentStepId = Convert.ToString(value); break;
                case "LogsJson": _LogsJson = Convert.ToString(value); break;
                case "ResultsJson": _ResultsJson = Convert.ToString(value); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                case "VariablesJson": _VariablesJson = Convert.ToString(value); break;
                case "StepResultsJson": _StepResultsJson = Convert.ToString(value); break;
                case "Progress": _Progress = value.ToDouble(); break;
                case "TriggeredBy": _TriggeredBy = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据执行ID查找</summary>
    /// <param name="id">执行ID</param>
    /// <returns>实体对象</returns>
    public static WorkflowExecution FindById(Int64 id)
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
    public static IList<WorkflowExecution> FindAllByWorkflowId(Int64 workflowId)
    {
        if (workflowId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.WorkflowId == workflowId);

        return FindAll(_.WorkflowId == workflowId);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowExecution> FindAllByStatus(Int32 status)
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
    /// <param name="status">状态</param>
    /// <param name="start">开始时间开始</param>
    /// <param name="end">开始时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowExecution> Search(Int64 workflowId, Int32 status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (workflowId >= 0) exp &= _.WorkflowId == workflowId;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.StartTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得工作流执行记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>执行ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>工作流ID</summary>
        public static readonly Field WorkflowId = FindByName("WorkflowId");

        /// <summary>工作流名称</summary>
        public static readonly Field WorkflowName = FindByName("WorkflowName");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>开始时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>当前步骤ID</summary>
        public static readonly Field CurrentStepId = FindByName("CurrentStepId");

        /// <summary>日志JSON</summary>
        public static readonly Field LogsJson = FindByName("LogsJson");

        /// <summary>结果JSON</summary>
        public static readonly Field ResultsJson = FindByName("ResultsJson");

        /// <summary>错误信息</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        /// <summary>变量JSON</summary>
        public static readonly Field VariablesJson = FindByName("VariablesJson");

        /// <summary>步骤结果JSON</summary>
        public static readonly Field StepResultsJson = FindByName("StepResultsJson");

        /// <summary>进度</summary>
        public static readonly Field Progress = FindByName("Progress");

        /// <summary>触发者</summary>
        public static readonly Field TriggeredBy = FindByName("TriggeredBy");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得工作流执行记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>执行ID</summary>
        public const String Id = "Id";

        /// <summary>工作流ID</summary>
        public const String WorkflowId = "WorkflowId";

        /// <summary>工作流名称</summary>
        public const String WorkflowName = "WorkflowName";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>开始时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>当前步骤ID</summary>
        public const String CurrentStepId = "CurrentStepId";

        /// <summary>日志JSON</summary>
        public const String LogsJson = "LogsJson";

        /// <summary>结果JSON</summary>
        public const String ResultsJson = "ResultsJson";

        /// <summary>错误信息</summary>
        public const String ErrorMessage = "ErrorMessage";

        /// <summary>变量JSON</summary>
        public const String VariablesJson = "VariablesJson";

        /// <summary>步骤结果JSON</summary>
        public const String StepResultsJson = "StepResultsJson";

        /// <summary>进度</summary>
        public const String Progress = "Progress";

        /// <summary>触发者</summary>
        public const String TriggeredBy = "TriggeredBy";
    }
    #endregion
}
