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
using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Plugins.DesignSystem.Entities;

public partial class DesignGuideline : Entity<DesignGuideline>
{
    #region 对象操作
    // 同族实体的生成物写的是 `Meta.Session.Count < MaxCacheCount`（引用本 Biz 文件里的字段），
    // 而 xcode 给本表生成的那份把上限**内联成了字面量 1000**（见 DesignGuideline.cs 的五处 Find/FindAll），
    // 所以这个字段在本表没有被引用，留着就是 CS0414 死码。若日后重新生成改回引用字段，按同族把它加回来。

    /// <summary>
    /// 验证并修补数据。
    ///
    /// 这一层是**最后一道形校验**：REST/工具/生成器各自都会查，但库里存的行不能指望调用方自觉——
    /// 一个手写进来的 `category=乱写` 会让界面按分类分组时凭空多一组，而"分组少了一类"永远没人发现。
    /// 词表一律读 <see cref="GuidelineCategories"/>，不在这里另列一份。
    /// </summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        if (Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Code), "规范标识不能为空！");
        if (!GuidelineCategories.IsCode(Code))
            throw new ArgumentException($"规范标识 {Code} 不合形（须匹配 {GuidelineCategories.CodePattern}：小写字母数字开头的 kebab 名）", nameof(Code));
        if (!Title.IsNullOrEmpty() && Title.Length > 100)
            throw new ArgumentException($"标题长度 {Title.Length} 超过上限 100", nameof(Title));
        if (!Summary.IsNullOrEmpty() && Summary.Length > 500)
            throw new ArgumentException($"摘要长度 {Summary.Length} 超过上限 500", nameof(Summary));
        if (!Body.IsNullOrEmpty() && Body.Length > GuidelineCategories.MaxBodyLength)
            throw new ArgumentException($"正文长度 {Body.Length} 超过上限 {GuidelineCategories.MaxBodyLength}", nameof(Body));

        if (Category.IsNullOrEmpty()) Category = "layout";
        else if (!GuidelineCategories.Has(Category))
            throw new ArgumentException($"分类 {Category} 不在词表内，可用：{String.Join("|", GuidelineCategories.All)}", nameof(Category));

        if (Status.IsNullOrEmpty()) Status = "adopted";
        else if (!GuidelineCategories.HasStatus(Status))
            throw new ArgumentException($"状态 {Status} 非法，可用：{String.Join("|", GuidelineCategories.Statuses)}（归档=软删，本表不提供删除）", nameof(Status));

        if (Source.IsNullOrEmpty()) Source = "generated";
        else if (!GuidelineCategories.HasSource(Source))
            throw new ArgumentException($"来源 {Source} 非法，可用：{String.Join("|", GuidelineCategories.Sources)}", nameof(Source));

        // 规则/引用/适用三个 JSON 袋：允许空，但非空时必须是合法 JSON 数组（半截 JSON 会让导出与界面同时炸）
        CheckJson(RulesJson, nameof(RulesJson));
        CheckJson(TokenRefsJson, nameof(TokenRefsJson));
        CheckJson(AppliesToJson, nameof(AppliesToJson));

        // 时间戳：单点维护，免得"调用方忘了写"变成界面上一片 0001-01-01
        var now = DateTime.Now;
        if (method == DataMethod.Insert && CreatedAt == default) CreatedAt = now;
        if (UpdatedAt == default || method == DataMethod.Update) UpdatedAt = now;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        return true;
    }

    static void CheckJson(String? json, String field)
    {
        if (json.IsNullOrEmpty()) return;
        var trimmed = json.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
            throw new ArgumentException($"{field} 必须是 JSON 数组（当前不是以 [ 开头、] 结尾）", field);
        try
        {
            System.Text.Json.JsonDocument.Parse(trimmed).Dispose();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"{field} 不是合法 JSON：{ex.Message}", field);
        }
    }

    /// <summary>是否处于"用户手改"保护态（重新生成默认规范时不得覆盖）</summary>
    public Boolean IsManualProtected => Source.Equals("manual", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否已归档（软删：读路径默认过滤，界面仍可见可恢复）</summary>
    public Boolean IsArchived => Status.Equals("archived", StringComparison.OrdinalIgnoreCase);
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // ── M3 收口（2026-10-03 输入48）：本插件的实体读取统一经 Biz 直查方法 ──────────────
    // Entities/<Name>.cs 是 xcode 生成物、会被覆写，业务查询只能写在这里（plugin-development 铁律 9）。
    // 也别把下面三个与生成器的「缓存助手」混淆：FindByXxx / FindAllByXxx 那一族，
    // 函数体首行注释就是「// 实体缓存」、内部走 Meta.Cache；本插件从不调用它们
    // （例：DesignProjectService.FindByCode 已改写成 FindAll 直查）。
    // XCode 的 Find / FindAll / FindCount 本身不读实体缓存，故下面三个方法对调用点行为等价；
    // 收益是「全插件只有一处读入口，将来换读法只改这三行」。

    /// <summary>直查：条件匹配的全部行（不过实体缓存）</summary>
    public static IList<DesignGuideline> QueryAll(Expression exp) => FindAll(exp);

    /// <summary>直查：条件匹配的第一行；无匹配返回 null</summary>
    public static DesignGuideline? QueryFirst(Expression exp) => Find(exp);

    /// <summary>直查：条件匹配的行数</summary>
    public static Int64 QueryCount(Expression exp) => FindCount(exp);

    /// <summary>直查：分页取条件匹配的行（不过实体缓存）</summary>
    public static IList<DesignGuideline> QueryAll(Expression exp, PageParameter page) => FindAll(exp, page);
    #endregion

    #region 业务操作
    #endregion
}
