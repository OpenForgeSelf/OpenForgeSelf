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

/// <summary>运行命令（项目根执行，宿主库，ConnName=ForgeSelf）。</summary>
[Serializable]
[DataObject]
[Description("运行命令。")]
[BindIndex("IX_RunCommand_ProjectId", false, "ProjectId")]
[BindTable("RunCommand", Description = "运行命令", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class RunCommand : IRunCommandModel, IEntity<IRunCommandModel>
{
    #region 属性
    private Int32 _Id;
    /// <summary>命令ID</summary>
    [DisplayName("命令ID")]
    [Description("命令ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "命令ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ProjectId;
    /// <summary>所属项目（逻辑外键，索引）</summary>
    [DisplayName("所属项目")]
    [Description("所属项目（逻辑外键，索引）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "所属项目", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Name;
    /// <summary>显示名（如「前端 dev」）</summary>
    [DisplayName("显示名")]
    [Description("显示名（如「前端 dev」）")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "显示名", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Script;
    /// <summary>运行脚本（在项目根执行）</summary>
    [DisplayName("运行脚本")]
    [Description("运行脚本（在项目根执行）")]
    [DataObjectField(false, false, false, 2000)]
    [BindColumn("Script", "运行脚本", "")]
    public String Script { get => _Script; set { if (OnPropertyChanging("Script", value)) { _Script = value; OnPropertyChanged("Script"); } } }

    private String _Url;
    /// <summary>运行后访问地址（可空）</summary>
    [DisplayName("访问地址")]
    [Description("运行后访问地址（可空）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Url", "访问地址", "")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private Int32 _Sort;
    /// <summary>排序，默认 0</summary>
    [DisplayName("排序")]
    [Description("排序，默认 0")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序", "", DefaultValue = "0")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IRunCommandModel model)
    {
        Id = model.Id;
        ProjectId = model.ProjectId;
        Name = model.Name;
        Script = model.Script;
        Url = model.Url;
        Sort = model.Sort;
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
            "ProjectId" => _ProjectId,
            "Name" => _Name,
            "Script" => _Script,
            "Url" => _Url,
            "Sort" => _Sort,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Script": _Script = Convert.ToString(value); break;
                case "Url": _Url = Convert.ToString(value); break;
                case "Sort": _Sort = value.ToInt(); break;
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
    /// <summary>根据命令ID查找</summary>
    /// <param name="id">命令ID</param>
    /// <returns>实体对象</returns>
    public static RunCommand? FindById(Int32 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据所属项目查找全部命令（按 Sort 升序）</summary>
    /// <param name="projectId">所属项目 Id</param>
    /// <returns>实体列表</returns>
    public static IList<RunCommand> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return [];
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();
        return FindAll(_.ProjectId == projectId).OrderBy(e => e.Sort).ThenBy(e => e.Id).ToList();
    }
    #endregion

    #region 高级查询
    /// <summary>分页查询运行命令</summary>
    /// <param name="projectId">所属项目 Id（≤0 表示不限）</param>
    /// <param name="key">关键字（匹配 名称/脚本）</param>
    /// <param name="page">分页参数信息</param>
    /// <returns>实体列表</returns>
    public static IList<RunCommand> Search(Int32 projectId, String? key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (projectId > 0) exp &= _.ProjectId == projectId;
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Script.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得运行命令字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>命令ID</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>所属项目</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");
        /// <summary>显示名</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>运行脚本</summary>
        public static readonly Field Script = FindByName("Script");
        /// <summary>访问地址</summary>
        public static readonly Field Url = FindByName("Url");
        /// <summary>排序</summary>
        public static readonly Field Sort = FindByName("Sort");
        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");
        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得运行命令字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>命令ID</summary>
        public const String Id = "Id";
        /// <summary>所属项目</summary>
        public const String ProjectId = "ProjectId";
        /// <summary>显示名</summary>
        public const String Name = "Name";
        /// <summary>运行脚本</summary>
        public const String Script = "Script";
        /// <summary>访问地址</summary>
        public const String Url = "Url";
        /// <summary>排序</summary>
        public const String Sort = "Sort";
        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";
        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}

/// <summary>运行命令模型接口（供 XCode 实体实现）。</summary>
public partial interface IRunCommandModel
{
    Int32 Id { get; set; }
    Int32 ProjectId { get; set; }
    String Name { get; set; }
    String Script { get; set; }
    String Url { get; set; }
    Int32 Sort { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
