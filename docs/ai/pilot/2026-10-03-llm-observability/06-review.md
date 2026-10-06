# 06 评审（Review）

> 任务：`2026-10-03-llm-observability`
> 方法：**结论先行 + 逐条技术核对**。每条缺陷都落到具体文件与行为，修复后配回归测试 + 反向探针。

## 一、评审范围

| 范围 | 说明 |
|---|---|
| 在内 | 本 pilot 新增/修改的 C# 代码（契约、成本引擎、聚合、物化、trace、端点、插件前端配置） |
| 不在 | `ModelPriceResolver` 的逐行复核（属 A4 已验收范围）；并行会话的 ToolBridge / McpCenter / 发布脚本改动 |

## 二、缺陷清单（4 个真实缺陷，全部已修）

| 级别 | 缺陷 | 根因 | 修法 | 回归测试 |
|---|---|---|---|---|
| **P0** | `PUT /api/cost-scope/budgets` **必然 500**，编辑预算功能完全不可用 | `CostController.SafeUpdateBudget` 调的是**新增**通道 `BudgetService.Save`，而 `Save` 对「同名 + 内容不同」抛 `InvalidOperationException`；`Guard` 只捕获 `ArgumentException` ⇒ 异常冒泡成 500 | `BudgetService` 补 `Update` 通道（要求已存在、**不做 upsert**）；控制器改走 `Update`；`Guard` 增捕 `InvalidOperationException` ⇒ **409**（状态冲突是客户端可纠正的，不该伪装成 500） | `UpdateBudget_存在且内容不同_改成功_不返回500` |
| **P1** | **provider 作用域预算「已用」恒为 0** | `BudgetAchievement` 只用 `Breakdown(by: Model)` 建字典，却拿 provider 预算的 `Target`（**供应商名**）去查 | 按作用域选字典：`global` 取全额、`provider` 用 Provider 维度、`model` 用 Model 维度 | `BudgetAchievement_provider作用域_按供应商维度实算_不恒为0` |
| **P1** | **历史日的失败数在缓存路径上凭空消失** | `CostTurnDaySummary` **表里没有 `FailCount` 列**，`ToPoint` 只能填 0；而已结束日走缓存路径 | `Model.xml` 加 `FailCount` 列 + 重跑 `xcode` 重生成实体；`Upsert` 写入、`ToPoint` 还原 | `QueryWithCache_历史日的失败数从缓存还原_不静默丢成0` |
| **P2** | `unattributedModels` 语义混乱且制造噪音 | ① 把「没配单价」也塞进去（与 `unpricedModels` 双重计数，看不出到底缺什么）；② resolver 未接线时**全部模型**都进列 | 收窄为「有解析器但解析不出」；resolver 为 null 时不产出该列表 | `Overview_未接线供应商解析器_未归属列表为空_不制造噪音` + `Overview_有解析器但解析不出_计入未归属` |

### P0 的教训（最值得记的一条）

根因是**我给 prices 做了 Save/Update 拆分，却漏了 budgets** —— 而这正是**同一天刚写进 `plugin-development` 技能**的那个坑（「新增防误覆盖」与「改价」必须两个通道）。
⇒ **知识写进文档 ≠ 落地到代码**。同构路径必须逐个检查，不能因为「另一处已经改过了」就推定全改完。

## 三、修复过程中我自己引入的缺陷（如实记录，不隐藏）

| 缺陷 | 怎么被抓住 | 处置 |
|---|---|---|
| 第一版 `BudgetService.Update` 直接调 `ApplyTo`、**绕过全部校验** ⇒ 非法作用域/周期/限额可经 PUT 写进库 | 新写的 `Update_非法内容_抛入参异常` 变红（第一次修完立刻跑全量） | **不是补一个洞**，而是抽出共用 `Validate` 让 Save / Update 走同一份校验（根治，两通道口径必然一致） |
| 暂存 `ProjectReference` 时用脚本重写文件，`encoding='utf-8-sig'` 给原本**无 BOM** 的 `ForgeSelf.Api.Tests.csproj` 加上了 BOM | 提交后 `git show --stat` 出现「1 deletion」⇒ 逐文件 diff 核对时发现 | 追加一个还原提交（`bdd9246`），把字节变更还原为原状 |
| 端点供应商解析**每条记录**调一次 `Match`（未记忆化） | 评审读码时发现（非测试） | 加 `Dictionary` 按模型记忆化（本次已修） |

## 四、评审中对「测试有效性」的核查

- 每组守门测试都做了**反向探针**，实测「改动前后结果变化」；共 7 组探针，全部实红并复绿（见 05-evidence 第四节）。
- 发现并修掉三类「测试自身错误」：分位样本数期望写错；`Breakdown` 是维度排行榜（按成本降序）而非时序升序；静态扫描守卫扫到了**注释里**的说明文字（`IHostedService`、`api/usage` 各一次）⇒ 已加 `StripComments`。
- 修掉一个会挂死测试的缺陷：`RepoRoot()` 的 `while` 漏了 `dir = dir.Parent` ⇒ **死循环**，测试挂起 11 分钟才被察觉（用 `TaskStop` 终止）。

## 五、发现但本轮不改的（低优先，如实列出）

| 项 | 判断 |
|---|---|
| `turns` 端点返回 `AgentRunId`，但 A9 取消后**恒为 null** | 保留：这是契约字段、返回 null 是诚实的；删掉反而像在藏能力 |
| 宿主 30 个实体无 `Model.xml`（铁律 9/11 在宿主侧从未成立） | 既有技术债，不在本 pilot；实测重建不可行，已 ABORT 并记录 |
| BC-8「插件未启用返回『未启用』而非 404」 | 属宿主插件装载层职责，控制器内无法实现 |
| `CostModelPrice.Model` 缺 DB 级唯一索引 | FR-3.5 唯一性目前只在服务层保证；XCode 因索引非唯一只生成 `FindAllByModel` |

## 六、Final Decision

**APPROVED（附两项条件）**

评审结论：4 个真实缺陷（1 P0 / 2 P1 / 1 P2）已全部修复并配回归测试，修复过程中新引入的缺陷亦已处置；终态全量 **227/227**、契约 **30/30**、CostScope 与宿主构建 **0 错误**、前端 `vue-tsc` 0 错误 + 构建通过。无遗留 P0/P1。

附条件（不阻塞本次提交，但**必须在对外宣称可用前**解决）：
1. **端点 401 与真实 HTTP 链路未验证** —— 当前仅有反射守卫，须在宿主起运行时后补一条真实请求（e2e）复核。
2. **根 solution 构建当前被并行会话弄坏**（`Plugins/McpCenter/Services/DshMcpConfigWriter.cs` 9 个错误，非本任务引入）—— 须由该会话修复后补跑一次根构建。

**已知能力缺口（非缺陷，是范围与决策的结果）**：主聊天链路用量不可见（U-2 裁决不迁接缝）；trace 关联为近似推断（A9 取消，宿主不加外键）；`IAgentRunTelemetryProvider` 实现方在 AIAgent 插件侧、尚未接线。
