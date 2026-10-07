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
[BindIndex("IX_CostModelPrice_Model", false, "Model")]
[BindIndex("IX_CostModelPrice_Provider", false, "Provider")]
[BindIndex("IX_CostModelPrice_Model_Provider", false, "Model,Provider")]
[BindIndex("IX_CostModelPrice_IsEnabled", false, "IsEnabled")]
[BindTable("CostModelPrice", Description = "模型单价目录", ConnName = "CostScope", DbType = DatabaseType.None)]
public partial class CostModelPrice
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Model;
    /// <summary>模型名称（与 ChatTurn.Model 对齐）</summary>
    [DisplayName("模型名称（与ChatTurn")]
    [Description("模型名称（与 ChatTurn.Model 对齐）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Model", "模型名称（与 ChatTurn.Model 对齐）", "", Master = true)]
    public String Model { get => _Model; set { if (OnPropertyChanging("Model", value)) { _Model = value; OnPropertyChanged("Model"); } } }

    private String _Provider;
    /// <summary>供应商（如 openai / anthropic / 本地）</summary>
    [DisplayName("供应商（如openai_anthropic_本地）")]
    [Description("供应商（如 openai / anthropic / 本地）")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Provider", "供应商（如 openai / anthropic / 本地）", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private Decimal _InputPricePer1M;
    /// <summary>输入单价（每 1M token，币种见 Currency）</summary>
    [DisplayName("输入单价（每1Mtoken")]
    [Description("输入单价（每 1M token，币种见 Currency）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputPricePer1M", "输入单价（每 1M token，币种见 Currency）", "")]
    public Decimal InputPricePer1M { get => _InputPricePer1M; set { if (OnPropertyChanging("InputPricePer1M", value)) { _InputPricePer1M = value; OnPropertyChanged("InputPricePer1M"); } } }

    private Decimal _OutputPricePer1M;
    /// <summary>输出单价（每 1M token，币种见 Currency）</summary>
    [DisplayName("输出单价（每1Mtoken")]
    [Description("输出单价（每 1M token，币种见 Currency）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputPricePer1M", "输出单价（每 1M token，币种见 Currency）", "")]
    public Decimal OutputPricePer1M { get => _OutputPricePer1M; set { if (OnPropertyChanging("OutputPricePer1M", value)) { _OutputPricePer1M = value; OnPropertyChanged("OutputPricePer1M"); } } }

    private String _Currency;
    /// <summary>币种（默认 CNY）</summary>
    [DisplayName("币种（默认CNY）")]
    [Description("币种（默认 CNY）")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Currency", "币种（默认 CNY）", "")]
    public String Currency { get => _Currency; set { if (OnPropertyChanging("Currency", value)) { _Currency = value; OnPropertyChanged("Currency"); } } }

    private DateTime _EffectiveFrom;
    /// <summary>生效起始时间</summary>
    [DisplayName("生效起始时间")]
    [Description("生效起始时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EffectiveFrom", "生效起始时间", "")]
    public DateTime EffectiveFrom { get => _EffectiveFrom; set { if (OnPropertyChanging("EffectiveFrom", value)) { _EffectiveFrom = value; OnPropertyChanged("EffectiveFrom"); } } }

    private DateTime _EffectiveTo;
    /// <summary>生效结束时间（null 表示长期有效）</summary>
    [DisplayName("生效结束时间（null表示长期有效）")]
    [Description("生效结束时间（null 表示长期有效）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("EffectiveTo", "生效结束时间（null 表示长期有效）", "")]
    public DateTime EffectiveTo { get => _EffectiveTo; set { if (OnPropertyChanging("EffectiveTo", value)) { _EffectiveTo = value; OnPropertyChanged("EffectiveTo"); } } }

    private Boolean _IsEnabled;
    /// <summary>是否启用</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsEnabled", "是否启用", "")]
    public Boolean IsEnabled { get => _IsEnabled; set { if (OnPropertyChanging("IsEnabled", value)) { _IsEnabled = value; OnPropertyChanged("IsEnabled"); } } }

    private DateTime _CreatedTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedTime", "创建时间", "")]
    public DateTime CreatedTime { get => _CreatedTime; set { if (OnPropertyChanging("CreatedTime", value)) { _CreatedTime = value; OnPropertyChanged("CreatedTime"); } } }

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
            "Model" => _Model,
            "Provider" => _Provider,
            "InputPricePer1M" => _InputPricePer1M,
            "OutputPricePer1M" => _OutputPricePer1M,
            "Currency" => _Currency,
            "EffectiveFrom" => _EffectiveFrom,
            "EffectiveTo" => _EffectiveTo,
            "IsEnabled" => _IsEnabled,
            "CreatedTime" => _CreatedTime,
            "UpdatedTime" => _UpdatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "InputPricePer1M": _InputPricePer1M = Convert.ToDecimal(value); break;
                case "OutputPricePer1M": _OutputPricePer1M = Convert.ToDecimal(value); break;
                case "Currency": _Currency = Convert.ToString(value); break;
                case "EffectiveFrom": _EffectiveFrom = value.ToDateTime(); break;
                case "EffectiveTo": _EffectiveTo = value.ToDateTime(); break;
                case "IsEnabled": _IsEnabled = value.ToBoolean(); break;
                case "CreatedTime": _CreatedTime = value.ToDateTime(); break;
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
    public static CostModelPrice FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据模型名称（与ChatTurn查找</summary>
    /// <param name="model">模型名称（与ChatTurn</param>
    /// <returns>实体列表</returns>
    public static IList<CostModelPrice> FindAllByModel(String model)
    {
        if (model.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Model.EqualIgnoreCase(model));

        return FindAll(_.Model == model);
    }

    /// <summary>根据供应商（如openai_anthropic_本地）查找</summary>
    /// <param name="provider">供应商（如openai_anthropic_本地）</param>
    /// <returns>实体列表</returns>
    public static IList<CostModelPrice> FindAllByProvider(String provider)
    {
        if (provider.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Provider.EqualIgnoreCase(provider));

        return FindAll(_.Provider == provider);
    }

    /// <summary>根据模型名称（与ChatTurn、供应商（如openai_anthropic_本地）查找</summary>
    /// <param name="model">模型名称（与ChatTurn</param>
    /// <param name="provider">供应商（如openai_anthropic_本地）</param>
    /// <returns>实体列表</returns>
    public static IList<CostModelPrice> FindAllByModelAndProvider(String model, String provider)
    {
        if (model.IsNullOrEmpty()) return [];
        if (provider.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Model.EqualIgnoreCase(model) && e.Provider.EqualIgnoreCase(provider));

        return FindAll(_.Model == model & _.Provider == provider);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="provider">供应商（如 openai / anthropic / 本地）</param>
    /// <param name="isEnabled">是否启用</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<CostModelPrice> Search(String provider, Boolean? isEnabled, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (isEnabled != null) exp &= _.IsEnabled == isEnabled;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得模型单价目录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>模型名称（与 ChatTurn.Model 对齐）</summary>
        public static readonly Field Model = FindByName("Model");

        /// <summary>供应商（如 openai / anthropic / 本地）</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>输入单价（每 1M token，币种见 Currency）</summary>
        public static readonly Field InputPricePer1M = FindByName("InputPricePer1M");

        /// <summary>输出单价（每 1M token，币种见 Currency）</summary>
        public static readonly Field OutputPricePer1M = FindByName("OutputPricePer1M");

        /// <summary>币种（默认 CNY）</summary>
        public static readonly Field Currency = FindByName("Currency");

        /// <summary>生效起始时间</summary>
        public static readonly Field EffectiveFrom = FindByName("EffectiveFrom");

        /// <summary>生效结束时间（null 表示长期有效）</summary>
        public static readonly Field EffectiveTo = FindByName("EffectiveTo");

        /// <summary>是否启用</summary>
        public static readonly Field IsEnabled = FindByName("IsEnabled");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedTime = FindByName("CreatedTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedTime = FindByName("UpdatedTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得模型单价目录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>模型名称（与 ChatTurn.Model 对齐）</summary>
        public const String Model = "Model";

        /// <summary>供应商（如 openai / anthropic / 本地）</summary>
        public const String Provider = "Provider";

        /// <summary>输入单价（每 1M token，币种见 Currency）</summary>
        public const String InputPricePer1M = "InputPricePer1M";

        /// <summary>输出单价（每 1M token，币种见 Currency）</summary>
        public const String OutputPricePer1M = "OutputPricePer1M";

        /// <summary>币种（默认 CNY）</summary>
        public const String Currency = "Currency";

        /// <summary>生效起始时间</summary>
        public const String EffectiveFrom = "EffectiveFrom";

        /// <summary>生效结束时间（null 表示长期有效）</summary>
        public const String EffectiveTo = "EffectiveTo";

        /// <summary>是否启用</summary>
        public const String IsEnabled = "IsEnabled";

        /// <summary>创建时间</summary>
        public const String CreatedTime = "CreatedTime";

        /// <summary>更新时间</summary>
        public const String UpdatedTime = "UpdatedTime";
    }
    #endregion
}
