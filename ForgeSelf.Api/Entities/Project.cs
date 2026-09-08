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

/// <summary>项目工作区：一个目录即一个项目（宿主库，ConnName=ForgeSelf）。</summary>
[Serializable]
[DataObject]
[Description("项目工作区。")]
[BindIndex("UX_Project_Root", true, "Root")]
[BindIndex("IX_Project_LastActiveAt", false, "LastActiveAt")]
[BindTable("Project", Description = "项目工作区", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class Project : IProjectModel, IEntity<IProjectModel>
{
    #region 属性
    private Int32 _Id;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "项目ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Root;
    /// <summary>项目根目录绝对路径（唯一键）</summary>
    [DisplayName("项目根")]
    [Description("项目根目录绝对路径（唯一键）")]
    [DataObjectField(false, false, false, 500)]
    [BindColumn("Root", "项目根目录绝对路径", "")]
    public String Root { get => _Root; set { if (OnPropertyChanging("Root", value)) { _Root = value; OnPropertyChanged("Root"); } } }

    private String _Name;
    /// <summary>项目名（默认目录名）</summary>
    [DisplayName("项目名")]
    [Description("项目名（默认目录名）")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "项目名", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Type;
    /// <summary>类型：frontend/backend/fullstack/library/tool/other 或自定义文本</summary>
    [DisplayName("类型")]
    [Description("类型：frontend/backend/fullstack/library/tool/other 或自定义文本")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Type", "类型", "")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _Description;
    /// <summary>描述</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Tags;
    /// <summary>标签，逗号分隔（SQLite 无数组；渲染层 split）</summary>
    [DisplayName("标签")]
    [Description("标签，逗号分隔")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Tags", "标签", "")]
    public String Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private String _Source;
    /// <summary>登记来源（ai-agent/manual）</summary>
    [DisplayName("来源")]
    [Description("登记来源（ai-agent/manual）")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private DateTime _CreatedAt;
    /// <summary>首次登记</summary>
    [DisplayName("创建时间")]
    [Description("首次登记")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "首次登记", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }

    private DateTime _LastActiveAt;
    /// <summary>最近活动（选择目录时刷新）</summary>
    [DisplayName("最近活动")]
    [Description("最近活动（选择目录时刷新）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LastActiveAt", "最近活动", "")]
    public DateTime LastActiveAt { get => _LastActiveAt; set { if (OnPropertyChanging("LastActiveAt", value)) { _LastActiveAt = value; OnPropertyChanged("LastActiveAt"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IProjectModel model)
    {
        Id = model.Id;
        Root = model.Root;
        Name = model.Name;
        Type = model.Type;
        Description = model.Description;
        Tags = model.Tags;
        Source = model.Source;
        CreatedAt = model.CreatedAt;
        UpdatedAt = model.UpdatedAt;
        LastActiveAt = model.LastActiveAt;
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
            "Root" => _Root,
            "Name" => _Name,
            "Type" => _Type,
            "Description" => _Description,
            "Tags" => _Tags,
            "Source" => _Source,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "LastActiveAt" => _LastActiveAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Root": _Root = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Tags": _Tags = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "LastActiveAt": _LastActiveAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据项目ID查找</summary>
    /// <param name="id">项目ID</param>
    /// <returns>实体对象</returns>
    public static Project? FindById(Int32 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据项目根目录查找（大小写不敏感，唯一键）</summary>
    /// <param name="root">项目根目录绝对路径</param>
    /// <returns>实体对象；不存在返回 null</returns>
    public static Project? FindByRoot(String root)
    {
        if (root.IsNullOrEmpty()) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Root.EqualIgnoreCase(root));
        return Find(_.Root == root);
    }
    #endregion

    #region 高级查询
    /// <summary>分页查询项目</summary>
    /// <param name="key">关键字（匹配 名称/根/描述）</param>
    /// <param name="page">分页参数信息</param>
    /// <returns>实体列表</returns>
    public static IList<Project> Search(String? key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Root.Contains(key) | _.Description.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得项目字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>项目ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>项目根目录绝对路径</summary>
        public static readonly Field Root = FindByName("Root");
        /// <summary>项目名</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>类型</summary>
        public static readonly Field Type = FindByName("Type");
        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");
        /// <summary>标签</summary>
        public static readonly Field Tags = FindByName("Tags");
        /// <summary>来源</summary>
        public static readonly Field Source = FindByName("Source");
        /// <summary>首次登记</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");
        /// <summary>最近活动</summary>
        public static readonly Field LastActiveAt = FindByName("LastActiveAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得项目字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>项目ID</summary>
        public const String Id = "Id";
        /// <summary>项目根目录绝对路径</summary>
        public const String Root = "Root";
        /// <summary>项目名</summary>
        public const String Name = "Name";
        /// <summary>类型</summary>
        public const String Type = "Type";
        /// <summary>描述</summary>
        public const String Description = "Description";
        /// <summary>标签</summary>
        public const String Tags = "Tags";
        /// <summary>来源</summary>
        public const String Source = "Source";
        /// <summary>首次登记</summary>
        public const String CreatedAt = "CreatedAt";
        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
        /// <summary>最近活动</summary>
        public const String LastActiveAt = "LastActiveAt";
    }
    #endregion
}

/// <summary>项目模型接口（供 XCode 实体实现）。</summary>
public partial interface IProjectModel
{
    Int32 Id { get; set; }
    String Root { get; set; }
    String Name { get; set; }
    String Type { get; set; }
    String Description { get; set; }
    String Tags { get; set; }
    String Source { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    DateTime LastActiveAt { get; set; }
}
