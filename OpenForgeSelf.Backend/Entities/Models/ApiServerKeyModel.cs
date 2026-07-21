using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class ApiServerKeyModel
{
    #region 属性
    /// <summary>实体唯一标识</summary>
    public Int64 Id { get; set; }

    /// <summary>API密钥密文（AES-256-CBC加密存储）</summary>
    public String KeyCipher { get; set; }

    /// <summary>是否当前生效密钥</summary>
    public Boolean IsActive { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IApiServerKeyModel model)
    {
        Id = model.Id;
        KeyCipher = model.KeyCipher;
        IsActive = model.IsActive;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
