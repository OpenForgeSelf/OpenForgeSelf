# 06 Review — topbar-tab-icon-slot（顶部栏页签预留图标槽 · 悬浮零位移）

- date: 2026-11-23
- reviewer: lead（本会话自审，先审后报；按规范 §九阶段 Review 定义出八问 + Final Decision）

## 审查范围

- `ForgeSelf.Web/src/components/TopNavbar.vue`（`.close-x` 规则块重写，diff 局部）
- `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts`（追加 1 个 describe 块 4 条用例 + 读源方式对齐既有守卫）
- 问题类归档：PC-042 + case + 2 索引行
- pilot 工件：mini-task.md / 05-evidence.md / 06-review.md / 07-final-report.md

## 八问

1. **需求是否逐条满足？** 满足。用户四点诉求逐条对照：页签增加宽度（+22px 常驻槽位，Verified E4）/
   预留关闭固定图标位置（16px 槽常驻占位，Verified）/
   悬浮不变宽（hover 前后 width Δ0，Verified）/
   所有页签不位移（相邻页签 x Δ0，Verified）。
2. **实现是否最小且对位？** 是。单组件单规则块 + 守卫测试；未触碰 tabs store、其他组件、tokens、后端（Scope Forbidden 全程未越）。
3. **测试是否真跑、先红后绿？** 是。红：3 failed（18:00，断言级红非加载失败）；绿：8 passed（18:06）；
   全量回归 1270 passed（18:13）。过程偏差（jsdom URL、CSS 注释解析）如实记 05 §7。
4. **有无隐藏行为变更？** 有两处显性、可接受的行为变化，均已走查：
   ① 图标显隐从瞬时（display 切换）变为 160ms 淡入淡出（动效 token，符合 DESIGN.md 动效规范）；
   ② `visibility:hidden` 使隐藏态图标退出命中测试与无障碍树（原 `display:none` 同样退出，语义等价）。
   无其他隐性变更：DOM 结构、事件绑定、store 交互均未动。
5. **边界与兼容？** 首页签不渲染槽位（FR-4，量测 w=54.5 不变）；大量页签仅增加 tab-nav 滚动量（~22px/签，
   滚动容器吸收）；暗色主题无新颜色面（design:check 棘轮持平）；`flex:none` 防 flex 压缩（PC-019 lens 落实）。
6. **问题类闭环是否完整？** 完整：同类扫查（case §4，非同型命中均豁免有据）→ G1 守卫（红→绿实证）→
   PC-042 归档（invariant/signature/lens/canonical fix）→ 两索引 + 台账行 → `check-problem-classes.ps1` 42 PC 全过。
7. **证据等级是否诚实？** 是。E1–E8 全部 Verified（亲自实跑/实拍）；无 Inferred/Unknown 混入。
   走查服务实例与量测口径的偏差（并行 worktree 实例测 BEFORE、本 checkout 实例测 AFTER）在 05 §2/§7 显式披露，
   BEFORE 实例源码与修复前基线一致性有 computed cssText 逐属性比对背书。
8. **遗留与回流是否落位？** 落位：页签 × 键盘不可达（span 非 focusable，存量缺口）→ R1 提案记 case §6；
   dev-stack 端口假就绪缺陷 → TODO.md 新条目（本批不修）；commit 由用户批准后另行执行（工作区留存待审）。

## Final Decision

**APPROVED**（自审通过，可交用户验收；git 提交待用户明示批准后执行）。

- 依据：AC-1~AC-4 全 Verified（05 §1）；快档门禁全绿且为日志正文判定；问题类闭环机器校验通过；
  变更范围未越界；用户四点诉求全部量化达成。
