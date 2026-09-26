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

namespace ForgeSelf.Api.Plugins.MemorySystem.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_Memory_Type", false, "Type")]
[BindIndex("IX_Memory_CategoryId", false, "CategoryId")]
[BindIndex("IX_Memory_Importance", false, "Importance")]
[BindIndex("IX_Memory_CreatedAt", false, "CreatedAt")]
[BindTable("Memory", Description = "记忆", ConnName = "MemorySystem", DbType = DatabaseType.None)]
public partial class Memory
{
    #region 属性
    private Int64 _Id;
    /// <summary>记忆ID</summary>
    [DisplayName("记忆ID")]
    [Description("记忆ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "记忆ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, false, 500)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Content;
    /// <summary>内容</summary>
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, false, 4000)]
    [BindColumn("Content", "内容", "")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private Int32 _Type;
    /// <summary>类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）</summary>
    [DisplayName("类型（0事实1偏好2项目3个人4工作流5技能99其他）")]
    [Description("类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Type", "类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）", "")]
    public Int32 Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private Int32 _Importance;
    /// <summary>重要度（0低 1中 2高 3关键）</summary>
    [DisplayName("重要度（0低1中2高3关键）")]
    [Description("重要度（0低 1中 2高 3关键）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Importance", "重要度（0低 1中 2高 3关键）", "")]
    public Int32 Importance { get => _Importance; set { if (OnPropertyChanging("Importance", value)) { _Importance = value; OnPropertyChanged("Importance"); } } }

    private String _Tags;
    /// <summary>标签</summary>
    [DisplayName("标签")]
    [Description("标签")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Tags", "标签", "")]
    public String Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private String _Source;
    /// <summary>来源</summary>
    [DisplayName("来源")]
    [Description("来源")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Source", "来源", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int64 _CategoryId;
    /// <summary>分类ID</summary>
    [DisplayName("分类ID")]
    [Description("分类ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CategoryId", "分类ID", "")]
    public Int64 CategoryId { get => _CategoryId; set { if (OnPropertyChanging("CategoryId", value)) { _CategoryId = value; OnPropertyChanged("CategoryId"); } } }

    private Int32 _AccessCount;
    /// <summary>访问次数</summary>
    [DisplayName("访问次数")]
    [Description("访问次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AccessCount", "访问次数", "")]
    public Int32 AccessCount { get => _AccessCount; set { if (OnPropertyChanging("AccessCount", value)) { _AccessCount = value; OnPropertyChanged("AccessCount"); } } }

    private DateTime _LastAccessedAt;
    /// <summary>最后访问时间</summary>
    [DisplayName("最后访问时间")]
    [Description("最后访问时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastAccessedAt", "最后访问时间", "")]
    public DateTime LastAccessedAt { get => _LastAccessedAt; set { if (OnPropertyChanging("LastAccessedAt", value)) { _LastAccessedAt = value; OnPropertyChanged("LastAccessedAt"); } } }

    private Double _DecayScore;
    /// <summary>衰减分数</summary>
    [DisplayName("衰减分数")]
    [Description("衰减分数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DecayScore", "衰减分数", "")]
    public Double DecayScore { get => _DecayScore; set { if (OnPropertyChanging("DecayScore", value)) { _DecayScore = value; OnPropertyChanged("DecayScore"); } } }

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

    private Boolean _IsDeleted;
    /// <summary>是否删除</summary>
    [DisplayName("是否删除")]
    [Description("是否删除")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsDeleted", "是否删除", "")]
    public Boolean IsDeleted { get => _IsDeleted; set { if (OnPropertyChanging("IsDeleted", value)) { _IsDeleted = value; OnPropertyChanged("IsDeleted"); } } }
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
            "Content" => _Content,
            "Type" => _Type,
            "Importance" => _Importance,
            "Tags" => _Tags,
            "Source" => _Source,
            "CategoryId" => _CategoryId,
            "AccessCount" => _AccessCount,
            "LastAccessedAt" => _LastAccessedAt,
            "DecayScore" => _DecayScore,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "IsDeleted" => _IsDeleted,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "Type": _Type = value.ToInt(); break;
                case "Importance": _Importance = value.ToInt(); break;
                case "Tags": _Tags = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "CategoryId": _CategoryId = value.ToLong(); break;
                case "AccessCount": _AccessCount = value.ToInt(); break;
                case "LastAccessedAt": _LastAccessedAt = value.ToDateTime(); break;
                case "DecayScore": _DecayScore = value.ToDouble(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "IsDeleted": _IsDeleted = value.ToBoolean(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据记忆ID查找</summary>
    /// <param name="id">记忆ID</param>
    /// <returns>实体对象</returns>
    public static Memory FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据类型（0事实1偏好2项目3个人4工作流5技能99其他）查找</summary>
    /// <param name="type">类型（0事实1偏好2项目3个人4工作流5技能99其他）</param>
    /// <returns>实体列表</returns>
    public static IList<Memory> FindAllByType(Int32 type)
    {
        if (type < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Type == type);

        return FindAll(_.Type == type);
    }

    /// <summary>根据分类ID查找</summary>
    /// <param name="categoryId">分类ID</param>
    /// <returns>实体列表</returns>
    public static IList<Memory> FindAllByCategoryId(Int64 categoryId)
    {
        if (categoryId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CategoryId == categoryId);

        return FindAll(_.CategoryId == categoryId);
    }

    /// <summary>根据重要度（0低1中2高3关键）查找</summary>
    /// <param name="importance">重要度（0低1中2高3关键）</param>
    /// <returns>实体列表</returns>
    public static IList<Memory> FindAllByImportance(Int32 importance)
    {
        if (importance < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Importance == importance);

        return FindAll(_.Importance == importance);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="type">类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）</param>
    /// <param name="importance">重要度（0低 1中 2高 3关键）</param>
    /// <param name="categoryId">分类ID</param>
    /// <param name="isDeleted">是否删除</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Memory> Search(Int32 type, Int32 importance, Int64 categoryId, Boolean? isDeleted, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (type >= 0) exp &= _.Type == type;
        if (importance >= 0) exp &= _.Importance == importance;
        if (categoryId >= 0) exp &= _.CategoryId == categoryId;
        if (isDeleted != null) exp &= _.IsDeleted == isDeleted;
        exp &= _.CreatedAt.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得记忆字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>记忆ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>内容</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>重要度（0低 1中 2高 3关键）</summary>
        public static readonly Field Importance = FindByName("Importance");

        /// <summary>标签</summary>
        public static readonly Field Tags = FindByName("Tags");

        /// <summary>来源</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>分类ID</summary>
        public static readonly Field CategoryId = FindByName("CategoryId");

        /// <summary>访问次数</summary>
        public static readonly Field AccessCount = FindByName("AccessCount");

        /// <summary>最后访问时间</summary>
        public static readonly Field LastAccessedAt = FindByName("LastAccessedAt");

        /// <summary>衰减分数</summary>
        public static readonly Field DecayScore = FindByName("DecayScore");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        /// <summary>是否删除</summary>
        public static readonly Field IsDeleted = FindByName("IsDeleted");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得记忆字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>记忆ID</summary>
        public const String Id = "Id";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>内容</summary>
        public const String Content = "Content";

        /// <summary>类型（0事实 1偏好 2项目 3个人 4工作流 5技能 99其他）</summary>
        public const String Type = "Type";

        /// <summary>重要度（0低 1中 2高 3关键）</summary>
        public const String Importance = "Importance";

        /// <summary>标签</summary>
        public const String Tags = "Tags";

        /// <summary>来源</summary>
        public const String Source = "Source";

        /// <summary>分类ID</summary>
        public const String CategoryId = "CategoryId";

        /// <summary>访问次数</summary>
        public const String AccessCount = "AccessCount";

        /// <summary>最后访问时间</summary>
        public const String LastAccessedAt = "LastAccessedAt";

        /// <summary>衰减分数</summary>
        public const String DecayScore = "DecayScore";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";

        /// <summary>是否删除</summary>
        public const String IsDeleted = "IsDeleted";
    }
    #endregion
}
