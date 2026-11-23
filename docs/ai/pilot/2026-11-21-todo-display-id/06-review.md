# PILOT-085 · 06 Review（先审后报 · Final Decision: APPROVED）

> 审查对象：mini-task.md 方案 + 05-evidence.md 实测 ｜ 审查人：lead（同会话自查，证据均落盘可复核）

## 八问

1. **需求是否逐条满足？** ✅ 用户输入10 的诉求「界面上对不上 #93」→ 四处渲染面（列表行/看板卡/专注列表/抽屉）均现 `#<数字id>`，且与 agent 工具引用语义（list_todos/complete_todo 的数字 Id）同源——e2e 以「界面 #N == REST 回读 id」身份锚定，非仅可见性断言。
2. **实现是否完整自洽？** ✅ 数据链路零改动（TodoDto.Id 已有）；样式全 token（design:check 基线持平）；`#run:…` 委派串保留并与其分层（行顶编号/meta 委派串），title 提示语义明确；顺带纠偏 `.td-key` 的「任务编号：Guid」误导指称（同根因面：编号语义混淆）。
3. **验证是否真实执行？** ✅ 红灯/绿灯/三守卫/走查/对比度全部实跑，日志与截图落盘（见 05 §1-5）；无编造项。
4. **未覆盖场景？** ⚠️ 三项，均记录：① 专注列表仅走查覆盖、e2e 未断言（需把任务推到 Review 态，冒烟从简原则下取舍；同类风险低——专注列表与列表行同源 `task.id` 渲染）；② ⑤运行实例复验未做（依赖④发布获批）；③ AIAgent/Home 同根因渲染面未修（范围控制，TODO P2 另批）。
5. **回归风险？** 低：仅新增 span + 新类 `.tt-tid`/`.td-tid`（grep 确认不与既有类名冲突）；e2e 既有断言全数通过；宿主 1273 单测绿。
6. **范围是否越界？** 无：2 vue + 1 spec + 1 brief + 工件；AIAgent/Home 命中即记 TODO 不顺手改（§1.3 + problem-class-closure 异插件分流）。
7. **文档是否同步？** ✅ ui-design-brief 补「编号徽标」行（设计师向）；05/06/07 齐备；日志/TODO 收口见 07。
8. **悬空教训？** 见 07 §三：①「接口字段有、渲染面漏显」类（AIAgent TodoEntry/AgentWorkspace/Home 面板同病，P2）；② Guid 被 title 称「任务编号」的语义漂移——详情头已纠，其余插件如有同样写法需在 P2 批次顺带排查。

## Final Decision

**APPROVED** —— 代码面可交付（源码+dist+e2e 绿+走查绿）；④发布与⑤运行实例复验按 mini-task §0-Q2 挂起待用户明示，不构成本批代码完成度的阻塞项，但**未完成前插件维护闭环不算全闭环**（plugin-development §四），07 状态如实标 ⚠️。
