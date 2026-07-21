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
[BindIndex("IX_AIProvider_Name", false, "Name")]
[BindIndex("IX_AIProvider_IsDefault", false, "IsDefault")]
[BindTable("AIProvider", Description = "AI提供方配置", ConnName = "OpenForgeSelf", DbType = DatabaseType.None)]
public partial class AIProvider : IAIProviderModel, IEntity<IAIProviderModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>实体唯一标识</summary>
    [DisplayName("实体唯一标识")]
    [Description("实体唯一标识")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "实体唯一标识", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>提供方显示名（唯一）</summary>
    [DisplayName("提供方显示名（唯一）")]
    [Description("提供方显示名（唯一）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "提供方显示名（唯一）", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _ProviderType;
    /// <summary>提供方类型（OpenAI/Anthropic/Custom）</summary>
    [DisplayName("提供方类型（OpenAI_Anthropic_Custom）")]
    [Description("提供方类型（OpenAI/Anthropic/Custom）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("ProviderType", "提供方类型（OpenAI/Anthropic/Custom）", "")]
    public String ProviderType { get => _ProviderType; set { if (OnPropertyChanging("ProviderType", value)) { _ProviderType = value; OnPropertyChanged("ProviderType"); } } }

    private String _Endpoint;
    /// <summary>接入地址（base URL）</summary>
    [DisplayName("接入地址（baseURL）")]
    [Description("接入地址（base URL）")]
    [DataObjectField(false, false, false, 500)]
    [BindColumn("Endpoint", "接入地址（base URL）", "")]
    public String Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private String _ApiKey;
    /// <summary>访问密钥（加密存储）</summary>
    [DisplayName("访问密钥（加密存储）")]
    [Description("访问密钥（加密存储）")]
    [DataObjectField(false, false, false, 500)]
    [BindColumn("ApiKey", "访问密钥（加密存储）", "")]
    public String ApiKey { get => _ApiKey; set { if (OnPropertyChanging("ApiKey", value)) { _ApiKey = value; OnPropertyChanged("ApiKey"); } } }

    private String _SupportedModels;
    /// <summary>支持模型列表（逗号分隔）</summary>
    [DisplayName("支持模型列表（逗号分隔）")]
    [Description("支持模型列表（逗号分隔）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("SupportedModels", "支持模型列表（逗号分隔）", "")]
    public String SupportedModels { get => _SupportedModels; set { if (OnPropertyChanging("SupportedModels", value)) { _SupportedModels = value; OnPropertyChanged("SupportedModels"); } } }

    private Boolean _IsDefault;
    /// <summary>是否默认提供方</summary>
    [DisplayName("是否默认提供方")]
    [Description("是否默认提供方")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsDefault", "是否默认提供方", "")]
    public Boolean IsDefault { get => _IsDefault; set { if (OnPropertyChanging("IsDefault", value)) { _IsDefault = value; OnPropertyChanged("IsDefault"); } } }

    private Int32 _TimeoutSeconds;
    /// <summary>请求超时（秒）</summary>
    [DisplayName("请求超时（秒）")]
    [Description("请求超时（秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TimeoutSeconds", "请求超时（秒）", "")]
    public Int32 TimeoutSeconds { get => _TimeoutSeconds; set { if (OnPropertyChanging("TimeoutSeconds", value)) { _TimeoutSeconds = value; OnPropertyChanged("TimeoutSeconds"); } } }

    private String _VisionModel;
    /// <summary>多模态视觉模型名</summary>
    [DisplayName("多模态视觉模型名")]
    [Description("多模态视觉模型名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("VisionModel", "多模态视觉模型名", "")]
    public String VisionModel { get => _VisionModel; set { if (OnPropertyChanging("VisionModel", value)) { _VisionModel = value; OnPropertyChanged("VisionModel"); } } }

    private Boolean _EnableMultimodal;
    /// <summary>是否启用多模态自动处理</summary>
    [DisplayName("是否启用多模态自动处理")]
    [Description("是否启用多模态自动处理")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EnableMultimodal", "是否启用多模态自动处理", "")]
    public Boolean EnableMultimodal { get => _EnableMultimodal; set { if (OnPropertyChanging("EnableMultimodal", value)) { _EnableMultimodal = value; OnPropertyChanged("EnableMultimodal"); } } }

    private String _VisionPromptTemplate;
    /// <summary>图片识别系统提示词模板</summary>
    [DisplayName("图片识别系统提示词模板")]
    [Description("图片识别系统提示词模板")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("VisionPromptTemplate", "图片识别系统提示词模板", "")]
    public String VisionPromptTemplate { get => _VisionPromptTemplate; set { if (OnPropertyChanging("VisionPromptTemplate", value)) { _VisionPromptTemplate = value; OnPropertyChanged("VisionPromptTemplate"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IAIProviderModel model)
    {
        Id = model.Id;
        Name = model.Name;
        ProviderType = model.ProviderType;
        Endpoint = model.Endpoint;
        ApiKey = model.ApiKey;
        SupportedModels = model.SupportedModels;
        IsDefault = model.IsDefault;
        TimeoutSeconds = model.TimeoutSeconds;
        VisionModel = model.VisionModel;
        EnableMultimodal = model.EnableMultimodal;
        VisionPromptTemplate = model.VisionPromptTemplate;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
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
            "Name" => _Name,
            "ProviderType" => _ProviderType,
            "Endpoint" => _Endpoint,
            "ApiKey" => _ApiKey,
            "SupportedModels" => _SupportedModels,
            "IsDefault" => _IsDefault,
            "TimeoutSeconds" => _TimeoutSeconds,
            "VisionModel" => _VisionModel,
            "EnableMultimodal" => _EnableMultimodal,
            "VisionPromptTemplate" => _VisionPromptTemplate,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ProviderType": _ProviderType = Convert.ToString(value); break;
                case "Endpoint": _Endpoint = Convert.ToString(value); break;
                case "ApiKey": _ApiKey = Convert.ToString(value); break;
                case "SupportedModels": _SupportedModels = Convert.ToString(value); break;
                case "IsDefault": _IsDefault = value.ToBoolean(); break;
                case "TimeoutSeconds": _TimeoutSeconds = value.ToInt(); break;
                case "VisionModel": _VisionModel = Convert.ToString(value); break;
                case "EnableMultimodal": _EnableMultimodal = value.ToBoolean(); break;
                case "VisionPromptTemplate": _VisionPromptTemplate = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据实体唯一标识查找</summary>
    /// <param name="id">实体唯一标识</param>
    /// <returns>实体对象</returns>
    public static AIProvider FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据提供方显示名（唯一）查找</summary>
    /// <param name="name">提供方显示名（唯一）</param>
    /// <returns>实体列表</returns>
    public static IList<AIProvider> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="isDefault">是否默认提供方</param>
    /// <param name="enableMultimodal">是否启用多模态自动处理</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AIProvider> Search(Boolean? isDefault, Boolean? enableMultimodal, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (isDefault != null) exp &= _.IsDefault == isDefault;
        if (enableMultimodal != null) exp &= _.EnableMultimodal == enableMultimodal;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得AI提供方配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>实体唯一标识</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>提供方显示名（唯一）</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>提供方类型（OpenAI/Anthropic/Custom）</summary>
        public static readonly Field ProviderType = FindByName("ProviderType");

        /// <summary>接入地址（base URL）</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>访问密钥（加密存储）</summary>
        public static readonly Field ApiKey = FindByName("ApiKey");

        /// <summary>支持模型列表（逗号分隔）</summary>
        public static readonly Field SupportedModels = FindByName("SupportedModels");

        /// <summary>是否默认提供方</summary>
        public static readonly Field IsDefault = FindByName("IsDefault");

        /// <summary>请求超时（秒）</summary>
        public static readonly Field TimeoutSeconds = FindByName("TimeoutSeconds");

        /// <summary>多模态视觉模型名</summary>
        public static readonly Field VisionModel = FindByName("VisionModel");

        /// <summary>是否启用多模态自动处理</summary>
        public static readonly Field EnableMultimodal = FindByName("EnableMultimodal");

        /// <summary>图片识别系统提示词模板</summary>
        public static readonly Field VisionPromptTemplate = FindByName("VisionPromptTemplate");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得AI提供方配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>实体唯一标识</summary>
        public const String Id = "Id";

        /// <summary>提供方显示名（唯一）</summary>
        public const String Name = "Name";

        /// <summary>提供方类型（OpenAI/Anthropic/Custom）</summary>
        public const String ProviderType = "ProviderType";

        /// <summary>接入地址（base URL）</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>访问密钥（加密存储）</summary>
        public const String ApiKey = "ApiKey";

        /// <summary>支持模型列表（逗号分隔）</summary>
        public const String SupportedModels = "SupportedModels";

        /// <summary>是否默认提供方</summary>
        public const String IsDefault = "IsDefault";

        /// <summary>请求超时（秒）</summary>
        public const String TimeoutSeconds = "TimeoutSeconds";

        /// <summary>多模态视觉模型名</summary>
        public const String VisionModel = "VisionModel";

        /// <summary>是否启用多模态自动处理</summary>
        public const String EnableMultimodal = "EnableMultimodal";

        /// <summary>图片识别系统提示词模板</summary>
        public const String VisionPromptTemplate = "VisionPromptTemplate";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
