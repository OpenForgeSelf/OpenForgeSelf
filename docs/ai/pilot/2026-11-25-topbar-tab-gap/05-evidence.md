# 05 Evidence — topbar-tab-gap（v1 间距收紧 + v2 logo/关闭图标同位互换）

date: 2026-11-25 ｜ 执行: lead ｜ 等级标注: Verified（亲跑拿到真实输出）/ Inferred / Unknown

## 1. 变更清单（实际发生）

| 文件 | 变更 |
| --- | --- |
| `ForgeSelf.Web/src/components/TopNavbar.vue` | v1: `.tab-item` padding `6px 14px`→`6px 9px`；`.active::after` inset `12px`→`7px`。v2: 模板改「`.tab-label` + `.tab-icon-slot`(16px, flex:none, position:relative)」；槽内 `.tab-logo`（`lucideIconSvg(icon,14)`，按 path 匹配 `mergeFeatureList`）与 `.close-x`（absolute inset:0，📌/× 语义不变）同位互换；script 增 `logoByPath` computed + `tabLogo()`；样式增槽/logo/互换规则（显隐一律 visibility+opacity，160ms `--dur-fast`/`--ease-out`） |
| `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts` | 守卫改造：不变量载体 `.close-x`→`.tab-icon-slot` + `.tab-logo`；断言 ①三类规则禁 `display:none` ②槽 16px/flex:none/relative ③× absolute+inset:0+visibility:hidden ④logo 默认 visible、页签 hover/focus 隐（visibility 语义；过滤只取 `.tab-item:(hover\|focus-within)` 驱动的规则，排除 `.close-x:hover` 自身反馈）⑤DOM 槽/logo/× 计数 + ≥4 个已知功能 logo 渲染 SVG。8 条 → 9 条 |

## 2. 验证记录

| # | 验证项 | 命令/方法 | 结果 | 等级 |
| --- | --- | --- | --- | --- |
| V1 | v1 守卫回归（改前 8 条） | `pnpm exec vitest run src/__tests__/TopNavbar.test.ts` | 8/8 passed | Verified |
| V2 | v1 快档 | `pnpm run check` + `pnpm exec vitest run` + `pnpm run design:check` | check PASS（component-conventions PASS）；vitest 123 文件/1277 通过；design:check PASS（a=170 < 基线 209） | Verified |
| V3 | v1 走查量测 | dev:web :7002 + `getBoundingClientRect` | 相邻页签文字间距 30px→20px（首页→智能体）；带槽边界 52→42px；按钮间距 gap 2px 不变；页签宽各 −10px（79.8/93.1/93.1/93.1/44.5） | Verified |
| V4 | v2 守卫首轮 | 同 V1 | 8/9：`logo` 用例红——`.close-x:hover`（图标自身反馈，无 visibility）被误纳入显现规则断言。修正守卫过滤（非实现缺陷） | Verified |
| V5 | v2 守卫复跑 | 同 V1 | **9/9 passed**（6.26s） | Verified |
| V6 | v2 快档 | 同 V2 | check exit 0 / `[component-conventions] PASS`（`no-explicit-any` 警告在 BackgroundSettings.vue、EmptyState.vue——非本批文件，存量，§5.5 Warning 不阻断）；vitest **123 文件/1278 通过**；design:check 双段 PASS（style-tokens 较基线 −2049 处） | Verified |
| V7 | v2 静态走查 | Playwright evaluate | 4 个非首页签 `.tab-icon-slot` 恒 16px、`.tab-logo` 含 `<svg>`、computed visibility=visible、`.close-x`=hidden；首页签无槽 | Verified |
| V8 | v2 hover 零位移 | hover「任务中心」前后页签宽对比 | `zeroShift:true`（44.5/79.8/93.1/93.1/93.1 全等）；hoverLogo=hidden、hoverX=visible | Verified |
| V9 | v2 截图读图（亮/暗 × 静态/悬浮） | Playwright 截图 + 逐张读图 | 亮静态 `gap2-idle-light.png`：4 页签 logo 呈现（bot/wrench/monitor/clock）、首页无图标；亮悬浮 `gap2-hover-light.png`：任务中心胶囊内 × 现、logo 隐；暗静态 `gap2-idle-dark.png`、暗悬浮 `gap2-hover-dark.png`：同位互换与配色（currentColor）正常 | Verified |
| V10 | 图标数据面 | 读 `featureIcons.ts` / `features.ts mergeFeatureList` | 内置 4 页签 icon 齐备；插件菜单缺省 `package`；未知图标名返回空串（空占位不报错）——Inferred（静态代码确认，插件页签实际渲染未在本次走查出现） | Inferred |

截图存档（gitignored 工作区证据）：`ForgeSelf.Web/screenshots/dev/gap2-idle-light.png`、`gap2-hover-light.png`、`gap2-idle-dark.png`、`gap2-hover-dark.png`（v1 过程件 gap-after-*.png 同目录留档）。

## 3. AC 核对

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC-1 守卫套件（改造后）绿 | ✅ | V5（9/9） |
| AC-2 logo 渲染 + hover 零位移 + 间距收紧 | ✅ | V7/V8/V3 |
| AC-3 快档全绿 | ✅ | V6 |
| AC-4 亮/暗两态截图读图 | ✅ | V9 |

## 4. 偏差与迭代记录

- 一次守卫断言误报（V4）——守卫过滤条件过宽，修测试不改实现；第 2 次验证通过（§6 迭代控制内）。
- MCP 截图输出根定位绕行：`browser_take_screenshot` 绝对路径被拒后暴露 DSH 会话配置目录下的 `.playwright-mcp` 允许根（具体盘符路径按提交卫生门禁省略，换机按「DSH 会话配置目录/.playwright-mcp」定位）；Copy-Item 入 `ForgeSelf.Web/screenshots/dev/`（gitignored）。
- 环境前置按 §5.0：NO_PROXY/TEMP/TMP 全程设置；日志正文判定（V1-V6 均读 log body）。

## 5. Unknown / 未覆盖

- 插件贡献页签（`route` 动态注册）的 logo 实际渲染：数据面确认有缺省值（`package`），但本次走查会话未打开插件页签（Inferred，见 V10）。
- 键盘 focus-within 互换：CSS 规则与 hover 同源（结构保证），未单独键盘走查。

## 6. v3 增补（14:10 用户反馈「图标在左，而不是右」）

变更：`TopNavbar.vue` 模板仅调序——`.tab-icon-slot` 移到 `.tab-label` 之前（`[槽][标签]`）；互换机制/槽规格/守卫断言不动（守卫为选择器断言，与序无关）。

| # | 验证项 | 结果 | 等级 |
| --- | --- | --- | --- |
| V11 | 守卫套件复跑（v3 模板） | 9/9 passed（15.12s） | Verified |
| V12 | 图标在左量测 | 4 个非首页签 `order=icon-left`（槽 right ≤ label left），槽恒 16px、logo SVG 齐备 | Verified |
| V13 | hover 零位移复测 | 页签宽 44.5/79.8/93.1/93.1/93.1 前后全等（zeroShift:true）；hoverLogo=hidden / hoverX=visible | Verified |
| V14 | 四态截图读图 | 亮静态 `gap3-idle-light.png`（图标在左）、亮悬浮 `gap3-hover-light.png`、暗悬浮 `gap3-hover-dark.png`（× 在左同位互换）读图通过；截图已 Copy 入 `screenshots/dev/` | Verified |
| V15 | 快档：check / design:check | exit 0，`[component-conventions] PASS`；design 双段 PASS | Verified |
| V16 | 快档：全量 vitest | 首跑 tinypool worker 被杀（`Channel closed`/`ERR_IPC_CHANNEL_CLOSED`，14:14-14:16 机器内存压力连带：pwsh 宿主 OOM/栈溢出 ×2、Node `Zone Allocation failed` ×1，同为环境事件非用例失败）；限 `--maxWorkers=2` 重跑见 V16b | Verified（环境事件归因）/ V16b 见下 |

**V16b（vitest2 重跑，`--maxWorkers=2`）**：**123 文件 / 1278 tests 全部通过**（154.76s；限 worker 后慢于默认档 28.86s，为内存压力下的取舍）。v3 快档四段全绿。
