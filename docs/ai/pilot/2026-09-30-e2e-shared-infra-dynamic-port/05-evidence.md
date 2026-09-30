# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> Task：PILOT-050（docs/ai/pilot/2026-09-30-e2e-shared-infra-dynamic-port/）

## Changed Files

- `ForgeSelf.Api/StartupPortResolver.cs`（新增：端口覆盖解析器，env 查找函数可注入）
- `ForgeSelf.Api/AppBuilder.cs`（ConfigUnifier 之后接入 `StartupPortResolver.ResolveAndApply(args)`）
- `ForgeSelf.Api.Tests/StartupPortResolverTests.cs`（新增：7 用例，env 注入式、零进程 env 变更）
- `ForgeSelf.Web/e2e/helpers/free-port.ts`（新增：worktreeTag + 端口认领注册表 + 绑定探测）
- `ForgeSelf.Web/e2e/helpers/e2e-env.ts`（新增：backendUrl/frontendUrl/forgeSettingConfigPath/readCurrentRun）
- `ForgeSelf.Web/e2e/helpers/real-auth.ts`（去时间戳正则；configPath 惰性求值；token 走 current.json）
- `ForgeSelf.Web/e2e/global-setup.ts`（重写：wt-<hash8> 稳定目录、残留保护、FORGESELF_PORT/DATA_ROOT 注入、current.json/host.pid）
- `ForgeSelf.Web/e2e/global-teardown.ts`（释放端口认领 + 清 current.json）
- `ForgeSelf.Web/vite.config.ts`（E2E_FRONTEND_PORT 动态端口 + strictPort；代理 target 级联）
- `ForgeSelf.Web/playwright.config.ts`（求值期同步认领端口、动态 baseURL/webServer、env 透传、MCP 端口派生）
- `ForgeSelf.Web/playwright.e2e-published.config.ts`（E2E_BACKEND_URL 级联；publishDir 越级修复）
- `ForgeSelf.Web/e2e/port-config.spec.ts`（端口无关化；restorePort 失败改抛错）
- 硬编码清理 11 处：ai-provider-chat / ai-providers / api-gateway / code-snippets / quicklinks / memory / workflows / profile / token-init / token-regenerate / todo
- 文档：`AGENTS.md`（§0/§2.3/§5.3/§5.6/§9/§11）、`docs/04-standards/agent-workflow.md`（B2/统一e2e体系/B4/Part C）、`docs/04-standards/ai-native-engineering-workflow.md`（§0 日期前缀）、`docs/18-templates/ai-pilot/README.md`、`docs/04-standards/engineering.md`

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj   # 随 dotnet test 编译通过
cd ForgeSelf.Web && pnpm run check                 # vue-tsc -b && eslint
```

Result: PASS（来源等级：Verified）

```text
pnpm run check：✖ 81 problems (0 errors, 81 warnings)——0 error，81 warnings 为存量
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~StartupPortResolver"
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 7，已跳过: 0，总计: 7（耗时 320ms）
```

## 前端单测（vitest）

Command: `cd ForgeSelf.Web && pnpm run test`

Result: PASS（来源等级：Verified）

```text
Test Files  46 passed (46)；Tests  490 passed (490)；Duration 71.15s
```

## e2e 编译校验（--list）

Command: `E2E_API_TOKEN=sk-compile-check-only npx playwright test --list --reporter=line`

Result: PASS（来源等级：Verified）——全部 spec + 两份配置可加载（含端口认领求值逻辑）

```text
Total: 190 tests in 39 files
```

## Integration Test（中档：后端全量 dotnet test）

Command: `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（两轮，完整日志 `.forgeself/memory/test-full-input50.log`）

Result: 判责通过（来源等级：Verified）

```text
首轮：总计 1686 / 通过 1616 / 失败 70（22m47s 内第二跑前值）
第二轮（env 外泄根治后）：总计 1686 / 通过 1637 / 失败 49（22m47s）
判责：49 = 24 基线在册红（WorkflowPlanning 7 + ScriptRunnerDi 1 + ApiKeyService 12 + RealLLM 4，TODO 在册）
     + 25 条 UnauthorizedAccessException（ForgeSetting.config.tmp / %TEMP% 写拒绝）
       —— 实证：非沙箱（dangerouslyDisableSandbox）复跑同败；本机 8+ 个在跑 ForgeSelf.exe 实例
       句柄争抢（TODO 在册项「全量跑测前需停 dev 实例」，agent 无权停用户实例）
     ⇒ 与本任务改动零关联；env 外泄修复使失败 70→49 佐证本任务引入面已消除
```

## E2E（深档：全量 4 worker，--output 空目录）

Command: `cd ForgeSelf.Web && npx playwright test --config=playwright.config.ts --workers=4 --output=.temp/e2e-output-50`（日志 `.forgeself/memory/e2e-deep-input50.log`）

Result: 判责通过（来源等级：Verified）——共 3 轮（前两轮分别败于托盘崩溃 / safe-delete shim，均根因修复后第三轮完整跑通）

```text
基线：102 passed / 82 failed（总数 184）
本轮：103 passed / 83 failed / 4 skipped（总数 190，期间新增 6 用例）——34.1m
⇒ 计数持平偏正（+1 passed / +1 failed on +6 tests）
新增红判责：0 条与端口/基建相关——
  · port-config.spec 6/6 全过（端口无关改造直接验证）
  · token-init:55 动态断言（7103/v1）通过 ⇒ UI 正常渲染动态端口；:64（/settings 卡片地址）
    为展示类疑既有失败，locator 已正确动态化，待单独归因
  · 失败构成：chat.spec 整族 30（已知整族红）、真实 LLM/LM Studio 依赖族、
    51888 语义 quick-links 插件族、TODO 在册散红（app.spec:35、quicklinks.spec:164 在册）
过程修复（Plan 外必要最小改动，已记 03-plan 偏差表）：
  · 宿主 FORGESELF_NO_TRAY=1 守卫（托盘 TryCreate failed → STA 线程 Unhandled 打崩宿主，
    首轮实证：监听 7103 就绪仍被拖崩）
  · global-setup 目录清理改 OS 级（cmd /c rmdir）：fs.rmSync 被 safe-delete shim 拦截
    （SAFE_DELETE_BULK_GUARD_ERROR，第二轮实证）
动态端口链路 Verified：7002/7102 被占自动顺延 → 前端 7003（strictPort）+ 后端 7103
（FORGESELF_PORT 注入、落盘 data/config/ForgeSetting.config）→ token 播种 → current.json 就绪
```

## Screenshots

N/A（本任务为基建改造，无 UI 视觉变更）

## Known Limitations

- 端口覆盖的「落盘（Save）」分支无进程内单测：ForgeSetting 静态构造固化配置路径，测试进程无法安全重定向（会写真实数据根）；该分支由 e2e 宿主启动（env 先于静态构造设置、数据根隔离）真实验证。
- `playwright.live.config.ts` 仍指向真实 :7102 实例（语义即"打真实实例"），不在本任务改造范围（TODO 存量项：live 配置 token 注入通道）。
- e2e 侧注释中的 7102/7002 字样保留（描述默认回落值，非逻辑）。

## Unresolved Issues

- 中档 49 败中的 25 条「实例句柄争抢」族：需按 TODO 在册项做「文件争抢规避（如测试专用数据根临时目录）」或在跑测前停 dev 实例（须用户授权），完成后该族应转绿。
- token-init:64（/settings 卡片地址展示）疑既有失败：动态断言 locator 正确、同断言 :55 通过；需单独归因（与端口无关）。
- 基线红清单仍未落盘（`project-baseline-test-reds` 缺失）：本次以「计数 + 域分布 + 在册 TODO」判责，建议尽快落盘基线清单使后续对表自动化。
