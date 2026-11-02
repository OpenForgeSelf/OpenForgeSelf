using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>权限策略决策日志（B4）：每次策略判定的只增事实（审计数据源之一）。</summary>
[Serializable]
[DataObject]
[Description("权限策略决策日志。")]
[BindIndex("IX_PermissionDecisionLog_Time", false, "Time")]
[BindIndex("IX_PermissionDecisionLog_ToolName", false, "ToolName")]
[BindTable("PermissionDecisionLog", Description = "权限策略决策日志", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class PermissionDecisionLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>日志ID</summary>
    [DisplayName("日志ID")]
    [Description("日志ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "日志ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private DateTime _Time;
    /// <summary>时间</summary>
    [DisplayName("决策时间")]
    [Description("决策时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Time", "决策时间", "", Master = true)]
    public DateTime Time { get => _Time; set { if (OnPropertyChanging("Time", value)) { _Time = value; OnPropertyChanged("Time"); } } }

    private String _SessionId = string.Empty;
    /// <summary>会话ID</summary>
    [DisplayName("会话ID")]
    [Description("会话ID")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("SessionId", "会话ID", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _AgentId = string.Empty;
    /// <summary>决策命中的 Agent id（未记忆会话为空）</summary>
    [DisplayName("AgentID")]
    [Description("决策命中的 Agent id（未记忆会话为空）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("AgentId", "AgentID", "")]
    public String AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private String _ToolName = string.Empty;
    /// <summary>工具名</summary>
    [DisplayName("工具名")]
    [Description("工具名")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("ToolName", "工具名", "")]
    public String ToolName { get => _ToolName; set { if (OnPropertyChanging("ToolName", value)) { _ToolName = value; OnPropertyChanged("ToolName"); } } }

    private String _ArgsHash = string.Empty;
    /// <summary>参数哈希（SHA256，审计幂等；不落原始参数避免敏感面扩大）</summary>
    [DisplayName("参数哈希")]
    [Description("参数哈希（SHA256，审计幂等；不落原始参数避免敏感面扩大）")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("ArgsHash", "参数哈希", "")]
    public String ArgsHash { get => _ArgsHash; set { if (OnPropertyChanging("ArgsHash", value)) { _ArgsHash = value; OnPropertyChanged("ArgsHash"); } } }

    private String _Effect = "Allow";
    /// <summary>决策 Allow/Ask/Deny</summary>
    [DisplayName("决策")]
    [Description("决策 Allow/Ask/Deny")]
    [DataObjectField(false, false, false, 16)]
    [BindColumn("Effect", "决策", "")]
    public String Effect { get => _Effect; set { if (OnPropertyChanging("Effect", value)) { _Effect = value; OnPropertyChanged("Effect"); } } }

    private String _Source = "default-policy";
    /// <summary>决策来源 profile:{id} / default-policy</summary>
    [DisplayName("决策来源")]
    [Description("决策来源 profile:{id} / default-policy")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("Source", "决策来源", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _Reason = string.Empty;
    /// <summary>决策理由（给人看；Ask 时为审批原因）</summary>
    [DisplayName("理由")]
    [Description("决策理由（给人看；Ask 时为审批原因）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Reason", "理由", "")]
    public String Reason { get => _Reason; set { if (OnPropertyChanging("Reason", value)) { _Reason = value; OnPropertyChanged("Reason"); } } }

    private Int64 _ApprovalId;
    /// <summary>关联审批 id（Ask 产生审批时回填；0 = 无）</summary>
    [DisplayName("审批ID")]
    [Description("关联审批 id（Ask 产生审批时回填；0 = 无）")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ApprovalId", "审批ID", "")]
    public Int64 ApprovalId { get => _ApprovalId; set { if (OnPropertyChanging("ApprovalId", value)) { _ApprovalId = value; OnPropertyChanged("ApprovalId"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <inheritdoc />
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Time" => _Time,
            "SessionId" => _SessionId,
            "AgentId" => _AgentId,
            "ToolName" => _ToolName,
            "ArgsHash" => _ArgsHash,
            "Effect" => _Effect,
            "Source" => _Source,
            "Reason" => _Reason!,
            "ApprovalId" => _ApprovalId,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Time": _Time = value.ToDateTime(); break;
                case "SessionId": _SessionId = Convert.ToString(value) ?? string.Empty; break;
                case "AgentId": _AgentId = Convert.ToString(value) ?? string.Empty; break;
                case "ToolName": _ToolName = Convert.ToString(value) ?? string.Empty; break;
                case "ArgsHash": _ArgsHash = Convert.ToString(value) ?? string.Empty; break;
                case "Effect": _Effect = Convert.ToString(value) ?? "Allow"; break;
                case "Source": _Source = Convert.ToString(value) ?? "default-policy"; break;
                case "Reason": _Reason = Convert.ToString(value); break;
                case "ApprovalId": _ApprovalId = value.ToLong(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>按 id 查找（未命中返回 null）。</summary>
    public static PermissionDecisionLog? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }
    #endregion

    #region 字段名
    /// <summary>取得权限策略决策日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>日志ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>决策时间</summary>
        public static readonly Field Time = FindByName("Time");
        /// <summary>会话ID</summary>
        public static readonly Field SessionId = FindByName("SessionId");
        /// <summary>工具名</summary>
        public static readonly Field ToolName = FindByName("ToolName");

        private static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
