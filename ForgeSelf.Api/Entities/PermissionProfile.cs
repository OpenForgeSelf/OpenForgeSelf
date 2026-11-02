using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>权限策略 profile（B4）：按 Agent/工具 allow/ask/deny + 白名单，JSON 规则集落库。</summary>
[Serializable]
[DataObject]
[Description("权限策略配置。")]
[BindIndex("IX_PermissionProfile_AgentId", false, "AgentId")]
[BindTable("PermissionProfile", Description = "权限策略配置", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class PermissionProfile
{
    #region 属性
    private Int64 _Id;
    /// <summary>策略ID</summary>
    [DisplayName("策略ID")]
    [Description("策略ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "策略ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _AgentId = string.Empty;
    /// <summary>目标 Agent id；空 = 全局默认 profile</summary>
    [DisplayName("目标Agent")]
    [Description("目标 Agent id；空 = 全局默认 profile")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("AgentId", "目标Agent", "", Master = true)]
    public String AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private Boolean _Enabled;
    /// <summary>启用；禁用不参与决策（保留配置）</summary>
    [DisplayName("启用")]
    [Description("启用；禁用不参与决策（保留配置）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "启用", "")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private String _RulesJson = "[]";
    /// <summary>工具规则集 JSON（ToolPermissionRule[]，camelCase）</summary>
    [DisplayName("规则JSON")]
    [Description("工具规则集 JSON（ToolPermissionRule[]，camelCase）")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("RulesJson", "规则JSON", "")]
    public String RulesJson { get => _RulesJson; set { if (OnPropertyChanging("RulesJson", value)) { _RulesJson = value; OnPropertyChanged("RulesJson"); } } }

    private String _Remark = string.Empty;
    /// <summary>备注（给人看）</summary>
    [DisplayName("备注")]
    [Description("备注（给人看）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <inheritdoc />
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "AgentId" => _AgentId,
            "Enabled" => _Enabled,
            "RulesJson" => _RulesJson,
            "Remark" => _Remark!,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "AgentId": _AgentId = Convert.ToString(value) ?? string.Empty; break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "RulesJson": _RulesJson = Convert.ToString(value) ?? "[]"; break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 持久化
    /// <summary>已重载。决策/保存前的数据校验与时间戳维护。</summary>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (method == DataMethod.Insert)
        {
            if (!Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;
            if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = DateTime.Now;
        }
        else if (method == DataMethod.Update)
        {
            UpdatedAt = DateTime.Now;
        }
        if (AgentId == null) AgentId = string.Empty;
        if (RulesJson.IsNullOrEmpty()) RulesJson = "[]";
        return base.Valid(method);
    }
    #endregion

    #region 扩展查询
    /// <summary>按主键查找（照 ToolApproval.FindById 模式；未命中返回 null）。</summary>
    public static PermissionProfile? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>按 AgentId 查找（空串 = 全局默认；未命中返回 null）。</summary>
    public static PermissionProfile? FindByAgentId(String agentId)
    {
        if (agentId == null) agentId = string.Empty;
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AgentId == agentId);
        return Find(_.AgentId == agentId);
    }

    /// <summary>全部策略（Id 升序）。</summary>
    public static IList<PermissionProfile> FindAllOrdered()
        => FindAll().OrderBy(e => e.Id).ToList();
    #endregion

    #region 字段名
    /// <summary>取得权限策略字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>策略ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>目标Agent</summary>
        public static readonly Field AgentId = FindByName("AgentId");
        /// <summary>启用</summary>
        public static readonly Field Enabled = FindByName("Enabled");

        private static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
