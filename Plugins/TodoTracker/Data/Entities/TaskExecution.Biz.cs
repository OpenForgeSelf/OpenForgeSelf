using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace ForgeSelf.Api.Plugins.TodoTracker.Entities;

public partial class TaskExecution : Entity<TaskExecution>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static TaskExecution()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(TodoId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Action == null) throw new ArgumentNullException(nameof(Action), "做了什么操作（一句话）不能为空！");
        if (Action.Length > 300) throw new ArgumentOutOfRangeException(nameof(Action), "做了什么操作（一句话）长度不能超过 300 字符！");
        if (TodoId <= 0) throw new ArgumentOutOfRangeException(nameof(TodoId), "执行记录必须挂在一条任务上（TodoId>0）！");
        CheckLength(nameof(Detail), Detail, 8000);
        CheckLength(nameof(Result), Result, 8000);
        CheckLength(nameof(FilesChanged), FilesChanged, 8000);
        CheckLength(nameof(Verification), Verification, 4000);
        CheckLength(nameof(Risks), Risks, 2000);
        CheckLength(nameof(Residuals), Residuals, 2000);
        CheckLength(nameof(Evidence), Evidence, 1000);
        CheckLength(nameof(BlockReason), BlockReason, 1000);
        CheckLength(nameof(NextStep), NextStep, 1000);

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正
        if (method == DataMethod.Insert)
        {
            if (CreatedAt == DateTime.MinValue) CreatedAt = DateTime.Now;
        }

        return true;
    }

    private static void CheckLength(String field, String? value, Int32 max)
    {
        if (value != null && value.Length > max)
            throw new ArgumentOutOfRangeException(field, $"{field} 长度不能超过 {max} 字符！");
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化TaskExecution[待办执行记录（append-only台账）]数据……");

    //    var entity = new TaskExecution();
    //    entity.TodoId = 0;
    //    entity.Seq = 0;
    //    entity.Actor = "abc";
    //    entity.Action = "abc";
    //    entity.Detail = "abc";
    //    entity.Result = "abc";
    //    entity.FilesChanged = "abc";
    //    entity.Verification = "abc";
    //    entity.Risks = "abc";
    //    entity.Residuals = "abc";
    //    entity.Evidence = "abc";
    //    entity.StageFrom = 0;
    //    entity.StageTo = 0;
    //    entity.ElapsedMs = 0;
    //    entity.BlockReason = "abc";
    //    entity.NextStep = "abc";
    //    entity.CreatedAt = DateTime.Now;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化TaskExecution[待办执行记录（append-only台账）]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    /// <summary>所属任务Id</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Todo Todo => Extends.Get(nameof(Todo), k => Todo.FindById(TodoId));

    /// <summary>所属任务Id</summary>
    [Map(nameof(TodoId), typeof(Todo), "Id")]
    public String TodoTitle => Todo?.Title;
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From TaskExecution Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<TaskExecution> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作

    /// <summary>
    /// 某任务的全部记录（按 Seq 升序，上限 <paramref name="limit"/> 条）。<b>直查库</b>，不走实体缓存（铁律 11）。
    /// 排序只能走 <see cref="PageParameter.Sort"/> —— XCode 的 <c>FindAll(filter, page, params Field[])</c>
    /// 第三参是"取哪些列"，把 <c>_.Seq.Asc()</c> 传进去会得到"只选了 Seq 列"的行，其余字段全空（实测踩过）。
    /// </summary>
    public static IList<TaskExecution> FindAllByTask(Int32 todoId, Int32 limit = 1000) =>
        todoId <= 0 || limit <= 0
            ? []
            : FindAll(_.TodoId == todoId, new PageParameter { PageSize = limit, Sort = "Seq ASC" });

    /// <summary>
    /// 某任务当前最大序号（用于「任务内 Seq 递增」）。
    /// 用「按 Seq 倒序取 1 条」而不是"读全量再 Max"：前者只落 1 行，任务记录再多也不会随规模退化，
    /// 而读全量的写法一旦超过上限就会算出偏小的序号，直接把唯一索引撞掉。
    /// </summary>
    public static Int32 MaxSeqOf(Int32 todoId)
    {
        if (todoId <= 0) return 0;
        var top = FindAll(_.TodoId == todoId, new PageParameter { PageSize = 1, Sort = "Seq DESC" }).FirstOrDefault();
        return top?.Seq ?? 0;
    }

    /// <summary>
    /// 批量统计多个任务的记录数（列表页 recordCount 用，避免逐行查询造成 N+1）。
    /// 返回具体 <c>Dictionary</c> 而非 <c>IDictionary</c>：后者不实现 <c>IReadOnlyDictionary</c>，
    /// 声明成接口会让调用方的字典扩展（Pick/GetValueOrDefault）推断不出类型参数。
    /// </summary>
    public static Dictionary<Int32, Int32> CountByTasks(IEnumerable<Int32> todoIds)
    {
        var ids = todoIds?.Where(i => i > 0).Distinct().ToList() ?? new List<Int32>();
        var result = new Dictionary<Int32, Int32>();
        if (ids.Count == 0) return result;

        var exp = new WhereExpression();
        foreach (var id in ids) exp |= _.TodoId == id;

        // 一次查询后在内存分组（列表页每行一个计数若逐行查即 N+1）
        foreach (var g in FindAll(exp).GroupBy(e => e.TodoId))
            result[g.Key] = g.Count();
        return result;
    }

    #endregion
}
