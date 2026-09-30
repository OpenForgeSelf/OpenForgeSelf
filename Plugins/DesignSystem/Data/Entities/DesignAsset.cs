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
[BindIndex("IU_DesignAsset_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignAsset_ProjectId_Kind", false, "ProjectId,Kind")]
[BindTable("DesignAsset", Description = "设计资产", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignAsset
{
    #region 属性
    private Int64 _Id;
    /// <summary>资产ID</summary>
    [DisplayName("资产ID")]
    [Description("资产ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "资产ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>资产标识</summary>
    [DisplayName("资产标识")]
    [Description("资产标识")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "资产标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>资产名称</summary>
    [DisplayName("资产名称")]
    [Description("资产名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "资产名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Kind;
    /// <summary>类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state</summary>
    [DisplayName("类型")]
    [Description("类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Kind", "类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state", "", DefaultValue = "logo")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _SvgBody;
    /// <summary>矢量内容（可引用 --ds-* 令牌变量）</summary>
    [DisplayName("矢量内容（可引用--ds-*令牌变量）")]
    [Description("矢量内容（可引用 --ds-* 令牌变量）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("SvgBody", "矢量内容（可引用 --ds-* 令牌变量）", "")]
    public String SvgBody { get => _SvgBody; set { if (OnPropertyChanging("SvgBody", value)) { _SvgBody = value; OnPropertyChanged("SvgBody"); } } }

    private String _FileRef;
    /// <summary>二进制/大文件在插件数据目录内的相对路径</summary>
    [DisplayName("二进制_大文件在插件数据目录内的相对路径")]
    [Description("二进制/大文件在插件数据目录内的相对路径")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("FileRef", "二进制/大文件在插件数据目录内的相对路径", "")]
    public String FileRef { get => _FileRef; set { if (OnPropertyChanging("FileRef", value)) { _FileRef = value; OnPropertyChanged("FileRef"); } } }

    private String _TokenRefsJson;
    /// <summary>资产所依赖的令牌引用</summary>
    [DisplayName("资产所依赖的令牌引用")]
    [Description("资产所依赖的令牌引用")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("TokenRefsJson", "资产所依赖的令牌引用", "")]
    public String TokenRefsJson { get => _TokenRefsJson; set { if (OnPropertyChanging("TokenRefsJson", value)) { _TokenRefsJson = value; OnPropertyChanged("TokenRefsJson"); } } }

    private String _Description;
    /// <summary>说明与使用规则</summary>
    [DisplayName("说明与使用规则")]
    [Description("说明与使用规则")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "说明与使用规则", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _License;
    /// <summary>许可证</summary>
    [DisplayName("许可证")]
    [Description("许可证")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("License", "许可证", "", DefaultValue = "Owned")]
    public String License { get => _License; set { if (OnPropertyChanging("License", value)) { _License = value; OnPropertyChanged("License"); } } }

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
            "Kind" => _Kind,
            "SvgBody" => _SvgBody,
            "FileRef" => _FileRef,
            "TokenRefsJson" => _TokenRefsJson,
            "Description" => _Description,
            "License" => _License,
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
                case "Kind": _Kind = Convert.ToString(value); break;
                case "SvgBody": _SvgBody = Convert.ToString(value); break;
                case "FileRef": _FileRef = Convert.ToString(value); break;
                case "TokenRefsJson": _TokenRefsJson = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "License": _License = Convert.ToString(value); break;
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
    /// <summary>根据资产ID查找</summary>
    /// <param name="id">资产ID</param>
    /// <returns>实体对象</returns>
    public static DesignAsset FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、资产标识查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">资产标识</param>
    /// <returns>实体对象</returns>
    public static DesignAsset FindByProjectIdAndCode(Int64 projectId, String code)
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
    public static IList<DesignAsset> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、类型查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="kind">类型</param>
    /// <returns>实体列表</returns>
    public static IList<DesignAsset> FindAllByProjectIdAndKind(Int64 projectId, String kind)
    {
        if (projectId < 0) return [];
        if (kind.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Kind.EqualIgnoreCase(kind));

        return FindAll(_.ProjectId == projectId & _.Kind == kind);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">资产标识</param>
    /// <param name="kind">类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignAsset> Search(Int64 projectId, String code, String kind, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计资产字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>资产ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>资产标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>资产名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>矢量内容（可引用 --ds-* 令牌变量）</summary>
        public static readonly Field SvgBody = FindByName("SvgBody");

        /// <summary>二进制/大文件在插件数据目录内的相对路径</summary>
        public static readonly Field FileRef = FindByName("FileRef");

        /// <summary>资产所依赖的令牌引用</summary>
        public static readonly Field TokenRefsJson = FindByName("TokenRefsJson");

        /// <summary>说明与使用规则</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>许可证</summary>
        public static readonly Field License = FindByName("License");

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

    /// <summary>取得设计资产字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>资产ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>资产标识</summary>
        public const String Code = "Code";

        /// <summary>资产名称</summary>
        public const String Name = "Name";

        /// <summary>类型：logo|logo-mark|motif|illustration|gradient|texture|avatar|empty-state</summary>
        public const String Kind = "Kind";

        /// <summary>矢量内容（可引用 --ds-* 令牌变量）</summary>
        public const String SvgBody = "SvgBody";

        /// <summary>二进制/大文件在插件数据目录内的相对路径</summary>
        public const String FileRef = "FileRef";

        /// <summary>资产所依赖的令牌引用</summary>
        public const String TokenRefsJson = "TokenRefsJson";

        /// <summary>说明与使用规则</summary>
        public const String Description = "Description";

        /// <summary>许可证</summary>
        public const String License = "License";

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
