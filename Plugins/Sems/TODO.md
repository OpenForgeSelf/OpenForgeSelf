# Sems 插件 TODO（插件级遗留）

> 条目格式：`- [ ] <事项>（P<优先级>，来源）`；完成即移除，不留 ✅ 堆积（AGENTS §7.5）。
> 当前版本：`plugin.json` 1.2.0（B1 规格功能，未发布）；运行实例 `:51888` 上的版本以实例为准，未核实。

## ⬜ 待办

- [ ] **B1 规格功能（v1.2.0）未发布未走查（P1，来源:输入7，2026-10-30 更新）**：9 个规格工具已实现，连同既有 13 个共 22 个 sems_* 工具；验收标准覆盖与注册守卫已有单测。2026-10-30 `.temp/results-sems-b1b/sems-b1b.trx` 为 passed=103 failed=0。剩余：冒烟 e2e、dev-stack 源码走查、本地目录发布（须用户同意）、`:51888` 只读复验。运行时匿名 401 仍 Unknown。
- [ ] **安全：sems_add_command 接受任意 script，配合 sems_run_command 可在项目目录执行任意命令（P1，来源:输入1/2 读码发现）**：mcp-center 调用是否鉴权未核实；修复方式由用户另立任务决定，本任务不改。
- [ ] **运行实例 1.1.1 激活后只读复验（P1，来源:输入12/13）**：用户在 `:51888` 运行实例把 sems 更新到 1.1.1（激活 `current` 或页面自动更新）后，按 `plugin-publish-verify`「运行实例只读复验」执行：验 token 有效（401 立即上报）→ 注入 `forge_api_token` → 核 `.view-title` 版本徽标 = 1.1.1 → 走一条主链路并采中间态 → 截图存 `ForgeSelf.Web/screenshots/live-<端口>/` 并读图。只读、不点不可逆、不启停宿主。
- [ ] **sems e2e :261 负载 flake 加固（P2，来源:输入13 深档全量实测）**：4 worker 全量下「确认停止」后 `.run__item` 20s 不归零（期望 0 实得 1）；单跑 4/4 绿（53.8s）。与 RunnerServiceTests 并发 flake 同根。根治两处：① 宿主 `RunnerService.KillTree` 退出码判定加「进程已不存在视为成功」（host 侧，`taskkill /T /F` 返回非 0 时进程可能已退出）；② `RunPanel.vue` 停止后以 `list_runs` 真值兜底刷新，不依赖单次停止响应。
- [ ] **运行实例插件目录旧产物清理（P3，来源:输入1 发布实测；agent 删工作区外文件被拦，需手清或授权）**：`D:\src\tools\ForgeSelf\plugins\sems\versions\1.1.0` 与 `_backups\sems\1.1.0` 里的 `ForgeSelf.Abstractions.pdb`/`ForgeSelf.Core.pdb`（宿主共享 pdb 不应随插件包）；上游布局已「去 _backups」，`_backups/` 目录本身可一并清。

## 📌 宿主级/仓库级暂寄（根 TODO.md 随 worktree 销毁，先寄存在此防丢；处理时可挪回根队列）

- [ ] **批次E 存量红（7 项）收口（P2，来源:输入1 全量门禁实测；输入13 复跑实名定性）**：`WorkflowPlanningIntegrationTests`×6 + `ScriptRunnerDiIntegrationTests.GetRuntimes_...`（测试宿主缺插件 DLL/路由 404，`ForgeSelf.Api.Tests.csproj:74-87` 失效模式）。注意：原第 8 项 TerminalCommandGuard 大小写断言已实测 55/55 全绿（过时，移出）。
- [ ] **ApiKeyServiceTests 文件锁 flake 根治评估（P3，来源:输入13 全量复跑定性）**：`System.IO.IOException 无法删除要被替换的文件`——NewLife FileConfigProvider 在 Dispose/ctor 落 config 时文件被占用，每轮全量浮 1-2 例；单跑即绿。方向：测试独占 temp config 路径或 Save 带重试。
- [ ] **新 worktree 跑插件 UI e2e 前须先构建全部带界面插件的 web/dist（P2，来源:输入1 回归定位）**：待回写 `e2e-testing` 技能前置检查单。
