using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>工具审批记录（P0）：高风险工具执行前的可持久化人审事实。</summary>
[Serializable]
[DataObject]
[Description("工具审批记录。")]
[BindIndex("IX_ToolApproval_SessionId", false, "SessionId")]
[BindIndex("IX_ToolApproval_Status", false, "Status")]
[BindTable("ToolApproval", Description = "工具审批记录", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class ToolApproval
{
    #region 属性
    private Int64 _Id;
    /// <summary>审批ID</summary>
    [DisplayName("审批ID")]
    [Description("审批ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "审批ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionId = string.Empty;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("SessionId", "会话ID", "", Master = true)]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _ToolName = string.Empty;
    /// <summary>工具名</summary>
    [DisplayName("工具名")]
    [Description("工具名")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("ToolName", "工具名", "")]
    public String ToolName { get => _ToolName; set { if (OnPropertyChanging("ToolName", value)) { _ToolName = value; OnPropertyChanged("ToolName"); } } }

    private String _Arguments = "{}";
    /// <summary>原始参数 JSON（批准后必须按此执行，禁止模型改参）</summary>
    [DisplayName("参数JSON")]
    [Description("参数JSON")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("Arguments", "参数JSON", "")]
    public String Arguments { get => _Arguments; set { if (OnPropertyChanging("Arguments", value)) { _Arguments = value; OnPropertyChanged("Arguments"); } } }

    private String _ArgumentsHash = string.Empty;
    /// <summary>参数哈希（审计/幂等）</summary>
    [DisplayName("参数哈希")]
    [Description("参数哈希")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("ArgumentsHash", "参数哈希", "")]
    public String ArgumentsHash { get => _ArgumentsHash; set { if (OnPropertyChanging("ArgumentsHash", value)) { _ArgumentsHash = value; OnPropertyChanged("ArgumentsHash"); } } }

    private String _RiskLevel = "high";
    /// <summary>风险等级</summary>
    [DisplayName("风险等级")]
    [Description("风险等级")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("RiskLevel", "风险等级", "")]
    public String RiskLevel { get => _RiskLevel; set { if (OnPropertyChanging("RiskLevel", value)) { _RiskLevel = value; OnPropertyChanged("RiskLevel"); } } }

    private String _Description = string.Empty;
    /// <summary>审批说明（给人看）</summary>
    [DisplayName("说明")]
    [Description("说明")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Description", "说明", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Status = "Pending";
    /// <summary>状态 Pending/Approved/Denied/Expired/Cancelled</summary>
    [DisplayName("状态")]
    [Description("状态")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("Status", "状态", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _ExpiresAt;
    /// <summary>过期时间（超时 fail-closed）</summary>
    [DisplayName("过期时间")]
    [Description("过期时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExpiresAt", "过期时间", "")]
    public DateTime ExpiresAt { get => _ExpiresAt; set { if (OnPropertyChanging("ExpiresAt", value)) { _ExpiresAt = value; OnPropertyChanged("ExpiresAt"); } } }

    private String? _Note;
    /// <summary>决策备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Note", "备注", "")]
    public String? Note { get => _Note; set { if (OnPropertyChanging("Note", value)) { _Note = value; OnPropertyChanged("Note"); } } }

    private String? _MeaiRequestId;
    /// <summary>MEAI ToolApprovalRequestContent.RequestId</summary>
    [DisplayName("MEAI请求ID")]
    [Description("MEAI请求ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("MeaiRequestId", "MEAI请求ID", "")]
    public String? MeaiRequestId { get => _MeaiRequestId; set { if (OnPropertyChanging("MeaiRequestId", value)) { _MeaiRequestId = value; OnPropertyChanged("MeaiRequestId"); } } }

    private String? _CallId;
    /// <summary>FunctionCallContent.CallId</summary>
    [DisplayName("调用ID")]
    [Description("调用ID")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CallId", "调用ID", "")]
    public String? CallId { get => _CallId; set { if (OnPropertyChanging("CallId", value)) { _CallId = value; OnPropertyChanged("CallId"); } } }

    private Boolean _ContinuationConsumed;
    /// <summary>MAF 续跑是否已消费（防止重复注入 ToolApprovalResponseContent）</summary>
    [DisplayName("续跑已消费")]
    [Description("续跑已消费")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ContinuationConsumed", "续跑已消费", "")]
    public Boolean ContinuationConsumed { get => _ContinuationConsumed; set { if (OnPropertyChanging("ContinuationConsumed", value)) { _ContinuationConsumed = value; OnPropertyChanged("ContinuationConsumed"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <inheritdoc />
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "SessionId" => _SessionId,
            "ToolName" => _ToolName,
            "Arguments" => _Arguments,
            "ArgumentsHash" => _ArgumentsHash,
            "RiskLevel" => _RiskLevel,
            "Description" => _Description,
            "Status" => _Status,
            "CreatedAt" => _CreatedAt,
            "ExpiresAt" => _ExpiresAt,
            "Note" => _Note!,
            "MeaiRequestId" => _MeaiRequestId!,
            "CallId" => _CallId!,
            "ContinuationConsumed" => _ContinuationConsumed,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SessionId": _SessionId = Convert.ToString(value) ?? string.Empty; break;
                case "ToolName": _ToolName = Convert.ToString(value) ?? string.Empty; break;
                case "Arguments": _Arguments = Convert.ToString(value) ?? "{}"; break;
                case "ArgumentsHash": _ArgumentsHash = Convert.ToString(value) ?? string.Empty; break;
                case "RiskLevel": _RiskLevel = Convert.ToString(value) ?? "high"; break;
                case "Description": _Description = Convert.ToString(value) ?? string.Empty; break;
                case "Status": _Status = Convert.ToString(value) ?? "Pending"; break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "ExpiresAt": _ExpiresAt = value.ToDateTime(); break;
                case "Note": _Note = Convert.ToString(value); break;
                case "MeaiRequestId": _MeaiRequestId = Convert.ToString(value); break;
                case "CallId": _CallId = Convert.ToString(value); break;
                case "ContinuationConsumed": _ContinuationConsumed = value.ToBoolean(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>按主键查找。</summary>
    public static ToolApproval? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }
    #endregion

    #region 字段名
    /// <summary>取得工具审批字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>审批ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");
        /// <summary>工具名</summary>
        public static readonly Field ToolName = FindByName("ToolName");
        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        private static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
