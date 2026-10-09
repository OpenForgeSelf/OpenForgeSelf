# 03 Plan

## Implementation Steps（具体到文件）
1. file: Plugins/ToolBridge/web/src/clipboard.ts
   reason: 新增纯函数 buildCopyNote(prev, outcome, charCount, now)，返回 { count, text, mode }；不改 copyText 现有签名与语义。
2. file: Plugins/ToolBridge/web/src/ToolBridgeView.vue
   reason: 删除 copiedKey；新增 copyNotes（按 prompt/result 各一份）；按钮文案恒定，旁边渲染提示 span；doParse/doTurn/doExecuteParsed/reloadTurn 复位结果提示；粘贴框加 @mousedown / @blur 与焦点标记；copy() 不再写 copyHint。
3. file: ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts
   reason: 新增两条用例（复制：连点 + 复位 + 剪贴板读回；粘贴框：失焦点击全选 / 二次点击折叠 / 程序化 focus 不全选）；用例头注释同步。
4. file: Plugins/ToolBridge/plugin.json
   reason: Version 1.0.2 → 1.0.3（仅源码清单；不碰已安装目录的清单）。
5. 条件项 file: Plugins/ToolBridge/web/src/clipboard.test.ts
   reason: 仅当 web 已有 vitest 才新增，覆盖 buildCopyNote；否则由 e2e 覆盖，并在 05 记录。

## Test Plan
- e2e 用例 A：page.context().grantPermissions(['clipboard-read','clipboard-write']) 后跑一轮解析执行，连点复制结果两次，断言提示序号与 clipboard.readText；再点解析并执行，断言提示清空，再复制回到第 1 次。
- e2e 用例 B：先点页面其他位置使框失焦，再点粘贴框，读 selectionStart/selectionEnd；再次点击读选区折叠；blur 后程序化 focus() 读选区折叠。
- 反向探针：把全选条件临时改成「总是全选」，期望 AC6 变红；还原后复绿。

## Verification
- Build：cd Plugins/ToolBridge/web && pnpm run build
- 类型（发布链同参数）：cd ForgeSelf.Web && npx vue-tsc -b
- E2E：cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/tool-bridge（前置：NO_PROXY、TEMP/TMP 指进仓库 .temp/tmp，AGENTS §5.0）
- 档位：快档 + 本插件 e2e；不涉及宿主源码与脚本，不触发中档、深档。

## 偏差记录
暂无（实施中发现 Plan 与仓库不符，先记此处再改）。
