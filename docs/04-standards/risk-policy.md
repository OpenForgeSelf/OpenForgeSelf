---
status: approved       # 2026-10-06 用户批准；AGENTS.md §2.2 分级闸门自此生效
version: 0.2
---
# 风险分级与闸门策略

> 作用：决定人在什么时候介入。不决定要不要做 Evidence 与 Review，任何级别都必须有。
> 与验证档位的关系：本文决定"谁批准"，AGENTS.md §4.3 的快/中/深决定"测多深"。
> 本文属 L4，Agent 不得自行修改。

## 1 判级原则
1. 机械判定优先（`classify-risk.ps1` 落地前由实现者手工按 §3 判，Reviewer 复核）。
2. 只能上调，不能下调；多项触发取最高；拿不准上调一级。
3. 实现中 diff 触及未预期触发项，立即停止并重新判级。
4. 结果写入 04-task.md 头部块；Review 的 Risk 须与之一致，不一致即 CHANGES_REQUIRED。

## 2 等级与闸门
见 AGENTS.md §2.2 的矩阵（以 AGENTS.md 为准，此处不重复维护）。

## 3 触发表（2026-10-06 已按真实仓库核对；未核对项已剔除）
| 路径或模式 | 最低级别 |
|---|---|
| `docs/**`（受保护项除外）、`README*`、`docs/ai/pilot/**` | L0 |
| `ForgeSelf.Web/src/**`（不含路由、公共组件、store 契约）、`ForgeSelf.Api.Tests/**`、`ForgeSelf.Web/e2e/**`（不含全局配置与夹具）、`Plugins/*/web/src/**` | L1 |
| 路由、公共组件、store 契约；`Plugins/*/Controllers/**`、`Plugins/*/Services/**`；`ForgeSelf.Api/**` 默认；`docs/03-design/**`；`.agents/skills/**`；`docs/04-standards/agent-workflow.md` | L2 |
| `plugin.json`；插件契约层与宿主内核接缝；`ForgeSelf.Api/{Entities,Security,Data}/`、宿主装配 `AppBuilder.cs`（已核对）；`Plugins/DesignSystem/**`（设计系统与全局 token）；`*.csproj`、`package.json`、锁文件；打包、升级、备份、update-agent、根启动器（`scripts/release/**`、`scripts/update-agent.ps1`）；`scripts/release*`、`scripts/sign-publish.ps1`；`.github/workflows/**`；`ForgeSelf.Web/e2e` 的 `global-setup.ts`、`playwright.*.config.ts`、`fixtures/**` | L3 |
| `AGENTS.md`、流程规范、本文件、`docs/18-templates/**`、`scripts/hooks/**`、`verify-pilot-artifacts.ps1`、`install-git-hooks.ps1`、`classify-risk.ps1`；`.env`、`appsettings.Production.json`、含密钥文件 | L4 |
| `openwiki/**` | 禁止手改 |

内容触发（diff 中出现即至少 L3）：`Authorize`、`ApiKey`、`AES`、`Encrypt`、`Password`、`Secret`、`DROP `、`ALTER `、`Process.Kill`、`Process.Start`、`File.Delete`、`Directory.Delete`、`Remove-Item -Recurse`、`git push`、`--force`。
规模触发（起始值，按数据调整）：超过 10 个文件或 300 行（不含锁文件与生成文件）上调一级，最高到 L3。

## 4 永远需要人批准（不论级别）
- 宿主发行 tag（含 `-preview`）；插件 tag 可由 Agent 在闸门3后打。
- 新增依赖、修改构建配置；数据库迁移或任何不可逆数据操作。
- 修改本文、闸门脚本、钩子、AGENTS.md 的流程条款。
- 对用户运行实例的任何写操作（只读复验除外）。

## 5 抽检与熔断
- L0、L1 自动提交的任务，前 4 周**全部**抽检；之后每周抽检 20%。
- 抽检发现 1 次 Critical 或连续 2 次 Major：L1 临时升为 L2，持续 7 天。
- 人每推翻一次 Agent 的自动判定，就把该类变更加进 §3（棘轮）。
- 每月看一次：各级任务数、被推翻比例、返工率、升级次数。被推翻比例高的级别，说明规则太松。
