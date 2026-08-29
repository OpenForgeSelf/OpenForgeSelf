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

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_UpdateTrace_UpdateTime", false, "UpdateTime")]
[BindIndex("IX_UpdateTrace_Result", false, "Result")]
[BindTable("UpdateTrace", Description = "更新操作记录", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class UpdateTrace : IUpdateTraceModel, IEntity<IUpdateTraceModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "主键", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _PreVersion;
    /// <summary>更新前版本号</summary>
    [DisplayName("更新前版本号")]
    [Description("更新前版本号")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("PreVersion", "更新前版本号", "")]
    public String PreVersion { get => _PreVersion; set { if (OnPropertyChanging("PreVersion", value)) { _PreVersion = value; OnPropertyChanged("PreVersion"); } } }

    private String _PostVersion;
    /// <summary>更新后版本号</summary>
    [DisplayName("更新后版本号")]
    [Description("更新后版本号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PostVersion", "更新后版本号", "")]
    public String PostVersion { get => _PostVersion; set { if (OnPropertyChanging("PostVersion", value)) { _PostVersion = value; OnPropertyChanged("PostVersion"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新操作时间</summary>
    [DisplayName("更新操作时间")]
    [Description("更新操作时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "更新操作时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private Int32 _Result;
    /// <summary>结果状态：0=成功, 1=失败, 2=回滚</summary>
    [DisplayName("结果状态")]
    [Description("结果状态：0=成功, 1=失败, 2=回滚")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Result", "结果状态：0=成功, 1=失败, 2=回滚", "")]
    public Int32 Result { get => _Result; set { if (OnPropertyChanging("Result", value)) { _Result = value; OnPropertyChanged("Result"); } } }

    private String _ErrorMessage;
    /// <summary>失败时的错误信息</summary>
    [DisplayName("失败时的错误信息")]
    [Description("失败时的错误信息")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ErrorMessage", "失败时的错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

    private String _RollbackVersion;
    /// <summary>回滚到的版本号</summary>
    [DisplayName("回滚到的版本号")]
    [Description("回滚到的版本号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("RollbackVersion", "回滚到的版本号", "")]
    public String RollbackVersion { get => _RollbackVersion; set { if (OnPropertyChanging("RollbackVersion", value)) { _RollbackVersion = value; OnPropertyChanged("RollbackVersion"); } } }

    private String _DownloadUrl;
    /// <summary>更新包下载地址</summary>
    [DisplayName("更新包下载地址")]
    [Description("更新包下载地址")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("DownloadUrl", "更新包下载地址", "")]
    public String DownloadUrl { get => _DownloadUrl; set { if (OnPropertyChanging("DownloadUrl", value)) { _DownloadUrl = value; OnPropertyChanged("DownloadUrl"); } } }

    private Int64 _PackageSize;
    /// <summary>更新包大小</summary>
    [DisplayName("更新包大小")]
    [Description("更新包大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PackageSize", "更新包大小", "")]
    public Int64 PackageSize { get => _PackageSize; set { if (OnPropertyChanging("PackageSize", value)) { _PackageSize = value; OnPropertyChanged("PackageSize"); } } }

    private Int64 _DurationMs;
    /// <summary>更新流程耗时(毫秒)</summary>
    [DisplayName("更新流程耗时(毫秒)")]
    [Description("更新流程耗时(毫秒)")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "更新流程耗时(毫秒)", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUpdateTraceModel model)
    {
        Id = model.Id;
        PreVersion = model.PreVersion;
        PostVersion = model.PostVersion;
        UpdateTime = model.UpdateTime;
        Result = model.Result;
        ErrorMessage = model.ErrorMessage;
        RollbackVersion = model.RollbackVersion;
        DownloadUrl = model.DownloadUrl;
        PackageSize = model.PackageSize;
        DurationMs = model.DurationMs;
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
            "PreVersion" => _PreVersion,
            "PostVersion" => _PostVersion,
            "UpdateTime" => _UpdateTime,
            "Result" => _Result,
            "ErrorMessage" => _ErrorMessage,
            "RollbackVersion" => _RollbackVersion,
            "DownloadUrl" => _DownloadUrl,
            "PackageSize" => _PackageSize,
            "DurationMs" => _DurationMs,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "PreVersion": _PreVersion = Convert.ToString(value); break;
                case "PostVersion": _PostVersion = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Result": _Result = value.ToInt(); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                case "RollbackVersion": _RollbackVersion = Convert.ToString(value); break;
                case "DownloadUrl": _DownloadUrl = Convert.ToString(value); break;
                case "PackageSize": _PackageSize = value.ToLong(); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static UpdateTrace FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据结果状态查找</summary>
    /// <param name="result">结果状态</param>
    /// <returns>实体列表</returns>
    public static IList<UpdateTrace> FindAllByResult(Int32 result)
    {
        if (result < 0) return [];

        return FindAll(_.Result == result);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="updateTime">更新操作时间</param>
    /// <param name="result">结果状态：0=成功, 1=失败, 2=回滚</param>
    /// <param name="start">主键开始</param>
    /// <param name="end">主键结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UpdateTrace> Search(DateTime updateTime, Int32 result, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (result >= 0) exp &= _.Result == result;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得更新操作记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>更新前版本号</summary>
        public static readonly Field PreVersion = FindByName("PreVersion");

        /// <summary>更新后版本号</summary>
        public static readonly Field PostVersion = FindByName("PostVersion");

        /// <summary>更新操作时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>结果状态：0=成功, 1=失败, 2=回滚</summary>
        public static readonly Field Result = FindByName("Result");

        /// <summary>失败时的错误信息</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        /// <summary>回滚到的版本号</summary>
        public static readonly Field RollbackVersion = FindByName("RollbackVersion");

        /// <summary>更新包下载地址</summary>
        public static readonly Field DownloadUrl = FindByName("DownloadUrl");

        /// <summary>更新包大小</summary>
        public static readonly Field PackageSize = FindByName("PackageSize");

        /// <summary>更新流程耗时(毫秒)</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得更新操作记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>更新前版本号</summary>
        public const String PreVersion = "PreVersion";

        /// <summary>更新后版本号</summary>
        public const String PostVersion = "PostVersion";

        /// <summary>更新操作时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>结果状态：0=成功, 1=失败, 2=回滚</summary>
        public const String Result = "Result";

        /// <summary>失败时的错误信息</summary>
        public const String ErrorMessage = "ErrorMessage";

        /// <summary>回滚到的版本号</summary>
        public const String RollbackVersion = "RollbackVersion";

        /// <summary>更新包下载地址</summary>
        public const String DownloadUrl = "DownloadUrl";

        /// <summary>更新包大小</summary>
        public const String PackageSize = "PackageSize";

        /// <summary>更新流程耗时(毫秒)</summary>
        public const String DurationMs = "DurationMs";
    }
    #endregion
}
