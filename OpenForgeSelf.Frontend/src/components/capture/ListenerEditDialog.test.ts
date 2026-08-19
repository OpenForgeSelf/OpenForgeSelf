import { describe, it, expect } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ListenerEditDialog from '@/components/capture/ListenerEditDialog.vue'
import type { ListenerConfig } from '@/types/capture'

interface SubmitPayload {
  id?: number
  data: {
    name: string
    listenAddress: string
    listenPort: number
    targetHost?: string | null
    targetPort?: number | null
    enabled: boolean
    description?: string | null
  }
}

const stub = { stubs: { teleport: true } }

function makeListener(partial: Partial<ListenerConfig> = {}): ListenerConfig {
  return {
    id: 1,
    name: '旧监听',
    listenAddress: '127.0.0.1',
    listenPort: 8080,
    targetHost: 'example.com',
    targetPort: 80,
    enabled: true,
    description: '旧描述',
    createdAt: '2026-08-19T08:00:00Z',
    updatedAt: '2026-08-19T08:00:00Z',
    isRunning: false,
    ...partial
  }
}

async function openDialog(props: Record<string, unknown>) {
  const wrapper = mount(ListenerEditDialog, {
    props: { visible: false, listener: null, ...props },
    global: stub,
    attachTo: document.body
  })
  await wrapper.setProps({ visible: true })
  await flushPromises()
  return wrapper
}

function findSubmitButton(wrapper: ReturnType<typeof mount>) {
  return wrapper.findAll('button').find((b) => ['创建', '保存'].some((t) => b.text().includes(t)))!
}

function findNameInput(wrapper: ReturnType<typeof mount>) {
  return wrapper.findAll('input').find((i) => (i.attributes('placeholder') ?? '').includes('本机测试代理'))!
}

describe('ListenerEditDialog 监听器弹窗', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('新建模式标题为「新建监听器」', async () => {
    const wrapper = await openDialog({ listener: null })
    expect(wrapper.text()).toContain('新建监听器')
  })

  it('编辑模式标题为「编辑监听器」且预填数据', async () => {
    const wrapper = await openDialog({ listener: makeListener() })
    expect(wrapper.text()).toContain('编辑监听器')
    const nameInput = findNameInput(wrapper)
    expect((nameInput.element as HTMLInputElement).value).toBe('旧监听')
  })

  it('新建模式提交 emit submit（无 id）且空目标归一为 null', async () => {
    const wrapper = await openDialog({ listener: null })
    await findNameInput(wrapper).setValue('新监听器')
    const submitBtn = findSubmitButton(wrapper)
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeTruthy()
    const payload = wrapper.emitted('submit')![0][0] as SubmitPayload
    expect(payload.id).toBeUndefined()
    expect(payload.data.name).toBe('新监听器')
    expect(payload.data.targetHost).toBeNull()
    expect(payload.data.targetPort).toBeNull()
  })

  it('编辑模式提交 emit submit（带 id 且保留目标）', async () => {
    const wrapper = await openDialog({ listener: makeListener() })
    const submitBtn = findSubmitButton(wrapper)
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeTruthy()
    const payload = wrapper.emitted('submit')![0][0] as SubmitPayload
    expect(payload.id).toBe(1)
    expect(payload.data.targetHost).toBe('example.com')
    expect(payload.data.targetPort).toBe(80)
  })

  it('取消按钮 emit cancel 与 update:visible(false)', async () => {
    const wrapper = await openDialog({ listener: null })
    const cancelBtn = wrapper.findAll('button').find((b) => b.text().includes('取消'))!
    await cancelBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('cancel')).toBeTruthy()
    expect(wrapper.emitted('update:visible')![0]).toEqual([false])
  })

  it('名称为空时不提交', async () => {
    const wrapper = await openDialog({ listener: null })
    const submitBtn = findSubmitButton(wrapper)
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeFalsy()
  })

  it('配置目标主机但未填目标端口时不提交', async () => {
    const wrapper = await openDialog({ listener: null })
    await findNameInput(wrapper).setValue('无端口监听')
    // 填写目标主机
    const hostInput = wrapper
      .findAll('input')
      .find((i) => (i.attributes('placeholder') ?? '').includes('example.com'))!
    await hostInput.setValue('127.0.0.1')
    await flushPromises()

    const submitBtn = findSubmitButton(wrapper)
    await submitBtn.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('submit')).toBeFalsy()
    expect(wrapper.text()).toContain('必须填写目标端口')
  })
})
