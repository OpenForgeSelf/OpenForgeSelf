# 01-vision — 项目愿景

> 状态：生效（2026-11-02 用户裁决：执行中枢 MAF 一等 · 插件名「智能体」· 绿场重设计）
> 最后更新：2026-11-02
> 关联：[`02-goals.md`](02-goals.md) · [`04-direction.md`](04-direction.md) · [`../01-architecture/maf-centric-architecture.md`](../01-architecture/maf-centric-architecture.md) · [ADR-006](../07-decisions/006-maf-first-class-greenfield.md)

## 一句话愿景

**铸己匣（OpenForgeSelf）：本机个人可扩展 Agent 平台——用户看到的是「智能体」，执行中枢是 MAF，把「铸造自己的 AI 助手」变成每个普通用户都能在自己机器上完成的事。**

## 产品定位

| 维度 | 定义 |
|------|------|
| **是什么** | Windows 本机个人 Agent 平台（托盘常驻 + 插件扩展 + 本地数据） |
| **用户可见插件名** | **智能体**（菜单、页面标题、功能清单、`plugin.json` 展示名）。插件 Id 仍 `ai-agent`，路由仍 `/ai-agent` |
| **执行中枢** | **MAF**（技术名；Microsoft Agent Framework 落地。架构层称 **MAF Runtime**）：任务循环、工具调用、审批与轨迹以 MAF 为中心组织，不把 MAF 当作插件名 |
| **入口形态** | Chat **只是任务入口之一**；真正驱动单元是 **Task / Run**（开放任务或计划任务） |
| **扩展方式** | Cordis 插件提供本机能力；内核 / MAF Runtime 只做编排与安全接缝（插件简化） |
| **用户** | 个人；不做团队共享、企业管理、云端计算机 |

## 阐述

- **本机优先**：数据、工具执行、审批与记忆默认留在用户自己机器上；可接本地模型（如 LM Studio），不强制云端绑定。
- **对外「智能体」、对内执行中枢「MAF」**：菜单、页面标题、功能清单与产品说明用 **智能体**。MAF 是执行中枢技术名（MAF Runtime / `MafTaskLoopAgent`），不是用户看到的插件名。能力远景（统一 Run、工具权限与审批、文件系统/终端、Skills、Routines/Scheduler、Browser、Memory）一律建在 **MAF + ToolRegistry + Cordis 插件** 上。
- **Task/Run 驱动**：用户目标进入 Run；工具统一走 **Permission → Approval → Execute → Verify → Result**；可取消、可留痕、可恢复。
- **插件化能力**：文件、终端、浏览器、调度、记忆等进插件；禁止再堆平行执行引擎 / LocalBot Runtime。
- **普通用户可及**：有设计稿驱动的桌面应用（Vue + 托盘），配置即可用，不是纯开发者 CLI。

## 我们不是（红线）

- **不是**云端计算机 / 远程沙箱 SaaS；
- **不是**团队协作或企业管理套件（账号体系、组织权限、共享工作区）；
- **不是**平行 LocalBot / 第二套 Agent Runtime（见 [ADR-005](../07-decisions/005-maf-local-agent-reuse.md) 精神 + [ADR-006](../07-decisions/006-maf-first-class-greenfield.md)）；
- **不是**「通用聊天框」产品——Chat 服务于任务，不以闲聊为终局；
- **不是**强制账号/订阅的云端绑定。

## 命名锚点（产品层）

| 对外说法 | 含义 |
|----------|------|
| **铸己匣** | 产品 / 宿主平台 |
| **智能体** | 用户可见插件名（菜单、标题、功能清单）。对内执行中枢技术名是 **MAF** |
| **开放任务** | 原 FreeLoop：对话入口驱动的任务循环（引擎 MAF Runtime） |
| **计划任务** | 原 PlanDriven：先规划再逐步执行、落 AgentRun 的任务（架构称 MAF Run） |
| **本机能力插件** | 文件 / 终端 / Skills / Scheduler / Browser / Memory 等 |

架构分层与代码标识对照见 [`../01-architecture/maf-centric-architecture.md`](../01-architecture/maf-centric-architecture.md)。
