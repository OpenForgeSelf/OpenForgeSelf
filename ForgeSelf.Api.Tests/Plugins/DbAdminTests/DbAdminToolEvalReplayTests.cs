using System.Text.Json;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>
/// 评测集 Plugins/DbAdmin/docs/agent-tool-eval-cases.json 的结构校验 + 逐条回放（临时 SQLite）。
/// 回放验证的是“按期望序列调用时，系统给出的就是用例写明的结果”——评测集与真实行为不漂移。
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
            var steps = c.GetProperty("steps").EnumerateArray().ToList();
            Assert.NotEmpty(steps);
            foreach (var s in steps) Assert.Contains(s.GetProperty("tool").GetString(), ToolNames);
        }
        var negCats = cases.Where(c => c.GetProperty("negative").GetBoolean()).Select(c => c.GetProperty("category").GetString()!).ToList();
        Assert.Contains("negative-drop-database", negCats);
        Assert.Contains("negative-bypass-approval", negCats);
        Assert.Contains("negative-read-password", negCats);
        Assert.Contains("negative-write-system", negCats);
    }

    [Fact]
    public void EvalCases_Replay_MatchExpectations()
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
            var i = 0;
            foreach (var step in c.GetProperty("steps").EnumerateArray())
            {
                i++;
                var tool = step.GetProperty("tool").GetString()!;
                var args = Substitute(step.TryGetProperty("args", out var a) ? a : default, vars);
                var raw = tools[tool].ExecuteAsync(args).GetAwaiter().GetResult();
                outputs.Add(raw);
                using var doc = JsonDocument.Parse(raw);
                var res = doc.RootElement;
                if (res.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("approvalId", out var ap) && ap.ValueKind == JsonValueKind.Number)
                    vars["$approvalId"] = ap.GetInt64();
                if (step.TryGetProperty("expect", out var exp))
                    foreach (var e in exp.EnumerateObject())
                    {
                        var actual = JPath(res, e.Name);
                        if (actual is null || !JsonEq(actual.Value, e.Value))
                            failures.Add($"{id} step{i} {tool}: 期望 {e.Name}={e.Value.GetRawText()}，实际 {(actual is null ? "<缺失>" : actual.Value.GetRawText())}；回包 {raw}");
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

    private static Boolean JsonEq(JsonElement a, JsonElement b) =>
        a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number
            ? a.GetDecimal() == b.GetDecimal()
            : a.GetRawText() == b.GetRawText();
}
