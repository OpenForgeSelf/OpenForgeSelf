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

  it('渲染主题/强调色/动画开关，字体大小设置已移除', () => {
    const wrapper = mount(AppearancePanel, { attachTo: document.body })
    expect(wrapper.text()).toContain('主题')
    expect(wrapper.text()).toContain('强调色')
    expect(wrapper.text()).toContain('动画效果')
    expect(wrapper.text()).not.toContain('字体大小')
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

  it('隐藏独立纯色选项，保留 6 张预设图及背景设置操作', () => {
    const wrapper = mount(BackgroundSettings)
    // 主题切换会自动联动背景色，纯色 swatch 区块按设计隐藏。
    expect(wrapper.findAll('.bg-swatch')).toHaveLength(0)
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
