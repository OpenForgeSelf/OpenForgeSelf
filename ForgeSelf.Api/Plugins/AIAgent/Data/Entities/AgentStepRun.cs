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

/// <summary>{name}。Agent 计划驱动执行步骤记录</summary>
[Serializable]
[DataObject]
[Description("{name}。Agent 计划驱动执行步骤记录")]
[BindIndex("IX_AgentStepRun_RunId", false, "RunId")]
[BindIndex("IX_AgentStepRun_RunId_StepIndex", false, "RunId,StepIndex")]
[BindTable("AgentStepRun", Description = "Agent 计划驱动执行步骤记录", ConnName = "AIAgent", DbType = DatabaseType.None)]
public partial class AgentStepRun
{
    #region 属性
    private Int64 _Id;
    /// <summary>步骤记录ID</summary>
    [DisplayName("步骤记录ID")]
    [Description("步骤记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "步骤记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _RunId;
    /// <summary>关联执行实例ID</summary>
    [DisplayName("关联执行实例ID")]
    [Description("关联执行实例ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RunId", "关联执行实例ID", "", Master = true)]
    public Int64 RunId { get => _RunId; set { if (OnPropertyChanging("RunId", value)) { _RunId = value; OnPropertyChanged("RunId"); } } }

    private Int32 _StepIndex;
    /// <summary>步骤下标（0-based）</summary>
    [DisplayName("步骤下标（0-based）")]
    [Description("步骤下标（0-based）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StepIndex", "步骤下标（0-based）", "", Master = true)]
    public Int32 StepIndex { get => _StepIndex; set { if (OnPropertyChanging("StepIndex", value)) { _StepIndex = value; OnPropertyChanged("StepIndex"); } } }

    private String _StepId;
    /// <summary>Plan步骤ID（如s1）</summary>
    [DisplayName("Plan步骤ID（如s1）")]
    [Description("Plan步骤ID（如s1）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("StepId", "Plan步骤ID（如s1）", "")]
    public String StepId { get => _StepId; set { if (OnPropertyChanging("StepId", value)) { _StepId = value; OnPropertyChanged("StepId"); } } }

    private String _Name;
    /// <summary>步骤名</summary>
    [DisplayName("步骤名")]
    [Description("步骤名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Name", "步骤名", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Objective;
    /// <summary>步骤目标</summary>
    [DisplayName("步骤目标")]
    [Description("步骤目标")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Objective", "步骤目标", "")]
    public String Objective { get => _Objective; set { if (OnPropertyChanging("Objective", value)) { _Objective = value; OnPropertyChanged("Objective"); } } }

    private Int32 _Status;
    /// <summary>步骤状态（AgentStepStatus枚举）</summary>
    [DisplayName("步骤状态（AgentStepStatus枚举）")]
    [Description("步骤状态（AgentStepStatus枚举）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "步骤状态（AgentStepStatus枚举）", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _InputJson;
    /// <summary>组装给LLM的上下文摘要（入参）</summary>
    [DisplayName("组装给LLM的上下文摘要（入参）")]
    [Description("组装给LLM的上下文摘要（入参）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("InputJson", "组装给LLM的上下文摘要（入参）", "")]
    public String InputJson { get => _InputJson; set { if (OnPropertyChanging("InputJson", value)) { _InputJson = value; OnPropertyChanged("InputJson"); } } }

    private String _OutputJson;
    /// <summary>complete_step声明的产出（出参）</summary>
    [DisplayName("complete_step声明的产出（出参）")]
    [Description("complete_step声明的产出（出参）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("OutputJson", "complete_step声明的产出（出参）", "")]
    public String OutputJson { get => _OutputJson; set { if (OnPropertyChanging("OutputJson", value)) { _OutputJson = value; OnPropertyChanged("OutputJson"); } } }

    private String _ToolCallsJson;
    /// <summary>本步工具调用轨迹</summary>
    [DisplayName("本步工具调用轨迹")]
    [Description("本步工具调用轨迹")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ToolCallsJson", "本步工具调用轨迹", "")]
    public String ToolCallsJson { get => _ToolCallsJson; set { if (OnPropertyChanging("ToolCallsJson", value)) { _ToolCallsJson = value; OnPropertyChanged("ToolCallsJson"); } } }

    private String _ErrorMessage;
    /// <summary>失败错误</summary>
    [DisplayName("失败错误")]
    [Description("失败错误")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("ErrorMessage", "失败错误", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

    private String _StuckReason;
    /// <summary>卡住原因</summary>
    [DisplayName("卡住原因")]
    [Description("卡住原因")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("StuckReason", "卡住原因", "")]
    public String StuckReason { get => _StuckReason; set { if (OnPropertyChanging("StuckReason", value)) { _StuckReason = value; OnPropertyChanged("StuckReason"); } } }

    private String _HumanNote;
    /// <summary>人工批注</summary>
    [DisplayName("人工批注")]
    [Description("人工批注")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("HumanNote", "人工批注", "")]
    public String HumanNote { get => _HumanNote; set { if (OnPropertyChanging("HumanNote", value)) { _HumanNote = value; OnPropertyChanged("HumanNote"); } } }

    private String _HumanOverride;
    /// <summary>人工补位产出</summary>
    [DisplayName("人工补位产出")]
    [Description("人工补位产出")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("HumanOverride", "人工补位产出", "")]
    public String HumanOverride { get => _HumanOverride; set { if (OnPropertyChanging("HumanOverride", value)) { _HumanOverride = value; OnPropertyChanged("HumanOverride"); } } }

    private Int32 _RetryCount;
    /// <summary>重试次数</summary>
    [DisplayName("重试次数")]
    [Description("重试次数")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RetryCount", "重试次数", "")]
    public Int32 RetryCount { get => _RetryCount; set { if (OnPropertyChanging("RetryCount", value)) { _RetryCount = value; OnPropertyChanged("RetryCount"); } } }

    private Int64 _TokensUsed;
    /// <summary>本步Token</summary>
    [DisplayName("本步Token")]
    [Description("本步Token")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("TokensUsed", "本步Token", "")]
    public Int64 TokensUsed { get => _TokensUsed; set { if (OnPropertyChanging("TokensUsed", value)) { _TokensUsed = value; OnPropertyChanged("TokensUsed"); } } }

    private Int64 _DurationMs;
    /// <summary>耗时（毫秒）</summary>
    [DisplayName("耗时（毫秒）")]
    [Description("耗时（毫秒）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DurationMs", "耗时（毫秒）", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private DateTime _StartedAt;
    /// <summary>开始时间</summary>
    [DisplayName("开始时间")]
    [Description("开始时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartedAt", "开始时间", "")]
    public DateTime StartedAt { get => _StartedAt; set { if (OnPropertyChanging("StartedAt", value)) { _StartedAt = value; OnPropertyChanged("StartedAt"); } } }

    private DateTime _CompletedAt;
    /// <summary>完成时间</summary>
    [DisplayName("完成时间")]
    [Description("完成时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CompletedAt", "完成时间", "")]
    public DateTime CompletedAt { get => _CompletedAt; set { if (OnPropertyChanging("CompletedAt", value)) { _CompletedAt = value; OnPropertyChanged("CompletedAt"); } } }
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
            "RunId" => _RunId,
            "StepIndex" => _StepIndex,
            "StepId" => _StepId,
            "Name" => _Name,
            "Objective" => _Objective,
            "Status" => _Status,
            "InputJson" => _InputJson,
            "OutputJson" => _OutputJson,
            "ToolCallsJson" => _ToolCallsJson,
            "ErrorMessage" => _ErrorMessage,
            "StuckReason" => _StuckReason,
            "HumanNote" => _HumanNote,
            "HumanOverride" => _HumanOverride,
            "RetryCount" => _RetryCount,
            "TokensUsed" => _TokensUsed,
            "DurationMs" => _DurationMs,
            "StartedAt" => _StartedAt,
            "CompletedAt" => _CompletedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RunId": _RunId = value.ToLong(); break;
                case "StepIndex": _StepIndex = value.ToInt(); break;
                case "StepId": _StepId = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Objective": _Objective = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "InputJson": _InputJson = Convert.ToString(value); break;
                case "OutputJson": _OutputJson = Convert.ToString(value); break;
                case "ToolCallsJson": _ToolCallsJson = Convert.ToString(value); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                case "StuckReason": _StuckReason = Convert.ToString(value); break;
                case "HumanNote": _HumanNote = Convert.ToString(value); break;
                case "HumanOverride": _HumanOverride = Convert.ToString(value); break;
                case "RetryCount": _RetryCount = value.ToInt(); break;
                case "TokensUsed": _TokensUsed = value.ToLong(); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "StartedAt": _StartedAt = value.ToDateTime(); break;
                case "CompletedAt": _CompletedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据步骤记录ID查找</summary>
    /// <param name="id">步骤记录ID</param>
    /// <returns>实体对象</returns>
    public static AgentStepRun FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据关联执行实例ID查找</summary>
    /// <param name="runId">关联执行实例ID</param>
    /// <returns>实体列表</returns>
    public static IList<AgentStepRun> FindAllByRunId(Int64 runId)
    {
        if (runId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.RunId == runId);

        return FindAll(_.RunId == runId);
    }

    /// <summary>根据关联执行实例ID、步骤下标（0-based）查找</summary>
    /// <param name="runId">关联执行实例ID</param>
    /// <param name="stepIndex">步骤下标（0-based）</param>
    /// <returns>实体列表</returns>
    public static IList<AgentStepRun> FindAllByRunIdAndStepIndex(Int64 runId, Int32 stepIndex)
    {
        if (runId < 0) return [];
        if (stepIndex < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.RunId == runId && e.StepIndex == stepIndex);

        return FindAll(_.RunId == runId & _.StepIndex == stepIndex);
    }
    #endregion

    #region 字段名
    /// <summary>取得Agent计划驱动执行步骤记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>步骤记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>关联执行实例ID</summary>
        public static readonly Field RunId = FindByName("RunId");

        /// <summary>步骤下标（0-based）</summary>
        public static readonly Field StepIndex = FindByName("StepIndex");

        /// <summary>Plan步骤ID（如s1）</summary>
        public static readonly Field StepId = FindByName("StepId");

        /// <summary>步骤名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>步骤目标</summary>
        public static readonly Field Objective = FindByName("Objective");

        /// <summary>步骤状态（AgentStepStatus枚举）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>组装给LLM的上下文摘要（入参）</summary>
        public static readonly Field InputJson = FindByName("InputJson");

        /// <summary>complete_step声明的产出（出参）</summary>
        public static readonly Field OutputJson = FindByName("OutputJson");

        /// <summary>本步工具调用轨迹</summary>
        public static readonly Field ToolCallsJson = FindByName("ToolCallsJson");

        /// <summary>失败错误</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        /// <summary>卡住原因</summary>
        public static readonly Field StuckReason = FindByName("StuckReason");

        /// <summary>人工批注</summary>
        public static readonly Field HumanNote = FindByName("HumanNote");

        /// <summary>人工补位产出</summary>
        public static readonly Field HumanOverride = FindByName("HumanOverride");

        /// <summary>重试次数</summary>
        public static readonly Field RetryCount = FindByName("RetryCount");

        /// <summary>本步Token</summary>
        public static readonly Field TokensUsed = FindByName("TokensUsed");

        /// <summary>耗时（毫秒）</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>开始时间</summary>
        public static readonly Field StartedAt = FindByName("StartedAt");

        /// <summary>完成时间</summary>
        public static readonly Field CompletedAt = FindByName("CompletedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得Agent计划驱动执行步骤记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>步骤记录ID</summary>
        public const String Id = "Id";

        /// <summary>关联执行实例ID</summary>
        public const String RunId = "RunId";

        /// <summary>步骤下标（0-based）</summary>
        public const String StepIndex = "StepIndex";

        /// <summary>Plan步骤ID（如s1）</summary>
        public const String StepId = "StepId";

        /// <summary>步骤名</summary>
        public const String Name = "Name";

        /// <summary>步骤目标</summary>
        public const String Objective = "Objective";

        /// <summary>步骤状态（AgentStepStatus枚举）</summary>
        public const String Status = "Status";

        /// <summary>组装给LLM的上下文摘要（入参）</summary>
        public const String InputJson = "InputJson";

        /// <summary>complete_step声明的产出（出参）</summary>
        public const String OutputJson = "OutputJson";

        /// <summary>本步工具调用轨迹</summary>
        public const String ToolCallsJson = "ToolCallsJson";

        /// <summary>失败错误</summary>
        public const String ErrorMessage = "ErrorMessage";

        /// <summary>卡住原因</summary>
        public const String StuckReason = "StuckReason";

        /// <summary>人工批注</summary>
        public const String HumanNote = "HumanNote";

        /// <summary>人工补位产出</summary>
        public const String HumanOverride = "HumanOverride";

        /// <summary>重试次数</summary>
        public const String RetryCount = "RetryCount";

        /// <summary>本步Token</summary>
        public const String TokensUsed = "TokensUsed";

        /// <summary>耗时（毫秒）</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>开始时间</summary>
        public const String StartedAt = "StartedAt";

        /// <summary>完成时间</summary>
        public const String CompletedAt = "CompletedAt";
    }
    #endregion
}
