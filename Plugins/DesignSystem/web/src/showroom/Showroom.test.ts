import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { PresetMatch, Project, StylePreset } from '../api'

/**
 * 展厅深链还原的组件级回归（AC22 缺陷 D10）。
 *
 * 缺陷现场：从 `#/showroom/…?theme=dark` 直接进入时，舞台一开始确实是深色，
 * 但只要父层的项目列表晚一步到达，`buildOutfits` 就会重算、把"预设衣服"对象
 * **按 id 重建**（同一 id、新引用）。若还原逻辑挂在"衣服对象"上，这次重建会被
 * 当成一次"换衣服"，把刚还原的 `theme=dark` 重置回该件首档 `light`。
 * 只有在"深链给了非首档主题 + 衣柜随后刷新"这一组合下才暴露，浅色档看不出来。
 *
 * 判据落在 `[data-stage]` 的 `data-theme`：它是舞台真实状态的唯一出口（不是内部变量）。
 */

/** 后端入口替身（vi.hoisted：mock 工厂先于 import 执行，不能引用顶层 const） */
const apiMock = vi.hoisted(() => ({
  listPresets: vi.fn(),
  recommendPresets: vi.fn(),
  exportText: vi.fn(),
  previewCss: vi.fn(),
  listThemes: vi.fn(),
  quickCreate: vi.fn(),
}))

vi.mock('../api', () => ({ api: apiMock }))

import Showroom from './Showroom.vue'
import { projects as sharedProjects } from '../state'

const PRESETS: StylePreset[] = [
  { id: 'admin-calm', name: '克制后台', tagline: '', tones: [], kinds: [], industries: [], keywords: [] },
]

const MATCHES: PresetMatch[] = [
  { id: 'admin-calm', name: '克制后台', tagline: '', score: 1, reasons: [], request: {} },
]

/** 一件"有令牌"的项目：晚一步进入衣柜，用来触发预设衣服对象重建 */
const LATE_PROJECT: Project = {
  id: 1,
  code: 'late-p1',
  name: '迟到项目',
  kind: 'app',
  version: '1',
  status: 'active',
  parentProjectId: 0,
  defaultThemeId: 0,
  tokenCount: 12,
  componentCount: 0,
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
}

/** 与 C5 同形的深链：指定衣服 + 非首档主题 + 非默认设备 */
const DEEP_LINK = {
  mode: 'showroom' as const,
  sub: 'admin-dashboard',
  outfit: 'preset:admin-calm',
  theme: 'dark',
  device: 'mobile',
}

async function mountShowroom() {
  apiMock.listPresets.mockResolvedValue(PRESETS)
  apiMock.recommendPresets.mockResolvedValue(MATCHES)
  apiMock.listThemes.mockResolvedValue([])
  apiMock.exportText.mockResolvedValue('')
  apiMock.previewCss.mockResolvedValue({ theme: 'dark', css: '', seed: '', industry: '', hue: 0, notes: [] })

  const wrapper = mount(Showroom, {
    props: { initial: DEEP_LINK },
    global: {
      stubs: {
        Wardrobe: true,
        TunePanel: true,
        CompareStrip: true,
        PanelState: true,
        DeviceFrame: true,
        OutfitScope: true,
      },
    },
  })
  await flushPromises()
  await flushPromises()
  return wrapper
}

describe('Showroom 深链还原（AC22）', () => {
  beforeEach(() => {
    sharedProjects.value = []
    vi.clearAllMocks()
  })

  it('深链的 theme/device/scene/page 全部落到舞台', async () => {
    const wrapper = await mountShowroom()
    const stage = wrapper.get('[data-stage]')
    expect(stage.attributes('data-outfit')).toBe('preset:admin-calm')
    expect(stage.attributes('data-theme')).toBe('dark')
    expect(stage.attributes('data-device')).toBe('mobile')
    expect(stage.attributes('data-scene')).toBe('admin')
    expect(stage.attributes('data-page')).toBe('admin-dashboard')
  })

  it('衣柜随后刷新（衣服对象按 id 重建）不得重置深链主题', async () => {
    const wrapper = await mountShowroom()
    expect(wrapper.get('[data-stage]').attributes('data-theme')).toBe('dark')

    // 父层项目列表到达：bundle 重算 → 预设衣服对象被重建（同 id、新引用）
    sharedProjects.value = [LATE_PROJECT]
    await flushPromises()

    expect(wrapper.get('[data-stage]').attributes('data-theme')).toBe('dark')
    expect(wrapper.get('[data-stage]').attributes('data-device')).toBe('mobile')
  })

  it('深链指定预设时，衣柜首个非空快照只有项目也不得把选择闩死（缺陷 D14）', async () => {
    // 真实环境的到达顺序：项目走宿主 shell 的 `projects`（先到），预设要等
    // `GET presets` + `presets/recommend` 合并后一次性落值（后到）。
    // 于是"衣柜首个非空快照"可能只有项目衣服——此时若就消费深链并回落 `list[0]`，
    // 预设到达后深链指定的那件将永不选中（复验实测 `aria-selected` 恒 false）。
    sharedProjects.value = [LATE_PROJECT]
    const wrapper = await mountShowroom()

    expect(wrapper.get('[data-stage]').attributes('data-outfit')).toBe('preset:admin-calm')
    expect(wrapper.get('[data-stage]').attributes('data-theme')).toBe('dark')
    expect(wrapper.get('[data-stage]').attributes('data-device')).toBe('mobile')
    expect(wrapper.get('[data-stage]').attributes('data-page')).toBe('admin-dashboard')
  })
})