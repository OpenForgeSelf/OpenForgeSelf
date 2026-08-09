using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial interface IChatRecordModel
{
    #region 属性
    /// <summary>记录ID</summary>
    Int64 Id { get; set; }

    /// <summary>会话ID</summary>
    String SessionId { get; set; }

    /// <summary>API风格</summary>
    String Style { get; set; }

    /// <summary>模型名称</summary>
    String Model { get; set; }

    /// <summary>请求方法</summary>
    String RequestMethod { get; set; }

    /// <summary>请求路径</summary>
    String RequestPath { get; set; }

    /// <summary>请求头JSON</summary>
    String RequestHeaders { get; set; }

    /// <summary>请求体JSON</summary>
    String RequestBody { get; set; }

    /// <summary>响应状态码</summary>
    Int32 ResponseStatus { get; set; }

    /// <summary>响应头JSON</summary>
    String ResponseHeaders { get; set; }

    /// <summary>响应体JSON</summary>
    String ResponseBody { get; set; }

    /// <summary>流式请求关联ID（用于流式期间按同一请求追加更新）</summary>
    String RequestId { get; set; }

    /// <summary>实时/增量纯文本回复（流式期间逐批更新，便于前端实时展示）</summary>
    String ResponseText { get; set; }

    /// <summary>温度参数</summary>
    Double Temperature { get; set; }

    /// <summary>最大token</summary>
    Int32 MaxTokens { get; set; }

    /// <summary>消息数量</summary>
    Int32 MessageCount { get; set; }

    /// <summary>工具调用次数</summary>
    Int32 ToolCallCount { get; set; }

    /// <summary>是否有reasoning</summary>
    Boolean HasReasoning { get; set; }

    /// <summary>调用耗时</summary>
    Int64 DurationMs { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreatedTime { get; set; }
    #endregion
}
