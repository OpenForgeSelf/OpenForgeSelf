# AI-Native Pilot Result（最终汇报）

> 任务：智能终端助手/凭据恒 424 —— StHostGateway 拿错容器（PC-034 首见，缺陷类闭环）。闸门1 = 输入19「开工并修复」。

## 1. Repository Understanding

确认：插件 DI 构造服务收到的 `IServiceProvider` = MS 自解析的**插件子容器本体**（`PluginContextDiExtensionsTests.cs:34-43` 实证注释）；宿主契约只有两条注册路——ai-agent `ctx.Register<IChatCompletion>`（`AIAgentChatCompletion.cs:186-187` 双注册）+ 宿主 `ProvideHostServices` seed `ISecretEncryptionService`——都只对 `IContext` 通路可见（`PluginManager.cs:781` 只向子容器注册 IContext）。

## 2. Selected Task

输入18 日志 `[smart-terminal] host_module_unavailable: 宿主对话模块不可用，会话不受影响` → Q&A 定位 → mini-task 落盘 → 输入19 批准（默认 P1 随批修 + P2 异根因进 TODO）→ 按 problem-class-closure 技能执行缺陷类闭环。

## 3. Changed Files

修复 6 + 测试 3 + 知识库 6（全清单见 05-evidence.md Changed Files）：StHostGateway（canonical fix）、WorkflowExecutor/TaskExecutor/WorkflowTaskHandler + 两 Plugin 调用点（G2 扫查命中随批修）、StHostGatewayWiringTests（新，G1）、ProblemClassEnumerationTests（新 PC-034 G2）、smart-terminal e2e 冒烟扩展、PC-034 + case + 三处索引 + 插件文档 PC 指针。

## 4. Validation

Build: `dotnet build ForgeSelf.slnx` → 成功 0 错误（Verified）

Unit Test: 红灯 2 失败（修复前）→ 接线 3/3 + Pc034 5/5 + 过滤集 123/123 + Registry 5/5（Verified）；`check-problem-classes.ps1` OK 34 PC（Verified）

E2E: smart-terminal 冒烟 1/1（真实 publish 宿主 + 真实会话 + 助手往返，Verified）；助手气泡 = 提供方层错误（未找到可用对话模型），**424 病文案「宿主对话模块不可用」零容忍断言通过**

Origin 走查: dev-stack 源码宿主 API 层复验，与 publish 宿主同判据（Verified）；`check-commit-hygiene -Staged` PASS（Verified）

## 5. Evidence

`docs/ai/pilot/2026-11-14-st-host-gateway-ctx/05-evidence.md`——关键结论：接线守卫断言 `gateway.TryChat()` 与 `ctx.Get<IChatCompletion>()` 同一实例（红→绿）；G2 守卫首跑真实命中 4 处、修复后 allowlist 空账全绿；截图 `ForgeSelf.Web/screenshots/e2e/smart-terminal/smoke-chat.png`（助手气泡为提供方层文案）。

## 6. Review

`06-review.md` Final Decision：**APPROVED**（八问全过；L1 风险；「真实模型回复」已于 2026-11-15 运行实例侧载后实测闭合——见 05-evidence「运行实例侧载与浏览器实控走查」）。

## 7. Risk

L1。构造签名收敛、双装配路径实参同一。2026-11-15 更新：:51888 运行实例已侧载 2.0.1 并经浏览器实控走查，真实模型回复实测产出（助手实答当前目录 + 命令提议卡片），「宿主对话模块不可用」零命中——残余 Unknown 缺口已闭合。

## 8. Problems Found

- WorkflowExecutor L566-571 工作流脚本步骤恒抛「脚本执行器未注册」（PC-001 提供侧缺口）→ TODO P1
- SystemMonitorPlugin Apply 期 eager 解析致采样 effect 空转（PC-005 同族）→ TODO P2
- PC-004/PC-009 planned 守卫 todo 曾被队列清理破坏 → 补回 TODO（Registry 测试 + checker 双兜底）
- DbAdminView.vue 基线 TS 红（非本批）→ TODO P3

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（红→绿 + G2 咬合实测） |
| Test → Evidence | PASS（真实输出留档 .temp/*.log） |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填）

**经验提取（保留打法）**：
1. **G2 枚举式守卫先自证再上岗**：检测器配阳性/阴性合成用例（Pc034_Detector_*），首跑即咬出 3 个真实存量——守卫写完必须先证明「能红」再证明「该绿的绿」。
2. **失败消息截断时用 pwsh 复刻检测逻辑拿全量清单**（FluentAssertions BeEmpty 只报首个命中），再按清单逐个甄别「同根因修 / 异根因 TODO / 非语义面收窄扫描」。
3. **守卫判据锚 message 层而非状态码层**：424 code 是 V2 既有契约，缺陷判据 = 病文案消失 + 提供方层文案出现——避免把契约保留误判为修复失败。

**待改进**：
1. e2e 冒烟扩展两处临场踩坑（`~` 起始目录在 publish cwd 失效、聊天面板藏在 rail）——插件 e2e 写 UI 链路前先读视图 testid 与弹层结构，减少一轮重跑。
2. 前端 `pnpm run check` 的插件全域基线红（DbAdmin）掩盖「我的面是否绿」的快速判读——每次要靠手工排除文件归属；待 DbAdmin 修复后此损耗自消（TODO 已记）。

## 10. 问题类归档

- **PC-034（新建，首见）**：`docs/11-troubleshooting/problem-classes/PC-034-plugin-di-service-host-contract-miss.md`——不变量「插件 DI 构造服务构造函数只许注入 IContext」；status=guarded（G1 接线 + G2 枚举双 active，allowlist 空账）。
- case：`docs/11-troubleshooting/cases/2026-11-14-plugin-di-service-host-contract-miss.md`（severity P1，evidence Verified）。
- 台账三处索引（problem-classes/README 台账+tags 检索、cases/README、index.md 速查行）均已更新；`scripts/check-problem-classes.ps1` OK、ProblemClassRegistryTests 5/5。
- 与 PC-001（提供侧未进共享表）/ PC-005（构造期 eager）的类边界在 PC-034 §1/lens 写明。

## 11. 重构提案

- **R1（本批已随修）**：WorkflowExecutor 半迁移收敛（单 IContext 构造）；Scheduler 两服务构造统一。
- **R2（提案，不在本批，需闸门1）**：为「插件确需宿主根 scope」提供内核级官方通路（如 IHostGateway 抽象或 IContext 上的受控宿主 scope 语义），把 `ctx.Get<IServiceProvider>()` 回落收进内核一处，消除散落各插件的回落写法。触发条件：该回落形态出现 ≥2 个消费点时上会（当前 AgentDelegationProvider 一处 + PC-034 canonical fix 引用，暂不动）。

## 12. 悬空教训清单（必须为空）

无——本报告第 8 节全部条目已落成 PC-034 / case 命中表 / TODO.md 逐字可检索条目（WorkflowExecutor 脚本步骤、SystemMonitor eager、PC-004/PC-009 todo 补回、DbAdmin 基线红）。
