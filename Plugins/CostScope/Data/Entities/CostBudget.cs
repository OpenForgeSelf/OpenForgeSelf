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
[BindIndex("IX_CostBudget_Scope", false, "Scope")]
[BindIndex("IX_CostBudget_Scope_Target", false, "Scope,Target")]
[BindTable("CostBudget", Description = "成本预算规则", ConnName = "CostScope", DbType = DatabaseType.None)]
public partial class CostBudget
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>规则名称</summary>
    [DisplayName("规则名称")]
    [Description("规则名称")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "规则名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Scope;
    /// <summary>作用域（global/model/session）</summary>
    [DisplayName("作用域（global_model_session）")]
    [Description("作用域（global/model/session）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Scope", "作用域（global/model/session）", "")]
    public String Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private String _Target;
    /// <summary>作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）</summary>
    [DisplayName("作用目标（Scope=model时为模型名")]
    [Description("作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Target", "作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）", "")]
    public String Target { get => _Target; set { if (OnPropertyChanging("Target", value)) { _Target = value; OnPropertyChanged("Target"); } } }

    private Decimal _LimitAmount;
    /// <summary>额度上限（币种见 Currency）</summary>
    [DisplayName("额度上限（币种见Currency）")]
    [Description("额度上限（币种见 Currency）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LimitAmount", "额度上限（币种见 Currency）", "")]
    public Decimal LimitAmount { get => _LimitAmount; set { if (OnPropertyChanging("LimitAmount", value)) { _LimitAmount = value; OnPropertyChanged("LimitAmount"); } } }

    private String _Currency;
    /// <summary>币种（默认 CNY）</summary>
    [DisplayName("币种（默认CNY）")]
    [Description("币种（默认 CNY）")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Currency", "币种（默认 CNY）", "")]
    public String Currency { get => _Currency; set { if (OnPropertyChanging("Currency", value)) { _Currency = value; OnPropertyChanged("Currency"); } } }

    private String _Period;
    /// <summary>周期（daily/monthly）</summary>
    [DisplayName("周期（daily_monthly）")]
    [Description("周期（daily/monthly）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Period", "周期（daily/monthly）", "")]
    public String Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

    private Double _AlertThreshold;
    /// <summary>告警阈值（0-1，达此比例触发横幅）</summary>
    [DisplayName("告警阈值（0-1")]
    [Description("告警阈值（0-1，达此比例触发横幅）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlertThreshold", "告警阈值（0-1，达此比例触发横幅）", "")]
    public Double AlertThreshold { get => _AlertThreshold; set { if (OnPropertyChanging("AlertThreshold", value)) { _AlertThreshold = value; OnPropertyChanged("AlertThreshold"); } } }

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
            "Name" => _Name,
            "Scope" => _Scope,
            "Target" => _Target,
            "LimitAmount" => _LimitAmount,
            "Currency" => _Currency,
            "Period" => _Period,
            "AlertThreshold" => _AlertThreshold,
            "CreatedTime" => _CreatedTime,
            "UpdatedTime" => _UpdatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Target": _Target = Convert.ToString(value); break;
                case "LimitAmount": _LimitAmount = Convert.ToDecimal(value); break;
                case "Currency": _Currency = Convert.ToString(value); break;
                case "Period": _Period = Convert.ToString(value); break;
                case "AlertThreshold": _AlertThreshold = value.ToDouble(); break;
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
    public static CostBudget FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据作用域（global_model_session）查找</summary>
    /// <param name="scope">作用域（global_model_session）</param>
    /// <returns>实体列表</returns>
    public static IList<CostBudget> FindAllByScope(String scope)
    {
        if (scope.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Scope.EqualIgnoreCase(scope));

        return FindAll(_.Scope == scope);
    }

    /// <summary>根据作用域（global_model_session）、作用目标（Scope=model时为模型名查找</summary>
    /// <param name="scope">作用域（global_model_session）</param>
    /// <param name="target">作用目标（Scope=model时为模型名</param>
    /// <returns>实体列表</returns>
    public static IList<CostBudget> FindAllByScopeAndTarget(String scope, String target)
    {
        if (scope.IsNullOrEmpty()) return [];
        if (target.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Scope.EqualIgnoreCase(scope) && e.Target.EqualIgnoreCase(target));

        return FindAll(_.Scope == scope & _.Target == target);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="scope">作用域（global/model/session）</param>
    /// <param name="target">作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<CostBudget> Search(String scope, String target, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!scope.IsNullOrEmpty()) exp &= _.Scope == scope;
        if (!target.IsNullOrEmpty()) exp &= _.Target == target;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得成本预算规则字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>规则名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>作用域（global/model/session）</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）</summary>
        public static readonly Field Target = FindByName("Target");

        /// <summary>额度上限（币种见 Currency）</summary>
        public static readonly Field LimitAmount = FindByName("LimitAmount");

        /// <summary>币种（默认 CNY）</summary>
        public static readonly Field Currency = FindByName("Currency");

        /// <summary>周期（daily/monthly）</summary>
        public static readonly Field Period = FindByName("Period");

        /// <summary>告警阈值（0-1，达此比例触发横幅）</summary>
        public static readonly Field AlertThreshold = FindByName("AlertThreshold");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedTime = FindByName("CreatedTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedTime = FindByName("UpdatedTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得成本预算规则字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>规则名称</summary>
        public const String Name = "Name";

        /// <summary>作用域（global/model/session）</summary>
        public const String Scope = "Scope";

        /// <summary>作用目标（Scope=model 时为模型名；session 时为会话键；global 为 null）</summary>
        public const String Target = "Target";

        /// <summary>额度上限（币种见 Currency）</summary>
        public const String LimitAmount = "LimitAmount";

        /// <summary>币种（默认 CNY）</summary>
        public const String Currency = "Currency";

        /// <summary>周期（daily/monthly）</summary>
        public const String Period = "Period";

        /// <summary>告警阈值（0-1，达此比例触发横幅）</summary>
        public const String AlertThreshold = "AlertThreshold";

        /// <summary>创建时间</summary>
        public const String CreatedTime = "CreatedTime";

        /// <summary>更新时间</summary>
        public const String UpdatedTime = "UpdatedTime";
    }
    #endregion
}
