using System;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>凭据授权（Grant）：谁可以在什么目标上以什么操作使用哪条机密（ConnName=ForgeSelf）。</summary>
/// <remarks>
/// 方案真源：<c>docs/08-security/credential-vault-design.md</c> §4（PILOT-086）。
/// 默认拒绝：没有匹配 Grant 的使用一律失败；撤销（RevokedAt 置时间）立即生效，无缓存。
/// RevokedAt/ExpiresAt/ValidAt 用 DateTime.MinValue 表示「不限」。
/// </remarks>
[Serializable]
[DataObject]
[Description("凭据授权（Grant）。")]
[BindIndex("IX_CredentialGrant_SecretId", false, "SecretId")]
[BindTable("CredentialGrant", Description = "凭据授权（Grant）", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class CredentialGrant : ICredentialGrantModel, IEntity<ICredentialGrantModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>授权ID</summary>
    [DisplayName("授权ID")]
    [Description("授权ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "授权ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _SecretId;
    /// <summary>被授权的机密ID（StoredSecret.Id）</summary>
    [DisplayName("机密ID")]
    [Description("被授权的机密ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SecretId", "机密ID", "")]
    public Int64 SecretId { get => _SecretId; set { if (OnPropertyChanging("SecretId", value)) { _SecretId = value; OnPropertyChanged("SecretId"); } } }

    private String _SubjectType;
    /// <summary>主体类型（host | agent | plugin | automation）</summary>
    [DisplayName("主体类型")]
    [Description("主体类型")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("SubjectType", "主体类型", "")]
    public String SubjectType { get => _SubjectType; set { if (OnPropertyChanging("SubjectType", value)) { _SubjectType = value; OnPropertyChanged("SubjectType"); } } }

    private String _SubjectId;
    /// <summary>主体标识（如 host / ai-agent / 插件名）</summary>
    [DisplayName("主体标识")]
    [Description("主体标识")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("SubjectId", "主体标识", "")]
    public String SubjectId { get => _SubjectId; set { if (OnPropertyChanging("SubjectId", value)) { _SubjectId = value; OnPropertyChanged("SubjectId"); } } }

    private String _Operation;
    /// <summary>授权操作（use | test-connection | *）</summary>
    [DisplayName("授权操作")]
    [Description("授权操作，如 use/test-connection/*")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Operation", "授权操作", "")]
    public String Operation { get => _Operation; set { if (OnPropertyChanging("Operation", value)) { _Operation = value; OnPropertyChanged("Operation"); } } }

    private String _TargetConstraint;
    /// <summary>目标约束：空 或 * = 不限目标；否则目标大小写不敏感全等匹配（P1 最小语义）</summary>
    [DisplayName("目标约束")]
    [Description("目标约束，空或*为不限，否则全等匹配")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TargetConstraint", "目标约束", "")]
    public String TargetConstraint { get => _TargetConstraint; set { if (OnPropertyChanging("TargetConstraint", value)) { _TargetConstraint = value; OnPropertyChanged("TargetConstraint"); } } }

    private DateTime _ValidFrom;
    /// <summary>生效时间。MinValue 表示「立即生效」</summary>
    [DisplayName("生效时间")]
    [Description("生效时间，MinValue 表示立即生效")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ValidFrom", "生效时间", "")]
    public DateTime ValidFrom { get => _ValidFrom; set { if (OnPropertyChanging("ValidFrom", value)) { _ValidFrom = value; OnPropertyChanged("ValidFrom"); } } }

    private DateTime _ExpiresAt;
    /// <summary>过期时间。MinValue 表示「不过期」</summary>
    [DisplayName("过期时间")]
    [Description("过期时间，MinValue 表示不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpiresAt", "过期时间", "")]
    public DateTime ExpiresAt { get => _ExpiresAt; set { if (OnPropertyChanging("ExpiresAt", value)) { _ExpiresAt = value; OnPropertyChanged("ExpiresAt"); } } }

    private DateTime _RevokedAt;
    /// <summary>撤销时间。MinValue 表示「有效」</summary>
    [DisplayName("撤销时间")]
    [Description("撤销时间，MinValue 表示有效")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RevokedAt", "撤销时间", "")]
    public DateTime RevokedAt { get => _RevokedAt; set { if (OnPropertyChanging("RevokedAt", value)) { _RevokedAt = value; OnPropertyChanged("RevokedAt"); } } }

    private String _CreatedBy;
    /// <summary>创建人/创建方（审计链路）</summary>
    [DisplayName("创建方")]
    [Description("创建方")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("CreatedBy", "创建方", "")]
    public String CreatedBy { get => _CreatedBy; set { if (OnPropertyChanging("CreatedBy", value)) { _CreatedBy = value; OnPropertyChanged("CreatedBy"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ICredentialGrantModel model)
    {
        Id = model.Id;
        SecretId = model.SecretId;
        SubjectType = model.SubjectType;
        SubjectId = model.SubjectId;
        Operation = model.Operation;
        TargetConstraint = model.TargetConstraint;
        ValidFrom = model.ValidFrom;
        ExpiresAt = model.ExpiresAt;
        RevokedAt = model.RevokedAt;
        CreatedBy = model.CreatedBy;
        CreatedAt = model.CreatedAt;
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
            "SecretId" => _SecretId,
            "SubjectType" => _SubjectType,
            "SubjectId" => _SubjectId,
            "Operation" => _Operation,
            "TargetConstraint" => _TargetConstraint,
            "ValidFrom" => _ValidFrom,
            "ExpiresAt" => _ExpiresAt,
            "RevokedAt" => _RevokedAt,
            "CreatedBy" => _CreatedBy,
            "CreatedAt" => _CreatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SecretId": _SecretId = value.ToLong(); break;
                case "SubjectType": _SubjectType = Convert.ToString(value); break;
                case "SubjectId": _SubjectId = Convert.ToString(value); break;
                case "Operation": _Operation = Convert.ToString(value); break;
                case "TargetConstraint": _TargetConstraint = Convert.ToString(value); break;
                case "ValidFrom": _ValidFrom = value.ToDateTime(); break;
                case "ExpiresAt": _ExpiresAt = value.ToDateTime(); break;
                case "RevokedAt": _RevokedAt = value.ToDateTime(); break;
                case "CreatedBy": _CreatedBy = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>根据授权ID查找</summary>
    /// <param name="id">授权ID</param>
    /// <returns>实体对象</returns>
    public static CredentialGrant? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>查找某机密的全部授权（授权矩阵热路径）</summary>
    /// <param name="secretId">机密ID</param>
    /// <returns>授权实体集合</returns>
    public static IList<CredentialGrant> FindAllBySecretId(Int64 secretId)
    {
        if (secretId <= 0) return [];
        return FindAll(_.SecretId == secretId);
    }
    #endregion

    #region 字段名
    /// <summary>取得凭据授权字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>授权ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>机密ID</summary>
        public static readonly Field SecretId = FindByName("SecretId");
        /// <summary>主体类型</summary>
        public static readonly Field SubjectType = FindByName("SubjectType");
        /// <summary>主体标识</summary>
        public static readonly Field SubjectId = FindByName("SubjectId");
        /// <summary>授权操作</summary>
        public static readonly Field Operation = FindByName("Operation");
        /// <summary>目标约束</summary>
        public static readonly Field TargetConstraint = FindByName("TargetConstraint");
        /// <summary>生效时间</summary>
        public static readonly Field ValidFrom = FindByName("ValidFrom");
        /// <summary>过期时间</summary>
        public static readonly Field ExpiresAt = FindByName("ExpiresAt");
        /// <summary>撤销时间</summary>
        public static readonly Field RevokedAt = FindByName("RevokedAt");
        /// <summary>创建方</summary>
        public static readonly Field CreatedBy = FindByName("CreatedBy");
        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得凭据授权字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>授权ID</summary>
        public const String Id = "Id";
        /// <summary>机密ID</summary>
        public const String SecretId = "SecretId";
        /// <summary>主体类型</summary>
        public const String SubjectType = "SubjectType";
        /// <summary>主体标识</summary>
        public const String SubjectId = "SubjectId";
        /// <summary>授权操作</summary>
        public const String Operation = "Operation";
        /// <summary>目标约束</summary>
        public const String TargetConstraint = "TargetConstraint";
        /// <summary>生效时间</summary>
        public const String ValidFrom = "ValidFrom";
        /// <summary>过期时间</summary>
        public const String ExpiresAt = "ExpiresAt";
        /// <summary>撤销时间</summary>
        public const String RevokedAt = "RevokedAt";
        /// <summary>创建方</summary>
        public const String CreatedBy = "CreatedBy";
        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
    }
    #endregion
}

/// <summary>凭据授权模型接口（供 XCode 实体实现）。</summary>
public partial interface ICredentialGrantModel
{
    Int64 Id { get; set; }
    Int64 SecretId { get; set; }
    String SubjectType { get; set; }
    String SubjectId { get; set; }
    String Operation { get; set; }
    String TargetConstraint { get; set; }
    DateTime ValidFrom { get; set; }
    DateTime ExpiresAt { get; set; }
    DateTime RevokedAt { get; set; }
    String CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
}
