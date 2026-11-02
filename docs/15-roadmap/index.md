# 15-roadmap — 路线图（执行层）

> 状态：活队列以 `TODO.md` 为准；阶段目标以愿景为准
> 最后更新：2026-11-02

> 本目录排"下一步做什么"。当前**活队列在 `TODO.md`**（消费式管理，完成即移除）。
> **产品阶段真源（MAF P0–P4）**：[`../00-vision/02-goals.md`](../00-vision/02-goals.md)  
> **架构命名**：[`../01-architecture/maf-centric-architecture.md`](../01-architecture/maf-centric-architecture.md)  
> **当前 pilot**：[`../ai/pilot/2026-11-02-maf-first-class/`](../ai/pilot/2026-11-02-maf-first-class/)（闸门1 待批）

## MAF 阶段（摘自 02-goals，勿在此复制细节）

| 阶段 | 主题 | 下一动作 |
|------|------|----------|
| P0 | 执行安全底座（审批 + 取消杀进程） | 闸门1 后开 `maf-tool-approval` / `maf-cancel-kill-process` |
| P1 | 文件/终端/Skills | P0 后 |
| P2 | Scheduler → MAF Run | P1 后 |
| P3 | Browser 插件 | 立项技能先行 |
| P4 | Memory / 本地模型 | 后置 |

## 当前已知 backlog（摘自 TODO / 历史）

以 `TODO.md` 为准；下列仅为索引提示，可能过期：

| 项 | 优先级 | 说明 |
|----|--------|------|
| 执行中枢 MAF（插件名智能体） | P1 | 路由不改、插件名已批（2026-11-02）；P0 审批代码未开工 |
| 聊天界面模型选择动态化等 | — | 见 TODO.md |

## 下一步决策

新功能提案评审：先对照 `00-vision/03-principles.md`（含 P9 MAF 一等 / P10 插件简化）→ `01-vision.md` → `02-goals.md` 阶段，再排期；落地写 `02-features/` 与 `docs/ai/pilot/`。
