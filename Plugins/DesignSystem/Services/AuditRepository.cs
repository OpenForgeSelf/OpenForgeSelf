using NewLife;
using XCode;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>一条审计结论（由审计引擎产出，落 DesignAudit）</summary>
public sealed record AuditItem(
    String Kind,
    String Rule,
    String Severity,
    String TargetType,
    String TargetPath,
    Boolean Passed,
    String? PairedPath = null,
    String? Expected = null,
    String? Actual = null,
    Double Ratio = -1,
    String? Suggestion = null,
    String? Message = null);

/// <summary>审计汇总（供发布门禁与界面徽标）</summary>
public sealed record AuditSummary(Int32 Total, Int32 Passed, Int32 Critical, Int32 Warning, Int32 Info)
{
    public Boolean Blocking => Critical > 0;
}

/// <summary>
/// 审计结果读写。可达性在这里是**数据**而不是文档散文：
/// 每条结论按 (项目, 发布, 类别, 对象类型, 对象路径) 唯一，重跑审计覆盖旧行而非堆历史。
/// </summary>
public sealed class AuditRepository
{
    public IList<DesignAudit> List(Int64 projectId, Int64 releaseId, String? kind, Boolean? passed)
    {
        var exp = DesignAudit._.ProjectId == projectId & DesignAudit._.ReleaseId == releaseId;
        if (!kind.IsNullOrEmpty()) exp &= DesignAudit._.Kind == kind;
        if (passed != null) exp &= DesignAudit._.Passed == passed.Value;
        return DesignAudit.FindAll(exp)
            .OrderBy(a => a.Severity, StringComparer.Ordinal)
            .ThenBy(a => a.Kind, StringComparer.Ordinal)
            .ThenBy(a => a.TargetPath, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 写入一批审计结论（同键覆盖，并**清掉本次没再产出的旧行**）。
    /// 整批一个事务：逐条 Save 会把一次审计写成几百个独立提交，
    /// 与并发读撞 WAL 检查点的概率随之上升（实测 SQLITE_BUSY 把 /releases 打成 500）。
    ///
    /// 为什么必须清旧行：一条结论这次没产出（对象解析不出颜色了、对不复存在、主题不再参与判定），
    /// 库里留着上一轮的 `Passed=true` 就是**用旧的绿灯冒充今天查过** ——
    /// 而 `HasBlocking` 读的正是这些行，门禁会因此对一个没人检查的状态放行。
    /// "没人报"和"没问题"必须能从库里区分开，办法就是让每次 Run 的产出等于该 scope 的全部结论。
    /// </summary>
    public Int32 Record(Int64 projectId, Int64 releaseId, IEnumerable<AuditItem> items)
    {
        var list = items as IList<AuditItem> ?? items.ToList();

        var now = DateTime.Now;
        var count = 0;
        using var et = new EntityTransaction<DesignAudit>();
        foreach (var item in list)
        {
            var e = DesignAudit.FindAll(DesignAudit._.ProjectId == projectId
                & DesignAudit._.ReleaseId == releaseId
                & DesignAudit._.Kind == item.Kind
                & DesignAudit._.TargetType == item.TargetType
                & DesignAudit._.TargetPath == item.TargetPath).FirstOrDefault()
                ?? new DesignAudit
                {
                    ProjectId = projectId,
                    ReleaseId = releaseId,
                    Kind = item.Kind,
                    TargetType = item.TargetType,
                    TargetPath = item.TargetPath,
                    CreatedAt = now,
                };

            e.Rule = item.Rule;
            e.Severity = item.Severity;
            e.Passed = item.Passed;
            e.PairedPath = item.PairedPath ?? "";
            e.Expected = item.Expected ?? "";
            e.Actual = item.Actual ?? "";
            e.Ratio = item.Ratio;
            e.Name = $"{item.Kind}:{item.TargetPath}";
            e.Suggestion = item.Suggestion ?? "";
            e.Message = item.Message ?? "";
            e.EngineVersion = DesignSystemConstants.GeneratorVersion;
            e.CheckedAt = now;
            e.UpdatedAt = now;
            e.Save();
            count++;
        }

        var live = new HashSet<String>(list.Select(i => Key(i.Kind, i.TargetType, i.TargetPath)), StringComparer.Ordinal);
        foreach (var stale in DesignAudit.FindAll(DesignAudit._.ProjectId == projectId & DesignAudit._.ReleaseId == releaseId)
                     .Where(e => !live.Contains(Key(e.Kind, e.TargetType, e.TargetPath))))
            stale.Delete();

        et.Commit();
        return count;
    }

    static String Key(String kind, String targetType, String targetPath) => $"{kind}|{targetType}|{targetPath}";

    /// <summary>草稿态（releaseId=0）审计汇总</summary>
    public AuditSummary Summarize(Int64 projectId, Int64 releaseId = DesignSystemConstants.SharedThemeId)
    {
        var rows = DesignAudit.FindAll(DesignAudit._.ProjectId == projectId & DesignAudit._.ReleaseId == releaseId);
        return new AuditSummary(
            rows.Count,
            rows.Count(r => r.Passed),
            rows.Count(r => !r.Passed && r.Severity == "critical"),
            rows.Count(r => !r.Passed && r.Severity == "warning"),
            rows.Count(r => !r.Passed && r.Severity == "info"));
    }

    /// <summary>是否存在未通过的 critical 项（发布门禁判据）</summary>
    public Boolean HasBlocking(Int64 projectId, Int64 releaseId = DesignSystemConstants.SharedThemeId) =>
        DesignAudit.FindCount(DesignAudit._.ProjectId == projectId & DesignAudit._.ReleaseId == releaseId
            & DesignAudit._.Passed == false & DesignAudit._.Severity == "critical") > 0;
}
