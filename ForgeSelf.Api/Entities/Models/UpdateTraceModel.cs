using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>{name}。</summary>
public partial class UpdateTraceModel
{
    #region 属性
    /// <summary>主键</summary>
    public Int64 Id { get; set; }

    /// <summary>更新前版本号</summary>
    public String PreVersion { get; set; }

    /// <summary>更新后版本号</summary>
    public String PostVersion { get; set; }

    /// <summary>更新操作时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>结果状态：0=成功, 1=失败, 2=回滚</summary>
    public Int32 Result { get; set; }

    /// <summary>失败时的错误信息</summary>
    public String ErrorMessage { get; set; }

    /// <summary>回滚到的版本号</summary>
    public String RollbackVersion { get; set; }

    /// <summary>更新包下载地址</summary>
    public String DownloadUrl { get; set; }

    /// <summary>更新包大小</summary>
    public Int64 PackageSize { get; set; }

    /// <summary>更新流程耗时(毫秒)</summary>
    public Int64 DurationMs { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUpdateTraceModel model)
    {
        Id = model.Id;
        PreVersion = model.PreVersion;
        PostVersion = model.PostVersion;
        UpdateTime = model.UpdateTime;
        Result = model.Result;
        ErrorMessage = model.ErrorMessage;
        RollbackVersion = model.RollbackVersion;
        DownloadUrl = model.DownloadUrl;
        PackageSize = model.PackageSize;
        DurationMs = model.DurationMs;
    }
    #endregion
}
