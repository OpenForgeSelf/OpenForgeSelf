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

/// <summary>{name}。；与 AIChatMessage 按 SessionId 对应）</summary>
[Serializable]
[DataObject]
[Description("{name}。；与 AIChatMessage 按 SessionId 对应）")]
[BindIndex("IU_AIChatSession_SessionId", true, "SessionId")]
[BindIndex("IX_AIChatSession_Archived", false, "Archived")]
[BindTable("AIChatSession", Description = "AI会话元数据（归档等会话级状态；与 AIChatMessage 按 SessionId 对应）", ConnName = "AIAgent", DbType = DatabaseType.None)]
public partial class AIChatSession
{
    #region 属性
    private Int64 _Id;
    /// <summary>会话记录ID</summary>
    [DisplayName("会话记录ID")]
    [Description("会话记录ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "会话记录ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionId;
    /// <summary>会话ID（与 AIChatMessage.SessionId 一致）</summary>
    [DisplayName("会话ID（与AIChatMessage")]
    [Description("会话ID（与 AIChatMessage.SessionId 一致）")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("SessionId", "会话ID（与 AIChatMessage.SessionId 一致）", "", Master = true)]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private Boolean _Archived;
    /// <summary>是否已归档（true=已归档，默认不在 agent 页展示）</summary>
    [DisplayName("是否已归档（true=已归档")]
    [Description("是否已归档（true=已归档，默认不在 agent 页展示）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Archived", "是否已归档（true=已归档，默认不在 agent 页展示）", "", DefaultValue = "0")]
    public Boolean Archived { get => _Archived; set { if (OnPropertyChanging("Archived", value)) { _Archived = value; OnPropertyChanged("Archived"); } } }

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
            "Archived" => _Archived,
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
                case "Archived": _Archived = value.ToBoolean(); break;
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
    /// <summary>根据会话记录ID查找</summary>
    /// <param name="id">会话记录ID</param>
    /// <returns>实体对象</returns>
    public static AIChatSession FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据会话ID（与AIChatMessage查找</summary>
    /// <param name="sessionId">会话ID（与AIChatMessage</param>
    /// <returns>实体对象</returns>
    public static AIChatSession FindBySessionId(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.SessionId.EqualIgnoreCase(sessionId));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(sessionId) as AIChatSession;

        //return Find(_.SessionId == sessionId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="archived">是否已归档（true=已归档，默认不在 agent 页展示）</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AIChatSession> Search(Boolean? archived, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (archived != null) exp &= _.Archived == archived;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得AI会话元数据（归档等会话级状态字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>会话记录ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话ID（与 AIChatMessage.SessionId 一致）</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>是否已归档（true=已归档，默认不在 agent 页展示）</summary>
        public static readonly Field Archived = FindByName("Archived");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得AI会话元数据（归档等会话级状态字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>会话记录ID</summary>
        public const String Id = "Id";

        /// <summary>会话ID（与 AIChatMessage.SessionId 一致）</summary>
        public const String SessionId = "SessionId";

        /// <summary>是否已归档（true=已归档，默认不在 agent 页展示）</summary>
        public const String Archived = "Archived";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
