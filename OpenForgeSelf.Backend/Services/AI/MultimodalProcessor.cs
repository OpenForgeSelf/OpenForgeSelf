using System.Text.RegularExpressions;
using System.Web;
using OpenForgeSelf.Backend.Services.AI.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Services.AI;

/// <summary>
/// 多模态处理器 - 自动检测图片并调用视觉模型识别
/// </summary>
public class MultimodalProcessor
{
    private readonly AIProviderRegistry _registry;
    private readonly IAIProvider? _visionProvider;
    private readonly AIProviderConfig? _config;
    private readonly IImageRecognitionCache? _cache;

    // 识别失败占位结果前缀（与 RecognizeImagesAsync 的异常返回保持一致），失败结果不写入缓存
    private const string FailurePrefix = "[图片识别失败";

    // 图片 URL 正则表达式
    private static readonly Regex ImageUrlPattern = new(
        @"https?://[^\s""'<>]+\.(?:jpg|jpeg|png|gif|webp|bmp|svg)(\?[^\s""'<>]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MultimodalProcessor(AIProviderRegistry registry, AIProviderConfig? config, IImageRecognitionCache? cache = null)
    {
        _registry = registry;
        _config = config;
        _cache = cache;

        // 获取视觉模型提供者
        if (!string.IsNullOrEmpty(config?.VisionModel))
        {
            _visionProvider = registry.GetProviderByModel(config.VisionModel);
            if (_visionProvider != null)
            {
                XTrace.Log.Info("多模态处理器初始化 - 视觉模型: {0}, 提供者: {1}",
                    config.VisionModel, _visionProvider.ProviderName);
            }
            else
            {
                XTrace.Log.Warn("多模态处理器初始化 - 视觉模型 {0} 未找到对应提供者",
                    config.VisionModel);
            }
        }
    }

    /// <summary>
    /// 检测请求是否包含图片
    /// </summary>
    public bool HasImages(UnifiedChatRequest request)
    {
        foreach (var msg in request.Messages)
        {
            // 检查 ContentBlocks 中的图片
            if (msg.ContentBlocks != null && msg.ContentBlocks.Count > 0)
            {
                if (msg.ContentBlocks.Any(b => b.Type == "image_url" || b.Type == "image_base64"))
                    return true;
            }

            // 检查 Content 中的图片 URL
            if (!string.IsNullOrEmpty(msg.Content) && ContainsImageUrl(msg.Content))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检测文本是否包含图片 URL
    /// </summary>
    public static bool ContainsImageUrl(string text)
    {
        return ImageUrlPattern.IsMatch(text);
    }

    /// <summary>
    /// 从文本中提取所有图片 URL
    /// </summary>
    public static List<string> ExtractImageUrls(string text)
    {
        var matches = ImageUrlPattern.Matches(text);
        return matches.Select(m => m.Value).ToList();
    }

    /// <summary>
    /// 处理多模态请求：检测图片 -> 调用视觉模型识别 -> 注入结果到消息。
    /// 传入 <paramref name="sessionId"/> 且构造时注入了缓存时，启用按会话分区的图片识别缓存：
    /// 每张图片先查本地缓存，未命中才调用视觉模型（逐图识别），成功结果写回缓存，
    /// 同一会话内相同图片后续请求直接复用缓存结果。
    /// </summary>
    public async Task<UnifiedChatRequest> ProcessAsync(UnifiedChatRequest request, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        if (!HasImages(request))
        {
            return request;
        }

        if (_visionProvider == null)
        {
            XTrace.Log.Warn("请求包含图片但未配置视觉模型，跳过多模态处理");
            return request;
        }

        XTrace.Log.Info("检测到图片，开始多模态处理...");

        // 提取所有图片信息
        var imageInfos = await ExtractImageInfosAsync(request, cancellationToken);

        // 调用视觉模型识别（启用缓存时逐图处理：命中缓存的图片跳过模型调用）
        string visionResult;
        if (_cache != null && !string.IsNullOrWhiteSpace(sessionId) && imageInfos.Count > 0)
        {
            var parts = new List<string>(imageInfos.Count);
            var cacheHits = 0;
            foreach (var img in imageInfos)
            {
                var imageKey = ImageRecognitionCacheKey.Compute(img, _config?.VisionModel);

                string? cached = null;
                try
                {
                    cached = await _cache.TryGetAsync(sessionId!, imageKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("读取图片识别缓存失败（按未命中处理）: {0}", ex.Message);
                }

                if (cached != null)
                {
                    parts.Add(cached);
                    cacheHits++;
                    continue;
                }

                var single = await RecognizeImagesAsync(new List<ImageInfo> { img }, cancellationToken);

                // 仅缓存成功且非空的识别结果，失败占位结果不缓存（下次重试）
                if (single.Length > 0 && !single.StartsWith(FailurePrefix, StringComparison.Ordinal))
                {
                    try
                    {
                        await _cache.SetAsync(sessionId!, imageKey, single, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        XTrace.Log.Warn("写入图片识别缓存失败（不影响本次响应）: {0}", ex.Message);
                    }
                }

                parts.Add(single);
            }

            if (cacheHits > 0)
                XTrace.Log.Info("图片识别缓存命中 {0}/{1}", cacheHits, imageInfos.Count);

            visionResult = string.Join("\n\n", parts);
        }
        else
        {
            visionResult = await RecognizeImagesAsync(imageInfos, cancellationToken);
        }

        // 构建新的请求，将图片描述注入到系统提示
        var processedRequest = BuildProcessedRequest(request, imageInfos, visionResult);

        XTrace.Log.Info("多模态处理完成，共识别 {0} 张图片", imageInfos.Count);

        return processedRequest;
    }

    /// <summary>
    /// 从请求中提取所有图片信息
    /// </summary>
    private async Task<List<ImageInfo>> ExtractImageInfosAsync(UnifiedChatRequest request, CancellationToken cancellationToken)
    {
        var images = new List<ImageInfo>();

        foreach (var msg in request.Messages)
        {
            // 从 ContentBlocks 提取
            if (msg.ContentBlocks != null)
            {
                foreach (var block in msg.ContentBlocks)
                {
                    if (block.Type == "image_url" && !string.IsNullOrEmpty(block.ImageUrl))
                    {
                        images.Add(new ImageInfo
                        {
                            Source = "url",
                            Url = block.ImageUrl,
                            Detail = block.ImageDetail ?? "auto"
                        });
                    }
                    else if (block.Type == "image_base64" && !string.IsNullOrEmpty(block.ImageBase64))
                    {
                        images.Add(new ImageInfo
                        {
                            Source = "base64",
                            Base64Data = block.ImageBase64,
                            MediaType = block.ImageMediaType ?? "image/png"
                        });
                    }
                }
            }

            // 从 Content 文本中提取 URL
            if (!string.IsNullOrEmpty(msg.Content))
            {
                var urls = ExtractImageUrls(msg.Content);
                foreach (var url in urls)
                {
                    // 避免重复添加
                    if (!images.Any(i => i.Url == url))
                    {
                        images.Add(new ImageInfo
                        {
                            Source = "url",
                            Url = url,
                            Detail = "auto"
                        });
                    }
                }
            }
        }

        // 如果没有找到图片，尝试从用户消息中查找
        if (images.Count == 0 && request.Messages.Count > 0)
        {
            var lastUserMsg = request.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMsg != null && ContainsImageUrl(lastUserMsg.Content))
            {
                var urls = ExtractImageUrls(lastUserMsg.Content);
                foreach (var url in urls)
                {
                    images.Add(new ImageInfo
                    {
                        Source = "url",
                        Url = url,
                        Detail = "auto"
                    });
                }
            }
        }

        return images;
    }

    /// <summary>
    /// 调用视觉模型识别图片
    /// </summary>
    private async Task<string> RecognizeImagesAsync(List<ImageInfo> images, CancellationToken cancellationToken)
    {
        if (images.Count == 0)
            return string.Empty;

        try
        {
            // 构建视觉请求
            var visionRequest = BuildVisionRequest(images);

            // 调用视觉模型
            var visionResponse = await _visionProvider!.ChatAsync(visionRequest, cancellationToken);

            // 提取响应内容
            var result = visionResponse.Choices.FirstOrDefault()?.Content ?? string.Empty;

            XTrace.Log.Info("图片识别完成，结果长度: {0}", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("图片识别失败: {0}", ex.Message);
            return $"[图片识别失败: {ex.Message}]";
        }
    }

    /// <summary>
    /// 构建视觉模型请求
    /// </summary>
    private UnifiedChatRequest BuildVisionRequest(List<ImageInfo> images)
    {
        var prompt = _config?.VisionPromptTemplate ?? "请描述这张图片的内容。";
        prompt = prompt.Replace("{image_count}", images.Count.ToString());

        // 视觉模型配置可能带 "提供商:上游模型id" 前缀（用于显式路由到指定提供方），
        // 但转发给上游时只需裸上游模型 id，须剥离前缀，否则上游不识别。
        var visionModelId = _config!.VisionModel!;
        var prefixIdx = visionModelId.IndexOf(':');
        if (prefixIdx > 0)
            visionModelId = visionModelId[(prefixIdx + 1)..];

        var visionRequest = new UnifiedChatRequest
        {
            Model = visionModelId,
            SystemPrompt = "你是一个专业的图片识别助手。请详细描述用户提供的图片内容。"
        };

        // 根据提供者类型构建消息格式
        if (_visionProvider!.ProviderType == AIProviderType.Anthropic)
        {
            // Anthropic 格式
            var contentBlocks = new List<ContentBlock>
            {
                new() { Type = "text", Text = prompt }
            };

            foreach (var img in images)
            {
                if (img.Source == "url")
                {
                    contentBlocks.Add(new ContentBlock
                    {
                        Type = "image",
                        ImageUrl = img.Url,
                        ImageDetail = img.Detail
                    });
                }
                else if (img.Source == "base64")
                {
                    contentBlocks.Add(new ContentBlock
                    {
                        Type = "image",
                        ImageBase64 = img.Base64Data,
                        ImageMediaType = img.MediaType
                    });
                }
            }

            // Anthropic 需要特殊格式
            visionRequest.OriginalContentBlocks = contentBlocks;
        }
        else
        {
            // OpenAI 格式
            var contentBlocks = new List<ContentBlock>
            {
                new() { Type = "text", Text = prompt }
            };

            foreach (var img in images)
            {
                if (img.Source == "url")
                {
                    contentBlocks.Add(new ContentBlock
                    {
                        Type = "image_url",
                        ImageUrl = img.Url,
                        ImageDetail = img.Detail
                    });
                }
                else if (img.Source == "base64")
                {
                    contentBlocks.Add(new ContentBlock
                    {
                        Type = "image_base64",
                        ImageBase64 = img.Base64Data,
                        ImageMediaType = img.MediaType
                    });
                }
            }

            visionRequest.ContentBlocks = contentBlocks;
        }

        visionRequest.MaxTokens = 2048;

        return visionRequest;
    }

    /// <summary>
    /// 构建处理后的请求，将图片识别结果注入
    /// </summary>
    private UnifiedChatRequest BuildProcessedRequest(UnifiedChatRequest original, List<ImageInfo> images, string visionResult)
    {
        var processed = new UnifiedChatRequest
        {
            Model = original.Model,
            Messages = new List<UnifiedChatMessage>(),
            Temperature = original.Temperature,
            TopP = original.TopP,
            MaxTokens = original.MaxTokens,
            Stream = original.Stream,
            Tools = original.Tools
        };

        // 构建图片上下文描述
        var imageContext = $"""
            [多模态图片识别结果]
            用户发送了 {images.Count} 张图片，以下是图片内容识别结果：

            {visionResult}

            以上是图片的详细描述，请结合这些信息回答用户的问题。
            """;

        // 构建系统提示
        if (!string.IsNullOrEmpty(original.SystemPrompt))
        {
            processed.SystemPrompt = original.SystemPrompt + "\n\n" + imageContext;
        }
        else
        {
            processed.SystemPrompt = imageContext;
        }

        // 复制消息，但移除图片 URL（因为已经识别了）
        foreach (var msg in original.Messages)
        {
            var processedMsg = new UnifiedChatMessage
            {
                Role = msg.Role,
                Content = RemoveImageUrls(msg.Content),
                Name = msg.Name,
                ToolCalls = msg.ToolCalls,
                ToolCallId = msg.ToolCallId
            };
            processed.Messages.Add(processedMsg);
        }

        return processed;
    }

    /// <summary>
    /// 从文本中移除图片 URL，保留其他内容
    /// </summary>
    private static string RemoveImageUrls(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 移除图片 URL，但保留周围文字
        return ImageUrlPattern.Replace(text, "[图片]").Trim();
    }
}

/// <summary>
/// 图片信息
/// </summary>
public class ImageInfo
{
    public string Source { get; set; } = "url"; // url, base64
    public string? Url { get; set; }
    public string? Base64Data { get; set; }
    public string? MediaType { get; set; }
    public string Detail { get; set; } = "auto";
}
