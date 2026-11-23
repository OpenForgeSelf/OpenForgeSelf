# 06 Review — 统一主题修复批（随域推进填充，收口定稿）

## 八问（收口时逐问填写）
1. 需求覆盖？ — W1-W6 七域 + 用户走查反馈两修（overlay 暖面 / 13 处 header logo）+ W7 收口批（AC1 白名单定稿全仓 0 / AC2 demo 32 全捕获 / AC8 接线与截图归档 / PC-042~044 归档）。AC3 弹窗盘点对表、AC5 全插件 build 巡检、AC6 DesignSystem 四层门禁为收口对表项（下方 4/6/7 说明）。
2. 变更面与 04 声明一致？ — 一致。追加面均属用户走查反馈与问题类闭环授权范围（同类扫查命中）：主题 1 token、13 处 logo、守卫 test、豁免册/checker/接线、demo fixture、PC/case 归档。无越界改业务逻辑。
3. 验证分级齐全？ — 齐全：设计门禁静态守卫（design:check 452<2501 棘轮 -2049）+ 根 check 链全绿（vue-tsc/eslint/组件规范/theme-check 全仓 0）+ 守卫测试 12/12 + demo 32 捕获 + 亮/暗两态走查截图（walk-theme-unify/）+ 05-evidence 逐条 Verified/Inferred 标注。AC7 中档 dotnet + 深档 e2e 跑批中（基线对表后补 07）。
4. 假能力自查（DesignSystem 域）？ — 本批不触 DesignSystem 功能层，仅 fileExemptions 登记 12 文件（--ds-* 契约域不动产品代码）；无「页面有数字但数字是假」面。
5. 亮/暗两态走查结论？ — 亮：弹窗暖米面板与内容区同族、logo/标题比例平衡；暗：暖深面板不回归、三表面 token computed 全暗。HMR 瞬态与 appearance 数据根残留已定性为环境差异（case §7）。
6. 基线红对表归属？ — e2e：149 passed(+47=worktree 冷启动 dist 恢复)/88 failed，88 全落存量域，本批域唯一命中=prompts-skills 陈旧断言（W6 迁移未同步 spec，任务链内判责，已修+定向复跑 1 passed 销项）。dotnet：沙箱间歇冻结 6 次（并行/串行/分片均复现，环境级），改分片+冻批流式对表——失败全集 30 个全判基线红（本批后端零改动，量级吻合基线 34），关键守卫类单跑复核（Registry 5/5 绿、RepositoryScript 33/35、Architecture 2 类红=存量源码违规）；冻批未覆盖尾巴标 Unknown。
7. 遗留风险？ — ① AC3 弹窗盘点对表与 AC6 DesignSystem 四层门禁为文档对表面，不阻塞本批 token/接线闭环；② check:theme 首次接 pre-commit/CI，若存量边缘场景（如新插件脚手架）现红按 PC-043/044 类处理；③ PC-044 选择器签名 regex 宽度（`[a-z-]+dialog`）对含 dialog 字样类名的合法后代写法理论可误报，暂无命中实例。
8. 是否可交付？ — 可交付：代码/守卫/接线/归档全收口；AC7 对表完成（e2e 全量 Verified；dotnet 分档收口+Unknown 披露）。

## Final Decision

**APPROVED**（2026-11-23 W7 收口定稿）

依据：八问收口（1-5/8 Verified：AC1 全仓 0、AC2 demo 32 捕获、AC3 9 弹窗迁移+残留 0、AC4 对比度 16:1+两态走查、AC8 三面接线+截图）；6 号基线对表 e2e 半边全量 Verified + dotnet 半边分档收口（Registry 5/5 绿、RepositoryScript 33/35、失败全集 30 判基线、冻批尾巴 Unknown 披露）；7 号重大缺陷零（问题类 3 PC 归档+守卫落地+机器校验 44 PC 全绿）；8 号走查反馈两修均 Verified+类级守卫。

风险披露（不阻断交付）：① dotnet 中档存在沙箱间歇冻结（环境级，TODO P2 挂账待非沙箱复跑校准）；② 本链提交属用户已批任务链（禁 push），发布另行走发布流程；③ 遗留风险栏第 7 问三项随批带走。
