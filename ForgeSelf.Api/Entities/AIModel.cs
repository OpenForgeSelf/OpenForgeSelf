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

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IU_AIModel_ProviderId_UpstreamModelId", true, "ProviderId,UpstreamModelId")]
[BindIndex("IX_AIModel_ProviderId", false, "ProviderId")]
[BindTable("AIModel", Description = "供应商模型记录", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class AIModel : IAIModelModel, IEntity<IAIModelModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>实体唯一标识</summary>
    [DisplayName("实体唯一标识")]
    [Description("实体唯一标识")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "实体唯一标识", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProviderId;
    /// <summary>归属供应商Id（AIProvider.Id）</summary>
    [DisplayName("归属供应商Id（AIProvider")]
    [Description("归属供应商Id（AIProvider.Id）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProviderId", "归属供应商Id（AIProvider.Id）", "")]
    public Int64 ProviderId { get => _ProviderId; set { if (OnPropertyChanging("ProviderId", value)) { _ProviderId = value; OnPropertyChanged("ProviderId"); } } }

    private String _ProviderName;
    /// <summary>归属供应商名（拼接聊天id用，锁定不可编辑）</summary>
    [DisplayName("归属供应商名（拼接聊天id用")]
    [Description("归属供应商名（拼接聊天id用，锁定不可编辑）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("ProviderName", "归属供应商名（拼接聊天id用，锁定不可编辑）", "")]
    public String ProviderName { get => _ProviderName; set { if (OnPropertyChanging("ProviderName", value)) { _ProviderName = value; OnPropertyChanged("ProviderName"); } } }

    private String _UpstreamModelId;
    /// <summary>上游原始模型标识（如 gpt-4o），锁定不可编辑</summary>
    [DisplayName("上游原始模型标识（如gpt-4o）")]
    [Description("上游原始模型标识（如 gpt-4o），锁定不可编辑")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("UpstreamModelId", "上游原始模型标识（如 gpt-4o），锁定不可编辑", "")]
    public String UpstreamModelId { get => _UpstreamModelId; set { if (OnPropertyChanging("UpstreamModelId", value)) { _UpstreamModelId = value; OnPropertyChanged("UpstreamModelId"); } } }

    private String _ChatModelId;
    /// <summary>项目聊天模型id：提供商:原始模型id（复制按钮输出）</summary>
    [DisplayName("项目聊天模型id")]
    [Description("项目聊天模型id：提供商:原始模型id（复制按钮输出）")]
    [DataObjectField(false, false, false, 320)]
    [BindColumn("ChatModelId", "项目聊天模型id：提供商:原始模型id（复制按钮输出）", "")]
    public String ChatModelId { get => _ChatModelId; set { if (OnPropertyChanging("ChatModelId", value)) { _ChatModelId = value; OnPropertyChanged("ChatModelId"); } } }

    private String _Alias;
    /// <summary>显示别名/备注（用户可编辑）</summary>
    [DisplayName("显示别名_备注（用户可编辑）")]
    [Description("显示别名/备注（用户可编辑）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Alias", "显示别名/备注（用户可编辑）", "")]
    public String Alias { get => _Alias; set { if (OnPropertyChanging("Alias", value)) { _Alias = value; OnPropertyChanged("Alias"); } } }

    private String _Capabilities;
    /// <summary>能力标签（逗号分隔，如 vision,stream，用户可编辑）</summary>
    [DisplayName("能力标签（逗号分隔")]
    [Description("能力标签（逗号分隔，如 vision,stream，用户可编辑）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Capabilities", "能力标签（逗号分隔，如 vision,stream，用户可编辑）", "")]
    public String Capabilities { get => _Capabilities; set { if (OnPropertyChanging("Capabilities", value)) { _Capabilities = value; OnPropertyChanged("Capabilities"); } } }

    private Int32 _MaxContext;
    /// <summary>最大上下文长度（token），0=未设置</summary>
    [DisplayName("最大上下文长度（token）")]
    [Description("最大上下文长度（token），0=未设置")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxContext", "最大上下文长度（token），0=未设置", "")]
    public Int32 MaxContext { get => _MaxContext; set { if (OnPropertyChanging("MaxContext", value)) { _MaxContext = value; OnPropertyChanged("MaxContext"); } } }

    private Boolean _Enabled;
    /// <summary>是否启用（已启用/已禁用），默认启用</summary>
    [DisplayName("是否启用（已启用_已禁用）")]
    [Description("是否启用（已启用/已禁用），默认启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "是否启用（已启用/已禁用），默认启用", "")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private String _Owner;
    /// <summary>上游返回的 owner/owned_by</summary>
    [DisplayName("上游返回的owner_owned_by")]
    [Description("上游返回的 owner/owned_by")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Owner", "上游返回的 owner/owned_by", "")]
    public String Owner { get => _Owner; set { if (OnPropertyChanging("Owner", value)) { _Owner = value; OnPropertyChanged("Owner"); } } }

    private DateTime _LastSyncTime;
    /// <summary>最近同步（拉取）时间</summary>
    [DisplayName("最近同步（拉取）时间")]
    [Description("最近同步（拉取）时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastSyncTime", "最近同步（拉取）时间", "")]
    public DateTime LastSyncTime { get => _LastSyncTime; set { if (OnPropertyChanging("LastSyncTime", value)) { _LastSyncTime = value; OnPropertyChanged("LastSyncTime"); } } }

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

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IAIModelModel model)
    {
        Id = model.Id;
        ProviderId = model.ProviderId;
        ProviderName = model.ProviderName;
        UpstreamModelId = model.UpstreamModelId;
        ChatModelId = model.ChatModelId;
        Alias = model.Alias;
        Capabilities = model.Capabilities;
        MaxContext = model.MaxContext;
        Enabled = model.Enabled;
        Owner = model.Owner;
        LastSyncTime = model.LastSyncTime;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
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
            "ProviderId" => _ProviderId,
            "ProviderName" => _ProviderName,
            "UpstreamModelId" => _UpstreamModelId,
            "ChatModelId" => _ChatModelId,
            "Alias" => _Alias,
            "Capabilities" => _Capabilities,
            "MaxContext" => _MaxContext,
            "Enabled" => _Enabled,
            "Owner" => _Owner,
            "LastSyncTime" => _LastSyncTime,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ProviderId": _ProviderId = value.ToLong(); break;
                case "ProviderName": _ProviderName = Convert.ToString(value); break;
                case "UpstreamModelId": _UpstreamModelId = Convert.ToString(value); break;
                case "ChatModelId": _ChatModelId = Convert.ToString(value); break;
                case "Alias": _Alias = Convert.ToString(value); break;
                case "Capabilities": _Capabilities = Convert.ToString(value); break;
                case "MaxContext": _MaxContext = value.ToInt(); break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "Owner": _Owner = Convert.ToString(value); break;
                case "LastSyncTime": _LastSyncTime = value.ToDateTime(); break;
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
    /// <summary>根据实体唯一标识查找</summary>
    /// <param name="id">实体唯一标识</param>
    /// <returns>实体对象</returns>
    public static AIModel FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据归属供应商Id（AIProvider、上游原始模型标识（如gpt-4o）查找</summary>
    /// <param name="providerId">归属供应商Id（AIProvider</param>
    /// <param name="upstreamModelId">上游原始模型标识（如gpt-4o）</param>
    /// <returns>实体对象</returns>
    public static AIModel FindByProviderIdAndUpstreamModelId(Int64 providerId, String upstreamModelId)
    {
        if (providerId < 0) return null;
        if (upstreamModelId.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProviderId == providerId && e.UpstreamModelId.EqualIgnoreCase(upstreamModelId));

        return Find(_.ProviderId == providerId & _.UpstreamModelId == upstreamModelId);
    }

    /// <summary>根据归属供应商Id（AIProvider查找</summary>
    /// <param name="providerId">归属供应商Id（AIProvider</param>
    /// <returns>实体列表</returns>
    public static IList<AIModel> FindAllByProviderId(Int64 providerId)
    {
        if (providerId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProviderId == providerId);

        return FindAll(_.ProviderId == providerId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="providerId">归属供应商Id（AIProvider.Id）</param>
    /// <param name="upstreamModelId">上游原始模型标识（如 gpt-4o），锁定不可编辑</param>
    /// <param name="enabled">是否启用（已启用/已禁用），默认启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AIModel> Search(Int64 providerId, String upstreamModelId, Boolean? enabled, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (providerId >= 0) exp &= _.ProviderId == providerId;
        if (!upstreamModelId.IsNullOrEmpty()) exp &= _.UpstreamModelId == upstreamModelId;
        if (enabled != null) exp &= _.Enabled == enabled;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得供应商模型记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>实体唯一标识</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>归属供应商Id（AIProvider.Id）</summary>
        public static readonly Field ProviderId = FindByName("ProviderId");

        /// <summary>归属供应商名（拼接聊天id用，锁定不可编辑）</summary>
        public static readonly Field ProviderName = FindByName("ProviderName");

        /// <summary>上游原始模型标识（如 gpt-4o），锁定不可编辑</summary>
        public static readonly Field UpstreamModelId = FindByName("UpstreamModelId");

        /// <summary>项目聊天模型id：提供商:原始模型id（复制按钮输出）</summary>
        public static readonly Field ChatModelId = FindByName("ChatModelId");

        /// <summary>显示别名/备注（用户可编辑）</summary>
        public static readonly Field Alias = FindByName("Alias");

        /// <summary>能力标签（逗号分隔，如 vision,stream，用户可编辑）</summary>
        public static readonly Field Capabilities = FindByName("Capabilities");

        /// <summary>最大上下文长度（token），0=未设置</summary>
        public static readonly Field MaxContext = FindByName("MaxContext");

        /// <summary>是否启用（已启用/已禁用），默认启用</summary>
        public static readonly Field Enabled = FindByName("Enabled");

        /// <summary>上游返回的 owner/owned_by</summary>
        public static readonly Field Owner = FindByName("Owner");

        /// <summary>最近同步（拉取）时间</summary>
        public static readonly Field LastSyncTime = FindByName("LastSyncTime");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得供应商模型记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>实体唯一标识</summary>
        public const String Id = "Id";

        /// <summary>归属供应商Id（AIProvider.Id）</summary>
        public const String ProviderId = "ProviderId";

        /// <summary>归属供应商名（拼接聊天id用，锁定不可编辑）</summary>
        public const String ProviderName = "ProviderName";

        /// <summary>上游原始模型标识（如 gpt-4o），锁定不可编辑</summary>
        public const String UpstreamModelId = "UpstreamModelId";

        /// <summary>项目聊天模型id：提供商:原始模型id（复制按钮输出）</summary>
        public const String ChatModelId = "ChatModelId";

        /// <summary>显示别名/备注（用户可编辑）</summary>
        public const String Alias = "Alias";

        /// <summary>能力标签（逗号分隔，如 vision,stream，用户可编辑）</summary>
        public const String Capabilities = "Capabilities";

        /// <summary>最大上下文长度（token），0=未设置</summary>
        public const String MaxContext = "MaxContext";

        /// <summary>是否启用（已启用/已禁用），默认启用</summary>
        public const String Enabled = "Enabled";

        /// <summary>上游返回的 owner/owned_by</summary>
        public const String Owner = "Owner";

        /// <summary>最近同步（拉取）时间</summary>
        public const String LastSyncTime = "LastSyncTime";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
