# mini-task：修复发布链前端 vue-tsc 死代码错误（2.4.4 出包阻塞）

## Intent（为什么/做什么）
- 背景：2026-10-14 用户指令「打个本地包，然后操作浏览器点击更新宿主」。首发 `release-local.ps1 -Version 2.4.4 -Sign` 在前端 `vue-tsc -b` 处 exit 2，3 个 TS6133（未使用符号），出包链中断。
- 目标：最小修复使 `pnpm run check`（vue-tsc + eslint）转绿，发布链跑通，产出 2.4.4 本地更新包。

## Spec（改什么）
1. `ForgeSelf.Web/src/components/BackgroundSettings.vue`
   - 删除导入中的 `BG_SOLID_PRESETS`（模板对应纯色选择器块已被 `<!-- -->` 注释，注释写明「已隐藏：主题切换自动联动背景色」）。
   - 删除 `selectSolid(color)` 函数（仅被上述注释块引用）。
2. `ForgeSelf.Web/src/components/settings/AppearancePanel.vue`
   - 导入 `import { ref, onMounted } from 'vue'` → `import { ref } from 'vue'`（`onMounted` 全文未使用）。
- 不改任何行为；模板注释块保留原样（后续若恢复纯色选择器需一并恢复导入与函数）。

## Plan（文件级）
- 上述 2 文件 3 处删除；不新增功能、不改样式。

## Task（允许/禁止）
- 允许：删除未使用导入与未使用函数；重跑 `pnpm run check`。
- 禁止：顺手重构；动注释掉的模板块；改 appearance store；动业务代码。

## 验证
- `cd ForgeSelf.Web && pnpm run check` → exit 0，0 errors（既有 143 warnings 为历史风格告警，Warning 级不阻断）。
- 发布链重跑：`pwsh scripts/release/release-local.ps1 -Version 2.4.4 -Sign` → `D:\src\my-proj\OpenForgeSelf\updates\` 出现 `OpenForgeSelf-2.4.4.*-win-x64.zip` + `SHA256SUMS.txt`；exe 签名 `Get-AuthenticodeSignature` = Valid。
