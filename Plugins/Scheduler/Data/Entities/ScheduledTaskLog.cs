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

namespace ForgeSelf.Api.Plugins.Scheduler.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_ScheduledTaskLog_TaskId", false, "TaskId")]
[BindIndex("IX_ScheduledTaskLog_StartTime", false, "StartTime")]
[BindTable("ScheduledTaskLog", Description = "计划任务日志", ConnName = "Scheduler", DbType = DatabaseType.None)]
public partial class ScheduledTaskLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>日志ID</summary>
    [DisplayName("日志ID")]
    [Description("日志ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "日志ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _TaskId;
    /// <summary>任务ID</summary>
    [DisplayName("任务ID")]
    [Description("任务ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TaskId", "任务ID", "")]
    public Int64 TaskId { get => _TaskId; set { if (OnPropertyChanging("TaskId", value)) { _TaskId = value; OnPropertyChanged("TaskId"); } } }

    private DateTime _StartTime;
    /// <summary>开始时间</summary>
    [DisplayName("开始时间")]
    [Description("开始时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartTime", "开始时间", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private DateTime _EndTime;
    /// <summary>结束时间</summary>
    [DisplayName("结束时间")]
    [Description("结束时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("EndTime", "结束时间", "")]
    public DateTime EndTime { get => _EndTime; set { if (OnPropertyChanging("EndTime", value)) { _EndTime = value; OnPropertyChanged("EndTime"); } } }

    private Int32 _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _ResultMessage;
    /// <summary>结果消息</summary>
    [DisplayName("结果消息")]
    [Description("结果消息")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ResultMessage", "结果消息", "")]
    public String ResultMessage { get => _ResultMessage; set { if (OnPropertyChanging("ResultMessage", value)) { _ResultMessage = value; OnPropertyChanged("ResultMessage"); } } }

    private String _ErrorMessage;
    /// <summary>错误信息</summary>
    [DisplayName("错误信息")]
    [Description("错误信息")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ErrorMessage", "错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

    private Double _DurationMs;
    /// <summary>持续时间（毫秒）</summary>
    [DisplayName("持续时间（毫秒）")]
    [Description("持续时间（毫秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "持续时间（毫秒）", "")]
    public Double DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }
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
            "TaskId" => _TaskId,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "Status" => _Status,
            "ResultMessage" => _ResultMessage,
            "ErrorMessage" => _ErrorMessage,
            "DurationMs" => _DurationMs,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "TaskId": _TaskId = value.ToLong(); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "Status": _Status = value.ToInt(); break;
                case "ResultMessage": _ResultMessage = Convert.ToString(value); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                case "DurationMs": _DurationMs = value.ToDouble(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据日志ID查找</summary>
    /// <param name="id">日志ID</param>
    /// <returns>实体对象</returns>
    public static ScheduledTaskLog FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据任务ID查找</summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>实体列表</returns>
    public static IList<ScheduledTaskLog> FindAllByTaskId(Int64 taskId)
    {
        if (taskId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TaskId == taskId);

        return FindAll(_.TaskId == taskId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="start">开始时间开始</param>
    /// <param name="end">开始时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ScheduledTaskLog> Search(Int64 taskId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (taskId >= 0) exp &= _.TaskId == taskId;
        exp &= _.StartTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得计划任务日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>日志ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>任务ID</summary>
        public static readonly Field TaskId = FindByName("TaskId");

        /// <summary>开始时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>结果消息</summary>
        public static readonly Field ResultMessage = FindByName("ResultMessage");

        /// <summary>错误信息</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        /// <summary>持续时间（毫秒）</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得计划任务日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>日志ID</summary>
        public const String Id = "Id";

        /// <summary>任务ID</summary>
        public const String TaskId = "TaskId";

        /// <summary>开始时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>结果消息</summary>
        public const String ResultMessage = "ResultMessage";

        /// <summary>错误信息</summary>
        public const String ErrorMessage = "ErrorMessage";

        /// <summary>持续时间（毫秒）</summary>
        public const String DurationMs = "DurationMs";
    }
    #endregion
}
