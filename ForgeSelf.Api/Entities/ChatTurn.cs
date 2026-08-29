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

namespace ForgeSelf.Api.Entities;

/// <summary>聊天轮次（会话内的一轮请求-响应）。原 ChatRecord 改名而来。</summary>
[Serializable]
[DataObject]
[Description("聊天轮次。")]
[BindIndex("IX_ChatTurn_ChatSessionId", false, "ChatSessionId")]
[BindIndex("IX_ChatTurn_ChatSessionId_Turn", false, "ChatSessionId,TurnIndex")]
[BindIndex("IX_ChatTurn_SessionKey", false, "SessionKey")]
[BindIndex("IX_ChatTurn_CreatedTime", false, "CreatedTime")]
[BindIndex("IX_ChatTurn_Style", false, "Style")]
[BindIndex("IX_ChatTurn_Model", false, "Model")]
[BindIndex("IX_ChatTurn_RequestId", false, "RequestId")]
[BindTable("ChatTurn", Description = "聊天轮次", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class ChatTurn : IChatTurnModel, IEntity<IChatTurnModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>轮次ID</summary>
    [DisplayName("轮次ID")]
    [Description("轮次ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "轮次ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ChatSessionId;
    /// <summary>所属会话ID（→ ChatSession.Id）</summary>
    [DisplayName("所属会话ID")]
    [Description("所属会话ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ChatSessionId", "所属会话ID", "")]
    public Int64 ChatSessionId { get => _ChatSessionId; set { if (OnPropertyChanging("ChatSessionId", value)) { _ChatSessionId = value; OnPropertyChanged("ChatSessionId"); } } }

    private Int32 _TurnIndex;
    /// <summary>会话内第几轮（从 1 递增）</summary>
    [DisplayName("轮次序号")]
    [Description("会话内第几轮")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TurnIndex", "会话内第几轮", "")]
    public Int32 TurnIndex { get => _TurnIndex; set { if (OnPropertyChanging("TurnIndex", value)) { _TurnIndex = value; OnPropertyChanged("TurnIndex"); } } }

    private String _SessionKey;
    /// <summary>统一会话键（= ChatSession.SessionKey，冗余以便列表/WS 免 JOIN）</summary>
    [DisplayName("会话键")]
    [Description("统一会话键")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("SessionKey", "统一会话键", "")]
    public String SessionKey { get => _SessionKey; set { if (OnPropertyChanging("SessionKey", value)) { _SessionKey = value; OnPropertyChanged("SessionKey"); } } }

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
    /// <summary>请求体JSON（含 messages 数组，便于后续分析）</summary>
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

    private String _RequestId;
    /// <summary>流式请求关联ID（用于流式期间按同一请求追加更新）</summary>
    [DisplayName("流式请求关联ID")]
    [Description("流式请求关联ID")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("RequestId", "流式请求关联ID", "")]
    public String RequestId { get => _RequestId; set { if (OnPropertyChanging("RequestId", value)) { _RequestId = value; OnPropertyChanged("RequestId"); } } }

    private String _ResponseText;
    /// <summary>实时/增量纯文本回复</summary>
    [DisplayName("实时回复文本")]
    [Description("实时/增量纯文本回复")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ResponseText", "实时/增量纯文本回复", "")]
    public String ResponseText { get => _ResponseText; set { if (OnPropertyChanging("ResponseText", value)) { _ResponseText = value; OnPropertyChanged("ResponseText"); } } }

    private String _UserPreview;
    /// <summary>用户消息速览（列表展示用）</summary>
    [DisplayName("用户消息速览")]
    [Description("用户消息速览")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("UserPreview", "用户消息速览", "")]
    public String UserPreview { get => _UserPreview; set { if (OnPropertyChanging("UserPreview", value)) { _UserPreview = value; OnPropertyChanged("UserPreview"); } } }

    private String _AssistantPreview;
    /// <summary>助手回复速览（列表展示用）</summary>
    [DisplayName("助手回复速览")]
    [Description("助手回复速览")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AssistantPreview", "助手回复速览", "")]
    public String AssistantPreview { get => _AssistantPreview; set { if (OnPropertyChanging("AssistantPreview", value)) { _AssistantPreview = value; OnPropertyChanged("AssistantPreview"); } } }

    private Int32 _PromptTokens;
    /// <summary>本轮输入 tokens（分析用，直接落列）</summary>
    [DisplayName("输入Tokens")]
    [Description("本轮输入tokens")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PromptTokens", "本轮输入tokens", "")]
    public Int32 PromptTokens { get => _PromptTokens; set { if (OnPropertyChanging("PromptTokens", value)) { _PromptTokens = value; OnPropertyChanged("PromptTokens"); } } }

    private Int32 _CompletionTokens;
    /// <summary>本轮输出 tokens</summary>
    [DisplayName("输出Tokens")]
    [Description("本轮输出tokens")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CompletionTokens", "本轮输出tokens", "")]
    public Int32 CompletionTokens { get => _CompletionTokens; set { if (OnPropertyChanging("CompletionTokens", value)) { _CompletionTokens = value; OnPropertyChanged("CompletionTokens"); } } }

    private Int32 _TotalTokens;
    /// <summary>本轮总 tokens</summary>
    [DisplayName("总Tokens")]
    [Description("本轮总tokens")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "本轮总tokens", "")]
    public Int32 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private Int64 _FirstTokenMs;
    /// <summary>首 token 延迟（流式场景，分析响应速度）</summary>
    [DisplayName("首Token延迟")]
    [Description("首token延迟")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("FirstTokenMs", "首token延迟", "")]
    public Int64 FirstTokenMs { get => _FirstTokenMs; set { if (OnPropertyChanging("FirstTokenMs", value)) { _FirstTokenMs = value; OnPropertyChanged("FirstTokenMs"); } } }

    private String _ErrorMessage;
    /// <summary>失败原因（便于失败率分析）</summary>
    [DisplayName("失败原因")]
    [Description("失败原因")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("ErrorMessage", "失败原因", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

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
    /// <summary>本轮请求携带的消息数量（= 该会话当前完整长度）</summary>
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
    public void Copy(IChatTurnModel model)
    {
        Id = model.Id;
        ChatSessionId = model.ChatSessionId;
        TurnIndex = model.TurnIndex;
        SessionKey = model.SessionKey;
        Style = model.Style;
        Model = model.Model;
        RequestMethod = model.RequestMethod;
        RequestPath = model.RequestPath;
        RequestHeaders = model.RequestHeaders;
        RequestBody = model.RequestBody;
        ResponseStatus = model.ResponseStatus;
        ResponseHeaders = model.ResponseHeaders;
        ResponseBody = model.ResponseBody;
        RequestId = model.RequestId;
        ResponseText = model.ResponseText;
        UserPreview = model.UserPreview;
        AssistantPreview = model.AssistantPreview;
        PromptTokens = model.PromptTokens;
        CompletionTokens = model.CompletionTokens;
        TotalTokens = model.TotalTokens;
        FirstTokenMs = model.FirstTokenMs;
        ErrorMessage = model.ErrorMessage;
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
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "ChatSessionId" => _ChatSessionId,
            "TurnIndex" => _TurnIndex,
            "SessionKey" => _SessionKey,
            "Style" => _Style,
            "Model" => _Model,
            "RequestMethod" => _RequestMethod,
            "RequestPath" => _RequestPath,
            "RequestHeaders" => _RequestHeaders,
            "RequestBody" => _RequestBody,
            "ResponseStatus" => _ResponseStatus,
            "ResponseHeaders" => _ResponseHeaders,
            "ResponseBody" => _ResponseBody,
            "RequestId" => _RequestId,
            "ResponseText" => _ResponseText,
            "UserPreview" => _UserPreview,
            "AssistantPreview" => _AssistantPreview,
            "PromptTokens" => _PromptTokens,
            "CompletionTokens" => _CompletionTokens,
            "TotalTokens" => _TotalTokens,
            "FirstTokenMs" => _FirstTokenMs,
            "ErrorMessage" => _ErrorMessage,
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
                case "ChatSessionId": _ChatSessionId = value.ToLong(); break;
                case "TurnIndex": _TurnIndex = value.ToInt(); break;
                case "SessionKey": _SessionKey = Convert.ToString(value); break;
                case "Style": _Style = Convert.ToString(value); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "RequestMethod": _RequestMethod = Convert.ToString(value); break;
                case "RequestPath": _RequestPath = Convert.ToString(value); break;
                case "RequestHeaders": _RequestHeaders = Convert.ToString(value); break;
                case "RequestBody": _RequestBody = Convert.ToString(value); break;
                case "ResponseStatus": _ResponseStatus = value.ToInt(); break;
                case "ResponseHeaders": _ResponseHeaders = Convert.ToString(value); break;
                case "ResponseBody": _ResponseBody = Convert.ToString(value); break;
                case "RequestId": _RequestId = Convert.ToString(value); break;
                case "ResponseText": _ResponseText = Convert.ToString(value); break;
                case "UserPreview": _UserPreview = Convert.ToString(value); break;
                case "AssistantPreview": _AssistantPreview = Convert.ToString(value); break;
                case "PromptTokens": _PromptTokens = value.ToInt(); break;
                case "CompletionTokens": _CompletionTokens = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToInt(); break;
                case "FirstTokenMs": _FirstTokenMs = value.ToLong(); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
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

    #region 扩展查询
    public static ChatTurn FindById(Int64 id)
    {
        if (id < 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>按所属会话ID查找轮次</summary>
    public static IList<ChatTurn> FindAllByChatSessionId(Int64 chatSessionId)
    {
        if (chatSessionId <= 0) return [];
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ChatSessionId == chatSessionId);
        return FindAll(_.ChatSessionId == chatSessionId);
    }

    public static IList<ChatTurn> FindAllByStyle(String style)
    {
        if (style.IsNullOrEmpty()) return [];
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Style.EqualIgnoreCase(style));
        return FindAll(_.Style == style);
    }

    public static IList<ChatTurn> FindAllByRequestId(String requestId)
    {
        if (requestId.IsNullOrEmpty()) return [];
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.RequestId.EqualIgnoreCase(requestId));
        return FindAll(_.RequestId == requestId);
    }
    #endregion

    #region 高级查询
    public static IList<ChatTurn> Search(Int64 chatSessionId, String style, String requestId, Boolean? hasReasoning, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (chatSessionId > 0) exp &= _.ChatSessionId == chatSessionId;
        if (!style.IsNullOrEmpty()) exp &= _.Style == style;
        if (!requestId.IsNullOrEmpty()) exp &= _.RequestId == requestId;
        if (hasReasoning != null) exp &= _.HasReasoning == hasReasoning;
        exp &= _.CreatedTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field ChatSessionId = FindByName("ChatSessionId");
        public static readonly Field TurnIndex = FindByName("TurnIndex");
        public static readonly Field SessionKey = FindByName("SessionKey");
        public static readonly Field Style = FindByName("Style");
        public static readonly Field Model = FindByName("Model");
        public static readonly Field RequestMethod = FindByName("RequestMethod");
        public static readonly Field RequestPath = FindByName("RequestPath");
        public static readonly Field RequestHeaders = FindByName("RequestHeaders");
        public static readonly Field RequestBody = FindByName("RequestBody");
        public static readonly Field ResponseStatus = FindByName("ResponseStatus");
        public static readonly Field ResponseHeaders = FindByName("ResponseHeaders");
        public static readonly Field ResponseBody = FindByName("ResponseBody");
        public static readonly Field RequestId = FindByName("RequestId");
        public static readonly Field ResponseText = FindByName("ResponseText");
        public static readonly Field UserPreview = FindByName("UserPreview");
        public static readonly Field AssistantPreview = FindByName("AssistantPreview");
        public static readonly Field PromptTokens = FindByName("PromptTokens");
        public static readonly Field CompletionTokens = FindByName("CompletionTokens");
        public static readonly Field TotalTokens = FindByName("TotalTokens");
        public static readonly Field FirstTokenMs = FindByName("FirstTokenMs");
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");
        public static readonly Field Temperature = FindByName("Temperature");
        public static readonly Field MaxTokens = FindByName("MaxTokens");
        public static readonly Field MessageCount = FindByName("MessageCount");
        public static readonly Field ToolCallCount = FindByName("ToolCallCount");
        public static readonly Field HasReasoning = FindByName("HasReasoning");
        public static readonly Field DurationMs = FindByName("DurationMs");
        public static readonly Field CreatedTime = FindByName("CreatedTime");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    public partial class __
    {
        public const String Id = "Id";
        public const String ChatSessionId = "ChatSessionId";
        public const String TurnIndex = "TurnIndex";
        public const String SessionKey = "SessionKey";
        public const String Style = "Style";
        public const String Model = "Model";
        public const String RequestMethod = "RequestMethod";
        public const String RequestPath = "RequestPath";
        public const String RequestHeaders = "RequestHeaders";
        public const String RequestBody = "RequestBody";
        public const String ResponseStatus = "ResponseStatus";
        public const String ResponseHeaders = "ResponseHeaders";
        public const String ResponseBody = "ResponseBody";
        public const String RequestId = "RequestId";
        public const String ResponseText = "ResponseText";
        public const String UserPreview = "UserPreview";
        public const String AssistantPreview = "AssistantPreview";
        public const String PromptTokens = "PromptTokens";
        public const String CompletionTokens = "CompletionTokens";
        public const String TotalTokens = "TotalTokens";
        public const String FirstTokenMs = "FirstTokenMs";
        public const String ErrorMessage = "ErrorMessage";
        public const String Temperature = "Temperature";
        public const String MaxTokens = "MaxTokens";
        public const String MessageCount = "MessageCount";
        public const String ToolCallCount = "ToolCallCount";
        public const String HasReasoning = "HasReasoning";
        public const String DurationMs = "DurationMs";
        public const String CreatedTime = "CreatedTime";
    }
    #endregion
}
