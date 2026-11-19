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

/// <summary>{name}。外部 Runtime 会话（对外 SessionKey）</summary>
[Serializable]
[DataObject]
[Description("{name}。外部 Runtime 会话（对外 SessionKey）")]
[BindIndex("IU_AgentHubSession_SessionKey", true, "SessionKey")]
[BindIndex("IX_AgentHubSession_AgentId", false, "AgentId")]
[BindIndex("IX_AgentHubSession_Status", false, "Status")]
[BindTable("AgentHubSession", Description = "外部 Runtime 会话（对外 SessionKey）", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class AgentHubSession
{
    #region 属性
    private Int32 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionKey;
    /// <summary>对外会话键（GUID）</summary>
    [DisplayName("对外会话键（GUID）")]
    [Description("对外会话键（GUID）")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("SessionKey", "对外会话键（GUID）", "", Master = true)]
    public String SessionKey { get => _SessionKey; set { if (OnPropertyChanging("SessionKey", value)) { _SessionKey = value; OnPropertyChanged("SessionKey"); } } }

    private Int32 _AgentId;
    /// <summary>关联 Agent</summary>
    [DisplayName("关联Agent")]
    [Description("关联 Agent")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentId", "关联 Agent", "", DefaultValue = "0")]
    public Int32 AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private Int32 _AccessPointId;
    /// <summary>关联交互口</summary>
    [DisplayName("关联交互口")]
    [Description("关联交互口")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AccessPointId", "关联交互口", "", DefaultValue = "0")]
    public Int32 AccessPointId { get => _AccessPointId; set { if (OnPropertyChanging("AccessPointId", value)) { _AccessPointId = value; OnPropertyChanged("AccessPointId"); } } }

    private String _Transport;
    /// <summary>传输方式</summary>
    [DisplayName("传输方式")]
    [Description("传输方式")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Transport", "传输方式", "", DefaultValue = "Oar")]
    public String Transport { get => _Transport; set { if (OnPropertyChanging("Transport", value)) { _Transport = value; OnPropertyChanged("Transport"); } } }

    private String _RuntimeId;
    /// <summary>OAR Runtime id</summary>
    [DisplayName("OARRuntimeid")]
    [Description("OAR Runtime id")]
    [DataObjectField(false, false, true, 80)]
    [BindColumn("RuntimeId", "OAR Runtime id", "")]
    public String RuntimeId { get => _RuntimeId; set { if (OnPropertyChanging("RuntimeId", value)) { _RuntimeId = value; OnPropertyChanged("RuntimeId"); } } }

    private String _OarSessionId;
    /// <summary>Worker/OAR 侧会话 id（不对插件暴露）</summary>
    [DisplayName("Worker_OAR侧会话id（不对插件暴露）")]
    [Description("Worker/OAR 侧会话 id（不对插件暴露）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("OarSessionId", "Worker/OAR 侧会话 id（不对插件暴露）", "")]
    public String OarSessionId { get => _OarSessionId; set { if (OnPropertyChanging("OarSessionId", value)) { _OarSessionId = value; OnPropertyChanged("OarSessionId"); } } }

    private String _Cwd;
    /// <summary>工作目录</summary>
    [DisplayName("工作目录")]
    [Description("工作目录")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Cwd", "工作目录", "")]
    public String Cwd { get => _Cwd; set { if (OnPropertyChanging("Cwd", value)) { _Cwd = value; OnPropertyChanged("Cwd"); } } }

    private String _Model;
    /// <summary>模型提示</summary>
    [DisplayName("模型提示")]
    [Description("模型提示")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Model", "模型提示", "")]
    public String Model { get => _Model; set { if (OnPropertyChanging("Model", value)) { _Model = value; OnPropertyChanged("Model"); } } }

    private String _Effort;
    /// <summary>努力程度提示</summary>
    [DisplayName("努力程度提示")]
    [Description("努力程度提示")]
    [DataObjectField(false, false, true, 40)]
    [BindColumn("Effort", "努力程度提示", "")]
    public String Effort { get => _Effort; set { if (OnPropertyChanging("Effort", value)) { _Effort = value; OnPropertyChanged("Effort"); } } }

    private String _OptionsJson;
    /// <summary>选项 JSON（无密钥）</summary>
    [DisplayName("选项JSON（无密钥）")]
    [Description("选项 JSON（无密钥）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("OptionsJson", "选项 JSON（无密钥）", "")]
    public String OptionsJson { get => _OptionsJson; set { if (OnPropertyChanging("OptionsJson", value)) { _OptionsJson = value; OnPropertyChanged("OptionsJson"); } } }

    private String _Status;
    /// <summary>状态（Open|Closed|Interrupted）</summary>
    [DisplayName("状态（Open|Closed|Interrupted）")]
    [Description("状态（Open|Closed|Interrupted）")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Status", "状态（Open|Closed|Interrupted）", "", DefaultValue = "Open")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _Generation;
    /// <summary>世代（open/resume +1）</summary>
    [DisplayName("世代（open_resume+1）")]
    [Description("世代（open/resume +1）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Generation", "世代（open/resume +1）", "", DefaultValue = "1")]
    public Int32 Generation { get => _Generation; set { if (OnPropertyChanging("Generation", value)) { _Generation = value; OnPropertyChanged("Generation"); } } }

    private Int32 _LastTaskId;
    /// <summary>最近任务主键</summary>
    [DisplayName("最近任务主键")]
    [Description("最近任务主键")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LastTaskId", "最近任务主键", "", DefaultValue = "0")]
    public Int32 LastTaskId { get => _LastTaskId; set { if (OnPropertyChanging("LastTaskId", value)) { _LastTaskId = value; OnPropertyChanged("LastTaskId"); } } }

    private Int64 _AgentHubSequence;
    /// <summary>铸己匣事件序号水位</summary>
    [DisplayName("铸己匣事件序号水位")]
    [Description("铸己匣事件序号水位")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentHubSequence", "铸己匣事件序号水位", "", DefaultValue = "0")]
    public Int64 AgentHubSequence { get => _AgentHubSequence; set { if (OnPropertyChanging("AgentHubSequence", value)) { _AgentHubSequence = value; OnPropertyChanged("AgentHubSequence"); } } }

    private Int64 _OarSequence;
    /// <summary>OAR/Worker 侧序号水位</summary>
    [DisplayName("OAR_Worker侧序号水位")]
    [Description("OAR/Worker 侧序号水位")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OarSequence", "OAR/Worker 侧序号水位", "", DefaultValue = "0")]
    public Int64 OarSequence { get => _OarSequence; set { if (OnPropertyChanging("OarSequence", value)) { _OarSequence = value; OnPropertyChanged("OarSequence"); } } }

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
            "SessionKey" => _SessionKey,
            "AgentId" => _AgentId,
            "AccessPointId" => _AccessPointId,
            "Transport" => _Transport,
            "RuntimeId" => _RuntimeId,
            "OarSessionId" => _OarSessionId,
            "Cwd" => _Cwd,
            "Model" => _Model,
            "Effort" => _Effort,
            "OptionsJson" => _OptionsJson,
            "Status" => _Status,
            "Generation" => _Generation,
            "LastTaskId" => _LastTaskId,
            "AgentHubSequence" => _AgentHubSequence,
            "OarSequence" => _OarSequence,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "SessionKey": _SessionKey = Convert.ToString(value); break;
                case "AgentId": _AgentId = value.ToInt(); break;
                case "AccessPointId": _AccessPointId = value.ToInt(); break;
                case "Transport": _Transport = Convert.ToString(value); break;
                case "RuntimeId": _RuntimeId = Convert.ToString(value); break;
                case "OarSessionId": _OarSessionId = Convert.ToString(value); break;
                case "Cwd": _Cwd = Convert.ToString(value); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "Effort": _Effort = Convert.ToString(value); break;
                case "OptionsJson": _OptionsJson = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "Generation": _Generation = value.ToInt(); break;
                case "LastTaskId": _LastTaskId = value.ToInt(); break;
                case "AgentHubSequence": _AgentHubSequence = value.ToLong(); break;
                case "OarSequence": _OarSequence = value.ToLong(); break;
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
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static AgentHubSession FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据对外会话键（GUID）查找</summary>
    /// <param name="sessionKey">对外会话键（GUID）</param>
    /// <returns>实体对象</returns>
    public static AgentHubSession FindBySessionKey(String sessionKey)
    {
        if (sessionKey.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.SessionKey.EqualIgnoreCase(sessionKey));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(sessionKey) as AgentHubSession;

        //return Find(_.SessionKey == sessionKey);
    }

    /// <summary>根据关联Agent查找</summary>
    /// <param name="agentId">关联Agent</param>
    /// <returns>实体列表</returns>
    public static IList<AgentHubSession> FindAllByAgentId(Int32 agentId)
    {
        if (agentId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AgentId == agentId);

        return FindAll(_.AgentId == agentId);
    }

    /// <summary>根据状态（Open|Closed|Interrupted）查找</summary>
    /// <param name="status">状态（Open|Closed|Interrupted）</param>
    /// <returns>实体列表</returns>
    public static IList<AgentHubSession> FindAllByStatus(String status)
    {
        if (status.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status.EqualIgnoreCase(status));

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="agentId">关联 Agent</param>
    /// <param name="status">状态（Open|Closed|Interrupted）</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AgentHubSession> Search(Int32 agentId, String status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (agentId >= 0) exp &= _.AgentId == agentId;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得外部Runtime会话（对外SessionKey）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>对外会话键（GUID）</summary>
        public static readonly Field SessionKey = FindByName("SessionKey");

        /// <summary>关联 Agent</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>关联交互口</summary>
        public static readonly Field AccessPointId = FindByName("AccessPointId");

        /// <summary>传输方式</summary>
        public static readonly Field Transport = FindByName("Transport");

        /// <summary>OAR Runtime id</summary>
        public static readonly Field RuntimeId = FindByName("RuntimeId");

        /// <summary>Worker/OAR 侧会话 id（不对插件暴露）</summary>
        public static readonly Field OarSessionId = FindByName("OarSessionId");

        /// <summary>工作目录</summary>
        public static readonly Field Cwd = FindByName("Cwd");

        /// <summary>模型提示</summary>
        public static readonly Field Model = FindByName("Model");

        /// <summary>努力程度提示</summary>
        public static readonly Field Effort = FindByName("Effort");

        /// <summary>选项 JSON（无密钥）</summary>
        public static readonly Field OptionsJson = FindByName("OptionsJson");

        /// <summary>状态（Open|Closed|Interrupted）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>世代（open/resume +1）</summary>
        public static readonly Field Generation = FindByName("Generation");

        /// <summary>最近任务主键</summary>
        public static readonly Field LastTaskId = FindByName("LastTaskId");

        /// <summary>铸己匣事件序号水位</summary>
        public static readonly Field AgentHubSequence = FindByName("AgentHubSequence");

        /// <summary>OAR/Worker 侧序号水位</summary>
        public static readonly Field OarSequence = FindByName("OarSequence");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得外部Runtime会话（对外SessionKey）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>对外会话键（GUID）</summary>
        public const String SessionKey = "SessionKey";

        /// <summary>关联 Agent</summary>
        public const String AgentId = "AgentId";

        /// <summary>关联交互口</summary>
        public const String AccessPointId = "AccessPointId";

        /// <summary>传输方式</summary>
        public const String Transport = "Transport";

        /// <summary>OAR Runtime id</summary>
        public const String RuntimeId = "RuntimeId";

        /// <summary>Worker/OAR 侧会话 id（不对插件暴露）</summary>
        public const String OarSessionId = "OarSessionId";

        /// <summary>工作目录</summary>
        public const String Cwd = "Cwd";

        /// <summary>模型提示</summary>
        public const String Model = "Model";

        /// <summary>努力程度提示</summary>
        public const String Effort = "Effort";

        /// <summary>选项 JSON（无密钥）</summary>
        public const String OptionsJson = "OptionsJson";

        /// <summary>状态（Open|Closed|Interrupted）</summary>
        public const String Status = "Status";

        /// <summary>世代（open/resume +1）</summary>
        public const String Generation = "Generation";

        /// <summary>最近任务主键</summary>
        public const String LastTaskId = "LastTaskId";

        /// <summary>铸己匣事件序号水位</summary>
        public const String AgentHubSequence = "AgentHubSequence";

        /// <summary>OAR/Worker 侧序号水位</summary>
        public const String OarSequence = "OarSequence";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
