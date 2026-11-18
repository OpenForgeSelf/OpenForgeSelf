# Specification

> 阶段：Stage 2｜Task ID：2026-11-15-dev-api-multi-instance

## Functional Requirements

- FR-1：根 `pnpm dev` 调用既有 `scripts/dev-stack.ps1`，同时启动本地宿主 API 与宿主前端 dev server。
- FR-2：既有整栈脚本负责端口探测、实例环境、数据根、日志、探活与状态文件；本批不复制这些逻辑。
- FR-3：`pnpm run dev:web` 保留为只启动宿主前端的快捷命令。
- FR-4：根脚本须有回归守卫，确保其调用既有整栈入口；不再要求新增 API-only 启动器。
- FR-5：保留 `scripts/dev-stack.ps1` 的现有启停和单实例语义。

## Input

- 用户从仓库根执行 `pnpm dev` 启动完整开发栈；如只需前端，可执行 `pnpm run dev:web`。
- `scripts/dev-stack.ps1` 已支持的参数（如 `-Stop`、端口、插件目录）不在本批新增包装参数。

## Output

- 后端与前端 URL、PID、日志和数据根按既有脚本输出，并写入 `.temp/dev-stack.json`。

## Business Rules

- 根 `dev` 必须复用 `scripts/dev-stack.ps1`，不应再实现第二套整栈生命周期/端口/数据目录/探活逻辑。
- `dev-stack.ps1` 为固定状态文件与单一整栈实例语义；此任务不承诺并行启动多套整栈，也不改变 `-Stop` 行为。
- `dev:web` 继续只负责启动 `ForgeSelf.Web` dev server。

## Boundary Conditions

- 后端或前端依赖缺失、启动失败、探活超时等沿用 `dev-stack.ps1` 的既有错误与日志行为。
- `dev-stack.json` 已存在时，现有脚本会先停止其记录的实例；此既有单实例行为保持不变。

## Error Handling

- 根命令应透传调用 `dev-stack.ps1` 的运行结果，不另加进程管理。
- `pnpm run dev:web` 的独立前端行为不变。

## Compatibility

- 根 `pnpm dev` 路由到既有全栈启动脚本；`dev:web` 保留为独立前端入口。
- 不新增 `dev-api.ps1`，也不改变 `Program.cs`、`dev-stack.ps1` 或 e2e 的既有行为。

## Non-functional Requirements

- 零新增依赖；隔离目录位于已忽略的 `.temp/`。
- 端口检测应限制尝试次数并关闭探测 socket；失败时不得进入宿主启动。
- 本次改动按 scripts 触发中档门禁：过滤集、仓库脚本检查、问题类检查及全量 `dotnet test`；遵循 §5.0 设置 `TEMP`/`TMP` 与 `NO_PROXY`。

## Failure Modes

| 已知问题类 | 风险 | 本批处置 |
| --- | --- | --- |
| PC-007 本地启动入口绕过脚本，且端口/启动参数存在静默陷阱 | 根 `dev` 未复用已有整栈入口，曾引入额外 API-only 脚本和入口语义混淆 | 让根 `dev` 调用现有 `dev-stack.ps1`；守卫锁定 package 路由与脚本启动前后端；删除不再使用的 `dev-api.ps1`；更新 PC-007 与 case。 |

## Interaction Design（交互设计）

本任务无 UI。用户在终端运行命令后，启动器打印实例 URL/端口并前台运行；Ctrl+C 只终止本次启动的进程。错误路径打印失败原因并返回非零，不提供额外交互提示或确认弹窗。

## Acceptance Criteria

- [ ] AC-1：根 `package.json` 的 `dev` 调用 `scripts/dev-stack.ps1`；`dev:web` 保留；`dev-api.ps1` 不再保留。
- [ ] AC-2：G2 守卫验证根命令指向现有 `dev-stack.ps1`，且该脚本同时启动 API 和前端。
- [ ] AC-3：真实运行 `pnpm dev` 后后端健康端点和前端页面均可访问；状态文件记录两者地址和 PID。
- [ ] AC-4：`dev-stack.ps1 -Stop` 与其现有单实例行为未改动。
- [ ] AC-5：PC-007 G2 守卫与问题类归档检查通过；testhost 权限异常需如实记录。
- [ ] AC-6：解决方案构建、相关测试与 scripts 改动触发的中档全量后端测试真实执行，Evidence 记录真实结果与基线对照。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| `dev-stack.ps1` 是否可由根 package 命令直接复用 | 用户期望根 `pnpm dev` 起全栈 | 已读脚本确认它是全栈入口；接线后实跑 `pnpm dev` 验证前后端探活。 |
