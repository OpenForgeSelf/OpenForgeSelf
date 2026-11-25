# 01 Intent — 统一主题修复批（el-token 全项目收敛 + 弹窗标准化）

## Problem（为什么做）
审计批（2026-11-20-el-token-unify）实证全项目 **6021 处样式 token 违反**（自定义 4392 / 硬编码 1522 / 未知 el 107）散落在 16 域，主题切换语义失控；另有 **9 个自造 modal** 绕开 Element Plus 组件体系，其中 SkillsView 弹窗浅色对比度 1.06:1 不可读。这些是输入11 指出的「固定 token 写死 + 自造轮子」结构问题的存量实体。

## Why
- token 单一来源才能让亮/暗主题、未来主题扩展一次性生效；任何一处漏配都直接呈现为可见缺陷（输入9/输入10 已两次实证）。
- el-dialog 等既有组件自带焦点圈闭、对比度、无障碍与行为一致性，自造 modal 每个都是独立缺陷面。

## Expected Outcome（做什么）
1. 全项目页面/组件样式只引用 `--el-*` token（或 R6 登记豁免项），`check:theme` 报 0 违反。
2. 9 个自造 modal 全迁 `<el-dialog>`，SkillsView 1.06:1 缺陷随之消除。
3. `check:theme` 接入根 check 链 + pre-commit + CI；`check:theme:demo` 反模式 fixture 32 处全捕获常驻回归。
4. 检查器五件套 + 迁移器两件（备份独存资产）随本批入库。

## Constraints（边界）
- 等值映射不改观感（暗色严格等值；浅色 --gold-400→--el-color-primary 收敛为 #B45309 属 03-plan 已记录的既定取舍，走查确认）。
- Element Plus 不升级；`forgeself-design/`、`ct/` 不在扫描范围；DESIGN.md/package.json 主检出在途件不碰。
- 主题文件（themes/*.css）仍是唯一值定义点（R4）；本批不改主题视觉基调。
- 发布动作（本地目录发布/tag/push）不在本批（须用户明示）；本批验证边界 = build + 冒烟 e2e + 亮/暗走查。

## Success Criteria（到什么程度）
见 02-spec §Acceptance Criteria（AC1–AC8），全部可机器判或截图判。
