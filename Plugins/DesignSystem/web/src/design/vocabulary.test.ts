/**
 * 词表镜像守卫（v2.6.6 / M12g）。
 *
 * 界面用的这几份词表 —— 层级序、色族序、审计类别序、状态档位序、尺度档位序 ——
 * 唯一真源在后端 `DesignSystemConstants`（`TokenTiers.All` / `ColorFamilies.All` / `AuditKinds.All` / `VariantAxes`）
 * 与 `ScaleGenerators`，经 `GET /meta` 出过来。它们历史上各自在界面里存了一份手抄数组：
 * 抄的那天一致，之后各自漂移，表现就是"界面排序和产物排序不一样""后端加了一类界面筛不出来"。
 *
 * 于是机械核对：**界面源码里不许再出现这些词表的多成员字面量清单**（同一行 ≥3 个成员被引号列出来）。
 * 单条文案提到某个取值（如审计说明里的 `contrast`）是允许的 —— 那是给人看的转述，不参与判定；
 * 成列才是第二份真相的形状。
 */
import path from 'node:path'
import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/**
 * `import.meta.url` 在 vitest（jsdom）下是 `/@fs/` 形式的 http URL 而非 `file:`，
 * 直接 `fileURLToPath` 会抛 —— 两种形态都归一成磁盘路径（同 classes.test.ts）。
 */
function toFsPath(url: string): string {
  const u = new URL(url)
  if (u.protocol === 'file:') return fileURLToPath(u)
  return decodeURIComponent(u.pathname).replace(/^\/@fs/, '').replace(/^\/([A-Za-z]:)/, '$1')
}

const SRC_DIR = path.resolve(path.dirname(toFsPath(import.meta.url)), '..')

function walk(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) return full.includes('node_modules') ? [] : walk(full)
    return e.isFile() ? [full] : []
  })
}

/** 界面源码：.ts / .vue，排除单测自身（守卫用的成员清单本身就是一份字面量） */
function uiSources(): { file: string; lines: string[] }[] {
  return walk(SRC_DIR)
    .filter((f) => /\.(ts|vue)$/.test(f) && !f.endsWith('.test.ts'))
    .map((f) => ({ file: path.relative(SRC_DIR, f), lines: readFileSync(f, 'utf8').split(/\r?\n/) }))
}

const VOCABULARIES: { name: string; members: string[] }[] = [
  { name: '层级 tiers', members: ['primitive', 'semantic', 'component'] },
  { name: '色族 colorFamilies', members: ['brand', 'accent', 'neutral', 'success', 'warning', 'danger', 'info'] },
  {
    name: '审计类别 auditKinds',
    members: ['contrast', 'alias', 'tier-violation', 'focus', 'reduced-motion', 'target-size', 'ramp-monotonic', 'naming', 'lifecycle-ref', 'orphan', 'unused'],
  },
  { name: '状态档位 stateOrder', members: ['default', 'hover', 'active', 'focus-visible', 'disabled'] },
  { name: '尺寸档位 sizeOrder', members: ['xs', 'sm', 'md', 'lg', 'xl'] },
  { name: '间距/圆角档位 scaleOrders', members: ['hairline', 'thin', 'pill', 'thick', 'macro', 'quick'] },
]

describe('界面不许再存一份后端词表', () => {
  const files = uiSources()

  it('扫到了界面源码（一条没扫到 = 守卫本身失效）', () => {
    expect(files.length).toBeGreaterThan(10)
  })

  for (const vocab of VOCABULARIES) {
    it(`${vocab.name}：没有多成员字面量清单`, () => {
      const hits: string[] = []
      for (const { file, lines } of files) {
        lines.forEach((line, i) => {
          const quoted = line.match(/'([^']+)'|"([^"]+)"/g) ?? []
          // 只数**不同的**成员：`severity === 'warning' ? 'warning' : 'info'` 这种三元式重复取值，
          // 不是"把词表抄了一份"的形状（真抄必然是一列互不相同的成员）。
          const named = new Set(quoted.map((q) => q.slice(1, -1)).filter((v) => vocab.members.includes(v)))
          if (named.size >= 3) hits.push(`${file}:${i + 1} → ${[...named].join(',')}｜${line.trim()}`)
        })
      }
      expect(hits, `${vocab.name} 又被人抄成界面字面量了（应改读 GET /meta 的对应字段）：\n${hits.join('\n')}`).toEqual([])
    })
  }
})
