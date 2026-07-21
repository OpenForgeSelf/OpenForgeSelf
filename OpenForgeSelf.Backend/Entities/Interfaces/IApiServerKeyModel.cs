using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial interface IApiServerKeyModel
{
    #region 属性
    /// <summary>实体唯一标识</summary>
    Int64 Id { get; set; }

    /// <summary>API密钥密文（AES-256-CBC加密存储）</summary>
    String KeyCipher { get; set; }

    /// <summary>是否当前生效密钥</summary>
    Boolean IsActive { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }
    #endregion
}
