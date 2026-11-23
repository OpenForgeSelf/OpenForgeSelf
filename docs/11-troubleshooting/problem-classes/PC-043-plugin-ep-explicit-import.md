---
id: PC-043
title: 插件前端 SFC 用 Element Plus 组件未显式 import，独立预编译产物下组件缺失
tags: [插件前端, Element-Plus, 构建产物, unplugin, 运行时崩溃]
invariant: Plugins/<X>/web/src 的 SFC 模板用到 <el-*> 组件时必须显式 `import { ElXxx } from 'element-plus'` 并在 components 登记（或全局注册清单覆盖）；插件 web 是独立预编译产物，不享受宿主的 unplugin 自动导入
status: guarded
first_seen: 2026-11-23
recurrences: []
cases: [2026-11-23-plugin-dialog-elimport-missing]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/src/guards/plugin-ep-import.test.ts"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Web/src/guards/plugin-ep-import.test.ts", status: active}
lens:
  - 插件 SFC 里新用了一个宿主没用过的 EP 组件？先查该插件的显式 import 清单——unplugin 自动导入只在宿主 ForgeSelf.Web 生效。
  - 插件本地 build 绿不代表运行时组件解析成功——产物是预编译的，缺 import 在加载期才暴露。
  - 迁移自造 modal → el-dialog 时，import 行是否跟模板同步落地？
---

# PC-043 插件前端 SFC 用 Element Plus 组件未显式 import，独立预编译产物下组件缺失

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

**不变量**：插件 `web/src` 的 SFC 模板用到任何 `<el-*>` 组件，必须显式 `import { ElXxx } from 'element-plus'`。宿主 `ForgeSelf.Web` 用 unplugin 自动导入（AGENTS.md 编码规范「禁止显式 import」只适用于宿主），插件 web 是独立预编译产物（vite lib 构建，无 unplugin 插件链），缺 import = 运行时组件解析失败。

**为什么会犯**：
- AGENTS.md §4.2「禁止显式 `import { ElXxx } from 'element-plus'`」的本意是宿主侧 unplugin 语境，读规范时把它当成了全局规则，迁 `<el-dialog>` 时就没写 import；
- 插件本地 `pnpm run build` 对模板里未解析组件不报错（vue-tsc 按全局组件类型放行），构建绿掩盖了运行时缺件。

## 2 特征签名与扫描方法

- **S1**（类级守卫，G2）：`ForgeSelf.Web/src/guards/plugin-ep-import.test.ts`——静态扫查全部插件 SFC：出现 `<el-dialog` 用法 → 同文件必须含 `import { ElDialog } from 'element-plus'`。范围先锁 el-dialog（弹窗迁移 FR5 主组件），其余 el-* 组件按迁移批增量纳入。
- 教训记录：首版 grep 负向 lookahead 签名方向写反（import 在用法之前，lookahead 向后看 400 字符永不命中 → 4 个已修文件全误报）且 grep 单正则表达不了「文件级有用法无导入」结构条件——**结构条件一律上 test 签名**，2026-11-23 W7 批实证。

## 3 修法

SFC 显式 `import { ElDialog } from 'element-plus'`（迁移的每个 EP 组件一条）；宿主壳层侧自定义 class 见 PC-044 的配套规则。

## 4 关联

- case: `docs/11-troubleshooting/cases/2026-11-23-plugin-dialog-elimport-missing.md`
- 与 PC-044 是同一迁移批的两面：①组件怎么进产物（本条）②样式怎么进产物（PC-044）。
