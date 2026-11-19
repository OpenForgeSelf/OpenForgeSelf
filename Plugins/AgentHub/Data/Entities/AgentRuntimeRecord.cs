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

/// <summary>底层 Runtime 审计 Record（脱敏；对外不暴露 OarSessionId）。</summary>
[Serializable]
[DataObject]
[Description("底层 Runtime 审计 Record（脱敏）")]
[BindIndex("IX_AgentRuntimeRecord_SessionKey_AgentHubSequence", false, "SessionKey,AgentHubSequence")]
[BindIndex("IX_AgentRuntimeRecord_AgentHubSequence", false, "AgentHubSequence")]
[BindTable("AgentRuntimeRecord", Description = "底层 Runtime 审计 Record（脱敏）", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class AgentRuntimeRecord
{
    #region 属性
    private Int32 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SessionKey;
    /// <summary>对外会话键</summary>
    [DisplayName("对外会话键")]
    [Description("对外会话键")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("SessionKey", "对外会话键", "")]
    public String SessionKey { get => _SessionKey; set { if (OnPropertyChanging("SessionKey", value)) { _SessionKey = value; OnPropertyChanged("SessionKey"); } } }

    private Int32 _TaskId;
    /// <summary>关联任务（可 0）</summary>
    [DisplayName("关联任务")]
    [Description("关联任务（可 0）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TaskId", "关联任务（可 0）", "", DefaultValue = "0")]
    public Int32 TaskId { get => _TaskId; set { if (OnPropertyChanging("TaskId", value)) { _TaskId = value; OnPropertyChanged("TaskId"); } } }

    private Int64 _OarSequence;
    /// <summary>OAR/Worker 序号</summary>
    [DisplayName("OAR_Worker序号")]
    [Description("OAR/Worker 序号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OarSequence", "OAR/Worker 序号", "", DefaultValue = "0")]
    public Int64 OarSequence { get => _OarSequence; set { if (OnPropertyChanging("OarSequence", value)) { _OarSequence = value; OnPropertyChanged("OarSequence"); } } }

    private Int64 _AgentHubSequence;
    /// <summary>铸己匣全局序号</summary>
    [DisplayName("铸己匣全局序号")]
    [Description("铸己匣全局序号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AgentHubSequence", "铸己匣全局序号", "", DefaultValue = "0")]
    public Int64 AgentHubSequence { get => _AgentHubSequence; set { if (OnPropertyChanging("AgentHubSequence", value)) { _AgentHubSequence = value; OnPropertyChanged("AgentHubSequence"); } } }

    private Int32 _Generation;
    /// <summary>会话世代</summary>
    [DisplayName("会话世代")]
    [Description("会话世代")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Generation", "会话世代", "", DefaultValue = "1")]
    public Int32 Generation { get => _Generation; set { if (OnPropertyChanging("Generation", value)) { _Generation = value; OnPropertyChanged("Generation"); } } }

    private String _Type;
    /// <summary>记录类型</summary>
    [DisplayName("记录类型")]
    [Description("记录类型")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("Type", "记录类型", "", DefaultValue = "record")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _PayloadJson;
    /// <summary>脱敏载荷 JSON</summary>
    [DisplayName("脱敏载荷JSON")]
    [Description("脱敏载荷 JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("PayloadJson", "脱敏载荷 JSON", "")]
    public String PayloadJson { get => _PayloadJson; set { if (OnPropertyChanging("PayloadJson", value)) { _PayloadJson = value; OnPropertyChanged("PayloadJson"); } } }

    private Boolean _Truncated;
    /// <summary>已截断</summary>
    [DisplayName("已截断")]
    [Description("已截断")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Truncated", "已截断", "", DefaultValue = "0")]
    public Boolean Truncated { get => _Truncated; set { if (OnPropertyChanging("Truncated", value)) { _Truncated = value; OnPropertyChanged("Truncated"); } } }

    private DateTime _Timestamp;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Timestamp", "时间", "")]
    public DateTime Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <inheritdoc />
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "SessionKey" => _SessionKey,
            "TaskId" => _TaskId,
            "OarSequence" => _OarSequence,
            "AgentHubSequence" => _AgentHubSequence,
            "Generation" => _Generation,
            "Type" => _Type,
            "PayloadJson" => _PayloadJson,
            "Truncated" => _Truncated,
            "Timestamp" => _Timestamp,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "SessionKey": _SessionKey = Convert.ToString(value); break;
                case "TaskId": _TaskId = value.ToInt(); break;
                case "OarSequence": _OarSequence = value.ToLong(); break;
                case "AgentHubSequence": _AgentHubSequence = value.ToLong(); break;
                case "Generation": _Generation = value.ToInt(); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "PayloadJson": _PayloadJson = Convert.ToString(value); break;
                case "Truncated": _Truncated = value.ToBoolean(); break;
                case "Timestamp": _Timestamp = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>字段信息快捷方式</summary>
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field SessionKey = FindByName("SessionKey");
        public static readonly Field TaskId = FindByName("TaskId");
        public static readonly Field OarSequence = FindByName("OarSequence");
        public static readonly Field AgentHubSequence = FindByName("AgentHubSequence");
        public static readonly Field Generation = FindByName("Generation");
        public static readonly Field Type = FindByName("Type");
        public static readonly Field PayloadJson = FindByName("PayloadJson");
        public static readonly Field Truncated = FindByName("Truncated");
        public static readonly Field Timestamp = FindByName("Timestamp");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>字段名称快捷方式</summary>
    public partial class __
    {
        public const String Id = "Id";
        public const String SessionKey = "SessionKey";
        public const String TaskId = "TaskId";
        public const String OarSequence = "OarSequence";
        public const String AgentHubSequence = "AgentHubSequence";
        public const String Generation = "Generation";
        public const String Type = "Type";
        public const String PayloadJson = "PayloadJson";
        public const String Truncated = "Truncated";
        public const String Timestamp = "Timestamp";
    }
    #endregion
}
