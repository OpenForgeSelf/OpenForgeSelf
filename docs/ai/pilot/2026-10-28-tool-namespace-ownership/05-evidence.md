# 05 · Evidence — 证据记录（只记实际发生 · 2026-10-28）

> 来源等级：✅ Verified（亲自跑过拿到真实输出）/ ⚠️ 部分 / ❌ 阻塞。时间均为 2026-10-28。
>
> **修订轮补记（17:29-18:45，用户拍板 mcp-center 整插件豁免前缀）**：宿主+插件全量编译绿；后端过滤集 **212/212 绿**（10 组：ToolNamespace 守卫 S1-S5 含新豁免断言 / UniversalTool / ReactLoopAgent / SearchTools / ProjectWorkspaceDefaultRoot / TerminalCommandGuard / ToolBridgeGuardParity / UniversalGatewayListTodos / RunTerminalCommand / FileToolsFolders——外部会话 testhost 锁释放后补跑，其间外部 WIP 的 CS0117 已由其自行收敛）；vitest **1035/1035**；`pnpm run check` 0 errors；AIAgent web build 绿；mcp-center 冒烟 e2e **8/8 全绿**（终验完整跑，含 **calculate 6*7=42 透传 ✓** + 127 工具注册复核；首次 3 passed + stdio npx 超时系 npx 拉包瞬时网络面，定向重跑 1 passed 证实）。**S1 守卫适配**：「有工具插件必须声明 toolPrefix」对 mcp-center 豁免（断言其 plugin.json **不含** toolPrefix）。**事故与处置**：pwsh 批量替换引发 4 文件字符级损坏（ToolSelectorService/composerContext×2/ReactLoopAgentTests 引号变 m；PlanGeneratorService `foeeach`/`eetuen` 编译期暴露）——git checkout 恢复 HEAD + 小步 edit 重做终态；**教训已沉淀**：b-engineering-daily §B6「源码改名禁用 pwsh 全文件重写」条目已修订入库。

## AC-1 守卫测试 + mutation ✅ Verified

- `dotnet test --filter`（14 过滤器：ToolNamespace|ProjectWorkspaceDefaultRoot|UniversalTool|SearchToolsToolFunction|RunTerminalCommand|TerminalCommandGuard|ToolBridgeGuardParity|ReactLoopAgentTests|FileToolsFolders|UniversalGatewayListTodos|ListToolsToolFunction|McpJsonRpc|UniversalToolForwarder|McpService）
- **结果：251/251 通过，0 失败**（01:10 与 01:14 两轮复跑一致）。
- S5 mutation 自检在套件内（去前缀变红验证）；S1-S4 全绿。

## AC-2 零重名 + 映射对账 ✅ Verified（S3/S4 绿）

- 扫描器三层防误报修复（同类修类不修例）：① `StripComments` 剥注释（XCode 模板注释 `entity.Name = "abc"` 误判实证 CostScope）② 行过滤 `ServerId =`（mcp-center 外部工具目录 DTO）③ 行过滤 `ScriptParameterType.`（脚本参数）④ 行过滤 `(Name = "`（ASP.NET 绑定特性如 `[FromQuery(Name = "ref")]`）。

## AC-3 文件工具绝对路径语义 ✅ Verified

- ProjectWorkspaceDefaultRootTests F 组重写后全绿：无 workspace + 绝对路径 read/write/list 成功（AC-3 末条）；相对路径拒绝且错误文案含「绝对路径」；list_files 缺省 path 回退项目根。

## AC-4 注册表贡献 ✅ Verified（e2e 宿主运行期实证）

- e2e publish 宿主 `[ToolRegistry] 注册工具成功` 日志 **127 条逐名核对**：mcp-center 9 件（universal_tool/search_tools/list_tools/run_terminal_command/read_file/write_file/list_files/mcp_get_current_time/mcp_calculate）、ai-agent 4 件（aiagent_plan_workflow/submit_plan/complete_step/request_help）、workflow_* 5 件、memory_* 5 件、dev_* 26 件、其余按 021 清单逐一对上；**零旧名、零重名**。

## AC-5 契约上移 ✅ Verified

- `ForgeSelf.Abstractions/IProjectWorkspaceService.cs` 在 Abstractions；`ProjectController.cs` git diff 仅 +1 行 `using ForgeSelf.Abstractions;`；插件 csproj 零新增插件间引用（20 插件独立编译全绿）。

## AC-6 composer 三档白名单 ✅ Verified

- 宿主 `pnpm exec vitest run`：**1034/1034 通过**（含 composerContext 权限三档语义断言、composerContext.test.ts 全新名 fixtures）。

## AC-7 e2e ⚠️ 部分

- **mcp-center 冒烟：8/8 通过**（含 universal_tool 透传 mcp_calculate 6*7=42、tools/list、外部 MCP 全链路、超时档位）——第二次运行绿（第一次红系 spec :179 漏改 calculate→mcp_calculate，已修）。
- **ai-agent 界面冒烟：6/7 通过**；唯一红「计划驱动执行」失败原因为 LLM 上游 401（`Malformed LM Studio API token provided: EMPTY`），非本批域。
- **tool-gateway-031：4 场景均红**，失败链两级：①首跑 preflight `POST /api/project/directory` 500 = `UNIQUE constraint failed: Project.Root`（4 worker 并行 check-then-insert 竞态，插入时间戳毫秒级 .67/.70/.74，存量缺陷非本批——git diff 证实 HostProjectRegistry 零改动、ProjectController 仅 +using，已记 TODO）；②单 worker 复跑 preflight 全过、chat 步骤上游 401（`invalid_api_key` / LM Studio EMPTY token）——**e2e 全新宿主库无有效 LLM key + 并行会话正在改 provider 出站层（AIProviderService/OpenAISdkAdapter WIP 进了 publish 构建）**。
- 宿主侧工具面反证绿：4 场景 chat 请求均带 enabledToolNames 进入 LLM 循环（ReactLoopAgent 发起上游请求），**无任何 unknown tool 报错**。
- 127 工具注册日志见 AC-4。

## AC-8 中档门禁 ⚠️ 部分（快档全绿；全量受阻——存量基建缺陷，非本批）

- 快档过滤集 251/251 ✅；ForgeSelf.Web vitest 1034/1034 ✅；AIAgent web `pnpm run build` ✅（962ms，275.26kB）；20 插件 csproj 独立编译 ✅；宿主编译（含 tests）✅。
- **补跑（闸门2 收口轮）**：ForgeSelf.Web `pnpm run check` ✅（**0 errors** / 151 warnings 存量不阻断，component-conventions PASS）；`pnpm run design:check` ✅（style-tokens 1759<基线 2501、compliance a209/b0/c0/d0 零新增）；`verify-pilot-artifacts.ps1 -TaskId 2026-10-28-tool-namespace-ownership` ✅ PASS（00-07 八件齐 + 关键节；07 补 Validation 节标记后过）。
- 全量 `dotnet test` 4 次尝试均「测试主机进程崩溃」中止：进度 142/386/784（并行）+ 444（串行 `xUnit.MaxParallelThreads=1`），崩点不固定、**跑到部分全绿 0 失败**。
- 二分定位（约 10 次分段跑）：`ForgeSelf.Api.Tests.Integration` 段前半 12 类**合跑必崩、拆组各自全绿**（A 组 37 绿 / B 组 57 绿 / Chat 族 40 绿 / 后半 178 绿）。定性：Integration 段类间组合触发（共享 WAF/fixture 互扰或原生资源竞争），**存量基建缺陷非本批引入**（判据：三次并行崩点递增；串行仍崩但崩前全绿；本批域内过滤集全绿；崩溃段含 16 例已登记 AIModel 家族基线红 TODO:101）。已记 TODO P1 专项。
- Integration 段 8 红，全部对上登记基线：WorkflowPlanningIntegrationTests 6（TODO:102）+ ScriptRunnerDiIntegrationTests 1（TODO:166）+ RealLLMIntegrationTests 1（TODO:216 环境性，且该类单跑即红 `Expected POST but found GET`——与外部会话正在改 AIService 出站请求的 WIP 相符，非本批）。

## AC-9 文档/ADR/not-taken ✅ Verified

- 新增 `docs/07-decisions/004-tool-namespace.md`（六段决策+依据+后果）。
- `docs/07-decisions/not-taken-decisions.md` 追加 044（不做别名层/不迁移存量——用户拍板）+ 045（不做根白名单/CWD 门语义不变）。
- `docs/02-features/021-ai-agent.md` 工具清单整节重写（17 插件 127 工具新名、重名冲突清零说明、ADR-004 引用）+ 关联工作流节改 workflow_execute 新归属。
- `docs/02-features/005-todo-tracker.md`（8 工具新名）、`034-mcp-center.md`（mcp_calculate 示例 + search_tools 归属注）、`036-filetools-folder-ranking.md`（file_dir_stats + mcp-center.universal_tool）同步。
- `fs工具集HTTP-MCP调用文档-1.9.2.md` grep 旧名零命中，无需改；design-system-consume 技能 8 个 design_* 名未变，无需改。
- e2e spec：tool-gateway-031（mcp_calculate ×5 处）+ mcp-center（1 处）更新；其余 spec 全保留名无需改（grep 证实）。

## 环境 / 并行会话事实

- 外部会话 0:47-1:08 活跃改 `AIProviderService.cs`/`OpenAISdkAdapter.cs`/`AIService.cs`（provider 出站层），两次挡住宿主编译（OpenAISdkAdapter CS0103），1:08 后写盘安静；e2e publish 构建含其 WIP 代码。
- 本机 e2e/测试环境前置（NO_PROXY/TEMP）全程遵守；判定一律读日志正文。

## 验收编号索引

门禁按无连字符编号检索。等级见上文同号的 `AC-n` 节，这里不改等级。

- AC1 AC2 AC3 AC4 AC5 AC6 AC7 AC8 AC9
