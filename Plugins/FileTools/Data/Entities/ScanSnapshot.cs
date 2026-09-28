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

namespace ForgeSelf.Api.Plugins.FileTools.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_ScanSnapshot_ScannedAt", false, "ScannedAt")]
[BindIndex("IX_ScanSnapshot_RootPath", false, "RootPath")]
[BindTable("ScanSnapshot", Description = "目录大小扫描快照", ConnName = "FileTools", DbType = DatabaseType.None)]
public partial class ScanSnapshot
{
    #region 属性
    private Int64 _Id;
    /// <summary>快照ID</summary>
    [DisplayName("快照ID")]
    [Description("快照ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "快照ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _RootPath;
    /// <summary>扫描根路径</summary>
    [DisplayName("扫描根路径")]
    [Description("扫描根路径")]
    [DataObjectField(false, false, false, 1000)]
    [BindColumn("RootPath", "扫描根路径", "", Master = true)]
    public String RootPath { get => _RootPath; set { if (OnPropertyChanging("RootPath", value)) { _RootPath = value; OnPropertyChanged("RootPath"); } } }

    private DateTime _ScannedAt;
    /// <summary>扫描完成时间</summary>
    [DisplayName("扫描完成时间")]
    [Description("扫描完成时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ScannedAt", "扫描完成时间", "")]
    public DateTime ScannedAt { get => _ScannedAt; set { if (OnPropertyChanging("ScannedAt", value)) { _ScannedAt = value; OnPropertyChanged("ScannedAt"); } } }

    private Int64 _DurationMs;
    /// <summary>扫描耗时毫秒</summary>
    [DisplayName("扫描耗时毫秒")]
    [Description("扫描耗时毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DurationMs", "扫描耗时毫秒", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private Int64 _RootTotalBytes;
    /// <summary>根目录总字节（含全部后代）</summary>
    [DisplayName("根目录总字节（含全部后代）")]
    [Description("根目录总字节（含全部后代）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RootTotalBytes", "根目录总字节（含全部后代）", "")]
    public Int64 RootTotalBytes { get => _RootTotalBytes; set { if (OnPropertyChanging("RootTotalBytes", value)) { _RootTotalBytes = value; OnPropertyChanged("RootTotalBytes"); } } }

    private Int64 _RootOwnBytes;
    /// <summary>根目录本级文件字节（不属于任何子目录）</summary>
    [DisplayName("根目录本级文件字节（不属于任何子目录）")]
    [Description("根目录本级文件字节（不属于任何子目录）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RootOwnBytes", "根目录本级文件字节（不属于任何子目录）", "")]
    public Int64 RootOwnBytes { get => _RootOwnBytes; set { if (OnPropertyChanging("RootOwnBytes", value)) { _RootOwnBytes = value; OnPropertyChanged("RootOwnBytes"); } } }

    private Int64 _DirectoryCount;
    /// <summary>计入目录数</summary>
    [DisplayName("计入目录数")]
    [Description("计入目录数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DirectoryCount", "计入目录数", "")]
    public Int64 DirectoryCount { get => _DirectoryCount; set { if (OnPropertyChanging("DirectoryCount", value)) { _DirectoryCount = value; OnPropertyChanged("DirectoryCount"); } } }

    private Int64 _ChildCount;
    /// <summary>已知直接子目录总数（用于还原被 Top 挤掉的其他行）</summary>
    [DisplayName("已知直接子目录总数（用于还原被Top挤掉的其他行）")]
    [Description("已知直接子目录总数（用于还原被 Top 挤掉的其他行）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ChildCount", "已知直接子目录总数（用于还原被 Top 挤掉的其他行）", "")]
    public Int64 ChildCount { get => _ChildCount; set { if (OnPropertyChanging("ChildCount", value)) { _ChildCount = value; OnPropertyChanged("ChildCount"); } } }

    private Int64 _FileCount;
    /// <summary>计入文件数</summary>
    [DisplayName("计入文件数")]
    [Description("计入文件数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FileCount", "计入文件数", "")]
    public Int64 FileCount { get => _FileCount; set { if (OnPropertyChanging("FileCount", value)) { _FileCount = value; OnPropertyChanged("FileCount"); } } }

    private Int32 _Top;
    /// <summary>保存的排行条数</summary>
    [DisplayName("保存的排行条数")]
    [Description("保存的排行条数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Top", "保存的排行条数", "")]
    public Int32 Top { get => _Top; set { if (OnPropertyChanging("Top", value)) { _Top = value; OnPropertyChanged("Top"); } } }

    private Boolean _Truncated;
    /// <summary>是否因条目上限被截断</summary>
    [DisplayName("是否因条目上限被截断")]
    [Description("是否因条目上限被截断")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Truncated", "是否因条目上限被截断", "")]
    public Boolean Truncated { get => _Truncated; set { if (OnPropertyChanging("Truncated", value)) { _Truncated = value; OnPropertyChanged("Truncated"); } } }

    private Int64 _InaccessibleCount;
    /// <summary>无权限条目数</summary>
    [DisplayName("无权限条目数")]
    [Description("无权限条目数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InaccessibleCount", "无权限条目数", "")]
    public Int64 InaccessibleCount { get => _InaccessibleCount; set { if (OnPropertyChanging("InaccessibleCount", value)) { _InaccessibleCount = value; OnPropertyChanged("InaccessibleCount"); } } }

    private Int64 _SkippedReparseCount;
    /// <summary>跳过的重解析点数</summary>
    [DisplayName("跳过的重解析点数")]
    [Description("跳过的重解析点数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SkippedReparseCount", "跳过的重解析点数", "")]
    public Int64 SkippedReparseCount { get => _SkippedReparseCount; set { if (OnPropertyChanging("SkippedReparseCount", value)) { _SkippedReparseCount = value; OnPropertyChanged("SkippedReparseCount"); } } }

    private String _Note;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Note", "备注", "")]
    public String Note { get => _Note; set { if (OnPropertyChanging("Note", value)) { _Note = value; OnPropertyChanged("Note"); } } }
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
            "RootPath" => _RootPath,
            "ScannedAt" => _ScannedAt,
            "DurationMs" => _DurationMs,
            "RootTotalBytes" => _RootTotalBytes,
            "RootOwnBytes" => _RootOwnBytes,
            "DirectoryCount" => _DirectoryCount,
            "ChildCount" => _ChildCount,
            "FileCount" => _FileCount,
            "Top" => _Top,
            "Truncated" => _Truncated,
            "InaccessibleCount" => _InaccessibleCount,
            "SkippedReparseCount" => _SkippedReparseCount,
            "Note" => _Note,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RootPath": _RootPath = Convert.ToString(value); break;
                case "ScannedAt": _ScannedAt = value.ToDateTime(); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "RootTotalBytes": _RootTotalBytes = value.ToLong(); break;
                case "RootOwnBytes": _RootOwnBytes = value.ToLong(); break;
                case "DirectoryCount": _DirectoryCount = value.ToLong(); break;
                case "ChildCount": _ChildCount = value.ToLong(); break;
                case "FileCount": _FileCount = value.ToLong(); break;
                case "Top": _Top = value.ToInt(); break;
                case "Truncated": _Truncated = value.ToBoolean(); break;
                case "InaccessibleCount": _InaccessibleCount = value.ToLong(); break;
                case "SkippedReparseCount": _SkippedReparseCount = value.ToLong(); break;
                case "Note": _Note = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据快照ID查找</summary>
    /// <param name="id">快照ID</param>
    /// <returns>实体对象</returns>
    public static ScanSnapshot FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据扫描根路径查找</summary>
    /// <param name="rootPath">扫描根路径</param>
    /// <returns>实体列表</returns>
    public static IList<ScanSnapshot> FindAllByRootPath(String rootPath)
    {
        if (rootPath.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.RootPath.EqualIgnoreCase(rootPath));

        return FindAll(_.RootPath == rootPath);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="truncated">是否因条目上限被截断</param>
    /// <param name="start">扫描完成时间开始</param>
    /// <param name="end">扫描完成时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ScanSnapshot> Search(Boolean? truncated, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (truncated != null) exp &= _.Truncated == truncated;
        exp &= _.ScannedAt.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得目录大小扫描快照字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>快照ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>扫描根路径</summary>
        public static readonly Field RootPath = FindByName("RootPath");

        /// <summary>扫描完成时间</summary>
        public static readonly Field ScannedAt = FindByName("ScannedAt");

        /// <summary>扫描耗时毫秒</summary>
        public static readonly Field DurationMs = FindByName("DurationMs");

        /// <summary>根目录总字节（含全部后代）</summary>
        public static readonly Field RootTotalBytes = FindByName("RootTotalBytes");

        /// <summary>根目录本级文件字节（不属于任何子目录）</summary>
        public static readonly Field RootOwnBytes = FindByName("RootOwnBytes");

        /// <summary>计入目录数</summary>
        public static readonly Field DirectoryCount = FindByName("DirectoryCount");

        /// <summary>已知直接子目录总数（用于还原被 Top 挤掉的其他行）</summary>
        public static readonly Field ChildCount = FindByName("ChildCount");

        /// <summary>计入文件数</summary>
        public static readonly Field FileCount = FindByName("FileCount");

        /// <summary>保存的排行条数</summary>
        public static readonly Field Top = FindByName("Top");

        /// <summary>是否因条目上限被截断</summary>
        public static readonly Field Truncated = FindByName("Truncated");

        /// <summary>无权限条目数</summary>
        public static readonly Field InaccessibleCount = FindByName("InaccessibleCount");

        /// <summary>跳过的重解析点数</summary>
        public static readonly Field SkippedReparseCount = FindByName("SkippedReparseCount");

        /// <summary>备注</summary>
        public static readonly Field Note = FindByName("Note");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得目录大小扫描快照字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>快照ID</summary>
        public const String Id = "Id";

        /// <summary>扫描根路径</summary>
        public const String RootPath = "RootPath";

        /// <summary>扫描完成时间</summary>
        public const String ScannedAt = "ScannedAt";

        /// <summary>扫描耗时毫秒</summary>
        public const String DurationMs = "DurationMs";

        /// <summary>根目录总字节（含全部后代）</summary>
        public const String RootTotalBytes = "RootTotalBytes";

        /// <summary>根目录本级文件字节（不属于任何子目录）</summary>
        public const String RootOwnBytes = "RootOwnBytes";

        /// <summary>计入目录数</summary>
        public const String DirectoryCount = "DirectoryCount";

        /// <summary>已知直接子目录总数（用于还原被 Top 挤掉的其他行）</summary>
        public const String ChildCount = "ChildCount";

        /// <summary>计入文件数</summary>
        public const String FileCount = "FileCount";

        /// <summary>保存的排行条数</summary>
        public const String Top = "Top";

        /// <summary>是否因条目上限被截断</summary>
        public const String Truncated = "Truncated";

        /// <summary>无权限条目数</summary>
        public const String InaccessibleCount = "InaccessibleCount";

        /// <summary>跳过的重解析点数</summary>
        public const String SkippedReparseCount = "SkippedReparseCount";

        /// <summary>备注</summary>
        public const String Note = "Note";
    }
    #endregion
}
