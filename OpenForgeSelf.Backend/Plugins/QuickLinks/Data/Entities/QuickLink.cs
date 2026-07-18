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

namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_QuickLink_CategoryId", false, "CategoryId")]
[BindIndex("IX_QuickLink_SortOrder", false, "SortOrder")]
[BindTable("QuickLink", Description = "快捷链接", ConnName = "QuickLinks", DbType = DatabaseType.None)]
public partial class QuickLink
{
    #region 属性
    private Int64 _Id;
    /// <summary>链接ID</summary>
    [DisplayName("链接ID")]
    [Description("链接ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "链接ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Url;
    /// <summary>地址</summary>
    [DisplayName("地址")]
    [Description("地址")]
    [DataObjectField(false, false, false, 2000)]
    [BindColumn("Url", "地址", "")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private String _Icon;
    /// <summary>图标</summary>
    [DisplayName("图标")]
    [Description("图标")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Icon", "图标", "")]
    public String Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private Int64 _CategoryId;
    /// <summary>分类ID</summary>
    [DisplayName("分类ID")]
    [Description("分类ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CategoryId", "分类ID", "")]
    public Int64 CategoryId { get => _CategoryId; set { if (OnPropertyChanging("CategoryId", value)) { _CategoryId = value; OnPropertyChanged("CategoryId"); } } }

    private Int32 _SortOrder;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SortOrder", "排序", "")]
    public Int32 SortOrder { get => _SortOrder; set { if (OnPropertyChanging("SortOrder", value)) { _SortOrder = value; OnPropertyChanged("SortOrder"); } } }

    private Int32 _ClickCount;
    /// <summary>点击次数</summary>
    [DisplayName("点击次数")]
    [Description("点击次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ClickCount", "点击次数", "")]
    public Int32 ClickCount { get => _ClickCount; set { if (OnPropertyChanging("ClickCount", value)) { _ClickCount = value; OnPropertyChanged("ClickCount"); } } }

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
            "Name" => _Name,
            "Url" => _Url,
            "Icon" => _Icon,
            "Description" => _Description,
            "CategoryId" => _CategoryId,
            "SortOrder" => _SortOrder,
            "ClickCount" => _ClickCount,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Url": _Url = Convert.ToString(value); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "CategoryId": _CategoryId = value.ToLong(); break;
                case "SortOrder": _SortOrder = value.ToInt(); break;
                case "ClickCount": _ClickCount = value.ToInt(); break;
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
    /// <summary>根据链接ID查找</summary>
    /// <param name="id">链接ID</param>
    /// <returns>实体对象</returns>
    public static QuickLink FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据分类ID查找</summary>
    /// <param name="categoryId">分类ID</param>
    /// <returns>实体列表</returns>
    public static IList<QuickLink> FindAllByCategoryId(Int64 categoryId)
    {
        if (categoryId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CategoryId == categoryId);

        return FindAll(_.CategoryId == categoryId);
    }

    /// <summary>根据排序查找</summary>
    /// <param name="sortOrder">排序</param>
    /// <returns>实体列表</returns>
    public static IList<QuickLink> FindAllBySortOrder(Int32 sortOrder)
    {
        if (sortOrder < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.SortOrder == sortOrder);

        return FindAll(_.SortOrder == sortOrder);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="categoryId">分类ID</param>
    /// <param name="sortOrder">排序</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<QuickLink> Search(Int64 categoryId, Int32 sortOrder, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (categoryId >= 0) exp &= _.CategoryId == categoryId;
        if (sortOrder >= 0) exp &= _.SortOrder == sortOrder;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得快捷链接字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>链接ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>地址</summary>
        public static readonly Field Url = FindByName("Url");

        /// <summary>图标</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>分类ID</summary>
        public static readonly Field CategoryId = FindByName("CategoryId");

        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");

        /// <summary>点击次数</summary>
        public static readonly Field ClickCount = FindByName("ClickCount");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得快捷链接字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>链接ID</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>地址</summary>
        public const String Url = "Url";

        /// <summary>图标</summary>
        public const String Icon = "Icon";

        /// <summary>描述</summary>
        public const String Description = "Description";

        /// <summary>分类ID</summary>
        public const String CategoryId = "CategoryId";

        /// <summary>排序</summary>
        public const String SortOrder = "SortOrder";

        /// <summary>点击次数</summary>
        public const String ClickCount = "ClickCount";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
