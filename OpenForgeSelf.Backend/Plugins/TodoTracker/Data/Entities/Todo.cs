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

namespace OpenForgeSelf.Backend.Plugins.TodoTracker.Entities;

/// <summary>待办事项。</summary>
[Serializable]
[DataObject]
[Description("待办事项。")]
[BindIndex("IX_Todo_Status", false, "Status")]
[BindIndex("IX_Todo_CreatedAt", false, "CreatedAt")]
[BindTable("Todo", Description = "待办事项", ConnName = "TodoTracker", DbType = DatabaseType.None)]
public partial class Todo
{
    #region 属性
    private Int32 _Id;
    /// <summary>待办ID</summary>
    [DisplayName("待办ID")]
    [Description("待办ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "待办ID", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Title;
    /// <summary>标题</summary>
    [DisplayName("标题")]
    [Description("标题")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Title", "标题", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private Int32 _Status;
    /// <summary>状态 0=Pending 1=Completed</summary>
    [DisplayName("状态")]
    [Description("状态 0=Pending 1=Completed")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态 0=Pending 1=Completed", "", DefaultValue = "0")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _DueDate;
    /// <summary>截止日期</summary>
    [DisplayName("截止日期")]
    [Description("截止日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DueDate", "截止日期", "")]
    public DateTime DueDate { get => _DueDate; set { if (OnPropertyChanging("DueDate", value)) { _DueDate = value; OnPropertyChanged("DueDate"); } } }

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

    private DateTime _CompletedAt;
    /// <summary>完成时间</summary>
    [DisplayName("完成时间")]
    [Description("完成时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CompletedAt", "完成时间", "")]
    public DateTime CompletedAt { get => _CompletedAt; set { if (OnPropertyChanging("CompletedAt", value)) { _CompletedAt = value; OnPropertyChanged("CompletedAt"); } } }
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
            "Title" => _Title,
            "Remark" => _Remark,
            "Status" => _Status,
            "DueDate" => _DueDate,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            "CompletedAt" => _CompletedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "DueDate": _DueDate = value.ToDateTime(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                case "CompletedAt": _CompletedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据待办ID查找</summary>
    /// <param name="id">待办ID</param>
    /// <returns>实体对象</returns>
    public static Todo FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> FindAllByStatus(Int32 status)
    {
        if (status < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Status == status);

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="status">状态</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Todo> Search(Int32 status, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (status >= 0) exp &= _.Status == status;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得待办事项字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>待办ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>标题</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>截止日期</summary>
        public static readonly Field DueDate = FindByName("DueDate");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        /// <summary>完成时间</summary>
        public static readonly Field CompletedAt = FindByName("CompletedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得待办事项字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>待办ID</summary>
        public const String Id = "Id";

        /// <summary>标题</summary>
        public const String Title = "Title";

        /// <summary>备注</summary>
        public const String Remark = "Remark";

        /// <summary>状态</summary>
        public const String Status = "Status";

        /// <summary>截止日期</summary>
        public const String DueDate = "DueDate";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";

        /// <summary>完成时间</summary>
        public const String CompletedAt = "CompletedAt";
    }
    #endregion
}
