/**
 * 生成插件自身外壳的运行时样式 `src/styles/tokens.css`。
 *
 * 单一事实源：`src/design/presets.ts` 的 `SHELL`（中性外壳主题）。
 * 生成出的变量命名与「生成结果预览」完全一致（见 design/tokensToCss.ts），
 * 因此组件只需消费 `--ds-*` 语义插槽，预览换肤时整体覆盖即可，无需改组件。
 *
 * ⚠️ 生成物，**请勿手改**；改 token 请改 presets.ts 后重新生成。
 *
 * 用法：node scripts/gen-tokens-css.ts
 */

import { writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { SHELL } from '../src/design/presets.ts'
import { tokensToCss } from '../src/design/tokensToCss.ts'

const outFile = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'styles',
  'tokens.css',
)

const header = [
  '/* =============================================================================',
  ' * 插件外壳运行时样式（自动生成）',
  ' *',
  ' * 本文件由 scripts/gen-tokens-css.ts 自动生成，请勿手动修改。',
  ' * 单一事实源：src/design/presets.ts -> SHELL（中性外壳主题）。',
  ' *',
  ' * 命名与「生成结果预览」共用一套 --ds-* 语义插槽（见 design/tokensToCss.ts）：',
  ' * 组件只消费插槽，预览时由容器覆盖插槽即可整体换肤。',
  ' *',
  ' * 注意：不要在这里加任何 @import url(...)：CSS @import 是渲染阻塞的，',
  ' * 第三方域名不可达会卡住整个文件，使变量与基础规则全部失效。',
  ' * 字体已通过 --ds-font-sans 的 fallback 栈覆盖，无需外链。',
  ' * ========================================================================== */',
  '',
].join('\n')

writeFileSync(outFile, header + tokensToCss(SHELL, ':root'), 'utf8')
console.log(`[gen-tokens-css] 已生成 ${outFile}`)
