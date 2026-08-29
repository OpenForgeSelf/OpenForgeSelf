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

/// <summary>聊天会话（统一聚合单元）。app 自有聊天与代理录制都归属到此表，靠 SessionKey + Source 区分。</summary>
[Serializable]
[DataObject]
[Description("聊天会话。")]
[BindIndex("UX_ChatSession_SessionKey", true, "SessionKey")]
[BindIndex("IX_ChatSession_UpdatedTime", false, "UpdatedTime")]
[BindIndex("IX_ChatSession_Source", false, "Source")]
[BindIndex("IX_ChatSession_ClientKind", false, "ClientKind")]
[BindTable("ChatSession", Description = "聊天会话", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class ChatSession : IChatSessionModel, IEntity<IChatSessionModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "会话ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionKey;
    /// <summary>统一稳定会话键（app=其 SessionId；代理=ResolveConversationKey 结果）</summary>
    [DisplayName("会话键")]
    [Description("统一稳定会话键")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("SessionKey", "统一稳定会话键", "")]
    public String SessionKey { get => _SessionKey; set { if (OnPropertyChanging("SessionKey", value)) { _SessionKey = value; OnPropertyChanged("SessionKey"); } } }

    private String _Source;
    /// <summary>会话来源：App / Proxy</summary>
    [DisplayName("会话来源")]
    [Description("会话来源：App / Proxy")]
    [DataObjectField(false, false, false, 16)]
    [BindColumn("Source", "会话来源", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _Title;
    /// <summary>会话标题（首条用户消息预览或模型名）</summary>
    [DisplayName("会话标题")]
    [Description("会话标题")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Title", "会话标题", "")]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Model;
    /// <summary>模型名称</summary>
    [DisplayName("模型名称")]
    [Description("模型名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Model", "模型名称", "")]
    public String Model { get => _Model; set { if (OnPropertyChanging("Model", value)) { _Model = value; OnPropertyChanged("Model"); } } }

    private String _Provider;
    /// <summary>上游提供方（代理场景）</summary>
    [DisplayName("上游提供方")]
    [Description("上游提供方")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Provider", "上游提供方", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String _Style;
    /// <summary>API 风格：OpenAI_Chat / Anthropic / Responses / AppChat</summary>
    [DisplayName("API风格")]
    [Description("API风格")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Style", "API风格", "")]
    public String Style { get => _Style; set { if (OnPropertyChanging("Style", value)) { _Style = value; OnPropertyChanged("Style"); } } }

    private String _ClientKind;
    /// <summary>客户端类型（UA 指纹；app 固定 App）</summary>
    [DisplayName("客户端类型")]
    [Description("客户端类型")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("ClientKind", "客户端类型", "")]
    public String ClientKind { get => _ClientKind; set { if (OnPropertyChanging("ClientKind", value)) { _ClientKind = value; OnPropertyChanged("ClientKind"); } } }

    private Int32 _RequestCount;
    /// <summary>轮次数量（代理=请求数；app=消息对数）</summary>
    [DisplayName("轮次数量")]
    [Description("轮次数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RequestCount", "轮次数量", "")]
    public Int32 RequestCount { get => _RequestCount; set { if (OnPropertyChanging("RequestCount", value)) { _RequestCount = value; OnPropertyChanged("RequestCount"); } } }

    private Int32 _MessageCount;
    /// <summary>该会话最新完整消息数</summary>
    [DisplayName("消息数量")]
    [Description("消息数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageCount", "消息数量", "")]
    public Int32 MessageCount { get => _MessageCount; set { if (OnPropertyChanging("MessageCount", value)) { _MessageCount = value; OnPropertyChanged("MessageCount"); } } }

    private String _FirstUserMsg;
    /// <summary>首条用户消息预览</summary>
    [DisplayName("首条用户消息")]
    [Description("首条用户消息预览")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("FirstUserMsg", "首条用户消息预览", "")]
    public String FirstUserMsg { get => _FirstUserMsg; set { if (OnPropertyChanging("FirstUserMsg", value)) { _FirstUserMsg = value; OnPropertyChanged("FirstUserMsg"); } } }

    private Int64 _TotalPromptTokens;
    /// <summary>累计输入 tokens</summary>
    [DisplayName("累计输入Tokens")]
    [Description("累计输入tokens")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalPromptTokens", "累计输入tokens", "")]
    public Int64 TotalPromptTokens { get => _TotalPromptTokens; set { if (OnPropertyChanging("TotalPromptTokens", value)) { _TotalPromptTokens = value; OnPropertyChanged("TotalPromptTokens"); } } }

    private Int64 _TotalCompletionTokens;
    /// <summary>累计输出 tokens</summary>
    [DisplayName("累计输出Tokens")]
    [Description("累计输出tokens")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCompletionTokens", "累计输出tokens", "")]
    public Int64 TotalCompletionTokens { get => _TotalCompletionTokens; set { if (OnPropertyChanging("TotalCompletionTokens", value)) { _TotalCompletionTokens = value; OnPropertyChanged("TotalCompletionTokens"); } } }

    private Int32 _LastStatus;
    /// <summary>末轮 HTTP 状态</summary>
    [DisplayName("末轮状态")]
    [Description("末轮HTTP状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LastStatus", "末轮HTTP状态", "")]
    public Int32 LastStatus { get => _LastStatus; set { if (OnPropertyChanging("LastStatus", value)) { _LastStatus = value; OnPropertyChanged("LastStatus"); } } }

    private DateTime _CreatedTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedTime", "创建时间", "")]
    public DateTime CreatedTime { get => _CreatedTime; set { if (OnPropertyChanging("CreatedTime", value)) { _CreatedTime = value; OnPropertyChanged("CreatedTime"); } } }

    private DateTime _UpdatedTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedTime", "更新时间", "")]
    public DateTime UpdatedTime { get => _UpdatedTime; set { if (OnPropertyChanging("UpdatedTime", value)) { _UpdatedTime = value; OnPropertyChanged("UpdatedTime"); } } }
    #endregion

    #region 枚举视图（不落库，仅解析；数据库值仍为字符串）
    /// <summary>客户端类型枚举视图。</summary>
    [ScriptIgnore, XmlIgnore]
    public ClientKind ClientKindEnum
    {
        get => Enum.TryParse<ClientKind>(ClientKind, true, out var v) ? v : global::ForgeSelf.Api.Entities.ClientKind.Unknown;
        set => ClientKind = value.ToString();
    }

    /// <summary>会话来源枚举视图。</summary>
    [ScriptIgnore, XmlIgnore]
    public SessionSource SourceEnum
    {
        get => Enum.TryParse<SessionSource>(Source, true, out var v) ? v : SessionSource.Proxy;
        set => Source = value.ToString();
    }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    public void Copy(IChatSessionModel model)
    {
        Id = model.Id;
        SessionKey = model.SessionKey;
        Source = model.Source;
        Title = model.Title;
        Model = model.Model;
        Provider = model.Provider;
        Style = model.Style;
        ClientKind = model.ClientKind;
        RequestCount = model.RequestCount;
        MessageCount = model.MessageCount;
        FirstUserMsg = model.FirstUserMsg;
        TotalPromptTokens = model.TotalPromptTokens;
        TotalCompletionTokens = model.TotalCompletionTokens;
        LastStatus = model.LastStatus;
        CreatedTime = model.CreatedTime;
        UpdatedTime = model.UpdatedTime;
    }
    #endregion

    #region 获取/设置 字段值
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "SessionKey" => _SessionKey,
            "Source" => _Source,
            "Title" => _Title,
            "Model" => _Model,
            "Provider" => _Provider,
            "Style" => _Style,
            "ClientKind" => _ClientKind,
            "RequestCount" => _RequestCount,
            "MessageCount" => _MessageCount,
            "FirstUserMsg" => _FirstUserMsg,
            "TotalPromptTokens" => _TotalPromptTokens,
            "TotalCompletionTokens" => _TotalCompletionTokens,
            "LastStatus" => _LastStatus,
            "CreatedTime" => _CreatedTime,
            "UpdatedTime" => _UpdatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SessionKey": _SessionKey = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "Style": _Style = Convert.ToString(value); break;
                case "ClientKind": _ClientKind = Convert.ToString(value); break;
                case "RequestCount": _RequestCount = value.ToInt(); break;
                case "MessageCount": _MessageCount = value.ToInt(); break;
                case "FirstUserMsg": _FirstUserMsg = Convert.ToString(value); break;
                case "TotalPromptTokens": _TotalPromptTokens = value.ToLong(); break;
                case "TotalCompletionTokens": _TotalCompletionTokens = value.ToLong(); break;
                case "LastStatus": _LastStatus = value.ToInt(); break;
                case "CreatedTime": _CreatedTime = value.ToDateTime(); break;
                case "UpdatedTime": _UpdatedTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>根据会话ID查找</summary>
    public static ChatSession FindById(Int64 id)
    {
        if (id < 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据统一会话键查找（大小写不敏感）</summary>
    public static ChatSession? FindBySessionKey(String sessionKey)
    {
        if (sessionKey.IsNullOrEmpty()) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.SessionKey.EqualIgnoreCase(sessionKey));
        return Find(_.SessionKey == sessionKey);
    }
    #endregion

    #region 高级查询
    /// <summary>分页查询会话</summary>
    public static IList<ChatSession> Search(String? source, String? clientKind, String? style, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (!source.IsNullOrEmpty()) exp &= _.Source == source;
        if (!clientKind.IsNullOrEmpty()) exp &= _.ClientKind == clientKind;
        if (!style.IsNullOrEmpty()) exp &= _.Style == style;
        exp &= _.CreatedTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.SessionKey.StartsWith(key) | _.Title.Contains(key) | _.FirstUserMsg.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field SessionKey = FindByName("SessionKey");
        public static readonly Field Source = FindByName("Source");
        public static readonly Field Title = FindByName("Title");
        public static readonly Field Model = FindByName("Model");
        public static readonly Field Provider = FindByName("Provider");
        public static readonly Field Style = FindByName("Style");
        public static readonly Field ClientKind = FindByName("ClientKind");
        public static readonly Field RequestCount = FindByName("RequestCount");
        public static readonly Field MessageCount = FindByName("MessageCount");
        public static readonly Field FirstUserMsg = FindByName("FirstUserMsg");
        public static readonly Field TotalPromptTokens = FindByName("TotalPromptTokens");
        public static readonly Field TotalCompletionTokens = FindByName("TotalCompletionTokens");
        public static readonly Field LastStatus = FindByName("LastStatus");
        public static readonly Field CreatedTime = FindByName("CreatedTime");
        public static readonly Field UpdatedTime = FindByName("UpdatedTime");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    public partial class __
    {
        public const String Id = "Id";
        public const String SessionKey = "SessionKey";
        public const String Source = "Source";
        public const String Title = "Title";
        public const String Model = "Model";
        public const String Provider = "Provider";
        public const String Style = "Style";
        public const String ClientKind = "ClientKind";
        public const String RequestCount = "RequestCount";
        public const String MessageCount = "MessageCount";
        public const String FirstUserMsg = "FirstUserMsg";
        public const String TotalPromptTokens = "TotalPromptTokens";
        public const String TotalCompletionTokens = "TotalCompletionTokens";
        public const String LastStatus = "LastStatus";
        public const String CreatedTime = "CreatedTime";
        public const String UpdatedTime = "UpdatedTime";
    }
    #endregion
}

/// <summary>聊天会话模型接口（供 XCode 实体实现）。</summary>
public partial interface IChatSessionModel
{
    Int64 Id { get; set; }
    String SessionKey { get; set; }
    String Source { get; set; }
    String Title { get; set; }
    String Model { get; set; }
    String Provider { get; set; }
    String Style { get; set; }
    String ClientKind { get; set; }
    Int32 RequestCount { get; set; }
    Int32 MessageCount { get; set; }
    String FirstUserMsg { get; set; }
    Int64 TotalPromptTokens { get; set; }
    Int64 TotalCompletionTokens { get; set; }
    Int32 LastStatus { get; set; }
    DateTime CreatedTime { get; set; }
    DateTime UpdatedTime { get; set; }
}
