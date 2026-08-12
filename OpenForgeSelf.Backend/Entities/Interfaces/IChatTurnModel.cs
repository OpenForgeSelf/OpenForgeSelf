using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace OpenForgeSelf.Backend.Entities;

/// <summary>聊天轮次模型接口（XCode 生成分部）。</summary>
public partial interface IChatTurnModel
{
    #region 属性
    /// <summary>轮次ID</summary>
    Int64 Id { get; set; }

    /// <summary>所属会话ID</summary>
    Int64 ChatSessionId { get; set; }

    /// <summary>轮次序号</summary>
    Int32 TurnIndex { get; set; }

    /// <summary>会话键</summary>
    String SessionKey { get; set; }

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

    /// <summary>流式请求关联ID</summary>
    String RequestId { get; set; }

    /// <summary>实时回复文本</summary>
    String ResponseText { get; set; }

    /// <summary>用户消息速览</summary>
    String UserPreview { get; set; }

    /// <summary>助手回复速览</summary>
    String AssistantPreview { get; set; }

    /// <summary>输入Tokens</summary>
    Int32 PromptTokens { get; set; }

    /// <summary>输出Tokens</summary>
    Int32 CompletionTokens { get; set; }

    /// <summary>总Tokens</summary>
    Int32 TotalTokens { get; set; }

    /// <summary>首Token延迟</summary>
    Int64 FirstTokenMs { get; set; }

    /// <summary>失败原因</summary>
    String ErrorMessage { get; set; }

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
