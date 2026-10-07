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
[BindIndex("IX_CostTurnDaySummary_Day", false, "Day")]
[BindIndex("IX_CostTurnDaySummary_Model", false, "Model")]
[BindIndex("IU_CostTurnDaySummary_Day_Model", true, "Day,Model")]
[BindIndex("IX_CostTurnDaySummary_Day_Provider", false, "Day,Provider")]
[BindTable("CostTurnDaySummary", Description = "成本按日惰性汇总", ConnName = "CostScope", DbType = DatabaseType.None)]
public partial class CostTurnDaySummary
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private DateTime _Day;
    /// <summary>汇总日（按日期截断；FR-3.7 口径为 Day×Model，故以日为寻址主键）</summary>
    [DisplayName("汇总日（按日期截断")]
    [Description("汇总日（按日期截断；FR-3.7 口径为 Day×Model，故以日为寻址主键）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Day", "汇总日（按日期截断；FR-3.7 口径为 Day×Model，故以日为寻址主键）", "", Master = true)]
    public DateTime Day { get => _Day; set { if (OnPropertyChanging("Day", value)) { _Day = value; OnPropertyChanged("Day"); } } }

    private String _Model;
    /// <summary>模型名称</summary>
    [DisplayName("模型名称")]
    [Description("模型名称")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Model", "模型名称", "")]
    public String Model { get => _Model; set { if (OnPropertyChanging("Model", value)) { _Model = value; OnPropertyChanged("Model"); } } }

    private String _Provider;
    /// <summary>供应商（经 A4 四级解析；未归属时留空，不猜）</summary>
    [DisplayName("供应商（经A4四级解析")]
    [Description("供应商（经 A4 四级解析；未归属时留空，不猜）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "供应商（经 A4 四级解析；未归属时留空，不猜）", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private Int64 _PromptTokens;
    /// <summary>提示词 token 累计</summary>
    [DisplayName("提示词token累计")]
    [Description("提示词 token 累计")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PromptTokens", "提示词 token 累计", "")]
    public Int64 PromptTokens { get => _PromptTokens; set { if (OnPropertyChanging("PromptTokens", value)) { _PromptTokens = value; OnPropertyChanged("PromptTokens"); } } }

    private Int64 _CompletionTokens;
    /// <summary>补全 token 累计</summary>
    [DisplayName("补全token累计")]
    [Description("补全 token 累计")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CompletionTokens", "补全 token 累计", "")]
    public Int64 CompletionTokens { get => _CompletionTokens; set { if (OnPropertyChanging("CompletionTokens", value)) { _CompletionTokens = value; OnPropertyChanged("CompletionTokens"); } } }

    private Int64 _TotalTokens;
    /// <summary>总 token 累计</summary>
    [DisplayName("总token累计")]
    [Description("总 token 累计")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总 token 累计", "")]
    public Int64 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private Decimal _CostAmount;
    /// <summary>当日成本金额（币种见 Currency）</summary>
    [DisplayName("当日成本金额（币种见Currency）")]
    [Description("当日成本金额（币种见 Currency）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CostAmount", "当日成本金额（币种见 Currency）", "")]
    public Decimal CostAmount { get => _CostAmount; set { if (OnPropertyChanging("CostAmount", value)) { _CostAmount = value; OnPropertyChanged("CostAmount"); } } }

    private Boolean _CostIsLowerBound;
    /// <summary>true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）</summary>
    [DisplayName("true=含未配单价未知段")]
    [Description("true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CostIsLowerBound", "true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）", "")]
    public Boolean CostIsLowerBound { get => _CostIsLowerBound; set { if (OnPropertyChanging("CostIsLowerBound", value)) { _CostIsLowerBound = value; OnPropertyChanged("CostIsLowerBound"); } } }

    private String _Currency;
    /// <summary>币种（默认 CNY）</summary>
    [DisplayName("币种（默认CNY）")]
    [Description("币种（默认 CNY）")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Currency", "币种（默认 CNY）", "")]
    public String Currency { get => _Currency; set { if (OnPropertyChanging("Currency", value)) { _Currency = value; OnPropertyChanged("Currency"); } } }

    private Int32 _FailCount;
    /// <summary>失败轮次数（BR-4：失败不计成本，但必须留痕）</summary>
    [DisplayName("失败轮次数（BR-4")]
    [Description("失败轮次数（BR-4：失败不计成本，但必须留痕）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FailCount", "失败轮次数（BR-4：失败不计成本，但必须留痕）", "")]
    public Int32 FailCount { get => _FailCount; set { if (OnPropertyChanging("FailCount", value)) { _FailCount = value; OnPropertyChanged("FailCount"); } } }

    private Int32 _TurnCount;
    /// <summary>轮次计数</summary>
    [DisplayName("轮次计数")]
    [Description("轮次计数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TurnCount", "轮次计数", "")]
    public Int32 TurnCount { get => _TurnCount; set { if (OnPropertyChanging("TurnCount", value)) { _TurnCount = value; OnPropertyChanged("TurnCount"); } } }

    private String _SessionKey;
    /// <summary>来源标注：跨会话汇总写 *；按会话查询时写具体会话键</summary>
    [DisplayName("来源标注")]
    [Description("来源标注：跨会话汇总写 *；按会话查询时写具体会话键")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("SessionKey", "来源标注：跨会话汇总写 *；按会话查询时写具体会话键", "")]
    public String SessionKey { get => _SessionKey; set { if (OnPropertyChanging("SessionKey", value)) { _SessionKey = value; OnPropertyChanged("SessionKey"); } } }

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
            "Day" => _Day,
            "Model" => _Model,
            "Provider" => _Provider,
            "PromptTokens" => _PromptTokens,
            "CompletionTokens" => _CompletionTokens,
            "TotalTokens" => _TotalTokens,
            "CostAmount" => _CostAmount,
            "CostIsLowerBound" => _CostIsLowerBound,
            "Currency" => _Currency,
            "FailCount" => _FailCount,
            "TurnCount" => _TurnCount,
            "SessionKey" => _SessionKey,
            "CreatedTime" => _CreatedTime,
            "UpdatedTime" => _UpdatedTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Day": _Day = value.ToDateTime(); break;
                case "Model": _Model = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "PromptTokens": _PromptTokens = value.ToLong(); break;
                case "CompletionTokens": _CompletionTokens = value.ToLong(); break;
                case "TotalTokens": _TotalTokens = value.ToLong(); break;
                case "CostAmount": _CostAmount = Convert.ToDecimal(value); break;
                case "CostIsLowerBound": _CostIsLowerBound = value.ToBoolean(); break;
                case "Currency": _Currency = Convert.ToString(value); break;
                case "FailCount": _FailCount = value.ToInt(); break;
                case "TurnCount": _TurnCount = value.ToInt(); break;
                case "SessionKey": _SessionKey = Convert.ToString(value); break;
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
    public static CostTurnDaySummary FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据模型名称查找</summary>
    /// <param name="model">模型名称</param>
    /// <returns>实体列表</returns>
    public static IList<CostTurnDaySummary> FindAllByModel(String model)
    {
        if (model.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Model.EqualIgnoreCase(model));

        return FindAll(_.Model == model);
    }

    /// <summary>根据汇总日（按日期截断查找</summary>
    /// <param name="day">汇总日（按日期截断</param>
    /// <returns>实体列表</returns>
    public static IList<CostTurnDaySummary> FindAllByDay(DateTime day)
    {
        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Day == day);
        return FindAll(_.Day == day);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="model">模型名称</param>
    /// <param name="provider">供应商（经 A4 四级解析；未归属时留空，不猜）</param>
    /// <param name="costIsLowerBound">true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<CostTurnDaySummary> Search(String model, String provider, Boolean? costIsLowerBound, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!model.IsNullOrEmpty()) exp &= _.Model == model;
        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (costIsLowerBound != null) exp &= _.CostIsLowerBound == costIsLowerBound;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得成本按日惰性汇总字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>汇总日（按日期截断；FR-3.7 口径为 Day×Model，故以日为寻址主键）</summary>
        public static readonly Field Day = FindByName("Day");

        /// <summary>模型名称</summary>
        public static readonly Field Model = FindByName("Model");

        /// <summary>供应商（经 A4 四级解析；未归属时留空，不猜）</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>提示词 token 累计</summary>
        public static readonly Field PromptTokens = FindByName("PromptTokens");

        /// <summary>补全 token 累计</summary>
        public static readonly Field CompletionTokens = FindByName("CompletionTokens");

        /// <summary>总 token 累计</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>当日成本金额（币种见 Currency）</summary>
        public static readonly Field CostAmount = FindByName("CostAmount");

        /// <summary>true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）</summary>
        public static readonly Field CostIsLowerBound = FindByName("CostIsLowerBound");

        /// <summary>币种（默认 CNY）</summary>
        public static readonly Field Currency = FindByName("Currency");

        /// <summary>失败轮次数（BR-4：失败不计成本，但必须留痕）</summary>
        public static readonly Field FailCount = FindByName("FailCount");

        /// <summary>轮次计数</summary>
        public static readonly Field TurnCount = FindByName("TurnCount");

        /// <summary>来源标注：跨会话汇总写 *；按会话查询时写具体会话键</summary>
        public static readonly Field SessionKey = FindByName("SessionKey");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedTime = FindByName("CreatedTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedTime = FindByName("UpdatedTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得成本按日惰性汇总字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>汇总日（按日期截断；FR-3.7 口径为 Day×Model，故以日为寻址主键）</summary>
        public const String Day = "Day";

        /// <summary>模型名称</summary>
        public const String Model = "Model";

        /// <summary>供应商（经 A4 四级解析；未归属时留空，不猜）</summary>
        public const String Provider = "Provider";

        /// <summary>提示词 token 累计</summary>
        public const String PromptTokens = "PromptTokens";

        /// <summary>补全 token 累计</summary>
        public const String CompletionTokens = "CompletionTokens";

        /// <summary>总 token 累计</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>当日成本金额（币种见 Currency）</summary>
        public const String CostAmount = "CostAmount";

        /// <summary>true=含未配单价未知段，本值仅为下界（BR-2，绝不静默计 0）</summary>
        public const String CostIsLowerBound = "CostIsLowerBound";

        /// <summary>币种（默认 CNY）</summary>
        public const String Currency = "Currency";

        /// <summary>失败轮次数（BR-4：失败不计成本，但必须留痕）</summary>
        public const String FailCount = "FailCount";

        /// <summary>轮次计数</summary>
        public const String TurnCount = "TurnCount";

        /// <summary>来源标注：跨会话汇总写 *；按会话查询时写具体会话键</summary>
        public const String SessionKey = "SessionKey";

        /// <summary>创建时间</summary>
        public const String CreatedTime = "CreatedTime";

        /// <summary>更新时间</summary>
        public const String UpdatedTime = "UpdatedTime";
    }
    #endregion
}
