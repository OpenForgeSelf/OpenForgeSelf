import { describe, it, expect, beforeEach, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import PluginsPanel from '@/components/settings/PluginsPanel.vue'
import { pluginApi } from '@/services/pluginApi'

// Mock ElMessage
vi.mock('element-plus', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...(actual as object),
    ElMessage: {
      success: vi.fn(),
      warning: vi.fn(),
      error: vi.fn(),
    },
  }
})

vi.mock('@/services/pluginApi', () => ({
  pluginApi: {
    fetchPluginUpdateSettings: vi.fn(),
    updatePluginUpdateSettings: vi.fn(),
  },
}))

describe('PluginsPanel — 插件更新源（本地包目录）', () => {
  const mockFetch = vi.mocked(pluginApi.fetchPluginUpdateSettings)
  const mockUpdate = vi.mocked(pluginApi.updatePluginUpdateSettings)

  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('挂载后回显已保存的本地目录', async () => {
    mockFetch.mockResolvedValue({ localDir: 'D:\\plugin-packages' })

    const wrapper = mount(PluginsPanel)
    await flushPromises()

    expect(mockFetch).toHaveBeenCalledTimes(1)
    const input = wrapper.findComponent({ name: 'ElInput' })
    expect((input.props('modelValue') as string)).toBe('D:\\plugin-packages')
  })

  it('点击保存调用后端并提示成功', async () => {
    mockFetch.mockResolvedValue({ localDir: '' })
    mockUpdate.mockResolvedValue({ localDir: 'D:\\new-packages' })

    const wrapper = mount(PluginsPanel)
    await flushPromises()

    const input = wrapper.findComponent({ name: 'ElInput' })
    await input.vm.$emit('update:modelValue', 'D:\\new-packages')
    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(mockUpdate).toHaveBeenCalledWith({ localDir: 'D:\\new-packages' })
    expect((input.props('modelValue') as string)).toBe('D:\\new-packages')
  })

  it('清空目录保存 = 停用插件更新源', async () => {
    mockFetch.mockResolvedValue({ localDir: 'D:\\plugin-packages' })
    mockUpdate.mockResolvedValue({ localDir: '' })

    const wrapper = mount(PluginsPanel)
    await flushPromises()

    const input = wrapper.findComponent({ name: 'ElInput' })
    await input.vm.$emit('update:modelValue', '')
    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(mockUpdate).toHaveBeenCalledWith({ localDir: '' })
  })

  it('保存失败时提示错误且不抛异常', async () => {
    mockFetch.mockResolvedValue({ localDir: '' })
    mockUpdate.mockRejectedValue(new Error('插件更新源目录不存在'))

    const wrapper = mount(PluginsPanel)
    await flushPromises()

    await inputSetValue(wrapper, 'D:\\bad-path')
    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(mockUpdate).toHaveBeenCalledWith({ localDir: 'D:\\bad-path' })
  })

  async function inputSetValue(wrapper: ReturnType<typeof mount>, value: string): Promise<void> {
    const input = wrapper.findComponent({ name: 'ElInput' })
    await input.vm.$emit('update:modelValue', value)
  }
})
