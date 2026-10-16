import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ContextBar from './ContextBar.vue'
import AskPanel from './AskPanel.vue'
import TodoEntry from './TodoEntry.vue'

/**
 * 041 图标行 + 待你动作区 + 待办独立入口组件测。
 *
 * 锁几条用户可见规则：
 *  1. **入口恒显示**：即使全未选，五个入口按钮也在（上一批的根因就是 chips「有值才出现」，
 *     用户找不到入口，判为「功能没做」）；
 *  2. 面板一次只开一个，点同一个再点收起，点面板外关闭；
 *  3. 权限胶囊三档单选，切换上抛；
 *  4. 待办是**独立入口**（输入框下方底行），与审批/询问（瞬时动作，输入框上方浮层）拆开。
 */

const items = [
  { key: 'project', icon: '📁', label: '项目', active: true, detail: 'OpenForgeSelf' },
  { key: 'agent', icon: '🤖', label: 'Agent', active: true, detail: '通用助手' },
  { key: 'model', icon: '💠', label: '模型', active: false },
  { key: 'tools', icon: '🔧', label: '工具', count: 3, active: true },
  { key: 'skills', icon: '⚡', label: '技能', count: 0, active: false },
]

const panels = {
  tools: {
    title: '选择工具（已选 3）',
    multi: true,
    items: [
      { id: 'read_file', name: 'read_file', desc: '读文件', active: true },
      { id: 'write_file', name: 'write_file', desc: '写文件', active: false },
    ],
  },
  agent: { title: '选择 Agent', items: [{ id: 'a1', name: '通用助手', active: true }] },
}

const access = {
  modes: [
    { key: 'readonly', label: '只读', desc: '只挂读取类工具' },
    { key: 'standard', label: '标准', desc: '可写文件' },
    { key: 'full', label: '完全访问', desc: '工具全开' },
  ],
  value: 'standard',
}

function mountBar(props: Record<string, unknown> = {}) {
  return mount(ContextBar, { props: { items, panels, access, ...props } })
}

describe('ContextBar 输入框内嵌图标行', () => {
  it('入口恒显示：全未选时五个按钮仍在（这是「功能可见」的根因修复）', () => {
    const w = mountBar({ items: items.map((i) => ({ ...i, active: false, count: 0 })) })
    const btns = w.findAll('.aiw-ctxbtn')
    expect(btns).toHaveLength(5)
    // 每个入口都有无障碍名（图标是 emoji，屏读靠 sr-only 文本）
    expect(w.findAll('.aiw-sr-only').map((n) => n.text())).toEqual(['项目', 'Agent', '模型', '工具', '技能'])
  })

  it('选中态与计数角标按注入状态渲染；count=0 不占位', () => {
    const w = mountBar()
    const toolsBtn = w.findAll('.aiw-ctxbtn')[3]
    expect(toolsBtn.classes()).toContain('is-active')
    expect(toolsBtn.find('.aiw-ctxbtn__n').text()).toBe('3')
    // 技能 count=0 → 无角标
    expect(w.findAll('.aiw-ctxbtn')[4].find('.aiw-ctxbtn__n').exists()).toBe(false)
  })

  it('点入口开面板，再点同一入口收起（一次只开一个）', async () => {
    const w = mountBar()
    const toolsBtn = w.findAll('.aiw-ctxbtn')[3]
    await toolsBtn.trigger('click')
    expect(w.find('.aiw-ctxpop').exists()).toBe(true)
    expect(w.find('.aiw-ctxmenu__t').text()).toContain('选择工具')
    expect(w.emitted('open')?.[0]).toEqual(['tools'])

    await toolsBtn.trigger('click')
    expect(w.find('.aiw-ctxpop').exists()).toBe(false)
    expect(w.emitted('close')?.[0]).toEqual(['tools'])
  })

  it('多选面板渲染复选框 + 已选项点亮；点选上抛 { key, id }', async () => {
    const w = mountBar()
    await w.findAll('.aiw-ctxbtn')[3].trigger('click')
    const boxes = w.findAll('.aiw-ctxmenu__box')
    expect(boxes).toHaveLength(2)
    expect(boxes[0].classes()).toContain('is-on')
    await w.findAll('.aiw-ctxmenu__row')[1].trigger('click')
    expect(w.emitted('pick')?.[0]).toEqual([{ key: 'tools', id: 'write_file' }])
  })

  it('单选面板不渲染复选框，选中项显示 ✓ 标记', async () => {
    const w = mountBar()
    await w.findAll('.aiw-ctxbtn')[1].trigger('click')
    expect(w.find('.aiw-ctxmenu__box').exists()).toBe(false)
    expect(w.find('.aiw-ctxmenu__tick').text()).toBe('✓')
  })

  it('面板标题缺失时回落到入口 label，不显示空标题', async () => {
    const w = mountBar({ panels: { skills: { items: [{ id: 's1', name: 'skill-a' }] } } })
    await w.findAll('.aiw-ctxbtn')[4].trigger('click')
    expect(w.find('.aiw-ctxmenu__t').text()).toBe('技能')
  })

  it('面板无条目时显示空态文案（不静默空白）', async () => {
    const w = mountBar({ panels: { model: { title: '选择模型', items: [], empty: '暂无可用模型' } } })
    await w.findAll('.aiw-ctxbtn')[2].trigger('click')
    expect(w.find('.aiw-ctxpop__empty').text()).toBe('暂无可用模型')
  })

  it('搜索输入可聚焦并按名称或描述过滤面板条目', async () => {
    const toolRows = Array.from({ length: 9 }, (_, i) => ({
      id: `tool-${i}`,
      name: i === 4 ? 'write_file' : `tool-${i}`,
      desc: i === 4 ? '写入项目文件' : '其他工具',
    }))
    const w = mountBar({ panels: { tools: { title: '选择工具', items: toolRows, multi: true } } })
    await w.findAll('.aiw-ctxbtn')[3].trigger('click')

    const search = w.get('.aiw-ctxmenu__q')
    const mouseDown = new MouseEvent('mousedown', { bubbles: true, cancelable: true })
    search.element.dispatchEvent(mouseDown)
    expect(mouseDown.defaultPrevented).toBe(false)
    await search.setValue('写入项目')

    expect(w.findAll('.aiw-ctxmenu__row').map((row) => row.text())).toHaveLength(1)
    expect(w.get('.aiw-ctxmenu__row').text()).toContain('write_file')
  })

  it('未打开任何面板时不渲染弹层（默认全关）', () => {
    const w = mountBar()
    expect(w.find('.aiw-ctxpop').exists()).toBe(false)
  })

  it('access 为 null 时不渲染权限胶囊（宿主没注入就不占位）', () => {
    const w = mountBar({ access: null })
    expect(w.find('.aiw-accessbtn').exists()).toBe(false)
  })
})

describe('ContextBar 权限胶囊', () => {
  it('显示当前档位文案，并带档位修饰类（供配色区分宽松程度）', () => {
    const w = mountBar()
    const btn = w.get('.aiw-accessbtn')
    expect(btn.text()).toContain('标准')
    expect(btn.classes()).toContain('aiw-accessbtn--standard')
  })

  it('点开列出三档，当前档 aria-checked=true', async () => {
    const w = mountBar()
    await w.get('.aiw-accessbtn').trigger('click')
    const rows = w.findAll('.aiw-ctxpop--right .aiw-ctxmenu__row')
    expect(rows.map((r) => r.find('.aiw-ctxmenu__name').text())).toEqual(['只读', '标准', '完全访问'])
    expect(rows[1].attributes('aria-checked')).toBe('true')
    expect(rows[0].attributes('aria-checked')).toBe('false')
  })

  it('切档上抛档位并收起面板', async () => {
    const w = mountBar()
    await w.get('.aiw-accessbtn').trigger('click')
    const rows = w.findAll('.aiw-ctxpop--right .aiw-ctxmenu__row')
    await rows[0].trigger('click')
    expect(w.emitted('access-change')?.[0]).toEqual(['readonly'])
    expect(w.find('.aiw-ctxpop').exists()).toBe(false)
  })
})

describe('AskPanel 待你动作区（只承载审批 / 询问，041 修订二）', () => {
  const two = [
    { id: 'a1', kind: 'approval' as const, title: '删除生产库', detail: 'DROP DATABASE prod', risk: 'critical' as const },
    { id: 'q1', kind: 'question' as const, title: '选哪个方案', options: ['方案 A', '方案 B'] },
  ]

  it('items 为空时不渲染（不占输入框上沿）', () => {
    const w = mount(AskPanel, { props: { items: [] } })
    expect(w.find('.aiw-ask').exists()).toBe(false)
  })

  it('todo 类条目不再由 AskPanel 渲染（已拆独立入口 TodoEntry）', () => {
    const w = mount(AskPanel, {
      props: { items: [{ id: 't1', kind: 'todo', title: '待办', todos: [{ title: 'x' }] } as never] },
    })
    expect(w.find('.aiw-ask').exists()).toBe(false)
  })

  it('审批带风险徽标 + 双按钮；批准/拒绝上抛 { id, decision }', async () => {
    const w = mount(AskPanel, { props: { items: two } })
    expect(w.get('.aiw-ask__badge--critical').text()).toBe('极高风险')
    const btns = w.findAll('.aiw-ask__btn')
    expect(btns).toHaveLength(2)
    await btns[0].trigger('click')
    await btns[1].trigger('click')
    expect(w.emitted('resolve')?.[0]).toEqual([{ id: 'a1', decision: 'approve' }])
    expect(w.emitted('resolve')?.[1]).toEqual([{ id: 'a1', decision: 'reject' }])
  })

  it('询问：点选项上抛 { id, option }', async () => {
    const w = mount(AskPanel, { props: { items: two } })
    const opts = w.findAll('.aiw-ask__opt')
    expect(opts.map((o) => o.text())).toEqual(['方案 A', '方案 B'])
    await opts[1].trigger('click')
    expect(w.emitted('answer')?.[0]).toEqual([{ id: 'q1', option: '方案 B' }])
  })
})

describe('TodoEntry 待办独立入口（输入框下方底行）', () => {
  const todos = [
    { id: 'x', title: '写文档', done: true },
    { id: 'y', title: '跑测试' },
    { id: 'z', title: '发布' },
  ]

  it('无待办时不渲染入口（不占底行位置）', () => {
    const w = mount(TodoEntry, { props: { todos: [], openCount: 0, progress: 0 } })
    expect(w.find('.aiw-todoentry').exists()).toBe(false)
  })

  it('入口显示「待办」+ 未完成数角标；默认收起（浮层不在）', () => {
    const w = mount(TodoEntry, { props: { todos, openCount: 2, progress: 1 / 3 } })
    const btn = w.get('.aiw-todoentry__btn')
    expect(btn.text()).toContain('待办')
    expect(btn.find('.aiw-todoentry__n').text()).toBe('2')
    expect(w.find('.aiw-todoentry__pop').exists()).toBe(false)
    expect(btn.attributes('aria-expanded')).toBe('false')
  })

  it('点入口开浮层（absolute 定位，不占内容区高度）', async () => {
    const w = mount(TodoEntry, { props: { todos, openCount: 2, progress: 1 / 3 } })
    await w.get('.aiw-todoentry__btn').trigger('click')
    expect(w.get('.aiw-todoentry__pop').exists()).toBe(true)
    expect(w.get('.aiw-todoentry__btn').attributes('aria-expanded')).toBe('true')
  })

  it('浮层内列表可整体收起/展开（头点击切换，默认展开）', async () => {
    const w = mount(TodoEntry, { props: { todos, openCount: 2, progress: 1 / 3 } })
    await w.get('.aiw-todoentry__btn').trigger('click')
    expect(w.get('.aiw-todopanel__list').element.style.display).toBe('') // 默认展开
    await w.get('.aiw-todopanel__head').trigger('click')
    expect(w.get('.aiw-todopanel__list').element.style.display).toBe('none')
    await w.get('.aiw-todopanel__head').trigger('click')
    expect(w.get('.aiw-todopanel__list').element.style.display).toBe('')
  })

  it('浮层头显示总数/未完成/进度；列表项 done 划掉', async () => {
    const w = mount(TodoEntry, { props: { todos, openCount: 2, progress: 1 / 3 } })
    await w.get('.aiw-todoentry__btn').trigger('click')
    expect(w.get('.aiw-todopanel__head').text()).toContain('待办 3 项 · 2 未完成')
    expect(w.get('.aiw-todopanel__head').text()).toContain('33%')
    const lis = w.findAll('.aiw-todopanel__list li')
    expect(lis[0].classes()).toContain('is-done')
    expect(lis[1].classes()).not.toContain('is-done')
  })
})
