# AI-Native Pilot Result（最终汇报）

> 任务：sems 插件自洽化 + 经 MCP 中心对外提供 13 个工具｜闸门2 状态：**待用户验收**（截至 2026-09-30 未提交、未 push）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：

- 宿主 `ForgeSelf.Api`（.NET 10 + NewLife.XCode + SQLite）与插件 `Plugins/Sems`（id=sems，route=/sems）分层；插件数据不自治，项目/命令真源在宿主 L1 接缝 `IProjectRegistry`（`ForgeSelf.Abstractions`）。
- 插件侧 `ProjectService` 原为只读消费方，写操作（登记/更新/移除、命令 CRUD、目录浏览）此前只存在于 AIAgent 会话链路，插件内部不自洽。
- MCP 对外面经宿主 `ToolRegistry` + `McpCenter` 的 `universal_tool` 单工具转发；`IToolFunctionExtension` 由插件 `Apply(IContext)` 注册。
- 关键内核事实（实测）：`ForgeSelf.Core.Context.GetService` 只查本地/共享服务表，**不会**转发到宿主 MS DI 容器；宿主由 `PluginManager.ProvideHostServices` 把根 `IServiceProvider` seed 进 root Context——这是插件工具拿宿主服务的唯一正规接缝。
- e2e 体系：`ForgeSelf.Web/playwright.config.ts` + `e2e/global-setup.ts`（真实宿主、零 mock、动态端口自 PILOT-050），插件层用例落 `e2e/plugins/<id>/`。

## 2. Selected Task

- 让 sems 插件**内部自洽**：添加项目/编辑/移除、命令增删改、启停与检查全在插件内完成，不再把用户支到其他页面。
- 让 sems 能力**经 MCP 中心对外**：13 个 `sems_*` 工具注册到宿主 ToolRegistry，可被 `universal_tool` 转发调用。
- 走 AI-Native 九阶段闭环并留下 00–07 工件链（本任务按用户批准走**全量**档）。

## 3. Changed Files

后端/契约（详见 05-evidence.md「Changed Files」）：

- `ForgeSelf.Abstractions/IProjectRegistry.cs`、`ForgeSelf.Api/Services/HostProjectRegistry.cs`、`ForgeSelf.Api/Entities/RunCommand.Biz.cs`
- `Plugins/Sems/Services/ProjectService.cs`（一处真相重写）、`Controllers/{Projects,ProjectCommands,Runs}Controller.cs`、删除 `Controllers/RunnerController.cs`
- `Plugins/Sems/ToolExtensions.cs`（新，13 工具 + 基类异常兜底/用量上报）、`SemsPlugin.cs`、`plugin.json`（1.0.3 → 1.1.0）

前端 `Plugins/Sems/web/`：`SemsView.vue`、`ProjectCard.vue`、`CommandList.vue`、`RunPanel.vue`、`http.ts`、`types.ts`、新增 `DirectoryPickerDialog.vue`、`confirmOps.ts`(+spec)、`package.json`

测试：`ForgeSelf.Api.Tests/Plugins/Sems/{ProjectServiceTests,SemsControllerTests,SemsToolExtensionTests}.cs`（新）、`Services/HostProjectRegistryTests.cs`（+6 例）、`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`（重写为 4 用例）

文档：`docs/02-features/028-project-workspace.md`、`docs/07-decisions/not-taken-decisions.md`（015–020）、本目录 00–07、`docs/04-standards/agent-workflow.md`（B1 同步纪律）、`.agents/skills/plugin-publish-verify/SKILL.md`

宿主其他插件与其他人代码：**未改**（McpCenter/AIAgent/宿主前端 src 零改动）。

## 4. Validation

Build:

- `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` → **0 错误**（849/823 存量警告，未新增）。在同步后基线 `b357a32` 复跑仍 0 错误。（Verified）

Unit Test:

- `dotnet test --filter "FullyQualifiedName~Sems|FullyQualifiedName~HostProjectRegistry"`（独占）→ **失败 0 / 通过 86 / 总计 86**，1m2s。（Verified）
- 宿主前端 `pnpm run test` → **46 files / 490 tests 全绿**；插件前端 vitest（confirmOps）8/8 含「取消→零请求」。（Verified）
- 全量 `dotnet test` → 1717 通过 / **17 失败** / 1734；17 红逐条归属=批次E 8 + 上游 dsh 自记录存量 7 + 负载相关 subprocess 类 2（后者在独占跑 86/86 全绿，定性 flake）。无一落在本任务代码路径。（Verified）

E2E:

- `e2e/plugins/sems/sems.spec.ts` → **4 passed**：① 远程加载冒烟 + v1.1.0 徽标；② UI 全生命周期（选目录→登记→加命令→启动→停止→取消移除→确认移除）；③ 经 McpCenter 网关真调 13 工具（枚举/登记/列表/命令 CRUD/必填拒绝/已删命令报错/移除后磁盘仍在）；④ 无令牌 401 矩阵。
- 复跑两次：旧基线（`78d065c`，1.5m）与同步后新动态端口基建（`b357a32`，1.7m，MCP 端口 19352、运行目录 `wt-ae077d4f`）。均零 mock 打真实后端。（Verified）

`pnpm run check`：本任务引入的 1 处 error（`sems.spec.ts:171`）已修；剩余 1 error 属上游 `e2e/global-setup.ts:183`（`preserve-caught-error`）。（Verified）

## 5. Evidence

- 全量证据在 `05-evidence.md`：Build / Unit / Integration / E2E（含 7 条迭代根因链）/ Regression / Static / Publish（运行实例侧载）/ Post-Sync Re-Verification / Known Limitations。
- 截图：`ForgeSelf.Web/screenshots/e2e/sems/`（sems.png、sems-after.png、sems-picker.png、sems-running.png、sems-remove-confirm.png、sems-removed.png）；e2e 取证日志与 `.temp/e2e/wt-ae077d4f/`。
- 运行实例证据：`D:\src\tools\ForgeSelf\plugins\Sems\versions\{1.0.3,1.1.0}` + `current`；指针热切前端实测（40184↔30220 字节）。

## 6. Review

- `06-review.md` 八问齐备，Final Decision：**APPROVED**（技术层面），**交付闸门2 仍待用户验收**。
- 范围核对：改动全部落在 sems + 一个宿主接缝 + 测试/文档；宿主与其他插件零改动；无数据库结构变更。

## 7. Risk

L1（低）。理由：数据落宿主既有表且未改结构；新增端点/工具都带 `[Authorize("ApiKeyPolicy")]`；不提供任意脚本执行；插件多版本目录保留 1.0.3 回滚位。

残余风险（须在验收时知悉）：

1. 运行实例 `:51888` 目前 `current=1.0.3`：v1.1.0 产物已铺但未激活；激活前提是宿主侧本次接缝改动部署到位，否则登记/移除路径会 `MissingMethodException`。
2. `plugins/Sems/versions/1.1.0` 混有 `ForgeSelf.Abstractions.pdb`/`ForgeSelf.Core.pdb`（脚本过滤只匹配 `.dll`），不参与加载但违反「活动插件目录只放插件自身程序集」，需手工清。
3. 全量后端存在 17 个非本任务红（已逐条归属），交付给 CI 时需按基线对表。

## 8. Problems Found

本任务过程中定位并**修掉**的真实产品缺陷（都由 e2e/门禁实抓，非推测）：

1. 插件工具基类把宿主 `IServiceProvider` 当 `IServiceScopeFactory` 用 → MCP 真调必抛 `No service for type 'IServiceScopeFactory'`（修 `ToolExtensions.cs`，加回归测试）。
2. `ProjectService.GetProjects` 未逐项带 Commands → 项目统计/快捷访问/启动全部静默失效。
3. `HostProjectRegistry` 同 Root 重登记会覆写 Name → 手工命名被自动登记冲掉。
4. `SemsView.runOne` 与 `CommandList.run` **重复 POST** → 每次启动必 409，且成功路径的刷新永不执行（运行面板恒空）。
5. `SemsView` 每次后台刷新置 `loading=true` → 整网格重挂载 → 卡片展开态丢失。
6. e2e 侧缺口：sems 是全仓唯一未在 goto 前注入真实 token 的 UI spec（导致把鉴权问题误读成插件不渲染）。

同期发现但**未越界处理**（已入 TODO）：TodoTracker 同款 CreateScope 潜伏缺陷、AIAgent `browse-directories` 无后端、上游 `global-setup.ts` lint error、mcp-center e2e 212 套内 flake、`publish-plugin.ps1` 漏 `.pdb`、`install-git-hooks.ps1` 在 worktree 下失败。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

流程自评的两处失分（如实记）：① 闸门1 批准后我在 Implement 前未先建 e2e 红灯，三个真实缺陷里有两个是「写完才发现」，应更早把 e2e 立成验收清单；② 有两次把方案只写在聊天里未及时落盘路径，被用户追问。

## 10. 本次实验发现的最大流程问题

**「门禁绿」不等于「交付生效」**：本任务两次差点把"改了插件文件"当成"发布完成"——一次是侧载后 `current` 只热切前端、后端程序集仍旧版（半升级态），一次是同步上游 18 个提交后未复验就拿旧证据报绿。缺的是「运行实例只读复验 + 同步后第一道 build 读真实错误数」这两条硬动作，现已写入 `agent-workflow.md` B1 与 `plugin-publish-verify`。

## 11. 下一步建议

最值得再做一个实验：**把插件发布闭环做成机器可判**——在 `plugin-publish-verify` 里加一条「侧载后自动比对 `GET /api/plugin` 版本 == 前端资源指纹 == 期望版本」的脚本化检查（当前靠人记得去比字节数），凡不一致直接 FAIL。这样「半升级态」这类问题下次会在发布当场暴露，而不是等用户点页面报错。
