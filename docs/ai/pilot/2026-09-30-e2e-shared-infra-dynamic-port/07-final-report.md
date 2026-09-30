# AI-Native Pilot Result（最终汇报）

> Task：PILOT-050 ｜ 目录：docs/ai/pilot/2026-09-30-e2e-shared-infra-dynamic-port/ ｜ 日期：2026-09-30

## 1. Repository Understanding

确认：后端端口真源 = `ForgeSetting.Current.PortNumber`（默认 7102），`AppBuilder.cs` 无条件 `app.Urls.Add`（→ ASPNETCORE_URLS 无效，任务书原假设被推翻）；`DataLocationService` 已支持 `FORGESELF_DATA_ROOT` 前置重载；`ApplicationRestartService` 重启不传 env（端口覆盖必须落盘）；Playwright 时序 webServer 先于 globalSetup；e2e 硬编码实测 7102×50 / 7002×9（非任务书的 37/11）。

## 2. Selected Task

e2e 共享基建改造：多 worktree 并行 + 动态端口 + 稳定目录，消除 Windows 防火墙弹窗；宿主新增启动端口覆盖（`FORGESELF_PORT` env 优先 / `--server-port`）；单 worktree 行为与基线不变。

## 3. Changed Files

后端 3：`StartupPortResolver.cs`（新增）、`AppBuilder.cs`（接入）、`Program.cs`（FORGESELF_NO_TRAY 守卫）。前端基建 7：`global-setup.ts`（重写）、`global-teardown.ts`、`helpers/free-port.ts`（新增）、`helpers/e2e-env.ts`（新增）、`helpers/real-auth.ts`、`vite.config.ts`、`playwright.config.ts`、`playwright.e2e-published.config.ts`。spec 清理 12：port-config 重写 + 硬编码清理 11 文件。测试 1：`StartupPortResolverTests.cs`（新增）。文档 5：AGENTS.md、agent-workflow.md、ai-native-engineering-workflow.md、ai-pilot/README.md、engineering.md。完整清单见 05-evidence。

## 4. Validation

Build: `pnpm run check` 0 error（81 存量 warnings）＋ dotnet 随 test 编译通过。
Unit Test: StartupPortResolver 7/7 绿；vitest 490/490 绿。
E2E: `playwright test --list` 190/39 全编译通过；深档全量 4 worker **103 passed / 83 failed / 4 skipped**（基线 102/82，+6 新用例，判责 0 条新增红与端口/基建相关；port-config 6/6 全过）。
Integration: 后端全量 `dotnet test` 1686 总 / 1637 过 / 49 败——24 条基线在册红 + 25 条在跑实例句柄争抢（UnauthorizedAccessException，非沙箱复跑同败实证），0 条与本任务相关。

## 5. Evidence

见 `05-evidence.md`（含三轮深档的失败→根因→修复→复跑闭环：托盘崩溃 → FORGESELF_NO_TRAY 守卫；safe-delete shim 拦截 rmSync → OS 级删除；env 外泄 → 解析器注入式重构）。动态端口链路 Verified：7002/7102 被占自动顺延 7003/7103 → strictPort + FORGESELF_PORT 注入 + 落盘 → token 播种 → current.json。

## 6. Review

`06-review.md` Final Decision：**APPROVED**（Risk L2；Minor×3：基线红清单未落盘、token-init:64 疑既有待归因、注释字样保留）。

## 7. Risk

L2 —— e2e 基建为全项目共享面；已用全量深档回归对表基线持平缓解。回滚点：全部改动未提交 git，`git checkout -- <files>` 即可整体回退。

## 8. Problems Found

① 任务书两大事实错误（ASPNETCORE_URLS 可用 / 硬编码计数 37×11）均被实测推翻；② 宿主托盘在多实例并存下 TryCreate failed 会以 Unhandled exception 打崩整个进程（独立 STA 线程未捕获）——e2e/无人值守必须 FORGESELF_NO_TRAY；③ 本环境 fs.rmSync 被 safe-delete shim 接管，大目录清理必须走 OS 级子进程；④ 单测改进程 env 会外泄给并行 WebApplicationFactory 用例（70 败实证）；⑤ 本机在跑实例与测试存在 `config/*.tmp` 句柄争抢（25 败，TODO 在册）；⑥ 基线红清单从未落盘，每次判责都在人肉考古。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（推翻任务书两处假设，均实测） |
| Intent → Spec | PASS |
| Spec → Plan | PASS（4 条偏差全部记录在案） |
| Plan → Code | PASS（两处 Plan 外必要最小改动有实证） |
| Code → Test | PASS（三轮深档闭环） |
| Test → Evidence | PASS（真实输出 + 判责原文） |
| Evidence → Review | PASS |
| **闸门遵守** | **首开违规后纠正**：本任务曾按临时方案先写代码，被用户指出后补齐 00-04 走闸门1——引导缺口（AGENTS.md 残留 specs/speckit 指引）已封堵 |

## 10. 最重要的问题

**流程引导的「多出口」问题**：AGENTS.md 同时残留 specs/speckit 指引与 §11 唯一流程，agent 在压力下会走出口跳过工件链——本次实证发生并被用户抓出。已修复（specs/speckit 全弃用 + §0 强制读规范步骤 + 日期前缀目录），但说明「唯一流程」必须靠**删除其他出口**而非仅「强调主流程」来保证。

## 11. 下一步建议

把「基线红清单」落盘为可 diff 的固定文件（`docs/ai/pilot/_baseline/backend.reds` + `e2e.reds`，随全量门禁自动更新），使「新增的红才是我的」从人工考古变成一条 `diff` 命令——本次 49/83 条失败的判责耗时远超修复本身。
