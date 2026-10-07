# CostScope 插件 TODO（插件级遗留）

> 本文件记录**成本观测插件的剩余问题与未尽事宜**。
> 条目格式：`- [ ] <事项>（P<优先级>，来源）`；完成即移除，不留 ✅ 堆积（AGENTS §7.5）。
> 验收对照：`.agents/skills/plugin-development/references/plugin-acceptance.md`。
> pilot 工件链：`docs/ai/pilot/2026-10-03-llm-observability/`（00–07 齐全，pre-commit 闸门 PASS）。

## 当前状态

- 版本：v0.1.0（首版交付，2026-10-06）
- 后端：`CostController` 18 个端点，路由前缀 `api/cost-scope`，类级 `[Authorize("ApiKeyPolicy")]`
- 数据：插件自有库 `CostScope`（4 表），与宿主库严格隔离（铁律 12）
- 前端：`web/`（Vite lib 模式），产物 `web/dist/index.js` + `web/dist/style.css`
- 测试：全量回归 **227/227**，遥测契约 6/6，**7 组反向探针全部实红后复绿**
- 构建：插件与宿主均 0 错误；`pnpm check`（vue-tsc）0 错误
- ⚠️ **代码交付完成 ≠ 功能可用**：下列甲/乙两类缺口在补齐前，真实环境跑不通

## ⬜ 待办

### 甲·功能性缺口（缺了在真实环境不成立）

- [ ] **`IAgentRunTelemetryProvider` 无生产实现（P1，来源:pilot 04-task A3b 偏差表）**：
      接口定义在 `ForgeSelf.Abstractions/IAgentRunTelemetryProvider.cs`，宿主 `TurnTelemetryQueryService` 以可选依赖注入
      （未注册返回 null，不伪造）；但**实现方应在 AIAgent 插件侧填 `AgentRun`/`AgentStepRun` 并注册，尚未实现**
      ⇒ trace 端点与瀑布视图在真实环境拿不到 AgentRun，关联功能等于不可用。
      目前只有测试桩 `ForgeSelf.Api.Tests/Services/TurnTelemetryQueryServiceTests.cs:142` 的 `StubAgentRunProvider`。
- [ ] **前端产物由发布流程生成（P1，来源:`.gitignore:30` `dist/`）**：
      `Plugins/CostScope/web/dist/` 不入库，须跑发布流程产出；未跑之前界面加载不出来。
      本地已能构建：`cd Plugins/CostScope/web && pnpm install && pnpm build`（`index.js` 27.65 kB + `style.css` 4.63 kB）。

### 乙·验证缺口

- [ ] **端点 401 真实链路未验证（P1）**：目前只有反射守卫测试（类级 `[Authorize("ApiKeyPolicy")]`），
      未发起真实 HTTP 请求验证未带 ApiKey 时确实 401（铁律 14/17）。
- [ ] **无 e2e（P1）**：本轮全链路零条真实 HTTP 请求；需起宿主运行时验 401 + 路由 + 序列化。
- [ ] **界面未运行时走查（P2）**：四个面板（Dashboard/Price/Budget/Trace）未在浏览器实际打开核对布局与交互。

### 丙·技术债与健壮性

- [ ] **`CostModelPrice.Model` 缺 DB 级唯一索引（P2，来源:pilot 04-task A5 已知缺口）**：
      Model.xml 中该列索引**非唯一**（XCode 只生成 `FindAllByModel`）⇒ FR-3.5 的模型名唯一性**目前只在服务层保证**，
      并发写入存在唯一性被绕过的窗口。补唯一索引需评估 `CheckDeleteIndex` 风险（索引名变更会触发删索引）。
- [ ] **单价目录为空时的首用引导（P3）**：未配任何单价时成本全为下界（BR-2），界面虽已显式标注，
      但缺少「去配置单价」的引导入口，新装即用体验差。

## 已决策保留（不计入待办，勿重复提）

| 事项 | 依据 |
|---|---|
| 主聊天链路用量不可见（`ChatController` 两处 `Usage: null`） | U-2 裁决 (c)：不迁接缝；端点与界面已显式声明 |
| trace 关联为**近似推断**（会话键 + 时间窗 ±30s，非外键） | A9 取消，用户裁决「宿主保持抽象、插件消费宿主已产出的数据」；界面顶部固定显示关联口径说明，BC-4 未关联如实上报 |
| BC-8「插件未启用返回『未启用』而非 404」 | 属宿主插件装载层职责，控制器内无法实现 |
| 宿主 30 个实体只有生成物、没有 `Model.xml` | 既有技术债（铁律 9/11 宿主侧从未满足）；实测重建不可行已 ABORT，建议独立立项 |
