# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-050 ｜ 日期：2026-09-30

## Problem

1. `ForgeSelf.Web/e2e/global-setup.ts` 每次运行把宿主/数据目录放在 `.temp/e2e/<时间戳>` 随机目录下；多 worktree / 多次并行运行时目录随机漂移，且每次新路径触发 **Windows 防火墙授权弹窗**，阻断无人值守自动化。
2. e2e 侧大量硬编码端口 `7102`（实测 50 处）/ `7002`（实测 9 处）；多 worktree 并行时固定端口互相冲突，无法并存。
3. 任务书原假设「`ASPNETCORE_URLS` 可覆盖端口」经实测不成立：`AppBuilder.cs:525` 无条件 `app.Urls.Add`，`IServerAddressesFeature.Addresses` 已非空 → 该环境变量被忽略。
4. 单 worktree 现有 e2e 行为与测试基线不得被破坏。

## Why

- 让 e2e 基建支持**多 worktree 并行**且**不再因目录随机而弹防火墙**，是 CI / 本地多分支验证的前置条件；否则每次跑 e2e 都需人工点弹窗。
- 端口稳定/动态派生 + 注入通道打通，使同一台机器上 dev 实例、多个 e2e worktree 互不干扰。

## Expected Outcome

- 目录路径按 worktree 稳定派生（不再用时间戳）；端口动态探测空闲端口并贯穿前后端。
- 通过三条注入通道打通前后端地址：`ASPNETCORE_URLS`（后端监听）/ `VITE_APP_BASE_API`（前端代理 target）/ `E2E_BACKEND_URL` + 新增 `E2E_FRONTEND_URL`（e2e 测试侧）。
- e2e 侧 7102/7002 硬编码消除（保留默认值回落，单 worktree 行为不变）。
- `port-config.spec.ts`、`playwright.e2e-published.config.ts` 的复用/特例正确处理。
- 回写 AGENTS.md §2.3/§5.3/§5.6 与 `docs/04-standards/agent-workflow.md`。

## Constraints

- **不伪造验证结果**（硬约束 §1.10 / 规范 §1.10）。
- **不改后端 C# 业务逻辑**：仅新增「启动端口覆盖」能力（`--server-port` + `FORGESELF_PORT` 环境变量优先），且须经用户明确授权（已授权）。
- **不引入新依赖**。
- **不改 e2e 业务断言逻辑**（只改基建/端口注入/硬编码清理）。
- **保持向后兼容**：默认端口 7102/7002 回落，单 worktree 行为不变。
- **平台限制如实上报**：本任务 e2e 依赖 Windows 宿主（playwright webServer 起真实 `ForgeSelf.exe`），非 Windows 环境无法跑深档 e2e。

## Success Criteria

1. 新增后端启动端口覆盖（`--server-port` 参数 + `FORGESELF_PORT` 环境变量优先），`dotnet test` 中对应单测全绿。
2. e2e 运行目录按 worktree 稳定派生（同 worktree 多次运行同目录），且不再因随机路径触发防火墙弹窗（按机制判定，实机弹窗由人观察）。
3. e2e 前后端端口动态派生并贯通（后端监听 / 前端代理 / e2e 测试三侧一致），多 worktree 并行不抢端口。
4. e2e 侧 7102/7002 硬编码清理完成，默认值回落保留；单 worktree 现有 e2e 用例行为不变。
5. `port-config.spec.ts` 改造为端口无关；`playwright.e2e-published.config.ts` 端口来源改为 `E2E_BACKEND_URL ?? 7102` 且修复 `publishDir` 越级 bug。
6. 门禁通过：前端 `pnpm run check`/`test` 绿；后端「中档」全量 `dotnet test` 基线持平（基线 1516/13 红，不因本任务新增红）；「深档」全量 e2e 4 worker 基线持平（基线 102 passed / 82 failed，不因本任务新增失败）。
7. 文档回写完成：AGENTS.md §2.3/§5.3/§5.6 + `docs/04-standards/agent-workflow.md` B2/Part C。
