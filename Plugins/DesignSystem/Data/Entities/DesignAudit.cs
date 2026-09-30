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
[BindIndex("IU_DesignAudit_ProjectId_ReleaseId_Kind_TargetType_TargetPath", true, "ProjectId,ReleaseId,Kind,TargetType,TargetPath")]
[BindIndex("IX_DesignAudit_ProjectId_Passed", false, "ProjectId,Passed")]
[BindIndex("IX_DesignAudit_ProjectId_Severity", false, "ProjectId,Severity")]
[BindTable("DesignAudit", Description = "设计审计结果", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignAudit
{
    #region 属性
    private Int64 _Id;
    /// <summary>审计ID</summary>
    [DisplayName("审计ID")]
    [Description("审计ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "审计ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private Int64 _ReleaseId;
    /// <summary>关联发布快照ID，0=草稿态审计</summary>
    [DisplayName("关联发布快照ID")]
    [Description("关联发布快照ID，0=草稿态审计")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ReleaseId", "关联发布快照ID，0=草稿态审计", "", DefaultValue = "0")]
    public Int64 ReleaseId { get => _ReleaseId; set { if (OnPropertyChanging("ReleaseId", value)) { _ReleaseId = value; OnPropertyChanged("ReleaseId"); } } }

    private String _Kind;
    /// <summary>审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias</summary>
    [DisplayName("审计类")]
    [Description("审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Kind", "审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Rule;
    /// <summary>规则标识，如 wcag22-1.4.3 / dtcg-alias-cycle</summary>
    [DisplayName("规则标识")]
    [Description("规则标识，如 wcag22-1.4.3 / dtcg-alias-cycle")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Rule", "规则标识，如 wcag22-1.4.3 / dtcg-alias-cycle", "")]
    public String Rule { get => _Rule; set { if (OnPropertyChanging("Rule", value)) { _Rule = value; OnPropertyChanged("Rule"); } } }

    private String _Severity;
    /// <summary>严重级：critical|warning|info</summary>
    [DisplayName("严重级")]
    [Description("严重级：critical|warning|info")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Severity", "严重级：critical|warning|info", "", DefaultValue = "warning")]
    public String Severity { get => _Severity; set { if (OnPropertyChanging("Severity", value)) { _Severity = value; OnPropertyChanged("Severity"); } } }

    private String _TargetType;
    /// <summary>对象类型：token|component|variant|screen|asset|icon</summary>
    [DisplayName("对象类型")]
    [Description("对象类型：token|component|variant|screen|asset|icon")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("TargetType", "对象类型：token|component|variant|screen|asset|icon", "", DefaultValue = "token")]
    public String TargetType { get => _TargetType; set { if (OnPropertyChanging("TargetType", value)) { _TargetType = value; OnPropertyChanged("TargetType"); } } }

    private String _TargetPath;
    /// <summary>被检对象路径/标识</summary>
    [DisplayName("被检对象路径_标识")]
    [Description("被检对象路径/标识")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("TargetPath", "被检对象路径/标识", "")]
    public String TargetPath { get => _TargetPath; set { if (OnPropertyChanging("TargetPath", value)) { _TargetPath = value; OnPropertyChanged("TargetPath"); } } }

    private String _PairedPath;
    /// <summary>配对对象路径（对比度类）</summary>
    [DisplayName("配对对象路径（对比度类）")]
    [Description("配对对象路径（对比度类）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("PairedPath", "配对对象路径（对比度类）", "")]
    public String PairedPath { get => _PairedPath; set { if (OnPropertyChanging("PairedPath", value)) { _PairedPath = value; OnPropertyChanged("PairedPath"); } } }

    private String _Expected;
    /// <summary>期望值/阈值</summary>
    [DisplayName("期望值_阈值")]
    [Description("期望值/阈值")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Expected", "期望值/阈值", "")]
    public String Expected { get => _Expected; set { if (OnPropertyChanging("Expected", value)) { _Expected = value; OnPropertyChanged("Expected"); } } }

    private String _Actual;
    /// <summary>实测值</summary>
    [DisplayName("实测值")]
    [Description("实测值")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Actual", "实测值", "")]
    public String Actual { get => _Actual; set { if (OnPropertyChanging("Actual", value)) { _Actual = value; OnPropertyChanged("Actual"); } } }

    private Double _Ratio;
    /// <summary>比率读数，非比率类为 -1</summary>
    [DisplayName("比率读数")]
    [Description("比率读数，非比率类为 -1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ratio", "比率读数，非比率类为 -1", "")]
    public Double Ratio { get => _Ratio; set { if (OnPropertyChanging("Ratio", value)) { _Ratio = value; OnPropertyChanged("Ratio"); } } }

    private String _Name;
    /// <summary>审计项显示名</summary>
    [DisplayName("审计项显示名")]
    [Description("审计项显示名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "审计项显示名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _Passed;
    /// <summary>是否通过</summary>
    [DisplayName("是否通过")]
    [Description("是否通过")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Passed", "是否通过", "", DefaultValue = "0")]
    public Boolean Passed { get => _Passed; set { if (OnPropertyChanging("Passed", value)) { _Passed = value; OnPropertyChanged("Passed"); } } }

    private String _Suggestion;
    /// <summary>可执行建议（如改用哪个 tone）</summary>
    [DisplayName("可执行建议（如改用哪个tone）")]
    [Description("可执行建议（如改用哪个 tone）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Suggestion", "可执行建议（如改用哪个 tone）", "")]
    public String Suggestion { get => _Suggestion; set { if (OnPropertyChanging("Suggestion", value)) { _Suggestion = value; OnPropertyChanged("Suggestion"); } } }

    private String _Message;
    /// <summary>人类可读明细</summary>
    [DisplayName("人类可读明细")]
    [Description("人类可读明细")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Message", "人类可读明细", "")]
    public String Message { get => _Message; set { if (OnPropertyChanging("Message", value)) { _Message = value; OnPropertyChanged("Message"); } } }

    private String _EngineVersion;
    /// <summary>审计引擎版本</summary>
    [DisplayName("审计引擎版本")]
    [Description("审计引擎版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("EngineVersion", "审计引擎版本", "")]
    public String EngineVersion { get => _EngineVersion; set { if (OnPropertyChanging("EngineVersion", value)) { _EngineVersion = value; OnPropertyChanged("EngineVersion"); } } }

    private DateTime _CheckedAt;
    /// <summary>检查时间</summary>
    [DisplayName("检查时间")]
    [Description("检查时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CheckedAt", "检查时间", "")]
    public DateTime CheckedAt { get => _CheckedAt; set { if (OnPropertyChanging("CheckedAt", value)) { _CheckedAt = value; OnPropertyChanged("CheckedAt"); } } }

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
            "ReleaseId" => _ReleaseId,
            "Kind" => _Kind,
            "Rule" => _Rule,
            "Severity" => _Severity,
            "TargetType" => _TargetType,
            "TargetPath" => _TargetPath,
            "PairedPath" => _PairedPath,
            "Expected" => _Expected,
            "Actual" => _Actual,
            "Ratio" => _Ratio,
            "Name" => _Name,
            "Passed" => _Passed,
            "Suggestion" => _Suggestion,
            "Message" => _Message,
            "EngineVersion" => _EngineVersion,
            "CheckedAt" => _CheckedAt,
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
                case "ReleaseId": _ReleaseId = value.ToLong(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Rule": _Rule = Convert.ToString(value); break;
                case "Severity": _Severity = Convert.ToString(value); break;
                case "TargetType": _TargetType = Convert.ToString(value); break;
                case "TargetPath": _TargetPath = Convert.ToString(value); break;
                case "PairedPath": _PairedPath = Convert.ToString(value); break;
                case "Expected": _Expected = Convert.ToString(value); break;
                case "Actual": _Actual = Convert.ToString(value); break;
                case "Ratio": _Ratio = value.ToDouble(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Passed": _Passed = value.ToBoolean(); break;
                case "Suggestion": _Suggestion = Convert.ToString(value); break;
                case "Message": _Message = Convert.ToString(value); break;
                case "EngineVersion": _EngineVersion = Convert.ToString(value); break;
                case "CheckedAt": _CheckedAt = value.ToDateTime(); break;
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
    /// <summary>根据审计ID查找</summary>
    /// <param name="id">审计ID</param>
    /// <returns>实体对象</returns>
    public static DesignAudit FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、严重级查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="severity">严重级</param>
    /// <returns>实体列表</returns>
    public static IList<DesignAudit> FindAllByProjectIdAndSeverity(Int64 projectId, String severity)
    {
        if (projectId < 0) return [];
        if (severity.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Severity.EqualIgnoreCase(severity));

        return FindAll(_.ProjectId == projectId & _.Severity == severity);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignAudit> FindAllByProjectId(Int64 projectId)
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
    /// <param name="releaseId">关联发布快照ID，0=草稿态审计</param>
    /// <param name="kind">审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias</param>
    /// <param name="severity">严重级：critical|warning|info</param>
    /// <param name="targetType">对象类型：token|component|variant|screen|asset|icon</param>
    /// <param name="targetPath">被检对象路径/标识</param>
    /// <param name="passed">是否通过</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignAudit> Search(Int64 projectId, Int64 releaseId, String kind, String severity, String targetType, String targetPath, Boolean? passed, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (releaseId >= 0) exp &= _.ReleaseId == releaseId;
        if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
        if (!severity.IsNullOrEmpty()) exp &= _.Severity == severity;
        if (!targetType.IsNullOrEmpty()) exp &= _.TargetType == targetType;
        if (!targetPath.IsNullOrEmpty()) exp &= _.TargetPath == targetPath;
        if (passed != null) exp &= _.Passed == passed;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计审计结果字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>审计ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>关联发布快照ID，0=草稿态审计</summary>
        public static readonly Field ReleaseId = FindByName("ReleaseId");

        /// <summary>审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>规则标识，如 wcag22-1.4.3 / dtcg-alias-cycle</summary>
        public static readonly Field Rule = FindByName("Rule");

        /// <summary>严重级：critical|warning|info</summary>
        public static readonly Field Severity = FindByName("Severity");

        /// <summary>对象类型：token|component|variant|screen|asset|icon</summary>
        public static readonly Field TargetType = FindByName("TargetType");

        /// <summary>被检对象路径/标识</summary>
        public static readonly Field TargetPath = FindByName("TargetPath");

        /// <summary>配对对象路径（对比度类）</summary>
        public static readonly Field PairedPath = FindByName("PairedPath");

        /// <summary>期望值/阈值</summary>
        public static readonly Field Expected = FindByName("Expected");

        /// <summary>实测值</summary>
        public static readonly Field Actual = FindByName("Actual");

        /// <summary>比率读数，非比率类为 -1</summary>
        public static readonly Field Ratio = FindByName("Ratio");

        /// <summary>审计项显示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>是否通过</summary>
        public static readonly Field Passed = FindByName("Passed");

        /// <summary>可执行建议（如改用哪个 tone）</summary>
        public static readonly Field Suggestion = FindByName("Suggestion");

        /// <summary>人类可读明细</summary>
        public static readonly Field Message = FindByName("Message");

        /// <summary>审计引擎版本</summary>
        public static readonly Field EngineVersion = FindByName("EngineVersion");

        /// <summary>检查时间</summary>
        public static readonly Field CheckedAt = FindByName("CheckedAt");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计审计结果字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>审计ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>关联发布快照ID，0=草稿态审计</summary>
        public const String ReleaseId = "ReleaseId";

        /// <summary>审计类：contrast|focus|reduced-motion|tier-violation|orphan|unused|alias</summary>
        public const String Kind = "Kind";

        /// <summary>规则标识，如 wcag22-1.4.3 / dtcg-alias-cycle</summary>
        public const String Rule = "Rule";

        /// <summary>严重级：critical|warning|info</summary>
        public const String Severity = "Severity";

        /// <summary>对象类型：token|component|variant|screen|asset|icon</summary>
        public const String TargetType = "TargetType";

        /// <summary>被检对象路径/标识</summary>
        public const String TargetPath = "TargetPath";

        /// <summary>配对对象路径（对比度类）</summary>
        public const String PairedPath = "PairedPath";

        /// <summary>期望值/阈值</summary>
        public const String Expected = "Expected";

        /// <summary>实测值</summary>
        public const String Actual = "Actual";

        /// <summary>比率读数，非比率类为 -1</summary>
        public const String Ratio = "Ratio";

        /// <summary>审计项显示名</summary>
        public const String Name = "Name";

        /// <summary>是否通过</summary>
        public const String Passed = "Passed";

        /// <summary>可执行建议（如改用哪个 tone）</summary>
        public const String Suggestion = "Suggestion";

        /// <summary>人类可读明细</summary>
        public const String Message = "Message";

        /// <summary>审计引擎版本</summary>
        public const String EngineVersion = "EngineVersion";

        /// <summary>检查时间</summary>
        public const String CheckedAt = "CheckedAt";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
