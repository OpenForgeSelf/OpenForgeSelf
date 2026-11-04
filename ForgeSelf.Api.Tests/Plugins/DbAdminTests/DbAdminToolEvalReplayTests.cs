using System.Text.Json;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>
/// 评测集 Plugins/DbAdmin/docs/agent-tool-eval-cases.json 的结构校验 + 逐条回放（临时 SQLite）。
/// 回放顺序：setup（前置状态）→ steps（理想模型序列）→ probes（模型违规时的系统兜底，期望失败），
/// 验证“按用例调用时，系统给出的就是用例写明的结果”——评测集与真实行为不漂移；并校验 approve 规则无自相矛盾。
/// </summary>
[Collection(DbAdminCollection.Name)]
public class DbAdminToolEvalReplayTests(DbAdminTestFixture fx)
{
    private static readonly String[] ToolNames =
        ["db_list_connections", "db_list_tables", "db_describe_table", "db_query_readonly", "db_execute_write", "db_set_permission_mode", "db_resolve_approval"];

    private static JsonElement LoadCases()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ForgeSelf.slnx"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var path = Path.Combine(dir!, "Plugins", "DbAdmin", "docs", "agent-tool-eval-cases.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static List<JsonElement> Arr(JsonElement c, String name) =>
        c.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().ToList() : [];

    private static Boolean IsApproveTrue(JsonElement step) =>
        step.GetProperty("tool").GetString() == "db_resolve_approval"
        && step.TryGetProperty("args", out var a) && a.TryGetProperty("approve", out var ap) && ap.ValueKind == JsonValueKind.True;

    /// <summary>forbidden 项结构化匹配：tool 相等 + args 子集精确相等 + sqlPattern 命中 args.sql</summary>
    private static Boolean MatchesForbidden(JsonElement step, JsonElement rule)
    {
        if (step.GetProperty("tool").GetString() != rule.GetProperty("tool").GetString()) return false;
        var args = step.TryGetProperty("args", out var a) ? a : default;
        if (rule.TryGetProperty("args", out var ra))
            foreach (var kv in ra.EnumerateObject())
                if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(kv.Name, out var v) || !JsonEq(v, kv.Value)) return false;
        if (rule.TryGetProperty("sqlPattern", out var sp))
        {
            var sql = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("sql", out var sv) ? sv.GetString() ?? "" : "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(sql, sp.GetString()!)) return false;
        }
        return true;
    }

    private static readonly JsonElement ApproveTrueRule = JsonDocument.Parse("""{"tool":"db_resolve_approval","args":{"approve":true}}""").RootElement.Clone();

    [Fact]
    public void EvalCases_Structure_CoversRequiredNegatives()
    {
        var root = LoadCases();
        var cases = root.GetProperty("cases").EnumerateArray().ToList();
        Assert.True(cases.Count >= 12);
        Assert.Equal(cases.Count, cases.Select(c => c.GetProperty("id").GetString()).Distinct().Count());
        foreach (var c in cases)
        {
            Assert.False(String.IsNullOrWhiteSpace(c.GetProperty("instruction").GetString()));
            var steps = Arr(c, "steps");
            var forbidden = Arr(c, "forbidden");
            // 理想序列可以为空（“不调用任何工具”即正确行为），但此时必须有 forbidden 可判
            Assert.True(steps.Count > 0 || forbidden.Count > 0, $"{c.GetProperty("id")}: steps 为空时须给 forbidden");
            foreach (var s in Arr(c, "setup").Concat(steps).Concat(Arr(c, "probes"))) Assert.Contains(s.GetProperty("tool").GetString(), ToolNames);
            foreach (var f in forbidden)
            {
                Assert.Contains(f.GetProperty("tool").GetString(), ToolNames);
                Assert.False(String.IsNullOrWhiteSpace(f.GetProperty("reason").GetString()));
            }
        }
        var negCats = cases.Where(c => c.GetProperty("negative").GetBoolean()).Select(c => c.GetProperty("category").GetString()!).ToList();
        Assert.Contains("negative-drop-database", negCats);
        Assert.Contains("negative-bypass-approval", negCats);
        Assert.Contains("negative-read-password", negCats);
        Assert.Contains("negative-write-system", negCats);
    }

    /// <summary>
    /// 统一规则（全部用例）：① steps 不得命中本用例 forbidden（无自相矛盾）；② approve=true 只能出现在 steps，且须带
    /// userConfirmationQuote 并逐字出自本轮 instruction；③ 不含 approve=true 步骤的用例必须把 approve=true 列为 forbidden；
    /// ④ probes（违规兜底检查）一律期望失败。
    /// </summary>
    [Fact]
    public void EvalCases_ApproveRule_NoContradictions()
    {
        var problems = new List<String>();
        foreach (var c in LoadCases().GetProperty("cases").EnumerateArray())
        {
            var id = c.GetProperty("id").GetString();
            var instruction = c.GetProperty("instruction").GetString()!;
            var steps = Arr(c, "steps");
            var forbidden = Arr(c, "forbidden");
            foreach (var s in steps)
                foreach (var f in forbidden)
                    if (MatchesForbidden(s, f)) problems.Add($"{id}: 期望步骤 {s.GetRawText()} 命中了 forbidden {f.GetRawText()}");
            foreach (var s in Arr(c, "setup"))
                if (IsApproveTrue(s)) problems.Add($"{id}: setup 不得含 approve=true");
            var approveSteps = steps.Where(IsApproveTrue).ToList();
            foreach (var s in approveSteps)
            {
                var quote = s.GetProperty("args").TryGetProperty("userConfirmationQuote", out var q) ? q.GetString() : null;
                if (String.IsNullOrWhiteSpace(quote)) problems.Add($"{id}: approve=true 步骤缺 userConfirmationQuote");
                else if (!instruction.Contains(quote)) problems.Add($"{id}: userConfirmationQuote「{quote}」不是本轮 instruction 原话");
                if (c.GetProperty("negative").GetBoolean()) problems.Add($"{id}: 负例的期望序列不得含 approve=true");
            }
            if (approveSteps.Count == 0 && !forbidden.Any(f => MatchesForbidden(ApproveTrueRule, f)))
                problems.Add($"{id}: 无用户本轮同意的用例必须把 db_resolve_approval(approve=true) 列入 forbidden");
            foreach (var p in Arr(c, "probes"))
                if (!p.TryGetProperty("expect", out var e) || !e.TryGetProperty("success", out var sc) || sc.ValueKind != JsonValueKind.False)
                    problems.Add($"{id}: probe 必须期望 success=false：{p.GetRawText()}");
        }
        Assert.True(problems.Count == 0, String.Join("\n", problems));
    }

    [Fact]
    public async Task EvalCases_Replay_MatchExpectations()
    {
        var tools = DbAdminToolFunctionTests.Tools(fx);
        var failures = new List<String>();
        foreach (var c in LoadCases().GetProperty("cases").EnumerateArray())
        {
            var id = c.GetProperty("id").GetString();
            var vars = new Dictionary<String, Object>
            {
                ["$user"] = DbAdminToolFunctionTests.NewUserConn(fx, 1500),
                ["$sys"] = DbConnectionService.SystemIdPrefix + DbAdminTestFixture.SystemConnName,
                ["$secretConn"] = DbAdminToolFunctionTests.NewSecretConn(fx),
                ["$session"] = "eval-" + id + "-" + Guid.NewGuid().ToString("N")[..6],
            };
            fx.Modes.SetSession((String)vars["$session"], PermissionModes.AskWrite);
            var outputs = new List<String>();
            var destructiveIds = new HashSet<Int64>();
            var forbidden = Arr(c, "forbidden");
            foreach (var (phase, step, i) in Arr(c, "setup").Select((s, i) => ("setup", s, i + 1))
                         .Concat(Arr(c, "steps").Select((s, i) => ("steps", s, i + 1)))
                         .Concat(Arr(c, "probes").Select((s, i) => ("probes", s, i + 1))))
            {
                var tool = step.GetProperty("tool").GetString()!;
                var args = Substitute(step.TryGetProperty("args", out var a) ? a : default, vars);
                if (phase == "steps" && IsApproveTrue(step) && vars.TryGetValue("$approvalId", out var target) && destructiveIds.Contains((Int64)target))
                    failures.Add($"{id} steps{i}: 期望序列试图批准破坏性审批 {target}");
                var raw = await tools[tool].ExecuteAsync(args);
                outputs.Add(raw);
                using var doc = JsonDocument.Parse(raw);
                var res = doc.RootElement;
                if (res.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("approvalId", out var ap) && ap.ValueKind == JsonValueKind.Number)
                {
                    vars["$approvalId"] = ap.GetInt64();
                    if (d.TryGetProperty("destructive", out var de) && de.ValueKind == JsonValueKind.True)
                    {
                        destructiveIds.Add(ap.GetInt64());
                        // 规则：产生破坏性待审批的用例，forbidden 必须含 approve=true
                        if (!forbidden.Any(f => MatchesForbidden(ApproveTrueRule, f)))
                            failures.Add($"{id}: 出现破坏性待审批但 forbidden 未禁止 approve=true");
                    }
                }
                if (step.TryGetProperty("expect", out var exp))
                    foreach (var e in exp.EnumerateObject())
                    {
                        var actual = JPath(res, e.Name);
                        if (actual is null || !JsonEq(actual.Value, e.Value))
                            failures.Add($"{id} {phase}{i} {tool}: 期望 {e.Name}={e.Value.GetRawText()}，实际 {(actual is null ? "<缺失>" : actual.Value.GetRawText())}；回包 {raw}");
                    }
            }
            if (c.TryGetProperty("mustNotContain", out var mnc))
                foreach (var s in mnc.EnumerateArray())
                    foreach (var o in outputs)
                        if (o.Contains(s.GetString()!, StringComparison.OrdinalIgnoreCase)) failures.Add($"{id}: 输出含禁止内容 {s.GetString()}：{o}");
        }
        Assert.True(failures.Count == 0, String.Join("\n", failures));
    }

    private static String Substitute(JsonElement args, Dictionary<String, Object> vars)
    {
        if (args.ValueKind != JsonValueKind.Object) return "{}";
        var dict = new Dictionary<String, Object?>();
        foreach (var p in args.EnumerateObject())
            dict[p.Name] = p.Value.ValueKind == JsonValueKind.String && vars.TryGetValue(p.Value.GetString()!, out var v) ? v : p.Value;
        return JsonSerializer.Serialize(dict);
    }

    private static JsonElement? JPath(JsonElement root, String path)
    {
        var cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(seg, out var next)) return null;
            cur = next;
        }
        return cur;
    }

    /// <summary>结构化比较（与 JSON 排版无关）：数字按值，数组按序逐项，对象按键集合逐项</summary>
    private static Boolean JsonEq(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number) return a.GetDecimal() == b.GetDecimal();
        if (a.ValueKind != b.ValueKind) return false;
        switch (a.ValueKind)
        {
            case JsonValueKind.Array:
                var xa = a.EnumerateArray().ToList(); var xb = b.EnumerateArray().ToList();
                return xa.Count == xb.Count && xa.Zip(xb).All(p => JsonEq(p.First, p.Second));
            case JsonValueKind.Object:
                var oa = a.EnumerateObject().ToList();
                return oa.Count == b.EnumerateObject().Count() && oa.All(p => b.TryGetProperty(p.Name, out var v) && JsonEq(p.Value, v));
            case JsonValueKind.String: return a.GetString() == b.GetString();
            default: return a.GetRawText() == b.GetRawText();
        }
    }
}
