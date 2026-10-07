# Spec

## Functional Requirements
- FR-1 在 `docs/04-standards/agent-workflow/` 下按主题建 **5 个内容文件 + 1 个索引**，承接原文件全部 22 个小节，**不改写任何一条规则文字**。
- FR-2 原路径 `docs/04-standards/agent-workflow.md` 保留为**薄指路文件**（≤20 行）：声明"内容已按 §编号拆分至 X 文件"，并给出 §编号 → 文件 的地址表入口。
- FR-3 `AGENTS.md` 内 13 处带 §指向的路由句改为指向新文件（§A* → 对应 A 文件；§B* → 对应 B 文件；纯 "Part A/B" 类表述改指目录）。
- FR-4 `docs/README.md` 速查表行、`packaging-upgrade-backup.md`（:6/:16/:236）、`ai-native-engineering-workflow.md`（:8/:156）的互引同步改指新路径。
- FR-5 索引文件承接原头部 1–13 行的**维护规则**（"新增规则→写对应小节"改为"→写对应主题文件"），作为该规则的唯一一处承载。

## Input
- 现文件 `docs/04-standards/agent-workflow.md`（925 行 / 136,339 B，UTF-8 **带 BOM**）
- 实测小节边界（00 文件§内部结构）

## Output
| 新文件 | 承接原行区间 | 行数 | 内容 |
| --- | --- | --- | --- |
| `agent-workflow/README.md` | 新写 | ~40 | 索引 + §编号地址表 + 落位规则 + 历史行号引用说明 |
| `agent-workflow/a-workflow-core.md` | 14–216 | 203 | Part A + A1–A5（技能/规划/编码/验证/迭代） |
| `agent-workflow/a-workflow-docs-report.md` | 217–454 | 238 | A6–A10（文档工作流/设计稿/弃用 speckit/汇报/群 SOP） |
| `agent-workflow/b-engineering-daily.md` | 455–763 | 309 | Part B + B1–B6（约定/测试/前端/后端/插件/PowerShell） |
| `agent-workflow/b-engineering-platform.md` | 764–905 | 142 | B7–B12（环境/架构/覆盖/CI/SDK/dsh） |
| `agent-workflow/c-changelog.md` | 906–925 | 20 | Part C 变更记录 |
| `agent-workflow.md`（旧路径） | 重写为薄指路 | ≤20 | 只做地址解析，不承载规则文字 |

## Business Rules
- **BR-1 编号即地址**：§A1–§A10、§B1–§B12 的字面编号与标题文本原样保留（AGENTS.md、技能、历史工件都按编号引用）。
- **BR-2 零内容改写**：只做区间搬运。判据用机械对账（见 AC1/AC2），不靠肉眼。
- **BR-3 不留第二份真相**：规则正文只存在于主题文件；旧路径与 README 只写指路，不复述任何规则条文。
- **BR-4 历史不回改**：`docs/ai/pilot/**` 里 60+ 处引用（含行号形式）一律不改，由 README 统一说明"历史行号按 §编号解读，原行号自 2026-10-07 起失效"。

## Boundary Conditions
- 头部 1–13 行不属于任何 §小节 ⇒ 其"维护规则"迁入 README，其余（状态/最后更新）随 README 重写。
- BOM：新文件一律 **UTF-8 无 BOM**（BOM 是 `.ps1` 的要求，文档不需要；原文件带 BOM 属历史）。
- 跨文件引用的相对链接层级变化：`docs/04-standards/agent-workflow/*.md` 内引同目录文件用**裸文件名**，引仓库其它文档需**多上一层** `../../`。
- A 组内存在跨 Part 互引（如 A4 提到 B2）⇒ 保留文字，不逐条改成链接（改动面失控，且 §编号本身已可寻址）。

## Error Handling
- 对账不通过（拼接结果与原文件有差异）⇒ **立即回滚整个拆分**（新目录删除、旧文件恢复 `git checkout`），不就地修补。
- 指针改漏 ⇒ 由 AC4 的全仓 grep 兜出，逐条补，不允许"差不多"。

## Compatibility
- 旧路径不删 ⇒ 任何未及更新的引用（含 `global-setup.ts:141` 代码注释、外部笔记）仍可解析；指路文件本身即兼容层。
- 不产生 git 历史断裂：拆分文件用 `git rm --cached`? 否——按新增 + 覆写旧路径处理（内容连续性由 AC1 哈希证明，而非 rename 检测）。

## Non-functional Requirements
- 单文件 ≤ ~310 行（现状最大 925）。
- 会话按需读入量：查 §B6 只需 309 行文件（原来 925 行）；查 A1 只需 203 行。
- 并行会话同文件写冲突：22 个小节从 1 个热点分散到 5 个热点。

## Acceptance Criteria
见 04-task AC1–AC7。

## Unknown
- U-1 是否有**仓外**引用（用户自己的笔记/别的仓库）指向旧路径的行号 ⇒ 未知，由旧路径薄指路兜住。
- U-2 是否存在读取该文件的自动化守卫（除 `RepositoryScriptTests` 扫 `scripts/**` 外）⇒ 已 grep `ForgeSelf.Api.Tests` 未见指向该文件的断言，但**未跑全量测试确认**（宿主当前被并行会话 McpCenter 9 个编译错误挡住）。
