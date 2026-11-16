using System;
using NewLife;
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
    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Kind" => _Kind,
            "SessionId" => _SessionId,
            "ProfileId" => _ProfileId,
            "Source" => _Source,
            "Actor" => _Actor,
            "CommandRedacted" => _CommandRedacted,
            "DetailJson" => _DetailJson,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "ProfileId": _ProfileId = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Actor": _Actor = Convert.ToString(value); break;
                case "CommandRedacted": _CommandRedacted = Convert.ToString(value); break;
                case "DetailJson": _DetailJson = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得审计记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>类型</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>会话</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>配置</summary>
        public static readonly Field ProfileId = FindByName("ProfileId");

        /// <summary>来源</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>操作者</summary>
        public static readonly Field Actor = FindByName("Actor");

        /// <summary>已脱敏命令</summary>
        public static readonly Field CommandRedacted = FindByName("CommandRedacted");

        /// <summary>明细</summary>
        public static readonly Field DetailJson = FindByName("DetailJson");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得审计记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>类型</summary>
        public const String Kind = "Kind";

        /// <summary>会话</summary>
        public const String SessionId = "SessionId";

        /// <summary>配置</summary>
        public const String ProfileId = "ProfileId";

        /// <summary>来源</summary>
        public const String Source = "Source";

        /// <summary>操作者</summary>
        public const String Actor = "Actor";

        /// <summary>已脱敏命令</summary>
        public const String CommandRedacted = "CommandRedacted";

        /// <summary>明细</summary>
        public const String DetailJson = "DetailJson";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
