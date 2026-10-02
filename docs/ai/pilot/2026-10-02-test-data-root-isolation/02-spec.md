# Spec

> 阶段：Stage 2｜不确定点须标 `Unknown`。

## Functional Requirements

1. 新增测试侧 `[ModuleInitializer]`：在测试程序集加载时（任何用例执行前）执行，仅在 `FORGESELF_DATA_ROOT` **未设置或空白**时，把它设为本进程专属的仓库内隔离目录。
2. 已显式设置 `FORGESELF_DATA_ROOT`（e2e / CI / 操作者前缀）→ **不覆盖**数据根，但日志/配置仍归位到该根。
3. 隔离目录 = `<仓库根>/.temp/dotnet-test/<yyyyMMdd-HHmmss>-<进程号>`（仓库根 = 从 `AppContext.BaseDirectory` 向上找到含 `ForgeSelf.slnx` 的目录）；进程号避免并行/多 worktree 撞目录。
4. **日志路径必须归位**：`XTrace.LogPath` 与 `NewLife.Setting.LogPath` 重定向到隔离根 `log/`，不得指向真实宿主根。
5. **全部已知 Config 文件名必须归位**：`ConfigUnifier.UnifyAllConfigFiles(<隔离根>/config)` 重定向 `NewLife.Setting` / `XCodeSetting` / `NewLife.Agent.Setting` / `Models.ForgeSetting` 的 `FileName`，切断「读程序目录残留 Core.config（固化宿主 LogPath/连接串）→ 写宿主日志/库」链路。
6. 新增守卫测试：断言「测试进程数据根 ≠ 真实 `~/.forgeself`」「自动隔离时落在仓库内 `.temp/dotnet-test/`」「日志路径不落宿主根」「配置文件不落宿主根」；挂 `EnvVarIsolation` 集合（与改环境变量的测试串行）。
7. 文档：`docs/04-standards/agent-workflow.md` §B12#830 由「手工前缀必须设 `FORGESELF_DATA_ROOT`」改为「已自动隔离，手工前缀仅在需要指定位置/覆盖时使用」。

## Input / Output / Boundary

- 输入 = 测试进程启动时的环境变量；输出 = 被设置的 `FORGESELF_DATA_ROOT` + 归位的日志/配置路径。无网络、无外部依赖。

## 顺序约束（关键，实证）

- **必须先 `ConfigUnifier.UnifyAllConfigFiles` 重定向 `FileName`，再设 `XTrace.LogPath`**。反序会在 `XTrace` 首访时触发 `NewLife.Setting` 按默认相对程序目录路径自动创建 `bin/Config/Core.config` 脏文件。

## Error Handling / Compatibility

- 隔离目录创建失败不阻断测试（兜底继续，仅尽力而为）；`RedirectLogAndConfig` 内异常一律吞掉，避免模块初始化异常导致整个测试程序集加载失败。
- e2e 已设 `FORGESELF_DATA_ROOT` → 本机制短路，二者不冲突。

## Acceptance Criteria

- 见 `01-intent.md` Success Criteria 1-4 与 `04-task.md` checkbox。

## Unknown

无（所有关键行为均已由实证确定）。