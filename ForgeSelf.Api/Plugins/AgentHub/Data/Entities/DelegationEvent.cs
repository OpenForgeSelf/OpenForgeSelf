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

namespace ForgeSelf.Api.Plugins.AgentHub.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IX_DelegationEvent_TaskId_Seq", false, "TaskId,Seq")]
[BindTable("DelegationEvent", Description = "委派事件流", ConnName = "AgentHub", DbType = DatabaseType.None)]
public partial class DelegationEvent
{
    #region 属性
    private Int32 _Id;
    /// <summary>主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _TaskId;
    /// <summary>所属任务</summary>
    [DisplayName("所属任务")]
    [Description("所属任务")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TaskId", "所属任务", "")]
    public Int32 TaskId { get => _TaskId; set { if (OnPropertyChanging("TaskId", value)) { _TaskId = value; OnPropertyChanged("TaskId"); } } }

    private Int32 _Seq;
    /// <summary>序号</summary>
    [DisplayName("序号")]
    [Description("序号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Seq", "序号", "")]
    public Int32 Seq { get => _Seq; set { if (OnPropertyChanging("Seq", value)) { _Seq = value; OnPropertyChanged("Seq"); } } }

    private String _Type;
    /// <summary>事件类型</summary>
    [DisplayName("事件类型")]
    [Description("事件类型")]
    [DataObjectField(false, false, false, 30)]
    [BindColumn("Type", "事件类型", "", DefaultValue = "Text")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _PayloadJson;
    /// <summary>负载 JSON</summary>
    [DisplayName("负载JSON")]
    [Description("负载 JSON")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("PayloadJson", "负载 JSON", "")]
    public String PayloadJson { get => _PayloadJson; set { if (OnPropertyChanging("PayloadJson", value)) { _PayloadJson = value; OnPropertyChanged("PayloadJson"); } } }

    private Boolean _Truncated;
    /// <summary>已截断</summary>
    [DisplayName("已截断")]
    [Description("已截断")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Truncated", "已截断", "", DefaultValue = "0")]
    public Boolean Truncated { get => _Truncated; set { if (OnPropertyChanging("Truncated", value)) { _Truncated = value; OnPropertyChanged("Truncated"); } } }

    private DateTime _Timestamp;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Timestamp", "时间", "")]
    public DateTime Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }
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
            "TaskId" => _TaskId,
            "Seq" => _Seq,
            "Type" => _Type,
            "PayloadJson" => _PayloadJson,
            "Truncated" => _Truncated,
            "Timestamp" => _Timestamp,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "TaskId": _TaskId = value.ToInt(); break;
                case "Seq": _Seq = value.ToInt(); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "PayloadJson": _PayloadJson = Convert.ToString(value); break;
                case "Truncated": _Truncated = value.ToBoolean(); break;
                case "Timestamp": _Timestamp = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据主键查找</summary>
    /// <param name="id">主键</param>
    /// <returns>实体对象</returns>
    public static DelegationEvent FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据所属任务、序号查找</summary>
    /// <param name="taskId">所属任务</param>
    /// <param name="seq">序号</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationEvent> FindAllByTaskIdAndSeq(Int32 taskId, Int32 seq)
    {
        if (taskId < 0) return [];
        if (seq < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TaskId == taskId && e.Seq == seq);

        return FindAll(_.TaskId == taskId & _.Seq == seq);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="taskId">所属任务</param>
    /// <param name="seq">序号</param>
    /// <param name="truncated">已截断</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DelegationEvent> Search(Int32 taskId, Int32 seq, Boolean? truncated, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (taskId >= 0) exp &= _.TaskId == taskId;
        if (seq >= 0) exp &= _.Seq == seq;
        if (truncated != null) exp &= _.Truncated == truncated;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得委派事件流字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>主键</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>所属任务</summary>
        public static readonly Field TaskId = FindByName("TaskId");

        /// <summary>序号</summary>
        public static readonly Field Seq = FindByName("Seq");

        /// <summary>事件类型</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>负载 JSON</summary>
        public static readonly Field PayloadJson = FindByName("PayloadJson");

        /// <summary>已截断</summary>
        public static readonly Field Truncated = FindByName("Truncated");

        /// <summary>时间</summary>
        public static readonly Field Timestamp = FindByName("Timestamp");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得委派事件流字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>主键</summary>
        public const String Id = "Id";

        /// <summary>所属任务</summary>
        public const String TaskId = "TaskId";

        /// <summary>序号</summary>
        public const String Seq = "Seq";

        /// <summary>事件类型</summary>
        public const String Type = "Type";

        /// <summary>负载 JSON</summary>
        public const String PayloadJson = "PayloadJson";

        /// <summary>已截断</summary>
        public const String Truncated = "Truncated";

        /// <summary>时间</summary>
        public const String Timestamp = "Timestamp";
    }
    #endregion
}
