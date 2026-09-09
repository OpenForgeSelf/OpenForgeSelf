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

namespace ForgeSelf.Api.Plugins.AIAgent.Entities;

/// <summary>AI Agent 定义（可编辑提示词）。</summary>
[Serializable]
[DataObject]
[Description("AI Agent 定义（可编辑提示词）")]
[BindIndex("IX_AgentDefinition_SortOrder", false, "SortOrder")]
[BindTable("AgentDefinition", Description = "AI Agent 定义（可编辑提示词）", ConnName = "AIAgent", DbType = DatabaseType.None)]
public partial class AgentDefinition
{
    #region 属性
    private String _Id;
    /// <summary>Agent ID（如 agent.generalist）</summary>
    [DisplayName("Agent ID")]
    [Description("Agent ID（如 agent.generalist）")]
    [DataObjectField(true, false, false, 64)]
    [BindColumn("Id", "Agent ID（如 agent.generalist）", "")]
    public String Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 512)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private Int32 _Type;
    /// <summary>Agent 类型（AgentType 枚举）</summary>
    [DisplayName("Agent 类型")]
    [Description("Agent 类型（AgentType 枚举）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Type", "Agent 类型（AgentType 枚举）", "")]
    public Int32 Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _Avatar;
    /// <summary>头像 emoji</summary>
    [DisplayName("头像")]
    [Description("头像 emoji")]
    [DataObjectField(false, false, true, 16)]
    [BindColumn("Avatar", "头像 emoji", "")]
    public String Avatar { get => _Avatar; set { if (OnPropertyChanging("Avatar", value)) { _Avatar = value; OnPropertyChanged("Avatar"); } } }

    private String _SystemPrompt;
    /// <summary>系统提示词</summary>
    [DisplayName("系统提示词")]
    [Description("系统提示词")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("SystemPrompt", "系统提示词", "")]
    public String SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

    private Int32 _MaxIterations;
    /// <summary>最大迭代次数</summary>
    [DisplayName("最大迭代次数")]
    [Description("最大迭代次数")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("MaxIterations", "最大迭代次数", "")]
    public Int32 MaxIterations { get => _MaxIterations; set { if (OnPropertyChanging("MaxIterations", value)) { _MaxIterations = value; OnPropertyChanged("MaxIterations"); } } }

    private Boolean _IsEnabled;
    /// <summary>是否启用</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsEnabled", "是否启用", "")]
    public Boolean IsEnabled { get => _IsEnabled; set { if (OnPropertyChanging("IsEnabled", value)) { _IsEnabled = value; OnPropertyChanged("IsEnabled"); } } }

    private Int32 _SortOrder;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("SortOrder", "排序", "")]
    public Int32 SortOrder { get => _SortOrder; set { if (OnPropertyChanging("SortOrder", value)) { _SortOrder = value; OnPropertyChanged("SortOrder"); } } }

    private String _ConfigJson;
    /// <summary>配置 JSON（Personality/Capabilities/Tools）</summary>
    [DisplayName("配置 JSON")]
    [Description("配置 JSON（Personality/Capabilities/Tools）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ConfigJson", "配置 JSON（Personality/Capabilities/Tools）", "")]
    public String ConfigJson { get => _ConfigJson; set { if (OnPropertyChanging("ConfigJson", value)) { _ConfigJson = value; OnPropertyChanged("ConfigJson"); } } }

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
            "Description" => _Description,
            "Type" => _Type,
            "Avatar" => _Avatar,
            "SystemPrompt" => _SystemPrompt,
            "MaxIterations" => _MaxIterations,
            "IsEnabled" => _IsEnabled,
            "SortOrder" => _SortOrder,
            "ConfigJson" => _ConfigJson,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Type": _Type = value.ToInt(); break;
                case "Avatar": _Avatar = Convert.ToString(value); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value); break;
                case "MaxIterations": _MaxIterations = value.ToInt(); break;
                case "IsEnabled": _IsEnabled = value.ToBoolean(); break;
                case "SortOrder": _SortOrder = value.ToInt(); break;
                case "ConfigJson": _ConfigJson = Convert.ToString(value); break;
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
    /// <summary>根据 Agent ID 查找</summary>
    /// <param name="id">Agent ID</param>
    /// <returns>实体对象</returns>
    public static AgentDefinition FindById(String id)
    {
        if (id.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id.EqualIgnoreCase(id));

        return Find(_.Id == id);
    }

    /// <summary>查找全部启用的 Agent（按 SortOrder 排序）</summary>
    public static IList<AgentDefinition> FindAllEnabled()
    {
        if (Meta.Session.Count < MaxCacheCount)
            return Meta.Cache.FindAll(e => e.IsEnabled).OrderBy(e => e.SortOrder).ToList();

        return FindAll(_.IsEnabled == true, _.SortOrder.Asc(), null, 0, 0);
    }
    #endregion

    #region 字段名
    /// <summary>取得 Agent 定义字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>Agent ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");
        /// <summary>Agent 类型</summary>
        public static readonly Field Type = FindByName("Type");
        /// <summary>头像</summary>
        public static readonly Field Avatar = FindByName("Avatar");
        /// <summary>系统提示词</summary>
        public static readonly Field SystemPrompt = FindByName("SystemPrompt");
        /// <summary>最大迭代次数</summary>
        public static readonly Field MaxIterations = FindByName("MaxIterations");
        /// <summary>是否启用</summary>
        public static readonly Field IsEnabled = FindByName("IsEnabled");
        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");
        /// <summary>配置 JSON</summary>
        public static readonly Field ConfigJson = FindByName("ConfigJson");
        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得 Agent 定义字段名称的快捷方式</summary>
    public partial class __
    {
        public const String Id = "Id";
        public const String Name = "Name";
        public const String Description = "Description";
        public const String Type = "Type";
        public const String Avatar = "Avatar";
        public const String SystemPrompt = "SystemPrompt";
        public const String MaxIterations = "MaxIterations";
        public const String IsEnabled = "IsEnabled";
        public const String SortOrder = "SortOrder";
        public const String ConfigJson = "ConfigJson";
        public const String CreatedAt = "CreatedAt";
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
