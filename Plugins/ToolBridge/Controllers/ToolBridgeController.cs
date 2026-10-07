using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ToolBridge.Models;
using ForgeSelf.Api.Plugins.ToolBridge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ToolBridge.Controllers;

/// <summary>
/// 工具桥管理面（PILOT-053 02-spec FR-8）。
/// 全部端点都会在用户机器上读写文件、启动子进程 ⇒ 类级鉴权（plugin-development 铁律 17；
/// 宿主无全局鉴权中间件，策略声明见 <c>ForgeSelf.Api/AppBuilder.cs:296-301</c>）。
/// 业务层失败（未识别/越界/被拒/超时）走 HTTP 200 + results 逐条表达；只有请求本身不合法才 400。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/tool-bridge")]
public class ToolBridgeController : ControllerBase
{
    private readonly SandboxRoot _sandbox;
    private readonly TurnLedger _ledger;

    public ToolBridgeController(SandboxRoot sandbox, TurnLedger ledger)
    {
        _sandbox = sandbox;
        _ledger = ledger;
    }

    /// <summary>GET prompt：可复制的初始指令（与解析别名、界面清单同源于 ToolSpec，FR-1.2）。</summary>
    [HttpGet("prompt")]
    public ActionResult<ApiResponse<PromptResponse>> GetPrompt()
    {
        var data = new PromptResponse
        {
            Text = PromptBuilder.Build(),
            Tools = PromptBuilder.ToolsAsJson()
        };
        return Ok(ApiResponse<PromptResponse>.Ok(data, "初始指令已生成"));
    }

    /// <summary>POST parse：只解析、零副作用（AC3）。文本上限 200 KB。</summary>
    [HttpPost("parse")]
    public ActionResult<ApiResponse<ParseResult>> Parse([FromBody] TextRequest request)
    {
        var text = request?.Text;
        if (!ValidText(text, out var textError))
        {
            return BadRequest(ApiResponse<ParseResult>.Error(textError!, 400));
        }

        return Ok(ApiResponse<ParseResult>.Ok(CallParser.Parse(text), "解析完成（未执行任何操作）"));
    }

    /// <summary>POST execute：执行已解析出的调用（界面「只解析看清了再执行」；不落台账）。</summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ApiResponse<ExecuteResponse>>> Execute([FromBody] ExecuteRequest? request,
        CancellationToken cancellationToken)
    {
        var calls = request?.Calls ?? new List<ParsedCall>();
        if (calls.Count == 0)
        {
            return BadRequest(ApiResponse<ExecuteResponse>.Error("calls 为空：请先解析出调用再执行", 400));
        }

        var start = Environment.TickCount64;
        var results = await RunAllAsync(calls, cancellationToken);
        var data = new ExecuteResponse
        {
            Results = results,
            ResultTextJson = ResultFormatter.BuildJson(results, new List<UnknownCall>(), new List<UnparsedFragment>()),
            ResultTextPlain = ResultFormatter.BuildPlain(results, new List<UnknownCall>(), new List<UnparsedFragment>()),
            Executed = results.Count(r => r.Ok),
            Rejected = results.Count(r => !r.Ok),
            DurationMs = Environment.TickCount64 - start
        };
        return Ok(ApiResponse<ExecuteResponse>.Ok(data, $"已执行 {results.Count} 条（未落台账）"));
    }

    /// <summary>POST turn：解析 + 执行 + 回粘文本 + 落台账（一轮完整回合）。</summary>
    [HttpPost("turn")]
    public async Task<ActionResult<ApiResponse<TurnResponse>>> Turn([FromBody] TextRequest? request,
        CancellationToken cancellationToken)
    {
        var text = request?.Text;
        if (!ValidText(text, out var textError))
        {
            return BadRequest(ApiResponse<TurnResponse>.Error(textError!, 400));
        }

        var start = Environment.TickCount64;
        // 图 2 缺口③：turn 自己再解析一次，不吃上一次 parse 的结果（防"界面看到的 ≠ 实际执行的"）。
        var parsed = CallParser.Parse(text);
        var results = await RunAllAsync(parsed.Calls, cancellationToken);

        var response = new TurnResponse
        {
            TurnId = TurnLedger.NewTurnId(),
            Calls = parsed.Calls,
            Unknown = parsed.Unknown,
            Unparsed = parsed.Unparsed,
            Results = results,
            ResultTextJson = ResultFormatter.BuildJson(results, parsed.Unknown, parsed.Unparsed),
            ResultTextPlain = ResultFormatter.BuildPlain(results, parsed.Unknown, parsed.Unparsed),
            Stats = new TurnStats
            {
                Recognized = parsed.Calls.Count,
                Unknown = parsed.Unknown.Count,
                Unparsed = parsed.Unparsed.Count,
                Executed = results.Count(r => r.Ok),
                Rejected = results.Count(r => !r.Ok),
                DurationMs = Environment.TickCount64 - start
            }
        };

        var record = new TurnRecord
        {
            TurnId = response.TurnId,
            CreatedAt = DateTimeOffset.Now.ToString("O"),
            WorkspaceRoot = _sandbox.Current().Root,
            Text = text!,
            Calls = parsed.Calls,
            Unknown = parsed.Unknown,
            Unparsed = parsed.Unparsed,
            Results = results,
            ResultTextJson = response.ResultTextJson,
            ResultTextPlain = response.ResultTextPlain,
            Stats = response.Stats
        };

        if (!_ledger.TryAppend(record, out var ledgerError))
        {
            // 图 2 缺口④：结果已拿到但没留档 ⇒ 必须显式告诉用户，别让他去翻空的台账。
            response.LedgerError = ledgerError;
        }

        return Ok(ApiResponse<TurnResponse>.Ok(response,
            $"一轮完成：识别 {response.Stats.Recognized} / 执行 {response.Stats.Executed} / 被拒 {response.Stats.Rejected}"));
    }

    /// <summary>GET workspace：当前工作根读数。</summary>
    [HttpGet("workspace")]
    public ActionResult<ApiResponse<WorkspaceResponse>> GetWorkspace()
    {
        var info = _sandbox.Current();
        var data = new WorkspaceResponse
        {
            Root = info.Root,
            Exists = info.Exists,
            DefaultRoot = info.DefaultRoot,
            Source = info.Source,
            Dangerous = info.Dangerous
        };
        return Ok(ApiResponse<WorkspaceResponse>.Ok(data, "当前工作根"));
    }

    /// <summary>PUT workspace：改工作根（点即保存，落 settings.json，重启后仍生效，AC10）。</summary>
    [HttpPut("workspace")]
    public ActionResult<ApiResponse<WorkspaceResponse>> PutWorkspace([FromBody] WorkspaceRequest request)
    {
        // 危险根（盘符根/用户目录根/git 仓库树内）在 SandboxRoot 内判定并给出原因；
        // 未带 confirmUnsafe 即 400，原因文本已含"确认要用请带 confirmUnsafe=true"（BC-11 单一真源）。
        if (!_sandbox.TrySetRoot(request.Root, request.ConfirmUnsafe, out var info, out var error))
        {
            return BadRequest(ApiResponse<WorkspaceResponse>.Error(error!, 400));
        }

        var data = ToResponse(info!);
        XTrace.Log.Info("[tool-bridge] 工作根已切换：{0}（dangerous={1}）", data.Root, data.Dangerous);
        return Ok(ApiResponse<WorkspaceResponse>.Ok(data, "工作根已保存"));
    }

    /// <summary>GET turns：台账倒序列表（不含原文与结果全文，AC11）。</summary>
    [HttpGet("turns")]
    public ActionResult<ApiResponse<TurnList>> ListTurns([FromQuery] int take = 20, [FromQuery] int skip = 0)
    {
        return Ok(ApiResponse<TurnList>.Ok(_ledger.List(take, skip), "回合记录"));
    }

    /// <summary>GET turns/{turnId}：单轮完整记录（含粘贴原文）。</summary>
    [HttpGet("turns/{turnId}")]
    public ActionResult<ApiResponse<TurnRecord>> GetTurn(string turnId)
    {
        var record = _ledger.Get(turnId, out var error);
        if (error != null)
        {
            return BadRequest(ApiResponse<TurnRecord>.Error(error, 400));
        }
        if (record == null)
        {
            return NotFound(ApiResponse<TurnRecord>.Error($"回合不存在：{turnId}", 404));
        }
        return Ok(ApiResponse<TurnRecord>.Ok(record, "回合详情"));
    }

    private async Task<List<ToolResult>> RunAllAsync(List<ParsedCall> calls, CancellationToken cancellationToken)
    {
        var list = new List<ToolResult>(calls.Count);
        foreach (var call in calls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            list.Add(await ToolDispatcher.DispatchAsync(call, _sandbox));
        }
        return list;
    }

    private static bool ValidText(string? text, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "text 为空：请把 AI 的回复整段粘贴进来";
            return false;
        }
        if (System.Text.Encoding.UTF8.GetByteCount(text) > ToolSpec.MaxTextBytes)
        {
            error = $"text 过大（超过 {ToolSpec.MaxTextBytes} 字节）：请分段粘贴";
            return false;
        }
        error = null;
        return true;
    }

    private static WorkspaceResponse ToResponse(WorkspaceInfo info) => new()
    {
        Root = info.Root,
        Exists = info.Exists,
        DefaultRoot = info.DefaultRoot,
        Source = info.Source,
        Dangerous = info.Dangerous
    };
}
