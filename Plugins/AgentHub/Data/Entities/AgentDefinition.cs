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

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>{name}。被管理的 Agent 定义</summary>
[Serializable]
[DataObject]
[Description("{name}。被管理的 Agent 定义")]
[BindIndex("IX_AgentDefinition_Enabled", false, "Enabled")]
[BindIndex("IX_AgentDefinition_Vendor", false, "Vendor")]
[BindTable("AgentDefinition", Description = "被管理的 Agent 定义", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class AgentDefinition
{
    #region 属性
    private Int32 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "显示名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Vendor;
    /// <summary>厂商标识</summary>
    [DisplayName("厂商标识")]
    [Description("厂商标识")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Vendor", "厂商标识", "", DefaultValue = "custom")]
    public String Vendor { get => _Vendor; set { if (OnPropertyChanging("Vendor", value)) { _Vendor = value; OnPropertyChanged("Vendor"); } } }

    private String _DisplayName;
    /// <summary>厂商展示名</summary>
    [DisplayName("厂商展示名")]
    [Description("厂商展示名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("DisplayName", "厂商展示名", "")]
    public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private String _Kind;
    /// <summary>类型</summary>
    [DisplayName("类型")]
    [Description("类型")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Kind", "类型", "", DefaultValue = "Coding")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Tags;
    /// <summary>擅长标签</summary>
    [DisplayName("擅长标签")]
    [Description("擅长标签")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Tags", "擅长标签", "")]
    public String Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private String _CapabilitiesJson;
    /// <summary>能力矩阵 JSON</summary>
    [DisplayName("能力矩阵JSON")]
    [Description("能力矩阵 JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("CapabilitiesJson", "能力矩阵 JSON", "")]
    public String CapabilitiesJson { get => _CapabilitiesJson; set { if (OnPropertyChanging("CapabilitiesJson", value)) { _CapabilitiesJson = value; OnPropertyChanged("CapabilitiesJson"); } } }

    private String _DefaultCwd;
    /// <summary>默认工作目录</summary>
    [DisplayName("默认工作目录")]
    [Description("默认工作目录")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("DefaultCwd", "默认工作目录", "")]
    public String DefaultCwd { get => _DefaultCwd; set { if (OnPropertyChanging("DefaultCwd", value)) { _DefaultCwd = value; OnPropertyChanged("DefaultCwd"); } } }

    private String _PolicyJson;
    /// <summary>策略 JSON</summary>
    [DisplayName("策略JSON")]
    [Description("策略 JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("PolicyJson", "策略 JSON", "")]
    public String PolicyJson { get => _PolicyJson; set { if (OnPropertyChanging("PolicyJson", value)) { _PolicyJson = value; OnPropertyChanged("PolicyJson"); } } }

    private Boolean _Enabled;
    /// <summary>是否启用</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "是否启用", "", DefaultValue = "1")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private Int32 _Priority;
    /// <summary>选路优先级</summary>
    [DisplayName("选路优先级")]
    [Description("选路优先级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "选路优先级", "", DefaultValue = "0")]
    public Int32 Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private Boolean _Trusted;
    /// <summary>是否授信</summary>
    [DisplayName("是否授信")]
    [Description("是否授信")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Trusted", "是否授信", "", DefaultValue = "0")]
    public Boolean Trusted { get => _Trusted; set { if (OnPropertyChanging("Trusted", value)) { _Trusted = value; OnPropertyChanged("Trusted"); } } }

    private String _TrustedScopes;
    /// <summary>授信范围</summary>
    [DisplayName("授信范围")]
    [Description("授信范围")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("TrustedScopes", "授信范围", "")]
    public String TrustedScopes { get => _TrustedScopes; set { if (OnPropertyChanging("TrustedScopes", value)) { _TrustedScopes = value; OnPropertyChanged("TrustedScopes"); } } }

    private String _Notes;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Notes", "备注", "")]
    public String Notes { get => _Notes; set { if (OnPropertyChanging("Notes", value)) { _Notes = value; OnPropertyChanged("Notes"); } } }

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

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Name" => _Name,
            "Vendor" => _Vendor,
            "DisplayName" => _DisplayName,
            "Kind" => _Kind,
            "Tags" => _Tags,
            "CapabilitiesJson" => _CapabilitiesJson,
            "DefaultCwd" => _DefaultCwd,
            "PolicyJson" => _PolicyJson,
            "Enabled" => _Enabled,
            "Priority" => _Priority,
            "Trusted" => _Trusted,
            "TrustedScopes" => _TrustedScopes,
            "Notes" => _Notes,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Vendor": _Vendor = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Tags": _Tags = Convert.ToString(value); break;
                case "CapabilitiesJson": _CapabilitiesJson = Convert.ToString(value); break;
                case "DefaultCwd": _DefaultCwd = Convert.ToString(value); break;
                case "PolicyJson": _PolicyJson = Convert.ToString(value); break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "Priority": _Priority = value.ToInt(); break;
                case "Trusted": _Trusted = value.ToBoolean(); break;
                case "TrustedScopes": _TrustedScopes = Convert.ToString(value); break;
                case "Notes": _Notes = Convert.ToString(value); break;
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
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static AgentDefinition FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="vendor">厂商标识</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="trusted">是否授信</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AgentDefinition> Search(String vendor, Boolean? enabled, Boolean? trusted, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!vendor.IsNullOrEmpty()) exp &= _.Vendor == vendor;
        if (enabled != null) exp &= _.Enabled == enabled;
        if (trusted != null) exp &= _.Trusted == trusted;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得被管理的Agent定义字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>显示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>厂商标识</summary>
        public static readonly Field Vendor = FindByName("Vendor");

        /// <summary>厂商展示名</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>类型</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>擅长标签</summary>
        public static readonly Field Tags = FindByName("Tags");

        /// <summary>能力矩阵 JSON</summary>
        public static readonly Field CapabilitiesJson = FindByName("CapabilitiesJson");

        /// <summary>默认工作目录</summary>
        public static readonly Field DefaultCwd = FindByName("DefaultCwd");

        /// <summary>策略 JSON</summary>
        public static readonly Field PolicyJson = FindByName("PolicyJson");

        /// <summary>是否启用</summary>
        public static readonly Field Enabled = FindByName("Enabled");

        /// <summary>选路优先级</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>是否授信</summary>
        public static readonly Field Trusted = FindByName("Trusted");

        /// <summary>授信范围</summary>
        public static readonly Field TrustedScopes = FindByName("TrustedScopes");

        /// <summary>备注</summary>
        public static readonly Field Notes = FindByName("Notes");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得被管理的Agent定义字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>显示名</summary>
        public const String Name = "Name";

        /// <summary>厂商标识</summary>
        public const String Vendor = "Vendor";

        /// <summary>厂商展示名</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>类型</summary>
        public const String Kind = "Kind";

        /// <summary>擅长标签</summary>
        public const String Tags = "Tags";

        /// <summary>能力矩阵 JSON</summary>
        public const String CapabilitiesJson = "CapabilitiesJson";

        /// <summary>默认工作目录</summary>
        public const String DefaultCwd = "DefaultCwd";

        /// <summary>策略 JSON</summary>
        public const String PolicyJson = "PolicyJson";

        /// <summary>是否启用</summary>
        public const String Enabled = "Enabled";

        /// <summary>选路优先级</summary>
        public const String Priority = "Priority";

        /// <summary>是否授信</summary>
        public const String Trusted = "Trusted";

        /// <summary>授信范围</summary>
        public const String TrustedScopes = "TrustedScopes";

        /// <summary>备注</summary>
        public const String Notes = "Notes";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
