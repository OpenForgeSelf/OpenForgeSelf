import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SessionPanel from './SessionPanel.vue'
import type { AgentDefinition, SessionSummary } from '../types'

/**
 * 会话管理改造（T4）组件契约单测 —— 锁住四项用户可见行为：
 *  1. 历史会话分组**默认收起**（收起态仍显示条数），点标题箭头可展开；
 *  2. 会话项操作由「删除」改「归档」，一律以**全 id** 回传（杜绝 T3 前缀 bug），且不连带触发切换；
 *  3. 归档图标改小（10px）且为内联 SVG —— 不新增宿主图标桥白名单条目（该白名单是手工维护的脆弱点）；
 *  4. 列表为空（全部已归档 / 无会话）时不渲染任何会话项。
 *
 * 「悬浮才显示图标」属 CSS 表现，jsdom 不跑布局，改由 e2e 用 getComputedStyle 断言（real browser）。
 */

/** 构造最小 props。 */
function mountPanel(sessions: SessionSummary[]) {
  return mount(SessionPanel, {
    props: {
      sessionId: 'cur-0001',
      messageCount: 3,
      toolCallCount: 1,
      tokenText: '—',
      agents: [] as AgentDefinition[],
      activeAgentId: '',
      sessions,
    },
  })
}

const twoSessions: SessionSummary[] = [
  { sessionId: 'mu7p581h-6oscch', title: '会话一', messageCount: 2, lastTime: '2026-09-22T10:00:00' },
  { sessionId: 'mu7p581h-abcdef', title: '会话二', messageCount: 1, lastTime: '2026-09-22T09:00:00' },
]

/** 展开历史会话分组（默认收起）。 */
async function expand(panel: ReturnType<typeof mountPanel>) {
  await panel.get('.sess__head-toggle').trigger('click')
}

describe('SessionPanel 历史会话（默认收起 / 归档 / 小图标）', () => {
  it('默认收起：不渲染会话项，但标题旁显示真实条数', () => {
    const panel = mountPanel(twoSessions)

    expect(panel.findAll('.sess__item')).toHaveLength(0)
    expect(panel.get('.sess__count').text()).toBe('2')
    // 收起态箭头朝右（rotate -90deg）
    expect(panel.get('.sess__chevron').classes()).toContain('sess__chevron--collapsed')
  })

  it('点标题箭头展开：列出全部会话项，箭头恢复朝下；再点收起', async () => {
    const panel = mountPanel(twoSessions)

    await expand(panel)
    expect(panel.findAll('.sess__item')).toHaveLength(2)
    expect(panel.get('.sess__chevron').classes()).not.toContain('sess__chevron--collapsed')

    await panel.get('.sess__head-toggle').trigger('click')
    expect(panel.findAll('.sess__item')).toHaveLength(0)
  })

  it('点会话项：以全 id 回传 select-session（不做前缀裁剪）', async () => {
    const panel = mountPanel(twoSessions)
    await expand(panel)

    await panel.findAll('.sess__item')[0].trigger('click')

    expect(panel.emitted('select-session')?.[0]).toEqual(['mu7p581h-6oscch'])
  })

  it('「删除」已改「归档」：无删除按钮，归档按钮以全 id 回传且不连带触发切换', async () => {
    const panel = mountPanel(twoSessions)
    await expand(panel)

    // 删除入口已移除
    expect(panel.find('.sess__item-del').exists()).toBe(false)

    const archiveButtons = panel.findAll('.sess__item-archive')
    expect(archiveButtons).toHaveLength(2)
    expect(archiveButtons[0].attributes('aria-label')).toBe('归档会话')

    await archiveButtons[0].trigger('click')

    expect(panel.emitted('archive-session')?.[0]).toEqual(['mu7p581h-6oscch'])
    // @click.stop：归档不得同时切换会话
    expect(panel.emitted('select-session')).toBeUndefined()
  })

  it('归档图标改小（10px）且为内联 SVG，不依赖宿主图标白名单', async () => {
    const panel = mountPanel(twoSessions)
    await expand(panel)

    const svg = panel.get('.sess__item-archive svg')
    expect(svg.attributes('width')).toBe('10')
    expect(svg.attributes('height')).toBe('10')
  })

  it('已归档会话不出现在列表（agent 页只拿到未归档数据时不渲染任何项）', async () => {
    const panel = mountPanel([])
    await expand(panel)

    expect(panel.findAll('.sess__item')).toHaveLength(0)
    expect(panel.get('.sess__count').text()).toBe('0')
  })
})
