# Spec

> 阶段：Stage 2｜Task ID：2026-10-17-problem-class-closure｜日期：2026-10-17
> 规格对象：问题类闭环机制（技能 + 知识库结构 + 模板 + 流程接入 + 守卫），全部为文件/脚本/测试层规格，无用户可见 UI（Interaction Design 节 N/A：不改任何界面）。

## 1. Functional Requirements

- **FR1 技能**：`.agents/skills/problem-class-closure/`，SKILL.md ≤120 行（触发、铁律 10 条、P0-P7 流程骨架表、守卫等级 G1-G4 决策、重构分档 R0-R3、出口清单）；`references/01~08` 八份细则（分诊归因 / 同类扫查 / 复现先测 / 重构提案 / 逃逸分析 / 机制上浮 / 归档规范 / 项目种子）；`scripts/new-problem-case.ps1` 脚手架（建 case + 挂/建 PC + 更新两处索引）。
- **FR2 知识库目录**：`docs/11-troubleshooting/problem-classes/README.md`（台账：tags 检索 + 守卫等级 + 复发计数 + 悬空检查列）+ `PC-<nnn>-<slug>.md`；`docs/11-troubleshooting/cases/README.md`（时间线索引）+ `<yyyy-mm-dd>-<slug>.md`；`index.md` 升级为现象速查入口（每行指向 case 或 PC）。
- **FR3 模板**：`docs/18-templates/postmortem/problem-class.tpl.md`（front-matter 机器可读：id/title/tags/invariant/status/first_seen/recurrences/signature/allowlist/guards/lens + 正文五节）与 `case.tpl.md`（现象→复现红灯→根因 5 Whys+第6问→扫查命中表→修复与否决→重构提案→守卫落地→逃逸分析→基建改进→复用赋能→证据分级）。
- **FR4 流程接入**：AGENTS.md §0 第 2 条增「同根因扫查命中属本任务范围」例外；§0 第 6 条出口清单加一行；§2.4 技能表登记本技能；规范升 v1.2.0（§1 第 5 条澄清「无关重构」定义、Stage 0 加「适用问题类」节、Stage 2 加 Failure Modes 要求、Stage 3 Test Plan 须列命中 PC 的守卫、Stage 8 八问变九问）；07 模板把 §10/§11 改为「问题类归档 / 重构提案 / 悬空教训清单（必须为空）」三节；docs/README.md §一 加「设计/修复前自查已知问题类」行。
- **FR5 守卫**：`scripts/check-problem-classes.ps1`（遍历 PC front-matter 的 signature 执行 grep 扫描，支持 allowlist 到期判定与 `-PilotTask` 模式校验缺陷类 07 三节）；`ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs`（守卫的守卫：台账↔文件双向一致、每 PC 有守卫或带截止日 TODO、case 引用有效、索引链接存在）；`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（三个类级枚举测试：PC-001 契约共享表、PC-002 前端裸 fetch、PC-003 PluginDbs 登记，均带阳性对照）。
- **FR6 种子回填**：首批 PC ≥3 个完整档案（契约共享表 / 鉴权-裸fetch / PluginDbs）+ 2 个范例归档（PowerShell 编码=G2/G3 已有守卫、手敲命令静默陷阱=G4 已有守卫）+ 1 个候选（状态回报≠进程终态，标 needs-confirmation、no-guard-yet 带 TODO id）；对应 case 至少 PC-001/002/003 各一条。

## 2. Input / Output

- Input：缺陷/回归/返工事件；或用户对方案的批准（本任务=输入全文）。
- Output：上述文件集合 + 绿灯证据。脚手架脚本输入 `-PcId -Title -Tags -Slug`，输出新建/更新的 case、PC、两处索引行。

## 3. Business Rules

- BR1 状态机：PC.status ∈ `open | guarded-partial | guarded | structural`；`recurrences` ≥2 强制 G2，≥3 强制 G4 提案（由 Registry 测试断言）。
- BR2 守卫完备性：每个 PC 必须 `guards` 至少一条 `status: active` 且路径真实存在，**或** `level: none` + `todo`（TODO.md 可 grep 到的条目关键字）+ `expires`（截止日）。
- BR3 allowlist 条目必须带 `reason` 与 `expires`；过期即红（防存量豁免永久化）。
- BR4 能写枚举式测试就不写 grep 正则；grep 只留给跨语言/文档场景。
- BR5 同一事实单一承载：AGENTS.md/技能只放指针（路径引用），不复述规则正文。

## 4. Boundary Conditions

- 扫查超限（命中 >10 处或触及 DB/鉴权核心）→ 修当前实例 + G3 baseline 模式 + 余项入 TODO——写入技能细则，不要求本批实现自动分叉逻辑。
- PC 文件缺失 front-matter 或必填键 → check-problem-classes 与 Registry 测试双双报错并指明文件与键名。
- 台账 README 与 PC 文件不一致（新增未登记/删除未清行）→ Registry 测试红。
- pilot 目录历史存量（2026-10-17 前）无「问题类归档」节 → `-PilotTask` 模式只对**含 `task_kind: defect` 标记（或 mini-task 标「缺陷」字样）且目录日期 ≥ 2026-10-17** 的新任务强制，避免旧目录假红。

## 5. Error Handling

- 脚本：任一 signature 命中违规 → 逐条输出 `文件:行 PC-id signature-id`，exit 1；台账解析失败 → 指明行号后 exit 1。不静默吞异常。
- 测试：断言消息必须携带修复指引（指向 PC 正文 §3 canonical fix）。

## 6. Compatibility

- 不改 `scripts/verify-pilot-artifacts.ps1`（L4）；pre-commit 追加独立调用，既有校验行为不变。
- 三个枚举测试若扫出**存量违规**：合法存量进代码内 baseline（带理由与到期日），非法存量修代码属越界——记 TODO，不阻塞守卫落地（守卫以 baseline 起步即 G3 定义）。
- 规范 v1.1.0→v1.2.0 仅增补与澄清，不改变既有九阶段语义；旧 pilot 目录不回溯。

## 7. Non-functional

- SKILL.md ≤120 行；references 单文件 ≤150 行；索引/台账可被脚本单向解析（简单 `key: value` front-matter 子集，不依赖 YAML 库）。
- 全部新脚本 UTF-8 with BOM、`pwsh` 语义（禁 5.1 依赖写法）。

## 8. Acceptance Criteria

- **AC1** `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1` exit 0，输出含每个 PC 的扫描计数（非空转）。
- **AC2** 植入一个临时违规（含 allowlist 过期样例）时 `check-problem-classes.ps1` 变红；移除后回绿（阳性对照，Verified）。
- **AC3** `dotnet build` 0 错误；`dotnet test --filter ProblemClass` 全绿（ProblemClassRegistryTests + ProblemClassEnumerationTests）。
- **AC4** 首批 PC：PC-001 契约共享表、PC-002 鉴权-裸fetch、PC-003 PluginDbs 三份档案 front-matter 完整、lens 非空、guards 有 active 项（或 none+TODO+expires，如 PC-004/005 候选）。
- **AC5** 每份 PC 的 `recurrences`/cases 引用在 `cases/` 有实体文件；`problem-classes/README.md` 与 `cases/README.md` 两处索引与实际文件双向一致。
- **AC6** AGENTS.md 恰好三处改动（§0 第 2 条例外句、§0 第 6 条出口一行、§2.4 技能表一行）；无其他正文膨胀。
- **AC7** 规范 v1.2.0：§1 第 5 条澄清、Stage 0/2/3 增补、九问、§5 汇报节名更新全部落笔。
- **AC8** 07-final-report 模板 §10/§11 替换为「问题类归档 / 重构提案 / 悬空教训清单（必须为空）」三节；verify-pilot-artifacts.ps1 对其关键节校验不受影响（含 Validation|Review 仍在）。
- **AC9** pre-commit 对触碰 `docs/11-troubleshooting/problem-classes/` 或 `docs/ai/pilot/` 的暂存集调用 `check-problem-classes.ps1`，本机以假暂存试运行验证红/绿两路。
- **AC10** 新增业务代码为零（不改 Plugins/ForgeSelf.Api 生产源码），git status 变更集与 00 触点清单一致。
