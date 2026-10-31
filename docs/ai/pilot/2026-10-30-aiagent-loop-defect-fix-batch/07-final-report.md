# 07 Final Report — PILOT-077 AIAgent 目标循环缺陷修复批（D1-D6）

> 任务：修复 2026-10-30 验收暴露的 6 项缺陷（4 项用户截图指认 + 2 项走查发现），插件 ai-agent 1.9.8 → 1.9.9。

## 1 交付摘要

| 缺陷 | 修法 | 载体 |
| --- | --- | --- |
| D1 goal 事件致前端「连接失败」 | SSE payload 封套 +2 case | AgentRunsController |
| D2 工具轨迹恒空 → 评估器零证据误判卡死 | 帧循环聚合轨迹落库 + 步骤产出/轨迹随行评估提示词 | RunOrchestratorService + GoalPursuitService |
| D3 I/O 块亮色模式黑底 | 硬编码色 → --aiw-* token | blocks.css |
| D4 步骤时间线压缩裁切成残行 | 子项 flex-shrink:0 + plan-body overflow（同类 3 处） | RunRecordPanel.vue + widget.css |
| D5 工具块「默认收起」反馈 | 实测不复现（四时点全展开）→ 守卫测试锁行为 + 向用户求证 | BlockRenderer.test.ts |
| D6 工具内容与文本不对齐 | 卡片内横向 padding 归零，I/O 块落到正文文本线 | blocks.css |

## 2 验证结论（Validation · 快档，来源等级见 05）

- Review：06-review 八问 + Final Decision = **APPROVED**（2026-10-31 gate-2 推荐，用户验收见闸门2）。

- 新守卫 9/9（先红后绿）；AIAgent 过滤集 120 通过、15 红全部为已归档基线（零新增红）；插件 web build ✓；BlockRenderer 22/22；design:check 双 PASS（硬编码色较基线降 742）；ai-agent 冒烟 e2e 7/7。
- 真机走查：goal 事件渲染正常（D1）；Run 4 真实工具轨迹落库（D2：`list_files → 成功 · 20ms`）；亮色主题化 + 对齐线 286 + 默认展开 11/11 + 步骤零裁切（D3/D4/D5/D6，截图 ×2）。
- 未现场取得：goal achieved→Completed 真机 happy-path（gpustack 8B 当前带工具结果的请求 45-70s，撞 45s 步骤超时；修复前既有参数问题）——该路径由单测确定性覆盖（脚本化 write_file→complete_step→achieved→done）。

## 3 问题类归档（problem-class-closure 闭环）

- **PC-019**（新）：flex 列滚动容器子项缺 flex-shrink:0 → 压扁/残行；容器内 max-height 无 overflow → 叠画。守卫 G2 = e2e 几何锁（flexShrink==='0' 且 clipped≤4px），active。case：`2026-10-31-aiagent-flex-shrink-compression`（同类扫查命中 3 处同批修 + Home 先发例记录）。`scripts/check-problem-classes.ps1` **19/19 OK**。
- **SSE 契约漂移**未独立建 PC：D1 属「新增事件类型未同步消费侧封套映射」，单测封套守卫已把五类+专型+goal 三组字节面全部锁定（该签名组即守卫），复发再评估建类。

## 4 重构提案（小范围，未实施）

- `MarkStepStuck` 恢复循环覆写步骤轨迹：前轮证据丢失（P3，TODO3）——建议轨迹按轮次合并而非整列覆写。
- MAF 路径轨迹聚合补 MafTaskLoopAgent harness 单测（P2，TODO2）。

## 5 悬空教训清单（新模板节，应为空）

- [x] 本批无悬空教训——三条新 TODO（下）均已有归属、优先级与方向，非悬空。

## 6 新 TODO（回流队列）

- TODO1（P2，宿主侧）：TurnFrame.ToolCompleted 携带结果摘要，让评估证据含结果全文（本批帧契约禁触未做）。
- TODO2（P2，测试基建）：MafTaskLoopAgent harness 补轨迹聚合单测（现靠真机实证）。
- TODO3（P3，产品参数）：步骤 45s 超时对慢模型过紧（超时可配/自适应）+ stuck 恢复覆写轨迹合并。

## 7 发布与后续（待用户动作）

1. **闸门2**：本报告 + 05/06 交用户验收。
2. **闸门3**：验收通过后提交归档（pre-commit 工件链门禁已就绪）。
3. 插件发布：默认本地目录更新源（`release-local.ps1 -UpdateDir`），需用户同意后执行；:51888 运行实例更新后再做只读复验（第⑤步）。
4. dev-stack（:7301/:7399）仍在运行，供用户现场复查走查现象（账号 token 见 `.temp/dev-stack.json`）。

## 8 悬空教训清单（必须为空）

- 无——本报告发现的全部问题均已落档：PC-019 建档守卫（§3）、TODO1-3 回流队列（§6）、D5 用户求证记录在 05、SSE 契约漂移由封套守卫即档案（复发再评估建类）。
