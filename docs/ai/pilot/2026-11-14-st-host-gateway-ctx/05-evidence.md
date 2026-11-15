# Evidence

> 阶段：Stage 7｜只记录实际发生的事情（2026-11-14 23:0x–23:4x 实跑）。
> 来源等级：Verified（亲跑有真实输出）/ Inferred / Unknown。

## Task

输入18/19：智能终端助手/凭据恒 424 —— StHostGateway 注入 IServiceProvider 拿到插件子容器（PC-034 首见，缺陷类闭环）。

## Changed Files

- `Plugins/SmartTerminal/Services/StHostGateway.cs`（canonical fix：IContext 构造 + ctx.Get 解析）
- `Plugins/WorkflowEngine/Services/WorkflowExecutor.cs`（G2 命中：半迁移收敛为单 IContext 构造）
- `Plugins/WorkflowEngine/WorkflowEnginePlugin.cs`（RegisterServices 签名 + 5 个工具调用点 `(IContext)` 传参）
- `Plugins/Scheduler/Services/TaskExecutor.cs`（G2 命中：IContext 构造）
- `Plugins/Scheduler/Services/WorkflowTaskHandler.cs`（G2 命中：IContext 构造 + using）
- `Plugins/Scheduler/SchedulerPlugin.cs`（4 处调用点改传 ctx）
- `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StHostGatewayWiringTests.cs`（新增，G1 接线守卫 3 例）
- `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（新增 PC-034 G2 枚举守卫 2 例 + 检测器）
- `ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts`（冒烟扩展：真实会话 + 助手往返 + 424 文案零容忍）
- `docs/11-troubleshooting/problem-classes/PC-034-plugin-di-service-host-contract-miss.md`（新建）
- `docs/11-troubleshooting/cases/2026-11-14-plugin-di-service-host-contract-miss.md`（新建）
- `docs/11-troubleshooting/problem-classes/README.md`（台账行 + tags 检索行）
- `docs/11-troubleshooting/cases/README.md`（case 行）
- `docs/11-troubleshooting/index.md`（速查行 + 更新日期）
- `Plugins/SmartTerminal/docs/03-架构与接口.md`（§1 精度补记 + 424 排查指引 + PC-034 指针）
- `TODO.md`（会话队列，不入库）

## Build

Command: `dotnet build ForgeSelf.slnx --nologo`

Result: PASS（Verified）

```text
已成功生成。
    8 个警告
    0 个错误
```

## Unit Test

Red（修复前，接线守卫）：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~StHostGatewayWiringTests"`

Result: FAIL（预期红，Verified）——`.temp/red-st-gateway.log`

```text
失败 TryChat_Resolves_Host_Contract_Through_Real_Plugin_Wiring:
  Expected gateway.TryChat() to refer to ...FakeChatCompletion because StHostGateway 经 IContext 通路…, but found <null>.
失败 TrySecrets_Resolves_Host_Contract_Through_Real_Plugin_Wiring: Expected gateway.TrySecrets() not to be <null>
失败! - 失败: 2，通过: 1，总计: 3
```

Green（修复后，同测试）：Result: PASS 3/3（Verified）——`.temp/green-st-gateway.log`

G2 守卫（PC-034 枚举）：`dotnet test --filter "FullyQualifiedName~Pc034|FullyQualifiedName~StHostGatewayWiring"`

- 首跑：编译错 CS0021（测试代码自身）→ 修正后二跑：检测器自证通过 + 全插件扫描 **真实命中 4 处**（Verified）——`.temp/g2-second-run.log` + pwsh 复刻全量清单：`ForgeSelf.Api/Plugins/PluginManager.cs（宿主基建，扫描面收窄排除）`、`Plugins/Scheduler/Services/TaskExecutor.cs`、`Plugins/Scheduler/Services/WorkflowTaskHandler.cs`、`Plugins/WorkflowEngine/Services/WorkflowExecutor.cs`
- 修复 + 扫描面收窄后三跑：**5/5 全绿，allowlist 空账**（Verified）——`.temp/g2-third-run.log`

```text
已通过! - 失败: 0，通过: 5，通过: 0 已跳过，总计: 5（Pc034×2 + StHostGatewayWiring×3）
```

受影响面过滤集：`dotnet test --filter "FullyQualifiedName~SmartTerminal|FullyQualifiedName~WorkflowExecutor|FullyQualifiedName~Scheduler|FullyQualifiedName~WorkflowEngine|FullyQualifiedName~ProblemClass"`

Result: PASS 123/123（Verified）——`.temp/test-filters.log`

```text
已通过! - 失败: 0，通过: 123，已跳过: 0，总计: 123，持续时间: 45 s
```

问题类台账一致性：`dotnet test --filter "FullyQualifiedName~ProblemClassRegistry"` → PASS 5/5（Verified，补回 PC-004/PC-009 planned 守卫 todo 行后）。

全量收尾（中档强证）：`dotnet test ForgeSelf.Api.Tests --nologo` → 3759 通过 / 27 红 / 3786（Verified，.temp/test-full-final.log）。归属实验（git stash → HEAD 复跑 → pop，Verified）：27 红全部为存量——基线红 23 例（AIAgent ProjectWorkspace×15 + WorkflowPlanning×6 + JsonNonAscii×1 + ScriptRunnerDi×1，抽样在 HEAD 无本批改动时复现失败）+ 顺序依赖 flake 4 例（ApiKey/Maf/ScriptRunner-Cancel/DesignAgent，HEAD 隔离跑全过、仅全量顺序跑红）；**零例与本批相关**（本批过滤面 123/123）。基线红清单已记 TODO。

## Integration Test

N/A（本批无新集成测试；G1 接线测试即真实双容器集成装配）。

## E2E

`pnpm run test:e2e -- e2e/plugins/smart-terminal/smart-terminal.spec.ts --workers=1`（globalSetup 真 publish 宿主 + 源码 vite 前端 + 解密真实 token，零 mock）

Result: PASS 1/1（Verified，1.0m）——`.temp/e2e-smoke5.log`

用例链路：路由加载（标题「智能终端」）→ 新建本机会话（起始目录置 `.`）→ 助手 rail 发送「你好，请报一声当前目录」→ thinking 往返完成 → 断言助手气泡存在且**不含「宿主对话模块不可用」**。实测气泡内容 = 「调用宿主模型失败：未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型…」（e2e 宿主未配模型，属提供方层错误——424 接线病文案未再现）。

## Static Analysis

- `pnpm exec eslint e2e/plugins/smart-terminal/smart-terminal.spec.ts` → 0 问题（Verified）
- `pnpm run test`（vitest）→ 116 files / 1209 tests 全绿（Verified）
- `pnpm run design:check` → style-tokens PASS + design-compliance PASS（Verified）
- `pnpm run check`（vue-tsc 全域）→ FAIL：**基线存量红**，全部位于 `Plugins/DbAdmin/web/src/DbAdminView.vue`（TS2339 cellDetail/copyText，L1558-1563；该文件本批未触碰，git status 干净）；SmartTerminal/e2e spec 无任何错误。已记 TODO（来源:输入19）。
- `pwsh scripts/check-problem-classes.ps1` → OK：34 个 PC 签名/allowlist/guards 全部通过（Verified）
- `pwsh scripts/check-commit-hygiene.ps1 -Staged` → PASS（files=17 lines=555 hits=0）（Verified）

## Origin-Code Walkthrough（③ 起源码走查，API 层）

`pwsh scripts/dev-stack.ps1` 起源码宿主（动态端口 7301/7399，插件目录=源码 `Plugins/`，隔离数据根 `.temp/dev-stack/data`）：

1. token 鉴权：GET /api/smart-terminal/meta → 200（Verified）
2. GET shells → cmd/PowerShell 等（Verified）
3. POST sessions `{kind:local, shellId:'cmd', startDir:'.'}` → `s-45218fb722974e0cbd26a6799f66c064`，status=connected（Verified）
4. POST sessions/{id}/chat/messages `{content:'你好，请报一声当前目录'}` → **HTTP 424 + `host_module_unavailable` + message=「未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型，或为该通道绑定模型后再试。」**（Verified）

判读：424 状态码与 code 为 V2 既有契约（本批约束不动，`TerminalSessionService.V2.cs:463-489`：TryChat()=null → 「宿主对话模块不可用」；TryChat()≠null 但模型缺失 → result.Error 文案）。**缺陷判据 = message 层**：旧病文案「宿主对话模块不可用，会话不受影响」未再现，返回的是提供方层错误 = IChatCompletion 已解析成功、请求已抵达宿主对话网关。与 publish 宿主 e2e 截图行为一致（双宿主互证）。

## 运行实例侧载与浏览器实控走查（2026-11-15，用户输入20 授权「只打包插件，操作浏览器到插件管理更新并测试」）

1. 打包：`plugin.json` 2.0.0→2.0.1；`dotnet build -c Release`（SmartTerminal.dll 208384B）+ 插件 web dist 构建；staged 至运行实例 `plugins\SmartTerminal\versions\2.0.1\`（只写版本目录 + 不动顶层清单；dll/manifest/deps.json/web-dist 四件，无 pdb/Data/共享 DLL）
2. 浏览器实控（Playwright live spec `e2e/plugins/smart-terminal/smart-terminal.live.spec.ts`，live config 仅匹配 `*.live.spec.ts`）：token 前置校验（鉴权端点 200）→ 插件管理页版本历史出现 v2.0.1 → 「激活此版本」+ 确认弹窗 → 插件卡片 v2.0.1 → `/smart-terminal` 徽标 **v2.0.1** → 新建会话（startDir `.`，已连接）→ 助手消息「你好，请报一声当前目录」→ **真实模型回复**：助手气泡实答「当前会话目录：…\2.4.22.2611131611」并提议 `pwd` 命令卡片（插入到终端/批准后执行）→ **「宿主对话模块不可用」零命中** → pageerror 零（1 passed, 30.8s）
3. 宿主侧互证：`GET /api/plugin` 与 `detail` 均报 2.0.1（Verified）；`current` 指针 = 2.0.1（Verified）；伺服前端 `…/web/dist/index.js` = 81624B 新包（Verified）
4. 过程缺陷发现与同类根治：视图版本徽标硬编码字面量（`SmartTerminalView.vue:1217` v2.0.0）——清单 bump 后滞后报旧版；修复 = vite `define` 从 `../plugin.json` 注入 `__PLUGIN_VERSION__`，模板改绑定（版本串单一来源），重建后徽标 v2.0.1 实测通过
5. 工件事故与恢复（全程留痕 .forgeself/memory/2026-11-15.md）：live 首跑 `--output` 路径笔误 → Playwright 输出目录清理清空 worktree → 手写 `.git` 文件 + `git worktree repair` + `git checkout -- .` 自 index 完整恢复暂存内容（21 文件 771+/64-）；`--output` 已修正并沉淀教训

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/smart-terminal/smoke-view.png`（publish 宿主整页）
- `ForgeSelf.Web/screenshots/e2e/smart-terminal/smoke-chat.png`（真实会话 + 助手气泡：提供方层错误文案，无 424 病文案）
- `ForgeSelf.Web/screenshots/live-51888/live-plugins-before.png` / `live-versions-dialog.png` / `live-plugins-after.png`（侧载激活前/版本历史/激活后）
- `ForgeSelf.Web/screenshots/live-51888/live-st-view-201.png`（运行实例视图，徽标 v2.0.1）
- `ForgeSelf.Web/screenshots/live-51888/live-st-chat-201.png`（**运行实例真实模型回复**：助手实答当前目录 + 命令提议，无 424 病文案）

## Known Limitations

- ~~「真实模型回复」未验证（Unknown）~~ **已闭合（Verified 2026-11-15，运行实例 :51888 侧载后实测）**：真实模型回复产出（见上「运行实例侧载与浏览器实控走查」+ live-st-chat-201.png），原 LM Studio / 隔离数据根限制不再构成缺口。
- `pnpm run check` 基线红（DbAdmin，非本批）——TODO 已记。

## Unresolved Issues

无 BLOCKED。遗留均已落账：WorkflowExecutor L566-571 脚本步骤（PC-001 同族，TODO P1）；SystemMonitor Apply 期 eager（PC-005 同族，TODO P2）；PC-004/PC-009 planned 守卫 todo 补回（TODO）；DbAdmin 基线 TS 红（TODO）。
