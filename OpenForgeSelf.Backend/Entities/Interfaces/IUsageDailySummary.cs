using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial interface IUsageDailySummary
{
    #region 属性
    /// <summary>汇总记录ID</summary>
    Int64 Id { get; set; }

    /// <summary>日期</summary>
    DateTime Date { get; set; }

    /// <summary>插件ID</summary>
    String PluginId { get; set; }

    /// <summary>工具ID</summary>
    String ToolId { get; set; }

    /// <summary>使用次数</summary>
    Int32 UseCount { get; set; }

    /// <summary>总持续时间（毫秒）</summary>
    Int64 TotalDurationMs { get; set; }

    /// <summary>独立用户数</summary>
    Int32 UniqueUsers { get; set; }
    #endregion
}
