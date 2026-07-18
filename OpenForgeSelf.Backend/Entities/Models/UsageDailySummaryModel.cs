using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class UsageDailySummaryModel
{
    #region 属性
    /// <summary>汇总记录ID</summary>
    public Int64 Id { get; set; }

    /// <summary>日期</summary>
    public DateTime Date { get; set; }

    /// <summary>插件ID</summary>
    public String PluginId { get; set; }

    /// <summary>工具ID</summary>
    public String ToolId { get; set; }

    /// <summary>使用次数</summary>
    public Int32 UseCount { get; set; }

    /// <summary>总持续时间（毫秒）</summary>
    public Int64 TotalDurationMs { get; set; }

    /// <summary>独立用户数</summary>
    public Int32 UniqueUsers { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUsageDailySummary model)
    {
        Id = model.Id;
        Date = model.Date;
        PluginId = model.PluginId;
        ToolId = model.ToolId;
        UseCount = model.UseCount;
        TotalDurationMs = model.TotalDurationMs;
        UniqueUsers = model.UniqueUsers;
    }
    #endregion
}
