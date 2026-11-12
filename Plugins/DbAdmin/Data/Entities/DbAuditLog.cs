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

namespace ForgeSelf.Api.Plugins.DbAdmin.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_DbAuditLog_CreateTime", false, "CreateTime")]
[BindIndex("IX_DbAuditLog_Kind", false, "Kind")]
[BindTable("DbAuditLog", Description = "审计日志（脱敏）", ConnName = "DbAdmin", DbType = DatabaseType.None)]
public partial class DbAuditLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Kind;
    /// <summary>conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode</summary>
    [DisplayName("conn_create_conn_update_conn_delete_conn_test_query_classify_approval_*_write_result_rejected_permission_mode")]
    [Description("conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Kind", "conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _ConnectionId;
    /// <summary>连接标识</summary>
    [DisplayName("连接标识")]
    [Description("连接标识")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ConnectionId", "连接标识", "")]
    public String ConnectionId { get => _ConnectionId; set { if (OnPropertyChanging("ConnectionId", value)) { _ConnectionId = value; OnPropertyChanged("ConnectionId"); } } }

    private String _SqlRedacted;
    /// <summary>脱敏后的 SQL</summary>
    [DisplayName("脱敏后的SQL")]
    [Description("脱敏后的 SQL")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("SqlRedacted", "脱敏后的 SQL", "")]
    public String SqlRedacted { get => _SqlRedacted; set { if (OnPropertyChanging("SqlRedacted", value)) { _SqlRedacted = value; OnPropertyChanged("SqlRedacted"); } } }

    private String _Decision;
    /// <summary>决策 allow/ask/reject/approved 等</summary>
    [DisplayName("决策allow_ask_reject_approved等")]
    [Description("决策 allow/ask/reject/approved 等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Decision", "决策 allow/ask/reject/approved 等", "")]
    public String Decision { get => _Decision; set { if (OnPropertyChanging("Decision", value)) { _Decision = value; OnPropertyChanged("Decision"); } } }

    private String _Detail;
    /// <summary>详情（脱敏）</summary>
    [DisplayName("详情（脱敏）")]
    [Description("详情（脱敏）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Detail", "详情（脱敏）", "", Master = true)]
    public String Detail { get => _Detail; set { if (OnPropertyChanging("Detail", value)) { _Detail = value; OnPropertyChanged("Detail"); } } }

    private String _Source;
    /// <summary>来源 ui/agent</summary>
    [DisplayName("来源ui_agent")]
    [Description("来源 ui/agent")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源 ui/agent", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _SessionId;
    /// <summary>Agent 会话标识（仅 agent）</summary>
    [DisplayName("Agent会话标识（仅agent）")]
    [Description("Agent 会话标识（仅 agent）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("SessionId", "Agent 会话标识（仅 agent）", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _Status;
    /// <summary>结果状态 ok/awaiting_approval/rejected/error（kind=agent 摘要用）</summary>
    [DisplayName("结果状态ok_awaiting_approval_rejected_error（kind=agent摘要用）")]
    [Description("结果状态 ok/awaiting_approval/rejected/error（kind=agent 摘要用）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Status", "结果状态 ok/awaiting_approval/rejected/error（kind=agent 摘要用）", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _Code;
    /// <summary>错误码（kind=agent 摘要用）</summary>
    [DisplayName("错误码（kind=agent摘要用）")]
    [Description("错误码（kind=agent 摘要用）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "错误码（kind=agent 摘要用）", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _SqlKind;
    /// <summary>语句类别 read/write（kind=agent 摘要用）</summary>
    [DisplayName("语句类别read_write（kind=agent摘要用）")]
    [Description("语句类别 read/write（kind=agent 摘要用）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SqlKind", "语句类别 read/write（kind=agent 摘要用）", "")]
    public String SqlKind { get => _SqlKind; set { if (OnPropertyChanging("SqlKind", value)) { _SqlKind = value; OnPropertyChanged("SqlKind"); } } }

    private Int64 _RowCount;
    /// <summary>返回行数或影响行数</summary>
    [DisplayName("返回行数或影响行数")]
    [Description("返回行数或影响行数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RowCount", "返回行数或影响行数", "")]
    public Int64 RowCount { get => _RowCount; set { if (OnPropertyChanging("RowCount", value)) { _RowCount = value; OnPropertyChanged("RowCount"); } } }

    private Boolean _Truncated;
    /// <summary>结果是否被 Agent 上限截断</summary>
    [DisplayName("结果是否被Agent上限截断")]
    [Description("结果是否被 Agent 上限截断")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Truncated", "结果是否被 Agent 上限截断", "")]
    public Boolean Truncated { get => _Truncated; set { if (OnPropertyChanging("Truncated", value)) { _Truncated = value; OnPropertyChanged("Truncated"); } } }

    private Int64 _ApprovalId;
    /// <summary>关联审批编号</summary>
    [DisplayName("关联审批编号")]
    [Description("关联审批编号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ApprovalId", "关联审批编号", "")]
    public Int64 ApprovalId { get => _ApprovalId; set { if (OnPropertyChanging("ApprovalId", value)) { _ApprovalId = value; OnPropertyChanged("ApprovalId"); } } }

    private DateTime _CreateTime;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
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
            "Kind" => _Kind,
            "ConnectionId" => _ConnectionId,
            "SqlRedacted" => _SqlRedacted,
            "Decision" => _Decision,
            "Detail" => _Detail,
            "Source" => _Source,
            "SessionId" => _SessionId,
            "Status" => _Status,
            "Code" => _Code,
            "SqlKind" => _SqlKind,
            "RowCount" => _RowCount,
            "Truncated" => _Truncated,
            "ApprovalId" => _ApprovalId,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "ConnectionId": _ConnectionId = Convert.ToString(value); break;
                case "SqlRedacted": _SqlRedacted = Convert.ToString(value); break;
                case "Decision": _Decision = Convert.ToString(value); break;
                case "Detail": _Detail = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "SqlKind": _SqlKind = Convert.ToString(value); break;
                case "RowCount": _RowCount = value.ToLong(); break;
                case "Truncated": _Truncated = value.ToBoolean(); break;
                case "ApprovalId": _ApprovalId = value.ToLong(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static DbAuditLog FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据conn_create_conn_update_conn_delete_conn_test_query_classify_approval_*_write_result_rejected_permission_mode查找</summary>
    /// <param name="kind">conn_create_conn_update_conn_delete_conn_test_query_classify_approval_*_write_result_rejected_permission_mode</param>
    /// <returns>实体列表</returns>
    public static IList<DbAuditLog> FindAllByKind(String kind)
    {
        if (kind.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Kind.EqualIgnoreCase(kind));

        return FindAll(_.Kind == kind);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="kind">conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode</param>
    /// <param name="truncated">结果是否被 Agent 上限截断</param>
    /// <param name="start">时间开始</param>
    /// <param name="end">时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DbAuditLog> Search(String kind, Boolean? truncated, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
        if (truncated != null) exp &= _.Truncated == truncated;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得审计日志（脱敏）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>连接标识</summary>
        public static readonly Field ConnectionId = FindByName("ConnectionId");

        /// <summary>脱敏后的 SQL</summary>
        public static readonly Field SqlRedacted = FindByName("SqlRedacted");

        /// <summary>决策 allow/ask/reject/approved 等</summary>
        public static readonly Field Decision = FindByName("Decision");

        /// <summary>详情（脱敏）</summary>
        public static readonly Field Detail = FindByName("Detail");

        /// <summary>来源 ui/agent</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>Agent 会话标识（仅 agent）</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>结果状态 ok/awaiting_approval/rejected/error（kind=agent 摘要用）</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>错误码（kind=agent 摘要用）</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>语句类别 read/write（kind=agent 摘要用）</summary>
        public static readonly Field SqlKind = FindByName("SqlKind");

        /// <summary>返回行数或影响行数</summary>
        public static readonly Field RowCount = FindByName("RowCount");

        /// <summary>结果是否被 Agent 上限截断</summary>
        public static readonly Field Truncated = FindByName("Truncated");

        /// <summary>关联审批编号</summary>
        public static readonly Field ApprovalId = FindByName("ApprovalId");

        /// <summary>时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得审计日志（脱敏）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>conn_create/conn_update/conn_delete/conn_test/query/classify/approval_*/write_result/rejected/permission_mode</summary>
        public const String Kind = "Kind";

        /// <summary>连接标识</summary>
        public const String ConnectionId = "ConnectionId";

        /// <summary>脱敏后的 SQL</summary>
        public const String SqlRedacted = "SqlRedacted";

        /// <summary>决策 allow/ask/reject/approved 等</summary>
        public const String Decision = "Decision";

        /// <summary>详情（脱敏）</summary>
        public const String Detail = "Detail";

        /// <summary>来源 ui/agent</summary>
        public const String Source = "Source";

        /// <summary>Agent 会话标识（仅 agent）</summary>
        public const String SessionId = "SessionId";

        /// <summary>结果状态 ok/awaiting_approval/rejected/error（kind=agent 摘要用）</summary>
        public const String Status = "Status";

        /// <summary>错误码（kind=agent 摘要用）</summary>
        public const String Code = "Code";

        /// <summary>语句类别 read/write（kind=agent 摘要用）</summary>
        public const String SqlKind = "SqlKind";

        /// <summary>返回行数或影响行数</summary>
        public const String RowCount = "RowCount";

        /// <summary>结果是否被 Agent 上限截断</summary>
        public const String Truncated = "Truncated";

        /// <summary>关联审批编号</summary>
        public const String ApprovalId = "ApprovalId";

        /// <summary>时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
