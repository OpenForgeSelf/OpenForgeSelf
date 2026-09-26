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

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_DelegationTask_Status", false, "Status")]
[BindIndex("IX_DelegationTask_AgentId", false, "AgentId")]
[BindIndex("IX_DelegationTask_CreateTime", false, "CreateTime")]
[BindTable("DelegationTask", Description = "委派任务", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class DelegationTask
{
    #region 属性
    private Int32 _Id;
    /// <summary>任务ID</summary>
    [DisplayName("任务ID")]
    [Description("任务ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "任务ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _TaskKey;
    /// <summary>任务标识（GUID）</summary>
    [DisplayName("任务标识（GUID）")]
    [Description("任务标识（GUID）")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("TaskKey", "任务标识（GUID）", "", Master = true)]
    public String TaskKey { get => _TaskKey; set { if (OnPropertyChanging("TaskKey", value)) { _TaskKey = value; OnPropertyChanged("TaskKey"); } } }

    private Int32 _AgentId;
    /// <summary>目标 Agent</summary>
    [DisplayName("目标Agent")]
    [Description("目标 Agent")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentId", "目标 Agent", "")]
    public Int32 AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private Int32 _AccessPointId;
    /// <summary>使用的交互口</summary>
    [DisplayName("使用的交互口")]
    [Description("使用的交互口")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AccessPointId", "使用的交互口", "", DefaultValue = "0")]
    public Int32 AccessPointId { get => _AccessPointId; set { if (OnPropertyChanging("AccessPointId", value)) { _AccessPointId = value; OnPropertyChanged("AccessPointId"); } } }

    private String _Prompt;
    /// <summary>提示词</summary>
    [DisplayName("提示词")]
    [Description("提示词")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("Prompt", "提示词", "")]
    public String Prompt { get => _Prompt; set { if (OnPropertyChanging("Prompt", value)) { _Prompt = value; OnPropertyChanged("Prompt"); } } }

    private String _Cwd;
    /// <summary>工作目录</summary>
    [DisplayName("工作目录")]
    [Description("工作目录")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Cwd", "工作目录", "")]
    public String Cwd { get => _Cwd; set { if (OnPropertyChanging("Cwd", value)) { _Cwd = value; OnPropertyChanged("Cwd"); } } }

    private String _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Status", "状态", "", DefaultValue = "Queued")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _SessionRef;
    /// <summary>外部会话 id</summary>
    [DisplayName("外部会话id")]
    [Description("外部会话 id")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("SessionRef", "外部会话 id", "")]
    public String SessionRef { get => _SessionRef; set { if (OnPropertyChanging("SessionRef", value)) { _SessionRef = value; OnPropertyChanged("SessionRef"); } } }

    private String _PermissionMode;
    /// <summary>权限模式</summary>
    [DisplayName("权限模式")]
    [Description("权限模式")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("PermissionMode", "权限模式", "")]
    public String PermissionMode { get => _PermissionMode; set { if (OnPropertyChanging("PermissionMode", value)) { _PermissionMode = value; OnPropertyChanged("PermissionMode"); } } }

    private Int32 _ExitCode;
    /// <summary>退出码</summary>
    [DisplayName("退出码")]
    [Description("退出码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExitCode", "退出码", "", DefaultValue = "0")]
    public Int32 ExitCode { get => _ExitCode; set { if (OnPropertyChanging("ExitCode", value)) { _ExitCode = value; OnPropertyChanged("ExitCode"); } } }

    private String _ErrorCode;
    /// <summary>错误分类</summary>
    [DisplayName("错误分类")]
    [Description("错误分类")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ErrorCode", "错误分类", "")]
    public String ErrorCode { get => _ErrorCode; set { if (OnPropertyChanging("ErrorCode", value)) { _ErrorCode = value; OnPropertyChanged("ErrorCode"); } } }

    private String _ResultText;
    /// <summary>结果</summary>
    [DisplayName("结果")]
    [Description("结果")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("ResultText", "结果", "")]
    public String ResultText { get => _ResultText; set { if (OnPropertyChanging("ResultText", value)) { _ResultText = value; OnPropertyChanged("ResultText"); } } }

    private String _ArtifactsJson;
    /// <summary>产物 JSON</summary>
    [DisplayName("产物JSON")]
    [Description("产物 JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ArtifactsJson", "产物 JSON", "")]
    public String ArtifactsJson { get => _ArtifactsJson; set { if (OnPropertyChanging("ArtifactsJson", value)) { _ArtifactsJson = value; OnPropertyChanged("ArtifactsJson"); } } }

    private String _UsageJson;
    /// <summary>用量 JSON</summary>
    [DisplayName("用量JSON")]
    [Description("用量 JSON")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("UsageJson", "用量 JSON", "")]
    public String UsageJson { get => _UsageJson; set { if (OnPropertyChanging("UsageJson", value)) { _UsageJson = value; OnPropertyChanged("UsageJson"); } } }

    private String _CreatedBy;
    /// <summary>发起方</summary>
    [DisplayName("发起方")]
    [Description("发起方")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CreatedBy", "发起方", "")]
    public String CreatedBy { get => _CreatedBy; set { if (OnPropertyChanging("CreatedBy", value)) { _CreatedBy = value; OnPropertyChanged("CreatedBy"); } } }

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

    private Int32 _ElapsedMs;
    /// <summary>执行耗时（毫秒，不含审批等待）</summary>
    [DisplayName("执行耗时（毫秒")]
    [Description("执行耗时（毫秒，不含审批等待）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "执行耗时（毫秒，不含审批等待）", "", DefaultValue = "0")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private Int32 _LastSeq;
    /// <summary>事件序号水位</summary>
    [DisplayName("事件序号水位")]
    [Description("事件序号水位")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LastSeq", "事件序号水位", "", DefaultValue = "0")]
    public Int32 LastSeq { get => _LastSeq; set { if (OnPropertyChanging("LastSeq", value)) { _LastSeq = value; OnPropertyChanged("LastSeq"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
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
            "TaskKey" => _TaskKey,
            "AgentId" => _AgentId,
            "AccessPointId" => _AccessPointId,
            "Prompt" => _Prompt,
            "Cwd" => _Cwd,
            "Status" => _Status,
            "SessionRef" => _SessionRef,
            "PermissionMode" => _PermissionMode,
            "ExitCode" => _ExitCode,
            "ErrorCode" => _ErrorCode,
            "ResultText" => _ResultText,
            "ArtifactsJson" => _ArtifactsJson,
            "UsageJson" => _UsageJson,
            "CreatedBy" => _CreatedBy,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "ElapsedMs" => _ElapsedMs,
            "LastSeq" => _LastSeq,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "TaskKey": _TaskKey = Convert.ToString(value); break;
                case "AgentId": _AgentId = value.ToInt(); break;
                case "AccessPointId": _AccessPointId = value.ToInt(); break;
                case "Prompt": _Prompt = Convert.ToString(value); break;
                case "Cwd": _Cwd = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "SessionRef": _SessionRef = Convert.ToString(value); break;
                case "PermissionMode": _PermissionMode = Convert.ToString(value); break;
                case "ExitCode": _ExitCode = value.ToInt(); break;
                case "ErrorCode": _ErrorCode = Convert.ToString(value); break;
                case "ResultText": _ResultText = Convert.ToString(value); break;
                case "ArtifactsJson": _ArtifactsJson = Convert.ToString(value); break;
                case "UsageJson": _UsageJson = Convert.ToString(value); break;
                case "CreatedBy": _CreatedBy = Convert.ToString(value); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
                case "LastSeq": _LastSeq = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据任务ID查找</summary>
    /// <param name="id">任务ID</param>
    /// <returns>实体对象</returns>
    public static DelegationTask FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据目标Agent查找</summary>
    /// <param name="agentId">目标Agent</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationTask> FindAllByAgentId(Int32 agentId)
    {
        if (agentId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AgentId == agentId);

        return FindAll(_.AgentId == agentId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="agentId">目标 Agent</param>
    /// <param name="status">状态</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationTask> Search(Int32 agentId, String status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (agentId >= 0) exp &= _.AgentId == agentId;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得委派任务字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>任务ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>任务标识（GUID）</summary>
        public static readonly Field TaskKey = FindByName("TaskKey");

        /// <summary>目标 Agent</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>使用的交互口</summary>
        public static readonly Field AccessPointId = FindByName("AccessPointId");

        /// <summary>提示词</summary>
        public static readonly Field Prompt = FindByName("Prompt");

        /// <summary>工作目录</summary>
        public static readonly Field Cwd = FindByName("Cwd");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>外部会话 id</summary>
        public static readonly Field SessionRef = FindByName("SessionRef");

        /// <summary>权限模式</summary>
        public static readonly Field PermissionMode = FindByName("PermissionMode");

        /// <summary>退出码</summary>
        public static readonly Field ExitCode = FindByName("ExitCode");

        /// <summary>错误分类</summary>
        public static readonly Field ErrorCode = FindByName("ErrorCode");

        /// <summary>结果</summary>
        public static readonly Field ResultText = FindByName("ResultText");

        /// <summary>产物 JSON</summary>
        public static readonly Field ArtifactsJson = FindByName("ArtifactsJson");

        /// <summary>用量 JSON</summary>
        public static readonly Field UsageJson = FindByName("UsageJson");

        /// <summary>发起方</summary>
        public static readonly Field CreatedBy = FindByName("CreatedBy");

        /// <summary>开始时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>执行耗时（毫秒，不含审批等待）</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>事件序号水位</summary>
        public static readonly Field LastSeq = FindByName("LastSeq");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得委派任务字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>任务ID</summary>
        public const String Id = "Id";

        /// <summary>任务标识（GUID）</summary>
        public const String TaskKey = "TaskKey";

        /// <summary>目标 Agent</summary>
        public const String AgentId = "AgentId";

        /// <summary>使用的交互口</summary>
        public const String AccessPointId = "AccessPointId";

        /// <summary>提示词</summary>
        public const String Prompt = "Prompt";

        /// <summary>工作目录</summary>
        public const String Cwd = "Cwd";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>外部会话 id</summary>
        public const String SessionRef = "SessionRef";

        /// <summary>权限模式</summary>
        public const String PermissionMode = "PermissionMode";

        /// <summary>退出码</summary>
        public const String ExitCode = "ExitCode";

        /// <summary>错误分类</summary>
        public const String ErrorCode = "ErrorCode";

        /// <summary>结果</summary>
        public const String ResultText = "ResultText";

        /// <summary>产物 JSON</summary>
        public const String ArtifactsJson = "ArtifactsJson";

        /// <summary>用量 JSON</summary>
        public const String UsageJson = "UsageJson";

        /// <summary>发起方</summary>
        public const String CreatedBy = "CreatedBy";

        /// <summary>开始时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>执行耗时（毫秒，不含审批等待）</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>事件序号水位</summary>
        public const String LastSeq = "LastSeq";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
