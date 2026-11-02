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

/// <summary>{name}。IM 网关消息记录（收/发双向，含失败态）</summary>
[Serializable]
[DataObject]
[Description("{name}。IM 网关消息记录（收/发双向，含失败态）")]
[BindIndex("IX_ImGatewayMessage_ChannelKey_Id", false, "ChannelKey,Id")]
[BindTable("ImGatewayMessage", Description = "IM 网关消息记录（收/发双向，含失败态）", ConnName = "ImGateway", DbType = DatabaseType.None)]
public partial class ImGatewayMessage
{
    #region 属性
    private Int64 _Id;
    /// <summary>消息ID</summary>
    [DisplayName("消息ID")]
    [Description("消息ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "消息ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

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

    private String _Direction;
    /// <summary>方向 in=收到 / out=回复</summary>
    [DisplayName("方向in=收到_out=回复")]
    [Description("方向 in=收到 / out=回复")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Direction", "方向 in=收到 / out=回复", "")]
    public String Direction { get => _Direction; set { if (OnPropertyChanging("Direction", value)) { _Direction = value; OnPropertyChanged("Direction"); } } }

    private String _Content;
    /// <summary>消息内容（回复失败时为空串）</summary>
    [DisplayName("消息内容（回复失败时为空串）")]
    [Description("消息内容（回复失败时为空串）")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("Content", "消息内容（回复失败时为空串）", "")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private String _SessionId;
    /// <summary>AI Agent 会话ID</summary>
    [DisplayName("AIAgent会话ID")]
    [Description("AI Agent 会话ID")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("SessionId", "AI Agent 会话ID", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _PlatformMsgId;
    /// <summary>平台消息ID</summary>
    [DisplayName("平台消息ID")]
    [Description("平台消息ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("PlatformMsgId", "平台消息ID", "")]
    public String PlatformMsgId { get => _PlatformMsgId; set { if (OnPropertyChanging("PlatformMsgId", value)) { _PlatformMsgId = value; OnPropertyChanged("PlatformMsgId"); } } }

    private String _Status;
    /// <summary>状态 ok / error</summary>
    [DisplayName("状态ok_error")]
    [Description("状态 ok / error")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Status", "状态 ok / error", "", DefaultValue = "ok")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _ErrorText;
    /// <summary>失败原因摘要（error 态）</summary>
    [DisplayName("失败原因摘要（error态）")]
    [Description("失败原因摘要（error 态）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("ErrorText", "失败原因摘要（error 态）", "")]
    public String ErrorText { get => _ErrorText; set { if (OnPropertyChanging("ErrorText", value)) { _ErrorText = value; OnPropertyChanged("ErrorText"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
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
            "ChannelKey" => _ChannelKey,
            "ChannelType" => _ChannelType,
            "Direction" => _Direction,
            "Content" => _Content,
            "SessionId" => _SessionId,
            "PlatformMsgId" => _PlatformMsgId,
            "Status" => _Status,
            "ErrorText" => _ErrorText,
            "CreatedAt" => _CreatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ChannelKey": _ChannelKey = Convert.ToString(value); break;
                case "ChannelType": _ChannelType = Convert.ToString(value); break;
                case "Direction": _Direction = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "PlatformMsgId": _PlatformMsgId = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "ErrorText": _ErrorText = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据消息ID查找</summary>
    /// <param name="id">消息ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewayMessage FindById(Int64 id)
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
    public static IList<ImGatewayMessage> FindAllByChannelKey(String channelKey)
    {
        if (channelKey.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ChannelKey.EqualIgnoreCase(channelKey));

        return FindAll(_.ChannelKey == channelKey);
    }
    #endregion

    #region 字段名
    /// <summary>取得IM网关消息记录（收_发双向字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>消息ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>通道会话用户复合键</summary>
        public static readonly Field ChannelKey = FindByName("ChannelKey");

        /// <summary>通道类型</summary>
        public static readonly Field ChannelType = FindByName("ChannelType");

        /// <summary>方向 in=收到 / out=回复</summary>
        public static readonly Field Direction = FindByName("Direction");

        /// <summary>消息内容（回复失败时为空串）</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>AI Agent 会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>平台消息ID</summary>
        public static readonly Field PlatformMsgId = FindByName("PlatformMsgId");

        /// <summary>状态 ok / error</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>失败原因摘要（error 态）</summary>
        public static readonly Field ErrorText = FindByName("ErrorText");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得IM网关消息记录（收_发双向字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>消息ID</summary>
        public const String Id = "Id";

        /// <summary>通道会话用户复合键</summary>
        public const String ChannelKey = "ChannelKey";

        /// <summary>通道类型</summary>
        public const String ChannelType = "ChannelType";

        /// <summary>方向 in=收到 / out=回复</summary>
        public const String Direction = "Direction";

        /// <summary>消息内容（回复失败时为空串）</summary>
        public const String Content = "Content";

        /// <summary>AI Agent 会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>平台消息ID</summary>
        public const String PlatformMsgId = "PlatformMsgId";

        /// <summary>状态 ok / error</summary>
        public const String Status = "Status";

        /// <summary>失败原因摘要（error 态）</summary>
        public const String ErrorText = "ErrorText";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
    }
    #endregion
}
