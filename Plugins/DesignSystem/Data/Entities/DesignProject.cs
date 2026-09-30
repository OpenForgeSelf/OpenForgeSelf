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
[BindIndex("IU_DesignProject_Code", true, "Code")]
[BindIndex("IX_DesignProject_Status", false, "Status")]
[BindIndex("IX_DesignProject_ParentProjectId", false, "ParentProjectId")]
[BindTable("DesignProject", Description = "设计系统项目", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignProject
{
    #region 属性
    private Int64 _Id;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "项目ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Code;
    /// <summary>项目标识</summary>
    [DisplayName("项目标识")]
    [Description("项目标识")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "项目标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>项目名称</summary>
    [DisplayName("项目名称")]
    [Description("项目名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "项目名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Kind;
    /// <summary>用途类型：product|console|brand|marketing|system</summary>
    [DisplayName("用途类型")]
    [Description("用途类型：product|console|brand|marketing|system")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Kind", "用途类型：product|console|brand|marketing|system", "", DefaultValue = "product")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Version;
    /// <summary>设计系统当前版本号</summary>
    [DisplayName("设计系统当前版本号")]
    [Description("设计系统当前版本号")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Version", "设计系统当前版本号", "", DefaultValue = "0.1.0")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _Status;
    /// <summary>状态：draft|published|archived（归档为软删除）</summary>
    [DisplayName("状态")]
    [Description("状态：draft|published|archived（归档为软删除）")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Status", "状态：draft|published|archived（归档为软删除）", "", DefaultValue = "draft")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int64 _ParentProjectId;
    /// <summary>父项目ID，0=无。用于多品牌继承</summary>
    [DisplayName("父项目ID")]
    [Description("父项目ID，0=无。用于多品牌继承")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentProjectId", "父项目ID，0=无。用于多品牌继承", "", DefaultValue = "0")]
    public Int64 ParentProjectId { get => _ParentProjectId; set { if (OnPropertyChanging("ParentProjectId", value)) { _ParentProjectId = value; OnPropertyChanged("ParentProjectId"); } } }

    private Int64 _DefaultThemeId;
    /// <summary>默认主题ID，0=未设</summary>
    [DisplayName("默认主题ID")]
    [Description("默认主题ID，0=未设")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DefaultThemeId", "默认主题ID，0=未设", "", DefaultValue = "0")]
    public Int64 DefaultThemeId { get => _DefaultThemeId; set { if (OnPropertyChanging("DefaultThemeId", value)) { _DefaultThemeId = value; OnPropertyChanged("DefaultThemeId"); } } }

    private String _SeedText;
    /// <summary>生成用 brief 原文</summary>
    [DisplayName("生成用brief原文")]
    [Description("生成用 brief 原文")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("SeedText", "生成用 brief 原文", "")]
    public String SeedText { get => _SeedText; set { if (OnPropertyChanging("SeedText", value)) { _SeedText = value; OnPropertyChanged("SeedText"); } } }

    private String _SeedJson;
    /// <summary>生成参数包（种子色/色相/彩度/比例/密度/主题集/档数）</summary>
    [DisplayName("生成参数包（种子色_色相_彩度_比例_密度_主题集_档数）")]
    [Description("生成参数包（种子色/色相/彩度/比例/密度/主题集/档数）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("SeedJson", "生成参数包（种子色/色相/彩度/比例/密度/主题集/档数）", "")]
    public String SeedJson { get => _SeedJson; set { if (OnPropertyChanging("SeedJson", value)) { _SeedJson = value; OnPropertyChanged("SeedJson"); } } }

    private String _Generator;
    /// <summary>生成器标识</summary>
    [DisplayName("生成器标识")]
    [Description("生成器标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Generator", "生成器标识", "")]
    public String Generator { get => _Generator; set { if (OnPropertyChanging("Generator", value)) { _Generator = value; OnPropertyChanged("Generator"); } } }

    private String _GeneratorVersion;
    /// <summary>生成器版本，决定可复现性</summary>
    [DisplayName("生成器版本")]
    [Description("生成器版本，决定可复现性")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("GeneratorVersion", "生成器版本，决定可复现性", "")]
    public String GeneratorVersion { get => _GeneratorVersion; set { if (OnPropertyChanging("GeneratorVersion", value)) { _GeneratorVersion = value; OnPropertyChanged("GeneratorVersion"); } } }

    private String _SchemaVersion;
    /// <summary>数据模型版本</summary>
    [DisplayName("数据模型版本")]
    [Description("数据模型版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("SchemaVersion", "数据模型版本", "")]
    public String SchemaVersion { get => _SchemaVersion; set { if (OnPropertyChanging("SchemaVersion", value)) { _SchemaVersion = value; OnPropertyChanged("SchemaVersion"); } } }

    private String _ProjectionVersion;
    /// <summary>导出投影版本</summary>
    [DisplayName("导出投影版本")]
    [Description("导出投影版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ProjectionVersion", "导出投影版本", "")]
    public String ProjectionVersion { get => _ProjectionVersion; set { if (OnPropertyChanging("ProjectionVersion", value)) { _ProjectionVersion = value; OnPropertyChanged("ProjectionVersion"); } } }

    private Int32 _TokenCount;
    /// <summary>令牌数</summary>
    [DisplayName("令牌数")]
    [Description("令牌数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TokenCount", "令牌数", "")]
    public Int32 TokenCount { get => _TokenCount; set { if (OnPropertyChanging("TokenCount", value)) { _TokenCount = value; OnPropertyChanged("TokenCount"); } } }

    private Int32 _ComponentCount;
    /// <summary>组件数</summary>
    [DisplayName("组件数")]
    [Description("组件数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ComponentCount", "组件数", "")]
    public Int32 ComponentCount { get => _ComponentCount; set { if (OnPropertyChanging("ComponentCount", value)) { _ComponentCount = value; OnPropertyChanged("ComponentCount"); } } }

    private DateTime _PublishedAt;
    /// <summary>发布时间</summary>
    [DisplayName("发布时间")]
    [Description("发布时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("PublishedAt", "发布时间", "")]
    public DateTime PublishedAt { get => _PublishedAt; set { if (OnPropertyChanging("PublishedAt", value)) { _PublishedAt = value; OnPropertyChanged("PublishedAt"); } } }

    private String _Extensions;
    /// <summary>扩展元数据袋（JSON），新增字段先入此再考虑加列</summary>
    [DisplayName("扩展元数据袋（JSON）")]
    [Description("扩展元数据袋（JSON），新增字段先入此再考虑加列")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Extensions", "扩展元数据袋（JSON），新增字段先入此再考虑加列", "")]
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
            "Code" => _Code,
            "Name" => _Name,
            "Description" => _Description,
            "Kind" => _Kind,
            "Version" => _Version,
            "Status" => _Status,
            "ParentProjectId" => _ParentProjectId,
            "DefaultThemeId" => _DefaultThemeId,
            "SeedText" => _SeedText,
            "SeedJson" => _SeedJson,
            "Generator" => _Generator,
            "GeneratorVersion" => _GeneratorVersion,
            "SchemaVersion" => _SchemaVersion,
            "ProjectionVersion" => _ProjectionVersion,
            "TokenCount" => _TokenCount,
            "ComponentCount" => _ComponentCount,
            "PublishedAt" => _PublishedAt,
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
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "ParentProjectId": _ParentProjectId = value.ToLong(); break;
                case "DefaultThemeId": _DefaultThemeId = value.ToLong(); break;
                case "SeedText": _SeedText = Convert.ToString(value); break;
                case "SeedJson": _SeedJson = Convert.ToString(value); break;
                case "Generator": _Generator = Convert.ToString(value); break;
                case "GeneratorVersion": _GeneratorVersion = Convert.ToString(value); break;
                case "SchemaVersion": _SchemaVersion = Convert.ToString(value); break;
                case "ProjectionVersion": _ProjectionVersion = Convert.ToString(value); break;
                case "TokenCount": _TokenCount = value.ToInt(); break;
                case "ComponentCount": _ComponentCount = value.ToInt(); break;
                case "PublishedAt": _PublishedAt = value.ToDateTime(); break;
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
    /// <summary>根据项目ID查找</summary>
    /// <param name="id">项目ID</param>
    /// <returns>实体对象</returns>
    public static DesignProject FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目标识查找</summary>
    /// <param name="code">项目标识</param>
    /// <returns>实体对象</returns>
    public static DesignProject FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<DesignProject> FindAllByStatus(String status)
    {
        if (status.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Status.EqualIgnoreCase(status));

        return FindAll(_.Status == status);
    }

    /// <summary>根据父项目ID查找</summary>
    /// <param name="parentProjectId">父项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignProject> FindAllByParentProjectId(Int64 parentProjectId)
    {
        if (parentProjectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ParentProjectId == parentProjectId);

        return FindAll(_.ParentProjectId == parentProjectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">项目标识</param>
    /// <param name="status">状态：draft|published|archived（归档为软删除）</param>
    /// <param name="parentProjectId">父项目ID，0=无。用于多品牌继承</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignProject> Search(String code, String status, Int64 parentProjectId, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        if (parentProjectId >= 0) exp &= _.ParentProjectId == parentProjectId;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计系统项目字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>项目ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>项目名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>用途类型：product|console|brand|marketing|system</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>设计系统当前版本号</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>状态：draft|published|archived（归档为软删除）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>父项目ID，0=无。用于多品牌继承</summary>
        public static readonly Field ParentProjectId = FindByName("ParentProjectId");

        /// <summary>默认主题ID，0=未设</summary>
        public static readonly Field DefaultThemeId = FindByName("DefaultThemeId");

        /// <summary>生成用 brief 原文</summary>
        public static readonly Field SeedText = FindByName("SeedText");

        /// <summary>生成参数包（种子色/色相/彩度/比例/密度/主题集/档数）</summary>
        public static readonly Field SeedJson = FindByName("SeedJson");

        /// <summary>生成器标识</summary>
        public static readonly Field Generator = FindByName("Generator");

        /// <summary>生成器版本，决定可复现性</summary>
        public static readonly Field GeneratorVersion = FindByName("GeneratorVersion");

        /// <summary>数据模型版本</summary>
        public static readonly Field SchemaVersion = FindByName("SchemaVersion");

        /// <summary>导出投影版本</summary>
        public static readonly Field ProjectionVersion = FindByName("ProjectionVersion");

        /// <summary>令牌数</summary>
        public static readonly Field TokenCount = FindByName("TokenCount");

        /// <summary>组件数</summary>
        public static readonly Field ComponentCount = FindByName("ComponentCount");

        /// <summary>发布时间</summary>
        public static readonly Field PublishedAt = FindByName("PublishedAt");

        /// <summary>扩展元数据袋（JSON），新增字段先入此再考虑加列</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计系统项目字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>项目ID</summary>
        public const String Id = "Id";

        /// <summary>项目标识</summary>
        public const String Code = "Code";

        /// <summary>项目名称</summary>
        public const String Name = "Name";

        /// <summary>描述</summary>
        public const String Description = "Description";

        /// <summary>用途类型：product|console|brand|marketing|system</summary>
        public const String Kind = "Kind";

        /// <summary>设计系统当前版本号</summary>
        public const String Version = "Version";

        /// <summary>状态：draft|published|archived（归档为软删除）</summary>
        public const String Status = "Status";

        /// <summary>父项目ID，0=无。用于多品牌继承</summary>
        public const String ParentProjectId = "ParentProjectId";

        /// <summary>默认主题ID，0=未设</summary>
        public const String DefaultThemeId = "DefaultThemeId";

        /// <summary>生成用 brief 原文</summary>
        public const String SeedText = "SeedText";

        /// <summary>生成参数包（种子色/色相/彩度/比例/密度/主题集/档数）</summary>
        public const String SeedJson = "SeedJson";

        /// <summary>生成器标识</summary>
        public const String Generator = "Generator";

        /// <summary>生成器版本，决定可复现性</summary>
        public const String GeneratorVersion = "GeneratorVersion";

        /// <summary>数据模型版本</summary>
        public const String SchemaVersion = "SchemaVersion";

        /// <summary>导出投影版本</summary>
        public const String ProjectionVersion = "ProjectionVersion";

        /// <summary>令牌数</summary>
        public const String TokenCount = "TokenCount";

        /// <summary>组件数</summary>
        public const String ComponentCount = "ComponentCount";

        /// <summary>发布时间</summary>
        public const String PublishedAt = "PublishedAt";

        /// <summary>扩展元数据袋（JSON），新增字段先入此再考虑加列</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
