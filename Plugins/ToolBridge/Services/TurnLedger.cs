using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.ToolBridge.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 回合台账（PILOT-053 02-spec FR-6）：一轮一个 JSON 文件，落在插件数据目录 <c>ledger/</c> 下。
/// **只追加、永不删除、不做清理自动化**（plugin-development 铁律 10）；
/// 读到损坏文件只标 corrupt，不把列表炸掉（BC-8）。
/// </summary>
public sealed class TurnLedger
{
    private static readonly Regex SafeTurnId = new(@"^[0-9a-zA-Z][0-9a-zA-Z-]{5,47}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions FileOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string LedgerDirectory { get; }

    public TurnLedger(string pluginDataDir)
    {
        LedgerDirectory = Path.Combine(pluginDataDir, "ledger");
    }

    /// <summary>turnId = 时间码 + 8 位随机（可排序、文件名安全，BR-7）。</summary>
    public static string NewTurnId() =>
        $"{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

    public bool TryAppend(TurnRecord record, out string? error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(LedgerDirectory);
            var path = Path.Combine(LedgerDirectory, record.TurnId + ".json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(record, FileOptions), SandboxRoot.NoBomUtf8);
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"台账写入失败（本轮结果仍可用，只是没留档）：{ex.Message}";
            XTrace.Log.Warn("[tool-bridge] {0}", error);
            return false;
        }
    }

    public TurnList List(int take, int skip)
    {
        var list = new TurnList { LedgerDirectory = LedgerDirectory };
        if (!Directory.Exists(LedgerDirectory)) return list;

        var files = Directory.EnumerateFiles(LedgerDirectory, "*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        list.Total = files.Count;

        take = Math.Clamp(take, 1, 200);
        foreach (var path in files.Skip(Math.Max(0, skip)).Take(take))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var record = JsonSerializer.Deserialize<TurnRecord>(json, ReadOptions);
                if (record == null || string.IsNullOrEmpty(record.TurnId))
                {
                    list.Items.Add(new TurnSummary { TurnId = name, Corrupt = true, Preview = "记录内容无法解析" });
                    continue;
                }

                list.Items.Add(new TurnSummary
                {
                    TurnId = record.TurnId,
                    CreatedAt = record.CreatedAt,
                    Stats = record.Stats,
                    Preview = Preview(record.Text)
                });
            }
            catch (Exception ex)
            {
                // BC-8：损坏文件不炸列表。
                list.Items.Add(new TurnSummary { TurnId = name, Corrupt = true, Preview = ex.Message });
            }
        }

        return list;
    }

    public TurnRecord? Get(string turnId, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(turnId) || !SafeTurnId.IsMatch(turnId))
        {
            error = "turnId 非法（只允许字母数字与连字符，防路径穿越）";
            return null;
        }

        var path = Path.Combine(LedgerDirectory, turnId + ".json");
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<TurnRecord>(File.ReadAllText(path, System.Text.Encoding.UTF8), ReadOptions);
        }
        catch (Exception ex)
        {
            error = $"记录无法解析：{ex.Message}";
            return null;
        }
    }

    private static string Preview(string text)
    {
        var t = (text ?? string.Empty).Replace("\n", " ").Trim();
        return t.Length <= 120 ? t : t[..120] + "…";
    }
}
