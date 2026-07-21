using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>{name}。</summary>
public partial class ChatRecordModel
{
    #region 属性
    /// <summary>记录ID</summary>
    public Int64 Id { get; set; }

    /// <summary>会话ID</summary>
    public String SessionId { get; set; }

    /// <summary>API风格</summary>
    public String Style { get; set; }

    /// <summary>模型名称</summary>
    public String Model { get; set; }

    /// <summary>请求方法</summary>
    public String RequestMethod { get; set; }

    /// <summary>请求路径</summary>
    public String RequestPath { get; set; }

    /// <summary>请求头JSON</summary>
    public String RequestHeaders { get; set; }

    /// <summary>请求体JSON</summary>
    public String RequestBody { get; set; }

    /// <summary>响应状态码</summary>
    public Int32 ResponseStatus { get; set; }

    /// <summary>响应头JSON</summary>
    public String ResponseHeaders { get; set; }

    /// <summary>响应体JSON</summary>
    public String ResponseBody { get; set; }

    /// <summary>温度参数</summary>
    public Double Temperature { get; set; }

    /// <summary>最大token</summary>
    public Int32 MaxTokens { get; set; }

    /// <summary>消息数量</summary>
    public Int32 MessageCount { get; set; }

    /// <summary>工具调用次数</summary>
    public Int32 ToolCallCount { get; set; }

    /// <summary>是否有reasoning</summary>
    public Boolean HasReasoning { get; set; }

    /// <summary>调用耗时</summary>
    public Int64 DurationMs { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IChatRecordModel model)
    {
        Id = model.Id;
        SessionId = model.SessionId;
        Style = model.Style;
        Model = model.Model;
        RequestMethod = model.RequestMethod;
        RequestPath = model.RequestPath;
        RequestHeaders = model.RequestHeaders;
        RequestBody = model.RequestBody;
        ResponseStatus = model.ResponseStatus;
        ResponseHeaders = model.ResponseHeaders;
        ResponseBody = model.ResponseBody;
        Temperature = model.Temperature;
        MaxTokens = model.MaxTokens;
        MessageCount = model.MessageCount;
        ToolCallCount = model.ToolCallCount;
        HasReasoning = model.HasReasoning;
        DurationMs = model.DurationMs;
        CreatedTime = model.CreatedTime;
    }
    #endregion
}
