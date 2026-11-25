# 02 Spec

## Functional Requirements
- FR1：`UpdateAgentRunner.RunAsync` 中 `RestartLauncher(o)` 必须先于 `CleanupStagedAndBackups(o.InstallDir, o.StagedDir)` 执行。
- FR2：步骤注释须写明不可交换的理由（staged 双重身份 + JIT 延迟加载）。
- FR3：新增 G2 单测断言该顺序，带阳性对照防假绿。

## Input / Output
- 输入：exe 轨更新代理调用 `RunAsync(UpdateAgentOptions)`，`o.StagedDir` 指向 staged 解压根。
- 输出：宿主被成功拉起（进程存在）后 staged 才清理；exit 0。

## Business Rules
- BR1：agent 运行时 `DOTNET_ROOT` = staged（防覆盖自锁），重启那刻才改指安装根。
- BR2：R3 要求应用后清理 staged——但不得先于任何 agent 后续仍需 BCL 的动作。

## Boundary Conditions
- 重启后 `AgentLog.Write("更新流程完成")` 仍可能触发新类型 JIT 加载——但日志类在步骤 0 已加载，风险低；本批不改此处。
- ps1 轨不受影响（运行时来自系统）。

## Error Handling
- 若 `RestartLauncher` 抛异常，整流程红（现有行为，不改）；cleanup 仍失败降级告警（现有行为，不改）。

## Compatibility
- ps1 轨行为不变；单测桩环境不变；仅 exe 轨真机顺序变化。

## Failure Modes（缺陷类必写，源于 P0 命中的 lens）
- FM1：staged 双重身份被后人遗忘，步骤被"优化"回原顺序 → G2 守卫拦截 + 注释劝阻。
- FM2：只预热单个 BCL 而不改顺序 → 贴片治标，其它延迟加载仍踩雷 → 本批不采用。

## Acceptance Criteria
- AC1：`RestartLauncher(o)` 在 `CleanupStagedAndBackups(...)` 之前，源码注释写明不可交换。
- AC2：修复后 agent 可用 SDK 单文件 publish 编译成功（产物存在）。
- AC3：G2 守卫 `Pc050_…` Passed；check-problem-classes 40/40；Registry 6/6。
- AC4：PC-050 + case + 三处索引归档，门禁 check-problem-classes + Registry 全绿。

## Interaction Design
无界面交互变更（后端更新代理步骤顺序）；不涉及 UI。
