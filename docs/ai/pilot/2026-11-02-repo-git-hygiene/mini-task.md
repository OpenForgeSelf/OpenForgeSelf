---
risk: { level: L1, triggers: [".gitignore", "TODO.md"], raised_by_agent: false }
task_kind: chore
feature: none
expected_files:
  - .gitignore
  - TODO.md
  - .forgeself/memory/2026-10-16.md
  - .forgeself/memory/2026-10-20.md
rollback: git revert 本 chore 提交即可；.trash/2026-11-02-git-hygiene/ 内有 TODO.md.bak 备份
---

# 轻量任务：仓库 git 卫生与待提交批次收口（输入1·2026-11-02）

> 来源：2026-11-02 用户指令「分批提交本项目代码，文档该整理整理，不该提交的文件忽略或者删掉」。
> 本任务 = 提交序列的仓库卫生批 + 各历史批次工件补件的载体目录；非功能开发，零业务代码改动。

## Intent（为什么 / 做什么 / 到什么程度）

工作区积压 91 条未提交变更（多个已闭环待闸门3 的批次）。本任务把仓库元数据整理干净（该忽略的忽略、误入库的停止跟踪、备份件归档），并把历史批次的 pilot 工件补齐到可通过 pre-commit 门禁的程度，使代码可按逻辑边界分批提交。

## 改动（Allowed）

- `.gitignore`：负向豁免 `!.agents/`、`!.github/`（`.*/` 兜底规则误伤必须入库目录，致新增技能/工作流文件静默不入库）；新增 `/TODO.md.bak` 忽略。
- `TODO.md`（本机活队列，2026-09-24 立不入库）、`.forgeself/memory/2026-10-16.md`、`2026-10-20.md`（误入库日记）：`git rm --cached` 停止跟踪，本地文件保留。
- `TODO.md.bak` → `.trash/2026-11-02-git-hygiene/`（禁永久删除约定）。
- 各历史批次工件补件（归属各自目录/提交）：无界面交互声明 ×3、04-task 头部块 ×2、chat-list 05 AC 对账、goal-sse 05/06/07 补录。

## Forbidden

- 不改任何业务代码/插件/脚本行为；不动远程（不 push）；不删除用户文件（只归档）；不提交「闸门1 待批」的纯计划目录（responses-incomplete-status、aiagent-ui-design-brief）。

## Interaction Design（无界面交互声明）

**无界面交互**：纯仓库元数据整理（gitignore / git 索引 / 工件文档补件），无任何 UI/交互改动。

## 验证

`git check-ignore` 三态实测 + `git status` 干净度 + 各 pilot 目录工件门禁 PASS（详见 05-evidence）。
