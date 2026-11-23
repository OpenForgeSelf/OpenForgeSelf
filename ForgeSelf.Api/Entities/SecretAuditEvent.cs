using System;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>凭据审计事件：脱敏记录保险库的关键动作（ConnName=ForgeSelf）。</summary>
/// <remarks>
/// 方案真源：<c>docs/08-security/credential-vault-design.md</c> §4（PILOT-086）。
/// <b>禁止记录</b>明文、密文、Authorization 头、完整连接串；Target 只存目标域名/端点等脱敏约束值。
/// Secret 删除后审计行保留（secretId 指向已删行仍可查）。
/// </remarks>
[Serializable]
[DataObject]
[Description("凭据审计事件。")]
[BindIndex("IX_SecretAuditEvent_SecretId", false, "SecretId")]
[BindTable("SecretAuditEvent", Description = "凭据审计事件", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class SecretAuditEvent : ISecretAuditEventModel, IEntity<ISecretAuditEventModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>事件ID</summary>
    [DisplayName("事件ID")]
    [Description("事件ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "事件ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _SecretId;
    /// <summary>关联机密ID（0 表示与单条机密无关）</summary>
    [DisplayName("机密ID")]
    [Description("关联机密ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SecretId", "机密ID", "")]
    public Int64 SecretId { get => _SecretId; set { if (OnPropertyChanging("SecretId", value)) { _SecretId = value; OnPropertyChanged("SecretId"); } } }

    private Int64 _GrantId;
    /// <summary>关联授权ID（0 表示不涉及具体授权）</summary>
    [DisplayName("授权ID")]
    [Description("关联授权ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GrantId", "授权ID", "")]
    public Int64 GrantId { get => _GrantId; set { if (OnPropertyChanging("GrantId", value)) { _GrantId = value; OnPropertyChanged("GrantId"); } } }

    private String _Actor;
    /// <summary>发起方（如 api:admin、host:llm-adapter）</summary>
    [DisplayName("发起方")]
    [Description("发起方")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("Actor", "发起方", "")]
    public String Actor { get => _Actor; set { if (OnPropertyChanging("Actor", value)) { _Actor = value; OnPropertyChanged("Actor"); } } }

    private String _SubjectType;
    /// <summary>使用主体类型（可空）</summary>
    [DisplayName("主体类型")]
    [Description("使用主体类型")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("SubjectType", "主体类型", "")]
    public String SubjectType { get => _SubjectType; set { if (OnPropertyChanging("SubjectType", value)) { _SubjectType = value; OnPropertyChanged("SubjectType"); } } }

    private String _SubjectId;
    /// <summary>使用主体标识（可空）</summary>
    [DisplayName("主体标识")]
    [Description("使用主体标识")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("SubjectId", "主体标识", "")]
    public String SubjectId { get => _SubjectId; set { if (OnPropertyChanging("SubjectId", value)) { _SubjectId = value; OnPropertyChanged("SubjectId"); } } }

    private String _Action;
    /// <summary>动作：create/update_value/revoke/delete/use_allowed/use_denied/grant_created/grant_revoked</summary>
    [DisplayName("动作")]
    [Description("动作")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Action", "动作", "")]
    public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

    private String _Target;
    /// <summary>目标（脱敏后的约束值/域名，可空）</summary>
    [DisplayName("目标")]
    [Description("目标，脱敏后约束值")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Target", "目标", "")]
    public String Target { get => _Target; set { if (OnPropertyChanging("Target", value)) { _Target = value; OnPropertyChanged("Target"); } } }

    private String _Result;
    /// <summary>结果：allowed | denied | error</summary>
    [DisplayName("结果")]
    [Description("结果：allowed/denied/error")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Result", "结果", "")]
    public String Result { get => _Result; set { if (OnPropertyChanging("Result", value)) { _Result = value; OnPropertyChanged("Result"); } } }

    private String _ReasonCode;
    /// <summary>拒绝原因码（no_grant/secret_revoked/secret_expired/target_mismatch 等，可空）</summary>
    [DisplayName("原因码")]
    [Description("拒绝原因码")]
    [DataObjectField(false, false, true, 40)]
    [BindColumn("ReasonCode", "原因码", "")]
    public String ReasonCode { get => _ReasonCode; set { if (OnPropertyChanging("ReasonCode", value)) { _ReasonCode = value; OnPropertyChanged("ReasonCode"); } } }

    private String _CorrelationId;
    /// <summary>调用链关联ID（可空）</summary>
    [DisplayName("关联ID")]
    [Description("调用链关联ID")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("CorrelationId", "关联ID", "")]
    public String CorrelationId { get => _CorrelationId; set { if (OnPropertyChanging("CorrelationId", value)) { _CorrelationId = value; OnPropertyChanged("CorrelationId"); } } }

    private DateTime _CreatedAt;
    /// <summary>事件时间</summary>
    [DisplayName("事件时间")]
    [Description("事件时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatedAt", "事件时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ISecretAuditEventModel model)
    {
        Id = model.Id;
        SecretId = model.SecretId;
        GrantId = model.GrantId;
        Actor = model.Actor;
        SubjectType = model.SubjectType;
        SubjectId = model.SubjectId;
        Action = model.Action;
        Target = model.Target;
        Result = model.Result;
        ReasonCode = model.ReasonCode;
        CorrelationId = model.CorrelationId;
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
            "GrantId" => _GrantId,
            "Actor" => _Actor,
            "SubjectType" => _SubjectType,
            "SubjectId" => _SubjectId,
            "Action" => _Action,
            "Target" => _Target,
            "Result" => _Result,
            "ReasonCode" => _ReasonCode,
            "CorrelationId" => _CorrelationId,
            "CreatedAt" => _CreatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SecretId": _SecretId = value.ToLong(); break;
                case "GrantId": _GrantId = value.ToLong(); break;
                case "Actor": _Actor = Convert.ToString(value); break;
                case "SubjectType": _SubjectType = Convert.ToString(value); break;
                case "SubjectId": _SubjectId = Convert.ToString(value); break;
                case "Action": _Action = Convert.ToString(value); break;
                case "Target": _Target = Convert.ToString(value); break;
                case "Result": _Result = Convert.ToString(value); break;
                case "ReasonCode": _ReasonCode = Convert.ToString(value); break;
                case "CorrelationId": _CorrelationId = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>取某机密的最近审计事件（新→旧）</summary>
    /// <param name="secretId">机密ID；0 表示不过滤</param>
    /// <param name="count">条数</param>
    /// <returns>事件集合</returns>
    public static IList<SecretAuditEvent> FindAllRecent(Int64 secretId, Int32 count)
    {
        var exp = new WhereExpression();
        if (secretId > 0) exp &= _.SecretId == secretId;
        return FindAll(exp).OrderByDescending(e => e.Id).Take(count).ToList();
    }
    #endregion

    #region 字段名
    /// <summary>取得凭据审计事件字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>事件ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>机密ID</summary>
        public static readonly Field SecretId = FindByName("SecretId");
        /// <summary>授权ID</summary>
        public static readonly Field GrantId = FindByName("GrantId");
        /// <summary>发起方</summary>
        public static readonly Field Actor = FindByName("Actor");
        /// <summary>主体类型</summary>
        public static readonly Field SubjectType = FindByName("SubjectType");
        /// <summary>主体标识</summary>
        public static readonly Field SubjectId = FindByName("SubjectId");
        /// <summary>动作</summary>
        public static readonly Field Action = FindByName("Action");
        /// <summary>目标</summary>
        public static readonly Field Target = FindByName("Target");
        /// <summary>结果</summary>
        public static readonly Field Result = FindByName("Result");
        /// <summary>原因码</summary>
        public static readonly Field ReasonCode = FindByName("ReasonCode");
        /// <summary>关联ID</summary>
        public static readonly Field CorrelationId = FindByName("CorrelationId");
        /// <summary>事件时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得凭据审计事件字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>事件ID</summary>
        public const String Id = "Id";
        /// <summary>机密ID</summary>
        public const String SecretId = "SecretId";
        /// <summary>授权ID</summary>
        public const String GrantId = "GrantId";
        /// <summary>发起方</summary>
        public const String Actor = "Actor";
        /// <summary>主体类型</summary>
        public const String SubjectType = "SubjectType";
        /// <summary>主体标识</summary>
        public const String SubjectId = "SubjectId";
        /// <summary>动作</summary>
        public const String Action = "Action";
        /// <summary>目标</summary>
        public const String Target = "Target";
        /// <summary>结果</summary>
        public const String Result = "Result";
        /// <summary>原因码</summary>
        public const String ReasonCode = "ReasonCode";
        /// <summary>关联ID</summary>
        public const String CorrelationId = "CorrelationId";
        /// <summary>事件时间</summary>
        public const String CreatedAt = "CreatedAt";
    }
    #endregion
}

/// <summary>凭据审计事件模型接口（供 XCode 实体实现）。</summary>
public partial interface ISecretAuditEventModel
{
    Int64 Id { get; set; }
    Int64 SecretId { get; set; }
    Int64 GrantId { get; set; }
    String Actor { get; set; }
    String SubjectType { get; set; }
    String SubjectId { get; set; }
    String Action { get; set; }
    String Target { get; set; }
    String Result { get; set; }
    String ReasonCode { get; set; }
    String CorrelationId { get; set; }
    DateTime CreatedAt { get; set; }
}
