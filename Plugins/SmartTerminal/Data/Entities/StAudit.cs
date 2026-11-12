using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>只追加审计</summary>
[Serializable]
[DataObject]
[Description("只追加审计")]
[BindIndex("IX_StAudit_SessionId", false, "SessionId")]
[BindIndex("IX_StAudit_CreateTime", false, "CreateTime")]
[BindTable("StAudit", Description = "只追加审计", ConnName = "SmartTerminal", DbType = DatabaseType.None)]
public partial class StAudit
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Kind;
    /// <summary>审计类型</summary>
    [DisplayName("审计类型")]
    [Description("审计类型")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("Kind", "审计类型", "", Master = true)]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _SessionId;
    /// <summary>会话 id</summary>
    [DisplayName("会话 id")]
    [Description("会话 id")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("SessionId", "会话 id", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private String _ProfileId;
    /// <summary>配置 id</summary>
    [DisplayName("配置 id")]
    [Description("配置 id")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("ProfileId", "配置 id", "")]
    public String ProfileId { get => _ProfileId; set { if (OnPropertyChanging("ProfileId", value)) { _ProfileId = value; OnPropertyChanged("ProfileId"); } } }

    private String _Source;
    /// <summary>ui / agent / system</summary>
    [DisplayName("ui / agent / system")]
    [Description("ui / agent / system")]
    [DataObjectField(false, false, true, 16)]
    [BindColumn("Source", "ui / agent / system", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _Actor;
    /// <summary>操作者</summary>
    [DisplayName("操作者")]
    [Description("操作者")]
    [DataObjectField(false, false, true, 128)]
    [BindColumn("Actor", "操作者", "")]
    public String Actor { get => _Actor; set { if (OnPropertyChanging("Actor", value)) { _Actor = value; OnPropertyChanged("Actor"); } } }

    private String _CommandRedacted;
    /// <summary>脱敏命令</summary>
    [DisplayName("脱敏命令")]
    [Description("脱敏命令")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("CommandRedacted", "脱敏命令", "")]
    public String CommandRedacted { get => _CommandRedacted; set { if (OnPropertyChanging("CommandRedacted", value)) { _CommandRedacted = value; OnPropertyChanged("CommandRedacted"); } } }

    private String _DetailJson;
    /// <summary>脱敏详情 JSON</summary>
    [DisplayName("脱敏详情 JSON")]
    [Description("脱敏详情 JSON")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("DetailJson", "脱敏详情 JSON", "")]
    public String DetailJson { get => _DetailJson; set { if (OnPropertyChanging("DetailJson", value)) { _DetailJson = value; OnPropertyChanged("DetailJson"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
    #endregion
}
