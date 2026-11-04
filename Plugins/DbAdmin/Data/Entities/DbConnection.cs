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

namespace ForgeSelf.Api.Plugins.DbAdmin.Entities;

/// <summary>{name}。用户数据库连接配置（密码/连接串加密存储）</summary>
[Serializable]
[DataObject]
[Description("{name}。用户数据库连接配置（密码/连接串加密存储）")]
[BindIndex("IU_DbConnection_Name", true, "Name")]
[BindTable("DbConnection", Description = "用户数据库连接配置（密码/连接串加密存储）", ConnName = "DbAdmin", DbType = DatabaseType.None)]
public partial class DbConnection
{
    #region 属性
    private Int32 _Id;
    /// <summary>连接ID</summary>
    [DisplayName("连接ID")]
    [Description("连接ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "连接ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>连接名（唯一）</summary>
    [DisplayName("连接名（唯一）")]
    [Description("连接名（唯一）")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Name", "连接名（唯一）", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Provider;
    /// <summary>数据库类型 sqlite/mysql/postgresql/sqlserver</summary>
    [DisplayName("数据库类型sqlite_mysql_postgresql_sqlserver")]
    [Description("数据库类型 sqlite/mysql/postgresql/sqlserver")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Provider", "数据库类型 sqlite/mysql/postgresql/sqlserver", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String _Mode;
    /// <summary>配置方式 form/connstr</summary>
    [DisplayName("配置方式form_connstr")]
    [Description("配置方式 form/connstr")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Mode", "配置方式 form/connstr", "", DefaultValue = "form")]
    public String Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private String _Host;
    /// <summary>主机（SQLite 为文件路径时放 Database）</summary>
    [DisplayName("主机（SQLite为文件路径时放Database）")]
    [Description("主机（SQLite 为文件路径时放 Database）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Host", "主机（SQLite 为文件路径时放 Database）", "")]
    public String Host { get => _Host; set { if (OnPropertyChanging("Host", value)) { _Host = value; OnPropertyChanged("Host"); } } }

    private Int32 _Port;
    /// <summary>端口</summary>
    [DisplayName("端口")]
    [Description("端口")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Port", "端口", "")]
    public Int32 Port { get => _Port; set { if (OnPropertyChanging("Port", value)) { _Port = value; OnPropertyChanged("Port"); } } }

    private String _Database;
    /// <summary>数据库名或 SQLite 文件路径</summary>
    [DisplayName("数据库名或SQLite文件路径")]
    [Description("数据库名或 SQLite 文件路径")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Database", "数据库名或 SQLite 文件路径", "")]
    public String Database { get => _Database; set { if (OnPropertyChanging("Database", value)) { _Database = value; OnPropertyChanged("Database"); } } }

    private String _UserName;
    /// <summary>用户名</summary>
    [DisplayName("用户名")]
    [Description("用户名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("UserName", "用户名", "")]
    public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String _PasswordEnc;
    /// <summary>加密后的密码</summary>
    [DisplayName("加密后的密码")]
    [Description("加密后的密码")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("PasswordEnc", "加密后的密码", "")]
    public String PasswordEnc { get => _PasswordEnc; set { if (OnPropertyChanging("PasswordEnc", value)) { _PasswordEnc = value; OnPropertyChanged("PasswordEnc"); } } }

    private String _ConnStrEnc;
    /// <summary>加密后的原始连接串（Mode=connstr）</summary>
    [DisplayName("加密后的原始连接串（Mode=connstr）")]
    [Description("加密后的原始连接串（Mode=connstr）")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ConnStrEnc", "加密后的原始连接串（Mode=connstr）", "")]
    public String ConnStrEnc { get => _ConnStrEnc; set { if (OnPropertyChanging("ConnStrEnc", value)) { _ConnStrEnc = value; OnPropertyChanged("ConnStrEnc"); } } }

    private Boolean _Readonly;
    /// <summary>用户自设只读</summary>
    [DisplayName("用户自设只读")]
    [Description("用户自设只读")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Readonly", "用户自设只读", "")]
    public Boolean Readonly { get => _Readonly; set { if (OnPropertyChanging("Readonly", value)) { _Readonly = value; OnPropertyChanged("Readonly"); } } }

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
            "Name" => _Name,
            "Provider" => _Provider,
            "Mode" => _Mode,
            "Host" => _Host,
            "Port" => _Port,
            "Database" => _Database,
            "UserName" => _UserName,
            "PasswordEnc" => _PasswordEnc,
            "ConnStrEnc" => _ConnStrEnc,
            "Readonly" => _Readonly,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "Mode": _Mode = Convert.ToString(value); break;
                case "Host": _Host = Convert.ToString(value); break;
                case "Port": _Port = value.ToInt(); break;
                case "Database": _Database = Convert.ToString(value); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "PasswordEnc": _PasswordEnc = Convert.ToString(value); break;
                case "ConnStrEnc": _ConnStrEnc = Convert.ToString(value); break;
                case "Readonly": _Readonly = value.ToBoolean(); break;
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
    /// <summary>根据连接ID查找</summary>
    /// <param name="id">连接ID</param>
    /// <returns>实体对象</returns>
    public static DbConnection FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据连接名（唯一）查找</summary>
    /// <param name="name">连接名（唯一）</param>
    /// <returns>实体对象</returns>
    public static DbConnection FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as DbConnection;

        //return Find(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="@readonly">用户自设只读</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DbConnection> Search(Boolean? @readonly, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (@readonly != null) exp &= _.Readonly == @readonly;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户数据库连接配置（密码_连接串加密存储）字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>连接ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>连接名（唯一）</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>数据库类型 sqlite/mysql/postgresql/sqlserver</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>配置方式 form/connstr</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>主机（SQLite 为文件路径时放 Database）</summary>
        public static readonly Field Host = FindByName("Host");

        /// <summary>端口</summary>
        public static readonly Field Port = FindByName("Port");

        /// <summary>数据库名或 SQLite 文件路径</summary>
        public static readonly Field Database = FindByName("Database");

        /// <summary>用户名</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>加密后的密码</summary>
        public static readonly Field PasswordEnc = FindByName("PasswordEnc");

        /// <summary>加密后的原始连接串（Mode=connstr）</summary>
        public static readonly Field ConnStrEnc = FindByName("ConnStrEnc");

        /// <summary>用户自设只读</summary>
        public static readonly Field Readonly = FindByName("Readonly");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用户数据库连接配置（密码_连接串加密存储）字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>连接ID</summary>
        public const String Id = "Id";

        /// <summary>连接名（唯一）</summary>
        public const String Name = "Name";

        /// <summary>数据库类型 sqlite/mysql/postgresql/sqlserver</summary>
        public const String Provider = "Provider";

        /// <summary>配置方式 form/connstr</summary>
        public const String Mode = "Mode";

        /// <summary>主机（SQLite 为文件路径时放 Database）</summary>
        public const String Host = "Host";

        /// <summary>端口</summary>
        public const String Port = "Port";

        /// <summary>数据库名或 SQLite 文件路径</summary>
        public const String Database = "Database";

        /// <summary>用户名</summary>
        public const String UserName = "UserName";

        /// <summary>加密后的密码</summary>
        public const String PasswordEnc = "PasswordEnc";

        /// <summary>加密后的原始连接串（Mode=connstr）</summary>
        public const String ConnStrEnc = "ConnStrEnc";

        /// <summary>用户自设只读</summary>
        public const String Readonly = "Readonly";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
