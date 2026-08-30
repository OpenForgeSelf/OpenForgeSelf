/**
 * 生成「宿主共享依赖桥」模块（供浏览器 import map 解析）。
 *
 * 背景（specs/010-plugin-frontend-runtime）：插件界面产物把 vue / vue-router / pinia /
 * element-plus 声明为外部依赖，运行时由 import map 把裸模块名解析到本脚本生成的 shim，
 * shim 再从宿主挂到 window.__FORGE_SHARED__ 的真实模块命名空间具名再导出。
 * 这样宿主与插件共用同一份运行时刻实例，从根本上杜绝 Vue 双实例导致的响应式失效。
 *
 * 为什么用脚本生成而不是手写：
 * ESM 不支持动态再导出（无法 `export * from window.xxx`），必须逐条具名导出；
 * 手工枚举极易遗漏，遗漏时插件运行期会报 "does not provide an export named X" 且难以定位。
 * 因此改为从真实包的导出名自动生成，保证清单完整且与宿主实际版本严格一致。
 *
 * 用法：node scripts/generate-shared-shims.mjs
 */

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const outDir = resolve(scriptDir, '../public/shared')

/**
 * 待生成的目标。globalKey 为宿主挂在 window.__FORGE_SHARED__ 上的键名，
 * 需与 src/shared/exposeSharedDeps.ts 保持一致。
 */
const targets = [
  { pkg: 'vue', file: 'vue.js', globalKey: 'vue', desc: 'Vue 运行时刻' },
  { pkg: 'vue-router', file: 'vue-router.js', globalKey: 'vueRouter', desc: 'Vue Router' },
  { pkg: 'pinia', file: 'pinia.js', globalKey: 'pinia', desc: 'Pinia 状态管理' },
]

/** 合法 ES 标识符（ESM 具名导出名必须可静态解析）。 */
const isValidIdentifier = (name) => /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(name)

/**
 * 生成单个 shim 文件内容。
 * @param {{globalKey: string, desc: string}} target 目标配置
 * @param {string[]} exportNames 导出名清单
 * @returns {string} ESM 文件内容
 */
function buildShim(target, exportNames) {
  const header = [
    '/**',
    ` * ${target.desc} 共享模块桥（自动生成，请勿手工编辑）。`,
    ' *',
    ' * 由 scripts/generate-shared-shims.mjs 依据宿主实际安装版本生成；',
    ' * 运行期从宿主挂载的 window.__FORGE_SHARED__ 再导出，保证插件与宿主共用同一实例。',
    ' */',
    `const m = window.__FORGE_SHARED__?.${target.globalKey}`,
    'if (!m) throw new Error(`[ForgeSelf] 共享依赖未就绪: ${target.globalKey}，宿主未挂载 window.__FORGE_SHARED__`)',
    '',
  ]

  const body = exportNames.map((name) => `export const ${name} = m.${name}`)

  return [...header, ...body, ''].join('\n')
}

mkdirSync(outDir, { recursive: true })

let total = 0
for (const target of targets) {
  const mod = await import(target.pkg)
  const exportNames = Object.keys(mod).filter((n) => n !== 'default' && isValidIdentifier(n)).sort()

  if (exportNames.length === 0) {
    throw new Error(`未能从 ${target.pkg} 解析出任何具名导出，请检查包是否正确安装`)
  }

  const content = buildShim(target, exportNames)
  writeFileSync(resolve(outDir, target.file), content, 'utf8')
  total += exportNames.length
  console.log(`已生成 ${target.file}：${exportNames.length} 个导出`)
}

console.log(`共享模块桥生成完成：${targets.length} 个文件，共 ${total} 个导出 → ${outDir}`)
