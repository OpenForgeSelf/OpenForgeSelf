#!/usr/bin/env node
/**
 * 组件开发规范守卫（机器可判部分）
 *
 * 背景：组件的 story / CT 用例属于「写了但没人知道」的高危文件——不注册到画廊就等于没写，
 * 缺测试状态注释就与「功能 ↔ 故事 ↔ 测试」对应表脱节。靠自觉必然漏，故做成门禁。
 *
 * 与设计守卫的分工：
 *   - scripts/check-style-tokens.mjs / check-design-compliance.mjs：样式 token（本脚本不重复）
 *   - 本源：story/spec 的组织结构（注册、配对、标注、禁止 glob）
 *
 * 规则：
 *   A. 每个 `*.story.ts` 必须被 `ct/stories.ts` 引用（显式 import 聚合；glob 已禁用）
 *   B. 每个 `*.ct.spec.ts` 必须在同目录有同名 `*.story.ts`
 *   C. 每个 `*.story.ts` 至少含一处 `// 测试状态：` 注释
 *   D. `ct/stories.ts` 不得出现 `import.meta.glob`
 *
 * 扫描范围：`src/**` 与 `../Plugins/<X>/web/src/**`（插件前端 story 与组件同目录）。
 *
 * 用法：node scripts/check-component-conventions.mjs      # 违规 exit 1
 */
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const WEB_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const STORY_INDEX = path.join(WEB_ROOT, 'ct', 'stories.ts')
const SKIP_DIRS = new Set(['node_modules', 'dist', 'coverage', 'test-results', 'playwright-report'])

/** 递归收集目标文件；`node_modules` / 构建产物目录一律跳过 */
function walk(dir, want, out = []) {
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name) || entry.name.startsWith('.')) continue
      walk(full, want, out)
    } else if (want(entry.name)) {
      out.push(full)
    }
  }
  return out
}

const roots = [path.join(WEB_ROOT, 'src')]
const pluginsDir = path.resolve(WEB_ROOT, '..', 'Plugins')
if (fs.existsSync(pluginsDir)) {
  for (const d of fs.readdirSync(pluginsDir, { withFileTypes: true })) {
    if (!d.isDirectory()) continue
    const src = path.join(pluginsDir, d.name, 'web', 'src')
    if (fs.existsSync(src)) roots.push(src)
  }
}

const storyFiles = roots.flatMap((r) => walk(r, (n) => n.endsWith('.story.ts')))
const specFiles = roots.flatMap((r) => walk(r, (n) => n.endsWith('.ct.spec.ts')))

const problems = []
const rel = (p) => path.relative(WEB_ROOT, p).split(path.sep).join('/')

if (!fs.existsSync(STORY_INDEX)) {
  problems.push(`[FAIL] 缺少画廊注册表：${rel(STORY_INDEX)}`)
} else {
  const indexText = fs.readFileSync(STORY_INDEX, 'utf8')

  // D. 禁用 glob 聚合（HMR 下会偶发漏注册）
  //    先剥注释再判：文件头的设计说明里会提到这个 API 名，不能因此误报
  const codeOnly = indexText.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '')
  if (codeOnly.includes('import.meta.glob')) {
    problems.push(`[FAIL] ${rel(STORY_INDEX)}: 禁止 import.meta.glob，请改用显式 import 聚合`)
  }

  // A. 每个 story 必须在注册表里被引用（按文件名匹配 import 路径，去掉 `.ts` 后缀）
  for (const file of storyFiles) {
    const imported = path.basename(file, '.ts') // 例如 ForgeCard.story
    if (!indexText.includes(imported)) {
      problems.push(`[FAIL] ${rel(file)}: 未在 ct/stories.ts 注册（画廊里看不到它）`)
    }
  }
}

// B. CT 用例必须与 story 同目录同名
//    例外：画廊自身契约（Gallery.ct.spec.ts）测的是挂载壳，没有对应组件与故事
const SHELL_SPECS = new Set(['Gallery.ct.spec.ts'])
for (const spec of specFiles) {
  if (SHELL_SPECS.has(path.basename(spec))) continue
  const sibling = spec.replace(/\.ct\.spec\.ts$/, '.story.ts')
  if (!fs.existsSync(sibling)) {
    problems.push(`[FAIL] ${rel(spec)}: 同目录缺少 ${path.basename(sibling)}（用例没有对应的故事变体）`)
  }
}

// C. story 必须标注「测试状态」
for (const file of storyFiles) {
  const text = fs.readFileSync(file, 'utf8')
  if (!text.includes('// 测试状态：')) {
    problems.push(`[FAIL] ${rel(file)}: 缺少「// 测试状态：…」注释（变体要说明验证什么状态）`)
  }
  if (!/id:\s*['"`]/.test(text)) {
    problems.push(`[FAIL] ${rel(file)}: 未导出任何带 id 的故事变体`)
  }
}

if (problems.length > 0) {
  for (const p of problems) console.error(p)
  console.error(`[component-conventions] 共 ${problems.length} 项违规`)
  process.exit(1)
}

console.log(
  `[component-conventions] PASS（story ${storyFiles.length} 个 / ct.spec ${specFiles.length} 个，均已注册、配对、标注）`,
)
