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

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_Todo_Status", false, "Status")]
[BindIndex("IX_Todo_CreatedAt", false, "CreatedAt")]
[BindIndex("IX_Todo_ProjectId", false, "ProjectId")]
[BindIndex("IX_Todo_Stage", false, "Stage")]
[BindIndex("IX_Todo_TaskKey", false, "TaskKey")]
[BindTable("Todo", Description = "待办事项", ConnName = "TodoTracker", DbType = DatabaseType.None)]
public partial class Todo
{
    #region 属性
    private Int32 _Id;
    /// <summary>待办ID</summary>
    [DisplayName("待办ID")]
    [Description("待办ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "待办ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private Int32 _Status;
    /// <summary>状态 0=Pending 1=Completed</summary>
    [DisplayName("状态0=Pending1=Completed")]
    [Description("状态 0=Pending 1=Completed")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态 0=Pending 1=Completed", "", DefaultValue = "0")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _DueDate;
    /// <summary>截止日期</summary>
    [DisplayName("截止日期")]
    [Description("截止日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DueDate", "截止日期", "")]
    public DateTime DueDate { get => _DueDate; set { if (OnPropertyChanging("DueDate", value)) { _DueDate = value; OnPropertyChanged("DueDate"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }

    private DateTime _CompletedAt;
    /// <summary>完成时间</summary>
    [DisplayName("完成时间")]
    [Description("完成时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CompletedAt", "完成时间", "")]
    public DateTime CompletedAt { get => _CompletedAt; set { if (OnPropertyChanging("CompletedAt", value)) { _CompletedAt = value; OnPropertyChanged("CompletedAt"); } } }

    private String _TaskKey;
    /// <summary>任务外部键 GUID(N)，供 agent 侧引用</summary>
    [DisplayName("任务外部键GUID(N)")]
    [Description("任务外部键 GUID(N)，供 agent 侧引用")]
    [DataObjectField(false, false, true, 32)]
    [BindColumn("TaskKey", "任务外部键 GUID(N)，供 agent 侧引用", "")]
    public String TaskKey { get => _TaskKey; set { if (OnPropertyChanging("TaskKey", value)) { _TaskKey = value; OnPropertyChanged("TaskKey"); } } }

    private Int32 _ProjectId;
    /// <summary>关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联</summary>
    [DisplayName("关联宿主项目Id（IProjectRegistry")]
    [Description("关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联", "", DefaultValue = "0")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _ProjectRoot;
    /// <summary>关联项目根（归一后的绝对路径快照）</summary>
    [DisplayName("关联项目根（归一后的绝对路径快照）")]
    [Description("关联项目根（归一后的绝对路径快照）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ProjectRoot", "关联项目根（归一后的绝对路径快照）", "")]
    public String ProjectRoot { get => _ProjectRoot; set { if (OnPropertyChanging("ProjectRoot", value)) { _ProjectRoot = value; OnPropertyChanged("ProjectRoot"); } } }

    private String _ProjectPathRaw;
    /// <summary>用户输入的原始路径写法（如 /d/project），便于回溯</summary>
    [DisplayName("用户输入的原始路径写法（如_d_project）")]
    [Description("用户输入的原始路径写法（如 /d/project），便于回溯")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ProjectPathRaw", "用户输入的原始路径写法（如 /d/project），便于回溯", "")]
    public String ProjectPathRaw { get => _ProjectPathRaw; set { if (OnPropertyChanging("ProjectPathRaw", value)) { _ProjectPathRaw = value; OnPropertyChanged("ProjectPathRaw"); } } }

    private String _Objective;
    /// <summary>可验证目标（一句话）</summary>
    [DisplayName("可验证目标（一句话）")]
    [Description("可验证目标（一句话）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Objective", "可验证目标（一句话）", "")]
    public String Objective { get => _Objective; set { if (OnPropertyChanging("Objective", value)) { _Objective = value; OnPropertyChanged("Objective"); } } }

    private String _Content;
    /// <summary>任务正文（markdown，可由工件组装）</summary>
    [DisplayName("任务正文（markdown")]
    [Description("任务正文（markdown，可由工件组装）")]
    [DataObjectField(false, false, true, 262144)]
    [BindColumn("Content", "任务正文（markdown，可由工件组装）", "")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private String _AllowedScope;
    /// <summary>允许改动范围</summary>
    [DisplayName("允许改动范围")]
    [Description("允许改动范围")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("AllowedScope", "允许改动范围", "")]
    public String AllowedScope { get => _AllowedScope; set { if (OnPropertyChanging("AllowedScope", value)) { _AllowedScope = value; OnPropertyChanged("AllowedScope"); } } }

    private String _ForbiddenScope;
    /// <summary>禁止改动范围</summary>
    [DisplayName("禁止改动范围")]
    [Description("禁止改动范围")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ForbiddenScope", "禁止改动范围", "")]
    public String ForbiddenScope { get => _ForbiddenScope; set { if (OnPropertyChanging("ForbiddenScope", value)) { _ForbiddenScope = value; OnPropertyChanged("ForbiddenScope"); } } }

    private String _Acceptance;
    /// <summary>验收判据，每行一条</summary>
    [DisplayName("验收判据")]
    [Description("验收判据，每行一条")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("Acceptance", "验收判据，每行一条", "")]
    public String Acceptance { get => _Acceptance; set { if (OnPropertyChanging("Acceptance", value)) { _Acceptance = value; OnPropertyChanged("Acceptance"); } } }

    private String _Verification;
    /// <summary>验证命令，每行一条</summary>
    [DisplayName("验证命令")]
    [Description("验证命令，每行一条")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Verification", "验证命令，每行一条", "")]
    public String Verification { get => _Verification; set { if (OnPropertyChanging("Verification", value)) { _Verification = value; OnPropertyChanged("Verification"); } } }

    private Int32 _Priority;
    /// <summary>优先级 1=P1 2=P2 3=P3</summary>
    [DisplayName("优先级1=P12=P23=P3")]
    [Description("优先级 1=P1 2=P2 3=P3")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "优先级 1=P1 2=P2 3=P3", "", DefaultValue = "1")]
    public Int32 Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private String _Assignee;
    /// <summary>下发对象（agent 名/manual）</summary>
    [DisplayName("下发对象（agent名_manual）")]
    [Description("下发对象（agent 名/manual）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Assignee", "下发对象（agent 名/manual）", "")]
    public String Assignee { get => _Assignee; set { if (OnPropertyChanging("Assignee", value)) { _Assignee = value; OnPropertyChanged("Assignee"); } } }

    private Int32 _Stage;
    /// <summary>下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled</summary>
    [DisplayName("下发阶段0=Draft1=Ready2=Dispatched3=Running4=Blocked5=Review6=Done7=Cancelled")]
    [Description("下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Stage", "下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled", "", DefaultValue = "0")]
    public Int32 Stage { get => _Stage; set { if (OnPropertyChanging("Stage", value)) { _Stage = value; OnPropertyChanged("Stage"); } } }

    private String _ArtifactRef;
    /// <summary>来源工件目录（相对项目根）</summary>
    [DisplayName("来源工件目录（相对项目根）")]
    [Description("来源工件目录（相对项目根）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ArtifactRef", "来源工件目录（相对项目根）", "")]
    public String ArtifactRef { get => _ArtifactRef; set { if (OnPropertyChanging("ArtifactRef", value)) { _ArtifactRef = value; OnPropertyChanged("ArtifactRef"); } } }

    private DateTime _DispatchedAt;
    /// <summary>下发时间，MinValue=未下发</summary>
    [DisplayName("下发时间")]
    [Description("下发时间，MinValue=未下发")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DispatchedAt", "下发时间，MinValue=未下发", "")]
    public DateTime DispatchedAt { get => _DispatchedAt; set { if (OnPropertyChanging("DispatchedAt", value)) { _DispatchedAt = value; OnPropertyChanged("DispatchedAt"); } } }

    private String _AgentTaskKey;
    /// <summary>委派任务 key（AgentHub taskKey 或本工具 AI Agent 的 run:id，一键执行后回填）</summary>
    [DisplayName("委派任务key（AgentHubtaskKey或本工具AIAgent的run")]
    [Description("委派任务 key（AgentHub taskKey 或本工具 AI Agent 的 run:id，一键执行后回填）")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("AgentTaskKey", "委派任务 key（AgentHub taskKey 或本工具 AI Agent 的 run:id，一键执行后回填）", "")]
    public String AgentTaskKey { get => _AgentTaskKey; set { if (OnPropertyChanging("AgentTaskKey", value)) { _AgentTaskKey = value; OnPropertyChanged("AgentTaskKey"); } } }

    private Int32 _AgentId;
    /// <summary>AgentHub agent Id，0=自动选路；内置引擎=角色序号 1..7</summary>
    [DisplayName("AgentHubagentId")]
    [Description("AgentHub agent Id，0=自动选路；内置引擎=角色序号 1..7")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentId", "AgentHub agent Id，0=自动选路；内置引擎=角色序号 1..7", "", DefaultValue = "0")]
    public Int32 AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private String _AgentEngine;
    /// <summary>委派引擎 agenthub=外部 AgentHub / builtin=本工具 AI Agent</summary>
    [DisplayName("委派引擎agenthub=外部AgentHub_builtin=本工具AIAgent")]
    [Description("委派引擎 agenthub=外部 AgentHub / builtin=本工具 AI Agent")]
    [DataObjectField(false, false, true, 16)]
    [BindColumn("AgentEngine", "委派引擎 agenthub=外部 AgentHub / builtin=本工具 AI Agent", "", DefaultValue = "agenthub")]
    public String AgentEngine { get => _AgentEngine; set { if (OnPropertyChanging("AgentEngine", value)) { _AgentEngine = value; OnPropertyChanged("AgentEngine"); } } }

    private String _PermissionMode;
    /// <summary>委派权限模式 read-only|workspace-write|accept-edits</summary>
    [DisplayName("委派权限模式read-only|workspace-write|accept-edits")]
    [Description("委派权限模式 read-only|workspace-write|accept-edits")]
    [DataObjectField(false, false, true, 32)]
    [BindColumn("PermissionMode", "委派权限模式 read-only|workspace-write|accept-edits", "", DefaultValue = "read-only")]
    public String PermissionMode { get => _PermissionMode; set { if (OnPropertyChanging("PermissionMode", value)) { _PermissionMode = value; OnPropertyChanged("PermissionMode"); } } }
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
            "Title" => _Title,
            "Remark" => _Remark,
            "Status" => _Status,
            "DueDate" => _DueDate,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "CompletedAt" => _CompletedAt,
            "TaskKey" => _TaskKey,
            "ProjectId" => _ProjectId,
            "ProjectRoot" => _ProjectRoot,
            "ProjectPathRaw" => _ProjectPathRaw,
            "Objective" => _Objective,
            "Content" => _Content,
            "AllowedScope" => _AllowedScope,
            "ForbiddenScope" => _ForbiddenScope,
            "Acceptance" => _Acceptance,
            "Verification" => _Verification,
            "Priority" => _Priority,
            "Assignee" => _Assignee,
            "Stage" => _Stage,
            "ArtifactRef" => _ArtifactRef,
            "DispatchedAt" => _DispatchedAt,
            "AgentTaskKey" => _AgentTaskKey,
            "AgentId" => _AgentId,
            "AgentEngine" => _AgentEngine,
            "PermissionMode" => _PermissionMode,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "DueDate": _DueDate = value.ToDateTime(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "CompletedAt": _CompletedAt = value.ToDateTime(); break;
                case "TaskKey": _TaskKey = Convert.ToString(value); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "ProjectRoot": _ProjectRoot = Convert.ToString(value); break;
                case "ProjectPathRaw": _ProjectPathRaw = Convert.ToString(value); break;
                case "Objective": _Objective = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "AllowedScope": _AllowedScope = Convert.ToString(value); break;
                case "ForbiddenScope": _ForbiddenScope = Convert.ToString(value); break;
                case "Acceptance": _Acceptance = Convert.ToString(value); break;
                case "Verification": _Verification = Convert.ToString(value); break;
                case "Priority": _Priority = value.ToInt(); break;
                case "Assignee": _Assignee = Convert.ToString(value); break;
                case "Stage": _Stage = value.ToInt(); break;
                case "ArtifactRef": _ArtifactRef = Convert.ToString(value); break;
                case "DispatchedAt": _DispatchedAt = value.ToDateTime(); break;
                case "AgentTaskKey": _AgentTaskKey = Convert.ToString(value); break;
                case "AgentId": _AgentId = value.ToInt(); break;
                case "AgentEngine": _AgentEngine = Convert.ToString(value); break;
                case "PermissionMode": _PermissionMode = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据待办ID查找</summary>
    /// <param name="id">待办ID</param>
    /// <returns>实体对象</returns>
    public static Todo FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据状态0=Pending1=Completed查找</summary>
    /// <param name="status">状态0=Pending1=Completed</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }

    /// <summary>根据关联宿主项目Id（IProjectRegistry查找</summary>
    /// <param name="projectId">关联宿主项目Id（IProjectRegistry</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> FindAllByProjectId(Int32 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据下发阶段0=Draft1=Ready2=Dispatched3=Running4=Blocked5=Review6=Done7=Cancelled查找</summary>
    /// <param name="stage">下发阶段0=Draft1=Ready2=Dispatched3=Running4=Blocked5=Review6=Done7=Cancelled</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> FindAllByStage(Int32 stage)
    {
        if (stage < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Stage == stage);

        return FindAll(_.Stage == stage);
    }

    /// <summary>根据任务外部键GUID(N)查找</summary>
    /// <param name="taskKey">任务外部键GUID(N)</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> FindAllByTaskKey(String taskKey)
    {
        if (taskKey.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.TaskKey.EqualIgnoreCase(taskKey));

        return FindAll(_.TaskKey == taskKey);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="status">状态 0=Pending 1=Completed</param>
    /// <param name="taskKey">任务外部键 GUID(N)，供 agent 侧引用</param>
    /// <param name="projectId">关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联</param>
    /// <param name="stage">下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> Search(Int32 status, String taskKey, Int32 projectId, Int32 stage, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (status >= 0) exp &= _.Status == status;
        if (!taskKey.IsNullOrEmpty()) exp &= _.TaskKey == taskKey;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (stage >= 0) exp &= _.Stage == stage;
        exp &= _.CreatedAt.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得待办事项字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>待办ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>状态 0=Pending 1=Completed</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>截止日期</summary>
        public static readonly Field DueDate = FindByName("DueDate");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        /// <summary>完成时间</summary>
        public static readonly Field CompletedAt = FindByName("CompletedAt");

        /// <summary>任务外部键 GUID(N)，供 agent 侧引用</summary>
        public static readonly Field TaskKey = FindByName("TaskKey");

        /// <summary>关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>关联项目根（归一后的绝对路径快照）</summary>
        public static readonly Field ProjectRoot = FindByName("ProjectRoot");

        /// <summary>用户输入的原始路径写法（如 /d/project），便于回溯</summary>
        public static readonly Field ProjectPathRaw = FindByName("ProjectPathRaw");

        /// <summary>可验证目标（一句话）</summary>
        public static readonly Field Objective = FindByName("Objective");

        /// <summary>任务正文（markdown，可由工件组装）</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>允许改动范围</summary>
        public static readonly Field AllowedScope = FindByName("AllowedScope");

        /// <summary>禁止改动范围</summary>
        public static readonly Field ForbiddenScope = FindByName("ForbiddenScope");

        /// <summary>验收判据，每行一条</summary>
        public static readonly Field Acceptance = FindByName("Acceptance");

        /// <summary>验证命令，每行一条</summary>
        public static readonly Field Verification = FindByName("Verification");

        /// <summary>优先级 1=P1 2=P2 3=P3</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>下发对象（agent 名/manual）</summary>
        public static readonly Field Assignee = FindByName("Assignee");

        /// <summary>下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled</summary>
        public static readonly Field Stage = FindByName("Stage");

        /// <summary>来源工件目录（相对项目根）</summary>
        public static readonly Field ArtifactRef = FindByName("ArtifactRef");

        /// <summary>下发时间，MinValue=未下发</summary>
        public static readonly Field DispatchedAt = FindByName("DispatchedAt");

        /// <summary>委派任务 key（AgentHub taskKey 或本工具 AI Agent 的 run:id，一键执行后回填）</summary>
        public static readonly Field AgentTaskKey = FindByName("AgentTaskKey");

        /// <summary>AgentHub agent Id，0=自动选路；内置引擎=角色序号 1..7</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>委派引擎 agenthub=外部 AgentHub / builtin=本工具 AI Agent</summary>
        public static readonly Field AgentEngine = FindByName("AgentEngine");

        /// <summary>委派权限模式 read-only|workspace-write|accept-edits</summary>
        public static readonly Field PermissionMode = FindByName("PermissionMode");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得待办事项字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>待办ID</summary>
        public const String Id = "Id";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>备注</summary>
        public const String Remark = "Remark";

        /// <summary>状态 0=Pending 1=Completed</summary>
        public const String Status = "Status";

        /// <summary>截止日期</summary>
        public const String DueDate = "DueDate";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";

        /// <summary>完成时间</summary>
        public const String CompletedAt = "CompletedAt";

        /// <summary>任务外部键 GUID(N)，供 agent 侧引用</summary>
        public const String TaskKey = "TaskKey";

        /// <summary>关联宿主项目 Id（IProjectRegistry.ProjectInfo.Id），0=未关联</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>关联项目根（归一后的绝对路径快照）</summary>
        public const String ProjectRoot = "ProjectRoot";

        /// <summary>用户输入的原始路径写法（如 /d/project），便于回溯</summary>
        public const String ProjectPathRaw = "ProjectPathRaw";

        /// <summary>可验证目标（一句话）</summary>
        public const String Objective = "Objective";

        /// <summary>任务正文（markdown，可由工件组装）</summary>
        public const String Content = "Content";

        /// <summary>允许改动范围</summary>
        public const String AllowedScope = "AllowedScope";

        /// <summary>禁止改动范围</summary>
        public const String ForbiddenScope = "ForbiddenScope";

        /// <summary>验收判据，每行一条</summary>
        public const String Acceptance = "Acceptance";

        /// <summary>验证命令，每行一条</summary>
        public const String Verification = "Verification";

        /// <summary>优先级 1=P1 2=P2 3=P3</summary>
        public const String Priority = "Priority";

        /// <summary>下发对象（agent 名/manual）</summary>
        public const String Assignee = "Assignee";

        /// <summary>下发阶段 0=Draft 1=Ready 2=Dispatched 3=Running 4=Blocked 5=Review 6=Done 7=Cancelled</summary>
        public const String Stage = "Stage";

        /// <summary>来源工件目录（相对项目根）</summary>
        public const String ArtifactRef = "ArtifactRef";

        /// <summary>下发时间，MinValue=未下发</summary>
        public const String DispatchedAt = "DispatchedAt";

        /// <summary>委派任务 key（AgentHub taskKey 或本工具 AI Agent 的 run:id，一键执行后回填）</summary>
        public const String AgentTaskKey = "AgentTaskKey";

        /// <summary>AgentHub agent Id，0=自动选路；内置引擎=角色序号 1..7</summary>
        public const String AgentId = "AgentId";

        /// <summary>委派引擎 agenthub=外部 AgentHub / builtin=本工具 AI Agent</summary>
        public const String AgentEngine = "AgentEngine";

        /// <summary>委派权限模式 read-only|workspace-write|accept-edits</summary>
        public const String PermissionMode = "PermissionMode";
    }
    #endregion
}
