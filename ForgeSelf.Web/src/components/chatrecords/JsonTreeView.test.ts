import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import JsonTreeView from './JsonTreeView.vue'

// 原始数据树状视图：键含义对照 + 可展开/收起 + 默认收起
describe('JsonTreeView 树状展开', () => {
  it('对象根节点显示键名与中文含义', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: { model: 'gpt-4o' }, name: 'root', defaultExpanded: true }
    })
    const text = wrapper.text()
    expect(text).toContain('model')
    expect(text).toContain('模型名称') // 含义对照
    expect(text).toContain('gpt-4o') // 叶子值
  })

  it('默认收起（defaultExpanded 不传）：子键不可见', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: { role: 'user', content: 'hi' }, name: 'root' }
    })
    expect(wrapper.text()).not.toContain('role')
    expect(wrapper.text()).not.toContain('hi')
  })

  it('点击根节点展开后，直接子键可见', async () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: { role: 'user', content: 'hi' }, name: 'root' }
    })
    await wrapper.find('.node-row').trigger('click')
    const text = wrapper.text()
    expect(text).toContain('role')
    expect(text).toContain('user')
    expect(text).toContain('hi')
  })

  it('defaultExpanded=true：顶层子节点直接可见，但嵌套仍默认收起', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: { messages: [{ role: 'user', content: 'hi' }] }, name: 'root', defaultExpanded: true }
    })
    // 顶层 messages 立即可见（根已展开）
    expect(wrapper.text()).toContain('messages')
    // 但 messages 数组内的 role/content 仍收起
    expect(wrapper.text()).not.toContain('role')
    expect(wrapper.text()).not.toContain('hi')
  })

  it('嵌套节点可逐层展开', async () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: { messages: [{ role: 'user', content: 'hi' }] }, name: 'root', defaultExpanded: true }
    })
    // 展开 messages 数组（rows: [0]=root, [1]=messages）
    let rows = wrapper.findAll('.node-row')
    await rows[1].trigger('click')
    // 再展开下标 0 对象（rows: [0]=root, [1]=messages, [2]=0）
    rows = wrapper.findAll('.node-row')
    await rows[2].trigger('click')
    expect(wrapper.text()).toContain('role')
    expect(wrapper.text()).toContain('hi')
  })

  it('数组以索引为键展示数量', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: [{ a: 1 }, { a: 2 }], defaultExpanded: true }
    })
    expect(wrapper.text()).toContain('数组[2]')
  })

  it('基本类型叶子显示类型与值（字符串带引号）', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: 'hello', name: 'title' }
    })
    const text = wrapper.text()
    expect(text).toContain('title')
    expect(text).toContain('字符串')
    expect(text).toContain('"hello"')
  })

  it('null 值正确显示', () => {
    const wrapper = mount(JsonTreeView, {
      props: { data: null, name: 'x' }
    })
    expect(wrapper.text()).toContain('null')
  })
})
