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

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_WorkflowDefinition_Category", false, "Category")]
[BindIndex("IX_WorkflowDefinition_Status", false, "Status")]
[BindTable("WorkflowDefinition", Description = "工作流定义", ConnName = "WorkflowEngine", DbType = DatabaseType.None)]
public partial class WorkflowDefinition
{
    #region 属性
    private Int64 _Id;
    /// <summary>工作流ID</summary>
    [DisplayName("工作流ID")]
    [Description("工作流ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "工作流ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Category;
    /// <summary>分类</summary>
    [DisplayName("分类")]
    [Description("分类")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Category", "分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Icon;
    /// <summary>图标</summary>
    [DisplayName("图标")]
    [Description("图标")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Icon", "图标", "")]
    public String Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private String _StepsJson;
    /// <summary>步骤JSON</summary>
    [DisplayName("步骤JSON")]
    [Description("步骤JSON")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("StepsJson", "步骤JSON", "")]
    public String StepsJson { get => _StepsJson; set { if (OnPropertyChanging("StepsJson", value)) { _StepsJson = value; OnPropertyChanged("StepsJson"); } } }

    private String _VariablesJson;
    /// <summary>变量JSON</summary>
    [DisplayName("变量JSON")]
    [Description("变量JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("VariablesJson", "变量JSON", "")]
    public String VariablesJson { get => _VariablesJson; set { if (OnPropertyChanging("VariablesJson", value)) { _VariablesJson = value; OnPropertyChanged("VariablesJson"); } } }

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

    private Boolean _IsFavorite;
    /// <summary>是否收藏</summary>
    [DisplayName("是否收藏")]
    [Description("是否收藏")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsFavorite", "是否收藏", "")]
    public Boolean IsFavorite { get => _IsFavorite; set { if (OnPropertyChanging("IsFavorite", value)) { _IsFavorite = value; OnPropertyChanged("IsFavorite"); } } }

    private Int32 _UsageCount;
    /// <summary>使用次数</summary>
    [DisplayName("使用次数")]
    [Description("使用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UsageCount", "使用次数", "")]
    public Int32 UsageCount { get => _UsageCount; set { if (OnPropertyChanging("UsageCount", value)) { _UsageCount = value; OnPropertyChanged("UsageCount"); } } }

    private Int32 _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _StartStepId;
    /// <summary>开始步骤ID</summary>
    [DisplayName("开始步骤ID")]
    [Description("开始步骤ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("StartStepId", "开始步骤ID", "")]
    public String StartStepId { get => _StartStepId; set { if (OnPropertyChanging("StartStepId", value)) { _StartStepId = value; OnPropertyChanged("StartStepId"); } } }

    private String _MetadataJson;
    /// <summary>元数据JSON</summary>
    [DisplayName("元数据JSON")]
    [Description("元数据JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("MetadataJson", "元数据JSON", "")]
    public String MetadataJson { get => _MetadataJson; set { if (OnPropertyChanging("MetadataJson", value)) { _MetadataJson = value; OnPropertyChanged("MetadataJson"); } } }
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
            "Category" => _Category,
            "Icon" => _Icon,
            "StepsJson" => _StepsJson,
            "VariablesJson" => _VariablesJson,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "IsFavorite" => _IsFavorite,
            "UsageCount" => _UsageCount,
            "Status" => _Status,
            "StartStepId" => _StartStepId,
            "MetadataJson" => _MetadataJson,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "StepsJson": _StepsJson = Convert.ToString(value); break;
                case "VariablesJson": _VariablesJson = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "IsFavorite": _IsFavorite = value.ToBoolean(); break;
                case "UsageCount": _UsageCount = value.ToInt(); break;
                case "Status": _Status = value.ToInt(); break;
                case "StartStepId": _StartStepId = Convert.ToString(value); break;
                case "MetadataJson": _MetadataJson = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据工作流ID查找</summary>
    /// <param name="id">工作流ID</param>
    /// <returns>实体对象</returns>
    public static WorkflowDefinition FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据分类查找</summary>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowDefinition> FindAllByCategory(String category)
    {
        if (category.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

        return FindAll(_.Category == category);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowDefinition> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">分类</param>
    /// <param name="status">状态</param>
    /// <param name="isFavorite">是否收藏</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<WorkflowDefinition> Search(String category, Int32 status, Boolean? isFavorite, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (status >= 0) exp &= _.Status == status;
        if (isFavorite != null) exp &= _.IsFavorite == isFavorite;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得工作流定义字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>工作流ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>图标</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>步骤JSON</summary>
        public static readonly Field StepsJson = FindByName("StepsJson");

        /// <summary>变量JSON</summary>
        public static readonly Field VariablesJson = FindByName("VariablesJson");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        /// <summary>是否收藏</summary>
        public static readonly Field IsFavorite = FindByName("IsFavorite");

        /// <summary>使用次数</summary>
        public static readonly Field UsageCount = FindByName("UsageCount");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>开始步骤ID</summary>
        public static readonly Field StartStepId = FindByName("StartStepId");

        /// <summary>元数据JSON</summary>
        public static readonly Field MetadataJson = FindByName("MetadataJson");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得工作流定义字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>工作流ID</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>描述</summary>
        public const String Description = "Description";

        /// <summary>分类</summary>
        public const String Category = "Category";

        /// <summary>图标</summary>
        public const String Icon = "Icon";

        /// <summary>步骤JSON</summary>
        public const String StepsJson = "StepsJson";

        /// <summary>变量JSON</summary>
        public const String VariablesJson = "VariablesJson";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";

        /// <summary>是否收藏</summary>
        public const String IsFavorite = "IsFavorite";

        /// <summary>使用次数</summary>
        public const String UsageCount = "UsageCount";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>开始步骤ID</summary>
        public const String StartStepId = "StartStepId";

        /// <summary>元数据JSON</summary>
        public const String MetadataJson = "MetadataJson";
    }
    #endregion
}
