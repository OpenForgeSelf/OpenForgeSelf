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

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_UsageRecord_PluginId", false, "PluginId")]
[BindIndex("IX_UsageRecord_ToolId", false, "ToolId")]
[BindIndex("IX_UsageRecord_Timestamp", false, "Timestamp")]
[BindIndex("IX_UsageRecord_ActionType", false, "ActionType")]
[BindIndex("IX_UsageRecord_PluginId_ToolId_Timestamp", false, "PluginId,ToolId,Timestamp")]
[BindTable("UsageRecord", Description = "使用记录", ConnName = "OpenForgeSelf", DbType = DatabaseType.None)]
public partial class UsageRecord : IUsageRecord, IEntity<IUsageRecord>
{
    #region 属性
    private Int64 _Id;
    /// <summary>记录ID</summary>
    [DisplayName("记录ID")]
    [Description("记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

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

    private String _ActionType;
    /// <summary>操作类型</summary>
    [DisplayName("操作类型")]
    [Description("操作类型")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("ActionType", "操作类型", "")]
    public String ActionType { get => _ActionType; set { if (OnPropertyChanging("ActionType", value)) { _ActionType = value; OnPropertyChanged("ActionType"); } } }

    private String _UserAgent;
    /// <summary>用户代理</summary>
    [DisplayName("用户代理")]
    [Description("用户代理")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("UserAgent", "用户代理", "")]
    public String UserAgent { get => _UserAgent; set { if (OnPropertyChanging("UserAgent", value)) { _UserAgent = value; OnPropertyChanged("UserAgent"); } } }

    private String _IpAddress;
    /// <summary>IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IpAddress", "IP地址", "")]
    public String IpAddress { get => _IpAddress; set { if (OnPropertyChanging("IpAddress", value)) { _IpAddress = value; OnPropertyChanged("IpAddress"); } } }

    private Int64 _DurationMs;
    /// <summary>持续时间（毫秒）</summary>
    [DisplayName("持续时间（毫秒）")]
    [Description("持续时间（毫秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "持续时间（毫秒）", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private DateTime _Timestamp;
    /// <summary>时间戳</summary>
    [DisplayName("时间戳")]
    [Description("时间戳")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Timestamp", "时间戳", "")]
    public DateTime Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }

    private String _MetadataJson;
    /// <summary>元数据JSON</summary>
    [DisplayName("元数据JSON")]
    [Description("元数据JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("MetadataJson", "元数据JSON", "")]
    public String MetadataJson { get => _MetadataJson; set { if (OnPropertyChanging("MetadataJson", value)) { _MetadataJson = value; OnPropertyChanged("MetadataJson"); } } }

    private Int64 _WorkflowExecutionId;
    /// <summary>工作流执行ID</summary>
    [DisplayName("工作流执行ID")]
    [Description("工作流执行ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("WorkflowExecutionId", "工作流执行ID", "")]
    public Int64 WorkflowExecutionId { get => _WorkflowExecutionId; set { if (OnPropertyChanging("WorkflowExecutionId", value)) { _WorkflowExecutionId = value; OnPropertyChanged("WorkflowExecutionId"); } } }

    private String _StepId;
    /// <summary>工作流步骤ID</summary>
    [DisplayName("工作流步骤ID")]
    [Description("工作流步骤ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("StepId", "工作流步骤ID", "")]
    public String StepId { get => _StepId; set { if (OnPropertyChanging("StepId", value)) { _StepId = value; OnPropertyChanged("StepId"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUsageRecord model)
    {
        Id = model.Id;
        PluginId = model.PluginId;
        ToolId = model.ToolId;
        ActionType = model.ActionType;
        UserAgent = model.UserAgent;
        IpAddress = model.IpAddress;
        DurationMs = model.DurationMs;
        Timestamp = model.Timestamp;
        MetadataJson = model.MetadataJson;
        WorkflowExecutionId = model.WorkflowExecutionId;
        StepId = model.StepId;
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
            "PluginId" => _PluginId,
            "ToolId" => _ToolId,
            "ActionType" => _ActionType,
            "UserAgent" => _UserAgent,
            "IpAddress" => _IpAddress,
            "DurationMs" => _DurationMs,
            "Timestamp" => _Timestamp,
            "MetadataJson" => _MetadataJson,
            "WorkflowExecutionId" => _WorkflowExecutionId,
            "StepId" => _StepId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "PluginId": _PluginId = Convert.ToString(value); break;
                case "ToolId": _ToolId = Convert.ToString(value); break;
                case "ActionType": _ActionType = Convert.ToString(value); break;
                case "UserAgent": _UserAgent = Convert.ToString(value); break;
                case "IpAddress": _IpAddress = Convert.ToString(value); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "Timestamp": _Timestamp = value.ToDateTime(); break;
                case "MetadataJson": _MetadataJson = Convert.ToString(value); break;
                case "WorkflowExecutionId": _WorkflowExecutionId = value.ToLong(); break;
                case "StepId": _StepId = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据记录ID查找</summary>
    /// <param name="id">记录ID</param>
    /// <returns>实体对象</returns>
    public static UsageRecord FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据插件ID查找</summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByPluginId(String pluginId)
    {
        if (pluginId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.PluginId.EqualIgnoreCase(pluginId));

        return FindAll(_.PluginId == pluginId);
    }

    /// <summary>根据工具ID查找</summary>
    /// <param name="toolId">工具ID</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByToolId(String toolId)
    {
        if (toolId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ToolId.EqualIgnoreCase(toolId));

        return FindAll(_.ToolId == toolId);
    }

    /// <summary>根据操作类型查找</summary>
    /// <param name="actionType">操作类型</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByActionType(String actionType)
    {
        if (actionType.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ActionType.EqualIgnoreCase(actionType));

        return FindAll(_.ActionType == actionType);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="toolId">工具ID</param>
    /// <param name="actionType">操作类型</param>
    /// <param name="start">时间戳开始</param>
    /// <param name="end">时间戳结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> Search(String pluginId, String toolId, String actionType, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!pluginId.IsNullOrEmpty()) exp &= _.PluginId == pluginId;
        if (!toolId.IsNullOrEmpty()) exp &= _.ToolId == toolId;
        if (!actionType.IsNullOrEmpty()) exp &= _.ActionType == actionType;
        exp &= _.Timestamp.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得使用记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>插件ID</summary>
        public static readonly Field PluginId = FindByName("PluginId");

        /// <summary>工具ID</summary>
        public static readonly Field ToolId = FindByName("ToolId");

        /// <summary>操作类型</summary>
        public static readonly Field ActionType = FindByName("ActionType");

        /// <summary>用户代理</summary>
        public static readonly Field UserAgent = FindByName("UserAgent");

        /// <summary>IP地址</summary>
        public static readonly Field IpAddress = FindByName("IpAddress");

        /// <summary>持续时间（毫秒）</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>时间戳</summary>
        public static readonly Field Timestamp = FindByName("Timestamp");

        /// <summary>元数据JSON</summary>
        public static readonly Field MetadataJson = FindByName("MetadataJson");

        /// <summary>工作流执行ID</summary>
        public static readonly Field WorkflowExecutionId = FindByName("WorkflowExecutionId");

        /// <summary>工作流步骤ID</summary>
        public static readonly Field StepId = FindByName("StepId");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得使用记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>记录ID</summary>
        public const String Id = "Id";

        /// <summary>插件ID</summary>
        public const String PluginId = "PluginId";

        /// <summary>工具ID</summary>
        public const String ToolId = "ToolId";

        /// <summary>操作类型</summary>
        public const String ActionType = "ActionType";

        /// <summary>用户代理</summary>
        public const String UserAgent = "UserAgent";

        /// <summary>IP地址</summary>
        public const String IpAddress = "IpAddress";

        /// <summary>持续时间（毫秒）</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>时间戳</summary>
        public const String Timestamp = "Timestamp";

        /// <summary>元数据JSON</summary>
        public const String MetadataJson = "MetadataJson";

        /// <summary>工作流执行ID</summary>
        public const String WorkflowExecutionId = "WorkflowExecutionId";

        /// <summary>工作流步骤ID</summary>
        public const String StepId = "StepId";
    }
    #endregion
}
