# 00 Repository Understanding — 2026-11-09-json-facade-unify

## 项目基线（实测确认）
- 技术栈：`ForgeSelf.Api`（.NET 10 + ASP.NET Core + NewLife.XCode，net10.0-windows）+ `ForgeSelf.Core` / `ForgeSelf.Abstractions`（共享层）+ 根 `Plugins/*`（26 插件工程，仅 ProjectReference Core+Abstractions）+ `ForgeSelf.Web`（Vue3，本批不动）。
- 批1（PC-027）产物 `ForgeSelf.Abstractions/ForgeJson.cs`（Default/Indented 门面）**未提交**（git `??` untracked）；批1 工件目录 `docs/ai/pilot/2026-11-07-json-chinese-raw-encoder/` 亦全部未入库（git ls-files = 0）。
- 工作区 194 处未提交 = 批1 + 51888 演示缺陷批（输入7/8，并行会话在改 AppBuilder/Controllers/UnifiedAI/Services）+ db-admin 批叠加。本批机械单行改写叠加其上，回滚须按文件清单逐文件。

## JSON 使用面盘点（2026-11-09 15:5x–16:3x git grep 实测，pathspec `*.cs ':(exclude)*Tests*'`；Tests/web/docs 全排除）
| 模式 | 处数/文件数 | 备注 |
|---|---|---|
| `JsonSerializer.` | 614 / 128 | Serialize 519 / Deserialize 95 / `SerializeAsync`·`DeserializeAsync`·`SerializeToUtf8Bytes` = 0 |
| 扩展形反序列化 | 真扩展 `element.Deserialize<T>(opts)` 仅 1 处（AIAgent PlanGeneratorService.cs:174）；ref-reader 形 3 处（AnthropicMessagesController.cs:564、OpenAIChatController.cs:701/707） |
| `new JsonSerializerOptions` | 18 / 14（另有 target-typed `= new()`：PermissionProfileService.cs:20 `RuleJsonOptions` 等，守卫 v1 已兜底扫法） | 配置分布见 03 普查表 |
| `new JsonWriterOptions` / `new Utf8JsonWriter` | 8 / 2 | 全在 Plugins/DevTools（JsonFormatterService.cs ×5 行、YamlFormatterService.cs ×1 行），均已带 Relaxed Encoder（批1） |
| `.ToJsonString(` | 20 / 10（无参 `()` = 0，批1 已清） |
| `JsonDocument.Parse` | 198 / 78 | 带 options 仅 ChatSessionResolver.cs:47/73（且为 `Parse(JsonSerializer.Serialize(…))` 回环）；`new JsonDocumentOptions` 1 处 |
| `JsonNode.Parse` | 21 / 9 | 边界外（理由见 02 Boundary） |
| Newtonsoft / STJ source-gen | 0 | |
| `ForgeJson.`（批1 门面既有引用） | 449 / 92 | 仅 Default/Indented 两实例 |
| MVC 入口 | AppBuilder.cs:161 `AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)` | 未引用门面共享实例 |

## 文件分布（本批改写面并集 = 155 个生产 .cs）
- 根 `Plugins/`（26 插件工程）119 文件；`ForgeSelf.Api` 36 文件（含 `ForgeSelf.Api/Plugins` 基建 8 文件：PluginManager / PluginArtifactValidator / PluginHostTools / PluginJobStore / PluginPackagerService / PluginScaffolderService / PluginSourceResolver / PluginSrcScaffold）。
- 扫描根与排除目录沿批1 守卫 `ProductionCsFiles`（ForgeSelf.Api/Core/Abstractions/Bootstrapper/Plugins，排除 Tests/TestResults/obj/bin/node_modules/web/dist/e2e/docs/openwiki/.git/.temp/.agents/forgeself-design/ForgeSelf.Web/build/publish/screenshots）。

## 适用问题类（tags 检索结论）
- PC-027（序列化默认转义非 ASCII）：本批是其**机制延伸**（入口收敛 + 守卫 v2），非缺陷修复、不新增行为变化（语义零漂移论证见 02 Compatibility）。
- PC-023（双 JSON/提取面）：不触碰。其余 PC 未命中。本批不新立 PC。
