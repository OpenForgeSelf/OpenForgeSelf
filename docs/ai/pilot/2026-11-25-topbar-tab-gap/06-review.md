# 06 Review — topbar-tab-gap

date: 2026-11-25 ｜ 审查人: lead（先审后报；审出问题已修，V4-V5 同批交付）

## 九问

1. **需求是否全部满足？** 是。两轮指令逐条落地：v1 间距收紧（30→20px 实测）；v2 删除图标在右（槽位于标签右侧）、页签默认显示功能/插件 logo（mergeFeatureList + lucideIconSvg 单一源）、悬浮 logo 同位变关闭图标（同位互换、零位移）。
2. **实现是否与 Spec 一致？** 是。FR-1~FR-5 全部落地；Interaction Design 表逐项对得上（走查 V7-V9）。
3. **验证是否真实执行？** 是。快档三命令 + 守卫套件 + 浏览器量测 + 四态截图，全部亲跑，日志正文判定（05 §2，全部 Verified，2 项 Inferred 已标注）。
4. **不变量是否保持？** PC-042 悬浮零位移：**结构上强化**——v1 靠「槽常驻预留」，v2 升级为「logo 与动作图标同一 16px 盒、盒宽恒定」，任何状态布局零变化（V8 实测 zeroShift:true）。守卫随载体改造且语义保持（常驻占位 + visibility/opacity 显隐 + display 禁令）。
5. **是否引入新风险？** 无高危。`v-html` 内容全部来自仓内静态 SVG 表 `featureIcons.ts`（ICON_INNER 键值映射，无用户输入/外部字符串拼接），无 XSS 面；`aria-hidden` 槽不影响可访问性（动作图标有 title、键盘 focus-within 等效）。
6. **范围是否越界？** 否。业务面 = TopNavbar.vue + TopNavbar.test.ts 两文件；未触 tabs store、featureIcons、其他组件、后端（git status 可复核）。
7. **失败模式是否处理？** 未知图标名 → 空串空占位（不报错、槽留空、hover 互换不受影响）；无匹配功能页签 → 槽留空仍可关闭（FR-5）；flex 压缩 → 槽 flex:none。
8. **文档/工件是否同步？** 是。mini-task v2（含需求变更记录）、05/06/07 本批产出；日记逐回合记录；TODO 归档。
9. **可否交付？** 可。未提交（未经用户批准不 commit/push，Cursor Cloud §1）。

## Final Decision

**APPROVED**（自审；闸门2 交用户验收，验收通过前不提交）。

### 审出问题及处置（先审后报）

| 问题 | 处置 |
| --- | --- |
| 守卫断言误报（`.close-x:hover` 自身反馈被当作显现规则） | 修守卫过滤条件（`.tab-item:(hover\|focus-within)` 驱动才计入），实现未动；复跑 9/9 绿（V4→V5） |
| v1 量测暴露「带槽页签文字间距 42px 仍偏大」 | 即用户「效果不行」的根因——v2 以 logo 填充该区域解决（空间变成有意义内容），无需进一步压 padding |
| v3（14:10 用户反馈「图标在左」）| 模板调序（槽移到标签前）；互换机制/守卫/零位移性质不变。V11-V14 复验通过。审查维持 APPROVED |
| 门禁批环境事件（14:01-14:16）| pwsh 宿主 OOM/栈溢出 ×2、Node `Zone Allocation failed` ×1、全量 vitest tinypool `Channel closed` ×1——同源机器瞬时内存压力，非代码缺陷；分段重跑 + 限 `--maxWorkers=2` 后逐段取证（V11/V15/V16b）。教训记日记：内存压力时段长跑门禁要限 worker、分段重定向日志 |
