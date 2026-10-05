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

/// <summary>{name}。UX 规范</summary>
[Serializable]
[DataObject]
[Description("{name}。UX 规范")]
[BindIndex("IU_DesignGuideline_ProjectId_Code", true, "ProjectId,Code")]
[BindIndex("IX_DesignGuideline_ProjectId_Category", false, "ProjectId,Category")]
[BindIndex("IX_DesignGuideline_ProjectId_Status", false, "ProjectId,Status")]
[BindTable("DesignGuideline", Description = "UX 规范", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignGuideline
{
    #region 属性
    private Int64 _Id;
    /// <summary>规范ID</summary>
    [DisplayName("规范ID")]
    [Description("规范ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "规范ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Code;
    /// <summary>规范标识（项目内唯一，kebab）</summary>
    [DisplayName("规范标识（项目内唯一")]
    [Description("规范标识（项目内唯一，kebab）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Code", "规范标识（项目内唯一，kebab）", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Category;
    /// <summary>分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data</summary>
    [DisplayName("分类")]
    [Description("分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Category", "分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data", "", DefaultValue = "layout")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Summary;
    /// <summary>一句话摘要</summary>
    [DisplayName("一句话摘要")]
    [Description("一句话摘要")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Summary", "一句话摘要", "")]
    public String Summary { get => _Summary; set { if (OnPropertyChanging("Summary", value)) { _Summary = value; OnPropertyChanged("Summary"); } } }

    private String _Body;
    /// <summary>正文（Markdown 文本；只写令牌路径不写数字）</summary>
    [DisplayName("正文（Markdown文本")]
    [Description("正文（Markdown 文本；只写令牌路径不写数字）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("Body", "正文（Markdown 文本；只写令牌路径不写数字）", "")]
    public String Body { get => _Body; set { if (OnPropertyChanging("Body", value)) { _Body = value; OnPropertyChanged("Body"); } } }

    private String _RulesJson;
    /// <summary>规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）</summary>
    [DisplayName("规则清单（JSON")]
    [Description("规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("RulesJson", "规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）", "")]
    public String RulesJson { get => _RulesJson; set { if (OnPropertyChanging("RulesJson", value)) { _RulesJson = value; OnPropertyChanged("RulesJson"); } } }

    private String _TokenRefsJson;
    /// <summary>引用的令牌路径（JSON 字符串数组）</summary>
    [DisplayName("引用的令牌路径（JSON字符串数组）")]
    [Description("引用的令牌路径（JSON 字符串数组）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("TokenRefsJson", "引用的令牌路径（JSON 字符串数组）", "")]
    public String TokenRefsJson { get => _TokenRefsJson; set { if (OnPropertyChanging("TokenRefsJson", value)) { _TokenRefsJson = value; OnPropertyChanged("TokenRefsJson"); } } }

    private String _AppliesToJson;
    /// <summary>适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）</summary>
    [DisplayName("适用用途类型（JSON字符串数组")]
    [Description("适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AppliesToJson", "适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）", "")]
    public String AppliesToJson { get => _AppliesToJson; set { if (OnPropertyChanging("AppliesToJson", value)) { _AppliesToJson = value; OnPropertyChanged("AppliesToJson"); } } }

    private String _Source;
    /// <summary>来源：generated|manual（manual 受重新生成保护）</summary>
    [DisplayName("来源")]
    [Description("来源：generated|manual（manual 受重新生成保护）")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Source", "来源：generated|manual（manual 受重新生成保护）", "", DefaultValue = "generated")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _Status;
    /// <summary>状态：adopted|draft|archived（归档为软删除）</summary>
    [DisplayName("状态")]
    [Description("状态：adopted|draft|archived（归档为软删除）")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Status", "状态：adopted|draft|archived（归档为软删除）", "", DefaultValue = "adopted")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _GeneratorVersion;
    /// <summary>生成器版本</summary>
    [DisplayName("生成器版本")]
    [Description("生成器版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("GeneratorVersion", "生成器版本", "")]
    public String GeneratorVersion { get => _GeneratorVersion; set { if (OnPropertyChanging("GeneratorVersion", value)) { _GeneratorVersion = value; OnPropertyChanged("GeneratorVersion"); } } }

    private String _GeneratorSeed;
    /// <summary>生成种子（kind/industry/density）</summary>
    [DisplayName("生成种子（kind_industry_density）")]
    [Description("生成种子（kind/industry/density）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("GeneratorSeed", "生成种子（kind/industry/density）", "")]
    public String GeneratorSeed { get => _GeneratorSeed; set { if (OnPropertyChanging("GeneratorSeed", value)) { _GeneratorSeed = value; OnPropertyChanged("GeneratorSeed"); } } }

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
    /// <summary>更新时间，兼作乐观并发依据</summary>
    [DisplayName("更新时间")]
    [Description("更新时间，兼作乐观并发依据")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间，兼作乐观并发依据", "")]
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
            "Category" => _Category,
            "Title" => _Title,
            "Summary" => _Summary,
            "Body" => _Body,
            "RulesJson" => _RulesJson,
            "TokenRefsJson" => _TokenRefsJson,
            "AppliesToJson" => _AppliesToJson,
            "Source" => _Source,
            "Status" => _Status,
            "GeneratorVersion" => _GeneratorVersion,
            "GeneratorSeed" => _GeneratorSeed,
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
                case "Category": _Category = Convert.ToString(value); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Summary": _Summary = Convert.ToString(value); break;
                case "Body": _Body = Convert.ToString(value); break;
                case "RulesJson": _RulesJson = Convert.ToString(value); break;
                case "TokenRefsJson": _TokenRefsJson = Convert.ToString(value); break;
                case "AppliesToJson": _AppliesToJson = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "GeneratorVersion": _GeneratorVersion = Convert.ToString(value); break;
                case "GeneratorSeed": _GeneratorSeed = Convert.ToString(value); break;
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
    /// <summary>根据规范ID查找</summary>
    /// <param name="id">规范ID</param>
    /// <returns>实体对象</returns>
    public static DesignGuideline FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、规范标识（项目内唯一查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">规范标识（项目内唯一</param>
    /// <returns>实体对象</returns>
    public static DesignGuideline FindByProjectIdAndCode(Int64 projectId, String code)
    {
        if (projectId < 0) return null;
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ProjectId == projectId && e.Code.EqualIgnoreCase(code));

        return Find(_.ProjectId == projectId & _.Code == code);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignGuideline> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、分类查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<DesignGuideline> FindAllByProjectIdAndCategory(Int64 projectId, String category)
    {
        if (projectId < 0) return [];
        if (category.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Category.EqualIgnoreCase(category));

        return FindAll(_.ProjectId == projectId & _.Category == category);
    }

    /// <summary>根据项目ID、状态查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<DesignGuideline> FindAllByProjectIdAndStatus(Int64 projectId, String status)
    {
        if (projectId < 0) return [];
        if (status.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Status.EqualIgnoreCase(status));

        return FindAll(_.ProjectId == projectId & _.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="code">规范标识（项目内唯一，kebab）</param>
    /// <param name="category">分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data</param>
    /// <param name="status">状态：adopted|draft|archived（归档为软删除）</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignGuideline> Search(Int64 projectId, String code, String category, String status, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得UX规范字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>规范ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>规范标识（项目内唯一，kebab）</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>一句话摘要</summary>
        public static readonly Field Summary = FindByName("Summary");

        /// <summary>正文（Markdown 文本；只写令牌路径不写数字）</summary>
        public static readonly Field Body = FindByName("Body");

        /// <summary>规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）</summary>
        public static readonly Field RulesJson = FindByName("RulesJson");

        /// <summary>引用的令牌路径（JSON 字符串数组）</summary>
        public static readonly Field TokenRefsJson = FindByName("TokenRefsJson");

        /// <summary>适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）</summary>
        public static readonly Field AppliesToJson = FindByName("AppliesToJson");

        /// <summary>来源：generated|manual（manual 受重新生成保护）</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>状态：adopted|draft|archived（归档为软删除）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>生成器版本</summary>
        public static readonly Field GeneratorVersion = FindByName("GeneratorVersion");

        /// <summary>生成种子（kind/industry/density）</summary>
        public static readonly Field GeneratorSeed = FindByName("GeneratorSeed");

        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间，兼作乐观并发依据</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得UX规范字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>规范ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>规范标识（项目内唯一，kebab）</summary>
        public const String Code = "Code";

        /// <summary>分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data</summary>
        public const String Category = "Category";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>一句话摘要</summary>
        public const String Summary = "Summary";

        /// <summary>正文（Markdown 文本；只写令牌路径不写数字）</summary>
        public const String Body = "Body";

        /// <summary>规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）</summary>
        public const String RulesJson = "RulesJson";

        /// <summary>引用的令牌路径（JSON 字符串数组）</summary>
        public const String TokenRefsJson = "TokenRefsJson";

        /// <summary>适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）</summary>
        public const String AppliesToJson = "AppliesToJson";

        /// <summary>来源：generated|manual（manual 受重新生成保护）</summary>
        public const String Source = "Source";

        /// <summary>状态：adopted|draft|archived（归档为软删除）</summary>
        public const String Status = "Status";

        /// <summary>生成器版本</summary>
        public const String GeneratorVersion = "GeneratorVersion";

        /// <summary>生成种子（kind/industry/density）</summary>
        public const String GeneratorSeed = "GeneratorSeed";

        /// <summary>排序</summary>
        public const String SortOrder = "SortOrder";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间，兼作乐观并发依据</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
