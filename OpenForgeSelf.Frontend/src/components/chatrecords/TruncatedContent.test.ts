import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TruncatedContent from './TruncatedContent.vue'

const LONG = 'x'.repeat(800)

describe('TruncatedContent 超长展开/收起 + 滚动', () => {
  it('短内容：无展开按钮、无滚动容器', () => {
    const wrapper = mount(TruncatedContent, { props: { content: '短内容', maxLength: 500 } })
    expect(wrapper.find('.action-row').exists()).toBe(false)
    expect(wrapper.find('.content-scroll').exists()).toBe(false)
    expect(wrapper.text()).toContain('短内容')
  })

  it('超长内容：默认收起，显示「展开」按钮与截断省略号', () => {
    const wrapper = mount(TruncatedContent, { props: { content: LONG, maxLength: 500 } })
    expect(wrapper.find('.action-row').exists()).toBe(true)
    expect(wrapper.find('.content-scroll').exists()).toBe(false)
    // 收起态文本被截断（含省略号，不含完整 800 字符）
    const preText = wrapper.find('.content-text').text()
    expect(preText).toContain('…')
    expect(preText.length).toBeLessThan(LONG.length)
    expect(wrapper.text()).toContain('展开')
  })

  it('点击展开后出现滚动容器（content-scroll），展示完整内容', async () => {
    const wrapper = mount(TruncatedContent, { props: { content: LONG, maxLength: 500 } })
    await wrapper.findAll('.act-btn')[0].trigger('click')
    expect(wrapper.find('.content-scroll').exists()).toBe(true)
    expect(wrapper.find('.content-text').text()).toBe(LONG) // 完整未截断
    expect(wrapper.text()).toContain('收起')
  })

  it('再次点击收起：滚容容器消失，回到截断', async () => {
    const wrapper = mount(TruncatedContent, { props: { content: LONG, maxLength: 500 } })
    const expandBtn = wrapper.findAll('.act-btn')[0]
    await expandBtn.trigger('click')
    expect(wrapper.find('.content-scroll').exists()).toBe(true)
    await expandBtn.trigger('click')
    expect(wrapper.find('.content-scroll').exists()).toBe(false)
    expect(wrapper.find('.content-text').text()).toContain('…')
  })

  it('复制按钮：触发后显示「已复制」', async () => {
    // 模拟剪贴板（navigator.clipboard 在 jsdom 下只读，需用 defineProperty 注入）
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      configurable: true,
      writable: true,
    })
    const wrapper = mount(TruncatedContent, { props: { content: LONG, maxLength: 500 } })
    const copyBtn = wrapper.findAll('.act-btn')[1]
    await copyBtn.trigger('click')
    expect(writeText).toHaveBeenCalledWith(LONG)
    // 等待 setTimeout 显示
    await new Promise((r) => setTimeout(r, 0))
    expect(wrapper.text()).toContain('已复制')
  })
})
