# 05 证据（Evidence）

> 任务：`2026-10-03-llm-observability`（PILOT-033 LLM 可观测性 / 可视化成本观测）
> 本件只收录**可复现的实测读数**。凡未跑过的，一律写「未验证」，不写「应该没问题」。

## 一、证据等级定义

| 等级 | 含义 |
|---|---|
| **Verified** | 本轮实际执行命令并读到数字，读数可按给出的命令复现 |
| Reported | 来自上游/第三方或前序任务转述，本轮未复核 |
| **未验证** | 本轮**没有**跑过（不得作为完成依据） |

## 二、逐原子任务验证读数（全部为 **Verified**）

| 任务 | 验证内容 | 读数 | 证据文件 |
|---|---|---|---|
| A1 | 遥测契约（只读接缝 + DTO） | 契约测试 **6/6** | `.temp/a3*-test*.log` |
| A2 | 插件骨架（plugin.json / Model.xml / 宿主库隔离） | 插件构建 **0 错误** | — |
| A3 | 计价纯函数（BR-3/BR-2/BR-4 锁死） | 单测 **52/52**（含 A3b 宿主取数 **13/13**） | `.temp/a3*-test*.log` |
| A4 | 模型→供应商四级解析 | 随聚合层 **17/17** | `.temp/a7-test3.log` |
| A5 | 单价目录 CRUD（Save/Update 双通道） | 随回归 **51/51** | `.temp/a7-test3.log` |
| A6 | 预算规则 + 达成率 | 随回归 **51/51** | `.temp/a7-test3.log` |
| A7 | 只读聚合 + FR-3.7 惰性物化 | **36/36**（聚合 24 + 物化 12） | `.temp/a7b-test3.log` |
| A10 | trace 关联 + waterfall（插件侧） | **19/19**（trace 13 + 契约 6） | `.temp/a10-test3.log` |
| A11 | 18 端点 + 鉴权/错误纪律 | **31/31** | `.temp/a11-test6.log` |
| A12 | 前端资产守卫（端点边界/契约对齐/诚实性） | **9/9** | `.temp/a12-test3.log` |

**取消项**：A8（Usage 接缝迁移，U-2 裁决不做）、A9（宿主加列，用户裁决不做）、A13（`dsh-ui-bundle` 与本 pilot 无关，用户澄清取消）。

## 三、全量回归演进（每一轮都跑，数字只增不减）

| 时点 | 命令 | 读数 |
|---|---|---|
| A5 后 | `--filter CostScopeTests｜TurnTelemetryQueryService` | **121/121** |
| A6 后 | 同上 | **149/149** |
| A7 收口 | 同上 | **161/161** |
| A10 后 | 同上 | **180/180** |
| A11 后 | 同上 | **211/211** |
| A12 后 | 同上 | **220/220** |
| **代码审查修复后（终态）** | 同上 | **227/227**（基线 220，净增 7 条回归测试） |

## 四、反向探针记录（判定标准＝**改动前后测试结果变化**，不是「代码写上了」）

| 探针 | 注入的缺陷 | 实红条数 | 证据 |
|---|---|---|---|
| A5 | 重复新增时静默覆盖既有配置 | **实红 2 条** | `.temp/a5-mutation.log` |
| A7 ① | 未配单价返回「非下界」（= 静默计 0 元，BR-2 明禁） | **实红 2 条** | `.temp/a7-mutation.log` |
| A7 ② | 让已结束日也重算（= 废掉缓存） | **实红 2 条** | `.temp/a7b-mutation.log` |
| A10 | 关联判定短路（「什么轮次都算关联」） | **实红 5 条** | `.temp/a10-mutation.log` |
| A11 | 非法/空 `by=` 也放行（= 静默按默认维度返回） | **实红 3 条** | `.temp/a11-mutation.log` |
| A12 | `http.ts` 真的调一次 `/api/usage-stats` | **实红 1 条** | `.temp/a12-mutation.log` |
| 审查 P0 | `PUT /budgets` 退回新增通道 | **实红 1 条** | `.temp/review-mutation.log` |

全部探针均已还原并复绿。

## 五、构建证据

| 目标 | 命令 | 读数 |
|---|---|---|
| CostScope 插件 | `dotnet build Plugins/CostScope/CostScope.csproj` | **0 错误** |
| 宿主（含全部被引用插件） | `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` | **0 错误** |
| 契约项目 | `dotnet test ForgeSelf.Abstractions.Tests` | **30/30** |
| 前端类型检查 | `cd Plugins/CostScope/web && pnpm check`（vue-tsc） | **0 错误** |
| 前端构建 | `pnpm build` | `dist/index.js` 27.65 kB + `dist/style.css` 4.63 kB |

前端产物实测复核：含 `export { st as CostScopeView, st as default }`（契约要求的具名 + 默认导出）；保留裸 `from "vue"`（external 生效，无 Vue 双实例）。

## 六、**未验证**的部分（如实列出，不得当作已通过）

| 项 | 为什么未验证 |
|---|---|
| 端点 **401 真实链路** | A11 全部为「直接实例化控制器」单测，未起 Web 主机；类级 `[Authorize]` 的存在性仅由**反射**守住 |
| **e2e / 真实 HTTP** | 本轮没有发出一条真实 HTTP 请求；路由、模型绑定、序列化均未验证 |
| 界面运行时 | `pnpm check` + `pnpm build` 通过，但**未在浏览器里跑过**（无 Playwright 走查） |
| trace 真实数据 | `IAgentRunTelemetryProvider` 的实现方在 AIAgent 插件侧，**尚未实现** ⇒ trace 只能靠入参单测 |
| 根 solution 构建 | **当前被并行会话弄坏**：`Plugins/McpCenter/Services/DshMcpConfigWriter.cs` 9 个错误（`DshMcpConfigDto` 尚缺 `EntryId`/`ServerName`/`Transport`/`Url`/`EntryExists`）。**非本任务引入**，本任务的插件与宿主构建单独验证均为 0 错误 |
