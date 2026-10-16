import { describe, expect, it, vi } from 'vitest'
import {
  BUILTIN_COMMANDS,
  FILE_SKIP_DIRS,
  buildCommandGroups,
  buildContextChips,
  collectProjectFiles,
  filterCommandGroups,
  filterFileItems,
  joinRel,
  pickItem,
  shouldDescend,
  shouldKeepFile,
} from './composer'

/**
 * composer 宿主组装层单测（2026-10-15 composer 升级）。
 *
 * 锁的是四条对外可见的契约：
 *  1. 命令面板分组：内置命令 + 工具 + 技能 + 脚本 + Agent + 模型，**空组不出现**；
 *  2. 选中语义（闸门1 P3 拍板）：工具/技能/Agent/模型**挂上下文、返回空文本**，
 *     内置命令执行动作返回空文本，脚本与文件插入**可编辑文本**；
 *  3. `@` 文件：跳过噪声目录、限深、总量上限；
 *  4. chips：有值才出现、可删项与不可删项区分清楚。
 */

/** 造一份数据源（各取两条，便于断言分组与顺序）。 */
function sources(overrides: Partial<Parameters<typeof buildCommandGroups>[0]> = {}) {
  return {
    tools: () => [
      { name: 'read_file', description: '读取文件', pluginId: 'FileTools' },
      { name: 'run_terminal_command', description: '执行命令', pluginId: 'ai-agent' },
    ],
    skills: () => [
      { id: 'sk-1', name: '架构评审', source: '.agents/skills' },
    ],
    scripts: () => [
      { id: '1', name: '构建前端', description: '执行 pnpm build', runtime: 'pwsh' },
    ],
    agents: () => [
      { id: 'ag-1', name: '通用助手', description: '默认' },
    ],
    models: () => [
      { chatModelId: 'm1', name: 'GPT-4o', providerName: 'OpenAI' },
    ],
    ...overrides,
  } as Parameters<typeof buildCommandGroups>[0]
}

describe('buildCommandGroups — 命令面板分组', () => {
  it('六组齐全且顺序固定：内置命令 → 工具 → 技能 → 脚本 → Agent → 模型', () => {
    const groups = buildCommandGroups(sources())
    expect(groups.map((g) => g.key)).toEqual(['builtin', 'tool', 'skill', 'script', 'agent', 'model'])
  })

  it('内置命令永远在（不依赖任何数据源）', () => {
    const groups = buildCommandGroups(sources())
    const builtin = groups.find((g) => g.key === 'builtin')!
    expect(builtin.items.map((i) => i.name)).toEqual(['/clear', '/export', '/help'])
    expect(BUILTIN_COMMANDS.length).toBe(3)
  })

  it('★ 空数据源不产生空组（面板里不该挂一排空标题）', () => {
    const groups = buildCommandGroups({
      tools: () => [],
      skills: () => [],
      scripts: () => [],
      agents: () => [],
      models: () => [],
    })
    expect(groups.map((g) => g.key)).toEqual(['builtin'])
  })

  it('工具带插件 id 作为 meta，便于按插件过滤', () => {
    const g = buildCommandGroups(sources()).find((x) => x.key === 'tool')!
    expect(g.items[0]).toMatchObject({ name: 'read_file', meta: 'FileTools', kind: 'tool' })
  })

  it('缺字段的脏数据被过滤掉（无 name 的工具 / 无 id 的技能）', () => {
    const groups = buildCommandGroups({
      tools: () => [{ description: '没名字' }],
      skills: () => [{ name: '没 id' }],
      scripts: () => [],
      agents: () => [],
      models: () => [],
    })
    expect(groups.map((g) => g.key)).toEqual(['builtin'])
  })

  it('数据源抛错时不拖垮整个面板（各自 catch 后为空）', () => {
    const groups = buildCommandGroups({
      tools: () => {
        throw new Error('boom')
      },
      skills: () => [],
      scripts: () => [],
      agents: () => [],
      models: () => [],
    })
    expect(() => buildCommandGroups(sources())).not.toThrow()
    expect(groups.length).toBeGreaterThan(0)
  })
})

describe('filterCommandGroups — 过滤与截断', () => {
  const groups = buildCommandGroups(sources())

  it('无 query 时每组保留（受 limit 截断）', () => {
    expect(filterCommandGroups(groups, '').map((g) => g.key)).toContain('tool')
  })

  it('过滤后空组被丢弃', () => {
    const kept = filterCommandGroups(groups, '架构评审')
    expect(kept.map((g) => g.key)).toEqual(['skill'])
  })

  it('★ 跨组搜索：搜「执行」能同时命中工具与脚本', () => {
    const kept = filterCommandGroups(groups, '执行')
    expect(kept.map((g) => g.key).sort()).toEqual(['script', 'tool'])
  })
})

describe('pickItem — 选中语义（闸门1 P3）', () => {
  function actions() {
    return {
      toggleTool: vi.fn(),
      toggleSkill: vi.fn(),
      selectAgent: vi.fn(),
      selectModel: vi.fn(),
      runBuiltin: vi.fn(),
    }
  }

  it('★ 工具：挂上下文，不往输入框插文本', () => {
    const a = actions()
    expect(pickItem({ id: 'tool:read_file', name: 'read_file', kind: 'tool' }, a)).toBe('')
    expect(a.toggleTool).toHaveBeenCalledWith('read_file')
  })

  it('★ 技能：挂上下文，不插文本', () => {
    const a = actions()
    expect(pickItem({ id: 'skill:sk-1', name: '架构评审', kind: 'skill' }, a)).toBe('')
    expect(a.toggleSkill).toHaveBeenCalledWith('sk-1')
  })

  it('Agent / 模型：切换选择，不插文本', () => {
    const a = actions()
    expect(pickItem({ id: 'agent:ag-1', name: '通用助手', kind: 'agent' }, a)).toBe('')
    expect(a.selectAgent).toHaveBeenCalledWith('ag-1')
    expect(pickItem({ id: 'model:m1', name: 'GPT-4o', kind: 'model' }, a)).toBe('')
    expect(a.selectModel).toHaveBeenCalledWith('m1')
  })

  it('内置命令：执行动作，不插文本', () => {
    const a = actions()
    expect(pickItem({ id: 'builtin:clear', name: '/clear', kind: 'builtin' }, a)).toBe('')
    expect(a.runBuiltin).toHaveBeenCalledWith('clear')
  })

  it('脚本：插入可编辑文本（用户能补参数再发）', () => {
    const a = actions()
    expect(pickItem({ id: 'script:1', name: '构建前端', kind: 'script' }, a)).toBe('运行脚本 构建前端 ')
  })

  it('文件（@ 提及）：插入 @路径 + 空格', () => {
    const a = actions()
    expect(pickItem({ id: 'file:src/a.ts', name: 'src/a.ts', kind: 'file' }, a)).toBe('@src/a.ts ')
  })

  it('未知 kind 兜底为空字符串，不插脏东西', () => {
    const a = actions()
    expect(pickItem({ id: 'x', name: 'x', kind: 'unknown' as never }, a)).toBe('')
  })
})

describe('`@` 文件提及 — 遍历规则与过滤', () => {
  it('★ 噪声目录不下钻（node_modules/.git/dist/bin/obj 等）', () => {
    FILE_SKIP_DIRS.forEach((d) => expect(shouldDescend(d, 0)).toBe(false))
    expect(shouldDescend('src', 0)).toBe(true)
  })

  it('★ 日志目录 / 构建产物 / 缓存目录同样不下钻', () => {
    ;['.atomcode-logs', '.playwright-mcp', '.temp', 'artifacts', 'publish', 'logs'].forEach((d) =>
      expect(shouldDescend(d, 0)).toBe(false)
    )
  })

  it('★ 日志 / 二进制 / 缓存文件不进候选（否则 1500 条全被 .log 占满）', () => {
    expect(shouldKeepFile('a.ts')).toBe(true)
    ;['x.log', 'a.dll', 'b.pdb', 'c.cache', 'd.db', 'e.tmp', 'f.br', 'g.gz'].forEach((n) =>
      expect(shouldKeepFile(n)).toBe(false)
    )
  })

  it('★ 超过最大深度不再下钻（防大项目卡死）', () => {
    expect(shouldDescend('a', 3)).toBe(true)
    expect(shouldDescend('a', 4)).toBe(false)
  })

  it('路径拼接统一为正斜杠，不留多余分隔符', () => {
    expect(joinRel('', 'src')).toBe('src')
    expect(joinRel('src', 'a.ts')).toBe('src/a.ts')
    expect(joinRel('src\\lib', 'b.ts')).toBe('src/lib/b.ts')
  })

  it('文件过滤：按路径子串匹配，上限 50 条', () => {
    const files = Array.from({ length: 80 }, (_, i) => ({ path: `src/f${i}.ts` }))
    expect(filterFileItems(files, 'f1')).toHaveLength(11) // f1、f10..f19
    expect(filterFileItems(files, '')).toHaveLength(50)
  })

  it('过滤时忽略用户已输入的 @ 前缀', () => {
    const files = [{ path: 'src/a.ts' }, { path: 'src/b.ts' }]
    expect(filterFileItems(files, '@a.ts').map((f) => f.path)).toEqual(['src/a.ts'])
  })

  it('脏数据（无 path）不进候选', () => {
    expect(filterFileItems([{ path: '' }], '')).toEqual([])
  })
})

describe('collectProjectFiles — BFS 收集', () => {
  /** 假目录树：顶层 2 个文件 + deep 目录；deep 下有海量文件（模拟 speckit 模板）。 */
  function fakeTree() {
    const tree: Record<string, Array<{ name: string; isDirectory: boolean }>> = {
      '': [
        { name: 'AGENTS.md', isDirectory: false },
        { name: 'README.md', isDirectory: false },
        { name: 'deep', isDirectory: true },
        { name: 'node_modules', isDirectory: true },
      ],
      deep: Array.from({ length: 2000 }, (_, i) => ({ name: `t${i}.md`, isDirectory: false })),
    }
    return (rel: string) => Promise.resolve(tree[rel] ?? [])
  }

  it('★ 浅层文件先收进来（BFS 而非 DFS）', async () => {
    const files = await collectProjectFiles(fakeTree(), 1500, 4)
    expect(files.slice(0, 2)).toEqual(['AGENTS.md', 'README.md'])
  })

  it('★ 总量上限生效（deep 的 2000 个文件被截到上限内）', async () => {
    const files = await collectProjectFiles(fakeTree(), 1500, 4)
    expect(files).toHaveLength(1500)
  })

  it('噪声目录不进候选（node_modules 被跳过，没有它的文件）', async () => {
    const files = await collectProjectFiles(fakeTree(), 1500, 4)
    expect(files.some((f) => f.startsWith('node_modules/'))).toBe(false)
  })

  it('噪声扩展名被剔除', async () => {
    const tree: Record<string, Array<{ name: string; isDirectory: boolean }>> = {
      '': [
        { name: 'a.ts', isDirectory: false },
        { name: 'run.log', isDirectory: false },
        { name: 'lib.dll', isDirectory: false },
      ],
    }
    const files = await collectProjectFiles((rel) => Promise.resolve(tree[rel] ?? []))
    expect(files).toEqual(['a.ts'])
  })

  it('单层取数失败只丢该层，其余照收', async () => {
    const tree: Record<string, Array<{ name: string; isDirectory: boolean }>> = {
      '': [
        { name: 'ok', isDirectory: true },
        { name: 'top.ts', isDirectory: false },
      ],
      ok: [{ name: 'inner.ts', isDirectory: false }],
    }
    const listEntries = (rel: string) =>
      rel === 'ok' ? Promise.reject(new Error('boom')) : Promise.resolve(tree[rel] ?? [])
    const files = await collectProjectFiles(listEntries)
    expect(files).toEqual(['top.ts'])
  })

  it('超过 maxDepth 不再下钻', async () => {
    const tree: Record<string, Array<{ name: string; isDirectory: boolean }>> = {
      '': [{ name: 'd1', isDirectory: true }],
      d1: [{ name: 'd2', isDirectory: true }],
      'd1/d2': [{ name: 'd3', isDirectory: true }],
      'd1/d2/d3': [{ name: 'deep.ts', isDirectory: false }],
    }
    const files = await collectProjectFiles((rel) => Promise.resolve(tree[rel] ?? []), 100, 2)
    expect(files).toEqual([])
  })
})

describe('buildContextChips — 上下文 chips（所见即所发）', () => {
  it('全部为空时不渲染任何 chip（空态不占位）', () => {
    expect(
      buildContextChips({ projectDir: '', toolNames: [], skillIds: [], agentName: '', modelName: '' })
    ).toEqual([])
  })

  it('项目显示最后一段目录名，可删', () => {
    const chips = buildContextChips({
      projectDir: 'D:\\src\\my-proj\\OpenForgeSelf',
      toolNames: [],
      skillIds: [],
      agentName: '',
      modelName: '',
    })
    expect(chips).toHaveLength(1)
    expect(chips[0]).toMatchObject({ key: 'project', label: 'OpenForgeSelf', removable: true })
  })

  it('工具/技能显示数量；多个技能显示「首个 +N」', () => {
    const chips = buildContextChips({
      projectDir: '',
      toolNames: ['read_file', 'write_file'],
      skillIds: ['sk-1', 'sk-2'],
      skillNames: { 'sk-1': '架构评审' },
      agentName: '',
      modelName: '',
    })
    expect(chips.find((c) => c.key === 'tools')?.label).toBe('工具 2')
    expect(chips.find((c) => c.key === 'skills')?.label).toBe('技能 架构评审 +1')
  })

  it('Agent / 模型不可删（切换而非清空，删了就不知道在用谁）', () => {
    const chips = buildContextChips({
      projectDir: '',
      toolNames: [],
      skillIds: [],
      agentName: '通用助手',
      modelName: 'GPT-4o',
    })
    expect(chips.find((c) => c.key === 'agent')).toMatchObject({ label: '通用助手', removable: false })
    expect(chips.find((c) => c.key === 'model')).toMatchObject({ label: 'GPT-4o', removable: false })
  })
})
