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
[BindIndex("IX_ScanFolderEntry_SnapshotId", false, "SnapshotId")]
[BindIndex("IX_ScanFolderEntry_SnapshotId_TotalBytes", false, "SnapshotId,TotalBytes")]
[BindTable("ScanFolderEntry", Description = "目录大小排行明细行（属于某次快照）", ConnName = "FileTools", DbType = DatabaseType.None)]
public partial class ScanFolderEntry
{
    #region 属性
    private Int64 _Id;
    /// <summary>行ID</summary>
    [DisplayName("行ID")]
    [Description("行ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "行ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _SnapshotId;
    /// <summary>快照ID</summary>
    [DisplayName("快照ID")]
    [Description("快照ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SnapshotId", "快照ID", "")]
    public Int64 SnapshotId { get => _SnapshotId; set { if (OnPropertyChanging("SnapshotId", value)) { _SnapshotId = value; OnPropertyChanged("SnapshotId"); } } }

    private String _RelativePath;
    /// <summary>相对根的目录路径，/分隔</summary>
    [DisplayName("相对根的目录路径")]
    [Description("相对根的目录路径，/分隔")]
    [DataObjectField(false, false, false, 1000)]
    [BindColumn("RelativePath", "相对根的目录路径，/分隔", "", Master = true)]
    public String RelativePath { get => _RelativePath; set { if (OnPropertyChanging("RelativePath", value)) { _RelativePath = value; OnPropertyChanged("RelativePath"); } } }

    private String _Name;
    /// <summary>目录名</summary>
    [DisplayName("目录名")]
    [Description("目录名")]
    [DataObjectField(false, false, true, 260)]
    [BindColumn("Name", "目录名", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int64 _TotalBytes;
    /// <summary>含全部后代的字节数</summary>
    [DisplayName("含全部后代的字节数")]
    [Description("含全部后代的字节数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalBytes", "含全部后代的字节数", "")]
    public Int64 TotalBytes { get => _TotalBytes; set { if (OnPropertyChanging("TotalBytes", value)) { _TotalBytes = value; OnPropertyChanged("TotalBytes"); } } }

    private Int64 _DirectBytes;
    /// <summary>本级直接字节数</summary>
    [DisplayName("本级直接字节数")]
    [Description("本级直接字节数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DirectBytes", "本级直接字节数", "")]
    public Int64 DirectBytes { get => _DirectBytes; set { if (OnPropertyChanging("DirectBytes", value)) { _DirectBytes = value; OnPropertyChanged("DirectBytes"); } } }

    private Int64 _FileCount;
    /// <summary>本级文件数</summary>
    [DisplayName("本级文件数")]
    [Description("本级文件数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FileCount", "本级文件数", "")]
    public Int64 FileCount { get => _FileCount; set { if (OnPropertyChanging("FileCount", value)) { _FileCount = value; OnPropertyChanged("FileCount"); } } }

    private Int64 _DirCount;
    /// <summary>本级子目录数</summary>
    [DisplayName("本级子目录数")]
    [Description("本级子目录数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DirCount", "本级子目录数", "")]
    public Int64 DirCount { get => _DirCount; set { if (OnPropertyChanging("DirCount", value)) { _DirCount = value; OnPropertyChanged("DirCount"); } } }
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
            "SnapshotId" => _SnapshotId,
            "RelativePath" => _RelativePath,
            "Name" => _Name,
            "TotalBytes" => _TotalBytes,
            "DirectBytes" => _DirectBytes,
            "FileCount" => _FileCount,
            "DirCount" => _DirCount,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SnapshotId": _SnapshotId = value.ToLong(); break;
                case "RelativePath": _RelativePath = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "TotalBytes": _TotalBytes = value.ToLong(); break;
                case "DirectBytes": _DirectBytes = value.ToLong(); break;
                case "FileCount": _FileCount = value.ToLong(); break;
                case "DirCount": _DirCount = value.ToLong(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据行ID查找</summary>
    /// <param name="id">行ID</param>
    /// <returns>实体对象</returns>
    public static ScanFolderEntry FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据快照ID查找</summary>
    /// <param name="snapshotId">快照ID</param>
    /// <returns>实体列表</returns>
    public static IList<ScanFolderEntry> FindAllBySnapshotId(Int64 snapshotId)
    {
        if (snapshotId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SnapshotId == snapshotId);

        return FindAll(_.SnapshotId == snapshotId);
    }

    /// <summary>根据快照ID、含全部后代的字节数查找</summary>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="totalBytes">含全部后代的字节数</param>
    /// <returns>实体列表</returns>
    public static IList<ScanFolderEntry> FindAllBySnapshotIdAndTotalBytes(Int64 snapshotId, Int64 totalBytes)
    {
        if (snapshotId < 0) return [];
        if (totalBytes < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.SnapshotId == snapshotId && e.TotalBytes == totalBytes);

        return FindAll(_.SnapshotId == snapshotId & _.TotalBytes == totalBytes);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="snapshotId">快照ID</param>
    /// <param name="totalBytes">含全部后代的字节数</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ScanFolderEntry> Search(Int64 snapshotId, Int64 totalBytes, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (snapshotId >= 0) exp &= _.SnapshotId == snapshotId;
        if (totalBytes >= 0) exp &= _.TotalBytes == totalBytes;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得目录大小排行明细行（属于某次快照）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>行ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>快照ID</summary>
        public static readonly Field SnapshotId = FindByName("SnapshotId");

        /// <summary>相对根的目录路径，/分隔</summary>
        public static readonly Field RelativePath = FindByName("RelativePath");

        /// <summary>目录名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>含全部后代的字节数</summary>
        public static readonly Field TotalBytes = FindByName("TotalBytes");

        /// <summary>本级直接字节数</summary>
        public static readonly Field DirectBytes = FindByName("DirectBytes");

        /// <summary>本级文件数</summary>
        public static readonly Field FileCount = FindByName("FileCount");

        /// <summary>本级子目录数</summary>
        public static readonly Field DirCount = FindByName("DirCount");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得目录大小排行明细行（属于某次快照）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>行ID</summary>
        public const String Id = "Id";

        /// <summary>快照ID</summary>
        public const String SnapshotId = "SnapshotId";

        /// <summary>相对根的目录路径，/分隔</summary>
        public const String RelativePath = "RelativePath";

        /// <summary>目录名</summary>
        public const String Name = "Name";

        /// <summary>含全部后代的字节数</summary>
        public const String TotalBytes = "TotalBytes";

        /// <summary>本级直接字节数</summary>
        public const String DirectBytes = "DirectBytes";

        /// <summary>本级文件数</summary>
        public const String FileCount = "FileCount";

        /// <summary>本级子目录数</summary>
        public const String DirCount = "DirCount";
    }
    #endregion
}
