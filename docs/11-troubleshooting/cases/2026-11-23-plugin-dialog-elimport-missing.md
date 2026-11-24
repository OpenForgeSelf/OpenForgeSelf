---
id: 2026-11-23-plugin-dialog-elimport-missing
title: 插件弹窗迁移批：EP 显式导入缺失与复合选择器死规则双缺陷
pc: PC-045
severity: P2
evidence: Verified
---
# Case：2026-11-23 插件弹窗迁移双缺陷（PC-045 / PC-046）

## 1 现象
W6 弹窗迁移批走查：插件弹窗首次渲染时 EP 组件缺失/样式不生效（预编译产物运行时暴露）；壳层覆盖样式按后代选择器写法写完零效果。

## 2 复现红灯
迁移文件对照检查：`<el-dialog>` 无 `import { ElDialog }` 的文件 + `.x-dialog .el-dialog` 后代选择器命中即红（S1 grep）。W6 批修复后同类扫查 9 文件全绿。

## 3 根因（5 Whys + 第 6 问）
1. 插件弹窗缺组件/样式死规则 → 插件 web 产物没带 EP 组件解析/壳层选择器不匹配。
2. → SFC 未显式 import；选择器按「外层包内层」直觉写。
3. → 宿主语境规范（unplugin 自动导入、custom class 旧版记忆）被平移进插件语境。
4. → 插件 web 是独立 vite lib 产物，无 unplugin 链；EP 2.14.3 custom class 与 .el-dialog 同根。
5. 不变量：插件 SFC 用 EP 组件必显式 import（PC-045）；壳层覆盖必复合选择器 `.el-dialog.x-dialog`（PC-046）。
- 第 6 问：为什么插件本地 build 绿？——vue-tsc 对全局组件名按类型放行，运行时才暴露；产物差异类缺陷本地构建不设防。

## 4 同类扫查命中表
| 位置 | 同根因？ | 处置 |
|------|----------|------|
| FileTools RenamePanel/CleanupPanel | 是（PC-046 选择器面） | 已修（复合选择器定式） |
| QuickLinks QuickLinksView/LinkFormModal | 是（自绘壳同改 26px logo） | 已修 |
| 宿主 9 个迁移文件 | 是（import + 复合选择器双面） | 已修并复查 |

## 5 修复与被否方案
修复：每个迁移文件补显式 import；壳层样式统一 `.el-dialog.x-dialog` 复合 + `:deep()` 下钻内部。
被否：给插件 web 引 unplugin 自动导入（产物构建链复杂化，与「独立预编译」架构裁决冲突）。

## 6 重构提案与结论
插件前端基建统一时升级 G2（vitest 静态解析 SFC 的 EP import 完整性），挂 TODO（P2，来源:输入24 批）。当前 G3 grep 签名承接。
