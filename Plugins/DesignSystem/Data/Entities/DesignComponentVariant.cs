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
[BindIndex("IU_DesignComponentVariant_ComponentId_VariantKey_State_ThemeId", true, "ComponentId,VariantKey,State,ThemeId")]
[BindIndex("IX_DesignComponentVariant_ProjectId_State", false, "ProjectId,State")]
[BindTable("DesignComponentVariant", Description = "组件变体", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignComponentVariant
{
    #region 属性
    private Int64 _Id;
    /// <summary>变体ID</summary>
    [DisplayName("变体ID")]
    [Description("变体ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "变体ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private Int64 _ComponentId;
    /// <summary>所属组件ID</summary>
    [DisplayName("所属组件ID")]
    [Description("所属组件ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ComponentId", "所属组件ID", "")]
    public Int64 ComponentId { get => _ComponentId; set { if (OnPropertyChanging("ComponentId", value)) { _ComponentId = value; OnPropertyChanged("ComponentId"); } } }

    private String _ComponentCode;
    /// <summary>所属组件标识（冗余便于查询）</summary>
    [DisplayName("所属组件标识（冗余便于查询）")]
    [Description("所属组件标识（冗余便于查询）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ComponentCode", "所属组件标识（冗余便于查询）", "")]
    public String ComponentCode { get => _ComponentCode; set { if (OnPropertyChanging("ComponentCode", value)) { _ComponentCode = value; OnPropertyChanged("ComponentCode"); } } }

    private String _Code;
    /// <summary>变体显示码</summary>
    [DisplayName("变体显示码")]
    [Description("变体显示码")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Code", "变体显示码", "", Master = true)]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _VariantKey;
    /// <summary>变体规范化键（键序稳定，保证幂等 upsert）</summary>
    [DisplayName("变体规范化键（键序稳定")]
    [Description("变体规范化键（键序稳定，保证幂等 upsert）")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("VariantKey", "变体规范化键（键序稳定，保证幂等 upsert）", "")]
    public String VariantKey { get => _VariantKey; set { if (OnPropertyChanging("VariantKey", value)) { _VariantKey = value; OnPropertyChanged("VariantKey"); } } }

    private String _VariantJson;
    /// <summary>变体属性（如 {size:lg,tone:brand}）</summary>
    [DisplayName("变体属性（如{size")]
    [Description("变体属性（如 {size:lg,tone:brand}）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("VariantJson", "变体属性（如 {size:lg,tone:brand}）", "")]
    public String VariantJson { get => _VariantJson; set { if (OnPropertyChanging("VariantJson", value)) { _VariantJson = value; OnPropertyChanged("VariantJson"); } } }

    private String _State;
    /// <summary>状态：default|hover|active|focus-visible|disabled|loading|error|selected</summary>
    [DisplayName("状态")]
    [Description("状态：default|hover|active|focus-visible|disabled|loading|error|selected")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("State", "状态：default|hover|active|focus-visible|disabled|loading|error|selected", "", DefaultValue = "default")]
    public String State { get => _State; set { if (OnPropertyChanging("State", value)) { _State = value; OnPropertyChanged("State"); } } }

    private Int64 _ThemeId;
    /// <summary>主题ID，0=全主题</summary>
    [DisplayName("主题ID")]
    [Description("主题ID，0=全主题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThemeId", "主题ID，0=全主题", "", DefaultValue = "0")]
    public Int64 ThemeId { get => _ThemeId; set { if (OnPropertyChanging("ThemeId", value)) { _ThemeId = value; OnPropertyChanged("ThemeId"); } } }

    private String _Name;
    /// <summary>变体名称</summary>
    [DisplayName("变体名称")]
    [Description("变体名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "变体名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _TokenRefsJson;
    /// <summary>该变体的令牌引用映射（背景/文字/边框等 → 令牌路径）</summary>
    [DisplayName("该变体的令牌引用映射（背景_文字_边框等→令牌路径）")]
    [Description("该变体的令牌引用映射（背景/文字/边框等 → 令牌路径）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("TokenRefsJson", "该变体的令牌引用映射（背景/文字/边框等 → 令牌路径）", "")]
    public String TokenRefsJson { get => _TokenRefsJson; set { if (OnPropertyChanging("TokenRefsJson", value)) { _TokenRefsJson = value; OnPropertyChanged("TokenRefsJson"); } } }

    private String _CssSnippet;
    /// <summary>由令牌引用生成的 CSS 片段（演示与 handoff 用）</summary>
    [DisplayName("由令牌引用生成的CSS片段（演示与handoff用）")]
    [Description("由令牌引用生成的 CSS 片段（演示与 handoff 用）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("CssSnippet", "由令牌引用生成的 CSS 片段（演示与 handoff 用）", "")]
    public String CssSnippet { get => _CssSnippet; set { if (OnPropertyChanging("CssSnippet", value)) { _CssSnippet = value; OnPropertyChanged("CssSnippet"); } } }

    private String _ContrastSummary;
    /// <summary>该变体主要色对的对比度摘要</summary>
    [DisplayName("该变体主要色对的对比度摘要")]
    [Description("该变体主要色对的对比度摘要")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ContrastSummary", "该变体主要色对的对比度摘要", "")]
    public String ContrastSummary { get => _ContrastSummary; set { if (OnPropertyChanging("ContrastSummary", value)) { _ContrastSummary = value; OnPropertyChanged("ContrastSummary"); } } }

    private String _Description;
    /// <summary>说明</summary>
    [DisplayName("说明")]
    [Description("说明")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "说明", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

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
            "ComponentId" => _ComponentId,
            "ComponentCode" => _ComponentCode,
            "Code" => _Code,
            "VariantKey" => _VariantKey,
            "VariantJson" => _VariantJson,
            "State" => _State,
            "ThemeId" => _ThemeId,
            "Name" => _Name,
            "TokenRefsJson" => _TokenRefsJson,
            "CssSnippet" => _CssSnippet,
            "ContrastSummary" => _ContrastSummary,
            "Description" => _Description,
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
                case "ComponentId": _ComponentId = value.ToLong(); break;
                case "ComponentCode": _ComponentCode = Convert.ToString(value); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "VariantKey": _VariantKey = Convert.ToString(value); break;
                case "VariantJson": _VariantJson = Convert.ToString(value); break;
                case "State": _State = Convert.ToString(value); break;
                case "ThemeId": _ThemeId = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "TokenRefsJson": _TokenRefsJson = Convert.ToString(value); break;
                case "CssSnippet": _CssSnippet = Convert.ToString(value); break;
                case "ContrastSummary": _ContrastSummary = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
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
    /// <summary>根据变体ID查找</summary>
    /// <param name="id">变体ID</param>
    /// <returns>实体对象</returns>
    public static DesignComponentVariant FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、状态查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="state">状态</param>
    /// <returns>实体列表</returns>
    public static IList<DesignComponentVariant> FindAllByProjectIdAndState(Int64 projectId, String state)
    {
        if (projectId < 0) return [];
        if (state.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.State.EqualIgnoreCase(state));

        return FindAll(_.ProjectId == projectId & _.State == state);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="componentId">所属组件ID</param>
    /// <param name="variantKey">变体规范化键（键序稳定，保证幂等 upsert）</param>
    /// <param name="state">状态：default|hover|active|focus-visible|disabled|loading|error|selected</param>
    /// <param name="themeId">主题ID，0=全主题</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignComponentVariant> Search(Int64 projectId, Int64 componentId, String variantKey, String state, Int64 themeId, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (componentId >= 0) exp &= _.ComponentId == componentId;
        if (!variantKey.IsNullOrEmpty()) exp &= _.VariantKey == variantKey;
        if (!state.IsNullOrEmpty()) exp &= _.State == state;
        if (themeId >= 0) exp &= _.ThemeId == themeId;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得组件变体字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>变体ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>所属组件ID</summary>
        public static readonly Field ComponentId = FindByName("ComponentId");

        /// <summary>所属组件标识（冗余便于查询）</summary>
        public static readonly Field ComponentCode = FindByName("ComponentCode");

        /// <summary>变体显示码</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>变体规范化键（键序稳定，保证幂等 upsert）</summary>
        public static readonly Field VariantKey = FindByName("VariantKey");

        /// <summary>变体属性（如 {size:lg,tone:brand}）</summary>
        public static readonly Field VariantJson = FindByName("VariantJson");

        /// <summary>状态：default|hover|active|focus-visible|disabled|loading|error|selected</summary>
        public static readonly Field State = FindByName("State");

        /// <summary>主题ID，0=全主题</summary>
        public static readonly Field ThemeId = FindByName("ThemeId");

        /// <summary>变体名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>该变体的令牌引用映射（背景/文字/边框等 → 令牌路径）</summary>
        public static readonly Field TokenRefsJson = FindByName("TokenRefsJson");

        /// <summary>由令牌引用生成的 CSS 片段（演示与 handoff 用）</summary>
        public static readonly Field CssSnippet = FindByName("CssSnippet");

        /// <summary>该变体主要色对的对比度摘要</summary>
        public static readonly Field ContrastSummary = FindByName("ContrastSummary");

        /// <summary>说明</summary>
        public static readonly Field Description = FindByName("Description");

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

    /// <summary>取得组件变体字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>变体ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>所属组件ID</summary>
        public const String ComponentId = "ComponentId";

        /// <summary>所属组件标识（冗余便于查询）</summary>
        public const String ComponentCode = "ComponentCode";

        /// <summary>变体显示码</summary>
        public const String Code = "Code";

        /// <summary>变体规范化键（键序稳定，保证幂等 upsert）</summary>
        public const String VariantKey = "VariantKey";

        /// <summary>变体属性（如 {size:lg,tone:brand}）</summary>
        public const String VariantJson = "VariantJson";

        /// <summary>状态：default|hover|active|focus-visible|disabled|loading|error|selected</summary>
        public const String State = "State";

        /// <summary>主题ID，0=全主题</summary>
        public const String ThemeId = "ThemeId";

        /// <summary>变体名称</summary>
        public const String Name = "Name";

        /// <summary>该变体的令牌引用映射（背景/文字/边框等 → 令牌路径）</summary>
        public const String TokenRefsJson = "TokenRefsJson";

        /// <summary>由令牌引用生成的 CSS 片段（演示与 handoff 用）</summary>
        public const String CssSnippet = "CssSnippet";

        /// <summary>该变体主要色对的对比度摘要</summary>
        public const String ContrastSummary = "ContrastSummary";

        /// <summary>说明</summary>
        public const String Description = "Description";

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
