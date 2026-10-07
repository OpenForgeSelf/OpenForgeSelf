namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 调用分派（PILOT-053 02-spec FR-3）：规范名 → 执行体；必填参数缺失先报 <c>missing_argument</c>，
/// 让 AI 看到自己漏了什么，而不是抛异常把整轮打断。
/// </summary>
public static class ToolDispatcher
{
    public static async Task<ToolResult> DispatchAsync(ParsedCall call, SandboxRoot sandbox)
    {
        var entry = ToolSpec.Find(call.Tool);
        if (entry == null)
        {
            return new ToolResult
            {
                Tool = call.Tool,
                RawName = call.RawName,
                Ok = false,
                Args = call.Args,
                Error = "unknown_tool",
                Reason = $"工具「{call.RawName}」不在清单内（可用：{string.Join(" / ", ToolSpec.All.Select(t => t.Name))}）"
            };
        }

        var missing = entry.Required.FirstOrDefault(r => !HasRequired(call, r));
        if (missing != null)
        {
            return new ToolResult
            {
                Tool = call.Tool,
                RawName = call.RawName,
                Ok = false,
                Args = call.Args,
                Error = $"missing_argument: {missing}",
                Reason = $"缺少必填参数「{missing}」（入参 schema：{entry.ParametersSchema.Trim()}）"
            };
        }

        switch (call.Tool)
        {
            case ToolSpec.ReadFile:
                return FileExecutor.Read(sandbox, call.GetArgString("path"));

            case ToolSpec.WriteFile:
                return FileExecutor.Write(sandbox, call.GetArgString("path"),
                    call.GetArgString("content") ?? string.Empty, ReadAppend(call));

            case ToolSpec.ListDir:
                return FileExecutor.ListDir(sandbox, call.GetArgString("path"));

            case ToolSpec.RunCommand:
                return await CommandExecutor.RunAsync(sandbox, call.GetArgString("command"),
                    call.GetArgString("cwd"), call.GetArgInt("timeoutSeconds"), call.Args);

            default:
                return new ToolResult
                {
                    Tool = call.Tool,
                    RawName = call.RawName,
                    Ok = false,
                    Args = call.Args,
                    Error = "not_implemented",
                    Reason = $"工具「{call.Tool}」在清单内但没有执行体（实现缺陷，请反馈）"
                };
        }
    }

    private static bool HasRequired(ParsedCall call, string key)
    {
        if (!call.Args.TryGetValue(key, out var node) || node == null) return false;
        if (node.GetValueKind() == JsonValueKind.String)
        {
            var s = node.GetValue<string>();
            // path/command 空串等于没给；content 空串是合法的空文件（02-spec Input 表）。
            return key == "content" || !string.IsNullOrWhiteSpace(s);
        }
        return true;
    }

    private static bool ReadAppend(ParsedCall call)
    {
        if (!call.Args.TryGetValue("append", out var node) || node == null) return false;
        try
        {
            return node.GetValueKind() switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => string.Equals(node.GetValue<string>(), "true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
