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
[BindTable("QuickLinkCategory", Description = "快捷链接分类", ConnName = "QuickLinks", DbType = DatabaseType.None)]
public partial class QuickLinkCategory
{
    #region 属性
    private Int64 _Id;
    /// <summary>分类ID</summary>
    [DisplayName("分类ID")]
    [Description("分类ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "分类ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Icon;
    /// <summary>图标</summary>
    [DisplayName("图标")]
    [Description("图标")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Icon", "图标", "")]
    public String Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private Int32 _SortOrder;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SortOrder", "排序", "")]
    public Int32 SortOrder { get => _SortOrder; set { if (OnPropertyChanging("SortOrder", value)) { _SortOrder = value; OnPropertyChanged("SortOrder"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }
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
            "Icon" => _Icon,
            "SortOrder" => _SortOrder,
            "CreatedAt" => _CreatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "SortOrder": _SortOrder = value.ToInt(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据分类ID查找</summary>
    /// <param name="id">分类ID</param>
    /// <returns>实体对象</returns>
    public static QuickLinkCategory FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 字段名
    /// <summary>取得快捷链接分类字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>分类ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>图标</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得快捷链接分类字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>分类ID</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>图标</summary>
        public const String Icon = "Icon";

        /// <summary>排序</summary>
        public const String SortOrder = "SortOrder";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
    }
    #endregion
}
