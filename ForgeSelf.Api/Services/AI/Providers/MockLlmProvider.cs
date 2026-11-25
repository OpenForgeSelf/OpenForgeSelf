using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Providers;

/// <summary>一条要模拟的工具调用。参数是 JSON 对象文本，可含 <c>{{cwd}}</c> 与 <c>{{file:相对路径}}</c>。</summary>
public sealed record MockLlmToolCall(string Name, string ArgumentsJson);

/// <summary>模型被叫到的一轮。可以只有正文、只有工具，或两者都有。</summary>
public sealed record MockLlmStep(string Content, IReadOnlyList<MockLlmToolCall>? ToolCalls = null);

/// <summary>
/// 一条 mock 规则。没有 <see cref="Steps"/> 时，<see cref="Content"/> 和 <see cref="ToolCalls"/> 就是唯一一轮。
/// 有 <see cref="Steps"/> 时按模型被叫的次数往下走：第 1 次用第 1 步，工具结果回来后的下一次用第 2 步。
/// </summary>
public sealed record MockLlmRule(
    string Key,
    string Content,
    IReadOnlyList<MockLlmToolCall>? ToolCalls = null,
    IReadOnlyList<MockLlmStep>? Steps = null);

/// <summary>一次 mock 决策：正文，以及这一轮要不要发出工具调用。</summary>
public readonly record struct MockLlmTurn(string Content, IReadOnlyList<MockLlmToolCall> ToolCalls);

/// <summary>从 mock-llm.json 读出的规则。测试可直接构造，不必落盘。</summary>
public sealed class MockLlmDocument
{
    public bool Found { get; init; }
    public bool Invalid { get; init; }
    public string Error { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string Fallback { get; init; } = "（mock）未命中关键字";
    public IReadOnlyList<MockLlmRule> Rules { get; init; } = Array.Empty<MockLlmRule>();
}

/// <summary>
/// 读 <c>config/mock-llm.json</c>（或环境变量 <c>FORGESELF_MOCK_LLM</c>）。
/// 每次调用都重新读文件，改完保存即可，不必重启。
/// </summary>
public static class MockLlmRules
{
    public const string EnvVar = "FORGESELF_MOCK_LLM";
    public const string DefaultEndpoint = "mock://local";
    public const string DefaultModel = "mock";

    public static void Normalize(AIProviderConfig config)
    {
        if (config.ProviderType != AIProviderType.Mock)
            return;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            config.Endpoint = DefaultEndpoint;
        if (config.SupportedModels == null
            || config.SupportedModels.Count == 0
            || config.SupportedModels.All(string.IsNullOrWhiteSpace))
        {
            config.SupportedModels = new List<string> { DefaultModel };
        }
    }

    public static MockLlmDocument Load(string? explicitPath = null)
    {
        var path = explicitPath;
        if (string.IsNullOrWhiteSpace(path))
            path = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(path))
            path = FindDefaultPath();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Missing(path);

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root == null)
                return Invalid(path, "规则文件必须是 JSON 对象");

            var fallback = Str(root["fallback"]) ?? "（mock）未命中关键字";
            var rules = new List<MockLlmRule>();
            if (root["rules"] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is not JsonObject obj)
                        continue;
                    var key = Str(obj["key"]) ?? string.Empty;
                    if (key.Length == 0)
                        continue;
                    rules.Add(new MockLlmRule(
                        key,
                        Str(obj["content"]) ?? string.Empty,
                        ReadToolCalls(obj),
                        ReadSteps(obj)));
                }
            }

            return new MockLlmDocument
            {
                Found = true,
                SourcePath = path,
                Fallback = fallback,
                Rules = rules
            };
        }
        catch (Exception ex)
        {
            return Invalid(path, ex.Message);
        }
    }

    /// <summary>取最长命中。相同长度时保留文件里更靠前的那条。没有命中时返回 null。</summary>
    public static MockLlmRule? MatchRule(MockLlmDocument doc, string text)
    {
        MockLlmRule? best = null;
        foreach (var rule in doc.Rules)
        {
            if (rule.Key.Length == 0)
                continue;
            if (text.Contains(rule.Key, StringComparison.Ordinal)
                && (best == null || rule.Key.Length > best.Key.Length))
            {
                best = rule;
            }
        }

        return best;
    }

    /// <summary>取最长命中的正文。没有命中时用 fallback。</summary>
    public static string Match(MockLlmDocument doc, string text)
    {
        var rule = MatchRule(doc, text);
        return rule != null ? rule.Content : doc.Fallback;
    }

    /// <summary>
    /// 把参数 JSON 里的 <c>{{cwd}}</c> 换成当前项目根，<c>{{file:相对路径}}</c> 换成根下的绝对路径。
    /// 项目根来自 <see cref="MockLlmBindings.ProjectRoot"/>，没有时再从请求文本里找已存在的绝对目录。
    /// </summary>
    public static string ApplyPlaceholders(string argumentsJson, string requestText)
    {
        if (argumentsJson.IndexOf("{{", StringComparison.Ordinal) < 0)
            return argumentsJson;
        var cwd = ResolveCwd(requestText);
        if (string.IsNullOrEmpty(cwd))
            return argumentsJson;

        var replaced = argumentsJson.Replace("{{cwd}}", EscapeJson(cwd), StringComparison.Ordinal);
        return FilePlaceholder.Replace(replaced, match =>
        {
            var relative = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            return EscapeJson(Path.Combine(cwd, relative));
        });
    }

    private static readonly System.Text.RegularExpressions.Regex FilePlaceholder = new(
        @"\{\{file:([^}]+)\}\}",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string? ResolveCwd(string requestText)
    {
        var hooked = MockLlmBindings.ProjectRoot?.Invoke();
        if (!string.IsNullOrWhiteSpace(hooked) && Directory.Exists(hooked))
            return Path.GetFullPath(hooked);

        foreach (System.Text.RegularExpressions.Match match in AbsoluteDir.Matches(requestText))
        {
            var candidate = match.Value.TrimEnd('\\', '/', '.', ',', ';', '"');
            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static readonly System.Text.RegularExpressions.Regex AbsoluteDir = new(
        @"[A-Za-z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]*",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string EscapeJson(string value)
    {
        var quoted = System.Text.Json.JsonSerializer.Serialize(value);
        return quoted.Length >= 2 ? quoted[1..^1] : value;
    }

    private static IReadOnlyList<MockLlmToolCall>? ReadToolCalls(JsonObject obj)
    {
        if (obj["toolCalls"] is not JsonArray calls)
            return null;
        var list = new List<MockLlmToolCall>();
        foreach (var node in calls)
        {
            if (node is not JsonObject call)
                continue;
            var name = Str(call["name"]) ?? string.Empty;
            if (name.Length == 0)
                continue;
            var arguments = call["arguments"] switch
            {
                JsonObject or JsonArray => call["arguments"]!.ToJsonString(),
                JsonValue value when value.TryGetValue<string>(out var text) => text,
                _ => "{}"
            };
            list.Add(new MockLlmToolCall(name, string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments));
        }

        return list.Count == 0 ? null : list;
    }

    private static IReadOnlyList<MockLlmStep>? ReadSteps(JsonObject obj)
    {
        if (obj["steps"] is not JsonArray steps)
            return null;
        var list = new List<MockLlmStep>();
        foreach (var node in steps)
        {
            if (node is not JsonObject step)
                continue;
            list.Add(new MockLlmStep(Str(step["content"]) ?? string.Empty, ReadToolCalls(step)));
        }

        return list.Count == 0 ? null : list;
    }

    /// <summary>
    /// 下一步的下标。已有工具结果时按结果条数计（同一次回复里的正文和工具只算一步）。
    /// 还没有工具结果时，退回助手消息条数，纯文本步也能往下走。
    /// </summary>
    public static int PriorModelTurns(IEnumerable<UnifiedChatMessage> messages)
    {
        var list = messages as IList<UnifiedChatMessage> ?? messages.ToList();
        var toolResults = list.Count(message =>
            string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(message.ToolCallId));
        if (toolResults > 0)
            return toolResults;
        return list.Count(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase));
    }

    public static string CollectText(UnifiedChatRequest request)
    {
        var sb = new StringBuilder();
        Append(sb, request.SystemPrompt);
        foreach (var message in request.Messages)
        {
            Append(sb, message.Content);
            if (message.ContentBlocks == null)
                continue;
            foreach (var block in message.ContentBlocks)
                Append(sb, block.Text);
        }

        return sb.ToString();
    }

    public static string? FindDefaultPath()
    {
        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "config", "mock-llm.json");
        if (File.Exists(cwd))
            return cwd;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "config", "mock-llm.json");
            if (File.Exists(candidate))
                return candidate;
        }

        return cwd;
    }

    private static void Append(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (sb.Length > 0)
            sb.Append('\n');
        sb.Append(text);
    }

    private static string? Str(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static MockLlmDocument Missing(string? path)
    {
        var error = string.IsNullOrWhiteSpace(path)
            ? "未找到 mock 规则文件。将 config/mock-llm.sample.json 复制为 config/mock-llm.json，或设置 FORGESELF_MOCK_LLM。"
            : $"未找到 mock 规则文件：{path}。将 config/mock-llm.sample.json 复制为该路径，或设置 FORGESELF_MOCK_LLM。";
        return new MockLlmDocument { Found = false, Error = error, Fallback = error, SourcePath = path ?? string.Empty };
    }

    private static MockLlmDocument Invalid(string path, string message)
    {
        var error = $"mock 规则文件无法解析：{path}。{message}";
        return new MockLlmDocument
        {
            Found = true,
            Invalid = true,
            Error = error,
            Fallback = error,
            SourcePath = path
        };
    }
}

/// <summary>不访问网络的提供方。命中规则 key 就返回对应 content。</summary>
public sealed class MockLlmProvider : IAIProvider
{
    private readonly AIProviderConfig _config;
    private readonly MockLlmDocument? _pinned;

    public MockLlmProvider(AIProviderConfig config, MockLlmDocument? pinned = null)
    {
        _config = config;
        _pinned = pinned;
        MockLlmRules.Normalize(_config);
    }

    public string ProviderName => _config.Name;
    public AIProviderType ProviderType => AIProviderType.Mock;
    public List<string> SupportedModels => _config.SupportedModels;
    public bool IsDefault => _config.IsDefault;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var turn = Describe(request);
        var model = string.IsNullOrWhiteSpace(request.Model) ? MockLlmRules.DefaultModel : request.Model;
        var calling = turn.ToolCalls.Count > 0;
        var response = new UnifiedChatResponse
        {
            Id = NewId(),
            Model = model,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProviderName = ProviderName,
            FinishReason = calling ? "tool_calls" : "stop",
            Usage = Usage(request, turn.Content),
            Choices = new List<UnifiedChatMessage>
            {
                new()
                {
                    Role = "assistant",
                    Content = calling ? string.Empty : turn.Content,
                    ToolCalls = calling
                        ? turn.ToolCalls.Select((call, index) => new UnifiedToolCall
                        {
                            Id = "call-mock-" + index,
                            Name = call.Name,
                            Arguments = call.ArgumentsJson
                        }).ToList()
                        : null
                }
            }
        };
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var turn = Describe(request);
        var model = string.IsNullOrWhiteSpace(request.Model) ? MockLlmRules.DefaultModel : request.Model;
        var id = NewId();
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (turn.ToolCalls.Count > 0)
        {
            if (!string.IsNullOrEmpty(turn.Content))
            {
                yield return new UnifiedStreamChunk
                {
                    Id = id,
                    Model = model,
                    Created = created,
                    DeltaRole = "assistant",
                    DeltaContent = turn.Content
                };
            }

            for (var index = 0; index < turn.ToolCalls.Count; index++)
            {
                var call = turn.ToolCalls[index];
                yield return new UnifiedStreamChunk
                {
                    Id = id,
                    Model = model,
                    Created = created,
                    DeltaRole = index == 0 ? "assistant" : null,
                    DeltaToolCall = new UnifiedToolCall
                    {
                        Id = "call-mock-" + index,
                        Name = call.Name,
                        Arguments = call.ArgumentsJson
                    }
                };
            }

            yield return new UnifiedStreamChunk
            {
                Id = id,
                Model = model,
                Created = created,
                FinishReason = "tool_calls",
                Usage = Usage(request, turn.Content)
            };
        }
        else
        {
            yield return new UnifiedStreamChunk
            {
                Id = id,
                Model = model,
                Created = created,
                DeltaRole = "assistant",
                DeltaContent = turn.Content,
                FinishReason = "stop",
                Usage = Usage(request, turn.Content)
            };
        }

        yield return new UnifiedStreamChunk { Id = id, Model = model, Created = created, IsDone = true };
    }

    /// <summary>按规则文件决定这一轮的正文和工具调用。同一轮用户消息里已经有工具结果时，单步规则不再重复发工具。</summary>
    public MockLlmTurn Describe(UnifiedChatRequest request)
    {
        var doc = _pinned ?? MockLlmRules.Load();
        var text = MockLlmRules.CollectText(request);
        var rule = MockLlmRules.MatchRule(doc, text);
        var multi = rule?.Steps is { Count: > 1 };
        IReadOnlyList<MockLlmStep> steps = multi
            ? rule!.Steps!
            : new[] { new MockLlmStep(rule?.Content ?? doc.Fallback, rule?.ToolCalls) };
        var index = MockLlmRules.PriorModelTurns(request.Messages);
        var sinceUser = MessagesSinceLastUser(request.Messages);
        var continued = sinceUser.Any(message =>
            string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(message.ToolCallId));
        if (!multi && continued)
            return new MockLlmTurn(steps[0].Content, Array.Empty<MockLlmToolCall>());
        if (index >= steps.Count)
            index = steps.Count - 1;
        var step = steps[index];
        var calls = (step.ToolCalls ?? Array.Empty<MockLlmToolCall>())
            .Select(call => new MockLlmToolCall(call.Name, MockLlmRules.ApplyPlaceholders(call.ArgumentsJson, text)))
            .ToArray();
        // 单步规则的 content 是工具结果回来后的回复，不能和第一次工具调用一起发出。
        if (!multi && calls.Length > 0)
            return new MockLlmTurn(string.Empty, calls);
        return new MockLlmTurn(step.Content, calls);
    }

    /// <summary>上一条用户消息之后的消息。上一步的工具结果不算进这一步。</summary>
    private static IEnumerable<UnifiedChatMessage> MessagesSinceLastUser(IReadOnlyList<UnifiedChatMessage> messages)
    {
        var start = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }
        for (var i = start; i < messages.Count; i++)
            yield return messages[i];
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var models = _config.SupportedModels
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => new ModelInfo
            {
                Id = m,
                Name = m,
                Owner = ProviderName,
                Created = now,
                ProviderName = ProviderName
            })
            .ToList();
        return Task.FromResult(models);
    }



    private static UnifiedUsage Usage(UnifiedChatRequest request, string content)
    {
        var prompt = MockLlmRules.CollectText(request).Length;
        var completion = content.Length;
        return new UnifiedUsage
        {
            PromptTokens = prompt,
            CompletionTokens = completion,
            TotalTokens = prompt + completion
        };
    }

    private static string NewId() => "chatcmpl-mock-" + Guid.NewGuid().ToString("N");
}
