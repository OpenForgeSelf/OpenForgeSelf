import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ComposeTrigger from './ComposeTrigger.vue'

/**
 * 行内触发面板（`/` 命令 / `@` 提及共用）的组件契约。
 *
 * 锁四条：① 分组渲染、空组不占位；② 按 query 内存过滤；
 *       ③ 键盘 move 循环、current 取到正确项（跨分组连续编号）；
 *       ④ 无匹配显示空态而非白板。
 *
 * 面板定位/圆角等是纯 CSS，jsdom 不跑布局，改由 e2e 用 getComputedStyle 断言。
 */

const groups = [
  {
    key: 'builtin',
    title: '内置命令',
    items: [
      { id: 'builtin:clear', name: '/clear', desc: '开始一个新会话' },
      { id: 'builtin:help', name: '/help', desc: '列出可用命令' },
    ],
  },
  {
    key: 'tool',
    title: '工具',
    items: [{ id: 'tool:read_file', name: 'read_file', desc: '读取文件内容', meta: 'FileTools' }],
  },
  { key: 'empty', title: '空组', items: [] },
]

function mountPanel(query = '') {
  return mount(ComposeTrigger, { props: { char: '/', groups, query, title: '命令与上下文' } })
}

describe('ComposeTrigger — 行内触发面板', () => {
  it('渲染分组标题与候选项，空组不占位', () => {
    const w = mountPanel()
    expect(w.findAll('.aiw-trg__grp-t').map((n) => n.text())).toEqual(['内置命令', '工具'])
    expect(w.findAll('.aiw-trg__opt')).toHaveLength(3)
  })

  it('按 query 过滤（名称命中）', () => {
    const w = mountPanel('read')
    expect(w.findAll('.aiw-trg__opt')).toHaveLength(1)
    expect(w.get('.aiw-trg__opt-name').text()).toBe('read_file')
  })

  it('按描述命中（搜「新会话」能找到 /clear）', () => {
    const w = mountPanel('新会话')
    expect(w.findAll('.aiw-trg__opt')).toHaveLength(1)
    expect(w.get('.aiw-trg__opt-name').text()).toBe('/clear')
  })

  it('无匹配显示空态，不留一排空标题', () => {
    const w = mountPanel('zzz')
    expect(w.find('.aiw-trg__empty').exists()).toBe(true)
    expect(w.findAll('.aiw-trg__grp-t')).toHaveLength(0)
  })

  it('★ 键盘 move 跨分组连续编号，且到头循环', () => {
    const w = mountPanel()
    const vm = w.vm as unknown as { move: (d: number) => void; current: () => { name: string } | null }
    expect(vm.current()?.name).toBe('/clear')
    vm.move(1)
    expect(vm.current()?.name).toBe('/help')
    vm.move(1)
    expect(vm.current()?.name).toBe('read_file')
    vm.move(1) // 到底回到第一条
    expect(vm.current()?.name).toBe('/clear')
    vm.move(-1) // 反向越界回到最后一条
    expect(vm.current()?.name).toBe('read_file')
  })

  it('高亮索引随 query 变化归零（否则会选中看不见的那条）', async () => {
    const w = mountPanel()
    const vm = w.vm as unknown as { move: (d: number) => void; current: () => { name: string } | null }
    vm.move(1)
    expect(vm.current()?.name).toBe('/help')
    await w.setProps({ query: 'read' })
    expect(vm.current()?.name).toBe('read_file')
  })

  it('点击候选项派发 pick（带完整 item，供宿主决定插什么）', async () => {
    const w = mountPanel()
    await w.get('.aiw-trg__opt').trigger('click')
    expect(w.emitted('pick')?.[0]?.[0]).toMatchObject({ id: 'builtin:clear', name: '/clear' })
  })

  it('无障碍：listbox + option 角色齐全，高亮项 aria-selected', () => {
    const w = mountPanel()
    expect(w.get('.aiw-trg__list').attributes('role')).toBe('listbox')
    expect(w.findAll('[role="option"]')).toHaveLength(3)
    expect(w.findAll('[role="option"]')[0].attributes('aria-selected')).toBe('true')
    expect(w.findAll('[role="option"]')[1].attributes('aria-selected')).toBe('false')
  })

  it('无 groups 时不炸（宿主还没取到数据时的首帧）', () => {
    const w = mount(ComposeTrigger, { props: { char: '/', groups: [] } })
    expect(w.find('.aiw-trg__empty').exists()).toBe(true)
  })
})
