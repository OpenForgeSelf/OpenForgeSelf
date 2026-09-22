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

/// <summary>{name}。IM 网关消息去重</summary>
[Serializable]
[DataObject]
[Description("{name}。IM 网关消息去重")]
[BindIndex("IU_ImGatewayProcessedMsg_ChannelType_MsgId", true, "ChannelType,MsgId")]
[BindTable("ImGatewayProcessedMsg", Description = "IM 网关消息去重", ConnName = "ImGateway", DbType = DatabaseType.None)]
public partial class ImGatewayProcessedMsg
{
    #region 属性
    private Int64 _Id;
    /// <summary>去重ID</summary>
    [DisplayName("去重ID")]
    [Description("去重ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "去重ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _ChannelType;
    /// <summary>通道类型</summary>
    [DisplayName("通道类型")]
    [Description("通道类型")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("ChannelType", "通道类型", "")]
    public String ChannelType { get => _ChannelType; set { if (OnPropertyChanging("ChannelType", value)) { _ChannelType = value; OnPropertyChanged("ChannelType"); } } }

    private String _MsgId;
    /// <summary>平台消息ID</summary>
    [DisplayName("平台消息ID")]
    [Description("平台消息ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("MsgId", "平台消息ID", "")]
    public String MsgId { get => _MsgId; set { if (OnPropertyChanging("MsgId", value)) { _MsgId = value; OnPropertyChanged("MsgId"); } } }

    private DateTime _ProcessedAt;
    /// <summary>处理时间</summary>
    [DisplayName("处理时间")]
    [Description("处理时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ProcessedAt", "处理时间", "")]
    public DateTime ProcessedAt { get => _ProcessedAt; set { if (OnPropertyChanging("ProcessedAt", value)) { _ProcessedAt = value; OnPropertyChanged("ProcessedAt"); } } }
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
            "ChannelType" => _ChannelType,
            "MsgId" => _MsgId,
            "ProcessedAt" => _ProcessedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ChannelType": _ChannelType = Convert.ToString(value); break;
                case "MsgId": _MsgId = Convert.ToString(value); break;
                case "ProcessedAt": _ProcessedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据去重ID查找</summary>
    /// <param name="id">去重ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewayProcessedMsg FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据通道类型、平台消息ID查找</summary>
    /// <param name="channelType">通道类型</param>
    /// <param name="msgId">平台消息ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewayProcessedMsg FindByChannelTypeAndMsgId(String channelType, String msgId)
    {
        if (channelType.IsNullOrEmpty()) return null;
        if (msgId.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ChannelType.EqualIgnoreCase(channelType) && e.MsgId.EqualIgnoreCase(msgId));

        return Find(_.ChannelType == channelType & _.MsgId == msgId);
    }

    /// <summary>根据通道类型查找</summary>
    /// <param name="channelType">通道类型</param>
    /// <returns>实体列表</returns>
    public static IList<ImGatewayProcessedMsg> FindAllByChannelType(String channelType)
    {
        if (channelType.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ChannelType.EqualIgnoreCase(channelType));

        return FindAll(_.ChannelType == channelType);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="channelType">通道类型</param>
    /// <param name="msgId">平台消息ID</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ImGatewayProcessedMsg> Search(String channelType, String msgId, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!channelType.IsNullOrEmpty()) exp &= _.ChannelType == channelType;
        if (!msgId.IsNullOrEmpty()) exp &= _.MsgId == msgId;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得IM网关消息去重字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>去重ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>通道类型</summary>
        public static readonly Field ChannelType = FindByName("ChannelType");

        /// <summary>平台消息ID</summary>
        public static readonly Field MsgId = FindByName("MsgId");

        /// <summary>处理时间</summary>
        public static readonly Field ProcessedAt = FindByName("ProcessedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得IM网关消息去重字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>去重ID</summary>
        public const String Id = "Id";

        /// <summary>通道类型</summary>
        public const String ChannelType = "ChannelType";

        /// <summary>平台消息ID</summary>
        public const String MsgId = "MsgId";

        /// <summary>处理时间</summary>
        public const String ProcessedAt = "ProcessedAt";
    }
    #endregion
}
