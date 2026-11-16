using System;
using NewLife;
using System.ComponentModel;
using System.Runtime.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>凭据句柄（材料由宿主加密保管）</summary>
[Serializable]
[DataObject]
[Description("凭据句柄（材料由宿主加密保管）")]
[BindIndex("IU_StSecretRef_PublicId", true, "PublicId")]
[BindTable("StSecretRef", Description = "凭据句柄（材料由宿主加密保管）", ConnName = "SmartTerminal", DbType = DatabaseType.None)]
public partial class StSecretRef
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
    /// <summary>对外 id，如 sec-1</summary>
    [DisplayName("对外 id，如 sec-1")]
    [Description("对外 id，如 sec-1")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("PublicId", "对外 id，如 sec-1", "", Master = true)]
    public String PublicId { get => _PublicId; set { if (OnPropertyChanging("PublicId", value)) { _PublicId = value; OnPropertyChanged("PublicId"); } } }

    private String _Kind;
    /// <summary>ssh-private-key 或 ssh-certificate</summary>
    [DisplayName("ssh-private-key 或 ssh-certificate")]
    [Description("ssh-private-key 或 ssh-certificate")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("Kind", "ssh-private-key 或 ssh-certificate", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Fingerprint;
    /// <summary>可展示指纹</summary>
    [DisplayName("可展示指纹")]
    [Description("可展示指纹")]
    [DataObjectField(false, false, true, 128)]
    [BindColumn("Fingerprint", "可展示指纹", "")]
    public String Fingerprint { get => _Fingerprint; set { if (OnPropertyChanging("Fingerprint", value)) { _Fingerprint = value; OnPropertyChanged("Fingerprint"); } } }

    private String _Label;
    /// <summary>展示名</summary>
    [DisplayName("展示名")]
    [Description("展示名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Label", "展示名", "")]
    public String Label { get => _Label; set { if (OnPropertyChanging("Label", value)) { _Label = value; OnPropertyChanged("Label"); } } }

    private String _Cipher;
    /// <summary>宿主加密密文，接口永不输出</summary>
    [DisplayName("宿主加密密文，接口永不输出")]
    [Description("宿主加密密文，接口永不输出")]
    [DataObjectField(false, false, true, 8000)]
    [BindColumn("Cipher", "宿主加密密文，接口永不输出", "")]
    public String Cipher { get => _Cipher; set { if (OnPropertyChanging("Cipher", value)) { _Cipher = value; OnPropertyChanged("Cipher"); } } }

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
            "PublicId" => _PublicId,
            "Kind" => _Kind,
            "Fingerprint" => _Fingerprint,
            "Label" => _Label,
            "Cipher" => _Cipher,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "PublicId": _PublicId = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Fingerprint": _Fingerprint = Convert.ToString(value); break;
                case "Label": _Label = Convert.ToString(value); break;
                case "Cipher": _Cipher = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>取得密钥引用字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>对外 id</summary>
        public static readonly Field PublicId = FindByName("PublicId");

        /// <summary>类型</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>指纹</summary>
        public static readonly Field Fingerprint = FindByName("Fingerprint");

        /// <summary>标签</summary>
        public static readonly Field Label = FindByName("Label");

        /// <summary>密文</summary>
        public static readonly Field Cipher = FindByName("Cipher");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得密钥引用字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>对外 id</summary>
        public const String PublicId = "PublicId";

        /// <summary>类型</summary>
        public const String Kind = "Kind";

        /// <summary>指纹</summary>
        public const String Fingerprint = "Fingerprint";

        /// <summary>标签</summary>
        public const String Label = "Label";

        /// <summary>密文</summary>
        public const String Cipher = "Cipher";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
