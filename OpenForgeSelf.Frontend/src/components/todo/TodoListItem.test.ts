import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TodoListItem from '@/components/todo/TodoListItem.vue'
import type { TodoItem } from '@/types/todo'

function makeTodo(overrides: Partial<TodoItem> = {}): TodoItem {
  return {
    id: 1,
    title: '买牛奶',
    status: 'Pending',
    createdAt: '2026-07-20T08:00:00Z',
    updatedAt: '2026-07-20T08:00:00Z',
    ...overrides,
  }
}

describe('TodoListItem', () => {
  it('渲染标题文本', () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo() } })
    expect(wrapper.text()).toContain('买牛奶')
  })

  it('有备注时渲染备注', () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo({ remark: '记得买低脂' }) } })
    expect(wrapper.text()).toContain('记得买低脂')
  })

  it('完成状态显示已完成标签且勾选框选中', () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo({ status: 'Completed' }) } })
    expect(wrapper.text()).toContain('已完成')
    const checkbox = wrapper.findComponent({ name: 'ElCheckbox' })
    expect(checkbox.props('modelValue')).toBe(true)
  })

  it('逾期未完成显示已逾期标签', () => {
    const past = new Date(Date.now() - 86400000).toISOString()
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo({ dueDate: past, status: 'Pending' }) } })
    expect(wrapper.text()).toContain('已逾期')
  })

  it('显示截止日期文本', () => {
    const wrapper = mount(TodoListItem, {
      props: { todo: makeTodo({ dueDate: '2026-08-01T00:00:00Z' }) },
    })
    expect(wrapper.text()).toContain('截止:')
  })

  it('勾选状态变化触发 toggle-status', async () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo() } })
    const checkbox = wrapper.findComponent({ name: 'ElCheckbox' })
    await checkbox.vm.$emit('change', true)
    expect(wrapper.emitted('toggle-status')).toBeTruthy()
    expect(wrapper.emitted('toggle-status')![0]).toEqual([1])
  })

  it('点击编辑按钮触发 edit', async () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo() } })
    const editBtn = wrapper.find('[aria-label="编辑待办"]')
    await editBtn.trigger('click')
    expect(wrapper.emitted('edit')).toBeTruthy()
    expect(wrapper.emitted('edit')![0]).toEqual([1])
  })

  it('点击删除按钮触发 delete', async () => {
    const wrapper = mount(TodoListItem, { props: { todo: makeTodo() } })
    const delBtn = wrapper.find('[aria-label="删除待办"]')
    await delBtn.trigger('click')
    expect(wrapper.emitted('delete')).toBeTruthy()
    expect(wrapper.emitted('delete')![0]).toEqual([1])
  })
})
