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

/// <summary>{name}。IM 网关会话映射</summary>
[Serializable]
[DataObject]
[Description("{name}。IM 网关会话映射")]
[BindIndex("IU_ImGatewaySession_ChannelKey", true, "ChannelKey")]
[BindTable("ImGatewaySession", Description = "IM 网关会话映射", ConnName = "ImGateway", DbType = DatabaseType.None)]
public partial class ImGatewaySession
{
    #region 属性
    private Int64 _Id;
    /// <summary>映射ID</summary>
    [DisplayName("映射ID")]
    [Description("映射ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "映射ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _ChannelKey;
    /// <summary>通道会话用户复合键</summary>
    [DisplayName("通道会话用户复合键")]
    [Description("通道会话用户复合键")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("ChannelKey", "通道会话用户复合键", "", Master = true)]
    public String ChannelKey { get => _ChannelKey; set { if (OnPropertyChanging("ChannelKey", value)) { _ChannelKey = value; OnPropertyChanged("ChannelKey"); } } }

    private String _SessionId;
    /// <summary>AI Agent 会话ID</summary>
    [DisplayName("AIAgent会话ID")]
    [Description("AI Agent 会话ID")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("SessionId", "AI Agent 会话ID", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _AgentId;
    /// <summary>绑定 AgentId</summary>
    [DisplayName("绑定AgentId")]
    [Description("绑定 AgentId")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("AgentId", "绑定 AgentId", "")]
    public String AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private String _ModelId;
    /// <summary>绑定聊天模型</summary>
    [DisplayName("绑定聊天模型")]
    [Description("绑定聊天模型")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ModelId", "绑定聊天模型", "")]
    public String ModelId { get => _ModelId; set { if (OnPropertyChanging("ModelId", value)) { _ModelId = value; OnPropertyChanged("ModelId"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
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
            "SessionId" => _SessionId,
            "AgentId" => _AgentId,
            "ModelId" => _ModelId,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ChannelKey": _ChannelKey = Convert.ToString(value); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "AgentId": _AgentId = Convert.ToString(value); break;
                case "ModelId": _ModelId = Convert.ToString(value); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据映射ID查找</summary>
    /// <param name="id">映射ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewaySession FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据通道会话用户复合键查找</summary>
    /// <param name="channelKey">通道会话用户复合键</param>
    /// <returns>实体对象</returns>
    public static ImGatewaySession FindByChannelKey(String channelKey)
    {
        if (channelKey.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ChannelKey.EqualIgnoreCase(channelKey));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(channelKey) as ImGatewaySession;

        //return Find(_.ChannelKey == channelKey);
    }
    #endregion

    #region 字段名
    /// <summary>取得IM网关会话映射字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>映射ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>通道会话用户复合键</summary>
        public static readonly Field ChannelKey = FindByName("ChannelKey");

        /// <summary>AI Agent 会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>绑定 AgentId</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>绑定聊天模型</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得IM网关会话映射字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>映射ID</summary>
        public const String Id = "Id";

        /// <summary>通道会话用户复合键</summary>
        public const String ChannelKey = "ChannelKey";

        /// <summary>AI Agent 会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>绑定 AgentId</summary>
        public const String AgentId = "AgentId";

        /// <summary>绑定聊天模型</summary>
        public const String ModelId = "ModelId";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
