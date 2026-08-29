using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace ForgeSelf.Api.Entities;

/// <summary>聊天轮次模型（XCode 生成分部）。</summary>
public partial class ChatTurnModel
{
    #region 属性
    /// <summary>轮次ID</summary>
    public Int64 Id { get; set; }

    /// <summary>所属会话ID</summary>
    public Int64 ChatSessionId { get; set; }

    /// <summary>轮次序号</summary>
    public Int32 TurnIndex { get; set; }

    /// <summary>会话键</summary>
    public String SessionKey { get; set; }

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

    /// <summary>流式请求关联ID</summary>
    public String RequestId { get; set; }

    /// <summary>实时回复文本</summary>
    public String ResponseText { get; set; }

    /// <summary>用户消息速览</summary>
    public String UserPreview { get; set; }

    /// <summary>助手回复速览</summary>
    public String AssistantPreview { get; set; }

    /// <summary>输入Tokens</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>输出Tokens</summary>
    public Int32 CompletionTokens { get; set; }

    /// <summary>总Tokens</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>首Token延迟</summary>
    public Int64 FirstTokenMs { get; set; }

    /// <summary>失败原因</summary>
    public String ErrorMessage { get; set; }

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
    public void Copy(IChatTurnModel model)
    {
        Id = model.Id;
        ChatSessionId = model.ChatSessionId;
        TurnIndex = model.TurnIndex;
        SessionKey = model.SessionKey;
        Style = model.Style;
        Model = model.Model;
        RequestMethod = model.RequestMethod;
        RequestPath = model.RequestPath;
        RequestHeaders = model.RequestHeaders;
        RequestBody = model.RequestBody;
        ResponseStatus = model.ResponseStatus;
        ResponseHeaders = model.ResponseHeaders;
        ResponseBody = model.ResponseBody;
        RequestId = model.RequestId;
        ResponseText = model.ResponseText;
        UserPreview = model.UserPreview;
        AssistantPreview = model.AssistantPreview;
        PromptTokens = model.PromptTokens;
        CompletionTokens = model.CompletionTokens;
        TotalTokens = model.TotalTokens;
        FirstTokenMs = model.FirstTokenMs;
        ErrorMessage = model.ErrorMessage;
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
