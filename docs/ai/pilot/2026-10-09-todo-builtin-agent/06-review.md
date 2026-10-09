# 06-review：todo 下发 → 本工具内置 AI Agent 链路

> 目录：`docs/ai/pilot/2026-10-09-todo-builtin-agent/`｜评审八问 + Final Decision

## 评审八问

| # | 问题 | 结论 |
|---|---|---|
| 1 | 需求是否全部满足？ | ✅ 用户主线「把 todo 任务交给本工具内置 AI Agent（非 AgentHub）并端到端跑通」已满足：委派 → 内置执行 → 状态回读 → 产物落地 → 记录留痕 → UI 徽标，全程运行实例实测（任务 50 → run:6 → Succeeded）。 |
| 2 | 实现是否自洽（契约/前端/后端/测试一致）？ | ✅ `BuiltInAgentRequest.Cwd` 契约 → StartAsync 设工作目录 → todo 传 ProjectRoot → 单测 Cwd 断言，四处一致；`engine=="builtin"` 分流、`run:` 前缀回读、状态映射词表对齐。 |
| 3 | 验证是否真实可复现？ | ✅ Verified：门禁（build 0 错 / 过滤集 32/32 / 前端 build / e2e 15/15）+ 打包（2.3.6.2610091212 签名 3/3）+ 运行实例升级 + 端到端七步（§2.5）全实测；唯一未自动化的是内置委派真实执行（依赖用户模型通道，不进 e2e，属已知取舍）。 |
| 4 | 是否引入回归？ | ✅ AgentHub 引擎零回归（e2e 15/15 含 E1 opencode 真实委派）；既有 todo 功能单测全绿。 |
| 5 | 边界与降级是否处理？ | ✅ 工作目录不可用（目录不存在/服务缺席）→ Fail 并给「请先在任务上关联项目」引导；接缝缺席 → 引擎下拉禁用 + 文案；StartAsync 60s 未产出 → 明确「后台可能仍在执行」而非硬失败割裂。 |
| 6 | 交互是否符合设计（ui-ux 走查）？ | ✅ A1 引擎/角色控件、A2 徽标口径、A5 记录留痕均在运行实例截图 OCR 核对通过（`screenshots/live-51888/todo-builtin-2.3.6-委派成功-ok-builtin.png`）。 |
| 7 | 文档/工件是否同步？ | ✅ 05-evidence 补齐 Cwd 修复闭环 + 运行实例端到端证据 + 交互符合性；06-review 本篇；TODO 残留已登记（workspace per-run 隔离 / 4B 默认模型引导）。 |
| 8 | 已知风险是否显式列出？ | ⚠️ ① workspace 全局单例：todo 委派会顺带切换 AIAgent 页面工作目录（per-run 隔离已记 TODO）；② 内置委派真实执行依赖用户模型通道（gpustack），通道不可用时无法跑通；③ 本批改动未 git 提交（用户未授权）。 |

## Final Decision

**APPROVED**

- 核心目标（todo → 本工具内置 AI Agent 端到端）已达成，证据链完整（Verified 为主）。
- 建议：后续迭代优先处理 workspace per-run 隔离与默认模型引导（P2，已登记 TODO）；模型通道恢复后可补 AgentHub opencode 成功回报终态复验。
