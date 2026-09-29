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

namespace ForgeSelf.Api.Entities;

/// <summary>会话事件日志（append-only）。</summary>
/// <remarks>
/// B2（040）：<c>ISessionStore</c> 的持久化载体。一行 = 一条 <see cref="ForgeSelf.Abstractions.SessionEvent"/>。
/// 只追加、不更新、不删除；<c>PayloadJson</c> 存 record 的完整序列化（System.Text.Json，多态还原见
/// <c>SessionEventJsonConverter</c>）。真相源唯一，模型历史 / UI / 统计一律从本表派生。
/// </remarks>
[Serializable]
[DataObject]
[Description("会话事件日志（append-only）。")]
[BindIndex("IX_SessionEvent_SessionId", false, "SessionId")]
[BindIndex("IX_SessionEvent_SessionId_Id", false, "SessionId,Id")]
[BindTable("SessionEvent", Description = "会话事件日志（append-only）", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class SessionEventEntity
{
    #region 属性
    private Int64 _Id;
    /// <summary>事件ID（全局单调递增，行主键即真相源）</summary>
    [DisplayName("事件ID")]
    [Description("事件ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "事件ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionId;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("SessionId", "会话ID", "", Master = true)]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private Int64 _Ts;
    /// <summary>事件时间戳（Unix 毫秒，规避时区与精度漂移）</summary>
    [DisplayName("事件时间戳")]
    [Description("事件时间戳（Unix 毫秒）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Ts", "事件时间戳（Unix毫秒）", "")]
    public Int64 Ts { get => _Ts; set { if (OnPropertyChanging("Ts", value)) { _Ts = value; OnPropertyChanged("Ts"); } } }

    private String _Type;
    /// <summary>事件类型名（如 user/message；必须在 SessionEventMap 中注册）</summary>
    [DisplayName("事件类型名")]
    [Description("事件类型名")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Type", "事件类型名", "")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _PayloadJson;
    /// <summary>事件载荷（record 完整序列化 JSON）</summary>
    [DisplayName("事件载荷")]
    [Description("事件载荷（record 完整序列化 JSON）")]
    // 长度 -1 = 文本类型（不限长）：工具结果 / 大段内容不受长度限制（同 ChatMessage.Content 惯例）
    [DataObjectField(false, false, false, -1)]
    [BindColumn("PayloadJson", "事件载荷", "")]
    public String PayloadJson { get => _PayloadJson; set { if (OnPropertyChanging("PayloadJson", value)) { _PayloadJson = value; OnPropertyChanged("PayloadJson"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(SessionEventEntity model)
    {
        Id = model.Id;
        SessionId = model.SessionId;
        Ts = model.Ts;
        Type = model.Type;
        PayloadJson = model.PayloadJson;
    }
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
            "Ts" => _Ts,
            "Type" => _Type,
            "PayloadJson" => _PayloadJson,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "Ts": _Ts = value.ToLong(); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "PayloadJson": _PayloadJson = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据事件ID查找</summary>
    /// <param name="id">事件ID</param>
    /// <returns>实体对象</returns>
    public static SessionEventEntity? FindById(Int64 id)
    {
        if (id <= 0) return null;

        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        return Meta.SingleCache[id];
    }

    /// <summary>根据会话ID查找（按 Id 升序）</summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>实体列表</returns>
    public static IList<SessionEventEntity> FindAllBySessionId(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return [];

        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SessionId.EqualIgnoreCase(sessionId));

        return FindAll(_.SessionId == sessionId);
    }
    #endregion

    #region 字段名
    /// <summary>取得会话事件字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>事件ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>事件时间戳</summary>
        public static readonly Field Ts = FindByName("Ts");

        /// <summary>事件类型名</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>事件载荷</summary>
        public static readonly Field PayloadJson = FindByName("PayloadJson");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得会话事件字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>事件ID</summary>
        public const String Id = "Id";

        /// <summary>会话ID</summary>
        public const String SessionId = "SessionId";

        /// <summary>事件时间戳</summary>
        public const String Ts = "Ts";

        /// <summary>事件类型名</summary>
        public const String Type = "Type";

        /// <summary>事件载荷</summary>
        public const String PayloadJson = "PayloadJson";
    }
    #endregion
}
