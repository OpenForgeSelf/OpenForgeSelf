# 07 Final Report：IM 网关会话视图方向/几何修复批（2026-10-31）

## 验证结果（Validation）与审查（Review）

- **e2e（快档 + 双守卫）**：`e2e/plugins/im-gateway/im-gateway.spec.ts` **4/4 绿**（57.3s，真实 publish 宿主）——既有 2 例不动仍绿；新几何锁（120 字压力下侧栏 ≤360 实过 + 主>侧 + 省略生效）与新语义锁（out=flex-start 左 / in=flex-end 右 + out cx < in cx）双绿；徽标 v2.1.1 对真宿主验证（05 §E2）。
- **起源码走查（插件门禁③）**：隔离 publish 实例（:7321，独立数据根/实例 ID，未触 :51888 与他人 dev-stack）+ 70+ 汉字压力种子：侧栏 **300px 恒定**、主栏 658px、零横向滚动、预览省略（647→211px）、回复=左（cx 594）< 来讯=右（cx 865）；亮/暗双主题截图读图通过（05 §E3）。
- **设计门禁**：`design:check` PASS（style-tokens 1759<基线 2501 零新增；棘轮 a=209/b=0/c=0/d=0）。
- **中档门禁（scripts/** 触发）**：全量 3273 过/80 败——**归本批 2 红（BOM 剥离，lead 认领）已修复**：RepositoryScriptTests 28/28 复跑绿；其余 78 红分桶=OutDir 环境红 + 既有基线（Inferred，正常 bin 终判待锁释放，TODO 登记）。
- **问题类闭环机器校验**：`check-problem-classes.ps1` OK（19 PC）；`ProblemClassRegistryTests` **5/5 绿**。
- **未跑**：深档全量 e2e（未触 e2e 基建，§5.6 不要求）；前端宿主三件套（零宿主前端变更面）。
- **Review**：八问 + Final Decision=**APPROVED** 见 [06-review.md](06-review.md)。

## 交付状态

- **状态**：✅ 实现与验证完成（**闸门2 待用户验收**；验收通过前不提交 git）
- **目标达成**：输入9 两缺陷全闭环（D1 几何 + D2 方向）+ 双 e2e 守卫 + 走查数值证据 + 规范/台账全链沉淀（输入11「先更新」部分另账完成）。
- **运行实例现状**：:51888（v2.1.0 插件）仍带缺陷行为，**修复随下次发布生效**（按 §8.5 等用户点头；步骤⑤ 只读复验待 :51888 更新后做）。

## 主要变更

| 文件 | 变更 |
|------|------|
| Plugins/ImGateway/web/src/ImGatewayView.vue | D1：`.ig-chat__side` + `min-width:0`（保留 300px 基准宽，注释引 B3 双面铁律） |
| Plugins/ImGateway/web/src/components/ChatThread.vue | D2：方向对调——out=左（flex-start）/ in=右（flex-end），注释留痕需求拍板日期 |
| Plugins/ImGateway/plugin.json | Version 2.1.0 → 2.1.1 |
| ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts | 新增几何锁 + 语义锁（压力种子 sqlite3 直插 + 文件级 serial 化） |
| docs/11-troubleshooting/ | PC-019 扩面（双面不变量/lens/S2 签名/守卫台账）+ case `2026-10-31-imgateway-minwidth-overflow`（新）+ cases/README 补 2 行（含 PILOT-077 批漏登行）+ problem-classes/README 索引更新 |
| docs/ai/pilot/2026-10-31-imgateway-layout-direction-fix/ | mini-task（AC 全勾）+ 05 + 06 + 07 |

## 问题类归档（技能 problem-class-closure）

- **PC-019 扩面（min-width 面复发）**：不变量升级为「flex/grid 子项尺寸约束双面」（shrink 面 + min-width 面）；lens +2 问句；签名 S2（压力数据几何锁）；守卫 G2 ×2（ai-agent + im-gateway）；case `2026-10-31-imgateway-minwidth-overflow`（逃逸定性：台账只收 shrink 面 + 短种子走查不可见）。
- **规范上浮**（与台账配套，输入11 授权）：DESIGN.md 新建页面 §7 双栏最小规范 / §8 方向语义=需求拍板项；agent-workflow B3「flex 尺寸约束双面铁律」；ui-ux-design 清单 +3 项（比例留痕/长文本压力/方向确认）；`verify-pilot-artifacts.ps1 -EnforceInteraction` + pre-commit + CI gate 接线。
- 机器校验：check-problem-classes OK（19 PC）+ Registry 5/5。

## 重构提案

- **R0（已执行）**：`min-width:0` 为语义正解的最小修复，不重构。
- **R1（候选小批，入 TODO 待批）**：跨插件同类扫查登记的 4 组低风险点（TodoTracker `.tt-list-panel` 内部 nowrap 无 overflow / `.tt-board-column` 列宽漂移 / AgentHub `.ok-evt__type` 溢出可见 / DesignSystem 展示 section 8 处 nowrap 无配对）——均为「布局不破只溢出/漂移」，另立小批统一处理。

## 悬空教训清单

**空**。三项后续（正常 bin 全量终判、:51888 只读复验、跨插件 R1 小批）均已落 TODO.md（含来源与优先级）。

## 遗留与风险

- 正常 bin 位置全量终判未跑（dev-stack DLL 锁，他人进程不碰）——78 红归因 Inferred；本批改动面（插件 CSS + 文档）不含后端源码，引入后端红的路径不存在，但仍按留终判项。
- :51888 实际更新后的只读复验（步骤⑤）未做——须用户更新实例后进行。
- e2e 文件级 serial 化属测试基建行为变更（非断言变更），已如实交代。
