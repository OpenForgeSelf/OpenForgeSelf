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

namespace ForgeSelf.Api.Plugins.AIAgent.Entities;

/// <summary>{name}。Agent 计划驱动执行实例</summary>
[Serializable]
[DataObject]
[Description("{name}。Agent 计划驱动执行实例")]
[BindIndex("IX_AgentRun_Status", false, "Status")]
[BindIndex("IX_AgentRun_SessionId_Status", false, "SessionId,Status")]
[BindIndex("IX_AgentRun_CreateTime", false, "CreateTime")]
[BindTable("AgentRun", Description = "Agent 计划驱动执行实例", ConnName = "AIAgent", DbType = DatabaseType.None)]
public partial class AgentRun
{
    #region 属性
    private Int64 _Id;
    /// <summary>执行实例ID</summary>
    [DisplayName("执行实例ID")]
    [Description("执行实例ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "执行实例ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _AgentId;
    /// <summary>所属Agent ID</summary>
    [DisplayName("所属AgentID")]
    [Description("所属Agent ID")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("AgentId", "所属Agent ID", "")]
    public String AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private String _AgentName;
    /// <summary>Agent名称（冗余）</summary>
    [DisplayName("Agent名称（冗余）")]
    [Description("Agent名称（冗余）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("AgentName", "Agent名称（冗余）", "")]
    public String AgentName { get => _AgentName; set { if (OnPropertyChanging("AgentName", value)) { _AgentName = value; OnPropertyChanged("AgentName"); } } }

    private String _SessionId;
    /// <summary>关联会话ID</summary>
    [DisplayName("关联会话ID")]
    [Description("关联会话ID")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionId", "关联会话ID", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private Int64 _WorkflowId;
    /// <summary>关联工作流ID（0/空=无，自生成Plan）</summary>
    [DisplayName("关联工作流ID（0_空=无")]
    [Description("关联工作流ID（0/空=无，自生成Plan）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("WorkflowId", "关联工作流ID（0/空=无，自生成Plan）", "")]
    public Int64 WorkflowId { get => _WorkflowId; set { if (OnPropertyChanging("WorkflowId", value)) { _WorkflowId = value; OnPropertyChanged("WorkflowId"); } } }

    private String _WorkflowName;
    /// <summary>工作流名称（冗余）</summary>
    [DisplayName("工作流名称（冗余）")]
    [Description("工作流名称（冗余）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("WorkflowName", "工作流名称（冗余）", "")]
    public String WorkflowName { get => _WorkflowName; set { if (OnPropertyChanging("WorkflowName", value)) { _WorkflowName = value; OnPropertyChanged("WorkflowName"); } } }

    private String _TaskInput;
    /// <summary>任务原文</summary>
    [DisplayName("任务原文")]
    [Description("任务原文")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("TaskInput", "任务原文", "")]
    public String TaskInput { get => _TaskInput; set { if (OnPropertyChanging("TaskInput", value)) { _TaskInput = value; OnPropertyChanged("TaskInput"); } } }

    private String _PlanJson;
    /// <summary>生成的执行计划（Plan DSL）</summary>
    [DisplayName("生成的执行计划（PlanDSL）")]
    [Description("生成的执行计划（Plan DSL）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("PlanJson", "生成的执行计划（Plan DSL）", "")]
    public String PlanJson { get => _PlanJson; set { if (OnPropertyChanging("PlanJson", value)) { _PlanJson = value; OnPropertyChanged("PlanJson"); } } }

    private Int32 _Status;
    /// <summary>执行状态（AgentRunStatus枚举）</summary>
    [DisplayName("执行状态（AgentRunStatus枚举）")]
    [Description("执行状态（AgentRunStatus枚举）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "执行状态（AgentRunStatus枚举）", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _CurrentStepIndex;
    /// <summary>当前/中断步骤下标</summary>
    [DisplayName("当前_中断步骤下标")]
    [Description("当前/中断步骤下标")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CurrentStepIndex", "当前/中断步骤下标", "")]
    public Int32 CurrentStepIndex { get => _CurrentStepIndex; set { if (OnPropertyChanging("CurrentStepIndex", value)) { _CurrentStepIndex = value; OnPropertyChanged("CurrentStepIndex"); } } }

    private String _StuckReason;
    /// <summary>卡住原因</summary>
    [DisplayName("卡住原因")]
    [Description("卡住原因")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("StuckReason", "卡住原因", "")]
    public String StuckReason { get => _StuckReason; set { if (OnPropertyChanging("StuckReason", value)) { _StuckReason = value; OnPropertyChanged("StuckReason"); } } }

    private Int32 _StepCount;
    /// <summary>步骤总数</summary>
    [DisplayName("步骤总数")]
    [Description("步骤总数")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StepCount", "步骤总数", "")]
    public Int32 StepCount { get => _StepCount; set { if (OnPropertyChanging("StepCount", value)) { _StepCount = value; OnPropertyChanged("StepCount"); } } }

    private Int64 _TotalTokens;
    /// <summary>全流程Token汇总</summary>
    [DisplayName("全流程Token汇总")]
    [Description("全流程Token汇总")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("TotalTokens", "全流程Token汇总", "")]
    public Int64 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
            "AgentId" => _AgentId,
            "AgentName" => _AgentName,
            "SessionId" => _SessionId,
            "WorkflowId" => _WorkflowId,
            "WorkflowName" => _WorkflowName,
            "TaskInput" => _TaskInput,
            "PlanJson" => _PlanJson,
            "Status" => _Status,
            "CurrentStepIndex" => _CurrentStepIndex,
            "StuckReason" => _StuckReason,
            "StepCount" => _StepCount,
            "TotalTokens" => _TotalTokens,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "AgentId": _AgentId = Convert.ToString(value); break;
                case "AgentName": _AgentName = Convert.ToString(value); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "WorkflowId": _WorkflowId = value.ToLong(); break;
                case "WorkflowName": _WorkflowName = Convert.ToString(value); break;
                case "TaskInput": _TaskInput = Convert.ToString(value); break;
                case "PlanJson": _PlanJson = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "CurrentStepIndex": _CurrentStepIndex = value.ToInt(); break;
                case "StuckReason": _StuckReason = Convert.ToString(value); break;
                case "StepCount": _StepCount = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToLong(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据执行实例ID查找</summary>
    /// <param name="id">执行实例ID</param>
    /// <returns>实体对象</returns>
    public static AgentRun FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据执行状态（AgentRunStatus枚举）查找</summary>
    /// <param name="status">执行状态（AgentRunStatus枚举）</param>
    /// <returns>实体列表</returns>
    public static IList<AgentRun> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }

    /// <summary>根据关联会话ID、执行状态（AgentRunStatus枚举）查找</summary>
    /// <param name="sessionId">关联会话ID</param>
    /// <param name="status">执行状态（AgentRunStatus枚举）</param>
    /// <returns>实体列表</returns>
    public static IList<AgentRun> FindAllBySessionIdAndStatus(String sessionId, Int32 status)
    {
        if (sessionId.IsNullOrEmpty()) return [];
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SessionId.EqualIgnoreCase(sessionId) && e.Status == status);

        return FindAll(_.SessionId == sessionId & _.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="sessionId">关联会话ID</param>
    /// <param name="status">执行状态（AgentRunStatus枚举）</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AgentRun> Search(String sessionId, Int32 status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得Agent计划驱动执行实例字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>执行实例ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>所属Agent ID</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>Agent名称（冗余）</summary>
        public static readonly Field AgentName = FindByName("AgentName");

        /// <summary>关联会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>关联工作流ID（0/空=无，自生成Plan）</summary>
        public static readonly Field WorkflowId = FindByName("WorkflowId");

        /// <summary>工作流名称（冗余）</summary>
        public static readonly Field WorkflowName = FindByName("WorkflowName");

        /// <summary>任务原文</summary>
        public static readonly Field TaskInput = FindByName("TaskInput");

        /// <summary>生成的执行计划（Plan DSL）</summary>
        public static readonly Field PlanJson = FindByName("PlanJson");

        /// <summary>执行状态（AgentRunStatus枚举）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>当前/中断步骤下标</summary>
        public static readonly Field CurrentStepIndex = FindByName("CurrentStepIndex");

        /// <summary>卡住原因</summary>
        public static readonly Field StuckReason = FindByName("StuckReason");

        /// <summary>步骤总数</summary>
        public static readonly Field StepCount = FindByName("StepCount");

        /// <summary>全流程Token汇总</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得Agent计划驱动执行实例字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>执行实例ID</summary>
        public const String Id = "Id";

        /// <summary>所属Agent ID</summary>
        public const String AgentId = "AgentId";

        /// <summary>Agent名称（冗余）</summary>
        public const String AgentName = "AgentName";

        /// <summary>关联会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>关联工作流ID（0/空=无，自生成Plan）</summary>
        public const String WorkflowId = "WorkflowId";

        /// <summary>工作流名称（冗余）</summary>
        public const String WorkflowName = "WorkflowName";

        /// <summary>任务原文</summary>
        public const String TaskInput = "TaskInput";

        /// <summary>生成的执行计划（Plan DSL）</summary>
        public const String PlanJson = "PlanJson";

        /// <summary>执行状态（AgentRunStatus枚举）</summary>
        public const String Status = "Status";

        /// <summary>当前/中断步骤下标</summary>
        public const String CurrentStepIndex = "CurrentStepIndex";

        /// <summary>卡住原因</summary>
        public const String StuckReason = "StuckReason";

        /// <summary>步骤总数</summary>
        public const String StepCount = "StepCount";

        /// <summary>全流程Token汇总</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
