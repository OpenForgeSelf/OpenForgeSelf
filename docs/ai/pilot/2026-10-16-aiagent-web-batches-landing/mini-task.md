# mini-task：AIAgent Web 侧五子批合并落地提交（2026-10-16）

- 日期：2026-10-16（提交落地日）
- 类型：交付/提交编排批（**不做任何新的代码改动**，只把已通过验收的工作树按可验证粒度入库）
- 来源：用户输入「把已可验收的批次分别验收并提交；发布暂缓」+「AIAgent 多批改动有交叠，尤其 `AiAgentView.vue`，需先按 pilot 工件厘清批次与批准状态，避免混批提交」。

## Intent / Spec

AIAgent 插件 Web 侧自 2026-10-14 起连续落地五个子批，均已实现并验收（证据在各子批 05-evidence / TODO / 日记），但**从未提交**。工作树中五个子批物理交叠、不可按 hunk 拆分：

| 子批 | pilot 工件 | 批准状态 | 与共享文件的交叠 |
| --- | --- | --- | --- |
| S1 组件迁移（ai-assistant-widget → agent-core） | `2026-10-14-aiagent-widget-migration`（00-07 齐全，本批补齐 AC 对照） | 闸门1 已批（日记 10-14） | `AiAgentView.vue`/`ChatWidget.vue`/`config.js`/守卫白名单 |
| S2 展示优化三项 | `2026-10-15-chat-display-polish`（轻量齐全） | 用户原话授权（日记 10-15 输入2） | `MessageItem.vue`/`widget.css`/`agentBridge.ts` |
| S3 composer 升级批次 1 | `2026-10-15-aiagent-composer-upgrade`（00-07 齐全，本批补齐关键节） | 闸门1 已批（日记 10-15 输入4） | `ChatWidget.vue`/`AiAgentView.vue`/`config.js` |
| S4 041 上下文图标行 + 权限胶囊 + 授权询问 | 独立 pilot 缺失；功能档案 `docs/02-features/041-ai-agent-composer-context-bar.md` + TODO 输入7 账目（✅ 2026-10-15 完成，逐项截图取证）——**流程缺口本批补记，缺口登记于 06-review** | 用户指令直授（TODO 输入7） | `ContextBar.vue`/`AskPanel.vue`/`TodoEntry.vue`/`AiAgentView.vue` |
| S5 聊天组件状态矩阵 + 设计 token（方案 A+B） | 独立 pilot 缺失；TODO 输入19 账目（✅ 2026-10-16 落地，「先测后改 → 18 红全部转绿」）——同上补记 | 用户拍板方案 A+B（TODO 输入19） | `*.states.test.ts`/`widget-contract.test.ts`/`widget.css` |

**不可拆分的实证理由**：S3/S4/S5 均在 S1 新建的 `ChatWidget.vue`/`config.js`/`widget.css` 与 `AiAgentView.vue` 上叠加改动；任何「按子批切分暂存」都需要伪造不存在的中间版本文件，且中间 commit 无法独立通过构建/测试（违反可验证性优先原则）。故本批以**五子批合并为一次可验证的集成落地提交**，提交信息逐子批列名、全部子批 pilot 工件随批入库。

## Scope

- `Plugins/AIAgent/plugin.json`（1.7.4→1.8.0）、`Plugins/AIAgent/web/package.json`（1.2.0→1.3.0）、`Plugins/AIAgent/web/src/**`（agent-core 全量 + agentBridge + composer 系列 + AiAgentView，含全部新增未跟踪文件）。
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`（断言面更新 + KNOWN_BASELINE_5XX）、`session-management.spec.ts`（injectRealApiKey）。
- `scripts/check-design-compliance.mjs`（`--aiw-` 白名单，10-14 用户批准方案 A）+ `scripts/design-compliance-baseline.json`（基线刷新）。
- 工件：S1/S2/S3 三个子批 pilot 目录 + `docs/02-features/041-*.md` + 本 pilot 目录。
- **禁止**：任何后端文件（`ForgeSelf.Api/**` 被未跟踪 `ModelProxy.cs` 编译错误阻断，用户明令暂停后端批次）；网络设置批任何文件（另一会话在跑）；aimodel 批任何文件；发布动作（用户暂缓）。

## Acceptance Criteria

- [x] AC1 集成树前端门禁全绿（本会话 2026-10-16 刷新）：宿主 `pnpm run check` 0 error；宿主全量 `vitest` 全绿；`design:check` PASS；插件 `pnpm run build` 成功。
- [x] AC2 四个入库 pilot 目录全部通过 `verify-pilot-artifacts.ps1 -EnforceHeader -EnforceAcCoverage`（pre-commit 门禁同判）。
- [x] AC3 提交不含任何后端/网络/aimodel 文件（精确暂存清单入 07）。
- [x] AC4 局限如实登记：冒烟 e2e 与 dev-stack 现场本会话无法刷新（后端编译阻断），引用 10-14/10-15 历史 Verified 证据；S4/S5 独立 pilot 为事后补记。
- [x] AC5 TODO.md 相应进行中条目状态更新（提交落地，发布仍暂缓）。

## Plan / Task

1. 补齐 S1/S2/S3 pilot 关键节（AC 对照 / 头部块 / Final Decision / Validation 节，全部映射既有证据，零新增事实）→ 门禁复跑绿。
2. 创建本 pilot（落地批记录）→ 门禁绿。
3. 刷新前端四门禁（见 05）。
4. 精确暂存清单文件 → 单条 `feat(aiagent)` 提交（trailer 指本目录，message 列五子批与各自 pilot 引用）。
5. TODO/日记回写。
