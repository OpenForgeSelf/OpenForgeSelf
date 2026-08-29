using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
public partial interface IAIModelModel
{
    #region 属性
    /// <summary>实体唯一标识</summary>
    Int64 Id { get; set; }

    /// <summary>归属供应商Id（AIProvider.Id）</summary>
    Int64 ProviderId { get; set; }

    /// <summary>归属供应商名（拼接聊天id用，锁定不可编辑）</summary>
    String ProviderName { get; set; }

    /// <summary>上游原始模型标识（如 gpt-4o），锁定不可编辑</summary>
    String UpstreamModelId { get; set; }

    /// <summary>项目聊天模型id：提供商:原始模型id（复制按钮输出）</summary>
    String ChatModelId { get; set; }

    /// <summary>显示别名/备注（用户可编辑）</summary>
    String Alias { get; set; }

    /// <summary>能力标签（逗号分隔，如 vision,stream，用户可编辑）</summary>
    String Capabilities { get; set; }

    /// <summary>最大上下文长度（token），0=未设置</summary>
    Int32 MaxContext { get; set; }

    /// <summary>是否启用（已启用/已禁用），默认启用</summary>
    Boolean Enabled { get; set; }

    /// <summary>上游返回的 owner/owned_by</summary>
    String Owner { get; set; }

    /// <summary>最近同步（拉取）时间</summary>
    DateTime LastSyncTime { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }
    #endregion
}
