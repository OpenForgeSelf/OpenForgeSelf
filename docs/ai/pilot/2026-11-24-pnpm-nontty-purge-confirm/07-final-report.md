# 07 Final Report — 发布链 pnpm install 非 TTY 清 modules 确认中断（输入34/35 2026-11-24）

## Repository Understanding

- `scripts/release/release-local.ps1` → `build-frontend.ps1:39` `pnpm install --frozen-lockfile`，作用于宿主 + 每个 `Plugins/<X>/web`。
- 环境核验：`pnpm 10.13.1/10.33.1`、`CI=unset`、无 TTY（`test -t 0`=no）。
- 适用问题类：PC-043/047 邻域（发链 install 失败族），根因不同 → 新建 PC-048。

## Selected Task

方案 A（用户回复「a」批准）：`build-frontend.ps1:39` install 加 `--config.confirmModulesPurge=false`，精确关闭 pnpm 清 modules 的 TTY 确认，保留 `--frozen-lockfile`。

## Changed Files

| 文件 | 关键变更 |
|------|----------|
| `scripts/release/build-frontend.ps1` | `:39` 加 `--config.confirmModulesPurge=false` + 中文注释 |
| `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` | 新增 Pc048 G2 守卫 |
| `docs/ai/pilot/2026-11-24-pnpm-nontty-purge-confirm/` | mini-task / 05-evidence / 06-review / 本 07 |
| `docs/11-troubleshooting/problem-classes/PC-048-*.md` | 新问题类真源 |
| `docs/11-troubleshooting/cases/2026-11-24-release-local-pnpm-nontty-purge-confirm.md` | case |
| `docs/11-troubleshooting/{problem-classes,cases}/README.md`、`docs/11-troubleshooting/index.md` | 三处索引 |
| `.forgeself/memory/2026-11-24.md`、`TODO.md` | 预飞日志 + 待办同步 |

## Validation（均 Verified）

| 项 | 结果 |
|----|------|
| 参数接受度 | fixture `pnpm install --frozen-lockfile --config.confirmModulesPurge=false` exit=0 |
| 单插件真跑 | `build-frontend.ps1 -PluginsOnly -Plugin SmartTerminal` exit=0，dist 产出，pnpm install 不再 abort |
| G2 守卫 | `dotnet test --filter Pc048`：1/1 通过 |
| PC 机器校验 | 48 个 PC 全部通过（含 PC-048） |

## Evidence / Review

见 05 / 06。Final Decision: APPROVED（闸门2 待用户验收；未提交，闸门3 未启动）。

## Risk

L0 低：单点参数 + 守卫；CI/人工 TTY 行为等价，无回归面。

## Problems Found

- 无 TTY 且无 CI 变量的「第三态」未被脚本契约覆盖（本批修复）。
- 本机沙箱 safe-delete 配额导致完整 `-Sign` 链无法单 tool call 跑通（既有 TODO P1，非本批）。

## 问题类归档

PC-048（guarded）+ case + 三处索引齐备；同类扫查同根因命中 1 处（已修），异根因 3 类不修并记入 case §4 / PC §2 盲区。

## 重构提案

R0 不重构：单点修复足够，`release-local.ps1` 主流程不动。

## 悬空教训清单

空。所有发现均落 PC-048 或 TODO id。
