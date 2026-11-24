---
id: 2026-11-23-dialog-overlay-white-mismatch
title: 技能管理弹窗白底与内容区不统一：亮块 overlay token 漏配
pc: PC-044
severity: P2
evidence: Verified
---
# Case：2026-11-23 技能管理弹窗浮层面漏配（PC-044）

## 1 现象
用户走查截图（技能管理弹窗）：弹窗背景偏白、与内容区不同族；暗色态弹窗正常暖深。

## 2 复现红灯
类级守卫「表面 token 接管」阳性对照：临时把亮块 `--el-bg-color-overlay` 还原 EP 默认 `#FFFFFF` → vitest 红（11 passed | 1 failed，红在 overlay）；还原修复态 → 绿 12/12。

## 3 根因（5 Whys + 第 6 问）
1. 弹窗白底与内容区不统一 → 浮层面用了 EP 默认纯白。
2. EP 默认值生效 → 主题亮块没接管 `--el-bg-color-overlay`。
3. 只接管了暗块 → 暗色批改造时按需清单只点了暗块。
4. 无两块对称性核对 → 检查器只查 token 存在性，不查「另一块留默认」。
5. 不变量：表面族 token 必须 :root / html.dark 成对接管（PC-044）。
- 第 6 问：为什么亮块漏配能存活到用户走查？——弹窗壳层视觉注意力全在「组件层样式」，主题层供给不对称是隐式失败，直到亮暗并排走查才可见。

## 4 同类扫查命中表
| 位置 | 同根因？ | 处置 |
|------|----------|------|
| 暗块全表面族 | 否（已接管） | — |
| 亮块其余 5 个表面 token | 否（已接管） | — |
| `--el-fill-color-blank` 两块留 EP 白 | 否（有意输入面对比，裁决不纳入） | 裁决记录入守卫注释 |

## 5 修复与被否方案
修复：亮块 `--el-bg-color-overlay: #FFFDF7`（= 本块 fill-color-lighter 既有值；零新增色值；两块同为「overlay 略亮于 bg」层级语义）。主题层单点 1 处，9 弹窗壳 + 全 EP 浮层统一受益。同走查批反馈②：13 处弹窗 header logo 20→26px（类级扫查宿主 9 + FileTools 2 + QuickLinks 自绘 2；TopNavbar :size=28 品牌位不属类）。
被否：逐弹窗组件层覆盖背景（治标、9 处重复、下次新弹窗再犯）。

## 6 重构提案与结论
G2 守卫已随批落地（surface describe）。R 提案：设计走查清单增加「浮层面与页面底同族核对」条目——已并入 ui-ux-design 走查清单回流（2026-11-23 批）。

## 7 走查环境差异（免误判）
- vite HMR 编辑主题文件丢暗块瞬态（硬刷新即复）；
- dark + 亮色 appearance 数据根背景（与主题独立，by-design）。
两者非产品缺陷，走查一律硬刷新后两态截证（screenshots/walk-theme-unify/）。
