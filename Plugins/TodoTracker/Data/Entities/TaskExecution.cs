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

namespace ForgeSelf.Api.Plugins.TodoTracker.Entities;

/// <summary>{name}。待办执行记录（append-only 台账）</summary>
[Serializable]
[DataObject]
[Description("{name}。待办执行记录（append-only 台账）")]
[BindIndex("IX_TaskExecution_TodoId_Seq", false, "TodoId,Seq")]
[BindIndex("IX_TaskExecution_CreatedAt", false, "CreatedAt")]
[BindTable("TaskExecution", Description = "待办执行记录（append-only 台账）", ConnName = "TodoTracker", DbType = DatabaseType.None)]
public partial class TaskExecution
{
    #region 属性
    private Int32 _Id;
    /// <summary>记录ID</summary>
    [DisplayName("记录ID")]
    [Description("记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "记录ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _TodoId;
    /// <summary>所属任务 Id</summary>
    [DisplayName("所属任务Id")]
    [Description("所属任务 Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TodoId", "所属任务 Id", "")]
    public Int32 TodoId { get => _TodoId; set { if (OnPropertyChanging("TodoId", value)) { _TodoId = value; OnPropertyChanged("TodoId"); } } }

    private Int32 _Seq;
    /// <summary>任务内序号，从 1 递增</summary>
    [DisplayName("任务内序号")]
    [Description("任务内序号，从 1 递增")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Seq", "任务内序号，从 1 递增", "", DefaultValue = "0")]
    public Int32 Seq { get => _Seq; set { if (OnPropertyChanging("Seq", value)) { _Seq = value; OnPropertyChanged("Seq"); } } }

    private String _Actor;
    /// <summary>执行者（agent 名/manual/agent-hub）</summary>
    [DisplayName("执行者（agent名_manual_agent-hub）")]
    [Description("执行者（agent 名/manual/agent-hub）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Actor", "执行者（agent 名/manual/agent-hub）", "")]
    public String Actor { get => _Actor; set { if (OnPropertyChanging("Actor", value)) { _Actor = value; OnPropertyChanged("Actor"); } } }

    private String _Action;
    /// <summary>做了什么操作（一句话）</summary>
    [DisplayName("做了什么操作（一句话）")]
    [Description("做了什么操作（一句话）")]
    [DataObjectField(false, false, false, 300)]
    [BindColumn("Action", "做了什么操作（一句话）", "")]
    public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

    private String _Detail;
    /// <summary>操作明细（步骤/命令/说明）</summary>
    [DisplayName("操作明细（步骤_命令_说明）")]
    [Description("操作明细（步骤/命令/说明）")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("Detail", "操作明细（步骤/命令/说明）", "")]
    public String Detail { get => _Detail; set { if (OnPropertyChanging("Detail", value)) { _Detail = value; OnPropertyChanged("Detail"); } } }

    private String _Result;
    /// <summary>什么结果（含输出要点）</summary>
    [DisplayName("什么结果（含输出要点）")]
    [Description("什么结果（含输出要点）")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("Result", "什么结果（含输出要点）", "")]
    public String Result { get => _Result; set { if (OnPropertyChanging("Result", value)) { _Result = value; OnPropertyChanged("Result"); } } }

    private String _FilesChanged;
    /// <summary>改了哪些文件，JSON 数组 [{path,change}]</summary>
    [DisplayName("改了哪些文件")]
    [Description("改了哪些文件，JSON 数组 [{path,change}]")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("FilesChanged", "改了哪些文件，JSON 数组 [{path,change}]", "")]
    public String FilesChanged { get => _FilesChanged; set { if (OnPropertyChanging("FilesChanged", value)) { _FilesChanged = value; OnPropertyChanged("FilesChanged"); } } }

    private String _Verification;
    /// <summary>跑了什么验证 + 结果</summary>
    [DisplayName("跑了什么验证+结果")]
    [Description("跑了什么验证 + 结果")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("Verification", "跑了什么验证 + 结果", "")]
    public String Verification { get => _Verification; set { if (OnPropertyChanging("Verification", value)) { _Verification = value; OnPropertyChanged("Verification"); } } }

    private String _Risks;
    /// <summary>风险</summary>
    [DisplayName("风险")]
    [Description("风险")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Risks", "风险", "")]
    public String Risks { get => _Risks; set { if (OnPropertyChanging("Risks", value)) { _Risks = value; OnPropertyChanged("Risks"); } } }

    private String _Residuals;
    /// <summary>遗留/未做</summary>
    [DisplayName("遗留_未做")]
    [Description("遗留/未做")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Residuals", "遗留/未做", "")]
    public String Residuals { get => _Residuals; set { if (OnPropertyChanging("Residuals", value)) { _Residuals = value; OnPropertyChanged("Residuals"); } } }

    private String _Evidence;
    /// <summary>证据（截图/日志/工件路径/链接）</summary>
    [DisplayName("证据（截图_日志_工件路径_链接）")]
    [Description("证据（截图/日志/工件路径/链接）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Evidence", "证据（截图/日志/工件路径/链接）", "")]
    public String Evidence { get => _Evidence; set { if (OnPropertyChanging("Evidence", value)) { _Evidence = value; OnPropertyChanged("Evidence"); } } }

    private Int32 _StageFrom;
    /// <summary>流转前阶段，-1=不变</summary>
    [DisplayName("流转前阶段")]
    [Description("流转前阶段，-1=不变")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StageFrom", "流转前阶段，-1=不变", "", DefaultValue = "-1")]
    public Int32 StageFrom { get => _StageFrom; set { if (OnPropertyChanging("StageFrom", value)) { _StageFrom = value; OnPropertyChanged("StageFrom"); } } }

    private Int32 _StageTo;
    /// <summary>流转后阶段，-1=不变</summary>
    [DisplayName("流转后阶段")]
    [Description("流转后阶段，-1=不变")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StageTo", "流转后阶段，-1=不变", "", DefaultValue = "-1")]
    public Int32 StageTo { get => _StageTo; set { if (OnPropertyChanging("StageTo", value)) { _StageTo = value; OnPropertyChanged("StageTo"); } } }

    private Int32 _ElapsedMs;
    /// <summary>耗时（毫秒）</summary>
    [DisplayName("耗时（毫秒）")]
    [Description("耗时（毫秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时（毫秒）", "", DefaultValue = "0")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private String _BlockReason;
    /// <summary>阻塞原因（Stage=Blocked 时填）</summary>
    [DisplayName("阻塞原因（Stage=Blocked时填）")]
    [Description("阻塞原因（Stage=Blocked 时填）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("BlockReason", "阻塞原因（Stage=Blocked 时填）", "")]
    public String BlockReason { get => _BlockReason; set { if (OnPropertyChanging("BlockReason", value)) { _BlockReason = value; OnPropertyChanged("BlockReason"); } } }

    private String _NextStep;
    /// <summary>下一步（回流入口）</summary>
    [DisplayName("下一步（回流入口）")]
    [Description("下一步（回流入口）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("NextStep", "下一步（回流入口）", "")]
    public String NextStep { get => _NextStep; set { if (OnPropertyChanging("NextStep", value)) { _NextStep = value; OnPropertyChanged("NextStep"); } } }

    private DateTime _CreatedAt;
    /// <summary>记录时间</summary>
    [DisplayName("记录时间")]
    [Description("记录时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "记录时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }
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
            "TodoId" => _TodoId,
            "Seq" => _Seq,
            "Actor" => _Actor,
            "Action" => _Action,
            "Detail" => _Detail,
            "Result" => _Result,
            "FilesChanged" => _FilesChanged,
            "Verification" => _Verification,
            "Risks" => _Risks,
            "Residuals" => _Residuals,
            "Evidence" => _Evidence,
            "StageFrom" => _StageFrom,
            "StageTo" => _StageTo,
            "ElapsedMs" => _ElapsedMs,
            "BlockReason" => _BlockReason,
            "NextStep" => _NextStep,
            "CreatedAt" => _CreatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "TodoId": _TodoId = value.ToInt(); break;
                case "Seq": _Seq = value.ToInt(); break;
                case "Actor": _Actor = Convert.ToString(value); break;
                case "Action": _Action = Convert.ToString(value); break;
                case "Detail": _Detail = Convert.ToString(value); break;
                case "Result": _Result = Convert.ToString(value); break;
                case "FilesChanged": _FilesChanged = Convert.ToString(value); break;
                case "Verification": _Verification = Convert.ToString(value); break;
                case "Risks": _Risks = Convert.ToString(value); break;
                case "Residuals": _Residuals = Convert.ToString(value); break;
                case "Evidence": _Evidence = Convert.ToString(value); break;
                case "StageFrom": _StageFrom = value.ToInt(); break;
                case "StageTo": _StageTo = value.ToInt(); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
                case "BlockReason": _BlockReason = Convert.ToString(value); break;
                case "NextStep": _NextStep = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
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
    public static TaskExecution FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据所属任务Id、任务内序号查找</summary>
    /// <param name="todoId">所属任务Id</param>
    /// <param name="seq">任务内序号</param>
    /// <returns>实体列表</returns>
    public static IList<TaskExecution> FindAllByTodoIdAndSeq(Int32 todoId, Int32 seq)
    {
        if (todoId < 0) return [];
        if (seq < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.TodoId == todoId && e.Seq == seq);

        return FindAll(_.TodoId == todoId & _.Seq == seq);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="todoId">所属任务 Id</param>
    /// <param name="seq">任务内序号，从 1 递增</param>
    /// <param name="start">记录时间开始</param>
    /// <param name="end">记录时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TaskExecution> Search(Int32 todoId, Int32 seq, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (todoId >= 0) exp &= _.TodoId == todoId;
        if (seq >= 0) exp &= _.Seq == seq;
        exp &= _.CreatedAt.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得待办执行记录（append-only台账）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>所属任务 Id</summary>
        public static readonly Field TodoId = FindByName("TodoId");

        /// <summary>任务内序号，从 1 递增</summary>
        public static readonly Field Seq = FindByName("Seq");

        /// <summary>执行者（agent 名/manual/agent-hub）</summary>
        public static readonly Field Actor = FindByName("Actor");

        /// <summary>做了什么操作（一句话）</summary>
        public static readonly Field Action = FindByName("Action");

        /// <summary>操作明细（步骤/命令/说明）</summary>
        public static readonly Field Detail = FindByName("Detail");

        /// <summary>什么结果（含输出要点）</summary>
        public static readonly Field Result = FindByName("Result");

        /// <summary>改了哪些文件，JSON 数组 [{path,change}]</summary>
        public static readonly Field FilesChanged = FindByName("FilesChanged");

        /// <summary>跑了什么验证 + 结果</summary>
        public static readonly Field Verification = FindByName("Verification");

        /// <summary>风险</summary>
        public static readonly Field Risks = FindByName("Risks");

        /// <summary>遗留/未做</summary>
        public static readonly Field Residuals = FindByName("Residuals");

        /// <summary>证据（截图/日志/工件路径/链接）</summary>
        public static readonly Field Evidence = FindByName("Evidence");

        /// <summary>流转前阶段，-1=不变</summary>
        public static readonly Field StageFrom = FindByName("StageFrom");

        /// <summary>流转后阶段，-1=不变</summary>
        public static readonly Field StageTo = FindByName("StageTo");

        /// <summary>耗时（毫秒）</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>阻塞原因（Stage=Blocked 时填）</summary>
        public static readonly Field BlockReason = FindByName("BlockReason");

        /// <summary>下一步（回流入口）</summary>
        public static readonly Field NextStep = FindByName("NextStep");

        /// <summary>记录时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得待办执行记录（append-only台账）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>记录ID</summary>
        public const String Id = "Id";

        /// <summary>所属任务 Id</summary>
        public const String TodoId = "TodoId";

        /// <summary>任务内序号，从 1 递增</summary>
        public const String Seq = "Seq";

        /// <summary>执行者（agent 名/manual/agent-hub）</summary>
        public const String Actor = "Actor";

        /// <summary>做了什么操作（一句话）</summary>
        public const String Action = "Action";

        /// <summary>操作明细（步骤/命令/说明）</summary>
        public const String Detail = "Detail";

        /// <summary>什么结果（含输出要点）</summary>
        public const String Result = "Result";

        /// <summary>改了哪些文件，JSON 数组 [{path,change}]</summary>
        public const String FilesChanged = "FilesChanged";

        /// <summary>跑了什么验证 + 结果</summary>
        public const String Verification = "Verification";

        /// <summary>风险</summary>
        public const String Risks = "Risks";

        /// <summary>遗留/未做</summary>
        public const String Residuals = "Residuals";

        /// <summary>证据（截图/日志/工件路径/链接）</summary>
        public const String Evidence = "Evidence";

        /// <summary>流转前阶段，-1=不变</summary>
        public const String StageFrom = "StageFrom";

        /// <summary>流转后阶段，-1=不变</summary>
        public const String StageTo = "StageTo";

        /// <summary>耗时（毫秒）</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>阻塞原因（Stage=Blocked 时填）</summary>
        public const String BlockReason = "BlockReason";

        /// <summary>下一步（回流入口）</summary>
        public const String NextStep = "NextStep";

        /// <summary>记录时间</summary>
        public const String CreatedAt = "CreatedAt";
    }
    #endregion
}
