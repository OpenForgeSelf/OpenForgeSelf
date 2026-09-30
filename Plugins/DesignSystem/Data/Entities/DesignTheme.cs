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

namespace ForgeSelf.Api.Plugins.DesignSystem.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IU_DesignTheme_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignTheme_ProjectId_ModeKind", false, "ProjectId,ModeKind")]
[BindTable("DesignTheme", Description = "设计主题", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignTheme
{
    #region 属性
    private Int64 _Id;
    /// <summary>主题ID</summary>
    [DisplayName("主题ID")]
    [Description("主题ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主题ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>主题标识：light|dark|high-contrast|compact 等</summary>
    [DisplayName("主题标识")]
    [Description("主题标识：light|dark|high-contrast|compact 等")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Code", "主题标识：light|dark|high-contrast|compact 等", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>主题名称</summary>
    [DisplayName("主题名称")]
    [Description("主题名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "主题名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _ModeKind;
    /// <summary>模式轴：color|density|brand</summary>
    [DisplayName("模式轴")]
    [Description("模式轴：color|density|brand")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ModeKind", "模式轴：color|density|brand", "", DefaultValue = "color")]
    public String ModeKind { get => _ModeKind; set { if (OnPropertyChanging("ModeKind", value)) { _ModeKind = value; OnPropertyChanged("ModeKind"); } } }

    private Boolean _IsDefault;
    /// <summary>是否项目默认主题</summary>
    [DisplayName("是否项目默认主题")]
    [Description("是否项目默认主题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsDefault", "是否项目默认主题", "", DefaultValue = "0")]
    public Boolean IsDefault { get => _IsDefault; set { if (OnPropertyChanging("IsDefault", value)) { _IsDefault = value; OnPropertyChanged("IsDefault"); } } }

    private Int64 _BaseThemeId;
    /// <summary>派生自哪个主题ID，0=直接继承共享层</summary>
    [DisplayName("派生自哪个主题ID")]
    [Description("派生自哪个主题ID，0=直接继承共享层")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BaseThemeId", "派生自哪个主题ID，0=直接继承共享层", "", DefaultValue = "0")]
    public Int64 BaseThemeId { get => _BaseThemeId; set { if (OnPropertyChanging("BaseThemeId", value)) { _BaseThemeId = value; OnPropertyChanged("BaseThemeId"); } } }

    private String _OverridesJson;
    /// <summary>主题级覆盖摘要（路径→值）</summary>
    [DisplayName("主题级覆盖摘要（路径→值）")]
    [Description("主题级覆盖摘要（路径→值）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("OverridesJson", "主题级覆盖摘要（路径→值）", "")]
    public String OverridesJson { get => _OverridesJson; set { if (OnPropertyChanging("OverridesJson", value)) { _OverridesJson = value; OnPropertyChanged("OverridesJson"); } } }

    private Int32 _SortOrder;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SortOrder", "排序", "")]
    public Int32 SortOrder { get => _SortOrder; set { if (OnPropertyChanging("SortOrder", value)) { _SortOrder = value; OnPropertyChanged("SortOrder"); } } }

    private String _Extensions;
    /// <summary>扩展元数据袋（JSON）</summary>
    [DisplayName("扩展元数据袋（JSON）")]
    [Description("扩展元数据袋（JSON）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Extensions", "扩展元数据袋（JSON）", "")]
    public String Extensions { get => _Extensions; set { if (OnPropertyChanging("Extensions", value)) { _Extensions = value; OnPropertyChanged("Extensions"); } } }

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
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "ProjectId" => _ProjectId,
            "Code" => _Code,
            "Name" => _Name,
            "ModeKind" => _ModeKind,
            "IsDefault" => _IsDefault,
            "BaseThemeId" => _BaseThemeId,
            "OverridesJson" => _OverridesJson,
            "SortOrder" => _SortOrder,
            "Extensions" => _Extensions,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ProjectId": _ProjectId = value.ToLong(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ModeKind": _ModeKind = Convert.ToString(value); break;
                case "IsDefault": _IsDefault = value.ToBoolean(); break;
                case "BaseThemeId": _BaseThemeId = value.ToLong(); break;
                case "OverridesJson": _OverridesJson = Convert.ToString(value); break;
                case "SortOrder": _SortOrder = value.ToInt(); break;
                case "Extensions": _Extensions = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据主题ID查找</summary>
    /// <param name="id">主题ID</param>
    /// <returns>实体对象</returns>
    public static DesignTheme FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、主题标识查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">主题标识</param>
    /// <returns>实体对象</returns>
    public static DesignTheme FindByProjectIdAndCode(Int64 projectId, String code)
    {
        if (projectId < 0) return null;
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProjectId == projectId && e.Code.EqualIgnoreCase(code));

        return Find(_.ProjectId == projectId & _.Code == code);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignTheme> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、模式轴查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="modeKind">模式轴</param>
    /// <returns>实体列表</returns>
    public static IList<DesignTheme> FindAllByProjectIdAndModeKind(Int64 projectId, String modeKind)
    {
        if (projectId < 0) return [];
        if (modeKind.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.ModeKind.EqualIgnoreCase(modeKind));

        return FindAll(_.ProjectId == projectId & _.ModeKind == modeKind);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">主题标识：light|dark|high-contrast|compact 等</param>
    /// <param name="modeKind">模式轴：color|density|brand</param>
    /// <param name="isDefault">是否项目默认主题</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignTheme> Search(Int64 projectId, String code, String modeKind, Boolean? isDefault, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!modeKind.IsNullOrEmpty()) exp &= _.ModeKind == modeKind;
        if (isDefault != null) exp &= _.IsDefault == isDefault;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计主题字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主题ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>主题标识：light|dark|high-contrast|compact 等</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>主题名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>模式轴：color|density|brand</summary>
        public static readonly Field ModeKind = FindByName("ModeKind");

        /// <summary>是否项目默认主题</summary>
        public static readonly Field IsDefault = FindByName("IsDefault");

        /// <summary>派生自哪个主题ID，0=直接继承共享层</summary>
        public static readonly Field BaseThemeId = FindByName("BaseThemeId");

        /// <summary>主题级覆盖摘要（路径→值）</summary>
        public static readonly Field OverridesJson = FindByName("OverridesJson");

        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计主题字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主题ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>主题标识：light|dark|high-contrast|compact 等</summary>
        public const String Code = "Code";

        /// <summary>主题名称</summary>
        public const String Name = "Name";

        /// <summary>模式轴：color|density|brand</summary>
        public const String ModeKind = "ModeKind";

        /// <summary>是否项目默认主题</summary>
        public const String IsDefault = "IsDefault";

        /// <summary>派生自哪个主题ID，0=直接继承共享层</summary>
        public const String BaseThemeId = "BaseThemeId";

        /// <summary>主题级覆盖摘要（路径→值）</summary>
        public const String OverridesJson = "OverridesJson";

        /// <summary>排序</summary>
        public const String SortOrder = "SortOrder";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
