# 文档反向同步 SOP（代码 → 文档）

> 状态：稳定版（2026-08-13，输入36 走完 A-G 全增量闭环）
> 适用：docs/ 体系落后于代码实现时，以代码事实为准反向更新文档。
> 最后更新：2026-08-13

## 0. 为什么单独有这份 SOP

输入34 已批量产出 34 文档初稿，但流程未走 loop 工程——未拆可迭代增量、校验点未沉淀。
本 SOP 把"怎么反向更文档"固定成可重复的闭环，后续每个增量都按它走，并在执行中反写校验点（迭代进化）。

## 1. 核心原则

1. **代码是唯一事实源**：文档里出现的一切端口/类名/路由/路径/状态，必须 grep 代码确认，禁止凭记忆或猜测。
2. **不臆造**：代码没有的能力不写进文档；代码已实现但文档缺失的，补；代码已变而文档过时的，改。
3. **宏观→微观、整体→局部**：按 `docs/` 目录编号顺序推进（00-vision → 01-architecture → 02-features → 03-16 → README），先立底座再填细节，避免细节与总览打架。
4. **增量迭代、不急于求成**：一次只做 1-2 个增量（如"宏观底座复核"），复核通过再进下一档；初稿不一次性标完成。
5. **specs/ 不纳入**：specs 是开发期产物（speckit），与 docs 职责分离；本 SOP 只动 docs/。

## 2. 闭环步骤（每轮增量都走）

```
[1] 侦察      → 定位目标目录/文件，grep 关键代码事实（类名/路由/端口/状态）
[2] 校核      → 把初稿文档与代码事实逐条比对，列出漂移点（过时/缺失/错误）
[3] 按序写/改 → 仅改本增量范围内的文档，不顺手改无关目录
[4] 登记      → 新建文档须在 docs/README.md 速查表+已归档内容表登记
[5] 校验      → 全量 grep 易错事实（端口号、过时值 7300/7380、类名拼写）
[6] 回流      → 把本增量结果+新发现校验点写回本 SOP + 日记「下一步」
```

## 3. 增量划分（与 TODO 对应）

| 增量 | 范围 | 状态 |
|------|------|------|
| A | SOP 建立（本文档） | ✅ 初版 |
| B | 宏观底座：00-vision + 01-architecture 复核 | ✅ 已完成 |
| C | 02-features（001-011 + 100 + redesign）逐篇核对 | ✅ 已完成 |
| D | 03-09（design/standards/guides/security/operations） | ✅ 已完成 |
| E | 10-16（testing/troubleshooting/faq/glossary/onboarding/roadmap/reference/changelog） | ✅ 已完成 |
| F | README 最终校验 + 全量一致性扫雷 | ✅ 已完成 |
| G | SOP 沉淀进 MEMORY 长期规律（AGENTS.md §7.5 改动待用户拍板） | ✅ 已完成 |

## 4. 已知漂移（沉淀，复核时重点查）

- **端口**：后端默认 `7102`（`Models/ForgeSetting.cs` `PortNumber=7102`）、前端 dev `7002`；AGENTS.md 旧注 `7300/7380` 已过时——文档若提端口须用 7102/7002，并可在脚注标注旧值过时。
- **AIProvider 鉴权**：`[Authorize("ApiKeyPolicy")]` 已落地（原 TODO"待运行时验证"已变代码事实）。
- **ChatRecord→ChatTurn + ChatSession 聚合**：已实现（代码已提交至 `9d84e48`，旧 607 行不迁移）。
- **多模态图片识别缓存**：`IImageRecognitionCache`+`LocalFileImageRecognitionCache` 已实现（同 commit）。
- **构建绕过 safe-delete**：`vite.config.ts` 的 `emptyOutDir:false` 已写入代码。

## 5. 校验清单（每增量出口必查）

- [ ] 文档引用的端口号与 `ForgeSetting.PortNumber` 一致（无臆造 7300/7380）
- [ ] 文档引用的实体/服务/控制器类名在代码中真实存在（`ls`/`grep` 确认）
- [ ] 文档状态（已实现/未提交/待验证）与 `git status` 实际一致
- [ ] 新建文档已在 README 登记
- [ ] 不顺手改本增量范围外的文档/代码
- [ ] **路由/端点须 grep 实际 `[Route]`/`[HttpGet/Post]` 确认**：初稿易把推测端点写进文档（如 ChatController 误写 `api/chat/session/{id}`，实际仅 `api/chat`、`api/chat/stream`、`api/chat/history/{sessionId}`）
- [ ] **UnifiedAI 网关成员须列全**：含 `AgentChatController`（`v1/agent/chat/completions`，实验性 Agent Framework 样本），不止 OpenAI/Anthropic/Responses/Models 四者
- [ ] **跨文档链接须指真实文件名**：初稿占位（如 `011-multimodal-image-cache` 写成 `xxx-multimodal-cache`）须改真实路径
- [ ] **`[Route("api/[controller]")]` token 路由**：实际路径 = `api/` + 小写 controller 名（如 `PortConfigurationController` → `api/portconfiguration`），文档勿臆造连字符（如 `api/port-config`）；须 `grep "[Route("` 确认是字面还是 token
- [ ] **ChatController 真实端点**：`api/chat`(POST 收发) / `api/chat/stream` / `api/chat/history/{sessionId}`(GET 历史) / `api/chat/session/{sessionId}`(DELETE 删除)；会话**列表/详情**由 `ChatRecordsController`(`api/chat-sessions`) 提供，文档勿把 ChatController 的 session 端点误写成"获取会话详情"
- [ ] **实体字段表易漏**：`grep "BindColumn"` 取全量字段，避免只列部分（如 `AIProvider` 漏 `VisionPromptTemplate`、`AIModel` 漏 `Enabled/Owner/LastSyncTime`）
- [ ] **插件化功能路由前缀须 grep 确认**：插件控制器在 `Plugins/<Name>/Controllers/*.cs`，路由前缀为 `api/<plugin>`（如 WorkflowEngine→`api/workflows`、MemorySystem→`api/memory`、ScriptRunner→`api/scripts` 且 CodeSnippet 同插件→`api/codesnippets`）；核心控制器（Mcp/Skills）在 `Controllers/` 直接挂 `api/mcp`/`api/skills`；写文档前须 `grep "[Route("` 取真实前缀，勿凭"功能名"臆造路径。**注意**：`Glob Plugins/*` 不递归，会误判"无子插件"，须 `Grep` 或 `Glob Plugins/**` 确认真实子目录
- [ ] **缺口功能须如实标注**：代码未实现的（如 Profile 后端无独立模块）不编造路径，文档标"后端缺口/待补"，并记录到 TODO

> 本清单随增量 B-G + 缺口补齐（输入38）执行反写补充。
