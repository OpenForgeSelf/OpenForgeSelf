---
name: architecture-design
description: OpenForgeSelf 架构设计与架构变更的统一分析处理流程。当任务涉及架构设计、架构变更、插件间通信/能力供给（服务互通、事件广播、能力接缝）、内核（IContext/EventBus/Fiber）语义调整、宿主与插件职责划分时使用。先查调研裁决与架构文档找答案，不足再调研 deepseek-harness/Cordis，禁止脱离依据自作设计。
---

# 架构设计统一流程（architecture-design）

## 何时用

- 任何架构设计、架构变更提案（新增/调整能力接缝、事件族、宿主-插件职责、内核语义）
- 插件间通信选型：服务互通 vs 事件广播 vs 拦截管道 vs 共享存储
- 发现「绕过内核」的隐式耦合（如插件间共享 JSON/静态类/直连 HTTP）需要归位时

## 铁律（先记住这 5 条）

1. **先查依据，不自作设计**：架构问题的答案优先从调研与架构文档获取（见下「依据查阅顺序」）；实现与调研方案分歧时，以调研 §6 偏差清单「Cordis 原案」列为准，除非有 ADR 记录偏离理由。
2. **契约必须入 `ForgeSelf.Abstractions`**：任何跨插件/跨宿主边界的能力契约、共享 DTO、事件名常量都定义在契约层，禁止定义在单个插件内部。
3. **禁止隐式耦合当契约**：插件间不得用共享文件格式、静态类、直连 HTTP 传递数据；共享文件只能作为某个 Provider 的持久化实现细节。
4. **插件服务取宿主契约一律 `IContext` + `ctx.Get<T>()`**：插件子容器只含插件自身服务 + IContext，ctor 注入宿主契约会解析失败（500）；消费插件能力同样 `ctx.Get<T>()`，**禁止缓存实例为字段**（每次用每次 Get，防热重载悬空）。
5. **高风险必升级**：数据库迁移、API 契约破坏性变更、主依赖大版本升级、内核语义调整 → 先升级给人确认再执行（AGENTS §3.3 / §6.4）。

## 依据查阅顺序（按序查，查到即停）

1. `docs/06-research/001-deepseek-harness-plugin-architecture.md`
   - §5.5 软依赖判例（可选增强不走事件）
   - §5.6 权威裁决 A-F（服务全局/本地、解析顺序、eager 单例、provide 即 effect、宿主 root 常驻、转发集合边界）
   - §6 偏差清单（7 项）+ 执行 FAQ Q1-Q6（插件互通标准答案）
2. `docs/01-architecture/cordis-kernel.md` §3.6（插件间服务互通规则）+ §4（Cordis→.NET 映射表）
3. `docs/01-architecture/host-capability-seams.md`（能力供给三层模型 L1 契约 / L2 事件 / L3 拦截 + 选型决策树）
4. `docs/07-decisions/`（既有 ADR 与「审慎不做」台账，避免重复评估与前后矛盾）
5. 以上无答案 → 继续调研 deepseek-harness / cordiverse/cordis（DeepWiki 定向问答），并把新裁决**回写 `docs/06-research/001`**（增补小节，注明日期与出处）

## 代码事实核对（设计前必做，不凭记忆）

- `ForgeSelf.Core/IContext.cs` / `Context.cs`：Register（全局共享表）/ RegisterLocal（本地值）/ Get（本地→共享表）/ Effect 当前语义
- `ForgeSelf.Core/IEventBus.cs` + 事件传播现状（每个 Context 独立 EventBus 还是已共享根总线——偏差#5 修正状态看 TODO 进行中项）
- `ForgeSelf.Api/Plugins/PluginManager.cs`：`ProvideHostServices` 宿主 seed 清单、`CollectForwardDescriptors` 转发边界
- 涉事插件的 `Apply(IContext)` 与服务构造方式

## 选型决策树（能力供给逐层问）

1. 消费者拿不到能力时能降级吗？→ 能：**L1 契约 + `ctx.Get<T>()`**（默认，停在这里，不要用事件）
2. 消费者必须在能力变化发生时实时动作？→ 补 **L2 事件**（`命名空间/动作` 事件族，L1 仍保留作拉取通道；跨插件事件需先确认偏差#5 已修）
3. 需要多方按序拦截/改写/否决？→ **L3 waterfall/serial**（判例：`tools/pre-execute` 拒绝门）
4. 能力缺失时消费者根本不该启动？→ 硬依赖 `Consumes` + PENDING（机制缓建，首个强依赖接缝出现时落地）

提供方三问：
- 契约在哪？→ Abstractions（宿主设计）
- 谁提供？→ 平台级核心流程底座 → 宿主 `ProvideHostServices` seed（root 常驻）；业务能力 → 插件 `Apply` eager 构造 + `ctx.Register<T>()`（提供即 effect，卸载自动摘除）
- 持久化？→ Provider 内部细节，消费者不感知

## 产出与门禁

1. 设计文档写 `docs/01-architecture/`（架构层）或 ADR 写 `docs/07-decisions/`（拍板理由），新文档登记 `docs/README.md`。
2. 明确「审慎不做」项 → 登记 `docs/07-decisions/not-taken-decisions.md`（为何不做 + 触发条件）。
3. 风险分级（低/中/高）与阶段划分写入设计；中风险（公共接口变更）执行后跑全量 Verify（`dotnet build` + `dotnet test`），高风险先升级。
4. 实施遵循 TDD：先写契约/行为测试（Red）→ 实现（Green）→ 全量回归。
5. 新产生的可复用裁决回写调研文档或 MEMORY，保持「下次同类问题先查到答案」。
