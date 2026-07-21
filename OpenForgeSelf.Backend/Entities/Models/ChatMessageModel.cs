using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class ChatMessageModel
{
    #region 属性
    /// <summary>消息ID</summary>
    public Int64 Id { get; set; }

    /// <summary>会话ID</summary>
    public String SessionId { get; set; }

    /// <summary>消息角色（user/assistant/system）</summary>
    public String Role { get; set; }

    /// <summary>消息内容</summary>
    public String Content { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IChatMessageModel model)
    {
        Id = model.Id;
        SessionId = model.SessionId;
        Role = model.Role;
        Content = model.Content;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
