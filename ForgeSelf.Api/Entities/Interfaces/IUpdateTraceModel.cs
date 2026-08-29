using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
public partial interface IUpdateTraceModel
{
    #region 属性
    /// <summary>主键</summary>
    Int64 Id { get; set; }

    /// <summary>更新前版本号</summary>
    String PreVersion { get; set; }

    /// <summary>更新后版本号</summary>
    String PostVersion { get; set; }

    /// <summary>更新操作时间</summary>
    DateTime UpdateTime { get; set; }

    /// <summary>结果状态：0=成功, 1=失败, 2=回滚</summary>
    Int32 Result { get; set; }

    /// <summary>失败时的错误信息</summary>
    String ErrorMessage { get; set; }

    /// <summary>回滚到的版本号</summary>
    String RollbackVersion { get; set; }

    /// <summary>更新包下载地址</summary>
    String DownloadUrl { get; set; }

    /// <summary>更新包大小</summary>
    Int64 PackageSize { get; set; }

    /// <summary>更新流程耗时(毫秒)</summary>
    Int64 DurationMs { get; set; }
    #endregion
}
