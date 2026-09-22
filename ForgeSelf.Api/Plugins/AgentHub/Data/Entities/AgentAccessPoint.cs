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

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>{name}。Agent 交互口</summary>
[Serializable]
[DataObject]
[Description("{name}。Agent 交互口")]
[BindIndex("IX_AgentAccessPoint_AgentId", false, "AgentId")]
[BindTable("AgentAccessPoint", Description = "Agent 交互口", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class AgentAccessPoint
{
    #region 属性
    private Int32 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AgentId;
    /// <summary>所属 Agent</summary>
    [DisplayName("所属Agent")]
    [Description("所属 Agent")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentId", "所属 Agent", "")]
    public Int32 AgentId { get => _AgentId; set { if (OnPropertyChanging("AgentId", value)) { _AgentId = value; OnPropertyChanged("AgentId"); } } }

    private String _Mode;
    /// <summary>能力等级（OneShot|Sessionful）</summary>
    [DisplayName("能力等级（OneShot|Sessionful）")]
    [Description("能力等级（OneShot|Sessionful）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Mode", "能力等级（OneShot|Sessionful）", "", DefaultValue = "OneShot")]
    public String Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private String _Transport;
    /// <summary>传输方式（Cli|Acp|Http）</summary>
    [DisplayName("传输方式（Cli|Acp|Http）")]
    [Description("传输方式（Cli|Acp|Http）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Transport", "传输方式（Cli|Acp|Http）", "", DefaultValue = "Cli")]
    public String Transport { get => _Transport; set { if (OnPropertyChanging("Transport", value)) { _Transport = value; OnPropertyChanged("Transport"); } } }

    private String _Executable;
    /// <summary>可执行文件</summary>
    [DisplayName("可执行文件")]
    [Description("可执行文件")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Executable", "可执行文件", "")]
    public String Executable { get => _Executable; set { if (OnPropertyChanging("Executable", value)) { _Executable = value; OnPropertyChanged("Executable"); } } }

    private String _ArgsTemplate;
    /// <summary>参数模板</summary>
    [DisplayName("参数模板")]
    [Description("参数模板")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("ArgsTemplate", "参数模板", "")]
    public String ArgsTemplate { get => _ArgsTemplate; set { if (OnPropertyChanging("ArgsTemplate", value)) { _ArgsTemplate = value; OnPropertyChanged("ArgsTemplate"); } } }

    private String _EnvJson;
    /// <summary>环境变量映射 JSON（只存名）</summary>
    [DisplayName("环境变量映射JSON（只存名）")]
    [Description("环境变量映射 JSON（只存名）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("EnvJson", "环境变量映射 JSON（只存名）", "")]
    public String EnvJson { get => _EnvJson; set { if (OnPropertyChanging("EnvJson", value)) { _EnvJson = value; OnPropertyChanged("EnvJson"); } } }

    private String _PromptInjection;
    /// <summary>提示词注入方式（Arg|Stdin）</summary>
    [DisplayName("提示词注入方式（Arg|Stdin）")]
    [Description("提示词注入方式（Arg|Stdin）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("PromptInjection", "提示词注入方式（Arg|Stdin）", "", DefaultValue = "Arg")]
    public String PromptInjection { get => _PromptInjection; set { if (OnPropertyChanging("PromptInjection", value)) { _PromptInjection = value; OnPropertyChanged("PromptInjection"); } } }

    private String _OutputFormat;
    /// <summary>输出格式（Text|JsonLines|Json）</summary>
    [DisplayName("输出格式（Text|JsonLines|Json）")]
    [Description("输出格式（Text|JsonLines|Json）")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("OutputFormat", "输出格式（Text|JsonLines|Json）", "", DefaultValue = "Text")]
    public String OutputFormat { get => _OutputFormat; set { if (OnPropertyChanging("OutputFormat", value)) { _OutputFormat = value; OnPropertyChanged("OutputFormat"); } } }

    private String _OutputMappingJson;
    /// <summary>输出字段映射 JSON（L2）</summary>
    [DisplayName("输出字段映射JSON（L2）")]
    [Description("输出字段映射 JSON（L2）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("OutputMappingJson", "输出字段映射 JSON（L2）", "")]
    public String OutputMappingJson { get => _OutputMappingJson; set { if (OnPropertyChanging("OutputMappingJson", value)) { _OutputMappingJson = value; OnPropertyChanged("OutputMappingJson"); } } }

    private String _SessionFlagTemplate;
    /// <summary>会话参数模板</summary>
    [DisplayName("会话参数模板")]
    [Description("会话参数模板")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("SessionFlagTemplate", "会话参数模板", "")]
    public String SessionFlagTemplate { get => _SessionFlagTemplate; set { if (OnPropertyChanging("SessionFlagTemplate", value)) { _SessionFlagTemplate = value; OnPropertyChanged("SessionFlagTemplate"); } } }

    private Boolean _CancelSupported;
    /// <summary>支持取消</summary>
    [DisplayName("支持取消")]
    [Description("支持取消")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CancelSupported", "支持取消", "", DefaultValue = "0")]
    public Boolean CancelSupported { get => _CancelSupported; set { if (OnPropertyChanging("CancelSupported", value)) { _CancelSupported = value; OnPropertyChanged("CancelSupported"); } } }

    private String _ProbeArgs;
    /// <summary>探测参数</summary>
    [DisplayName("探测参数")]
    [Description("探测参数")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ProbeArgs", "探测参数", "")]
    public String ProbeArgs { get => _ProbeArgs; set { if (OnPropertyChanging("ProbeArgs", value)) { _ProbeArgs = value; OnPropertyChanged("ProbeArgs"); } } }

    private String _Health;
    /// <summary>健康状态</summary>
    [DisplayName("健康状态")]
    [Description("健康状态")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Health", "健康状态", "", DefaultValue = "Unknown")]
    public String Health { get => _Health; set { if (OnPropertyChanging("Health", value)) { _Health = value; OnPropertyChanged("Health"); } } }

    private DateTime _LastProbeTime;
    /// <summary>最近探测时间</summary>
    [DisplayName("最近探测时间")]
    [Description("最近探测时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastProbeTime", "最近探测时间", "")]
    public DateTime LastProbeTime { get => _LastProbeTime; set { if (OnPropertyChanging("LastProbeTime", value)) { _LastProbeTime = value; OnPropertyChanged("LastProbeTime"); } } }

    private String _LastVersion;
    /// <summary>探测版本</summary>
    [DisplayName("探测版本")]
    [Description("探测版本")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("LastVersion", "探测版本", "")]
    public String LastVersion { get => _LastVersion; set { if (OnPropertyChanging("LastVersion", value)) { _LastVersion = value; OnPropertyChanged("LastVersion"); } } }

    private String _LastError;
    /// <summary>最近错误</summary>
    [DisplayName("最近错误")]
    [Description("最近错误")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("LastError", "最近错误", "")]
    public String LastError { get => _LastError; set { if (OnPropertyChanging("LastError", value)) { _LastError = value; OnPropertyChanged("LastError"); } } }

    private Boolean _IsDefault;
    /// <summary>是否默认</summary>
    [DisplayName("是否默认")]
    [Description("是否默认")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsDefault", "是否默认", "", DefaultValue = "0")]
    public Boolean IsDefault { get => _IsDefault; set { if (OnPropertyChanging("IsDefault", value)) { _IsDefault = value; OnPropertyChanged("IsDefault"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
            "AgentId" => _AgentId,
            "Mode" => _Mode,
            "Transport" => _Transport,
            "Executable" => _Executable,
            "ArgsTemplate" => _ArgsTemplate,
            "EnvJson" => _EnvJson,
            "PromptInjection" => _PromptInjection,
            "OutputFormat" => _OutputFormat,
            "OutputMappingJson" => _OutputMappingJson,
            "SessionFlagTemplate" => _SessionFlagTemplate,
            "CancelSupported" => _CancelSupported,
            "ProbeArgs" => _ProbeArgs,
            "Health" => _Health,
            "LastProbeTime" => _LastProbeTime,
            "LastVersion" => _LastVersion,
            "LastError" => _LastError,
            "IsDefault" => _IsDefault,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "AgentId": _AgentId = value.ToInt(); break;
                case "Mode": _Mode = Convert.ToString(value); break;
                case "Transport": _Transport = Convert.ToString(value); break;
                case "Executable": _Executable = Convert.ToString(value); break;
                case "ArgsTemplate": _ArgsTemplate = Convert.ToString(value); break;
                case "EnvJson": _EnvJson = Convert.ToString(value); break;
                case "PromptInjection": _PromptInjection = Convert.ToString(value); break;
                case "OutputFormat": _OutputFormat = Convert.ToString(value); break;
                case "OutputMappingJson": _OutputMappingJson = Convert.ToString(value); break;
                case "SessionFlagTemplate": _SessionFlagTemplate = Convert.ToString(value); break;
                case "CancelSupported": _CancelSupported = value.ToBoolean(); break;
                case "ProbeArgs": _ProbeArgs = Convert.ToString(value); break;
                case "Health": _Health = Convert.ToString(value); break;
                case "LastProbeTime": _LastProbeTime = value.ToDateTime(); break;
                case "LastVersion": _LastVersion = Convert.ToString(value); break;
                case "LastError": _LastError = Convert.ToString(value); break;
                case "IsDefault": _IsDefault = value.ToBoolean(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static AgentAccessPoint FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="agentId">所属 Agent</param>
    /// <param name="cancelSupported">支持取消</param>
    /// <param name="isDefault">是否默认</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AgentAccessPoint> Search(Int32 agentId, Boolean? cancelSupported, Boolean? isDefault, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (agentId >= 0) exp &= _.AgentId == agentId;
        if (cancelSupported != null) exp &= _.CancelSupported == cancelSupported;
        if (isDefault != null) exp &= _.IsDefault == isDefault;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得Agent交互口字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>所属 Agent</summary>
        public static readonly Field AgentId = FindByName("AgentId");

        /// <summary>能力等级（OneShot|Sessionful）</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>传输方式（Cli|Acp|Http）</summary>
        public static readonly Field Transport = FindByName("Transport");

        /// <summary>可执行文件</summary>
        public static readonly Field Executable = FindByName("Executable");

        /// <summary>参数模板</summary>
        public static readonly Field ArgsTemplate = FindByName("ArgsTemplate");

        /// <summary>环境变量映射 JSON（只存名）</summary>
        public static readonly Field EnvJson = FindByName("EnvJson");

        /// <summary>提示词注入方式（Arg|Stdin）</summary>
        public static readonly Field PromptInjection = FindByName("PromptInjection");

        /// <summary>输出格式（Text|JsonLines|Json）</summary>
        public static readonly Field OutputFormat = FindByName("OutputFormat");

        /// <summary>输出字段映射 JSON（L2）</summary>
        public static readonly Field OutputMappingJson = FindByName("OutputMappingJson");

        /// <summary>会话参数模板</summary>
        public static readonly Field SessionFlagTemplate = FindByName("SessionFlagTemplate");

        /// <summary>支持取消</summary>
        public static readonly Field CancelSupported = FindByName("CancelSupported");

        /// <summary>探测参数</summary>
        public static readonly Field ProbeArgs = FindByName("ProbeArgs");

        /// <summary>健康状态</summary>
        public static readonly Field Health = FindByName("Health");

        /// <summary>最近探测时间</summary>
        public static readonly Field LastProbeTime = FindByName("LastProbeTime");

        /// <summary>探测版本</summary>
        public static readonly Field LastVersion = FindByName("LastVersion");

        /// <summary>最近错误</summary>
        public static readonly Field LastError = FindByName("LastError");

        /// <summary>是否默认</summary>
        public static readonly Field IsDefault = FindByName("IsDefault");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得Agent交互口字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>所属 Agent</summary>
        public const String AgentId = "AgentId";

        /// <summary>能力等级（OneShot|Sessionful）</summary>
        public const String Mode = "Mode";

        /// <summary>传输方式（Cli|Acp|Http）</summary>
        public const String Transport = "Transport";

        /// <summary>可执行文件</summary>
        public const String Executable = "Executable";

        /// <summary>参数模板</summary>
        public const String ArgsTemplate = "ArgsTemplate";

        /// <summary>环境变量映射 JSON（只存名）</summary>
        public const String EnvJson = "EnvJson";

        /// <summary>提示词注入方式（Arg|Stdin）</summary>
        public const String PromptInjection = "PromptInjection";

        /// <summary>输出格式（Text|JsonLines|Json）</summary>
        public const String OutputFormat = "OutputFormat";

        /// <summary>输出字段映射 JSON（L2）</summary>
        public const String OutputMappingJson = "OutputMappingJson";

        /// <summary>会话参数模板</summary>
        public const String SessionFlagTemplate = "SessionFlagTemplate";

        /// <summary>支持取消</summary>
        public const String CancelSupported = "CancelSupported";

        /// <summary>探测参数</summary>
        public const String ProbeArgs = "ProbeArgs";

        /// <summary>健康状态</summary>
        public const String Health = "Health";

        /// <summary>最近探测时间</summary>
        public const String LastProbeTime = "LastProbeTime";

        /// <summary>探测版本</summary>
        public const String LastVersion = "LastVersion";

        /// <summary>最近错误</summary>
        public const String LastError = "LastError";

        /// <summary>是否默认</summary>
        public const String IsDefault = "IsDefault";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
