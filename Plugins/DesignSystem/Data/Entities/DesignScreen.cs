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
[BindIndex("IU_DesignScreen_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignScreen_ProjectId_Route", false, "ProjectId,Route")]
[BindTable("DesignScreen", Description = "设计页面清单", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignScreen
{
    #region 属性
    private Int64 _Id;
    /// <summary>页面ID</summary>
    [DisplayName("页面ID")]
    [Description("页面ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "页面ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>页面标识</summary>
    [DisplayName("页面标识")]
    [Description("页面标识")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "页面标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Title;
    /// <summary>页面标题</summary>
    [DisplayName("页面标题")]
    [Description("页面标题")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Title", "页面标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _IconCode;
    /// <summary>引用图标标识</summary>
    [DisplayName("引用图标标识")]
    [Description("引用图标标识")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("IconCode", "引用图标标识", "")]
    public String IconCode { get => _IconCode; set { if (OnPropertyChanging("IconCode", value)) { _IconCode = value; OnPropertyChanged("IconCode"); } } }

    private String _Route;
    /// <summary>目标路由/深链</summary>
    [DisplayName("目标路由_深链")]
    [Description("目标路由/深链")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Route", "目标路由/深链", "")]
    public String Route { get => _Route; set { if (OnPropertyChanging("Route", value)) { _Route = value; OnPropertyChanged("Route"); } } }

    private Int64 _ThemeId;
    /// <summary>演示主题</summary>
    [DisplayName("演示主题")]
    [Description("演示主题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThemeId", "演示主题", "", DefaultValue = "0")]
    public Int64 ThemeId { get => _ThemeId; set { if (OnPropertyChanging("ThemeId", value)) { _ThemeId = value; OnPropertyChanged("ThemeId"); } } }

    private String _ComponentIdsJson;
    /// <summary>页面所用组件清单</summary>
    [DisplayName("页面所用组件清单")]
    [Description("页面所用组件清单")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ComponentIdsJson", "页面所用组件清单", "")]
    public String ComponentIdsJson { get => _ComponentIdsJson; set { if (OnPropertyChanging("ComponentIdsJson", value)) { _ComponentIdsJson = value; OnPropertyChanged("ComponentIdsJson"); } } }

    private String _Description;
    /// <summary>页面职责说明</summary>
    [DisplayName("页面职责说明")]
    [Description("页面职责说明")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "页面职责说明", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Notes;
    /// <summary>handoff 备注</summary>
    [DisplayName("handoff备注")]
    [Description("handoff 备注")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Notes", "handoff 备注", "")]
    public String Notes { get => _Notes; set { if (OnPropertyChanging("Notes", value)) { _Notes = value; OnPropertyChanged("Notes"); } } }

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
            "Title" => _Title,
            "IconCode" => _IconCode,
            "Route" => _Route,
            "ThemeId" => _ThemeId,
            "ComponentIdsJson" => _ComponentIdsJson,
            "Description" => _Description,
            "Notes" => _Notes,
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
                case "Title": _Title = Convert.ToString(value); break;
                case "IconCode": _IconCode = Convert.ToString(value); break;
                case "Route": _Route = Convert.ToString(value); break;
                case "ThemeId": _ThemeId = value.ToLong(); break;
                case "ComponentIdsJson": _ComponentIdsJson = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Notes": _Notes = Convert.ToString(value); break;
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
    /// <summary>根据页面ID查找</summary>
    /// <param name="id">页面ID</param>
    /// <returns>实体对象</returns>
    public static DesignScreen FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、页面标识查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">页面标识</param>
    /// <returns>实体对象</returns>
    public static DesignScreen FindByProjectIdAndCode(Int64 projectId, String code)
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
    public static IList<DesignScreen> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、目标路由_深链查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="route">目标路由_深链</param>
    /// <returns>实体列表</returns>
    public static IList<DesignScreen> FindAllByProjectIdAndRoute(Int64 projectId, String route)
    {
        if (projectId < 0) return [];
        if (route.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Route.EqualIgnoreCase(route));

        return FindAll(_.ProjectId == projectId & _.Route == route);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">页面标识</param>
    /// <param name="route">目标路由/深链</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignScreen> Search(Int64 projectId, String code, String route, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!route.IsNullOrEmpty()) exp &= _.Route == route;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计页面清单字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>页面ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>页面标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>页面标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>引用图标标识</summary>
        public static readonly Field IconCode = FindByName("IconCode");

        /// <summary>目标路由/深链</summary>
        public static readonly Field Route = FindByName("Route");

        /// <summary>演示主题</summary>
        public static readonly Field ThemeId = FindByName("ThemeId");

        /// <summary>页面所用组件清单</summary>
        public static readonly Field ComponentIdsJson = FindByName("ComponentIdsJson");

        /// <summary>页面职责说明</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>handoff 备注</summary>
        public static readonly Field Notes = FindByName("Notes");

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

    /// <summary>取得设计页面清单字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>页面ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>页面标识</summary>
        public const String Code = "Code";

        /// <summary>页面标题</summary>
        public const String Title = "Title";

        /// <summary>引用图标标识</summary>
        public const String IconCode = "IconCode";

        /// <summary>目标路由/深链</summary>
        public const String Route = "Route";

        /// <summary>演示主题</summary>
        public const String ThemeId = "ThemeId";

        /// <summary>页面所用组件清单</summary>
        public const String ComponentIdsJson = "ComponentIdsJson";

        /// <summary>页面职责说明</summary>
        public const String Description = "Description";

        /// <summary>handoff 备注</summary>
        public const String Notes = "Notes";

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
