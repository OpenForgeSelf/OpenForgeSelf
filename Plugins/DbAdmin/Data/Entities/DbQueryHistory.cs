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

/// <summary>{name}。SQL 执行历史（仅存脱敏 SQL）</summary>
[Serializable]
[DataObject]
[Description("{name}。SQL 执行历史（仅存脱敏 SQL）")]
[BindIndex("IX_DbQueryHistory_CreateTime", false, "CreateTime")]
[BindIndex("IX_DbQueryHistory_ConnectionId", false, "ConnectionId")]
[BindTable("DbQueryHistory", Description = "SQL 执行历史（仅存脱敏 SQL）", ConnName = "DbAdmin", DbType = DatabaseType.None)]
public partial class DbQueryHistory
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
    /// <summary>连接标识（用户连接为数字Id，系统连接为 sys:连接名）</summary>
    [DisplayName("连接标识（用户连接为数字Id")]
    [Description("连接标识（用户连接为数字Id，系统连接为 sys:连接名）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ConnectionId", "连接标识（用户连接为数字Id，系统连接为 sys:连接名）", "")]
    public String ConnectionId { get => _ConnectionId; set { if (OnPropertyChanging("ConnectionId", value)) { _ConnectionId = value; OnPropertyChanged("ConnectionId"); } } }

    private String _SqlRedacted;
    /// <summary>脱敏后的 SQL</summary>
    [DisplayName("脱敏后的SQL")]
    [Description("脱敏后的 SQL")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("SqlRedacted", "脱敏后的 SQL", "", Master = true)]
    public String SqlRedacted { get => _SqlRedacted; set { if (OnPropertyChanging("SqlRedacted", value)) { _SqlRedacted = value; OnPropertyChanged("SqlRedacted"); } } }

    private String _Kind;
    /// <summary>read/write</summary>
    [DisplayName("read_write")]
    [Description("read/write")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Kind", "read/write", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Int64 _RowCount;
    /// <summary>返回或影响行数</summary>
    [DisplayName("返回或影响行数")]
    [Description("返回或影响行数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RowCount", "返回或影响行数", "")]
    public Int64 RowCount { get => _RowCount; set { if (OnPropertyChanging("RowCount", value)) { _RowCount = value; OnPropertyChanged("RowCount"); } } }

    private Int64 _ElapsedMs;
    /// <summary>耗时毫秒</summary>
    [DisplayName("耗时毫秒")]
    [Description("耗时毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时毫秒", "")]
    public Int64 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private Boolean _Success;
    /// <summary>是否成功</summary>
    [DisplayName("是否成功")]
    [Description("是否成功")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Success", "是否成功", "")]
    public Boolean Success { get => _Success; set { if (OnPropertyChanging("Success", value)) { _Success = value; OnPropertyChanged("Success"); } } }

    private String _Error;
    /// <summary>错误（脱敏）</summary>
    [DisplayName("错误（脱敏）")]
    [Description("错误（脱敏）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Error", "错误（脱敏）", "")]
    public String Error { get => _Error; set { if (OnPropertyChanging("Error", value)) { _Error = value; OnPropertyChanged("Error"); } } }

    private String _Source;
    /// <summary>ui/agent</summary>
    [DisplayName("ui_agent")]
    [Description("ui/agent")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Source", "ui/agent", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

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
            "ConnectionId" => _ConnectionId,
            "SqlRedacted" => _SqlRedacted,
            "Kind" => _Kind,
            "RowCount" => _RowCount,
            "ElapsedMs" => _ElapsedMs,
            "Success" => _Success,
            "Error" => _Error,
            "Source" => _Source,
            "CreateTime" => _CreateTime,
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
                case "RowCount": _RowCount = value.ToLong(); break;
                case "ElapsedMs": _ElapsedMs = value.ToLong(); break;
                case "Success": _Success = value.ToBoolean(); break;
                case "Error": _Error = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
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
    public static DbQueryHistory FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据连接标识（用户连接为数字Id查找</summary>
    /// <param name="connectionId">连接标识（用户连接为数字Id</param>
    /// <returns>实体列表</returns>
    public static IList<DbQueryHistory> FindAllByConnectionId(String connectionId)
    {
        if (connectionId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ConnectionId.EqualIgnoreCase(connectionId));

        return FindAll(_.ConnectionId == connectionId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="connectionId">连接标识（用户连接为数字Id，系统连接为 sys:连接名）</param>
    /// <param name="success">是否成功</param>
    /// <param name="start">时间开始</param>
    /// <param name="end">时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DbQueryHistory> Search(String connectionId, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!connectionId.IsNullOrEmpty()) exp &= _.ConnectionId == connectionId;
        if (success != null) exp &= _.Success == success;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得SQL执行历史（仅存脱敏SQL）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>连接标识（用户连接为数字Id，系统连接为 sys:连接名）</summary>
        public static readonly Field ConnectionId = FindByName("ConnectionId");

        /// <summary>脱敏后的 SQL</summary>
        public static readonly Field SqlRedacted = FindByName("SqlRedacted");

        /// <summary>read/write</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>返回或影响行数</summary>
        public static readonly Field RowCount = FindByName("RowCount");

        /// <summary>耗时毫秒</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>是否成功</summary>
        public static readonly Field Success = FindByName("Success");

        /// <summary>错误（脱敏）</summary>
        public static readonly Field Error = FindByName("Error");

        /// <summary>ui/agent</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得SQL执行历史（仅存脱敏SQL）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>连接标识（用户连接为数字Id，系统连接为 sys:连接名）</summary>
        public const String ConnectionId = "ConnectionId";

        /// <summary>脱敏后的 SQL</summary>
        public const String SqlRedacted = "SqlRedacted";

        /// <summary>read/write</summary>
        public const String Kind = "Kind";

        /// <summary>返回或影响行数</summary>
        public const String RowCount = "RowCount";

        /// <summary>耗时毫秒</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>是否成功</summary>
        public const String Success = "Success";

        /// <summary>错误（脱敏）</summary>
        public const String Error = "Error";

        /// <summary>ui/agent</summary>
        public const String Source = "Source";

        /// <summary>时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
