import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppearancePanel from '@/components/settings/AppearancePanel.vue'
import BackgroundSettings from '@/components/BackgroundSettings.vue'

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

describe('AppearancePanel — 基础外观设置', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('渲染主题/强调色/字体大小/动画开关', () => {
    const wrapper = mount(AppearancePanel, { attachTo: document.body })
    expect(wrapper.text()).toContain('外观')
    expect(wrapper.text()).toContain('主题')
    expect(wrapper.text()).toContain('强调色')
    expect(wrapper.text()).toContain('字体大小')
    expect(wrapper.text()).toContain('动画效果')
  })

  it('背景区复用 BackgroundSettings 组件（单一实现，不重复造轮子）', () => {
    const wrapper = mount(AppearancePanel, { attachTo: document.body })
    // 旧的手填 URL + 透明度 ElSlider 已删除，替换为 BackgroundSettings
    expect(wrapper.findComponent(BackgroundSettings).exists()).toBe(true)
    expect(wrapper.text()).not.toContain('背景图透明度')
  })
})

describe('BackgroundSettings — 背景设置内联组件', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('渲染 12 纯色 + 6 预设图 + 上传 + URL 输入 + 遮罩 + 恢复默认', () => {
    const wrapper = mount(BackgroundSettings)
    // 12 纯色 swatch
    expect(wrapper.findAll('.bg-swatch').length).toBe(12)
    // 6 预设背景图
    expect(wrapper.findAll('.bg-preset').length).toBe(6)
    // 上传按钮
    expect(wrapper.text()).toContain('上传本地图片')
    // URL 输入
    expect(wrapper.find('.url-input').exists()).toBe(true)
    expect(wrapper.find('.url-apply').exists()).toBe(true)
    // 遮罩滑块
    expect(wrapper.find('.slider-row input[type="range"]').exists()).toBe(true)
    // 恢复默认
    expect(wrapper.text()).toContain('恢复默认')
  })
})
