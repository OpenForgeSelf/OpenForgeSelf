# Repository Understanding

> 阶段：Stage 0｜事实必须来自真实仓库。

## 项目用途
OpenForgeSelf（铸己匣）—— 本地优先的 AI Agent 宿主：Vue 3 SPA 前端 + ASP.NET Core 后端 + 插件体系，配一套 AI-Native 开发流程（九阶段工件链 + 三道闸门）。

## 技术栈（实测）
- 前端 `ForgeSelf.Web/`：Vue 3.5 + Vite + TS + Element Plus + Tailwind + Pinia（pnpm）
- 后端 `ForgeSelf.Api/`：.NET 10 + SQLite + NewLife.XCode + 插件架构
- 测试 `ForgeSelf.Api.Tests/`：xUnit + Moq + FluentAssertions + Coverlet
- e2e：`ForgeSelf.Web/e2e/`（**不在仓库根**，此处为实测纠正）
- 插件 `Plugins/`（18 个，`plugin.json` 注册）

## 关键目录与文件
| 路径 | 职责 |
|---|---|
| `AGENTS.md` | 每次必守规则（宪法层） |
| `docs/04-standards/ai-native-engineering-workflow.md` | 开发流程唯一依据（三道闸门） |
| `docs/04-standards/agent-workflow.md` | 规则库（Part A/B/C，131KB） |
| `docs/02-features/` | 功能事实源（39 篇） |
| `docs/ai/pilot/` | 任务工件（每任务一目录） |
| `docs/18-templates/` | 模板（含 ai-pilot 八件） |
| `scripts/` | 验证/发布/钩子脚本 |
| `.github/workflows/` | CI（**实测：仓库内仅 `release.yml` 被 git 跟踪**） |

## 本任务相关的真实缺口（实测）
1. `verify-pilot-artifacts.ps1` 注释声称有 `.github/workflows/artifact-gate.yml`，**实际不存在**（仓库内被跟踪的 workflow 只有 `release.yml`）。
2. `scripts/hooks/pre-commit` 调用 `powershell.exe`（5.1），与 AGENTS 的 `pwsh` 硬规则冲突。
3. 校验脚本硬要求 00–07 八件，而流程规范 v1.1.0 允许 `mini-task.md`；仓库内**已存在轻量目录**（如 `2026-09-30-sign-default-off`）→ 二者矛盾，轻量目录会被钩子误拒。
4. `docs/02-features/` 39 篇**零 front-matter、零需求清单、零稳定需求 ID**；且编号有 3 组重复（028/031/036）。
5. `AGENTS.md` 与 `e2e/**` 路径、`themes/` tokens 表述与真实仓库不符（e2e 在 `ForgeSelf.Web/e2e`；全库无 `themes/`、无 tokens 文件）。

## 候选与选择理由
本任务的"仓库"不仅是代码，还包括流程机制本身；改动面判定为 **L4**（触及 `AGENTS.md`、`scripts/hooks/**`、`docs/18-templates/**`、`.github/workflows/**` 等宪法层），由用户明确指示执行。
