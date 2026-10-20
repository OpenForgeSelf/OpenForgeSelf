# Repository Understanding

> 阶段：Stage 0｜Task ID：2026-10-17-problem-class-closure｜日期：2026-10-17
> 本任务属「流程基建」类开发任务（动 AGENTS.md、规范、模板、hook、测试），非业务功能；理解范围以流程资产为主。

## 已确认事实（全部来自本会话实读，非推测）

| 依据 | 文件 | 关键事实 |
|------|------|----------|
| 开发流程唯一规范 | `docs/04-standards/ai-native-engineering-workflow.md`（v1.1.0，158 行全读） | 九阶段 + 三闸门；§1 第 5 条「不进行无关重构」；Stage 8 审查八问（L117-118）；§4 轻量档 = mini-task.md + 05/06/07 |
| 工件模板 | `docs/18-templates/ai-pilot/`（00~07 八份 + README） | `07-final-report.tpl.md` §10「最重要的问题」§11「下一步建议」为自由文本（教训悬空的载体）；`04-task.tpl.md` front-matter 含 risk/expected_files/feature/rollback |
| 工件门禁脚本 | `scripts/verify-pilot-artifacts.ps1`（206 行全读） | 校验 00-07 齐全 + 关键节 + 04 头部 + AC 覆盖；头部声明「本脚本属 L4 宪法层，Agent 不得自行修改」（L20） |
| pre-commit hook | `scripts/hooks/pre-commit`（72 行全读） | sh 脚本：pwsh 优先、回退 5.1 告警；触碰 `docs/ai/pilot/<task>/` 即调 verify-pilot-artifacts；另挂设计规范守卫；**注册新校验的既有模式 = 追加独立脚本调用** |
| 仓库级守卫范例 | `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（111 行全读） | 源码文本扫描式类级测试（BOM / git log 码页 / package-release 契约），FindRepoRoot 从 bin 上溯——新守卫测试照此模式写 |
| 排障知识库现状 | `docs/11-troubleshooting/index.md`（18 行，停在 2026-08-12） | 仅 6 行现象表，无 case/PC 归档结构 |
| 文档索引治理 | `docs/README.md` §六 | 「默认不新增目录」⇒ 采用方案：升级 11-troubleshooting，不新开顶层编号目录；§一 速查表为唯一真源入口 |
| 技能格式 | `.agents/skills/architecture-design/SKILL.md` | front-matter `name` + `description`（触发词内嵌）；技能登记到 AGENTS.md §2.4 表格 |
| 教训悬空实证① | `docs/ai/pilot/2026-10-10-aiagent-tools-features-fix/07-final-report.md` §10/§11 | 「跨插件契约只进 IServiceCollection 不进共享表……值得固化为检查项」——建议无 TODO id、无守卫（对应 TODO.md 现存 P3 条目「plugin-development 技能补铁律」仍未做） |
| 教训悬空实证② | `docs/ai/pilot/2026-10-08-todo-chain-close/07-final-report.md` Problems Found | 「AgentHub 成功回报 ≠ 进程退出（转 todo-ux-state 批次）」「e2e 基建删除重试/killTree（已在 ux-close 批次修复）」 |
| 契约 API 真源 | `ForgeSelf.Core/IContext.cs` L7-25 | `Register<TService>(instance)` 写 root 共享表（兄弟可见）；`RegisterLocal` 仅本上下文；`Get<T>()` 解析序 = 本地值→共享表，缺席返回 null |
| 插件数据登记真源 | `ForgeSelf.Api/Data/XCodeConfig.cs` L24-47 + `ForgeSelf.Api/Plugins/README.md` §三 | `PluginDbs` 字典 = 落数据根唯一入口；未登记 → `versions/<ver>/Data/` 升级即丢（AgentHub/ImGateway 2026-10-09 实证，现已补登记） |
| 裸 fetch 教训 | `.agents/skills/plugin-publish-verify/references/troubleshooting.md` 坑 8（L111-113） | 后端加鉴权后前端裸 `fetch()` 全 401；修法 `src/services/authFetch.ts`；教训 = 鉴权改造必须 grep 前端全部 fetch 调用方 |
| 既有相邻测试 | `ForgeSelf.Api.Tests/XCodeConfigTests.cs` | 只断言 DbFiles 条目形态，**未**断言「插件源码用到的连接名都已登记」——PC-003 枚举测试的缺口确认存在 |

## 触点清单（本任务将修改/新增的资产）

- 新增：`.agents/skills/problem-class-closure/`（SKILL + 8 references + 脚手架脚本）、`docs/11-troubleshooting/problem-classes/`、`docs/11-troubleshooting/cases/`、`docs/18-templates/postmortem/`、`scripts/check-problem-classes.ps1`、`ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs`、`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`
- 修改：`AGENTS.md`（§0 第 2/6 条 + §2.4 登记行）、`docs/04-standards/ai-native-engineering-workflow.md`（v1.2.0）、`docs/18-templates/ai-pilot/07-final-report.tpl.md`、`docs/README.md`（§一 两行 + 11/18 目录四问）、`docs/11-troubleshooting/index.md`、`scripts/hooks/pre-commit`

## 偏差与风险预告

- `verify-pilot-artifacts.ps1` 头部明文「L4 宪法层，Agent 不得自行修改」→ 方案表中「pre-commit 工件门禁加问题类节校验」**不通过改 L4 脚本实现**，改为：新增独立脚本 `check-problem-classes.ps1`（含 `-PilotTask` 模式），由 pre-commit 直接调用。此为对闸门1 方案的一处实现载体修正，已记 03-plan。
- `.agents/` 被远程 `.gitignore` `.*/` 规则忽略（TODO.md P2 已知），技能文件入库需 `-f`/豁免，本批不提交、只落盘，影响留档。
