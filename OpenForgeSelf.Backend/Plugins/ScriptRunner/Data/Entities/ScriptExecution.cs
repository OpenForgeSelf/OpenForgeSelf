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

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_ScriptExecution_ScriptId", false, "ScriptId")]
[BindIndex("IX_ScriptExecution_StartTime", false, "StartTime")]
[BindIndex("IX_ScriptExecution_Status", false, "Status")]
[BindTable("ScriptExecution", Description = "脚本执行记录", ConnName = "ScriptRunner", DbType = DatabaseType.None)]
public partial class ScriptExecution
{
    #region 属性
    private Int64 _Id;
    /// <summary>执行ID</summary>
    [DisplayName("执行ID")]
    [Description("执行ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "执行ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ScriptId;
    /// <summary>脚本ID</summary>
    [DisplayName("脚本ID")]
    [Description("脚本ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ScriptId", "脚本ID", "")]
    public Int64 ScriptId { get => _ScriptId; set { if (OnPropertyChanging("ScriptId", value)) { _ScriptId = value; OnPropertyChanged("ScriptId"); } } }

    private String _ScriptName;
    /// <summary>脚本名称</summary>
    [DisplayName("脚本名称")]
    [Description("脚本名称")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ScriptName", "脚本名称", "")]
    public String ScriptName { get => _ScriptName; set { if (OnPropertyChanging("ScriptName", value)) { _ScriptName = value; OnPropertyChanged("ScriptName"); } } }

    private Int32 _Status;
    /// <summary>状态</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

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

    private Int32 _ExitCode;
    /// <summary>退出码</summary>
    [DisplayName("退出码")]
    [Description("退出码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExitCode", "退出码", "")]
    public Int32 ExitCode { get => _ExitCode; set { if (OnPropertyChanging("ExitCode", value)) { _ExitCode = value; OnPropertyChanged("ExitCode"); } } }

    private String _Output;
    /// <summary>标准输出</summary>
    [DisplayName("标准输出")]
    [Description("标准输出")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("Output", "标准输出", "")]
    public String Output { get => _Output; set { if (OnPropertyChanging("Output", value)) { _Output = value; OnPropertyChanged("Output"); } } }

    private String _ErrorOutput;
    /// <summary>错误输出</summary>
    [DisplayName("错误输出")]
    [Description("错误输出")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ErrorOutput", "错误输出", "")]
    public String ErrorOutput { get => _ErrorOutput; set { if (OnPropertyChanging("ErrorOutput", value)) { _ErrorOutput = value; OnPropertyChanged("ErrorOutput"); } } }

    private Int64 _DurationMs;
    /// <summary>持续时间（毫秒）</summary>
    [DisplayName("持续时间（毫秒）")]
    [Description("持续时间（毫秒）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "持续时间（毫秒）", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private String _ParametersJson;
    /// <summary>参数JSON</summary>
    [DisplayName("参数JSON")]
    [Description("参数JSON")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ParametersJson", "参数JSON", "")]
    public String ParametersJson { get => _ParametersJson; set { if (OnPropertyChanging("ParametersJson", value)) { _ParametersJson = value; OnPropertyChanged("ParametersJson"); } } }

    private String _OutputLogsJson;
    /// <summary>输出日志JSON</summary>
    [DisplayName("输出日志JSON")]
    [Description("输出日志JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("OutputLogsJson", "输出日志JSON", "")]
    public String OutputLogsJson { get => _OutputLogsJson; set { if (OnPropertyChanging("OutputLogsJson", value)) { _OutputLogsJson = value; OnPropertyChanged("OutputLogsJson"); } } }
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
            "ScriptId" => _ScriptId,
            "ScriptName" => _ScriptName,
            "Status" => _Status,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "ExitCode" => _ExitCode,
            "Output" => _Output,
            "ErrorOutput" => _ErrorOutput,
            "DurationMs" => _DurationMs,
            "ParametersJson" => _ParametersJson,
            "OutputLogsJson" => _OutputLogsJson,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ScriptId": _ScriptId = value.ToLong(); break;
                case "ScriptName": _ScriptName = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "ExitCode": _ExitCode = value.ToInt(); break;
                case "Output": _Output = Convert.ToString(value); break;
                case "ErrorOutput": _ErrorOutput = Convert.ToString(value); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "ParametersJson": _ParametersJson = Convert.ToString(value); break;
                case "OutputLogsJson": _OutputLogsJson = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据执行ID查找</summary>
    /// <param name="id">执行ID</param>
    /// <returns>实体对象</returns>
    public static ScriptExecution FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据脚本ID查找</summary>
    /// <param name="scriptId">脚本ID</param>
    /// <returns>实体列表</returns>
    public static IList<ScriptExecution> FindAllByScriptId(Int64 scriptId)
    {
        if (scriptId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ScriptId == scriptId);

        return FindAll(_.ScriptId == scriptId);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<ScriptExecution> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="scriptId">脚本ID</param>
    /// <param name="status">状态</param>
    /// <param name="start">开始时间开始</param>
    /// <param name="end">开始时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ScriptExecution> Search(Int64 scriptId, Int32 status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (scriptId >= 0) exp &= _.ScriptId == scriptId;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.StartTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得脚本执行记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>执行ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>脚本ID</summary>
        public static readonly Field ScriptId = FindByName("ScriptId");

        /// <summary>脚本名称</summary>
        public static readonly Field ScriptName = FindByName("ScriptName");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>开始时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>退出码</summary>
        public static readonly Field ExitCode = FindByName("ExitCode");

        /// <summary>标准输出</summary>
        public static readonly Field Output = FindByName("Output");

        /// <summary>错误输出</summary>
        public static readonly Field ErrorOutput = FindByName("ErrorOutput");

        /// <summary>持续时间（毫秒）</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>参数JSON</summary>
        public static readonly Field ParametersJson = FindByName("ParametersJson");

        /// <summary>输出日志JSON</summary>
        public static readonly Field OutputLogsJson = FindByName("OutputLogsJson");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得脚本执行记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>执行ID</summary>
        public const String Id = "Id";

        /// <summary>脚本ID</summary>
        public const String ScriptId = "ScriptId";

        /// <summary>脚本名称</summary>
        public const String ScriptName = "ScriptName";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>开始时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>退出码</summary>
        public const String ExitCode = "ExitCode";

        /// <summary>标准输出</summary>
        public const String Output = "Output";

        /// <summary>错误输出</summary>
        public const String ErrorOutput = "ErrorOutput";

        /// <summary>持续时间（毫秒）</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>参数JSON</summary>
        public const String ParametersJson = "ParametersJson";

        /// <summary>输出日志JSON</summary>
        public const String OutputLogsJson = "OutputLogsJson";
    }
    #endregion
}
