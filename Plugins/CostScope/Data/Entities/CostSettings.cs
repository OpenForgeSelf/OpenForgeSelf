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

namespace ForgeSelf.Api.Plugins.CostScope.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IU_CostSettings_Key", true, "Key")]
[BindTable("CostSettings", Description = "成本插件设置", ConnName = "CostScope", DbType = DatabaseType.None)]
public partial class CostSettings
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Key;
    /// <summary>设置键（唯一）</summary>
    [DisplayName("设置键（唯一）")]
    [Description("设置键（唯一）")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("Key", "设置键（唯一）", "", Master = true)]
    public String Key { get => _Key; set { if (OnPropertyChanging("Key", value)) { _Key = value; OnPropertyChanged("Key"); } } }

    private String _Value;
    /// <summary>设置值（JSON 或纯文本）</summary>
    [DisplayName("设置值（JSON或纯文本）")]
    [Description("设置值（JSON 或纯文本）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Value", "设置值（JSON 或纯文本）", "")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private String _Description;
    /// <summary>说明</summary>
    [DisplayName("说明")]
    [Description("说明")]
    [DataObjectField(false, false, true, 256)]
    [BindColumn("Description", "说明", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private DateTime _UpdatedTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedTime", "更新时间", "")]
    public DateTime UpdatedTime { get => _UpdatedTime; set { if (OnPropertyChanging("UpdatedTime", value)) { _UpdatedTime = value; OnPropertyChanged("UpdatedTime"); } } }
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
            "Key" => _Key,
            "Value" => _Value,
            "Description" => _Description,
            "UpdatedTime" => _UpdatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Key": _Key = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "UpdatedTime": _UpdatedTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static CostSettings FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据设置键（唯一）查找</summary>
    /// <param name="key">设置键（唯一）</param>
    /// <returns>实体对象</returns>
    public static CostSettings FindByKey(String key)
    {
        if (key.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Key.EqualIgnoreCase(key));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(key) as CostSettings;

        //return Find(_.Key == key);
    }
    #endregion

    #region 字段名
    /// <summary>取得成本插件设置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设置键（唯一）</summary>
        public static readonly Field Key = FindByName("Key");

        /// <summary>设置值（JSON 或纯文本）</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>说明</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedTime = FindByName("UpdatedTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得成本插件设置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>设置键（唯一）</summary>
        public const String Key = "Key";

        /// <summary>设置值（JSON 或纯文本）</summary>
        public const String Value = "Value";

        /// <summary>说明</summary>
        public const String Description = "Description";

        /// <summary>更新时间</summary>
        public const String UpdatedTime = "UpdatedTime";
    }
    #endregion
}
