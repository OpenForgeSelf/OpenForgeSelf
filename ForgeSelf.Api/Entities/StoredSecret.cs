using System;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>凭据保险库：存储的机密（AES-256-GCM 信封加密，ConnName=ForgeSelf）。</summary>
/// <remarks>
/// 方案真源：<c>docs/08-security/credential-vault-design.md</c> §4/§5（PILOT-086）。
/// 库里只存信封材料（WrappedDek/WrapNonce/WrapTag/Nonce/Ciphertext/Tag/KeyId/FormatVersion），
/// <b>任何字段都不含明文</b>；本实体<b>绝不</b>直接作为 HTTP 响应返回，必须用 DTO 投影，
/// 否则信封材料会外泄（明文红线，同 ApiKeyCredential 先例）。
/// AAD 绑定 RefId/OwnerType/OwnerId/RotationVersion——行内绑定字段被改即不可解（Fail Closed）。
/// </remarks>
[Serializable]
[DataObject]
[Description("凭据保险库：存储的机密。")]
[BindIndex("UX_StoredSecret_RefId", true, "RefId")]
[BindIndex("IX_StoredSecret_Name", false, "Name")]
[BindTable("StoredSecret", Description = "凭据保险库：存储的机密", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class StoredSecret : IStoredSecretModel, IEntity<IStoredSecretModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>凭据ID</summary>
    [DisplayName("凭据ID")]
    [Description("凭据ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "凭据ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _RefId;
    /// <summary>引用ID（sec_ + 32 位小写 hex，唯一索引；SecretRef 引用携带它定位）</summary>
    [DisplayName("引用ID")]
    [Description("引用ID，sec_ + 32 位小写 hex，唯一索引")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("RefId", "引用ID", "")]
    public String RefId { get => _RefId; set { if (OnPropertyChanging("RefId", value)) { _RefId = value; OnPropertyChanged("RefId"); } } }

    private String _Name;
    /// <summary>凭据名称（展示用，非唯一键）</summary>
    [DisplayName("凭据名称")]
    [Description("凭据名称")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "凭据名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _SecretType;
    /// <summary>凭据类型：ApiKey | Token | Password | PrivateKey | Custom</summary>
    [DisplayName("凭据类型")]
    [Description("凭据类型：ApiKey/Token/Password/PrivateKey/Custom")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("SecretType", "凭据类型", "")]
    public String SecretType { get => _SecretType; set { if (OnPropertyChanging("SecretType", value)) { _SecretType = value; OnPropertyChanged("SecretType"); } } }

    private String _Provider;
    /// <summary>提供方提示（如 openai、github；仅展示与检索用，非机密）</summary>
    [DisplayName("提供方")]
    [Description("提供方提示，仅展示与检索用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "提供方提示", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String _OwnerType;
    /// <summary>所有者类型（host | agent | plugin；进 AAD 绑定）</summary>
    [DisplayName("所有者类型")]
    [Description("所有者类型，进 AAD 绑定")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("OwnerType", "所有者类型", "")]
    public String OwnerType { get => _OwnerType; set { if (OnPropertyChanging("OwnerType", value)) { _OwnerType = value; OnPropertyChanged("OwnerType"); } } }

    private String _OwnerId;
    /// <summary>所有者标识（如 host / ai-agent；进 AAD 绑定）</summary>
    [DisplayName("所有者标识")]
    [Description("所有者标识，进 AAD 绑定")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("OwnerId", "所有者标识", "")]
    public String OwnerId { get => _OwnerId; set { if (OnPropertyChanging("OwnerId", value)) { _OwnerId = value; OnPropertyChanged("OwnerId"); } } }

    private String _MetadataJson;
    /// <summary>元数据 JSON（严禁放机密值；≤2000 字符）</summary>
    [DisplayName("元数据")]
    [Description("元数据 JSON，严禁放机密值")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("MetadataJson", "元数据 JSON", "")]
    public String MetadataJson { get => _MetadataJson; set { if (OnPropertyChanging("MetadataJson", value)) { _MetadataJson = value; OnPropertyChanged("MetadataJson"); } } }

    private String _FormatVersion;
    /// <summary>信封格式版本（ev1；进 AAD 绑定）</summary>
    [DisplayName("信封格式版本")]
    [Description("信封格式版本，进 AAD 绑定")]
    [DataObjectField(false, false, false, 8)]
    [BindColumn("FormatVersion", "信封格式版本", "")]
    public String FormatVersion { get => _FormatVersion; set { if (OnPropertyChanging("FormatVersion", value)) { _FormatVersion = value; OnPropertyChanged("FormatVersion"); } } }

    private String _KeyId;
    /// <summary>包封 KEK 的 KeyId（kek1- + 8 位指纹 hex；轮换后用于识别旧包封密钥）</summary>
    [DisplayName("包封密钥ID")]
    [Description("包封 KEK 的 KeyId")]
    [DataObjectField(false, false, false, 40)]
    [BindColumn("KeyId", "包封密钥ID", "")]
    public String KeyId { get => _KeyId; set { if (OnPropertyChanging("KeyId", value)) { _KeyId = value; OnPropertyChanged("KeyId"); } } }

    private String _WrappedDek;
    /// <summary>被 KEK 包封的随机 DEK（AES-GCM 密文，base64）</summary>
    [DisplayName("包封DEK")]
    [Description("被 KEK 包封的随机 DEK，base64")]
    [DataObjectField(false, false, false, 128)]
    [BindColumn("WrappedDek", "包封DEK", "")]
    public String WrappedDek { get => _WrappedDek; set { if (OnPropertyChanging("WrappedDek", value)) { _WrappedDek = value; OnPropertyChanged("WrappedDek"); } } }

    private String _WrapNonce;
    /// <summary>DEK 包封 nonce（base64）</summary>
    [DisplayName("包封nonce")]
    [Description("DEK 包封 nonce，base64")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("WrapNonce", "包封nonce", "")]
    public String WrapNonce { get => _WrapNonce; set { if (OnPropertyChanging("WrapNonce", value)) { _WrapNonce = value; OnPropertyChanged("WrapNonce"); } } }

    private String _WrapTag;
    /// <summary>DEK 包封 tag（base64）</summary>
    [DisplayName("包封tag")]
    [Description("DEK 包封 tag，base64")]
    [DataObjectField(false, false, false, 32)]
    [BindColumn("WrapTag", "包封tag", "")]
    public String WrapTag { get => _WrapTag; set { if (OnPropertyChanging("WrapTag", value)) { _WrapTag = value; OnPropertyChanged("WrapTag"); } } }

    private String _Nonce;
    [DataObjectField(false, false, false, 32)]
    [BindColumn("Nonce", "数据加密 nonce，base64", "")]
    public String Nonce { get => _Nonce; set { if (OnPropertyChanging("Nonce", value)) { _Nonce = value; OnPropertyChanged("Nonce"); } } }

    private String _Ciphertext;
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Ciphertext", "数据密文，base64", "")]
    public String Ciphertext { get => _Ciphertext; set { if (OnPropertyChanging("Ciphertext", value)) { _Ciphertext = value; OnPropertyChanged("Ciphertext"); } } }

    private String _Tag;
    [DataObjectField(false, false, false, 32)]
    [BindColumn("Tag", "数据加密 tag，base64", "")]
    public String Tag { get => _Tag; set { if (OnPropertyChanging("Tag", value)) { _Tag = value; OnPropertyChanged("Tag"); } } }

    private Int32 _RotationVersion;
    /// <summary>轮值版本（从 1 起，每次 SetSecretValue +1；进 AAD 绑定）</summary>
    [DisplayName("轮值版本")]
    [Description("轮值版本，从 1 起，每次 SetSecretValue 加 1，进 AAD 绑定")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RotationVersion", "轮值版本", "")]
    public Int32 RotationVersion { get => _RotationVersion; set { if (OnPropertyChanging("RotationVersion", value)) { _RotationVersion = value; OnPropertyChanged("RotationVersion"); } } }

    private String _Status;
    /// <summary>状态：active | revoked（吊销后所有使用一律拒绝）</summary>
    [DisplayName("状态")]
    [Description("状态：active/revoked")]
    [DataObjectField(false, false, false, 10)]
    [BindColumn("Status", "状态", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _ExpiresAt;
    /// <summary>过期时间。MinValue 表示「不过期」</summary>
    [DisplayName("过期时间")]
    [Description("过期时间，MinValue 表示不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpiresAt", "过期时间", "")]
    public DateTime ExpiresAt { get => _ExpiresAt; set { if (OnPropertyChanging("ExpiresAt", value)) { _ExpiresAt = value; OnPropertyChanged("ExpiresAt"); } } }

    private DateTime _RevokedAt;
    /// <summary>吊销时间。MinValue 表示「未吊销」</summary>
    [DisplayName("吊销时间")]
    [Description("吊销时间，MinValue 表示未吊销")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("RevokedAt", "吊销时间", "")]
    public DateTime RevokedAt { get => _RevokedAt; set { if (OnPropertyChanging("RevokedAt", value)) { _RevokedAt = value; OnPropertyChanged("RevokedAt"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IStoredSecretModel model)
    {
        Id = model.Id;
        RefId = model.RefId;
        Name = model.Name;
        SecretType = model.SecretType;
        Provider = model.Provider;
        OwnerType = model.OwnerType;
        OwnerId = model.OwnerId;
        MetadataJson = model.MetadataJson;
        FormatVersion = model.FormatVersion;
        KeyId = model.KeyId;
        WrappedDek = model.WrappedDek;
        WrapNonce = model.WrapNonce;
        WrapTag = model.WrapTag;
        Nonce = model.Nonce;
        Ciphertext = model.Ciphertext;
        Tag = model.Tag;
        RotationVersion = model.RotationVersion;
        Status = model.Status;
        ExpiresAt = model.ExpiresAt;
        RevokedAt = model.RevokedAt;
        CreatedAt = model.CreatedAt;
        UpdatedAt = model.UpdatedAt;
    }
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
            "RefId" => _RefId,
            "Name" => _Name,
            "SecretType" => _SecretType,
            "Provider" => _Provider,
            "OwnerType" => _OwnerType,
            "OwnerId" => _OwnerId,
            "MetadataJson" => _MetadataJson,
            "FormatVersion" => _FormatVersion,
            "KeyId" => _KeyId,
            "WrappedDek" => _WrappedDek,
            "WrapNonce" => _WrapNonce,
            "WrapTag" => _WrapTag,
            "Nonce" => _Nonce,
            "Ciphertext" => _Ciphertext,
            "Tag" => _Tag,
            "RotationVersion" => _RotationVersion,
            "Status" => _Status,
            "ExpiresAt" => _ExpiresAt,
            "RevokedAt" => _RevokedAt,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RefId": _RefId = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "SecretType": _SecretType = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "OwnerType": _OwnerType = Convert.ToString(value); break;
                case "OwnerId": _OwnerId = Convert.ToString(value); break;
                case "MetadataJson": _MetadataJson = Convert.ToString(value); break;
                case "FormatVersion": _FormatVersion = Convert.ToString(value); break;
                case "KeyId": _KeyId = Convert.ToString(value); break;
                case "WrappedDek": _WrappedDek = Convert.ToString(value); break;
                case "WrapNonce": _WrapNonce = Convert.ToString(value); break;
                case "WrapTag": _WrapTag = Convert.ToString(value); break;
                case "Nonce": _Nonce = Convert.ToString(value); break;
                case "Ciphertext": _Ciphertext = Convert.ToString(value); break;
                case "Tag": _Tag = Convert.ToString(value); break;
                case "RotationVersion": _RotationVersion = value.ToInt(); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "ExpiresAt": _ExpiresAt = value.ToDateTime(); break;
                case "RevokedAt": _RevokedAt = value.ToDateTime(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>根据凭据ID查找</summary>
    /// <param name="id">凭据ID</param>
    /// <returns>实体对象</returns>
    public static StoredSecret? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据引用ID查找（唯一键，SecretRef 解引用热路径）</summary>
    /// <param name="refId">sec_ 前缀引用ID</param>
    /// <returns>实体对象；不存在返回 null</returns>
    public static StoredSecret? FindByRefId(String refId)
    {
        if (refId.IsNullOrEmpty()) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.RefId == refId);
        return Find(_.RefId == refId);
    }
    #endregion

    #region 字段名
    /// <summary>取得存储机密字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>凭据ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>引用ID</summary>
        public static readonly Field RefId = FindByName("RefId");
        /// <summary>凭据名称</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>凭据类型</summary>
        public static readonly Field SecretType = FindByName("SecretType");
        /// <summary>提供方</summary>
        public static readonly Field Provider = FindByName("Provider");
        /// <summary>所有者类型</summary>
        public static readonly Field OwnerType = FindByName("OwnerType");
        /// <summary>所有者标识</summary>
        public static readonly Field OwnerId = FindByName("OwnerId");
        /// <summary>元数据</summary>
        public static readonly Field MetadataJson = FindByName("MetadataJson");
        /// <summary>信封格式版本</summary>
        public static readonly Field FormatVersion = FindByName("FormatVersion");
        /// <summary>包封密钥ID</summary>
        public static readonly Field KeyId = FindByName("KeyId");
        /// <summary>包封DEK</summary>
        public static readonly Field WrappedDek = FindByName("WrappedDek");
        /// <summary>包封nonce</summary>
        public static readonly Field WrapNonce = FindByName("WrapNonce");
        /// <summary>包封tag</summary>
        public static readonly Field WrapTag = FindByName("WrapTag");
        /// <summary>数据加密nonce</summary>
        public static readonly Field Nonce = FindByName("Nonce");
        /// <summary>数据密文</summary>
        public static readonly Field Ciphertext = FindByName("Ciphertext");
        /// <summary>数据加密tag</summary>
        public static readonly Field Tag = FindByName("Tag");
        /// <summary>轮值版本</summary>
        public static readonly Field RotationVersion = FindByName("RotationVersion");
        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");
        /// <summary>过期时间</summary>
        public static readonly Field ExpiresAt = FindByName("ExpiresAt");
        /// <summary>吊销时间</summary>
        public static readonly Field RevokedAt = FindByName("RevokedAt");
        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得存储机密字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>凭据ID</summary>
        public const String Id = "Id";
        /// <summary>引用ID</summary>
        public const String RefId = "RefId";
        /// <summary>凭据名称</summary>
        public const String Name = "Name";
        /// <summary>凭据类型</summary>
        public const String SecretType = "SecretType";
        /// <summary>提供方</summary>
        public const String Provider = "Provider";
        /// <summary>所有者类型</summary>
        public const String OwnerType = "OwnerType";
        /// <summary>所有者标识</summary>
        public const String OwnerId = "OwnerId";
        /// <summary>元数据</summary>
        public const String MetadataJson = "MetadataJson";
        /// <summary>信封格式版本</summary>
        public const String FormatVersion = "FormatVersion";
        /// <summary>包封密钥ID</summary>
        public const String KeyId = "KeyId";
        /// <summary>包封DEK</summary>
        public const String WrappedDek = "WrappedDek";
        /// <summary>包封nonce</summary>
        public const String WrapNonce = "WrapNonce";
        /// <summary>包封tag</summary>
        public const String WrapTag = "WrapTag";
        /// <summary>数据加密nonce</summary>
        public const String Nonce = "Nonce";
        /// <summary>数据密文</summary>
        public const String Ciphertext = "Ciphertext";
        /// <summary>数据加密tag</summary>
        public const String Tag = "Tag";
        /// <summary>轮值版本</summary>
        public const String RotationVersion = "RotationVersion";
        /// <summary>状态</summary>
        public const String Status = "Status";
        /// <summary>过期时间</summary>
        public const String ExpiresAt = "ExpiresAt";
        /// <summary>吊销时间</summary>
        public const String RevokedAt = "RevokedAt";
        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}

/// <summary>存储机密模型接口（供 XCode 实体实现）。</summary>
public partial interface IStoredSecretModel
{
    Int64 Id { get; set; }
    String RefId { get; set; }
    String Name { get; set; }
    String SecretType { get; set; }
    String Provider { get; set; }
    String OwnerType { get; set; }
    String OwnerId { get; set; }
    String MetadataJson { get; set; }
    String FormatVersion { get; set; }
    String KeyId { get; set; }
    String WrappedDek { get; set; }
    String WrapNonce { get; set; }
    String WrapTag { get; set; }
    String Nonce { get; set; }
    String Ciphertext { get; set; }
    String Tag { get; set; }
    Int32 RotationVersion { get; set; }
    String Status { get; set; }
    DateTime ExpiresAt { get; set; }
    DateTime RevokedAt { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
