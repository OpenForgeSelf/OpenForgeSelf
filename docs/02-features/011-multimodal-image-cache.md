---
feature_key: F011
feature_no: 011
status: implemented
last_updated: 2026-10-06
aliases: ["011-multimodal-image-cache"]
---

# 011 多模态图片识别本地缓存 — 功能需求与设计

> 功能编号：011（无独立 spec，对应能力特性）
> 状态：已实现（输入 31 门禁：16/16，全量 dotnet test 906/906）
> 关联：004 统一网关（多模态路由）；AIProvider.VisionModel（001）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
多模态请求携带图片，每次都调用视觉模型识别既慢又烧 token。同一会话内相同图片（同视觉模型）应只识别一次，结果本地缓存复用。

### 1.2 目标
1. 图片识别结果按会话分区本地缓存；
2. 缓存键 = SHA256(视觉模型 | 来源 | 内容)，同源同模型命中；
3. 失败结果**不缓存**（避免把错误永久化）；
4. 缓存读写异常绝不影响识别主流程（按未命中处理）。

## 2. 设计

### 2.1 接口与实现（`Services/AI/`）
| 类型 | 职责 |
|------|------|
| `IImageRecognitionCache` | `TryGetAsync(sessionId, imageKey)` / `SetAsync(...)` |
| `LocalFileImageRecognitionCache` | 文件实现：`<root>/<sessionId>/<imageKey>.json` |
| `ImageRecognitionCacheKey.Compute(ImageInfo, visionModel)`（定义于 `IImageRecognitionCache.cs`） | SHA256 十六进制小写键 |

### 2.2 关键机制
- **键构成**：`{visionModel}|base64|{mediaType}|{data}` 或 `{visionModel}|url|{detail}|{url}`（URL 图片以 URL 文本为内容标识）；
- **分区键**：`sessionId` 即会话键（`OpenAIChatController` 把 `conversationKey` 解析提前作分区键）；
- **原子写**：临时文件 + `File.Move(overwrite)` 避免并发读到半截；
- **路径清洗**：`SanitizeFileName` 替换非法字符、去首尾点、截断 64，防路径穿越；
- **容错**：读取损坏/异常 → 返回 null（按未命中），主流程继续。

### 2.3 接入点
- `MultimodalProcessor`：构造函数注入 `IImageRecognitionCache?`；逐图识别前 `TryGet`，命中复用，未命中识别后 `Set`（失败不 Set）；`ProcessAsync` 加 `sessionId`；
- `OpenAIChatController`：注入缓存 + 解析 `conversationKey` 作分区；
- `AppBuilder`：注册单例，根目录 `Data/ImageRecognitionCache`。

## 3. 使用指南
无需用户操作：多模态请求（带图）首次识别后，同会话同图再次请求直接命中缓存，token 消耗骤降。

## 4. 注意事项
- 缓存目录：`ForgeSelf.Api/.../Data/ImageRecognitionCache/<会话键>/<sha256>.json`（代码路径，已随 9d84e48 提交）；
- 运行实例无权限停止时，新增缓存需**重启后端**生效。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `ImageRecognitionCacheTests` | 命中/未命中/原子写/路径清洗 |
| `MultimodalProcessorCacheTests` | 逐图识别+缓存、失败不缓存、sessionId 分区 |
