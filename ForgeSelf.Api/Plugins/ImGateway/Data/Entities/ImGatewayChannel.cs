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

/// <summary>{name}。IM 网关通道配置（单行）</summary>
[Serializable]
[DataObject]
[Description("{name}。IM 网关通道配置（单行）")]
[BindTable("ImGatewayChannel", Description = "IM 网关通道配置（单行）", ConnName = "ImGateway", DbType = DatabaseType.None)]
public partial class ImGatewayChannel
{
    #region 属性
    private Int64 _Id;
    /// <summary>配置ID</summary>
    [DisplayName("配置ID")]
    [Description("配置ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "配置ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Boolean _Enabled;
    /// <summary>是否启用</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "是否启用", "")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private String _BoundAgentId;
    /// <summary>绑定 AgentId</summary>
    [DisplayName("绑定AgentId")]
    [Description("绑定 AgentId")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("BoundAgentId", "绑定 AgentId", "")]
    public String BoundAgentId { get => _BoundAgentId; set { if (OnPropertyChanging("BoundAgentId", value)) { _BoundAgentId = value; OnPropertyChanged("BoundAgentId"); } } }

    private String _BoundChatModelId;
    /// <summary>绑定聊天模型</summary>
    [DisplayName("绑定聊天模型")]
    [Description("绑定聊天模型")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("BoundChatModelId", "绑定聊天模型", "")]
    public String BoundChatModelId { get => _BoundChatModelId; set { if (OnPropertyChanging("BoundChatModelId", value)) { _BoundChatModelId = value; OnPropertyChanged("BoundChatModelId"); } } }

    private String _BotId;
    /// <summary>企微智能机器人 BotID</summary>
    [DisplayName("企微智能机器人BotID")]
    [Description("企微智能机器人 BotID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("BotId", "企微智能机器人 BotID", "")]
    public String BotId { get => _BotId; set { if (OnPropertyChanging("BotId", value)) { _BotId = value; OnPropertyChanged("BotId"); } } }

    private String _SecretCipher;
    /// <summary>企微长连接密钥密文（ISecretEncryptionService 加密）</summary>
    [DisplayName("企微长连接密钥密文（ISecretEncryptionService加密）")]
    [Description("企微长连接密钥密文（ISecretEncryptionService 加密）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("SecretCipher", "企微长连接密钥密文（ISecretEncryptionService 加密）", "")]
    public String SecretCipher { get => _SecretCipher; set { if (OnPropertyChanging("SecretCipher", value)) { _SecretCipher = value; OnPropertyChanged("SecretCipher"); } } }

    private String _CliPath;
    /// <summary>企微 CLI 可执行文件路径（扫码授权用，空则自动探测）</summary>
    [DisplayName("企微CLI可执行文件路径（扫码授权用")]
    [Description("企微 CLI 可执行文件路径（扫码授权用，空则自动探测）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("CliPath", "企微 CLI 可执行文件路径（扫码授权用，空则自动探测）", "")]
    public String CliPath { get => _CliPath; set { if (OnPropertyChanging("CliPath", value)) { _CliPath = value; OnPropertyChanged("CliPath"); } } }

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
            "Enabled" => _Enabled,
            "BoundAgentId" => _BoundAgentId,
            "BoundChatModelId" => _BoundChatModelId,
            "BotId" => _BotId,
            "SecretCipher" => _SecretCipher,
            "CliPath" => _CliPath,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "BoundAgentId": _BoundAgentId = Convert.ToString(value); break;
                case "BoundChatModelId": _BoundChatModelId = Convert.ToString(value); break;
                case "BotId": _BotId = Convert.ToString(value); break;
                case "SecretCipher": _SecretCipher = Convert.ToString(value); break;
                case "CliPath": _CliPath = Convert.ToString(value); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据配置ID查找</summary>
    /// <param name="id">配置ID</param>
    /// <returns>实体对象</returns>
    public static ImGatewayChannel FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ImGatewayChannel> Search(Boolean? enabled, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (enabled != null) exp &= _.Enabled == enabled;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得IM网关通道配置（单行）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>配置ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>是否启用</summary>
        public static readonly Field Enabled = FindByName("Enabled");

        /// <summary>绑定 AgentId</summary>
        public static readonly Field BoundAgentId = FindByName("BoundAgentId");

        /// <summary>绑定聊天模型</summary>
        public static readonly Field BoundChatModelId = FindByName("BoundChatModelId");

        /// <summary>企微智能机器人 BotID</summary>
        public static readonly Field BotId = FindByName("BotId");

        /// <summary>企微长连接密钥密文（ISecretEncryptionService 加密）</summary>
        public static readonly Field SecretCipher = FindByName("SecretCipher");

        /// <summary>企微 CLI 可执行文件路径（扫码授权用，空则自动探测）</summary>
        public static readonly Field CliPath = FindByName("CliPath");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得IM网关通道配置（单行）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>配置ID</summary>
        public const String Id = "Id";

        /// <summary>是否启用</summary>
        public const String Enabled = "Enabled";

        /// <summary>绑定 AgentId</summary>
        public const String BoundAgentId = "BoundAgentId";

        /// <summary>绑定聊天模型</summary>
        public const String BoundChatModelId = "BoundChatModelId";

        /// <summary>企微智能机器人 BotID</summary>
        public const String BotId = "BotId";

        /// <summary>企微长连接密钥密文（ISecretEncryptionService 加密）</summary>
        public const String SecretCipher = "SecretCipher";

        /// <summary>企微 CLI 可执行文件路径（扫码授权用，空则自动探测）</summary>
        public const String CliPath = "CliPath";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
