# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：2026-11-14-commit-hygiene-gate

## 项目结构

- `ForgeSelf.Web/`：Vue 3 SPA（Vite 6 + TS 5.7 + Element Plus + Tailwind 4 + pnpm）。
- `ForgeSelf.Api/`：ASP.NET Core 宿主（.NET 10 + SQLite + NewLife.XCode，唯一 ORM）。
- `ForgeSelf.Api.Tests/`：xUnit + Moq + FluentAssertions；含仓库级守卫测试（如 `RepositoryScriptTests.cs`，127 行）。
- `scripts/`：仓库级脚本与门禁；`scripts/hooks/pre-commit`（sh，123 行）+ `scripts/install-git-hooks.ps1`（幂等安装，49 行）。
- `docs/ai/pilot/`：AI-Native 九阶段工件（00-07）；`docs/04-standards/agent-workflow/b-engineering-daily.md`（381 行，B1-B6）为项目不变工程规则真源。
- `.forgeself/`：不入库（.gitignore:69），工作日记与本地配置所在。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 前端 | Vue 3.5 + Vite 6 + TS + pnpm | ForgeSelf.Web/package.json |
| 后端 | .NET 10 + NewLife.XCode | ForgeSelf.Api/ForgeSelf.Api.csproj（版本行 2.4.22，非经允许不改） |
| 后端测试 | xUnit + FluentAssertions | ForgeSelf.Api.Tests/*.csproj |
| 脚本 | PowerShell 7（`pwsh -NoProfile -ExecutionPolicy Bypass -File`），禁 5.1 | AGENTS.md §2.3 |
| 提交门禁 | sh 预提交钩子 + pwsh 校验脚本 | scripts/hooks/pre-commit |

## 架构特点

现行提交门禁已有**三道固定审计门**（scripts/hooks/pre-commit 实读，HEAD 8eba29e）：

1. **设计规范守卫**（:38-78）：暂存触及前端源码时跑 check-style-tokens.mjs + check-design-compliance.mjs，含棘轮基线同步。
2. **问题类台账扫描**（:83-92）：触及 docs/11-troubleshooting/ 或扫描器自身时跑 check-problem-classes.ps1。
3. **PILOT 工件链门禁**（:94-114）：触及 docs/ai/pilot/ 时按任务目录校验 00-07 八件 + 缺陷类三节。
   聚合方式：`FAIL=1` 累加（:32），任一门红即 `exit 1`（:116-121）。
   已登记偏差：hook 内回退分支仍用 5.1 引擎（:20，TODO 存案，本批不修）。

**内容卫生维度目前无任何门禁**：提交的文档/代码/注释中硬编码工作区绝对地址、工作区外地址、人名/角色名/成员名、无关项目信息——既无脚本守卫也无规则条文。存量盘点（-All 口径实测，HEAD 8eba29e）：37 个已跟踪文件命中 POSIX 用户目录形态；9 个文件含群内称呼（含 AGENTS.md:326 对话方名录）；机器账号片段出现在 AGENTS.md:333、DESIGN.md:4/:98 等处。

## 测试方式

- 后端单测：`dotnet test`（ForgeSelf.Api.Tests）；仓库级守卫先例 `RepositoryScriptTests.cs`——用 FluentAssertions 对脚本内容做静态断言（含「含中文的 .ps1 必须 UTF-8 BOM」红灯）。
- 脚本级验证：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/<x>.ps1`，判据看日志正文不看 exit code（agent-workflow §B2 / AGENTS §5.0）。
- 前端设计守卫：`pnpm run design:check`（本批不触前端源码，只跑此档）。

## 构建命令

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1   # hook 幂等安装
cd ForgeSelf.Api.Tests && dotnet test --filter CommitHygieneGuard             # 本批守卫测试
cd ForgeSelf.Web && pnpm run design:check                                     # 设计规范守卫
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| scripts/ | 仓库级脚本/守卫/hook（本批新增守卫脚本落点） |
| scripts/hooks/ | git 钩子源（经 install-git-hooks.ps1 复制进 .git/hooks） |
| .forgeself/ | 不入库的本地配置（本批拒绝列表落点，天然不入库） |
| .agents/skills/ | 项目技能（每技能一个目录 + SKILL.md + 登记进 AGENTS §2.4） |
| docs/07-decisions/ | 决策记录（not-taken-decisions.md 记「为何不做」） |

## 代码组织方式

- 守卫脚本沿用 check-problem-classes.ps1 / verify-pilot-artifacts.ps1 的模式：pwsh 脚本 + pre-commit 挂接 + exit code 聚合。
- 测试沿用 RepositoryScriptTests 的子进程模式（§B2:95-99）：外部进程重定向到日志文件再断言，避免管道死锁。

## 现有工程规范（对本任务有约束力）

- AGENTS §0：预飞六步、完成清单；§2.3：pwsh 7 + BOM 铁律；§5.6：门禁分档（碰 `scripts/**` 必跑中档全量）。
- agent-workflow §B2:95-106：子进程测试排水模式 + TEMP 预检；§B6：.ps1 BOM/编辑工具剥 BOM 等坑。
- §11：本批为流程基建任务，工件链 00-07 齐备。
- 本批自洽红线：**新增的任何入库内容自身不得含违规样本**（守卫脚本、测试、文档、工件、证据一律只写「规则ID + file:line + 计数」，不贴命中原文）。

## 候选低风险任务

本任务即候选：仓库级脚本 + 文档登记，不触宿主源码、不触 DB、不触前端源码。

## 选择该任务的原因

用户 2026-11-14 输入1 直接指令（「是否要求固定流程审计？增加一条……」），且 Part 1（盘点回答）已先行答复；本批把「增加一条」落成第 4 道固定提交审计门。
