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

namespace ForgeSelf.Api.Plugins.ScriptRunner.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_CodeSnippet_Category", false, "Category")]
[BindIndex("IX_CodeSnippet_Language", false, "Language")]
[BindTable("CodeSnippet", Description = "代码片段", ConnName = "ScriptRunner", DbType = DatabaseType.None)]
public partial class CodeSnippet
{
    #region 属性
    private Int64 _Id;
    /// <summary>片段ID</summary>
    [DisplayName("片段ID")]
    [Description("片段ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "片段ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Code;
    /// <summary>代码</summary>
    [DisplayName("代码")]
    [Description("代码")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Code", "代码", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Language;
    /// <summary>语言</summary>
    [DisplayName("语言")]
    [Description("语言")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Language", "语言", "")]
    public String Language { get => _Language; set { if (OnPropertyChanging("Language", value)) { _Language = value; OnPropertyChanged("Language"); } } }

    private String _Category;
    /// <summary>分类</summary>
    [DisplayName("分类")]
    [Description("分类")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Category", "分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _TagsJson;
    /// <summary>标签JSON</summary>
    [DisplayName("标签JSON")]
    [Description("标签JSON")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("TagsJson", "标签JSON", "")]
    public String TagsJson { get => _TagsJson; set { if (OnPropertyChanging("TagsJson", value)) { _TagsJson = value; OnPropertyChanged("TagsJson"); } } }

    private Boolean _IsFavorite;
    /// <summary>是否收藏</summary>
    [DisplayName("是否收藏")]
    [Description("是否收藏")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsFavorite", "是否收藏", "")]
    public Boolean IsFavorite { get => _IsFavorite; set { if (OnPropertyChanging("IsFavorite", value)) { _IsFavorite = value; OnPropertyChanged("IsFavorite"); } } }

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

    private Int32 _UsageCount;
    /// <summary>使用次数</summary>
    [DisplayName("使用次数")]
    [Description("使用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UsageCount", "使用次数", "")]
    public Int32 UsageCount { get => _UsageCount; set { if (OnPropertyChanging("UsageCount", value)) { _UsageCount = value; OnPropertyChanged("UsageCount"); } } }

    private DateTime _LastUsedAt;
    /// <summary>最后使用时间</summary>
    [DisplayName("最后使用时间")]
    [Description("最后使用时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastUsedAt", "最后使用时间", "")]
    public DateTime LastUsedAt { get => _LastUsedAt; set { if (OnPropertyChanging("LastUsedAt", value)) { _LastUsedAt = value; OnPropertyChanged("LastUsedAt"); } } }

    private Int32 _Source;
    /// <summary>来源</summary>
    [DisplayName("来源")]
    [Description("来源")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Source", "来源", "")]
    public Int32 Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int64 _SourceScriptId;
    /// <summary>来源脚本ID</summary>
    [DisplayName("来源脚本ID")]
    [Description("来源脚本ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SourceScriptId", "来源脚本ID", "")]
    public Int64 SourceScriptId { get => _SourceScriptId; set { if (OnPropertyChanging("SourceScriptId", value)) { _SourceScriptId = value; OnPropertyChanged("SourceScriptId"); } } }
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
            "Title" => _Title,
            "Description" => _Description,
            "Code" => _Code,
            "Language" => _Language,
            "Category" => _Category,
            "TagsJson" => _TagsJson,
            "IsFavorite" => _IsFavorite,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "UsageCount" => _UsageCount,
            "LastUsedAt" => _LastUsedAt,
            "Source" => _Source,
            "SourceScriptId" => _SourceScriptId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Language": _Language = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "TagsJson": _TagsJson = Convert.ToString(value); break;
                case "IsFavorite": _IsFavorite = value.ToBoolean(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "UsageCount": _UsageCount = value.ToInt(); break;
                case "LastUsedAt": _LastUsedAt = value.ToDateTime(); break;
                case "Source": _Source = value.ToInt(); break;
                case "SourceScriptId": _SourceScriptId = value.ToLong(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据片段ID查找</summary>
    /// <param name="id">片段ID</param>
    /// <returns>实体对象</returns>
    public static CodeSnippet FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据分类查找</summary>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<CodeSnippet> FindAllByCategory(String category)
    {
        if (category.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

        return FindAll(_.Category == category);
    }

    /// <summary>根据语言查找</summary>
    /// <param name="language">语言</param>
    /// <returns>实体列表</returns>
    public static IList<CodeSnippet> FindAllByLanguage(String language)
    {
        if (language.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Language.EqualIgnoreCase(language));

        return FindAll(_.Language == language);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="language">语言</param>
    /// <param name="category">分类</param>
    /// <param name="isFavorite">是否收藏</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<CodeSnippet> Search(String language, String category, Boolean? isFavorite, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!language.IsNullOrEmpty()) exp &= _.Language == language;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (isFavorite != null) exp &= _.IsFavorite == isFavorite;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得代码片段字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>片段ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>代码</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>语言</summary>
        public static readonly Field Language = FindByName("Language");

        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>标签JSON</summary>
        public static readonly Field TagsJson = FindByName("TagsJson");

        /// <summary>是否收藏</summary>
        public static readonly Field IsFavorite = FindByName("IsFavorite");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        /// <summary>使用次数</summary>
        public static readonly Field UsageCount = FindByName("UsageCount");

        /// <summary>最后使用时间</summary>
        public static readonly Field LastUsedAt = FindByName("LastUsedAt");

        /// <summary>来源</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>来源脚本ID</summary>
        public static readonly Field SourceScriptId = FindByName("SourceScriptId");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得代码片段字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>片段ID</summary>
        public const String Id = "Id";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>描述</summary>
        public const String Description = "Description";

        /// <summary>代码</summary>
        public const String Code = "Code";

        /// <summary>语言</summary>
        public const String Language = "Language";

        /// <summary>分类</summary>
        public const String Category = "Category";

        /// <summary>标签JSON</summary>
        public const String TagsJson = "TagsJson";

        /// <summary>是否收藏</summary>
        public const String IsFavorite = "IsFavorite";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";

        /// <summary>使用次数</summary>
        public const String UsageCount = "UsageCount";

        /// <summary>最后使用时间</summary>
        public const String LastUsedAt = "LastUsedAt";

        /// <summary>来源</summary>
        public const String Source = "Source";

        /// <summary>来源脚本ID</summary>
        public const String SourceScriptId = "SourceScriptId";
    }
    #endregion
}
