# 032 · CostScope — 可行性报告（Feasibility）

> 阶段：立项第 2 步 ｜ Task ID：PILOT-032 ｜ 日期：2026-09-28
> 结论先行：**可做，低风险**。唯一需要用户裁决的是「契约落位」一处架构决策（§2）。

---

## 1. 技术前提核对（逐个 Verified）

### 1.1 插件能否读到 `ChatTurn` 数据？ —— **能，但需一次契约/连接选择**

**事实**：插件只引用 `ForgeSelf.Core` + `ForgeSelf.Abstractions`，**不引用** `ForgeSelf.Api`
（`Plugins/Sems/Sems.csproj:32-33`、同构于 `Plugins/Sems` 全部插件）。
→ 插件**拿不到** `ForgeSelf.Api.Entities.ChatTurn` 这个 XCode 实体类。

**数据本身是可达的**：
- 宿主库 = `{数据根}/ForgeSelf.db`，单一连接名 `ForgeSelf`（`XCodeConfig.cs:16` `HostDbs = { "ForgeSelf" }`）。
- 插件能从 `IContext` 拿到数据根：`ForgeSelf.Abstractions/IDataLocationService.cs:20`
  `string GetHostDataDirectory();`，并经 `ContextExtensions.cs:14,23` 暴露为 ctx 扩展。
- XCode 的连接注册是**静态全局**：`XCodeConfig.cs:88` `DAL.AddConnStr(name, connStr, null, "SQLite")`，
  在宿主启动即执行。SQLite 支持多连接并读同一文件（WAL 模式下读写不互锁）。

**两条可行路线（互斥，需裁决）**：

| 路线 | 做法 | 改动 | 代价 |
| --- | --- | --- | --- |
| **A. 只读契约（推荐）** | 在 `ForgeSelf.Abstractions` 加 `ITurnTelemetryQuery`（只读、分页、按时间/模型/会话过滤），宿主 `AppBuilder` 实现并注册 | 宿主 +1 接口 + 1 实现 + DI 注册 | 需碰宿主装配，但**契约层本就是为跨插件共享而设**，且符合技能铁律「首个消费者出现再上移并出 ADR」 |
| **B. 插件自建连接** | 插件用 `GetHostDataDirectory()` 拼出 `ForgeSelf.db` 绝对路径，自建连接名（如 `CostScopeHost`）`DAL.AddConnStr`，用 `DbServer.ExecQuery` 裸 SQL 读 `ChatTurn`/`UsageRecord` | 仅插件内 | 零宿主改动；但**裸 SQL 脆**——宿主表结构一变插件即静默破，且绕开 XCode 实体层 |

> **推荐 A**，理由：① 项目已确立「跨插件共享契约放 Abstractions，首个消费者出现即上移」的规范；
> ② 裸 SQL 绑死列名会放大维护面（本仓 `ChatTurnModel.cs` 已有 24 个业务字段）；
> ③ 该接口签名极窄（分页 + 过滤 + 投影 DTO），不引入性能风险。
> **这是闸门1 唯一需要用户点的一刀**（下文 §2 作为 `Unknown` 上交，不擅定）。

**兜底确认（若选 A）**：`ChatSessionService.RecordTurnStatsAsync` 已确保每轮落库（§research 1.1），
新增只读接口不改变任何写入路径，**零回归面**。

### 1.2 单价数据从哪来？—— 无需外部 API，纯用户配置

- 无任何定价源需要联网：模型单价完全由用户在插件设置里维护（每模型一行：输入单价/输出单价，每 100 万 token）。
- 未配单价的模型 → 用量正常统计、成本标 `Unknown`（降级策略见 design §7），**不阻断任何功能**。
- 内置一份「常见模型默认单价表」作为**可覆盖的初始值**（纯资源字典，不联网、不自动更新）——
  这一项标 `Unknown` 交用户裁决是否纳入 P0。

### 1.3 聚合查询量级可行性

- `ChatTurnModel` 24 字段（`ChatTurnModel.cs:15-99`），成本聚合只需 9 列：
  `CreatedTime / Model / PromptTokens / CompletionTokens / TotalTokens / DurationMs / FirstTokenMs / ResponseStatus / ErrorMessage`。
- 典型单机日增数百轮；`GROUP BY(date, Model)` 在 SQLite 上是毫秒级。
- 预算告警不需要后台长轮询：页面轮询 + 后端惰性重算日汇总即可（避免新增 `IHostedService`，
  规避技能铁律 14「插件热重载后 HostedService 不重启」的坑）。

---

## 2. 收益 / 成本 / 风险三栏

| 维度 | 内容 |
| --- | --- |
| **收益** | ① 把全仓唯一的「零成本概念」真空填上（research §2 grep 零命中）；② 消费 11 个孤儿端点中的 5 个，让 `UsageStatsController` 一半端点从「死代码」变「在用」；③ 兑现 `not-taken-decisions.md:89` 明文预设的「成本审计」触发条件；④ 首个**跨宿主库**读数的插件，跑通「插件↔宿主共享契约」这条新接缝，后续审计插件可复用 |
| **成本** | 后端 1 插件（约 4 表 + 5 服务 + 1 控制器）；前端 1 自带 `web/`（3 视图）；宿主仅 +1 只读契约 + DI 注册；测试约 25 条。估算 1 个工作日内可完成门禁 + e2e + 走查 |
| **风险（高）** | ⚠️ **无**。不涉及数据库迁移宿主表、不破坏任何 API 契约、不引入长连接/后台任务、不动插件加载机制 |
| **风险（中）** | ① 契约落位选择错误（A/B）→ 已作为闸门1 唯一问题上交，不擅定；② SQLite 并发写锁：插件**只读**不写宿主库（自己的表全建在插件库），风险面趋零 |
| **风险（低）** | 单价单位换算（元/100万 vs 元/万 token）易错 → 用单测锁死换算，前端统一「每 100 万 token」 |

---

## 3. 与既有 18 插件的边界（不抢、不重）

| 既有插件 | 是否重叠 | 分界 |
| --- | --- | --- |
| `system-monitor` | ❌ 无 | 它是 CPU/内存/磁盘/网络主机监控（`docs/02-features/015-system-monitor.md:26-29`）；CostScope 是**AI 用量的钱与量** |
| `ai-agent` | ❌ 无 | 它做执行与记录（`AgentRun`/`AgentStepRun`）；CostScope 只做**读侧聚合**，不碰执行链 |
| `workflow-engine` | ❌ 无 | 工作流统计端点（`workflows/*`）本插件**暂不消费**，留给原所有者（research §8） |
| `dev-tools` | ❌ 无 | 开发调试；不做用量视角 |
| `todo-tracker`/`memory-system` | ❌ 无 | 无关域 |
| 宿主设置页 | ⚠️ 需让位 | 单价/预算配置**放插件内**（不放宿主设置），保持宿主设置页不再新增插件项（技能铁律 3：界面归插件） |

---

## 4. 结论

- **技术上：可做**（§1.1-1.3 全通）。
- **值得做**：证据链闭合——数据底座已就位 + 全仓零成本概念 + 仓库自己点名「成本审计」为触发条件 + 顺带消化 5 个孤儿端点。
- **唯一待裁决**：契约落位 A/B（§1.1），推荐 A。
- **判定：通过可行性审查，进入设计方案（第 3 步）。**

---

## 5. Unknown（显式记录，不做假定）

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 契约落位选 A（Abstractions 新接口）还是 B（插件裸读宿主库） | 决定是否改动宿主 | **闸门1 上交用户裁决**，推荐 A，不擅定 |
| 是否内置「常见模型默认单价表」 | 影响首次配置体验 | 待裁决，默认**不内置**（P1 再做），避免维护过时单价 |
| 计价货币与税率 | 金额展示 | 固定人民币无税，用户按税前单价填（P0） |
| 单价精度（每 100 万 token 保留几位小数） | 小额计算误差 | 采用 decimal 8 位（design §8 固化） |
| `ChatTurn` 是否会随 040 改造（B1–B8 进行中）变更表结构 | 契约稳定性 | 契约只依赖 9 个稳定列；040 若改表，契约实现随之调整，DTO 不动 |
