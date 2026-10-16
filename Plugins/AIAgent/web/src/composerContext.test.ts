import { describe, expect, it } from 'vitest'
import {
  ACCESS_MODES,
  accessModeMeta,
  buildAskPanel,
  buildContextBar,
  deriveToolAllowlist,
  toolAllowedInMode,
} from './composerContext'

/**
 * 041 权限三档 + 图标行单测。
 *
 * 权限逻辑是**安全语义**（决定哪些工具真的会被挂载给 Agent），断言必须绑定
 * 业务规则本身（「只读不得挂命令执行」），而不是当前实现的快照数字 ——
 * 否则新增工具时会像魔法数字那样一破就红。
 */

const ALL = [
  'read_file', 'list_todos', 'search_memories', 'get_current_time',
  'write_file', 'create_todo', 'add_memory',
  'run_terminal_command', 'universal_tool', 'kill_process',
  'calculate', 'encode_base64',
]

describe('权限三档语义', () => {
  it('只读档：放行读类与纯计算，拦截写文件/命令执行/进程控制', () => {
    expect(toolAllowedInMode('read_file', 'readonly')).toBe(true)
    expect(toolAllowedInMode('list_todos', 'readonly')).toBe(true)
    expect(toolAllowedInMode('search_memories', 'readonly')).toBe(true)
    expect(toolAllowedInMode('calculate', 'readonly')).toBe(true)
    // 写能力必须被挡住 —— 这三条是本档位的核心承诺
    expect(toolAllowedInMode('write_file', 'readonly')).toBe(false)
    expect(toolAllowedInMode('create_todo', 'readonly')).toBe(false)
    expect(toolAllowedInMode('run_terminal_command', 'readonly')).toBe(false)
    expect(toolAllowedInMode('kill_process', 'readonly')).toBe(false)
  })

  it('标准档：可写文件与待办，但命令执行/任意代码/进程控制仍被扣下', () => {
    expect(toolAllowedInMode('write_file', 'standard')).toBe(true)
    expect(toolAllowedInMode('create_todo', 'standard')).toBe(true)
    expect(toolAllowedInMode('run_terminal_command', 'standard')).toBe(false)
    expect(toolAllowedInMode('universal_tool', 'standard')).toBe(false)
    expect(toolAllowedInMode('kill_process', 'standard')).toBe(false)
  })

  it('完全访问档：工具全开', () => {
    for (const t of ALL) expect(toolAllowedInMode(t, 'full')).toBe(true)
  })

  it('档位是单调放宽的：任一工具在更高档位下不会被更低档位允许（除只读纯计算类外无例外）', () => {
    for (const t of ALL) {
      const ro = toolAllowedInMode(t, 'readonly')
      const st = toolAllowedInMode(t, 'standard')
      const full = toolAllowedInMode(t, 'full')
      // full 恒真；standard 不能比 readonly 更严
      expect(full).toBe(true)
      if (ro) expect(st).toBe(true)
    }
  })

  it('未知档位回退标准（宁可少放开，不可意外放开）', () => {
    expect(accessModeMeta('不存在的档').key).toBe('standard')
    expect(toolAllowedInMode('run_terminal_command', undefined)).toBe(false)
    expect(toolAllowedInMode('run_terminal_command', 'bogus')).toBe(false)
  })

  it('三档元数据齐全且 key 唯一', () => {
    expect(ACCESS_MODES.map((m) => m.key)).toEqual(['readonly', 'standard', 'full'])
    expect(new Set(ACCESS_MODES.map((m) => m.key)).size).toBe(3)
    for (const m of ACCESS_MODES) {
      expect(!!m.label && !!m.desc).toBe(true)
    }
  })
})

describe('deriveToolAllowlist：档位是上限、勾选是收窄', () => {
  it('未勾选工具时也必须按档位下发全集 —— 空=全挂，否则档位形同虚设', () => {
    const ro = deriveToolAllowlist('readonly', [], ALL) ?? []
    const st = deriveToolAllowlist('standard', [], ALL) ?? []
    // 只读档结果里绝不能出现写工具/命令执行
    expect(ro).toContain('read_file')
    expect(ro).not.toContain('write_file')
    expect(ro).not.toContain('run_terminal_command')
    // 标准档比只读档更宽（write_file 进了）
    expect(st).toContain('write_file')
    expect(st).not.toContain('run_terminal_command')
    expect(st.length).toBeGreaterThan(ro.length)
  })

  it('已勾选时取交集：勾了命令执行但在只读档下仍被剔除', () => {
    const out = deriveToolAllowlist('readonly', ['run_terminal_command', 'read_file'], ALL) ?? []
    expect(out).toEqual(['read_file'])
  })

  it('已勾选时取交集：标准档剔除命令执行但保留文件写', () => {
    const out = deriveToolAllowlist('standard', ['run_terminal_command', 'write_file'], ALL) ?? []
    expect(out).toEqual(['write_file'])
  })

  it('完全访问档 + 未勾选 → 不下发字段（null，与后端默认一致，不做无谓全量过滤）', () => {
    expect(deriveToolAllowlist('full', [], ALL)).toBeNull()
  })

  it('完全访问档 + 已勾选 → 原样下发', () => {
    expect(deriveToolAllowlist('full', ['kill_process'], ALL)).toEqual(['kill_process'])
  })

  it('勾选了不存在的工具也不会被凭空放行（只读档下未知名按可写处理 → 剔除）', () => {
    const out = deriveToolAllowlist('readonly', ['根本没注册的工具'], ALL) ?? []
    expect(out).toEqual([])
  })

  it('派生结果永远是 ALL 的子集（不会引入池外工具）', () => {
    for (const mode of ['readonly', 'standard', 'full'] as const) {
      const out = deriveToolAllowlist(mode, [], ALL) ?? []
      for (const t of out) expect(ALL).toContain(t)
    }
  })
})

describe('buildContextBar：入口恒显示（这正是「功能像没做」的根因修复）', () => {
  it('五类入口恒出现，即使全未选 —— 用户必须能点开去选', () => {
    const bar = buildContextBar({ projectDir: '', agentName: '', modelName: '', toolCount: 0, skillCount: 0 })
    expect(bar.map((i) => i.key)).toEqual(['project', 'agent', 'model', 'tools', 'skills'])
    expect(bar.every((i) => !!i.icon && !!i.label)).toBe(true)
  })

  it('已选项带 active 与 detail（当前选择可见）', () => {
    const bar = buildContextBar({
      projectDir: 'D:/src/my-proj/OpenForgeSelf/OpenForgeSelf',
      agentName: '通用助手',
      modelName: 'deepseek-chat',
      toolCount: 3,
      skillCount: 0,
    })
    const by = Object.fromEntries(bar.map((i) => [i.key, i]))
    expect(by.project.active).toBe(true)
    expect(by.project.detail).toBe('OpenForgeSelf')
    expect(by.agent.detail).toBe('通用助手')
    expect(by.model.detail).toBe('deepseek-chat')
    expect(by.tools.count).toBe(3)
    expect(by.skills.count).toBe(0)
    expect(by.skills.active).toBe(false)
  })

  it('maxDirect 截断时把余下入口收进 +N', () => {
    const bar = buildContextBar({
      projectDir: '', agentName: '', modelName: '', toolCount: 0, skillCount: 0, maxDirect: 3,
    })
    expect(bar).toHaveLength(4)
    expect(bar[3].key).toBe('more')
    expect(bar[3].label).toContain('2')
  })
})

describe('buildAskPanel：只保留未解决条目', () => {
  it('按 kind 分计数并算待办进度', () => {
    const p = buildAskPanel([
      { id: 'a1', kind: 'approval', title: '删除 prod 库', risk: 'critical' },
      { id: 'q1', kind: 'question', title: '选哪个方案', options: ['A', 'B'] },
      { id: 't1', kind: 'todo', title: '本轮任务', todos: [{ title: 'x', done: true }, { title: 'y' }] },
    ])
    expect(p.items).toHaveLength(3)
    expect(p.approvalCount).toBe(1)
    expect(p.questionCount).toBe(1)
    expect(p.todoCount).toBe(1)
    expect(p.todoProgress).toBe(0.5)
  })

  it('空 / null / 缺字段条目一律过滤掉，不占输入框上沿', () => {
    const p = buildAskPanel([{ id: '', kind: 'approval', title: '无 id' }, { id: 'x', kind: 'todo', title: '' } as never])
    expect(p.items).toHaveLength(0)
    expect(buildAskPanel(null).items).toHaveLength(0)
    expect(buildAskPanel(undefined).approvalCount).toBe(0)
  })
})
