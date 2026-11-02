# 02-goals — 项目目标（MAF-centric）

> 状态：生效（2026-11-02 对齐 MAF 一等公民远景）
> 最后更新：2026-11-02
> 目标须**可衡量**：每个目标带验证方式；无验证方式的目标不成立。
> 上游愿景：[`01-vision.md`](01-vision.md)；架构：[`../01-architecture/maf-centric-architecture.md`](../01-architecture/maf-centric-architecture.md)

## 阶段总览（围绕执行中枢 MAF）

> 用户可见插件名是 **智能体**。下表里的 MAF 指执行中枢（MAF Runtime），不是菜单名或页面标题。

| 阶段 | 主题 | 成功判据（摘要） |
|------|------|------------------|
| **P0** | MAF 执行安全底座 | 统一执行上下文 + 工具审批接线 + 取消可杀子进程；有 pilot + 测试绿 |
| **P1** | 本机读写与 Skills | 文件/终端能力经 ToolRegistry 六闸门可验证；Skills 挂载进 MAF Run |
| **P2** | Routines → MAF Run | Scheduler/定时任务触发的是 AgentRun / MAF Run，而非旁路脚本直跑 |
| **P3** | Browser 插件 | 浏览器能力以插件注册工具，经 MAF 调用，不另起 Runtime |
| **P4** | Memory / 本地模型 | 记忆与本地模型适配深化；仍落 MAF + 插件，不平行引擎 |

> **本回合（2026-11-02）**：只定产品/架构真源 + 对外命名骨架；**不假装一次做完 P0–P4**。实现按 `docs/ai/pilot/2026-11-02-maf-first-class/` 闸门与后续批次推进。

---

### P0 — MAF 执行安全底座（当前优先落地批）

- [ ] 工具统一管线叙事落地：Permission → Approval → Execute → Verify → Result（宿主六闸门 + MAF `ApprovalRequiredAIFunction` / `ToolApprovalRequestContent` 接线）
- [ ] 执行上下文可观测（session / run / callId / cancel token 贯通）
- [ ] 取消：用户取消 → MAF token → 工具 Signal → **终端子进程可被杀**（修现有超时不杀缺口）
- [ ] `ReactLoop` / ad-hoc 旁路：文档标废弃；新代码禁止新增平行循环

**验证方式**：对应 `docs/ai/pilot/YYYY-MM-DD-maf-tool-approval/`（及杀进程专项）闸门2 Evidence + `dotnet test` 过滤集 + AIAgent 1 冒烟 e2e。

### P1 — 文件系统 / 终端 / Skills

- [ ] 文件读写、终端命令继续走 ToolRegistry；权限模型与审批策略可配置
- [ ] Skills 作为 MAF Run 的一等输入（选中即注入，可审计）
- [ ] 产品文案与功能档案统一「本机能力插件」归属

**验证方式**：单测 + 插件 e2e 主链路；功能档案 `02-features/` 回写。

### P2 — Scheduler / Routines → MAF Run

- [ ] 定时/例行任务触发 **MAF Run**（落轨迹、可取消、可审批），禁止「旁路直接调工具跳过 MAF」
- [ ] Routines 配置 UI 与 Run 记录可追溯

**验证方式**：集成测试「调度触发 → AgentRun/会话事件」+ e2e 只读走查。

### P3 — Browser 插件

- [ ] 新建 Browser 能力插件（立项走 `plugin-feasibility-study` → `plugin-development`）
- [ ] 工具经 ToolRegistry 供 MAF 调用；无平行浏览器 Runtime

**验证方式**：插件四层门禁 + 1 冒烟 e2e。

### P4 — Memory / 本地模型

- [ ] Memory 跨 Run 沉淀与检索强化（仍为插件能力）
- [ ] 本地模型适配体验（延迟、上下文、失败可解释）

**验证方式**：记忆相关单测/e2e + 本地模型接通回归清单。

---

## 明确不做（与愿景红线一致）

- 云端计算机 / 远程常驻沙箱产品化
- 团队共享工作区 / 企业管理（组织、席位、审计中台）
- 平行 LocalBot / 第二套 Agent Runtime（[not-taken 050](../07-decisions/not-taken-decisions.md)）
- 新建 `specs/` 规格目录（[not-taken 051](../07-decisions/not-taken-decisions.md)）
- 本阶段不做历史包袱迁移兼容（绿场：可废弃旧命名与旧旁路，见 [ADR-006](../07-decisions/006-maf-first-class-greenfield.md)）

## 目标评审规则

- 每个目标必须有验证方式，无法验证的目标移出本文件；
- 新提案对照 [`03-principles.md`](03-principles.md) → [`01-vision.md`](01-vision.md) → 本文件阶段；
- 达成后：勾选、记入 `17-changelog/`，必要时开独立 pilot 归档证据。
