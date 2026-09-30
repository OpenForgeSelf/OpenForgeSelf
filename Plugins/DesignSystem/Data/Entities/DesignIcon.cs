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
[BindIndex("IU_DesignIcon_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignIcon_Collection", false, "Collection")]
[BindTable("DesignIcon", Description = "设计图标", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignIcon
{
    #region 属性
    private Int64 _Id;
    /// <summary>图标ID</summary>
    [DisplayName("图标ID")]
    [Description("图标ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "图标ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID；0=内置库（常量 BuiltinProjectId，只读）</summary>
    [DisplayName("项目ID")]
    [Description("项目ID；0=内置库（常量 BuiltinProjectId，只读）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID；0=内置库（常量 BuiltinProjectId，只读）", "", DefaultValue = "0")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>图标标识</summary>
    [DisplayName("图标标识")]
    [Description("图标标识")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "图标标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>图标名称</summary>
    [DisplayName("图标名称")]
    [Description("图标名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "图标名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Collection;
    /// <summary>集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名</summary>
    [DisplayName("集合")]
    [Description("集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Collection", "集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名", "", DefaultValue = "forge")]
    public String Collection { get => _Collection; set { if (OnPropertyChanging("Collection", value)) { _Collection = value; OnPropertyChanged("Collection"); } } }

    private String _SvgBody;
    /// <summary>SVG 内容（path 数据或完整 svg 片段），为系统 of record</summary>
    [DisplayName("SVG内容（path数据或完整svg片段）")]
    [Description("SVG 内容（path 数据或完整 svg 片段），为系统 of record")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("SvgBody", "SVG 内容（path 数据或完整 svg 片段），为系统 of record", "")]
    public String SvgBody { get => _SvgBody; set { if (OnPropertyChanging("SvgBody", value)) { _SvgBody = value; OnPropertyChanged("SvgBody"); } } }

    private Double _StrokeWidth;
    /// <summary>描边宽度（令牌化项）</summary>
    [DisplayName("描边宽度（令牌化项）")]
    [Description("描边宽度（令牌化项）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StrokeWidth", "描边宽度（令牌化项）", "", DefaultValue = "1.5")]
    public Double StrokeWidth { get => _StrokeWidth; set { if (OnPropertyChanging("StrokeWidth", value)) { _StrokeWidth = value; OnPropertyChanged("StrokeWidth"); } } }

    private Int32 _GridPx;
    /// <summary>基准栅格 px</summary>
    [DisplayName("基准栅格px")]
    [Description("基准栅格 px")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GridPx", "基准栅格 px", "", DefaultValue = "24")]
    public Int32 GridPx { get => _GridPx; set { if (OnPropertyChanging("GridPx", value)) { _GridPx = value; OnPropertyChanged("GridPx"); } } }

    private String _ViewBox;
    /// <summary>viewBox</summary>
    [DisplayName("viewBox")]
    [Description("viewBox")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ViewBox", "viewBox", "")]
    public String ViewBox { get => _ViewBox; set { if (OnPropertyChanging("ViewBox", value)) { _ViewBox = value; OnPropertyChanged("ViewBox"); } } }

    private String _Sizes;
    /// <summary>可用光学尺寸阶，逗号分隔</summary>
    [DisplayName("可用光学尺寸阶")]
    [Description("可用光学尺寸阶，逗号分隔")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Sizes", "可用光学尺寸阶，逗号分隔", "")]
    public String Sizes { get => _Sizes; set { if (OnPropertyChanging("Sizes", value)) { _Sizes = value; OnPropertyChanged("Sizes"); } } }

    private String _Tags;
    /// <summary>检索标签</summary>
    [DisplayName("检索标签")]
    [Description("检索标签")]
    [DataObjectField(false, false, true, 300)]
    [BindColumn("Tags", "检索标签", "")]
    public String Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private String _Usage;
    /// <summary>语义槽/用途说明</summary>
    [DisplayName("语义槽_用途说明")]
    [Description("语义槽/用途说明")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Usage", "语义槽/用途说明", "")]
    public String Usage { get => _Usage; set { if (OnPropertyChanging("Usage", value)) { _Usage = value; OnPropertyChanged("Usage"); } } }

    private String _License;
    /// <summary>许可证：Owned（自绘）或导入集的真实许可</summary>
    [DisplayName("许可证")]
    [Description("许可证：Owned（自绘）或导入集的真实许可")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("License", "许可证：Owned（自绘）或导入集的真实许可", "", DefaultValue = "Owned")]
    public String License { get => _License; set { if (OnPropertyChanging("License", value)) { _License = value; OnPropertyChanged("License"); } } }

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
            "Collection" => _Collection,
            "SvgBody" => _SvgBody,
            "StrokeWidth" => _StrokeWidth,
            "GridPx" => _GridPx,
            "ViewBox" => _ViewBox,
            "Sizes" => _Sizes,
            "Tags" => _Tags,
            "Usage" => _Usage,
            "License" => _License,
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
                case "Collection": _Collection = Convert.ToString(value); break;
                case "SvgBody": _SvgBody = Convert.ToString(value); break;
                case "StrokeWidth": _StrokeWidth = value.ToDouble(); break;
                case "GridPx": _GridPx = value.ToInt(); break;
                case "ViewBox": _ViewBox = Convert.ToString(value); break;
                case "Sizes": _Sizes = Convert.ToString(value); break;
                case "Tags": _Tags = Convert.ToString(value); break;
                case "Usage": _Usage = Convert.ToString(value); break;
                case "License": _License = Convert.ToString(value); break;
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
    /// <summary>根据图标ID查找</summary>
    /// <param name="id">图标ID</param>
    /// <returns>实体对象</returns>
    public static DesignIcon FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、图标标识查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">图标标识</param>
    /// <returns>实体对象</returns>
    public static DesignIcon FindByProjectIdAndCode(Int64 projectId, String code)
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
    public static IList<DesignIcon> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据集合查找</summary>
    /// <param name="collection">集合</param>
    /// <returns>实体列表</returns>
    public static IList<DesignIcon> FindAllByCollection(String collection)
    {
        if (collection.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Collection.EqualIgnoreCase(collection));

        return FindAll(_.Collection == collection);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID；0=内置库（常量 BuiltinProjectId，只读）</param>
    /// <param name="code">图标标识</param>
    /// <param name="collection">集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignIcon> Search(Int64 projectId, String code, String collection, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!collection.IsNullOrEmpty()) exp &= _.Collection == collection;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计图标字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>图标ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID；0=内置库（常量 BuiltinProjectId，只读）</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>图标标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>图标名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名</summary>
        public static readonly Field Collection = FindByName("Collection");

        /// <summary>SVG 内容（path 数据或完整 svg 片段），为系统 of record</summary>
        public static readonly Field SvgBody = FindByName("SvgBody");

        /// <summary>描边宽度（令牌化项）</summary>
        public static readonly Field StrokeWidth = FindByName("StrokeWidth");

        /// <summary>基准栅格 px</summary>
        public static readonly Field GridPx = FindByName("GridPx");

        /// <summary>viewBox</summary>
        public static readonly Field ViewBox = FindByName("ViewBox");

        /// <summary>可用光学尺寸阶，逗号分隔</summary>
        public static readonly Field Sizes = FindByName("Sizes");

        /// <summary>检索标签</summary>
        public static readonly Field Tags = FindByName("Tags");

        /// <summary>语义槽/用途说明</summary>
        public static readonly Field Usage = FindByName("Usage");

        /// <summary>许可证：Owned（自绘）或导入集的真实许可</summary>
        public static readonly Field License = FindByName("License");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计图标字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>图标ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID；0=内置库（常量 BuiltinProjectId，只读）</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>图标标识</summary>
        public const String Code = "Code";

        /// <summary>图标名称</summary>
        public const String Name = "Name";

        /// <summary>集合：forge=本项目自绘内置零许可负担集；其余为用户导入集名</summary>
        public const String Collection = "Collection";

        /// <summary>SVG 内容（path 数据或完整 svg 片段），为系统 of record</summary>
        public const String SvgBody = "SvgBody";

        /// <summary>描边宽度（令牌化项）</summary>
        public const String StrokeWidth = "StrokeWidth";

        /// <summary>基准栅格 px</summary>
        public const String GridPx = "GridPx";

        /// <summary>viewBox</summary>
        public const String ViewBox = "ViewBox";

        /// <summary>可用光学尺寸阶，逗号分隔</summary>
        public const String Sizes = "Sizes";

        /// <summary>检索标签</summary>
        public const String Tags = "Tags";

        /// <summary>语义槽/用途说明</summary>
        public const String Usage = "Usage";

        /// <summary>许可证：Owned（自绘）或导入集的真实许可</summary>
        public const String License = "License";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
