using System.Collections.Generic;
using ForgeSelf.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;

namespace ForgeSelf.Api.Services;

/// <summary>
/// AI 提供方服务接口：负责 <see cref="AIProvider"/> 实体与运行期 <see cref="AIProviderConfig"/> 的相互转换、
/// 业务校验（名称唯一、Endpoint 非空、唯一默认约束）以及首次播种。
/// 经 <see cref="IAIProviderRepository"/> 读写，实体 ApiKey 为密文；需要明文时由本服务显式解密（谁要解密自己解）。
/// </summary>
public interface IAIProviderService
{
    /// <summary>获取全部提供方（ApiKey 为密文）</summary>
    IList<AIProvider> GetAll();

    /// <summary>按 Id 获取，不存在返回 null</summary>
    AIProvider? GetById(long id);

    /// <summary>获取默认提供方，不存在返回 null</summary>
    AIProvider? GetDefault();

    /// <summary>新增提供方；返回保存后的实体（含 Id，ApiKey 为密文）</summary>
    AIProvider Create(AIProviderConfig config);

    /// <summary>更新指定 Id 的提供方；不存在返回 null</summary>
    AIProvider? Update(long id, AIProviderConfig config);

    /// <summary>删除指定提供方（默认提供方需另有候选，否则抛异常）</summary>
    void Delete(long id);

    /// <summary>首次播种：仅当表为空时，将默认配置注入；已存在任意记录则跳过（幂等）</summary>
    void EnsureSeeded(IEnumerable<AIProviderConfig> defaults);

    /// <summary>导出全部提供方为运行期配置（供 AIProviderRegistry 重载使用，ApiKey 为明文）</summary>
    List<AIProviderConfig> GetAllConfigs();

    /// <summary>
    /// 测试指定提供方连通性：解密 ApiKey 后发一次最小探测请求，返回耗时与结果。
    /// modelId 为空=仅探连通（URL 归一化后最小 ping，任一 HTTP 响应即「代理/网络可用」）；
    /// 非空=用该已登记模型发真实 chat（content「请回复且仅回复ok」），成功判据=HTTP 200 且响应文本含 ok（忽略大小写）。
    /// </summary>
    Task<AIProviderTestResult> TestConnectionAsync(long id, long? modelId = null);
}

/// <summary>
/// AI 提供方服务实现。
/// </summary>
public class AIProviderService : IAIProviderService
{
    private readonly IAIProviderRepository _repo;
    private readonly ISecretEncryptionService _encryption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAIModelService _modelService;

    public AIProviderService(IAIProviderRepository repo, ISecretEncryptionService encryption, IHttpClientFactory httpClientFactory, IAIModelService modelService)
    {
        _repo = repo;
        _encryption = encryption;
        _httpClientFactory = httpClientFactory;
        _modelService = modelService;
    }

    /// <inheritdoc/>
    public IList<AIProvider> GetAll() => _repo.GetAll();

    /// <inheritdoc/>
    public AIProvider? GetById(long id) => _repo.GetById(id);

    /// <inheritdoc/>
    public AIProvider? GetDefault() => _repo.GetDefault();

    /// <inheritdoc/>
    public List<AIProviderConfig> GetAllConfigs()
    {
        var configs = new List<AIProviderConfig>();
        foreach (var e in _repo.GetAll())
        {
            configs.Add(ToConfig(e));
        }
        return configs;
    }

    /// <inheritdoc/>
    public AIProvider Create(AIProviderConfig config)
    {
        ValidateForCreate(config);

        var entity = ToEntity(config);
        var saved = _repo.Create(entity);

        // 保证唯一默认约束：若置为默认则清除其他默认；若无默认则提升首个
        if (saved.IsDefault) UnsetOtherDefaults(saved.Id);
        EnsureSingleDefault();

        // 重新读取以返回与库一致的最新状态（默认可能被自动提升）；返回密文实体
        return _repo.GetById(saved.Id)!;
    }

    /// <inheritdoc/>
    public AIProvider? Update(long id, AIProviderConfig config)
    {
        if (_repo.GetById(id) == null) return null;

        ValidateForUpdate(id, config);

        var entity = ToEntity(config);
        entity.Id = id; // 关键：保留主键
        var saved = _repo.Update(entity);
        if (saved == null) return null;

        if (saved.IsDefault) UnsetOtherDefaults(saved.Id);
        EnsureSingleDefault();

        return _repo.GetById(saved.Id)!;
    }

    /// <inheritdoc/>
    public void Delete(long id)
    {
        var target = _repo.GetById(id);
        if (target == null) return;

        // 删除默认前的"存在其他默认"守卫：自动提升第一个其他提供方，否则拒绝
        if (target.IsDefault)
        {
            var others = _repo.GetAll().Where(p => p.Id != id).ToList();
            if (others.Count == 0)
            {
                throw new InvalidOperationException("无法删除唯一的默认提供方，请先新增其他提供方或取消其默认状态。");
            }

            _repo.SetDefault(others[0].Id, true);
        }

        // 级联清理该供应商名下的全部模型记录（FR-010：无孤儿）
        _modelService.DeleteByProvider(id);

        _repo.Delete(id);
    }

    /// <inheritdoc/>
    public void EnsureSeeded(IEnumerable<AIProviderConfig> defaults)
    {
        // 已存在任意记录则跳过（幂等，避免重复播种）
        if (_repo.GetAll().Count > 0) return;

        foreach (var cfg in defaults)
        {
            var entity = ToEntity(cfg);
            _repo.Create(entity);
        }

        EnsureSingleDefault();
    }

    /// <inheritdoc/>
    public async Task<AIProviderTestResult> TestConnectionAsync(long id, long? modelId = null)
    {
        var entity = _repo.GetById(id);
        if (entity == null)
            return new AIProviderTestResult(false, 0, "提供方不存在", null);

        // 显式解密 ApiKey 为明文（仓储返回密文，遵循 D-4 约定：调用方按需解密）
        var config = ToConfig(entity);
        var type = config.ProviderType;

        // 测试连接不应长时间阻塞：取配置超时与 60s 的较小值（LM Studio 首次加载模型常慢于 15s）
        var testTimeout = Math.Min(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 120, 60);

        var client = _httpClientFactory.CreateClient();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 与真实聊天链路同一套 URL 归一化：Endpoint 配成基础地址（如 …/v1）时探测 /v1 必 404，
            // 必须推导到完整 chat 接口（…/v1/chat/completions 或 Anthropic …/v1/messages）才能代表真实连通性
            var testUrl = OpenAICompatibleProvider.GetChatEndpointByProviderType(type, config.Endpoint) ?? config.Endpoint;

            // 选定已登记模型时的「真实对话」探测：发一条要求仅回复 ok 的最小消息，判据为响应含 ok（忽略大小写）
            if (modelId.HasValue)
            {
                var model = AIModel.FindById(modelId.Value);
                if (model == null)
                    return new AIProviderTestResult(false, 0, "选定的模型不存在或已被删除", null);
                if (model.ProviderId != id)
                    return new AIProviderTestResult(false, 0, "选定的模型不属于该提供方", null);

                var probeModel = model.UpstreamModelId;
                var probeMsg = new[] { new { role = "user", content = "请回复且仅回复ok" } };
                // max_tokens 预算留足：思考型模型（qwen3/deepseek 等）先输出 reasoning 再输出 content，
                // 给太小（如 8）会在思考阶段就被 length 截断 → content 永远 null（2026-10-18 gpustack deepseek-v4-flash 实测）
                object payload = type == AIProviderType.Anthropic
                    ? new { model = probeModel, max_tokens = 128, messages = probeMsg }
                    : new { model = probeModel, messages = probeMsg, max_tokens = 128 };

                var probeReq = new HttpRequestMessage(HttpMethod.Post, testUrl);
                probeReq.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8, "application/json");
                if (type == AIProviderType.Anthropic)
                {
                    probeReq.Headers.Add("x-api-key", config.ApiKey);
                    probeReq.Headers.Add("anthropic-version", "2023-06-01");
                }
                else
                {
                    probeReq.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                }

                using var probeCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(testTimeout));
                var probeResp = await client.SendAsync(probeReq, probeCts.Token);
                stopwatch.Stop();
                var probeLatency = (int)stopwatch.ElapsedMilliseconds;
                var probeBody = await probeResp.Content.ReadAsStringAsync(probeCts.Token);

                if (!probeResp.IsSuccessStatusCode)
                {
                    var ps = (int)probeResp.StatusCode;
                    var pm = ps switch
                    {
                        401 or 403 => "鉴权失败：API Key 无效或权限不足",
                        _ => $"连接失败（HTTP {ps}）"
                    };
                    return new AIProviderTestResult(false, probeLatency, pm, ps);
                }

                var reply = ExtractContent(probeBody);
                // 成功判据：模型回复含 ok（忽略大小写）
                if (SaysOk(reply))
                    return new AIProviderTestResult(true, probeLatency, $"连接成功 · 模型回复「{reply}」",
                        (int)probeResp.StatusCode);
                // 解析失败时附原始响应片段（截 500 字）：便于用户/排障看清真实结构，而非黑盒「解析失败」
                var snippet = probeBody is { Length: > 500 } ? probeBody[..500] : probeBody;
                return new AIProviderTestResult(false, probeLatency,
                    reply == null ? $"响应解析失败（未取到模型回复）· 原始响应：{snippet}" : $"模型未回复 ok（实际回复：{reply}）", (int)probeResp.StatusCode);
            }

            HttpRequestMessage req;
            if (type == AIProviderType.Anthropic)
            {
                var model = config.SupportedModels.FirstOrDefault() ?? "claude-3-5-sonnet-20241022";
                req = new HttpRequestMessage(HttpMethod.Post, testUrl);
                req.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        model,
                        max_tokens = 1,
                        messages = new[] { new { role = "user", content = "ping" } }
                    }),
                    System.Text.Encoding.UTF8, "application/json");
                req.Headers.Add("x-api-key", config.ApiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
            }
            else // OpenAI / Custom（OpenAI 兼容）
            {
                var model = config.SupportedModels.FirstOrDefault() ?? "gpt-3.5-turbo";
                req = new HttpRequestMessage(HttpMethod.Post, testUrl);
                req.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        model,
                        messages = new[] { new { role = "user", content = "ping" } },
                        max_tokens = 1
                    }),
                    System.Text.Encoding.UTF8, "application/json");
                req.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            }

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(testTimeout));
            var resp = await client.SendAsync(req, cts.Token);
            stopwatch.Stop();
            var latency = (int)stopwatch.ElapsedMilliseconds;

            if (resp.IsSuccessStatusCode)
                return new AIProviderTestResult(true, latency, "连接成功", (int)resp.StatusCode);

            var status = (int)resp.StatusCode;
            var msg = status switch
            {
                401 or 403 => "鉴权失败：API Key 无效或权限不足",
                _ => $"连接失败（HTTP {status}）"
            };
            return new AIProviderTestResult(false, latency, msg, status);
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new AIProviderTestResult(false, (int)stopwatch.ElapsedMilliseconds, $"连接超时（>{testTimeout}s）", null);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new AIProviderTestResult(false, (int)stopwatch.ElapsedMilliseconds, $"无法连接：{ex.Message}", null);
        }
        catch (System.Exception ex)
        {
            stopwatch.Stop();
            return new AIProviderTestResult(false, (int)stopwatch.ElapsedMilliseconds, $"测试失败：{ex.Message}", null);
        }
    }

    /// <summary>
    /// 从 chat 响应体提取模型回复文本（与聊天链路 AIService 同一语义：优先 message.content，
    /// 空则回退思考字段——qwen3 用 reasoning_content，vLLM 0.31/deepseek 用 reasoning）；
    /// 兼容 OpenAI choices[0].message / choices[0].text（补全）/ Anthropic content[0].text。解析不出返回 null。
    /// </summary>
    internal static string? ExtractContent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == System.Text.Json.JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg))
                {
                    var content = ReadMessageText(msg, "content");
                    if (!string.IsNullOrEmpty(content)) return content;
                    var reasoning = ReadMessageText(msg, "reasoning_content");
                    if (!string.IsNullOrEmpty(reasoning)) return reasoning;
                    // vLLM 0.31（gpustack）/deepseek 系用 reasoning 而非 reasoning_content
                    var reasoning2 = ReadMessageText(msg, "reasoning");
                    if (!string.IsNullOrEmpty(reasoning2)) return reasoning2;
                }
                if (first.TryGetProperty("text", out var text) &&
                    text.ValueKind == System.Text.Json.JsonValueKind.String)
                    return text.GetString();
            }
            if (root.TryGetProperty("content", out var arr) &&
                arr.ValueKind == System.Text.Json.JsonValueKind.Array &&
                arr.GetArrayLength() > 0)
            {
                var c0 = arr[0];
                if (c0.TryGetProperty("text", out var ct) &&
                    ct.ValueKind == System.Text.Json.JsonValueKind.String)
                    return ct.GetString();
            }
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>读取 message 中指定字段为文本：兼容字符串型与多模态数组型（拼接 text 项）。</summary>
    private static string? ReadMessageText(System.Text.Json.JsonElement msg, string prop)
    {
        if (!msg.TryGetProperty(prop, out var el)) return null;
        return el.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => el.GetString(),
            System.Text.Json.JsonValueKind.Array => string.Concat(
                el.EnumerateArray().Select(x =>
                    x.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String
                        ? t.GetString() : null)),
            _ => null
        };
    }

    /// <summary>回复是否包含 ok（忽略大小写）——测试连接成功判据。</summary>
    internal static bool SaysOk(string? content)
    {
        return content != null &&
            content.IndexOf("ok", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    #region 校验

    private void ValidateForCreate(AIProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new System.ArgumentException("提供方名称不能为空。");
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new System.ArgumentException("接入地址（Endpoint）不能为空。");

        if (_repo.GetAll().Any(p => string.Equals(p.Name, config.Name, System.StringComparison.OrdinalIgnoreCase)))
            throw new System.InvalidOperationException("提供方名称已存在。");
    }

    private void ValidateForUpdate(long id, AIProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new System.ArgumentException("提供方名称不能为空。");
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new System.ArgumentException("接入地址（Endpoint）不能为空。");

        if (_repo.GetAll().Any(p => p.Id != id && string.Equals(p.Name, config.Name, System.StringComparison.OrdinalIgnoreCase)))
            throw new System.InvalidOperationException("提供方名称与既有记录冲突。");
    }

    #endregion

    #region 默认约束维护

    /// <summary>将除 keepId 外的所有提供方置为非默认（仅改标志，不碰 ApiKey 密文）</summary>
    private void UnsetOtherDefaults(long keepId)
    {
        foreach (var p in _repo.GetAll())
        {
            if (p.Id == keepId) continue;
            if (p.IsDefault)
            {
                _repo.SetDefault(p.Id, false);
            }
        }
    }

    /// <summary>当存在提供方但无默认时，将首个提升为默认（仅改标志）</summary>
    private void EnsureSingleDefault()
    {
        var all = _repo.GetAll();
        if (all.Count == 0) return;
        if (!all.Any(p => p.IsDefault))
        {
            _repo.SetDefault(all[0].Id, true);
        }
    }

    #endregion

    #region 映射

    /// <summary>运行期配置 → 实体（ApiKey 保持明文，由仓储加密）</summary>
    private static AIProvider ToEntity(AIProviderConfig config)
    {
        return new AIProvider
        {
            Name = config.Name?.Trim(),
            ProviderType = config.ProviderType.ToString(),
            Endpoint = config.Endpoint?.Trim(),
            ApiKey = config.ApiKey,
            SupportedModels = config.SupportedModels == null
                ? null
                : string.Join(",", config.SupportedModels
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())),
            IsDefault = config.IsDefault,
            TimeoutSeconds = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 120,
            VisionModel = config.VisionModel,
            EnableMultimodal = config.EnableMultimodal,
            VisionPromptTemplate = config.VisionPromptTemplate
        };
    }

    /// <summary>实体 → 运行期配置（显式解密 ApiKey 为明文，由本服务按需调用）</summary>
    private AIProviderConfig ToConfig(AIProvider entity)
    {
        return new AIProviderConfig
        {
            Name = entity.Name,
            ProviderType = System.Enum.TryParse<AIProviderType>(entity.ProviderType, true, out var t)
                ? t
                : AIProviderType.OpenAI,
            Endpoint = entity.Endpoint,
            ApiKey = _encryption.Decrypt(entity.ApiKey),
            SupportedModels = string.IsNullOrEmpty(entity.SupportedModels)
                ? new List<string>()
                : entity.SupportedModels
                    .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                    .ToList(),
            IsDefault = entity.IsDefault,
            TimeoutSeconds = entity.TimeoutSeconds > 0 ? entity.TimeoutSeconds : 120,
            VisionModel = string.IsNullOrEmpty(entity.VisionModel) ? null : entity.VisionModel,
            EnableMultimodal = entity.EnableMultimodal,
            VisionPromptTemplate = string.IsNullOrEmpty(entity.VisionPromptTemplate) ? null : entity.VisionPromptTemplate
        };
    }

    #endregion
}

/// <summary>测试连接结果</summary>
public record AIProviderTestResult(bool Success, int LatencyMs, string Message, int? StatusCode);
