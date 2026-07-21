using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class AIProviderModel
{
    #region 属性
    /// <summary>实体唯一标识</summary>
    public Int64 Id { get; set; }

    /// <summary>提供方显示名（唯一）</summary>
    public String Name { get; set; }

    /// <summary>提供方类型（OpenAI/Anthropic/Custom）</summary>
    public String ProviderType { get; set; }

    /// <summary>接入地址（base URL）</summary>
    public String Endpoint { get; set; }

    /// <summary>访问密钥（加密存储）</summary>
    public String ApiKey { get; set; }

    /// <summary>支持模型列表（逗号分隔）</summary>
    public String SupportedModels { get; set; }

    /// <summary>是否默认提供方</summary>
    public Boolean IsDefault { get; set; }

    /// <summary>请求超时（秒）</summary>
    public Int32 TimeoutSeconds { get; set; }

    /// <summary>多模态视觉模型名</summary>
    public String VisionModel { get; set; }

    /// <summary>是否启用多模态自动处理</summary>
    public Boolean EnableMultimodal { get; set; }

    /// <summary>图片识别系统提示词模板</summary>
    public String VisionPromptTemplate { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
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
}
