# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`。
> Task ID：PILOT-050

## Functional Requirements

- **FR-1 后端启动端口覆盖**：新增启动参数 `--server-port <port>` 与环境变量 `FORGESELF_PORT`（环境变量优先）。仅当来源端口合法（1024–65535）且与 `ForgeSetting.Current.PortNumber` 不一致时，覆盖内存值并 `Save()` 落盘到 `{数据根}/config/ForgeSetting.config`。未提供覆盖时零副作用。
- **FR-2 数据根隔离**：e2e 启动时通过既有的 `FORGESELF_DATA_ROOT`（`DataLocationService` B9-4 已支持）指向隔离数据目录，使落盘的 `ForgeSetting.config` 不污染真实实例。
- **FR-3 稳定目录**：e2e 宿主/数据目录按 worktree 稳定派生（如 `wt-<hash8>`），不再用时间戳；同 worktree 多次运行同目录，消除随机路径导致的防火墙弹窗。
- **FR-4 动态端口贯通**：e2e 侧探测空闲端口，经 `FORGESELF_PORT`（后端监听）、`VITE_APP_BASE_API`（前端代理 target）、`E2E_BACKEND_URL` + 新增 `E2E_FRONTEND_URL`（e2e 测试侧）三通道注入，前后端地址一致。
- **FR-5 硬编码清理**：e2e 侧 `7102`/`7002` 硬编码改为从注入通道/派生值读取，保留默认 `7102`/`7002` 回落（单 worktree 行为不变）。
- **FR-6 特例处理**：`port-config.spec.ts` 改造为端口无关（断言用模板串、恢复运行前端口）；`playwright.e2e-published.config.ts` 端口来源改为 `E2E_BACKEND_URL ?? 7102` 且修复 `publishDir` 越级 bug（原 `resolve(root,'../publish')` 在本机目标不存在）。
- **FR-7 文档回写**：AGENTS.md §2.3/§5.3/§5.6 与 `docs/04-standards/agent-workflow.md` B2/Part C 同步新端口注入机制与门禁说明。

## Input

- 任务书（@long-text）+ 用户追加授权：「宿主端口…加一个参数可启动设置后端运行端口，优先环境变量设置」。
- 既有权威事实（来自真实读取）：`ForgeSetting.Current.PortNumber` 默认 7102；`AppBuilder.cs:523-525` 无条件绑定；`DataLocationService` 已支持 `FORGESELF_DATA_ROOT`；`ApplicationRestartService` 重启不传 env（故端口须落盘）。

## Output

- 新增 `ForgeSelf.Api/StartupPortResolver.cs`（端口解析+覆盖+落盘）。
- `AppBuilder.cs` 在 `ConfigUnifier` 之后接入 `StartupPortResolver.ResolveAndApply(args)`。
- 新增 `ForgeSelf.Api.Tests/StartupPortResolverTests.cs`。
- 改造 `ForgeSelf.Web/e2e/` 下的 `global-setup.ts`、`helpers/real-auth.ts`、`helpers/`（新增 `free-port.ts`/`e2e-env.ts`）、`vite.config.ts`（间接）、`playwright.config.ts`、`playwright.e2e-published.config.ts`、各 `.spec.ts`（`port-config.spec.ts` 等）的硬编码与端口来源。

## Business Rules

- BR-1 端口覆盖优先级：`FORGESELF_PORT` env ＞ `--server-port` CLI ＞ 既有 `ForgeSetting.config` 端口 ＞ 默认 7102。
- BR-2 仅合法范围 1024–65535 生效；越界/非数字/空白忽略该来源（向下一来源回退），不抛异常阻断启动。
- BR-3 覆盖即落盘，使 `ApplicationRestartService` 重启（不传 env）读到同一端口，保证「env 覆盖 / config 落盘 / 重启读取」三方一致。
- BR-4 默认回落：任何来源缺失时保持 `ForgeSetting.Current.PortNumber`（默认 7102），前端默认 7002，单 worktree 行为不变。

## Boundary Conditions

- BC-1 测试宿主（`testhost` 入口，Program.cs:16-21）不设置 `FORGESELF_PORT` → 解析器返回既有端口、不落盘，零副作用。
- BC-2 e2e 未设置 `FORGESELF_DATA_ROOT` 时，落盘落到默认数据根（应始终设置以避免污染真实实例）。
- BC-3 `port-config.spec.ts` 在其内部改端口不重启宿主时，对「当前监听端口」无影响；其 API 恢复类断言需走文件回退（运行前端口），失败改抛错而非静默。
- BC-4 互斥端口 claim：跨 worktree 共享 `os.tmpdir()/forgeself-e2e-ports/<port>.lock`，`fs.openSync(lock,'wx')` 认领 + PID 存活/cwd 存在/mtime 老化（>2h）校验，防 TOCTOU 抢端口。

## Error Handling

- EH-1 来源端口非法（非数字/越界）：忽略该来源，回退下一来源；不 crash。
- EH-2 落盘失败（目录不可写）：catch 记 `XTrace` 警告，本轮仍按内存值绑定，仅重启后可能回退（最佳实践已注释说明）。
- EH-3 探测端口被占用：claim 锁已防占；万一绑定失败由宿主现有异常处理覆盖（本任务不改）。
- EH-4 `port-config.spec.ts` 文件回退失败：明确 `throw` 而非静默恢复，让用例快速失败暴露问题。

## Compatibility

- C-1 向后兼容：默认 7102/7002 回落，未设 env/CLI 时与原行为一致。
- C-2 不与托盘 `--port` 冲突：新增参数为 `--server-port`，托盘专用 `--port` 保持不变。
- C-3 单 worktree e2e 基线不变：102 passed / 82 failed 不因本任务新增失败（红线）。

## Non-functional Requirements

- NFR-1 不引入新依赖、不改业务逻辑、不改 e2e 业务断言。
- NFR-2 平台限制如实上报（e2e 依赖 Windows 宿主）。

## Acceptance Criteria

- AC-1 `StartupPortResolver` 单测覆盖：env 优先于 CLI、CLI `--server-port=7105` 与 `--server-port 7105` 两种形式、非法/越界/空白忽略、`ResolveAndApply` 无覆盖时返回既有端口且不落盘。全部 `dotnet test --filter StartupPortResolver` 绿。
- AC-2 `AppBuilder` 接入后，设置 `FORGESELF_PORT` 启动的宿主监听该端口（由 e2e 验证；单测不触发落盘污染）。
- AC-3 e2e 运行目录稳定派生（同 worktree 同目录），不再用时间戳。
- AC-4 e2e 前后端端口动态贯通、多 worktree 并行不冲突。
- AC-5 e2e 侧 7102/7002 硬编码清理完成，默认值回落保留。
- AC-6 `port-config.spec.ts` 端口无关；`playwright.e2e-published.config.ts` 端口来源与 `publishDir` 修复。
- AC-7 门禁：前端 `check`/`test` 绿；后端中档全量 `dotnet test` 基线持平；深档全量 e2e 4 worker 基线持平（不因本任务新增红/失败）。
- AC-8 文档回写完成（AGENTS.md §2.3/§5.3/§5.6 + agent-workflow.md B2/Part C）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 深档 e2e 全量 4 worker 在本环境的真实耗时与稳定性 | 门禁 AC-7 判定 | 实跑后按基线对表，新增失败归为「本次引入」并修 |
| `port-config.spec.ts` 现有 82 failed 中是否有与端口强相关的额外耦合 | FR-6/AC-6 | 改造后实跑，对表基线 |
| `E2E_FRONTEND_URL` 在全部 spec 中的实际消费点数量 | FR-4 | 改造时 grep 全量确认，写入 FR-5 清理清单 |
