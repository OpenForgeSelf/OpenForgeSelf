using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Data.Entities;

/// <summary>监听器配置：监听地址/端口 + 可选目标地址/端口。目标为空表示「仅抓包、不转发」。</summary>
[Serializable]
[DataObject]
[Description("监听器配置")]
[BindTable("ListenerConfig", Description = "监听器配置", ConnName = "ProxyCapture", DbType = DatabaseType.None)]
public partial class ListenerConfig : Entity<ListenerConfig>
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")][Description("编号")][DataObjectField(true, true, false, 0)][BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    [DisplayName("名称")][Description("名称")][DataObjectField(false, false, false, 128)][BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _ListenAddress;
    [DisplayName("监听地址")][Description("监听地址")][DataObjectField(false, false, false, 64)][BindColumn("ListenAddress", "监听地址", "")]
    public String ListenAddress { get => _ListenAddress; set { if (OnPropertyChanging("ListenAddress", value)) { _ListenAddress = value; OnPropertyChanged("ListenAddress"); } } }

    private Int32 _ListenPort;
    [DisplayName("监听端口")][Description("监听端口")][DataObjectField(false, false, false, 0)][BindColumn("ListenPort", "监听端口", "")]
    public Int32 ListenPort { get => _ListenPort; set { if (OnPropertyChanging("ListenPort", value)) { _ListenPort = value; OnPropertyChanged("ListenPort"); } } }

    private String _TargetHost;
    [DisplayName("目标主机")][Description("目标主机")][DataObjectField(false, false, true, 256)][BindColumn("TargetHost", "目标主机", "")]
    public String TargetHost { get => _TargetHost; set { if (OnPropertyChanging("TargetHost", value)) { _TargetHost = value; OnPropertyChanged("TargetHost"); } } }

    private Int32 _TargetPort;
    [DisplayName("目标端口")][Description("目标端口")][DataObjectField(false, false, false, 0)][BindColumn("TargetPort", "目标端口", "")]
    public Int32 TargetPort { get => _TargetPort; set { if (OnPropertyChanging("TargetPort", value)) { _TargetPort = value; OnPropertyChanged("TargetPort"); } } }

    private Boolean _Enabled;
    [DisplayName("启用")][Description("是否启用")][DataObjectField(false, false, false, 0)][BindColumn("Enabled", "是否启用", "")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private String _Description;
    [DisplayName("描述")][Description("描述")][DataObjectField(false, false, true, 512)][BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private DateTime _CreatedAt;
    [DisplayName("创建时间")][Description("创建时间")][DataObjectField(false, false, true, 0)][BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    [DisplayName("更新时间")][Description("更新时间")][DataObjectField(false, false, true, 0)][BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id, "Name" => _Name, "ListenAddress" => _ListenAddress, "ListenPort" => _ListenPort,
            "TargetHost" => _TargetHost, "TargetPort" => _TargetPort, "Enabled" => _Enabled, "Description" => _Description,
            "CreatedAt" => _CreatedAt, "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ListenAddress": _ListenAddress = Convert.ToString(value); break;
                case "ListenPort": _ListenPort = value.ToInt(); break;
                case "TargetHost": _TargetHost = Convert.ToString(value); break;
                case "TargetPort": _TargetPort = value.ToInt(); break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field Name = FindByName("Name");
        public static readonly Field ListenAddress = FindByName("ListenAddress");
        public static readonly Field ListenPort = FindByName("ListenPort");
        public static readonly Field TargetHost = FindByName("TargetHost");
        public static readonly Field TargetPort = FindByName("TargetPort");
        public static readonly Field Enabled = FindByName("Enabled");
        public static readonly Field Description = FindByName("Description");
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    public partial class __
    {
        public const String Id = "Id";
        public const String Name = "Name";
        public const String ListenAddress = "ListenAddress";
        public const String ListenPort = "ListenPort";
        public const String TargetHost = "TargetHost";
        public const String TargetPort = "TargetPort";
        public const String Enabled = "Enabled";
        public const String Description = "Description";
        public const String CreatedAt = "CreatedAt";
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
