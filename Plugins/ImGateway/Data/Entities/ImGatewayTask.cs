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

namespace ForgeSelf.Api.Plugins.ImGateway.Entities;

/// <summary>{name}。IM 网关派发任务绑定（PILOT-079）</summary>
[Serializable]
[DataObject]
[Description("{name}。IM 网关派发任务绑定（PILOT-079）")]
[BindIndex("IX_ImGatewayTask_ChannelKey_Id", false, "ChannelKey,Id")]
[BindTable("ImGatewayTask", Description = "IM 网关派发任务绑定（PILOT-079）", ConnName = "ImGateway", DbType = DatabaseType.None)]
public partial class ImGatewayTask
{
    #region 属性
    private Int64 _Id;
    /// <summary>绑定ID</summary>
    [DisplayName("绑定ID")]
    [Description("绑定ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "绑定ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _TaskKey;
    /// <summary>派发任务键（契约 taskKey）</summary>
    [DisplayName("派发任务键（契约taskKey）")]
    [Description("派发任务键（契约 taskKey）")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("TaskKey", "派发任务键（契约 taskKey）", "")]
    public String TaskKey { get => _TaskKey; set { if (OnPropertyChanging("TaskKey", value)) { _TaskKey = value; OnPropertyChanged("TaskKey"); } } }

    private Int64 _RunId;
    /// <summary>MAF RunId（捕获后回填，0=未捕获）</summary>
    [DisplayName("MAFRunId（捕获后回填")]
    [Description("MAF RunId（捕获后回填，0=未捕获）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RunId", "MAF RunId（捕获后回填，0=未捕获）", "")]
    public Int64 RunId { get => _RunId; set { if (OnPropertyChanging("RunId", value)) { _RunId = value; OnPropertyChanged("RunId"); } } }

    private String _ChannelKey;
    /// <summary>通道会话用户复合键</summary>
    [DisplayName("通道会话用户复合键")]
    [Description("通道会话用户复合键")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("ChannelKey", "通道会话用户复合键", "", Master = true)]
    public String ChannelKey { get => _ChannelKey; set { if (OnPropertyChanging("ChannelKey", value)) { _ChannelKey = value; OnPropertyChanged("ChannelKey"); } } }

    private String _ChannelType;
    /// <summary>通道类型</summary>
    [DisplayName("通道类型")]
    [Description("通道类型")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("ChannelType", "通道类型", "")]
    public String ChannelType { get => _ChannelType; set { if (OnPropertyChanging("ChannelType", value)) { _ChannelType = value; OnPropertyChanged("ChannelType"); } } }

    private String _ConversationId;
    /// <summary>会话标识</summary>
    [DisplayName("会话标识")]
    [Description("会话标识")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("ConversationId", "会话标识", "")]
    public String ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String _PlatformUserId;
    /// <summary>平台用户标识</summary>
    [DisplayName("平台用户标识")]
    [Description("平台用户标识")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("PlatformUserId", "平台用户标识", "")]
    public String PlatformUserId { get => _PlatformUserId; set { if (OnPropertyChanging("PlatformUserId", value)) { _PlatformUserId = value; OnPropertyChanged("PlatformUserId"); } } }

    private String _ChatType;
    /// <summary>聊天形态（single/group，主动推送用）</summary>
    [DisplayName("聊天形态（single_group")]
    [Description("聊天形态（single/group，主动推送用）")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ChatType", "聊天形态（single/group，主动推送用）", "")]
    public String ChatType { get => _ChatType; set { if (OnPropertyChanging("ChatType", value)) { _ChatType = value; OnPropertyChanged("ChatType"); } } }

    private String _ReplyReqId;
    /// <summary>入站消息 req_id（即时回执用）</summary>
    [DisplayName("入站消息req_id（即时回执用）")]
    [Description("入站消息 req_id（即时回执用）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ReplyReqId", "入站消息 req_id（即时回执用）", "")]
    public String ReplyReqId { get => _ReplyReqId; set { if (OnPropertyChanging("ReplyReqId", value)) { _ReplyReqId = value; OnPropertyChanged("ReplyReqId"); } } }

    private String _InboundMsgId;
    /// <summary>触发入站消息ID</summary>
    [DisplayName("触发入站消息ID")]
    [Description("触发入站消息ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("InboundMsgId", "触发入站消息ID", "")]
    public String InboundMsgId { get => _InboundMsgId; set { if (OnPropertyChanging("InboundMsgId", value)) { _InboundMsgId = value; OnPropertyChanged("InboundMsgId"); } } }

    private String _TaskInput;
    /// <summary>任务原文摘要（200 字）</summary>
    [DisplayName("任务原文摘要（200字）")]
    [Description("任务原文摘要（200 字）")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("TaskInput", "任务原文摘要（200 字）", "")]
    public String TaskInput { get => _TaskInput; set { if (OnPropertyChanging("TaskInput", value)) { _TaskInput = value; OnPropertyChanged("TaskInput"); } } }

    private String _Status;
    /// <summary>状态（任务中心统一小写词表）</summary>
    [DisplayName("状态（任务中心统一小写词表）")]
    [Description("状态（任务中心统一小写词表）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Status", "状态（任务中心统一小写词表）", "", DefaultValue = "pending")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _RepliedAt;
    /// <summary>终态回复时间（幂等防重发）</summary>
    [DisplayName("终态回复时间（幂等防重发）")]
    [Description("终态回复时间（幂等防重发）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RepliedAt", "终态回复时间（幂等防重发）", "")]
    public DateTime RepliedAt { get => _RepliedAt; set { if (OnPropertyChanging("RepliedAt", value)) { _RepliedAt = value; OnPropertyChanged("RepliedAt"); } } }

    private String _PromptedApprovalIds;
    /// <summary>已提示审批 Id（逗号分隔，上限 10）</summary>
    [DisplayName("已提示审批Id（逗号分隔")]
    [Description("已提示审批 Id（逗号分隔，上限 10）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("PromptedApprovalIds", "已提示审批 Id（逗号分隔，上限 10）", "")]
    public String PromptedApprovalIds { get => _PromptedApprovalIds; set { if (OnPropertyChanging("PromptedApprovalIds", value)) { _PromptedApprovalIds = value; OnPropertyChanged("PromptedApprovalIds"); } } }
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
            "RunId" => _RunId,
            "ChannelKey" => _ChannelKey,
            "ChannelType" => _ChannelType,
            "ConversationId" => _ConversationId,
            "PlatformUserId" => _PlatformUserId,
            "ChatType" => _ChatType,
            "ReplyReqId" => _ReplyReqId,
            "InboundMsgId" => _InboundMsgId,
            "TaskInput" => _TaskInput,
            "Status" => _Status,
            "CreatedAt" => _CreatedAt,
            "RepliedAt" => _RepliedAt,
            "PromptedApprovalIds" => _PromptedApprovalIds,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "TaskKey": _TaskKey = Convert.ToString(value); break;
                case "RunId": _RunId = value.ToLong(); break;
                case "ChannelKey": _ChannelKey = Convert.ToString(value); break;
                case "ChannelType": _ChannelType = Convert.ToString(value); break;
                case "ConversationId": _ConversationId = Convert.ToString(value); break;
                case "PlatformUserId": _PlatformUserId = Convert.ToString(value); break;
                case "ChatType": _ChatType = Convert.ToString(value); break;
                case "ReplyReqId": _ReplyReqId = Convert.ToString(value); break;
                case "InboundMsgId": _InboundMsgId = Convert.ToString(value); break;
                case "TaskInput": _TaskInput = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "RepliedAt": _RepliedAt = value.ToDateTime(); break;
                case "PromptedApprovalIds": _PromptedApprovalIds = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据绑定ID查找</summary>
    /// <param name="id">绑定ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewayTask FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据通道会话用户复合键查找</summary>
    /// <param name="channelKey">通道会话用户复合键</param>
    /// <returns>实体列表</returns>
    public static IList<ImGatewayTask> FindAllByChannelKey(String channelKey)
    {
        if (channelKey.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ChannelKey.EqualIgnoreCase(channelKey));

        return FindAll(_.ChannelKey == channelKey);
    }
    #endregion

    #region 字段名
    /// <summary>取得IM网关派发任务绑定（PILOT-079）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>绑定ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>派发任务键（契约 taskKey）</summary>
        public static readonly Field TaskKey = FindByName("TaskKey");

        /// <summary>MAF RunId（捕获后回填，0=未捕获）</summary>
        public static readonly Field RunId = FindByName("RunId");

        /// <summary>通道会话用户复合键</summary>
        public static readonly Field ChannelKey = FindByName("ChannelKey");

        /// <summary>通道类型</summary>
        public static readonly Field ChannelType = FindByName("ChannelType");

        /// <summary>会话标识</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>平台用户标识</summary>
        public static readonly Field PlatformUserId = FindByName("PlatformUserId");

        /// <summary>聊天形态（single/group，主动推送用）</summary>
        public static readonly Field ChatType = FindByName("ChatType");

        /// <summary>入站消息 req_id（即时回执用）</summary>
        public static readonly Field ReplyReqId = FindByName("ReplyReqId");

        /// <summary>触发入站消息ID</summary>
        public static readonly Field InboundMsgId = FindByName("InboundMsgId");

        /// <summary>任务原文摘要（200 字）</summary>
        public static readonly Field TaskInput = FindByName("TaskInput");

        /// <summary>状态（任务中心统一小写词表）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>终态回复时间（幂等防重发）</summary>
        public static readonly Field RepliedAt = FindByName("RepliedAt");

        /// <summary>已提示审批 Id（逗号分隔，上限 10）</summary>
        public static readonly Field PromptedApprovalIds = FindByName("PromptedApprovalIds");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得IM网关派发任务绑定（PILOT-079）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>绑定ID</summary>
        public const String Id = "Id";

        /// <summary>派发任务键（契约 taskKey）</summary>
        public const String TaskKey = "TaskKey";

        /// <summary>MAF RunId（捕获后回填，0=未捕获）</summary>
        public const String RunId = "RunId";

        /// <summary>通道会话用户复合键</summary>
        public const String ChannelKey = "ChannelKey";

        /// <summary>通道类型</summary>
        public const String ChannelType = "ChannelType";

        /// <summary>会话标识</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>平台用户标识</summary>
        public const String PlatformUserId = "PlatformUserId";

        /// <summary>聊天形态（single/group，主动推送用）</summary>
        public const String ChatType = "ChatType";

        /// <summary>入站消息 req_id（即时回执用）</summary>
        public const String ReplyReqId = "ReplyReqId";

        /// <summary>触发入站消息ID</summary>
        public const String InboundMsgId = "InboundMsgId";

        /// <summary>任务原文摘要（200 字）</summary>
        public const String TaskInput = "TaskInput";

        /// <summary>状态（任务中心统一小写词表）</summary>
        public const String Status = "Status";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>终态回复时间（幂等防重发）</summary>
        public const String RepliedAt = "RepliedAt";

        /// <summary>已提示审批 Id（逗号分隔，上限 10）</summary>
        public const String PromptedApprovalIds = "PromptedApprovalIds";
    }
    #endregion
}
