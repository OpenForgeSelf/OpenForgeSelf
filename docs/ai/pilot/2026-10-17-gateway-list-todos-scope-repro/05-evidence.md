# 05-evidence：通用网关 list_todos IServiceScopeFactory 复现（2026-10-17）

## Changed Files
| 文件 | 动作 |
| --- | --- |
| `ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeReproTests.cs` | 新增（唯一业务侧改动，纯测试） |
| `docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/mini-task.md` | 新增（本批工件） |
| `TODO.md` | 新增 2 条待办（复现批 P1 + 发现的基线红 P2） |
| `.forgeself/memory/2026-10-17.md` | 追加本输入拆解 |

未改任何 `Plugins/**`、`ForgeSelf.Api/**`、`ForgeSelf.Core/**` 业务代码。

## 验证记录（全部 Verified · 真跑）
1. **编译**：`dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`
   → **0 错误**（1458 警告均为存量 xUnit1031 等，非本批引入）。
   附带事实：2026-10-16 登记的 `ModelProxy.cs` 编译阻断已不存在（工作区内已被修复改动覆盖）。
2. **复现测试**：`dotnet test --no-build --filter "FullyQualifiedName~UniversalGatewayListTodosScopeReproTests"`
   → `已通过! - 失败: 0，通过: 1，总计: 1`（46ms）。
   含义：**复现成功**——该测试是 bug 特征化测试（断言当前 buggy 封套），绿 = 用户观察到的错误被稳定复刻：
   - 入参原文 `{"parameters":"{}","tool":"list_todos"}`；
   - 网关返回 `error` 与用户串**逐字相等**（FluentAssertions `.Be(ObservedError)` 锚定
     `No service for type 'Microsoft.Extensions.DependencyInjection.IServiceScopeFactory' has been registered.`）。
3. **相邻回归**：`dotnet test --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder"`
   → 160 通过 / **2 失败**（`TodoDispatchFlowTests.点关联应登记宿主档案并让任务拿到真实项目Id`、`正文写入一万两千字符应原样读回不被裁短`）。
   单独跑 `--filter ~TodoDispatchFlowTests`（本批测试不在选择集内）同样 2 失败 → **与本批无关的既有基线红**
   （断言写死小写盘符 `d:\…`，归一化返回 `D:\…`）；已记 TODO P2，本批不顺手修。

## 时序复刻证据（根因链 · Inferred→Verified 边界说明）
- 测试按 AppBuilder 真实顺序组装：Apply 期用插件子 Context 构造工具（eager `TodoToolProvider.Resolve` 恒 miss）→ Build 后才 seed 宿主 `IServiceProvider` → 请求期 `CreateScope()` 在 `ForgeSelf.Core.Context`（只解析本地值+共享表）上抛错 → 工具 catch 成 `{success:false,…}` → `ToolRegistry.InvokeToolBodyAsync`（ToolRegistry.cs:535-538 Success=true 原样透传）→ 网关原样回传。**测试通过即该链路在真实类型（Context/ToolRegistry/UniversalToolForwarder）上被证实**，非 mock 摆拍。
- 注意：本测试证明的是「该时序必然产生该错误封套」；线上实例是否恰因此起失败（而非其他路径产生同文错误）需修复批次用运行实例日志复核（宿主 `Log/` 中 `[TodoTracker] list_todos 执行失败` 警告行可作旁证）。

## Known Limitations / Unresolved
- 只复现未修复（用户指令范围）；修复方向候选：老三工具改运行期惰性解析（对齐 AgentHub `AgentHubDi.ResolveHost<T>` 范式）——交修复批次设计。
- 用户另附的「ABA001: 'IServiceProvider' cannot be resolved from root services within a service for 'BuildLocalAgent (L74-82)'」在本仓库**无对应符号**（全库检索 0 命中），疑为外部分析器/其他工程的诊断转述，本批以运行时错误原文为准；已留此记录供修复批次核对。
- ABA001 中的 `BuildLocalAgent` 不在本仓 → 若用户该线索来自 IDE 分析器，需用户提供出处文件。
