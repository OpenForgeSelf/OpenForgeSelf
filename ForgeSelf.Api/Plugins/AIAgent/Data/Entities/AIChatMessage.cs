using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.AIAgent.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_AIChatMessage_SessionId", false, "SessionId")]
[BindIndex("IX_AIChatMessage_CreateTime", false, "CreateTime")]
[BindIndex("IX_AIChatMessage_SessionId_CreateTime", false, "SessionId,CreateTime")]
[BindTable("AIChatMessage", Description = "AI聊天消息", ConnName = "AIAgent", DbType = DatabaseType.None)]
public partial class AIChatMessage
{
    #region 属性
    private Int64 _Id;
    /// <summary>消息ID</summary>
    [DisplayName("消息ID")]
    [Description("消息ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "消息ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionId;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("SessionId", "会话ID", "", Master = true)]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _Role;
    /// <summary>角色（user/assistant/system）</summary>
    [DisplayName("角色（user_assistant_system）")]
    [Description("角色（user/assistant/system）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Role", "角色（user/assistant/system）", "")]
    public String Role { get => _Role; set { if (OnPropertyChanging("Role", value)) { _Role = value; OnPropertyChanged("Role"); } } }

    private String _Content;
    /// <summary>内容</summary>
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("Content", "内容", "")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private String _ToolCallsJson;
    /// <summary>工具调用轨迹（FreeLoop 执行记录，JSON 数组；供刷新后复盘）</summary>
    [DisplayName("工具调用轨迹（FreeLoop执行记录")]
    [Description("工具调用轨迹（FreeLoop 执行记录，JSON 数组；供刷新后复盘）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ToolCallsJson", "工具调用轨迹（FreeLoop 执行记录，JSON 数组；供刷新后复盘）", "")]
    public String ToolCallsJson { get => _ToolCallsJson; set { if (OnPropertyChanging("ToolCallsJson", value)) { _ToolCallsJson = value; OnPropertyChanged("ToolCallsJson"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "SessionId" => _SessionId,
            "Role" => _Role,
            "Content" => _Content,
            "ToolCallsJson" => _ToolCallsJson,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "Role": _Role = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "ToolCallsJson": _ToolCallsJson = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据消息ID查找</summary>
    /// <param name="id">消息ID</param>
    /// <returns>实体对象</returns>
    public static AIChatMessage FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据会话ID查找</summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>实体列表</returns>
    public static IList<AIChatMessage> FindAllBySessionId(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SessionId.EqualIgnoreCase(sessionId));

        return FindAll(_.SessionId == sessionId);
    }
    #endregion

    #region 字段名
    /// <summary>取得AI聊天消息字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>消息ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>角色（user/assistant/system）</summary>
        public static readonly Field Role = FindByName("Role");

        /// <summary>内容</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>工具调用轨迹（FreeLoop 执行记录，JSON 数组；供刷新后复盘）</summary>
        public static readonly Field ToolCallsJson = FindByName("ToolCallsJson");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得AI聊天消息字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>消息ID</summary>
        public const String Id = "Id";

        /// <summary>会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>角色（user/assistant/system）</summary>
        public const String Role = "Role";

        /// <summary>内容</summary>
        public const String Content = "Content";

        /// <summary>工具调用轨迹（FreeLoop 执行记录，JSON 数组；供刷新后复盘）</summary>
        public const String ToolCallsJson = "ToolCallsJson";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
