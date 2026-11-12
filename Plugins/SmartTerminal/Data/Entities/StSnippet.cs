using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>命令片段</summary>
[Serializable]
[DataObject]
[Description("命令片段")]
[BindIndex("IU_StSnippet_PublicId", true, "PublicId")]
[BindTable("StSnippet", Description = "命令片段", ConnName = "SmartTerminal", DbType = DatabaseType.None)]
public partial class StSnippet
{
    #region 属性
    private Int64 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _PublicId;
    /// <summary>对外 id，如 snip-1</summary>
    [DisplayName("对外 id，如 snip-1")]
    [Description("对外 id，如 snip-1")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("PublicId", "对外 id，如 snip-1", "", Master = true)]
    public String PublicId { get => _PublicId; set { if (OnPropertyChanging("PublicId", value)) { _PublicId = value; OnPropertyChanged("PublicId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Command;
    /// <summary>命令原文</summary>
    [DisplayName("命令原文")]
    [Description("命令原文")]
    [DataObjectField(false, false, false, 4000)]
    [BindColumn("Command", "命令原文", "")]
    public String Command { get => _Command; set { if (OnPropertyChanging("Command", value)) { _Command = value; OnPropertyChanged("Command"); } } }

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
}
