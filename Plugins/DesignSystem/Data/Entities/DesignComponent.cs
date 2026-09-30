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
[BindIndex("IU_DesignComponent_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignComponent_ProjectId_Category", false, "ProjectId,Category")]
[BindIndex("IX_DesignComponent_ProjectId_Status", false, "ProjectId,Status")]
[BindTable("DesignComponent", Description = "设计组件", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignComponent
{
    #region 属性
    private Int64 _Id;
    /// <summary>组件ID</summary>
    [DisplayName("组件ID")]
    [Description("组件ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "组件ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>组件标识</summary>
    [DisplayName("组件标识")]
    [Description("组件标识")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "组件标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>组件名称</summary>
    [DisplayName("组件名称")]
    [Description("组件名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "组件名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>分类：primitive|shell|page|kit-chrome|composite</summary>
    [DisplayName("分类")]
    [Description("分类：primitive|shell|page|kit-chrome|composite")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Category", "分类：primitive|shell|page|kit-chrome|composite", "", DefaultValue = "primitive")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Boolean _Interactive;
    /// <summary>是否可交互组件</summary>
    [DisplayName("是否可交互组件")]
    [Description("是否可交互组件")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Interactive", "是否可交互组件", "", DefaultValue = "1")]
    public Boolean Interactive { get => _Interactive; set { if (OnPropertyChanging("Interactive", value)) { _Interactive = value; OnPropertyChanged("Interactive"); } } }

    private String _Description;
    /// <summary>职责说明</summary>
    [DisplayName("职责说明")]
    [Description("职责说明")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "职责说明", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _DocJson;
    /// <summary>props/API 表（JSON）</summary>
    [DisplayName("props_API表（JSON）")]
    [Description("props/API 表（JSON）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("DocJson", "props/API 表（JSON）", "")]
    public String DocJson { get => _DocJson; set { if (OnPropertyChanging("DocJson", value)) { _DocJson = value; OnPropertyChanged("DocJson"); } } }

    private String _GuidanceJson;
    /// <summary>do / don't 使用指引（JSON）</summary>
    [DisplayName("do_don't使用指引（JSON）")]
    [Description("do / don't 使用指引（JSON）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("GuidanceJson", "do / don't 使用指引（JSON）", "")]
    public String GuidanceJson { get => _GuidanceJson; set { if (OnPropertyChanging("GuidanceJson", value)) { _GuidanceJson = value; OnPropertyChanged("GuidanceJson"); } } }

    private String _A11yNotes;
    /// <summary>逐组件可达性标注（角色/键盘/focus 要求）</summary>
    [DisplayName("逐组件可达性标注（角色_键盘_focus要求）")]
    [Description("逐组件可达性标注（角色/键盘/focus 要求）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("A11yNotes", "逐组件可达性标注（角色/键盘/focus 要求）", "")]
    public String A11yNotes { get => _A11yNotes; set { if (OnPropertyChanging("A11yNotes", value)) { _A11yNotes = value; OnPropertyChanged("A11yNotes"); } } }

    private String _TokenRefsJson;
    /// <summary>组件级令牌引用（默认态）</summary>
    [DisplayName("组件级令牌引用（默认态）")]
    [Description("组件级令牌引用（默认态）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("TokenRefsJson", "组件级令牌引用（默认态）", "")]
    public String TokenRefsJson { get => _TokenRefsJson; set { if (OnPropertyChanging("TokenRefsJson", value)) { _TokenRefsJson = value; OnPropertyChanged("TokenRefsJson"); } } }

    private String _Status;
    /// <summary>成熟度：demo|ready|deprecated</summary>
    [DisplayName("成熟度")]
    [Description("成熟度：demo|ready|deprecated")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Status", "成熟度：demo|ready|deprecated", "", DefaultValue = "demo")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _Version;
    /// <summary>组件自身版本</summary>
    [DisplayName("组件自身版本")]
    [Description("组件自身版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Version", "组件自身版本", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _SourceRef;
    /// <summary>实现来源引用（文件/包）</summary>
    [DisplayName("实现来源引用（文件_包）")]
    [Description("实现来源引用（文件/包）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("SourceRef", "实现来源引用（文件/包）", "")]
    public String SourceRef { get => _SourceRef; set { if (OnPropertyChanging("SourceRef", value)) { _SourceRef = value; OnPropertyChanged("SourceRef"); } } }

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
            "Category" => _Category,
            "Interactive" => _Interactive,
            "Description" => _Description,
            "DocJson" => _DocJson,
            "GuidanceJson" => _GuidanceJson,
            "A11yNotes" => _A11yNotes,
            "TokenRefsJson" => _TokenRefsJson,
            "Status" => _Status,
            "Version" => _Version,
            "SourceRef" => _SourceRef,
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
                case "Category": _Category = Convert.ToString(value); break;
                case "Interactive": _Interactive = value.ToBoolean(); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "DocJson": _DocJson = Convert.ToString(value); break;
                case "GuidanceJson": _GuidanceJson = Convert.ToString(value); break;
                case "A11yNotes": _A11yNotes = Convert.ToString(value); break;
                case "TokenRefsJson": _TokenRefsJson = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "SourceRef": _SourceRef = Convert.ToString(value); break;
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
    /// <summary>根据组件ID查找</summary>
    /// <param name="id">组件ID</param>
    /// <returns>实体对象</returns>
    public static DesignComponent FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、组件标识查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">组件标识</param>
    /// <returns>实体对象</returns>
    public static DesignComponent FindByProjectIdAndCode(Int64 projectId, String code)
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
    public static IList<DesignComponent> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、分类查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<DesignComponent> FindAllByProjectIdAndCategory(Int64 projectId, String category)
    {
        if (projectId < 0) return [];
        if (category.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Category.EqualIgnoreCase(category));

        return FindAll(_.ProjectId == projectId & _.Category == category);
    }

    /// <summary>根据项目ID、成熟度查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="status">成熟度</param>
    /// <returns>实体列表</returns>
    public static IList<DesignComponent> FindAllByProjectIdAndStatus(Int64 projectId, String status)
    {
        if (projectId < 0) return [];
        if (status.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Status.EqualIgnoreCase(status));

        return FindAll(_.ProjectId == projectId & _.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">组件标识</param>
    /// <param name="category">分类：primitive|shell|page|kit-chrome|composite</param>
    /// <param name="status">成熟度：demo|ready|deprecated</param>
    /// <param name="interactive">是否可交互组件</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignComponent> Search(Int64 projectId, String code, String category, String status, Boolean? interactive, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        if (interactive != null) exp &= _.Interactive == interactive;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计组件字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>组件ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>组件标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>组件名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>分类：primitive|shell|page|kit-chrome|composite</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>是否可交互组件</summary>
        public static readonly Field Interactive = FindByName("Interactive");

        /// <summary>职责说明</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>props/API 表（JSON）</summary>
        public static readonly Field DocJson = FindByName("DocJson");

        /// <summary>do / don't 使用指引（JSON）</summary>
        public static readonly Field GuidanceJson = FindByName("GuidanceJson");

        /// <summary>逐组件可达性标注（角色/键盘/focus 要求）</summary>
        public static readonly Field A11yNotes = FindByName("A11yNotes");

        /// <summary>组件级令牌引用（默认态）</summary>
        public static readonly Field TokenRefsJson = FindByName("TokenRefsJson");

        /// <summary>成熟度：demo|ready|deprecated</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>组件自身版本</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>实现来源引用（文件/包）</summary>
        public static readonly Field SourceRef = FindByName("SourceRef");

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

    /// <summary>取得设计组件字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>组件ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>组件标识</summary>
        public const String Code = "Code";

        /// <summary>组件名称</summary>
        public const String Name = "Name";

        /// <summary>分类：primitive|shell|page|kit-chrome|composite</summary>
        public const String Category = "Category";

        /// <summary>是否可交互组件</summary>
        public const String Interactive = "Interactive";

        /// <summary>职责说明</summary>
        public const String Description = "Description";

        /// <summary>props/API 表（JSON）</summary>
        public const String DocJson = "DocJson";

        /// <summary>do / don't 使用指引（JSON）</summary>
        public const String GuidanceJson = "GuidanceJson";

        /// <summary>逐组件可达性标注（角色/键盘/focus 要求）</summary>
        public const String A11yNotes = "A11yNotes";

        /// <summary>组件级令牌引用（默认态）</summary>
        public const String TokenRefsJson = "TokenRefsJson";

        /// <summary>成熟度：demo|ready|deprecated</summary>
        public const String Status = "Status";

        /// <summary>组件自身版本</summary>
        public const String Version = "Version";

        /// <summary>实现来源引用（文件/包）</summary>
        public const String SourceRef = "SourceRef";

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
