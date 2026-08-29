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

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_UsageDailySummary_Date", false, "Date")]
[BindIndex("IX_UsageDailySummary_PluginId", false, "PluginId")]
[BindIndex("IX_UsageDailySummary_ToolId", false, "ToolId")]
[BindIndex("IX_UsageDailySummary_Date_PluginId_ToolId", false, "Date,PluginId,ToolId")]
[BindIndex("IX_UsageDailySummary_PluginId_ToolId_Date", false, "PluginId,ToolId,Date")]
[BindTable("UsageDailySummary", Description = "每日使用汇总", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class UsageDailySummary : IUsageDailySummaryModel, IEntity<IUsageDailySummaryModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>汇总记录ID</summary>
    [DisplayName("汇总记录ID")]
    [Description("汇总记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "汇总记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private DateTime _Date;
    /// <summary>日期</summary>
    [DisplayName("日期")]
    [Description("日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Date", "日期", "")]
    public DateTime Date { get => _Date; set { if (OnPropertyChanging("Date", value)) { _Date = value; OnPropertyChanged("Date"); } } }

    private String _PluginId;
    /// <summary>插件ID</summary>
    [DisplayName("插件ID")]
    [Description("插件ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("PluginId", "插件ID", "")]
    public String PluginId { get => _PluginId; set { if (OnPropertyChanging("PluginId", value)) { _PluginId = value; OnPropertyChanged("PluginId"); } } }

    private String _ToolId;
    /// <summary>工具ID</summary>
    [DisplayName("工具ID")]
    [Description("工具ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("ToolId", "工具ID", "")]
    public String ToolId { get => _ToolId; set { if (OnPropertyChanging("ToolId", value)) { _ToolId = value; OnPropertyChanged("ToolId"); } } }

    private Int32 _UseCount;
    /// <summary>使用次数</summary>
    [DisplayName("使用次数")]
    [Description("使用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UseCount", "使用次数", "")]
    public Int32 UseCount { get => _UseCount; set { if (OnPropertyChanging("UseCount", value)) { _UseCount = value; OnPropertyChanged("UseCount"); } } }

    private Int64 _TotalDurationMs;
    /// <summary>总持续时间（毫秒）</summary>
    [DisplayName("总持续时间（毫秒）")]
    [Description("总持续时间（毫秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalDurationMs", "总持续时间（毫秒）", "")]
    public Int64 TotalDurationMs { get => _TotalDurationMs; set { if (OnPropertyChanging("TotalDurationMs", value)) { _TotalDurationMs = value; OnPropertyChanged("TotalDurationMs"); } } }

    private Int32 _UniqueUsers;
    /// <summary>独立用户数</summary>
    [DisplayName("独立用户数")]
    [Description("独立用户数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UniqueUsers", "独立用户数", "")]
    public Int32 UniqueUsers { get => _UniqueUsers; set { if (OnPropertyChanging("UniqueUsers", value)) { _UniqueUsers = value; OnPropertyChanged("UniqueUsers"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUsageDailySummaryModel model)
    {
        Id = model.Id;
        Date = model.Date;
        PluginId = model.PluginId;
        ToolId = model.ToolId;
        UseCount = model.UseCount;
        TotalDurationMs = model.TotalDurationMs;
        UniqueUsers = model.UniqueUsers;
    }
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
            "Date" => _Date,
            "PluginId" => _PluginId,
            "ToolId" => _ToolId,
            "UseCount" => _UseCount,
            "TotalDurationMs" => _TotalDurationMs,
            "UniqueUsers" => _UniqueUsers,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Date": _Date = value.ToDateTime(); break;
                case "PluginId": _PluginId = Convert.ToString(value); break;
                case "ToolId": _ToolId = Convert.ToString(value); break;
                case "UseCount": _UseCount = value.ToInt(); break;
                case "TotalDurationMs": _TotalDurationMs = value.ToLong(); break;
                case "UniqueUsers": _UniqueUsers = value.ToInt(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据汇总记录ID查找</summary>
    /// <param name="id">汇总记录ID</param>
    /// <returns>实体对象</returns>
    public static UsageDailySummary FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据插件ID查找</summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>实体列表</returns>
    public static IList<UsageDailySummary> FindAllByPluginId(String pluginId)
    {
        if (pluginId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.PluginId.EqualIgnoreCase(pluginId));

        return FindAll(_.PluginId == pluginId);
    }

    /// <summary>根据工具ID查找</summary>
    /// <param name="toolId">工具ID</param>
    /// <returns>实体列表</returns>
    public static IList<UsageDailySummary> FindAllByToolId(String toolId)
    {
        if (toolId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ToolId.EqualIgnoreCase(toolId));

        return FindAll(_.ToolId == toolId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="toolId">工具ID</param>
    /// <param name="start">日期开始</param>
    /// <param name="end">日期结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UsageDailySummary> Search(String pluginId, String toolId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!pluginId.IsNullOrEmpty()) exp &= _.PluginId == pluginId;
        if (!toolId.IsNullOrEmpty()) exp &= _.ToolId == toolId;
        exp &= _.Date.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得每日使用汇总字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>汇总记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>日期</summary>
        public static readonly Field Date = FindByName("Date");

        /// <summary>插件ID</summary>
        public static readonly Field PluginId = FindByName("PluginId");

        /// <summary>工具ID</summary>
        public static readonly Field ToolId = FindByName("ToolId");

        /// <summary>使用次数</summary>
        public static readonly Field UseCount = FindByName("UseCount");

        /// <summary>总持续时间（毫秒）</summary>
        public static readonly Field TotalDurationMs = FindByName("TotalDurationMs");

        /// <summary>独立用户数</summary>
        public static readonly Field UniqueUsers = FindByName("UniqueUsers");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得每日使用汇总字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>汇总记录ID</summary>
        public const String Id = "Id";

        /// <summary>日期</summary>
        public const String Date = "Date";

        /// <summary>插件ID</summary>
        public const String PluginId = "PluginId";

        /// <summary>工具ID</summary>
        public const String ToolId = "ToolId";

        /// <summary>使用次数</summary>
        public const String UseCount = "UseCount";

        /// <summary>总持续时间（毫秒）</summary>
        public const String TotalDurationMs = "TotalDurationMs";

        /// <summary>独立用户数</summary>
        public const String UniqueUsers = "UniqueUsers";
    }
    #endregion
}
