using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Providers;

/// <summary>一条 mock 规则：请求文本里出现 <see cref="Key"/> 时，回复 <see cref="Content"/>。</summary>
public sealed record MockLlmRule(string Key, string Content);

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
                    rules.Add(new MockLlmRule(key, Str(obj["content"]) ?? string.Empty));
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

    /// <summary>取最长命中。相同长度时保留文件里更靠前的那条。</summary>
    public static string Match(MockLlmDocument doc, string text)
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

        return best != null ? best.Content : doc.Fallback;
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
        var content = Reply(request);
        var model = string.IsNullOrWhiteSpace(request.Model) ? MockLlmRules.DefaultModel : request.Model;
        var response = new UnifiedChatResponse
        {
            Id = NewId(),
            Model = model,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProviderName = ProviderName,
            FinishReason = "stop",
            Usage = Usage(request, content),
            Choices = new List<UnifiedChatMessage>
            {
                new() { Role = "assistant", Content = content }
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
        var content = Reply(request);
        var model = string.IsNullOrWhiteSpace(request.Model) ? MockLlmRules.DefaultModel : request.Model;
        var id = NewId();
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        yield return new UnifiedStreamChunk
        {
            Id = id,
            Model = model,
            Created = created,
            DeltaRole = "assistant",
            DeltaContent = content,
            FinishReason = "stop",
            Usage = Usage(request, content)
        };
        yield return new UnifiedStreamChunk { Id = id, Model = model, Created = created, IsDone = true };
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

    private string Reply(UnifiedChatRequest request)
    {
        var doc = _pinned ?? MockLlmRules.Load();
        return MockLlmRules.Match(doc, MockLlmRules.CollectText(request));
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
