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

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_ApiServerKey_IsActive", false, "IsActive")]
[BindTable("ApiServerKey", Description = "API服务器密钥", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class ApiServerKey : IApiServerKeyModel, IEntity<IApiServerKeyModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>实体唯一标识</summary>
    [DisplayName("实体唯一标识")]
    [Description("实体唯一标识")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "实体唯一标识", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _KeyCipher;
    /// <summary>API密钥密文（AES-256-CBC加密存储）</summary>
    [DisplayName("API密钥密文（AES-256-CBC加密存储）")]
    [Description("API密钥密文（AES-256-CBC加密存储）")]
    [DataObjectField(false, false, false, 512)]
    [BindColumn("KeyCipher", "API密钥密文（AES-256-CBC加密存储）", "")]
    public String KeyCipher { get => _KeyCipher; set { if (OnPropertyChanging("KeyCipher", value)) { _KeyCipher = value; OnPropertyChanged("KeyCipher"); } } }

    private Boolean _IsActive;
    /// <summary>是否当前生效密钥</summary>
    [DisplayName("是否当前生效密钥")]
    [Description("是否当前生效密钥")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsActive", "是否当前生效密钥", "")]
    public Boolean IsActive { get => _IsActive; set { if (OnPropertyChanging("IsActive", value)) { _IsActive = value; OnPropertyChanged("IsActive"); } } }

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

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IApiServerKeyModel model)
    {
        Id = model.Id;
        KeyCipher = model.KeyCipher;
        IsActive = model.IsActive;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
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
            "KeyCipher" => _KeyCipher,
            "IsActive" => _IsActive,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "KeyCipher": _KeyCipher = Convert.ToString(value); break;
                case "IsActive": _IsActive = value.ToBoolean(); break;
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
    /// <summary>根据实体唯一标识查找</summary>
    /// <param name="id">实体唯一标识</param>
    /// <returns>实体对象</returns>
    public static ApiServerKey FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="isActive">是否当前生效密钥</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ApiServerKey> Search(Boolean? isActive, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (isActive != null) exp &= _.IsActive == isActive;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得API服务器密钥字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>实体唯一标识</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>API密钥密文（AES-256-CBC加密存储）</summary>
        public static readonly Field KeyCipher = FindByName("KeyCipher");

        /// <summary>是否当前生效密钥</summary>
        public static readonly Field IsActive = FindByName("IsActive");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得API服务器密钥字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>实体唯一标识</summary>
        public const String Id = "Id";

        /// <summary>API密钥密文（AES-256-CBC加密存储）</summary>
        public const String KeyCipher = "KeyCipher";

        /// <summary>是否当前生效密钥</summary>
        public const String IsActive = "IsActive";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
