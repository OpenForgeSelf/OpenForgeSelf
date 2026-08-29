using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
public partial interface IAIProviderModel
{
    #region 属性
    /// <summary>实体唯一标识</summary>
    Int64 Id { get; set; }

    /// <summary>提供方显示名（唯一）</summary>
    String Name { get; set; }

    /// <summary>提供方类型（OpenAI/Anthropic/Custom）</summary>
    String ProviderType { get; set; }

    /// <summary>接入地址（base URL）</summary>
    String Endpoint { get; set; }

    /// <summary>访问密钥（加密存储）</summary>
    String ApiKey { get; set; }

    /// <summary>支持模型列表（逗号分隔）</summary>
    String SupportedModels { get; set; }

    /// <summary>是否默认提供方</summary>
    Boolean IsDefault { get; set; }

    /// <summary>请求超时（秒）</summary>
    Int32 TimeoutSeconds { get; set; }

    /// <summary>多模态视觉模型名</summary>
    String VisionModel { get; set; }

    /// <summary>是否启用多模态自动处理</summary>
    Boolean EnableMultimodal { get; set; }

    /// <summary>图片识别系统提示词模板</summary>
    String VisionPromptTemplate { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }
    #endregion
}
