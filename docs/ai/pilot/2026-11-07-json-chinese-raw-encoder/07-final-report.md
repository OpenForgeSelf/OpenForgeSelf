# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜状态只报事实（AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：本项目 JSON 序列化全部走 `System.Text.Json`，**无全局 Encoder 开关**（STJ 设计如此），每个写出点必须各自声明 Encoder；仓内既有代码全部用无参/最小参数重载 → 默认 `JavaScriptEncoder.Default` 把所有非 ASCII 转义成 `\uXXXX`。调用点分散于宿主（Controllers/Services/Plugins 服务/AppBuilder/MVC 管道）与 22 个插件目录，共 488 处违例（G2 扫描实测）。宿主与插件共享 `ForgeSelf.Abstractions` 层（放共享序列化选项的天然位置）；后端测试 xUnit 全量约 3700 用例；G1–G4 守卫体系与 `problem-classes` 台账已具备（PC-027 前共 26 个 PC）。

## 2. Selected Task

用户输入6（14:38）原文：「将本项目的json格式化，关于中文部分，设置不要序列化成 \u76EE\u5F55\u4E0A\u6EAF 这种，直接保持中文原文」。定位 = 跨切面配置变更 + 缺陷类闭环，PILOT 全量档（宿主 + 22 插件扫点 + G2 守卫 + 台账归档）。

## 3. Changed Files

- **新建（2）**：`ForgeSelf.Abstractions/ForgeJson.cs`（`Default`/`Indented`，Encoder=UnsafeRelaxedJsonEscaping，命名策略 null）；`ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`（G2 源码扫描 ×1 + G1 行为锁 ×4）
- **宿主生产代码（~35 文件，Lead 亲扫）**：UnifiedAI 控制器群、Chat、ToolRegistry(9)、SessionEventJsonConverter、PermissionProfileService、PersistentApprovalService、UsageStats×2、UpdateSettingsService×2（→`ForgeJson.Indented`）、Plugin 服务群（含脚手架模板内生成代码）、OpenAICompatible/OpenAIResponses Provider、AppBuilder `AddJsonOptions`（MVC 管道）
- **插件（22 目录 / 90 文件，3 并行子代理分区）**：组A 18 文件（WorkflowEngine/MemorySystem/TodoTracker/QuickLinks/Scheduler/ScriptRunner/Sems/Home）、组B 51 文件（McpCenter/DevTools/SystemMonitor/FileTools/AIAgent/AgentHub，含 JsonFormatterService 3 处 JsonWriterOptions）、组C 21 文件（DesignSystem/ToolBridge/CostScope/BrowserUse/SmartTerminal/DbAdmin/ImGateway/SamplePlugin）；终态由 G2 GREEN 机器背书
- **统一不变量补齐（Lead，10 处只读/反序列化初始化器）**：子代理豁免与裁定不一致处，含 9 处无 using 文件用全限定 Encoder + PC-027 注释
- **测试夹具修复（1，伴生缺陷）**：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js` — stdio LSP 帧读取改 Buffer 字节级切帧（旧实现按 JS 字符长度切，中文载荷帧被误判不完整）
- **文档/归档（4）**：PC-027 新建、case 新建、`problem-classes/README.md` + `cases/README.md` 各加索引行
- 完整清单与逐文件处置 → [05-evidence.md §Changed Files](./05-evidence.md)

## 4. Validation

Build: PASS — `dotnet build` 0 错误 / 371 警告（基线存量警告），`.temp/host-build2.log`

Unit Test: PASS — 守卫 5/5（G2 RED 488 违例 → 清扫后 GREEN 0，阳性对照成立，`.temp/guard-red2.log`→`.temp/guard-green.log`）；Mcp 夹具修复复跑 2/2 / 5s（`.temp/mcp-fixture-retest1.log`）

Integration Test: PASS（对表口径）— 全量 `dotnet test`：**3708 通过 / 29 失败 / 3737 总计 / 25m1s**（`.temp/full-test1.log`）。29 红九组归因闭环：本批引入 1（Registry 台账索引缺行，当场修，复跑 5/5 绿）+ 本批行为变更暴露伴生夹具缺陷 2（Mcp×2，修后复跑 2/2 绿）→ **修复后本批净新增红 = 0**；其余 26 红均非本批：存量 22（HEAD worktree 对照实验失败集逐个同红实证：ProjectWorkspace×15 / WorkflowPlanning×6 / ScriptRunnerDi×1）+ 并行会话 1（DbAdmin 新增工具，git diff 实锤）+ 编译快照瞬时中间态 1（Maf，干净重建后整组 6/6 绿）+ 负载 flake 2（Design / ScriptRunner.CancelAsync，机器空闲复跑均过）

E2E: N/A — **插件六步闸门批次级裁剪**（03-plan 已声明、闸门1 批准）：本批为机械跨切面配置变更，不改任何插件功能行为与界面；插件门禁以 G2 守卫 + 后端全量对表为机器裁判；e2e / 起源码走查 / 发布 / 运行实例复验不做；发布后 :51888 中文显示抽查（2–3 个中文密度高的插件面板）记 TODO ⬜，属升级实例动作。

## 5. Evidence

[05-evidence.md](./05-evidence.md) 全节 **Verified**，AC1–AC6 逐条对表；证据日志 11 份（guard-red2 / guard-green / host-build2 / full-test1 / attrib-head1 / registry-retest1 / flake-retest2 / maf-retest3 / flake-retest3 / mcp-fixture-retest1 / check-pc1），关键输出原文逐条摘录于 05，日志清理后仍可对账。

## 6. Review

**APPROVED**（[06-review.md](./06-review.md)）：八问逐项答、四项 Check 全 PASS、Risk L1、Critical/Major 无、Minor 4 条（组计数纠正、误杀 testhost、flake 判定纪律、编译快照瞬时态）各带机制修正。

## 7. Risk

L1（低）。残余三条均记 TODO 观察项：① G2 已知盲区（原始字符串模板内生成代码 / 插值孔 / 冷门重载）出现真实逃逸再升级实例级扫描（PC-027 §2 备忘）；② 发布后 :51888 运行实例中文显示抽查未做（属升级实例动作）；③ 并行会话活跃期跑全量测试的编译快照瞬时中间态（本批 Maf 假红实证）——长测试尽量避开并行写窗。

## 8. Problems Found

1. **PC-027 本体**：全仓 JSON 写出默认转义非 ASCII（488 处违例），用户可见面为落盘文件 / DB JSON 列 / 接口响应不可读。
2. **伴生（本批暴露）**：MCP 测试夹具 mock-mcp-server.js 用 JS 字符长度切 Content-Length 帧（潜伏缺陷，ASCII 载荷碰巧正确）——被「中文原文输出」触发为 2 个确定性测试红。
3. **本批引入（当场修）**：cases/README.md 索引缺本 case 行，被 ProblemClassRegistryTests 台账守卫抓住——台账闭环机制有效。
4. **过程缺陷**：全量测试与并行会话编辑并发的编译快照瞬时中间态（Maf 假红）；「复跑绿即 flake」判定在失败点一致性复核前不可下结论。
5. **存量（非本批，记 TODO）**：ProjectWorkspaceDefaultRootTests×15（测试按 FR-5 旧语义断言，与已提交 cwd 必传契约冲突）/ WorkflowPlanningIntegrationTests×6 + ScriptRunnerDiIntegrationTests×1（404）陈旧测试治理；Mcp stdio 长时超时（300s）观察项。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（序列化点形态盘点 + Abstractions 共享层确认，直接支撑分区清扫与 ForgeJson 落位） |
| Intent → Spec | PASS（「统一不变量含只读初始化器」在 Spec 期定死，子代理豁免争议消灭在裁定层） |
| Spec → Plan | PASS（宿主亲扫 + 3 子代理目录不相交分区 + G2 终态机器裁判） |
| Plan → Code | PASS（AppBuilder CS1061 首编译即纠；子代理回报 + 守卫背书） |
| Code → Test | PARTIAL（TDD 红绿齐备；但全量归因两次误判——Mcp×2 一度记 flake、Maf 假红延误定性，靠复跑纪律与失败点一致性复核纠正） |
| Test → Evidence | PASS（四重实验：HEAD 对照 / 空闲复跑 / 干净重建 / 夹具修复验证；日志逐条摘录） |
| Evidence → Review | PASS（APPROVED；Minor 4 条全部带机制修正沉淀） |

### 9.5 本批自复盘（必填）

- **经验提取（保留打法）**：
  1. **HEAD worktree 对照实验**定性存量红：干净 worktree 同 filter 复跑，失败集与主树逐个比对——一次实验把 22 个红定性为存量，是本批归因效率的关键。
  2. **「先立机器裁判，再 fan-out」**：跨切面机械变更先落 G2 守卫（RED 实证 488 → 目标 GREEN 0），再并行子代理分区清扫，终态由守卫背书而非逐文件人审。
  3. **flake 判定必须附失败点一致性复核**：红/绿翻转不等于 flake；本批 Mcp×2 失败点精确落在中文载荷 `tools/call`（同会话 ASCII 请求全过）= 确定性缺陷信号。
  4. **测试 bin 持久 SQLite（CWD 相对兜底路径）污染排查法**（移库实验）：虽在本批被证伪，排查路径已沉淀可复用。
- **待改进（带机制修正）**：
  1. 误杀 testhost 92832 致一次实验作废重跑 → 修正：空闲期残留 testhost 可能属并行会话，杀前先查 `job_list` 归属（已入日记教训）。
  2. `--no-build` 复跑不构成新证据（只复现旧快照，Maf 假红因此延误定性）→ 修正：定性类复跑先干净重建。
  3. 25 分钟全量与并行会话编辑并发 → 修正：长测试尽量避开并行写窗；无法避开时，归因优先怀疑编译快照瞬时态。

## 10. 问题类归档

**PC-027（新建，首见）**：`docs/11-troubleshooting/problem-classes/PC-027-json-nonascii-default-escaping.md`
Case：`docs/11-troubleshooting/cases/2026-11-07-json-chinese-raw-encoder.md`（含「伴生暴露」行记录 mock 夹具缺陷）
台账索引：`problem-classes/README.md`（tags 表 + 总表）与 `cases/README.md` 各一行；机器校验 `check-problem-classes.ps1` 27 PC 全过（`.temp/check-pc1.log`）。
伴生夹具缺陷不另立 PC：全仓 JS `Content-Length` 扫描仅此一处、无同类扩散；其常驻守卫 = 两个中文载荷 MCP 集成测试（任一侧回归字符级切帧即 300s 超时红）。

## 11. 重构提案

**R0（不重构）**：逐点映射为机械改动，不引入新抽象；唯一结构化新增 = `ForgeJson` 共享选项类（Abstractions 层，宿主与插件均引用）。备选方案「序列化出口统一收口（源生成器/包装层）」超出修「类」所需，属架构级演进——若未来序列化点再膨胀（>500 处）或出现第三种选项形态，再转 architecture-design 立独立 pilot。

## 12. 悬空教训清单（必须为空）

无。所有发现均已落位：PC-027 / case（本体 + 伴生）、06 Minor 4 条（机制修正）、TODO.md 观察项（存量红 22 治理、发布后 :51888 中文抽查、G2 盲区备忘、编译快照瞬时态与 Mcp 长时超时观察）、日记教训（误杀 testhost、worktree 相对路径陷阱）。
