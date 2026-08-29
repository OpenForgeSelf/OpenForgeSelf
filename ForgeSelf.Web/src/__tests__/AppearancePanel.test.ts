import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppearancePanel from '@/components/settings/AppearancePanel.vue'
import { useAppearanceStore } from '@/stores/appearance'

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

describe('AppearancePanel — 背景图透明度滑块', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  function mountPanel() {
    return mount(AppearancePanel, {
      attachTo: document.body,
    })
  }

  it('未设置背景图时滑块为 disabled 状态', () => {
    const wrapper = mountPanel()
    const store = useAppearanceStore()
    // 确认无背景图
    expect(store.backgroundImage).toBe('')
    // 滑块 label 应渲染
    expect(wrapper.text()).toContain('背景图透明度')
    expect(wrapper.text()).toContain('控制背景图片的清晰程度')
    // 找到第二个 ElSlider（第一个是字体大小），应处于 disabled 状态
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    expect(opacitySlider.props('disabled')).toBe(true)
  })

  it('设置背景图后滑块为启用状态', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    const wrapper = mountPanel()
    // 找到第二个 ElSlider
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    expect(opacitySlider.props('disabled')).toBe(false)
  })

  it('滑块初始值等于 store 的 backgroundOpacity', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    store.setBackgroundOpacity(75)
    const wrapper = mountPanel()
    // 验证百分比显示
    expect(wrapper.text()).toContain('75%')
    // 找到第二个 ElSlider，验证 modelValue
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    expect(opacitySlider.props('modelValue')).toBe(75)
  })

  it('拖动滑块后 store 的 backgroundOpacity 同步更新', async () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    const wrapper = mountPanel()
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    // 模拟滑块值变化
    await opacitySlider.vm.$emit('update:modelValue', 40)
    await wrapper.vm.$nextTick()
    // 验证 store 已更新
    expect(store.backgroundOpacity).toBe(40)
    // 验证百分比显示更新
    expect(wrapper.text()).toContain('40%')
  })

  it('清除背景图后滑块变为 disabled 状态', () => {
    const store = useAppearanceStore()
    store.setBackgroundImage('https://example.com/bg.jpg')
    const wrapper = mountPanel()
    // 先确认有背景图时滑块启用
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    expect(opacitySlider.props('disabled')).toBe(false)
    // 清除背景图
    store.clearBackgroundImage()
    // 滑块应变为 disabled
    // 因为 store 的 backgroundImage 为空，disabled 绑定到 !appearanceStore.backgroundImage
    expect(store.backgroundImage).toBe('')
    // 重新 mount 确认状态一致（模拟 UI 重新渲染）
    // 实际场景中 AppearancePanel 的 slot 计算属性取决于 store.backgroundImage
    // 这里直接验证 store 状态
    expect(opacitySlider.props('disabled')).toBe(false) // 旧 wrapper 的 props 不变
    // 重新 mount 以获取新状态
    const wrapper2 = mount(AppearancePanel, { attachTo: document.body })
    const sliders2 = wrapper2.findAllComponents({ name: 'ElSlider' })
    const opacitySlider2 = sliders2[1]
    expect(opacitySlider2.props('disabled')).toBe(true)
  })

  it('重新设置背景图后滑块恢复启用状态，显示保留的透明度值', () => {
    const store = useAppearanceStore()
    // 设置背景图和透明度
    store.setBackgroundImage('https://example.com/bg.jpg')
    store.setBackgroundOpacity(72)
    // 清除背景图（透明度保留）
    store.clearBackgroundImage()
    expect(store.backgroundImage).toBe('')
    expect(store.backgroundOpacity).toBe(72)
    // 重新设置背景图
    store.setBackgroundImage('https://example.com/new-bg.jpg')
    // 重新 mount 验证
    const wrapper = mount(AppearancePanel, { attachTo: document.body })
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders.length).toBeGreaterThanOrEqual(2)
    const opacitySlider = sliders[1]
    // 滑块应启用
    expect(opacitySlider.props('disabled')).toBe(false)
    // 透明度应保留之前的值
    expect(opacitySlider.props('modelValue')).toBe(72)
  })
})