# 06 Review — AIAgent Web 五子批合并落地（2026-10-16）

> 审查对象：本落地批（提交编排批，零新代码改动）的暂存范围、证据链与流程合规性。

## 1. 范围审查

- 入库清单（05 §5）与 mini-task Scope 逐条对表一致：AIAgent web + 插件版本号 + 两个 e2e spec + 设计守卫脚本/基线 + 四个 pilot 目录 + feature 041 档案。
- 排除清单核验：`ForgeSelf.Api/**`（含未跟踪 `ModelProxy.cs`、AIModel 批、PersistentSessionStore、网络批）、网络/aimodel 前端、`components.d.ts`（+1 行 NetworkPanel 声明属网络批）、`docs/02-features/042`、`docs/README.md`、`docs/04-standards/**`、`.agents/**`（单独 docs 批）均保留工作树未提交。✅ 无混批。

## 2. 证据审查

- AC1/AC2/AC3/AC4 均有本会话实跑 EXIT=0 记录（05 §1/§2/§5），来源等级 Verified，非引用旧结果。
- AC4 局限如实登记：e2e/dev-stack 现场属历史 Verified，本批未复跑、不做宣称（后端编译被 ModelProxy.cs 阻断 + 用户下令暂停后端批次与发布）。

## 3. 流程缺口登记（本批承认，不掩盖）

| 缺口 | 说明 | 处置 |
| --- | --- | --- |
| G1 S4（041 上下文栏）缺独立 pilot | 用户指令直授（TODO 输入7），实现时未建 `docs/ai/pilot/` 目录，证据散在 TODO/日记/截图 | 本批以 `docs/02-features/041-*.md` + TODO 账目 + 本 mini-task 表格补记；后续同类「指令直授小批」应按规范先建工件 |
| G2 S5（状态矩阵）同上 | 用户拍板方案 A+B（TODO 输入19），「先测后改 → 18 红转绿」证据在 TODO | 同上补记 |
| G3 S1/S3 子批关键节为 2026-10-16 事后补记 | 补记仅映射既有证据、零新增事实（各文件内已标注） | 可接受；登记备查 |
| G4 五子批合并单提交 | 交叠文件（`AiAgentView.vue`/`ChatWidget.vue`/`widget.css`）物理不可 hunk 拆分，拆分需伪造中间版本且不可独立验证 | mini-task 已论证；提交信息逐子批列名并引用各 pilot |

## 4. 风险审查

- 本批不改任何源代码，风险 = 「集成工作树状态入库」；该状态经宿主 check/test、design:check、插件 build 四门禁验证为绿。
- 回滚点：单条 revert 即回到 `f93a6f9` 后的工作树基线（不涉及数据库/接口契约变更）。
- 遗留：后端 AIModel/PersistentSessionStore/网络三批仍在工作树未入库，依赖 ModelProxy.cs 修复后另行验收——已列入汇报阻塞项，不属本批缺口。

## 5. 提交门禁自检

- commit-msg trailer：`Pilot: 2026-10-16-aiagent-web-batches-landing`（本目录，verify EXIT=0）。
- staged 含 `Plugins/AIAgent/web/src/` → pre-commit 触发 design-compliance 守卫：本会话实测 PASS（a=209=基线，b/c/d=0）。

## 6. Final Decision

**APPROVED** —— 范围精确、证据 Verified、缺口如实登记、门禁可过，准予按 05 §5 清单提交落地。
