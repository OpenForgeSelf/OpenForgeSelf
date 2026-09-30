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

namespace ForgeSelf.Api.Plugins.DesignSystem.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IU_DesignRelease_ProjectId_Version", true, "ProjectId,Version")]
[BindIndex("IX_DesignRelease_ProjectId_CreatedAt", false, "ProjectId,CreatedAt")]
[BindTable("DesignRelease", Description = "设计系统版本快照", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignRelease
{
    #region 属性
    private Int64 _Id;
    /// <summary>发布ID</summary>
    [DisplayName("发布ID")]
    [Description("发布ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "发布ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Version;
    /// <summary>语义化版本号</summary>
    [DisplayName("语义化版本号")]
    [Description("语义化版本号")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Version", "语义化版本号", "", Master = true)]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _Status;
    /// <summary>状态：published|superseded</summary>
    [DisplayName("状态")]
    [Description("状态：published|superseded")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Status", "状态：published|superseded", "", DefaultValue = "published")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _TokensHash;
    /// <summary>有效令牌集 SHA256，用于判重与回归定位</summary>
    [DisplayName("有效令牌集SHA256")]
    [Description("有效令牌集 SHA256，用于判重与回归定位")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("TokensHash", "有效令牌集 SHA256，用于判重与回归定位", "")]
    public String TokensHash { get => _TokensHash; set { if (OnPropertyChanging("TokensHash", value)) { _TokensHash = value; OnPropertyChanged("TokensHash"); } } }

    private String _SnapshotFile;
    /// <summary>不可变快照文件相对路径（落在插件数据目录 releases/ 下）</summary>
    [DisplayName("不可变快照文件相对路径（落在插件数据目录releases_下）")]
    [Description("不可变快照文件相对路径（落在插件数据目录 releases/ 下）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("SnapshotFile", "不可变快照文件相对路径（落在插件数据目录 releases/ 下）", "")]
    public String SnapshotFile { get => _SnapshotFile; set { if (OnPropertyChanging("SnapshotFile", value)) { _SnapshotFile = value; OnPropertyChanged("SnapshotFile"); } } }

    private Int32 _TokenCount;
    /// <summary>快照内令牌数</summary>
    [DisplayName("快照内令牌数")]
    [Description("快照内令牌数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TokenCount", "快照内令牌数", "")]
    public Int32 TokenCount { get => _TokenCount; set { if (OnPropertyChanging("TokenCount", value)) { _TokenCount = value; OnPropertyChanged("TokenCount"); } } }

    private String _AuditSummary;
    /// <summary>审计摘要（critical/warning/info 计数）</summary>
    [DisplayName("审计摘要（critical_warning_info计数）")]
    [Description("审计摘要（critical/warning/info 计数）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AuditSummary", "审计摘要（critical/warning/info 计数）", "")]
    public String AuditSummary { get => _AuditSummary; set { if (OnPropertyChanging("AuditSummary", value)) { _AuditSummary = value; OnPropertyChanged("AuditSummary"); } } }

    private Boolean _AuditPassed;
    /// <summary>发布时是否无 critical 未决</summary>
    [DisplayName("发布时是否无critical未决")]
    [Description("发布时是否无 critical 未决")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AuditPassed", "发布时是否无 critical 未决", "", DefaultValue = "0")]
    public Boolean AuditPassed { get => _AuditPassed; set { if (OnPropertyChanging("AuditPassed", value)) { _AuditPassed = value; OnPropertyChanged("AuditPassed"); } } }

    private Int64 _SourceReleaseId;
    /// <summary>diff 基线发布ID，0=首版</summary>
    [DisplayName("diff基线发布ID")]
    [Description("diff 基线发布ID，0=首版")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SourceReleaseId", "diff 基线发布ID，0=首版", "", DefaultValue = "0")]
    public Int64 SourceReleaseId { get => _SourceReleaseId; set { if (OnPropertyChanging("SourceReleaseId", value)) { _SourceReleaseId = value; OnPropertyChanged("SourceReleaseId"); } } }

    private String _ReleaseNotes;
    /// <summary>变更说明</summary>
    [DisplayName("变更说明")]
    [Description("变更说明")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ReleaseNotes", "变更说明", "")]
    public String ReleaseNotes { get => _ReleaseNotes; set { if (OnPropertyChanging("ReleaseNotes", value)) { _ReleaseNotes = value; OnPropertyChanged("ReleaseNotes"); } } }

    private String _CreatedBy;
    /// <summary>创建者</summary>
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CreatedBy", "创建者", "")]
    public String CreatedBy { get => _CreatedBy; set { if (OnPropertyChanging("CreatedBy", value)) { _CreatedBy = value; OnPropertyChanged("CreatedBy"); } } }

    private String _Extensions;
    /// <summary>扩展元数据袋（JSON）</summary>
    [DisplayName("扩展元数据袋（JSON）")]
    [Description("扩展元数据袋（JSON）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Extensions", "扩展元数据袋（JSON）", "")]
    public String Extensions { get => _Extensions; set { if (OnPropertyChanging("Extensions", value)) { _Extensions = value; OnPropertyChanged("Extensions"); } } }

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
            "ProjectId" => _ProjectId,
            "Version" => _Version,
            "Status" => _Status,
            "TokensHash" => _TokensHash,
            "SnapshotFile" => _SnapshotFile,
            "TokenCount" => _TokenCount,
            "AuditSummary" => _AuditSummary,
            "AuditPassed" => _AuditPassed,
            "SourceReleaseId" => _SourceReleaseId,
            "ReleaseNotes" => _ReleaseNotes,
            "CreatedBy" => _CreatedBy,
            "Extensions" => _Extensions,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ProjectId": _ProjectId = value.ToLong(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "TokensHash": _TokensHash = Convert.ToString(value); break;
                case "SnapshotFile": _SnapshotFile = Convert.ToString(value); break;
                case "TokenCount": _TokenCount = value.ToInt(); break;
                case "AuditSummary": _AuditSummary = Convert.ToString(value); break;
                case "AuditPassed": _AuditPassed = value.ToBoolean(); break;
                case "SourceReleaseId": _SourceReleaseId = value.ToLong(); break;
                case "ReleaseNotes": _ReleaseNotes = Convert.ToString(value); break;
                case "CreatedBy": _CreatedBy = Convert.ToString(value); break;
                case "Extensions": _Extensions = Convert.ToString(value); break;
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
    /// <summary>根据发布ID查找</summary>
    /// <param name="id">发布ID</param>
    /// <returns>实体对象</returns>
    public static DesignRelease FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、语义化版本号查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="version">语义化版本号</param>
    /// <returns>实体对象</returns>
    public static DesignRelease FindByProjectIdAndVersion(Int64 projectId, String version)
    {
        if (projectId < 0) return null;
        if (version.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProjectId == projectId && e.Version.EqualIgnoreCase(version));

        return Find(_.ProjectId == projectId & _.Version == version);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignRelease> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="auditPassed">发布时是否无 critical 未决</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignRelease> Search(Int64 projectId, Boolean? auditPassed, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (auditPassed != null) exp &= _.AuditPassed == auditPassed;
        exp &= _.CreatedAt.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计系统版本快照字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>发布ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>语义化版本号</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>状态：published|superseded</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>有效令牌集 SHA256，用于判重与回归定位</summary>
        public static readonly Field TokensHash = FindByName("TokensHash");

        /// <summary>不可变快照文件相对路径（落在插件数据目录 releases/ 下）</summary>
        public static readonly Field SnapshotFile = FindByName("SnapshotFile");

        /// <summary>快照内令牌数</summary>
        public static readonly Field TokenCount = FindByName("TokenCount");

        /// <summary>审计摘要（critical/warning/info 计数）</summary>
        public static readonly Field AuditSummary = FindByName("AuditSummary");

        /// <summary>发布时是否无 critical 未决</summary>
        public static readonly Field AuditPassed = FindByName("AuditPassed");

        /// <summary>diff 基线发布ID，0=首版</summary>
        public static readonly Field SourceReleaseId = FindByName("SourceReleaseId");

        /// <summary>变更说明</summary>
        public static readonly Field ReleaseNotes = FindByName("ReleaseNotes");

        /// <summary>创建者</summary>
        public static readonly Field CreatedBy = FindByName("CreatedBy");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计系统版本快照字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>发布ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>语义化版本号</summary>
        public const String Version = "Version";

        /// <summary>状态：published|superseded</summary>
        public const String Status = "Status";

        /// <summary>有效令牌集 SHA256，用于判重与回归定位</summary>
        public const String TokensHash = "TokensHash";

        /// <summary>不可变快照文件相对路径（落在插件数据目录 releases/ 下）</summary>
        public const String SnapshotFile = "SnapshotFile";

        /// <summary>快照内令牌数</summary>
        public const String TokenCount = "TokenCount";

        /// <summary>审计摘要（critical/warning/info 计数）</summary>
        public const String AuditSummary = "AuditSummary";

        /// <summary>发布时是否无 critical 未决</summary>
        public const String AuditPassed = "AuditPassed";

        /// <summary>diff 基线发布ID，0=首版</summary>
        public const String SourceReleaseId = "SourceReleaseId";

        /// <summary>变更说明</summary>
        public const String ReleaseNotes = "ReleaseNotes";

        /// <summary>创建者</summary>
        public const String CreatedBy = "CreatedBy";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
