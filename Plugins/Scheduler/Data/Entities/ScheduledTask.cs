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
[BindIndex("IX_ScheduledTask_Status", false, "Status")]
[BindIndex("IX_ScheduledTask_NextRunTime", false, "NextRunTime")]
[BindTable("ScheduledTask", Description = "计划任务", ConnName = "Scheduler", DbType = DatabaseType.None)]
public partial class ScheduledTask
{
    #region 属性
    private Int64 _Id;
    /// <summary>任务ID</summary>
    [DisplayName("任务ID")]
    [Description("任务ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "任务ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>任务名称</summary>
    [DisplayName("任务名称")]
    [Description("任务名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "任务名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private Int32 _TaskType;
    /// <summary>任务类型</summary>
    [DisplayName("任务类型")]
    [Description("任务类型")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TaskType", "任务类型", "")]
    public Int32 TaskType { get => _TaskType; set { if (OnPropertyChanging("TaskType", value)) { _TaskType = value; OnPropertyChanged("TaskType"); } } }

    private String _TargetId;
    /// <summary>目标ID</summary>
    [DisplayName("目标ID")]
    [Description("目标ID")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("TargetId", "目标ID", "")]
    public String TargetId { get => _TargetId; set { if (OnPropertyChanging("TargetId", value)) { _TargetId = value; OnPropertyChanged("TargetId"); } } }

    private Int32 _ScheduleType;
    /// <summary>调度类型</summary>
    [DisplayName("调度类型")]
    [Description("调度类型")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ScheduleType", "调度类型", "")]
    public Int32 ScheduleType { get => _ScheduleType; set { if (OnPropertyChanging("ScheduleType", value)) { _ScheduleType = value; OnPropertyChanged("ScheduleType"); } } }

    private String _CronExpression;
    /// <summary>Cron表达式</summary>
    [DisplayName("Cron表达式")]
    [Description("Cron表达式")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CronExpression", "Cron表达式", "")]
    public String CronExpression { get => _CronExpression; set { if (OnPropertyChanging("CronExpression", value)) { _CronExpression = value; OnPropertyChanged("CronExpression"); } } }

    private Int32 _IntervalMinutes;
    /// <summary>间隔分钟</summary>
    [DisplayName("间隔分钟")]
    [Description("间隔分钟")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IntervalMinutes", "间隔分钟", "")]
    public Int32 IntervalMinutes { get => _IntervalMinutes; set { if (OnPropertyChanging("IntervalMinutes", value)) { _IntervalMinutes = value; OnPropertyChanged("IntervalMinutes"); } } }

    private DateTime _RunAt;
    /// <summary>执行时间</summary>
    [DisplayName("执行时间")]
    [Description("执行时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RunAt", "执行时间", "")]
    public DateTime RunAt { get => _RunAt; set { if (OnPropertyChanging("RunAt", value)) { _RunAt = value; OnPropertyChanged("RunAt"); } } }

    private String _WeekDays;
    /// <summary>星期</summary>
    [DisplayName("星期")]
    [Description("星期")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("WeekDays", "星期", "")]
    public String WeekDays { get => _WeekDays; set { if (OnPropertyChanging("WeekDays", value)) { _WeekDays = value; OnPropertyChanged("WeekDays"); } } }

    private Int32 _DayOfMonth;
    /// <summary>每月几号</summary>
    [DisplayName("每月几号")]
    [Description("每月几号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DayOfMonth", "每月几号", "")]
    public Int32 DayOfMonth { get => _DayOfMonth; set { if (OnPropertyChanging("DayOfMonth", value)) { _DayOfMonth = value; OnPropertyChanged("DayOfMonth"); } } }

    private String _TimeOfDay;
    /// <summary>每天时间</summary>
    [DisplayName("每天时间")]
    [Description("每天时间")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TimeOfDay", "每天时间", "")]
    public String TimeOfDay { get => _TimeOfDay; set { if (OnPropertyChanging("TimeOfDay", value)) { _TimeOfDay = value; OnPropertyChanged("TimeOfDay"); } } }

    private String _TimeZone;
    /// <summary>时区</summary>
    [DisplayName("时区")]
    [Description("时区")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("TimeZone", "时区", "")]
    public String TimeZone { get => _TimeZone; set { if (OnPropertyChanging("TimeZone", value)) { _TimeZone = value; OnPropertyChanged("TimeZone"); } } }

    private Int32 _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _LastRunTime;
    /// <summary>最后执行时间</summary>
    [DisplayName("最后执行时间")]
    [Description("最后执行时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastRunTime", "最后执行时间", "")]
    public DateTime LastRunTime { get => _LastRunTime; set { if (OnPropertyChanging("LastRunTime", value)) { _LastRunTime = value; OnPropertyChanged("LastRunTime"); } } }

    private DateTime _NextRunTime;
    /// <summary>下次执行时间</summary>
    [DisplayName("下次执行时间")]
    [Description("下次执行时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("NextRunTime", "下次执行时间", "")]
    public DateTime NextRunTime { get => _NextRunTime; set { if (OnPropertyChanging("NextRunTime", value)) { _NextRunTime = value; OnPropertyChanged("NextRunTime"); } } }

    private Int32 _RunCount;
    /// <summary>执行次数</summary>
    [DisplayName("执行次数")]
    [Description("执行次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RunCount", "执行次数", "")]
    public Int32 RunCount { get => _RunCount; set { if (OnPropertyChanging("RunCount", value)) { _RunCount = value; OnPropertyChanged("RunCount"); } } }

    private Int32 _FailureCount;
    /// <summary>失败次数</summary>
    [DisplayName("失败次数")]
    [Description("失败次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FailureCount", "失败次数", "")]
    public Int32 FailureCount { get => _FailureCount; set { if (OnPropertyChanging("FailureCount", value)) { _FailureCount = value; OnPropertyChanged("FailureCount"); } } }

    private String _InputParameters;
    /// <summary>输入参数</summary>
    [DisplayName("输入参数")]
    [Description("输入参数")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("InputParameters", "输入参数", "")]
    public String InputParameters { get => _InputParameters; set { if (OnPropertyChanging("InputParameters", value)) { _InputParameters = value; OnPropertyChanged("InputParameters"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
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
            "Name" => _Name,
            "Description" => _Description,
            "TaskType" => _TaskType,
            "TargetId" => _TargetId,
            "ScheduleType" => _ScheduleType,
            "CronExpression" => _CronExpression,
            "IntervalMinutes" => _IntervalMinutes,
            "RunAt" => _RunAt,
            "WeekDays" => _WeekDays,
            "DayOfMonth" => _DayOfMonth,
            "TimeOfDay" => _TimeOfDay,
            "TimeZone" => _TimeZone,
            "Status" => _Status,
            "LastRunTime" => _LastRunTime,
            "NextRunTime" => _NextRunTime,
            "RunCount" => _RunCount,
            "FailureCount" => _FailureCount,
            "InputParameters" => _InputParameters,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "TaskType": _TaskType = value.ToInt(); break;
                case "TargetId": _TargetId = Convert.ToString(value); break;
                case "ScheduleType": _ScheduleType = value.ToInt(); break;
                case "CronExpression": _CronExpression = Convert.ToString(value); break;
                case "IntervalMinutes": _IntervalMinutes = value.ToInt(); break;
                case "RunAt": _RunAt = value.ToDateTime(); break;
                case "WeekDays": _WeekDays = Convert.ToString(value); break;
                case "DayOfMonth": _DayOfMonth = value.ToInt(); break;
                case "TimeOfDay": _TimeOfDay = Convert.ToString(value); break;
                case "TimeZone": _TimeZone = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "LastRunTime": _LastRunTime = value.ToDateTime(); break;
                case "NextRunTime": _NextRunTime = value.ToDateTime(); break;
                case "RunCount": _RunCount = value.ToInt(); break;
                case "FailureCount": _FailureCount = value.ToInt(); break;
                case "InputParameters": _InputParameters = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据任务ID查找</summary>
    /// <param name="id">任务ID</param>
    /// <returns>实体对象</returns>
    public static ScheduledTask FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<ScheduledTask> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="status">状态</param>
    /// <param name="start">下次执行时间开始</param>
    /// <param name="end">下次执行时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ScheduledTask> Search(Int32 status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (status >= 0) exp &= _.Status == status;
        exp &= _.NextRunTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得计划任务字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>任务ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>任务名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>任务类型</summary>
        public static readonly Field TaskType = FindByName("TaskType");

        /// <summary>目标ID</summary>
        public static readonly Field TargetId = FindByName("TargetId");

        /// <summary>调度类型</summary>
        public static readonly Field ScheduleType = FindByName("ScheduleType");

        /// <summary>Cron表达式</summary>
        public static readonly Field CronExpression = FindByName("CronExpression");

        /// <summary>间隔分钟</summary>
        public static readonly Field IntervalMinutes = FindByName("IntervalMinutes");

        /// <summary>执行时间</summary>
        public static readonly Field RunAt = FindByName("RunAt");

        /// <summary>星期</summary>
        public static readonly Field WeekDays = FindByName("WeekDays");

        /// <summary>每月几号</summary>
        public static readonly Field DayOfMonth = FindByName("DayOfMonth");

        /// <summary>每天时间</summary>
        public static readonly Field TimeOfDay = FindByName("TimeOfDay");

        /// <summary>时区</summary>
        public static readonly Field TimeZone = FindByName("TimeZone");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>最后执行时间</summary>
        public static readonly Field LastRunTime = FindByName("LastRunTime");

        /// <summary>下次执行时间</summary>
        public static readonly Field NextRunTime = FindByName("NextRunTime");

        /// <summary>执行次数</summary>
        public static readonly Field RunCount = FindByName("RunCount");

        /// <summary>失败次数</summary>
        public static readonly Field FailureCount = FindByName("FailureCount");

        /// <summary>输入参数</summary>
        public static readonly Field InputParameters = FindByName("InputParameters");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得计划任务字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>任务ID</summary>
        public const String Id = "Id";

        /// <summary>任务名称</summary>
        public const String Name = "Name";

        /// <summary>描述</summary>
        public const String Description = "Description";

        /// <summary>任务类型</summary>
        public const String TaskType = "TaskType";

        /// <summary>目标ID</summary>
        public const String TargetId = "TargetId";

        /// <summary>调度类型</summary>
        public const String ScheduleType = "ScheduleType";

        /// <summary>Cron表达式</summary>
        public const String CronExpression = "CronExpression";

        /// <summary>间隔分钟</summary>
        public const String IntervalMinutes = "IntervalMinutes";

        /// <summary>执行时间</summary>
        public const String RunAt = "RunAt";

        /// <summary>星期</summary>
        public const String WeekDays = "WeekDays";

        /// <summary>每月几号</summary>
        public const String DayOfMonth = "DayOfMonth";

        /// <summary>每天时间</summary>
        public const String TimeOfDay = "TimeOfDay";

        /// <summary>时区</summary>
        public const String TimeZone = "TimeZone";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>最后执行时间</summary>
        public const String LastRunTime = "LastRunTime";

        /// <summary>下次执行时间</summary>
        public const String NextRunTime = "NextRunTime";

        /// <summary>执行次数</summary>
        public const String RunCount = "RunCount";

        /// <summary>失败次数</summary>
        public const String FailureCount = "FailureCount";

        /// <summary>输入参数</summary>
        public const String InputParameters = "InputParameters";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
