# ADR-006：MAF 一等公民 + 绿场重设计（不做迁移兼容）

- 状态：Proposed → **命名两项已接受**（2026-11-02 用户拍板：路由不改；插件名「智能体」）。P0 审批/杀进程代码**未授权、未开工**。
- 关联：ADR-005（复用 MAF、不另起 Runtime）· ADR-001 · ADR-002 · [`00-vision/01-vision.md`](../00-vision/01-vision.md) · [`01-architecture/maf-centric-architecture.md`](../01-architecture/maf-centric-architecture.md) · pilot `docs/ai/pilot/2026-11-02-maf-first-class/`
- 背景：用户升级此前「只写文档、不重做 Runtime」窄口径——要把 **执行中枢 MAF 提升为一等公民**（指执行引擎，不是用户看到的插件名），产品与架构都以 MAF Runtime 为中心组织；对齐「本机个人 Agent Runtime」远景（统一 Run、审批、文件/终端、Skills、Routines、Browser、Memory）；**当前不考虑历史包袱，不做迁移**；符合插件简化；可废弃/替换旧命名与旧旁路。

## 决策

1. **执行中枢 MAF 是一等公民；用户可见插件名是「智能体」**。菜单、标题、功能清单、`plugin.json` 展示名用 **智能体**。文案写清：对外「智能体」，对内执行中枢「MAF」。架构层仍称 **MAF Runtime**。不把 MAF 当作插件名。铸己匣 = 本机个人可扩展 Agent 平台。
2. **继承 ADR-005 精神并升级口径**：仍 **不另起** LocalBot / 平行 Agent Runtime；能力建在 **MAF + ToolRegistry + Cordis 插件** 上。ADR-005 中「本批只写文档、不定产品品牌」的窄边界被本 ADR **取代**为「产品入口称智能体，架构与执行以 MAF 为中心」。
3. **绿场重设计、不做迁移兼容**：
   - 可直接改对外命名与模块展示名（已落地：插件名「智能体」）；
   - 可废弃 ReactLoop / ad-hoc / 旧 FreeLoop 品牌叙事；
   - **插件 Id `ai-agent` 与路由 `/ai-agent`、`api/ai-agent/*` 不改**（2026-11-02 已批，不改为 `/maf`）。DB 实体与枚举字面量的破坏性重命名不在本拍板内。
4. **插件简化**：本机能力进插件；内核与 MAF 只做编排与安全接缝；Scheduler 目标态触发 MAF Run；禁止再堆平行执行引擎。
5. **远景分阶段**：P0 执行安全（审批+取消杀进程）→ P1 文件/终端/Skills → P2 Scheduler→Run → P3 Browser → P4 Memory/本地模型（详见 `00-vision/02-goals.md`）。**单批不假装做完全谱**。
6. **明确不做**：云端计算机、团队/企业管理、平行 LocalBot Runtime、新建 `specs/`（延续 not-taken 050/051，并增补见下）。

## 依据

- 仓库事实：默认引擎已是 `MafTaskLoopAgent`（`UseMafRuntime=true`）；工具经 `RegistryToolFunction` → 六闸门。
- 用户裁决（2026-11-02）：执行中枢 MAF 一等（指执行引擎）；可重新设计命名与流程；绿场、不迁移；插件简化；先定真源再骨架。
- 用户拍板（2026-11-02 输入7，覆盖「展示名改为 MAF」与「路由另批」默认）：① 路由不改，插件 Id 仍 `ai-agent`，不改成 `/maf`；② 插件名改为「智能体」。本拍板**不授权**开始 P0 审批代码。
- 重复实现代价：平行 Runtime 与 MAF/ToolRegistry/会话日志分叉（ADR-005 已论证）。

## 后果

- **正面**：产品叙事与架构对齐；后续功能有统一挂载点；文档真源清晰（vision + maf-centric-architecture + 本 ADR）。
- **负面 / 成本**：对外文案与代码标识短期并存；契约重命名批次有回归面（用 pilot + 测试承载，不做兼容垫片）。
- **边界**：本 ADR **不定**审批 UI 实现细节、Browser 插件设计、DB 迁移脚本——留给对应 pilot。大规模业务代码改动仍须闸门1。

## 与 ADR-005 关系

| 项 | ADR-005 | ADR-006 |
|----|---------|---------|
| 不另起平行 Runtime | ✅ | ✅ 继承 |
| 复用 MAF + ToolRegistry | ✅ | ✅ 继承 |
| MAF 作为执行中枢一等公民 | 未强调（偏实现复用） | ✅ **升级**（技术名；插件名另见下） |
| 用户可见插件名 | 未定 | ✅ **智能体**（2026-11-02 已批；路由不改） |
| 绿场废弃旧名、不做迁移 | 未授权 | ✅ **新授** |
| 本批范围 | 当时只写文档定界 | 产品/架构真源 + 对外命名骨架；P0+ 另开 pilot |
