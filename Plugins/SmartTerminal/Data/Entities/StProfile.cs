using System;
using NewLife;
using System.ComponentModel;
using System.Runtime.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>已保存的本机/SSH 配置</summary>
[Serializable]
[DataObject]
[Description("已保存的本机/SSH 配置")]
[BindIndex("IU_StProfile_PublicId", true, "PublicId")]
[BindTable("StProfile", Description = "已保存的本机/SSH 配置", ConnName = "SmartTerminal", DbType = DatabaseType.None)]
public partial class StProfile
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
    /// <summary>对外 id，如 p-ssh-api</summary>
    [DisplayName("对外 id，如 p-ssh-api")]
    [Description("对外 id，如 p-ssh-api")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("PublicId", "对外 id，如 p-ssh-api", "", Master = true)]
    public String PublicId { get => _PublicId; set { if (OnPropertyChanging("PublicId", value)) { _PublicId = value; OnPropertyChanged("PublicId"); } } }

    private String _Kind;
    /// <summary>local 或 ssh</summary>
    [DisplayName("local 或 ssh")]
    [Description("local 或 ssh")]
    [DataObjectField(false, false, false, 16)]
    [BindColumn("Kind", "local 或 ssh", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Name;
    /// <summary>展示名</summary>
    [DisplayName("展示名")]
    [Description("展示名")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "展示名", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _ShellId;
    /// <summary>本机外壳 id</summary>
    [DisplayName("本机外壳 id")]
    [Description("本机外壳 id")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("ShellId", "本机外壳 id", "")]
    public String ShellId { get => _ShellId; set { if (OnPropertyChanging("ShellId", value)) { _ShellId = value; OnPropertyChanged("ShellId"); } } }

    private String _Cwd;
    /// <summary>起始目录</summary>
    [DisplayName("起始目录")]
    [Description("起始目录")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Cwd", "起始目录", "")]
    public String Cwd { get => _Cwd; set { if (OnPropertyChanging("Cwd", value)) { _Cwd = value; OnPropertyChanged("Cwd"); } } }

    private String _Host;
    /// <summary>SSH 主机</summary>
    [DisplayName("SSH 主机")]
    [Description("SSH 主机")]
    [DataObjectField(false, false, true, 255)]
    [BindColumn("Host", "SSH 主机", "")]
    public String Host { get => _Host; set { if (OnPropertyChanging("Host", value)) { _Host = value; OnPropertyChanged("Host"); } } }

    private Int32 _Port;
    /// <summary>SSH 端口</summary>
    [DisplayName("SSH 端口")]
    [Description("SSH 端口")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Port", "SSH 端口", "", DefaultValue = "22")]
    public Int32 Port { get => _Port; set { if (OnPropertyChanging("Port", value)) { _Port = value; OnPropertyChanged("Port"); } } }

    private String _User;
    /// <summary>SSH 用户</summary>
    [DisplayName("SSH 用户")]
    [Description("SSH 用户")]
    [DataObjectField(false, false, true, 128)]
    [BindColumn("User", "SSH 用户", "")]
    public String User { get => _User; set { if (OnPropertyChanging("User", value)) { _User = value; OnPropertyChanged("User"); } } }

    private String _Auth;
    /// <summary>key 或 interactive</summary>
    [DisplayName("key 或 interactive")]
    [Description("key 或 interactive")]
    [DataObjectField(false, false, true, 32)]
    [BindColumn("Auth", "key 或 interactive", "")]
    public String Auth { get => _Auth; set { if (OnPropertyChanging("Auth", value)) { _Auth = value; OnPropertyChanged("Auth"); } } }

    private String _SecretId;
    /// <summary>宿主凭据句柄</summary>
    [DisplayName("宿主凭据句柄")]
    [Description("宿主凭据句柄")]
    [DataObjectField(false, false, true, 64)]
    [BindColumn("SecretId", "宿主凭据句柄", "")]
    public String SecretId { get => _SecretId; set { if (OnPropertyChanging("SecretId", value)) { _SecretId = value; OnPropertyChanged("SecretId"); } } }

    private String _IdentityFile;
    /// <summary>本机私钥路径</summary>
    [DisplayName("本机私钥路径")]
    [Description("本机私钥路径")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("IdentityFile", "本机私钥路径", "")]
    public String IdentityFile { get => _IdentityFile; set { if (OnPropertyChanging("IdentityFile", value)) { _IdentityFile = value; OnPropertyChanged("IdentityFile"); } } }

    private String _Source;
    /// <summary>ui / agent / system</summary>
    [DisplayName("ui / agent / system")]
    [Description("ui / agent / system")]
    [DataObjectField(false, false, false, 16)]
    [BindColumn("Source", "ui / agent / system", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

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
            "PublicId" => _PublicId,
            "Kind" => _Kind,
            "Name" => _Name,
            "ShellId" => _ShellId,
            "Cwd" => _Cwd,
            "Host" => _Host,
            "Port" => _Port,
            "User" => _User,
            "Auth" => _Auth,
            "SecretId" => _SecretId,
            "IdentityFile" => _IdentityFile,
            "Source" => _Source,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "PublicId": _PublicId = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ShellId": _ShellId = Convert.ToString(value); break;
                case "Cwd": _Cwd = Convert.ToString(value); break;
                case "Host": _Host = Convert.ToString(value); break;
                case "Port": _Port = value.ToInt(); break;
                case "User": _User = Convert.ToString(value); break;
                case "Auth": _Auth = Convert.ToString(value); break;
                case "SecretId": _SecretId = Convert.ToString(value); break;
                case "IdentityFile": _IdentityFile = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得已保存的本机/SSH 配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>对外 id，如 p-ssh-api</summary>
        public static readonly Field PublicId = FindByName("PublicId");

        /// <summary>local 或 ssh</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>展示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>本机外壳 id</summary>
        public static readonly Field ShellId = FindByName("ShellId");

        /// <summary>起始目录</summary>
        public static readonly Field Cwd = FindByName("Cwd");

        /// <summary>SSH 主机</summary>
        public static readonly Field Host = FindByName("Host");

        /// <summary>SSH 端口</summary>
        public static readonly Field Port = FindByName("Port");

        /// <summary>SSH 用户</summary>
        public static readonly Field User = FindByName("User");

        /// <summary>key 或 interactive</summary>
        public static readonly Field Auth = FindByName("Auth");

        /// <summary>宿主凭据句柄</summary>
        public static readonly Field SecretId = FindByName("SecretId");

        /// <summary>本机私钥路径</summary>
        public static readonly Field IdentityFile = FindByName("IdentityFile");

        /// <summary>ui / agent / system</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得已保存的本机/SSH 配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>对外 id，如 p-ssh-api</summary>
        public const String PublicId = "PublicId";

        /// <summary>local 或 ssh</summary>
        public const String Kind = "Kind";

        /// <summary>展示名</summary>
        public const String Name = "Name";

        /// <summary>本机外壳 id</summary>
        public const String ShellId = "ShellId";

        /// <summary>起始目录</summary>
        public const String Cwd = "Cwd";

        /// <summary>SSH 主机</summary>
        public const String Host = "Host";

        /// <summary>SSH 端口</summary>
        public const String Port = "Port";

        /// <summary>SSH 用户</summary>
        public const String User = "User";

        /// <summary>key 或 interactive</summary>
        public const String Auth = "Auth";

        /// <summary>宿主凭据句柄</summary>
        public const String SecretId = "SecretId";

        /// <summary>本机私钥路径</summary>
        public const String IdentityFile = "IdentityFile";

        /// <summary>ui / agent / system</summary>
        public const String Source = "Source";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
