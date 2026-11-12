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

/// <summary>{name}。写操作审批（原始 SQL 仅内存持有）</summary>
[Serializable]
[DataObject]
[Description("{name}。写操作审批（原始 SQL 仅内存持有）")]
[BindIndex("IX_DbApproval_Status", false, "Status")]
[BindIndex("IX_DbApproval_CreateTime", false, "CreateTime")]
[BindTable("DbApproval", Description = "写操作审批（原始 SQL 仅内存持有）", ConnName = "DbAdmin", DbType = DatabaseType.None)]
public partial class DbApproval
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

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
    [BindColumn("SqlRedacted", "脱敏后的 SQL", "", Master = true)]
    public String SqlRedacted { get => _SqlRedacted; set { if (OnPropertyChanging("SqlRedacted", value)) { _SqlRedacted = value; OnPropertyChanged("SqlRedacted"); } } }

    private String _Kind;
    /// <summary>write/destructive</summary>
    [DisplayName("write_destructive")]
    [Description("write/destructive")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Kind", "write/destructive", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Boolean _Destructive;
    /// <summary>是否破坏性（需二次确认）</summary>
    [DisplayName("是否破坏性（需二次确认）")]
    [Description("是否破坏性（需二次确认）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Destructive", "是否破坏性（需二次确认）", "")]
    public Boolean Destructive { get => _Destructive; set { if (OnPropertyChanging("Destructive", value)) { _Destructive = value; OnPropertyChanged("Destructive"); } } }

    private String _Status;
    /// <summary>pending/approved/rejected/expired</summary>
    [DisplayName("pending_approved_rejected_expired")]
    [Description("pending/approved/rejected/expired")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Status", "pending/approved/rejected/expired", "", DefaultValue = "pending")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _Source;
    /// <summary>ui/agent</summary>
    [DisplayName("ui_agent")]
    [Description("ui/agent")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Source", "ui/agent", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _ResolveTime;
    /// <summary>处理时间</summary>
    [DisplayName("处理时间")]
    [Description("处理时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ResolveTime", "处理时间", "")]
    public DateTime ResolveTime { get => _ResolveTime; set { if (OnPropertyChanging("ResolveTime", value)) { _ResolveTime = value; OnPropertyChanged("ResolveTime"); } } }
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
            "ConnectionId" => _ConnectionId,
            "SqlRedacted" => _SqlRedacted,
            "Kind" => _Kind,
            "Destructive" => _Destructive,
            "Status" => _Status,
            "Source" => _Source,
            "CreateTime" => _CreateTime,
            "ResolveTime" => _ResolveTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ConnectionId": _ConnectionId = Convert.ToString(value); break;
                case "SqlRedacted": _SqlRedacted = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Destructive": _Destructive = value.ToBoolean(); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "ResolveTime": _ResolveTime = value.ToDateTime(); break;
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
    public static DbApproval FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据pending_approved_rejected_expired查找</summary>
    /// <param name="status">pending_approved_rejected_expired</param>
    /// <returns>实体列表</returns>
    public static IList<DbApproval> FindAllByStatus(String status)
    {
        if (status.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Status.EqualIgnoreCase(status));

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="status">pending/approved/rejected/expired</param>
    /// <param name="destructive">是否破坏性（需二次确认）</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DbApproval> Search(String status, Boolean? destructive, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        if (destructive != null) exp &= _.Destructive == destructive;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得写操作审批（原始SQL仅内存持有）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>连接标识</summary>
        public static readonly Field ConnectionId = FindByName("ConnectionId");

        /// <summary>脱敏后的 SQL</summary>
        public static readonly Field SqlRedacted = FindByName("SqlRedacted");

        /// <summary>write/destructive</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>是否破坏性（需二次确认）</summary>
        public static readonly Field Destructive = FindByName("Destructive");

        /// <summary>pending/approved/rejected/expired</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>ui/agent</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>处理时间</summary>
        public static readonly Field ResolveTime = FindByName("ResolveTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得写操作审批（原始SQL仅内存持有）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>连接标识</summary>
        public const String ConnectionId = "ConnectionId";

        /// <summary>脱敏后的 SQL</summary>
        public const String SqlRedacted = "SqlRedacted";

        /// <summary>write/destructive</summary>
        public const String Kind = "Kind";

        /// <summary>是否破坏性（需二次确认）</summary>
        public const String Destructive = "Destructive";

        /// <summary>pending/approved/rejected/expired</summary>
        public const String Status = "Status";

        /// <summary>ui/agent</summary>
        public const String Source = "Source";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>处理时间</summary>
        public const String ResolveTime = "ResolveTime";
    }
    #endregion
}
