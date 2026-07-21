using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial interface IChatMessageModel
{
    #region 属性
    /// <summary>消息ID</summary>
    Int64 Id { get; set; }

    /// <summary>会话ID</summary>
    String SessionId { get; set; }

    /// <summary>消息角色（user/assistant/system）</summary>
    String Role { get; set; }

    /// <summary>消息内容</summary>
    String Content { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }
    #endregion
}
