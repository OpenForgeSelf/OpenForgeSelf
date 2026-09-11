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

namespace ForgeSelf.Api.Entities;

/// <summary>API 子密钥凭据：与主密钥并列的额外访问凭证（ConnName=ForgeSelf）。</summary>
/// <remarks>
/// 明文仅在创建/重新生成的当次响应中出现一次，库里只存密文（<see cref="KeyCipher"/>）与
/// SHA-256 摘要（<see cref="KeyHash"/>，用于 O(1) 定位，避免逐条解密比对）。
/// 本实体<b>绝不</b>直接作为 HTTP 响应返回，必须用 DTO 投影，否则 KeyCipher/KeyHash 会外泄。
/// </remarks>
[Serializable]
[DataObject]
[Description("API 子密钥凭据。")]
[BindIndex("UX_ApiKeyCredential_KeyHash", true, "KeyHash")]
[BindIndex("IX_ApiKeyCredential_Enabled", false, "Enabled")]
[BindTable("ApiKeyCredential", Description = "API 子密钥凭据", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class ApiKeyCredential : IApiKeyCredentialModel, IEntity<IApiKeyCredentialModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>密钥ID</summary>
    [DisplayName("密钥ID")]
    [Description("密钥ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "密钥ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>密钥名称（可重命名，默认「密钥 N」）</summary>
    [DisplayName("密钥名称")]
    [Description("密钥名称（可重命名，默认「密钥 N」）")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "密钥名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Remark;
    /// <summary>用途备注（替代作用域设计）</summary>
    [DisplayName("用途备注")]
    [Description("用途备注（替代作用域设计）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "用途备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private String _KeyCipher;
    /// <summary>明文密钥经 ISecretEncryptionService 加密后的密文（写入即 v2）</summary>
    [DisplayName("密钥密文")]
    [Description("明文密钥加密后的密文")]
    [DataObjectField(false, false, false, 512)]
    [BindColumn("KeyCipher", "明文密钥加密后的密文", "")]
    public String KeyCipher { get => _KeyCipher; set { if (OnPropertyChanging("KeyCipher", value)) { _KeyCipher = value; OnPropertyChanged("KeyCipher"); } } }

    private String _KeyHash;
    /// <summary>明文的 SHA-256 小写 hex，用于按令牌 O(1) 定位（唯一索引）</summary>
    [DisplayName("密钥摘要")]
    [Description("明文的 SHA-256 小写 hex")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("KeyHash", "明文 SHA-256 摘要", "")]
    public String KeyHash { get => _KeyHash; set { if (OnPropertyChanging("KeyHash", value)) { _KeyHash = value; OnPropertyChanged("KeyHash"); } } }

    private String _KeyPrefix;
    /// <summary>掩码展示前缀（固定 sk-）</summary>
    [DisplayName("掩码前缀")]
    [Description("掩码展示前缀")]
    [DataObjectField(false, false, false, 8)]
    [BindColumn("KeyPrefix", "掩码展示前缀", "")]
    public String KeyPrefix { get => _KeyPrefix; set { if (OnPropertyChanging("KeyPrefix", value)) { _KeyPrefix = value; OnPropertyChanged("KeyPrefix"); } } }

    private Boolean _Enabled;
    /// <summary>启停开关（停用后该密钥立即 401，且不会回退到主密钥）</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enabled", "是否启用", "")]
    public Boolean Enabled { get => _Enabled; set { if (OnPropertyChanging("Enabled", value)) { _Enabled = value; OnPropertyChanged("Enabled"); } } }

    private DateTime _ExpiresAt;
    /// <summary>过期时间。MinValue 表示「不过期」</summary>
    [DisplayName("过期时间")]
    [Description("过期时间，MinValue 表示不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpiresAt", "过期时间", "")]
    public DateTime ExpiresAt { get => _ExpiresAt; set { if (OnPropertyChanging("ExpiresAt", value)) { _ExpiresAt = value; OnPropertyChanged("ExpiresAt"); } } }

    private DateTime _LastUsedAt;
    /// <summary>最后使用时间。MinValue 表示「从未使用」；认证命中时按 60 秒节流写入</summary>
    [DisplayName("最后使用")]
    [Description("最后使用时间，MinValue 表示从未使用")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastUsedAt", "最后使用时间", "")]
    public DateTime LastUsedAt { get => _LastUsedAt; set { if (OnPropertyChanging("LastUsedAt", value)) { _LastUsedAt = value; OnPropertyChanged("LastUsedAt"); } } }

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
    public void Copy(IApiKeyCredentialModel model)
    {
        Id = model.Id;
        Name = model.Name;
        Remark = model.Remark;
        KeyCipher = model.KeyCipher;
        KeyHash = model.KeyHash;
        KeyPrefix = model.KeyPrefix;
        Enabled = model.Enabled;
        ExpiresAt = model.ExpiresAt;
        LastUsedAt = model.LastUsedAt;
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
            "Name" => _Name,
            "Remark" => _Remark,
            "KeyCipher" => _KeyCipher,
            "KeyHash" => _KeyHash,
            "KeyPrefix" => _KeyPrefix,
            "Enabled" => _Enabled,
            "ExpiresAt" => _ExpiresAt,
            "LastUsedAt" => _LastUsedAt,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "KeyCipher": _KeyCipher = Convert.ToString(value); break;
                case "KeyHash": _KeyHash = Convert.ToString(value); break;
                case "KeyPrefix": _KeyPrefix = Convert.ToString(value); break;
                case "Enabled": _Enabled = value.ToBoolean(); break;
                case "ExpiresAt": _ExpiresAt = value.ToDateTime(); break;
                case "LastUsedAt": _LastUsedAt = value.ToDateTime(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据密钥ID查找</summary>
    /// <param name="id">密钥ID</param>
    /// <returns>实体对象</returns>
    public static ApiKeyCredential? FindById(Int64 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据明文摘要查找（唯一键，认证热路径）</summary>
    /// <param name="keyHash">明文 SHA-256 小写 hex</param>
    /// <returns>实体对象；不存在返回 null</returns>
    public static ApiKeyCredential? FindByKeyHash(String keyHash)
    {
        if (keyHash.IsNullOrEmpty()) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.KeyHash == keyHash);
        return Find(_.KeyHash == keyHash);
    }
    #endregion

    #region 字段名
    /// <summary>取得 API 子密钥凭据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>密钥ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>密钥名称</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>用途备注</summary>
        public static readonly Field Remark = FindByName("Remark");
        /// <summary>明文密钥加密后的密文</summary>
        public static readonly Field KeyCipher = FindByName("KeyCipher");
        /// <summary>明文 SHA-256 摘要</summary>
        public static readonly Field KeyHash = FindByName("KeyHash");
        /// <summary>掩码展示前缀</summary>
        public static readonly Field KeyPrefix = FindByName("KeyPrefix");
        /// <summary>是否启用</summary>
        public static readonly Field Enabled = FindByName("Enabled");
        /// <summary>过期时间</summary>
        public static readonly Field ExpiresAt = FindByName("ExpiresAt");
        /// <summary>最后使用时间</summary>
        public static readonly Field LastUsedAt = FindByName("LastUsedAt");
        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得 API 子密钥凭据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>密钥ID</summary>
        public const String Id = "Id";
        /// <summary>密钥名称</summary>
        public const String Name = "Name";
        /// <summary>用途备注</summary>
        public const String Remark = "Remark";
        /// <summary>明文密钥加密后的密文</summary>
        public const String KeyCipher = "KeyCipher";
        /// <summary>明文 SHA-256 摘要</summary>
        public const String KeyHash = "KeyHash";
        /// <summary>掩码展示前缀</summary>
        public const String KeyPrefix = "KeyPrefix";
        /// <summary>是否启用</summary>
        public const String Enabled = "Enabled";
        /// <summary>过期时间</summary>
        public const String ExpiresAt = "ExpiresAt";
        /// <summary>最后使用时间</summary>
        public const String LastUsedAt = "LastUsedAt";
        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}

/// <summary>API 子密钥凭据模型接口（供 XCode 实体实现）。</summary>
public partial interface IApiKeyCredentialModel
{
    Int64 Id { get; set; }
    String Name { get; set; }
    String Remark { get; set; }
    String KeyCipher { get; set; }
    String KeyHash { get; set; }
    String KeyPrefix { get; set; }
    Boolean Enabled { get; set; }
    DateTime ExpiresAt { get; set; }
    DateTime LastUsedAt { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
