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
[BindIndex("IX_ChatRecord_SessionId", false, "SessionId")]
[BindIndex("IX_ChatRecord_CreatedTime", false, "CreatedTime")]
[BindIndex("IX_ChatRecord_Style", false, "Style")]
[BindTable("ChatRecord", Description = "聊天记录", ConnName = "OpenForgeSelf", DbType = DatabaseType.None)]
public partial class ChatRecord : IChatRecordModel, IEntity<IChatRecordModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>记录ID</summary>
    [DisplayName("记录ID")]
    [Description("记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionId;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionId", "会话ID", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _Style;
    /// <summary>API风格</summary>
    [DisplayName("API风格")]
    [Description("API风格")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Style", "API风格", "")]
    public String Style { get => _Style; set { if (OnPropertyChanging("Style", value)) { _Style = value; OnPropertyChanged("Style"); } } }

    private String _Model;
    /// <summary>模型名称</summary>
    [DisplayName("模型名称")]
    [Description("模型名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Model", "模型名称", "")]
    public String Model { get => _Model; set { if (OnPropertyChanging("Model", value)) { _Model = value; OnPropertyChanged("Model"); } } }

    private String _RequestMethod;
    /// <summary>请求方法</summary>
    [DisplayName("请求方法")]
    [Description("请求方法")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("RequestMethod", "请求方法", "")]
    public String RequestMethod { get => _RequestMethod; set { if (OnPropertyChanging("RequestMethod", value)) { _RequestMethod = value; OnPropertyChanged("RequestMethod"); } } }

    private String _RequestPath;
    /// <summary>请求路径</summary>
    [DisplayName("请求路径")]
    [Description("请求路径")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("RequestPath", "请求路径", "")]
    public String RequestPath { get => _RequestPath; set { if (OnPropertyChanging("RequestPath", value)) { _RequestPath = value; OnPropertyChanged("RequestPath"); } } }

    private String _RequestHeaders;
    /// <summary>请求头JSON</summary>
    [DisplayName("请求头JSON")]
    [Description("请求头JSON")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("RequestHeaders", "请求头JSON", "")]
    public String RequestHeaders { get => _RequestHeaders; set { if (OnPropertyChanging("RequestHeaders", value)) { _RequestHeaders = value; OnPropertyChanged("RequestHeaders"); } } }

    private String _RequestBody;
    /// <summary>请求体JSON</summary>
    [DisplayName("请求体JSON")]
    [Description("请求体JSON")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("RequestBody", "请求体JSON", "")]
    public String RequestBody { get => _RequestBody; set { if (OnPropertyChanging("RequestBody", value)) { _RequestBody = value; OnPropertyChanged("RequestBody"); } } }

    private Int32 _ResponseStatus;
    /// <summary>响应状态码</summary>
    [DisplayName("响应状态码")]
    [Description("响应状态码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ResponseStatus", "响应状态码", "")]
    public Int32 ResponseStatus { get => _ResponseStatus; set { if (OnPropertyChanging("ResponseStatus", value)) { _ResponseStatus = value; OnPropertyChanged("ResponseStatus"); } } }

    private String _ResponseHeaders;
    /// <summary>响应头JSON</summary>
    [DisplayName("响应头JSON")]
    [Description("响应头JSON")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("ResponseHeaders", "响应头JSON", "")]
    public String ResponseHeaders { get => _ResponseHeaders; set { if (OnPropertyChanging("ResponseHeaders", value)) { _ResponseHeaders = value; OnPropertyChanged("ResponseHeaders"); } } }

    private String _ResponseBody;
    /// <summary>响应体JSON</summary>
    [DisplayName("响应体JSON")]
    [Description("响应体JSON")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ResponseBody", "响应体JSON", "")]
    public String ResponseBody { get => _ResponseBody; set { if (OnPropertyChanging("ResponseBody", value)) { _ResponseBody = value; OnPropertyChanged("ResponseBody"); } } }

    private Double _Temperature;
    /// <summary>温度参数</summary>
    [DisplayName("温度参数")]
    [Description("温度参数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Temperature", "温度参数", "")]
    public Double Temperature { get => _Temperature; set { if (OnPropertyChanging("Temperature", value)) { _Temperature = value; OnPropertyChanged("Temperature"); } } }

    private Int32 _MaxTokens;
    /// <summary>最大token</summary>
    [DisplayName("最大token")]
    [Description("最大token")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("MaxTokens", "最大token", "")]
    public Int32 MaxTokens { get => _MaxTokens; set { if (OnPropertyChanging("MaxTokens", value)) { _MaxTokens = value; OnPropertyChanged("MaxTokens"); } } }

    private Int32 _MessageCount;
    /// <summary>消息数量</summary>
    [DisplayName("消息数量")]
    [Description("消息数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageCount", "消息数量", "")]
    public Int32 MessageCount { get => _MessageCount; set { if (OnPropertyChanging("MessageCount", value)) { _MessageCount = value; OnPropertyChanged("MessageCount"); } } }

    private Int32 _ToolCallCount;
    /// <summary>工具调用次数</summary>
    [DisplayName("工具调用次数")]
    [Description("工具调用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ToolCallCount", "工具调用次数", "")]
    public Int32 ToolCallCount { get => _ToolCallCount; set { if (OnPropertyChanging("ToolCallCount", value)) { _ToolCallCount = value; OnPropertyChanged("ToolCallCount"); } } }

    private Boolean _HasReasoning;
    /// <summary>是否有reasoning</summary>
    [DisplayName("是否有reasoning")]
    [Description("是否有reasoning")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HasReasoning", "是否有reasoning", "")]
    public Boolean HasReasoning { get => _HasReasoning; set { if (OnPropertyChanging("HasReasoning", value)) { _HasReasoning = value; OnPropertyChanged("HasReasoning"); } } }

    private Int64 _DurationMs;
    /// <summary>调用耗时</summary>
    [DisplayName("调用耗时")]
    [Description("调用耗时")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "调用耗时", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private DateTime _CreatedTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedTime", "创建时间", "")]
    public DateTime CreatedTime { get => _CreatedTime; set { if (OnPropertyChanging("CreatedTime", value)) { _CreatedTime = value; OnPropertyChanged("CreatedTime"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IChatRecordModel model)
    {
        Id = model.Id;
        SessionId = model.SessionId;
        Style = model.Style;
        Model = model.Model;
        RequestMethod = model.RequestMethod;
        RequestPath = model.RequestPath;
        RequestHeaders = model.RequestHeaders;
        RequestBody = model.RequestBody;
        ResponseStatus = model.ResponseStatus;
        ResponseHeaders = model.ResponseHeaders;
        ResponseBody = model.ResponseBody;
        Temperature = model.Temperature;
        MaxTokens = model.MaxTokens;
        MessageCount = model.MessageCount;
        ToolCallCount = model.ToolCallCount;
        HasReasoning = model.HasReasoning;
        DurationMs = model.DurationMs;
        CreatedTime = model.CreatedTime;
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
            "SessionId" => _SessionId,
            "Style" => _Style,
            "Model" => _Model,
            "RequestMethod" => _RequestMethod,
            "RequestPath" => _RequestPath,
            "RequestHeaders" => _RequestHeaders,
            "RequestBody" => _RequestBody,
            "ResponseStatus" => _ResponseStatus,
            "ResponseHeaders" => _ResponseHeaders,
            "ResponseBody" => _ResponseBody,
            "Temperature" => _Temperature,
            "MaxTokens" => _MaxTokens,
            "MessageCount" => _MessageCount,
            "ToolCallCount" => _ToolCallCount,
            "HasReasoning" => _HasReasoning,
            "DurationMs" => _DurationMs,
            "CreatedTime" => _CreatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "Style": _Style = Convert.ToString(value); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "RequestMethod": _RequestMethod = Convert.ToString(value); break;
                case "RequestPath": _RequestPath = Convert.ToString(value); break;
                case "RequestHeaders": _RequestHeaders = Convert.ToString(value); break;
                case "RequestBody": _RequestBody = Convert.ToString(value); break;
                case "ResponseStatus": _ResponseStatus = value.ToInt(); break;
                case "ResponseHeaders": _ResponseHeaders = Convert.ToString(value); break;
                case "ResponseBody": _ResponseBody = Convert.ToString(value); break;
                case "Temperature": _Temperature = value.ToDouble(); break;
                case "MaxTokens": _MaxTokens = value.ToInt(); break;
                case "MessageCount": _MessageCount = value.ToInt(); break;
                case "ToolCallCount": _ToolCallCount = value.ToInt(); break;
                case "HasReasoning": _HasReasoning = value.ToBoolean(); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "CreatedTime": _CreatedTime = value.ToDateTime(); break;
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
    public static ChatRecord FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据会话ID查找</summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>实体列表</returns>
    public static IList<ChatRecord> FindAllBySessionId(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SessionId.EqualIgnoreCase(sessionId));

        return FindAll(_.SessionId == sessionId);
    }

    /// <summary>根据API风格查找</summary>
    /// <param name="style">API风格</param>
    /// <returns>实体列表</returns>
    public static IList<ChatRecord> FindAllByStyle(String style)
    {
        if (style.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Style.EqualIgnoreCase(style));

        return FindAll(_.Style == style);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="style">API风格</param>
    /// <param name="hasReasoning">是否有reasoning</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatRecord> Search(String sessionId, String style, Boolean? hasReasoning, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (!style.IsNullOrEmpty()) exp &= _.Style == style;
        if (hasReasoning != null) exp &= _.HasReasoning == hasReasoning;
        exp &= _.CreatedTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得聊天记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>API风格</summary>
        public static readonly Field Style = FindByName("Style");

        /// <summary>模型名称</summary>
        public static readonly Field Model = FindByName("Model");

        /// <summary>请求方法</summary>
        public static readonly Field RequestMethod = FindByName("RequestMethod");

        /// <summary>请求路径</summary>
        public static readonly Field RequestPath = FindByName("RequestPath");

        /// <summary>请求头JSON</summary>
        public static readonly Field RequestHeaders = FindByName("RequestHeaders");

        /// <summary>请求体JSON</summary>
        public static readonly Field RequestBody = FindByName("RequestBody");

        /// <summary>响应状态码</summary>
        public static readonly Field ResponseStatus = FindByName("ResponseStatus");

        /// <summary>响应头JSON</summary>
        public static readonly Field ResponseHeaders = FindByName("ResponseHeaders");

        /// <summary>响应体JSON</summary>
        public static readonly Field ResponseBody = FindByName("ResponseBody");

        /// <summary>温度参数</summary>
        public static readonly Field Temperature = FindByName("Temperature");

        /// <summary>最大token</summary>
        public static readonly Field MaxTokens = FindByName("MaxTokens");

        /// <summary>消息数量</summary>
        public static readonly Field MessageCount = FindByName("MessageCount");

        /// <summary>工具调用次数</summary>
        public static readonly Field ToolCallCount = FindByName("ToolCallCount");

        /// <summary>是否有reasoning</summary>
        public static readonly Field HasReasoning = FindByName("HasReasoning");

        /// <summary>调用耗时</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedTime = FindByName("CreatedTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得聊天记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>记录ID</summary>
        public const String Id = "Id";

        /// <summary>会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>API风格</summary>
        public const String Style = "Style";

        /// <summary>模型名称</summary>
        public const String Model = "Model";

        /// <summary>请求方法</summary>
        public const String RequestMethod = "RequestMethod";

        /// <summary>请求路径</summary>
        public const String RequestPath = "RequestPath";

        /// <summary>请求头JSON</summary>
        public const String RequestHeaders = "RequestHeaders";

        /// <summary>请求体JSON</summary>
        public const String RequestBody = "RequestBody";

        /// <summary>响应状态码</summary>
        public const String ResponseStatus = "ResponseStatus";

        /// <summary>响应头JSON</summary>
        public const String ResponseHeaders = "ResponseHeaders";

        /// <summary>响应体JSON</summary>
        public const String ResponseBody = "ResponseBody";

        /// <summary>温度参数</summary>
        public const String Temperature = "Temperature";

        /// <summary>最大token</summary>
        public const String MaxTokens = "MaxTokens";

        /// <summary>消息数量</summary>
        public const String MessageCount = "MessageCount";

        /// <summary>工具调用次数</summary>
        public const String ToolCallCount = "ToolCallCount";

        /// <summary>是否有reasoning</summary>
        public const String HasReasoning = "HasReasoning";

        /// <summary>调用耗时</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>创建时间</summary>
        public const String CreatedTime = "CreatedTime";
    }
    #endregion
}
