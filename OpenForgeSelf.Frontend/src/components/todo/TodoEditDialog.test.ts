import { describe, it, expect } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import TodoEditDialog from '@/components/todo/TodoEditDialog.vue'
import type { TodoItem } from '@/types/todo'

interface SubmitPayload {
  id?: number
  data: { title?: string; remark?: string; dueDate?: string }
}

const stub = { stubs: { teleport: true } }

async function openDialog(props: Record<string, unknown>) {
  const wrapper = mount(TodoEditDialog, {
    props: { visible: false, ...props },
    global: stub,
    attachTo: document.body,
  })
  await wrapper.setProps({ visible: true })
  await flushPromises()
  return wrapper
}

describe('TodoEditDialog', () => {
  it('新建模式标题为“新建待办”', async () => {
    const wrapper = await openDialog({ todo: null })
    expect(wrapper.text()).toContain('新建待办')
  })

  it('编辑模式标题为“编辑待办”且预填数据', async () => {
    const todo: TodoItem = {
      id: 7,
      title: '旧标题',
      remark: '旧备注',
      status: 'Pending',
      createdAt: '2026-07-20T08:00:00Z',
      updatedAt: '2026-07-20T08:00:00Z',
    }
    const wrapper = await openDialog({ todo })
    expect(wrapper.text()).toContain('编辑待办')
    const titleInput = wrapper.find('input')
    expect((titleInput.element as HTMLInputElement).value).toBe('旧标题')
  })

  it('新建模式提交 emit submit（无 id）', async () => {
    const wrapper = await openDialog({ todo: null })
    const titleInput = wrapper.find('input')
    await titleInput.setValue('新待办标题')
    const remarkInput = wrapper.find('textarea')
    await remarkInput.setValue('一些备注')
    const submitBtn = wrapper.findAll('button').find((b) => b.text().includes('创建'))!
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeTruthy()
    const payload = wrapper.emitted('submit')![0][0] as SubmitPayload
    expect(payload.id).toBeUndefined()
    expect(payload.data.title).toBe('新待办标题')
    expect(payload.data.remark).toBe('一些备注')
  })

  it('编辑模式提交 emit submit（带 id）', async () => {
    const todo: TodoItem = {
      id: 9,
      title: '编辑标题',
      status: 'Pending',
      createdAt: '2026-07-20T08:00:00Z',
      updatedAt: '2026-07-20T08:00:00Z',
    }
    const wrapper = await openDialog({ todo })
    const submitBtn = wrapper.findAll('button').find((b) => b.text().includes('保存'))!
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeTruthy()
    const payload = wrapper.emitted('submit')![0][0] as SubmitPayload
    expect(payload.id).toBe(9)
  })

  it('取消按钮 emit cancel 与 update:visible(false)', async () => {
    const wrapper = await openDialog({ todo: null })
    const cancelBtn = wrapper.findAll('button').find((b) => b.text().includes('取消'))!
    await cancelBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('cancel')).toBeTruthy()
    expect(wrapper.emitted('update:visible')).toBeTruthy()
    expect(wrapper.emitted('update:visible')![0]).toEqual([false])
  })

  it('标题为空时不提交', async () => {
    const wrapper = await openDialog({ todo: null })
    const submitBtn = wrapper.findAll('button').find((b) => b.text().includes('创建'))!
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeFalsy()
  })
})
