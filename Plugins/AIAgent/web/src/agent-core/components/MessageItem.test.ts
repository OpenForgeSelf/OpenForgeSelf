import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import MessageItem from './MessageItem.vue'

/**
 * 消息展示精简单测（v1.8.0 适配）—— 锁三条用户可见规则：
 *  1. `config.showBotAvatar === false` 时助手消息**不渲染头像**，默认（不传该键）仍渲染 ——
 *     组件源 FR-205 验收要点是「AI 左侧带头像」，去头像只能是宿主显式裁剪，不能改默认行为；
 *  2. 用户消息**永不折叠**（自己敲的字被截断会让人以为发错了）；
 *  3. 助手长回复折叠**只做视觉裁剪**：折叠态正文仍在 DOM 里（复制 / Ctrl+F / 读屏不受影响），
 *     点「展开全文 / 收起」只翻转 aria-expanded 与截断类。
 *
 * 高度截断本身是 CSS 表现（max-height + mask）——jsdom 不跑布局，
 * 这里用 stub `scrollHeight` 驱动判定分支，真实像素由 e2e 用 getComputedStyle 断言。
 */

const baseConfig = { theme: 'page', showBotAvatar: false, avatar: '' }

/** jsdom 里 scrollHeight 恒 0，折叠判定要手喂一个「超高」值才会走进折叠分支。
    measure() 只在 content / blocks / streaming 变化时重跑，所以喂完高度要让内容动一下
    来触发重测（末尾补一个空格，断言用的都是长度区间与存在性，不受影响）。 */
async function stubScrollHeight(wrapper: ReturnType<typeof mount>, height: number, message: Record<string, unknown>) {
  const bubble = wrapper.get('.aiw-msg__bubble').element
  Object.defineProperty(bubble, 'scrollHeight', { value: height, configurable: true })
  await wrapper.setProps({ message: { ...message, content: `${message.content} ` } })
  await nextTick()
  await nextTick()
}

async function mountMsg(message: Record<string, unknown>, config = baseConfig) {
  const wrapper = mount(MessageItem, {
    props: { message, avatar: '', config },
    global: { stubs: { BlockRenderer: true, BlockBoundary: true } },
  })
  await nextTick()
  await nextTick()
  return wrapper
}

describe('MessageItem 展示精简（头像开关 / 长回复折叠）', () => {
  it('宿主配置 showBotAvatar:false 时助手消息不渲染头像', async () => {
    const w = await mountMsg({ id: 'b1', role: 'assistant', content: '你好', time: Date.now() })
    expect(w.find('.aiw-msg__avatar').exists()).toBe(false)
    expect(w.find('.aiw-msg--bot.is-noavatar').exists()).toBe(true)
  })

  it('默认（config 不给 showBotAvatar）仍带头像 —— 组件独立交付时守住 FR-205', async () => {
    const w = await mountMsg(
      { id: 'b2', role: 'assistant', content: '你好', time: Date.now() },
      { theme: 'page', avatar: '' } as never
    )
    expect(w.find('.aiw-msg__avatar').exists()).toBe(true)
    expect(w.find('.aiw-msg--bot.is-noavatar').exists()).toBe(false)
  })

  it('用户消息永不折叠，即使远超阈值', async () => {
    const w = await mountMsg({ id: 'u1', role: 'user', content: '很长的用户输入'.repeat(200), time: Date.now() })
    await stubScrollHeight(w, 900, w.props('message') as Record<string, unknown>)

    expect(w.find('.aiw-msg__fold.is-collapsed').exists()).toBe(false)
    expect(w.find('.aiw-msg__toggle').exists()).toBe(false)
  })

  it('助手长回复默认折叠，且折叠态正文不丢；点「展开全文」翻成展开态', async () => {
    const w = await mountMsg({ id: 'b3', role: 'assistant', content: '很长的助手回复'.repeat(200), time: Date.now() })
    await stubScrollHeight(w, 900, w.props('message') as Record<string, unknown>)

    expect(w.find('.aiw-msg__fold.is-collapsed').exists()).toBe(true)
    expect(w.get('.aiw-msg__toggle').text()).toBe('展开全文')
    expect(w.get('.aiw-msg__toggle').attributes('aria-expanded')).toBe('false')
    /* 折叠是视觉裁剪：正文必须还在 DOM 里 */
    expect(w.get('.aiw-msg__bubble').text().length).toBeGreaterThan(1000)

    await w.get('.aiw-msg__toggle').trigger('click')
    expect(w.find('.aiw-msg__fold.is-collapsed').exists()).toBe(false)
    expect(w.get('.aiw-msg__toggle').text()).toBe('收起')
    expect(w.get('.aiw-msg__toggle').attributes('aria-expanded')).toBe('true')
  })

  it('短回复不折叠（无按钮、无截断类）', async () => {
    const w = await mountMsg({ id: 'b4', role: 'assistant', content: '好的', time: Date.now() })
    await stubScrollHeight(w, 40, w.props('message') as Record<string, unknown>)

    expect(w.find('.aiw-msg__fold.is-collapsed').exists()).toBe(false)
    expect(w.find('.aiw-msg__toggle').exists()).toBe(false)
  })

  it('流式输出中不折叠（正在生成的文字被截断比啰嗦更糟）', async () => {
    const w = await mountMsg({
      id: 'b5',
      role: 'assistant',
      content: '正在生成的长文'.repeat(200),
      time: Date.now(),
      streaming: true,
    })
    await stubScrollHeight(w, 900, w.props('message') as Record<string, unknown>)

    expect(w.find('.aiw-msg__fold.is-collapsed').exists()).toBe(false)
    expect(w.find('.aiw-msg__toggle').exists()).toBe(false)
  })
})
