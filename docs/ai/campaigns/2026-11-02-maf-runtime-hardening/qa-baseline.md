# qa-baseline — B2/B3 合入前基线（2026-11-02-maf-runtime-hardening）

> 采集人：qa-gate｜采集时间：2026-11-02 21:07–21:50 +08:00（Asia/Shanghai）
> 作用：战役批 B2（结果硬化）/B3（任务中心）合入前的三条门禁基线。**此后每批收口判责：不在本清单内的红 = 新改动引入；在本清单内的红 = 基线红，不归属新改动。**
> 判据纪律（AGENTS §5.0）：一律取日志正文，不拿 exit code 当证据。

## 0. 环境与构建态说明（必读）

- 环境前置已执行：`NO_PROXY`/`no_proxy='localhost,127.0.0.1,::1'`；`TEMP`/`TMP`=`<repo>\.temp\tmp`。
- **dev-stack 全程未触碰**：:7301（.NET Host pid 76080）/ :7399（pid 7476）运行中；:51888 用户实例未动。
- **构建态**：`dotnet test --no-build`，基于 `ForgeSelf.Api.Tests\bin\Debug\net10.0-windows`（测试构建时间 **2026-11-02 20:07:04**）。
  - HEAD = **07d314b**（2026-11-02 21:06:11 提交，走查批）；工作区除 3 个未跟踪 docs/pilot 目录外**零改动**。
  - 最新真实源码 = `ForgeSelf.Api.Tests\Integration\MemoryTimestampGuardTests.cs`（20:03:34）**早于**构建时间 20:07:04 ⇒ **该构建恰好完整覆盖 HEAD 07d314b 的全部源码**，基线与待合入前源码同源。
- **DLL 锁事故（按预案处置）**：21:10 首跑 `dotnet test`（带构建）撞 **MSB3027**——`ForgeSelf.Core.dll` 被 ".NET Host (76080)"（dev-stack :7301）锁定，重试 10 次失败。未停 dev-stack，改跑 `--no-build`。锁证据日志：`.temp/tmp/qa-baseline-dotnet-buildlock-MSB3027.log`（413KB）。
  - 副产物登记：失败构建使 `ForgeSelf.Api\bin\Debug\net10.0-windows\ForgeSelf.Api.dll` 暂缺（bin 残缺）；宿主已载入内存不受影响，下次成功构建自愈。
- **OOM 死亡事故与内存回收**：21:12 首次 `--no-build` 跑测的 testhost（pid 39208）于 21:24:12 静默死亡（死前 CPU 454s 真实工作中、无汇总、无 .NET 崩溃事件；机器空闲内存当时仅 1.6GB/31.4GB=5%，且与 verify-dev 的 b2-build-probe 构建并发）。lead 于 21:2x 执行内存回收（僵尸 testhost 自然消失 + `dotnet build-server shutdown` 清 22 个闲置 MSBuild 节点，空闲内存 1.6→2.6GB）后，21:26:58 以脱树分离 runner（`Start-Process`，runner pid 138252 → testhost 13688）重跑成功。WMI 拉起方式曾因存储不足被拒（Win32_Process Create ReturnValue=9）。

## 1. 三条基线数字

| # | 门禁 | 命令 | 结果（日志正文） | 日志 |
|---|------|------|------------------|------|
| 1 | 后端全量 | `ForgeSelf.Api.Tests`: `dotnet test --no-build --nologo -v q` | **总计 3372 / 通过 3283 / 失败 89 / 跳过 0**（12m19s，21:26:58–21:39:19，runner exit=1） | `.temp/tmp/qa-baseline-dotnet.log` |
| 2 | 前端 check | `ForgeSelf.Web`: `pnpm run check`（vue-tsc -b + eslint + 组件守卫） | **vue-tsc 0 错误；ESLint 0 error / 153 warnings（全部存量 lint 风格项）；组件规范守卫 PASS**（story 4 / ct.spec 4） | `.temp/tmp/qa-baseline-web-check.log` |
| 3 | 前端 test | `ForgeSelf.Web`: `pnpm run test`（vitest，CI 模式） | **90 文件 / 1042 通过 / 0 失败**（14.72s） | `.temp/tmp/qa-baseline-web-test.log` |

汇总原文（后端，日志末行，ASCII 数字可靠）：`失败: 89，通过: 3283，已跳过: 0，总计: 3372，持续时间: 12 m 19 s - ForgeSelf.Api.Tests.dll (net10.0)`。

## 2. 基线红清单（后端 89 条 · 按族归类 + 隔离复跑定性）

> 逐条清单（89 条全量）见 `.temp/tmp/qa-baseline-redlist.txt`；原始日志为权威证据。
> ⚠️ 日志为混合编码（xUnit/VSTest 两段编码不一，AGENTS B6 同族坑）：清单内 ASCII 类名/方法骨架 100% 可靠，**中文用例名存在一处转码伪影**，以原始日志或归属批自证为准。
> 「隔离复跑」= 同一构建态 `dotnet test --no-build --filter <族>` 单独跑（探针日志 `.temp/tmp/qa-probe-*.log`）。

### 2.1 稳定红（隔离复跑仍红 · 归属明确 · 共 24 条）

| 族 | 数量 | 隔离复跑 | 归属判定 |
|---|---|---|---|
| `Plugins.AIAgentTests.ProjectWorkspaceDefaultRootTests` | 15 | 未复跑（TODO 已登记「单独跑该类即红」，断言与实现不符） | **基线红**：TODO P2 已登记，归属 68df669 批会话（2026-10-30 入库）——修断言或修实现，归属批处理 |
| `WorkflowPlanningIntegrationTests` | 6 | **仍 6/7 红** | **基线红**：截至 07d314b 的存量稳定红，无既有登记条目，需归属批修复 |
| `ScriptRunnerDiIntegrationTests.GetRuntimes_…` | 1 | **仍 1/1 红** | 同上，存量稳定红 |
| `ProblemClassRegistryTests`（2 条） | 2 | **仍 2 红** | **基线红（随 07d314b 走查批入库，易修）**：错误原文——① `cases/README.md 索引缺 case 行: 2026-11-02-memory-timestamp-minvalue`；② `case 2026-11-02-memory-timestamp-minvalue 的 front-matter id '' 与文件名不一致`。恰为 TODO 已登记「new-problem-case.ps1 漏写 cases/README 索引行」缺陷（PILOT-073 发现）的**二次复发实证**（docs-only 修复：补索引行 + 补 front-matter id） |

### 2.2 环境红（隔离复跑全绿 · 全量类序/数据根污染 · PC-014 族 · 共 64 条）+ flake 1 条

| 族 | 全量红 | 隔离复跑 | 判定 |
|---|---|---|---|
| `Plugins.TodoTracker`（DispatchFlow 22 + BuiltInDispatch 6 + AgentStatusBatch 4） | 32 | **35/35 绿** | 环境红。注：TODO 曾登记其中 2 条盘符大小写红（TodoDispatchFlowTests:98/:135），**本次隔离跑已绿**，属环境漂移型 |
| `Plugins.ImGatewayTests`（ControllerQuery 7 + MessageStore 7 + RouterRecord 4） | 18 | 未复跑（PC-014 族已登记「no such table」数据根环境红，本次同插件 DbConfigStore 隔离绿佐证） | 环境红（PC-014 族） |
| `Integration.TodosControllerTests` | 7 | **18/18 绿** | 环境红 |
| `ImGateway.DbConfigStoreTests` | 4 | **4/4 绿** | 环境红 |
| `Integration.MemoryTimestampGuardTests` | 3 | **3/3 绿**（1s） | 环境红。该守卫为今日走查批（07d314b）新增，TDD 红灯证据在案（3 失败→26 通过），全量红属 PC-014 族污染，代码健康 |
| `Unit.ScriptRunnerTests.CancelAsync_ForRunningScript` | 1 | 未复跑（TODO 已登记竞速 flake，单跑复绿先例） | flake（竞速，5s 窗口） |

**合计**：稳定红 24 + 环境/flake 红 65 = **89**（与汇总行完全一致）。

### 2.3 前端基线红

- check：0 error；153 条存量 ESLint warning（`vue/max-attributes-per-line`、`vue/html-quotes` 等，集中于个别 .vue 文件）——**新增 error/新 warning 文件才判新引入**。
- test：无失败用例，基线红清单为空。

## 3. 判责对表规则（每批收口用）

1. 后端红：先比对 §2 的 89 条清单（按「命名空间.类.方法」全名对表；中文用例名按类级族+计数对表）——**清单外的新红 = 新改动引入，出真实报错原文 + 归属**。
2. 前端 check：新增 error 或新增 warning 文件 = 新引入（153 条存量 warning 文件清单见 check 日志）。
3. 前端 test：任何失败 = 新引入（基线 0 失败）。
4. 环境/lock/OOM 类假红先按 §5.0 排环境（NO_PROXY/TEMP/日志正文）再判责；本机 DLL 锁与 OOM 应对见 §0。

## 4. 遗留与移交

- ⬜ **registry 2 红易修**（§2.1）：补 `docs/11-troubleshooting/cases/README.md` 索引行 + case front-matter id——已登记 TODO（P2），建议随最近一批 docs 收口顺手修。
- 测试基建（PC-014 族 64 条环境红 + 39 处手搓 AddConnStr）已有专项批 TODO 在案，不在战役批内处理。
- 本文档与 `.temp/tmp/qa-baseline-*.log`、`qa-probe-*.log` 为基线证据链；`.temp` 不入库，归档时由 lead 决定是否将 redlist 摘要转存 docs。
