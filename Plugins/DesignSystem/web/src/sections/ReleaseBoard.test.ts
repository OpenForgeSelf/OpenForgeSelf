import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import type { MetaInfo, Project, Release, ReleaseDiff } from '../api'

/**
 * 版本对比「整类无法比较」提示的组件级回归（AC17 剩下的那一档证据）。
 *
 * 为什么此前只能标 Unknown：`ReleaseService.Create` 永远按 `CurrentSchema` 写快照，
 * 隔离库里造不出 schema 2 的旧快照；而 `ReleaseDto` 只回 `snapshotAvailable` 布尔、
 * **不回快照文件路径**，所以连"e2e 里把旧文件改成 schema 2 形态"这条路都不存在
 * （要让界面出现这行提示，只能伸手进宿主的数据目录猜文件名）。
 * 后端出参那一半早已 Verified（`GuidelineReleaseTests` 的 `NotComparableKinds`），
 * 缺的只是"界面到底画没画"。这里把后端三种真实出参各喂一次，逐条断言
 * **该出现时出现、不该出现时不出现**——提示是条件渲染，不是常驻装饰。
 */

const apiMock = vi.hoisted(() => ({
  listReleases: vi.fn(),
  createRelease: vi.fn(),
  diffReleases: vi.fn(),
  releaseTokens: vi.fn(),
  releaseDtcg: vi.fn(),
}))

vi.mock('../api', () => ({ api: apiMock }))

import ReleaseBoard from './ReleaseBoard.vue'
import { currentProject, meta } from '../state'

const R1: Release = {
  id: 11,
  projectId: 1,
  version: '1.0.0',
  status: 'published',
  tokensHash: 'a'.repeat(64),
  tokenCount: 250,
  auditSummary: '12 项（critical 0 / warning 2 / info 10）',
  auditPassed: true,
  sourceReleaseId: 0,
  releaseNotes: '',
  snapshotAvailable: true,
  createdAt: '2026-10-01T00:00:00Z',
}
const R2: Release = { ...R1, id: 12, version: '1.1.0', tokensHash: 'b'.repeat(64) }

/** 后端 `ReleaseDiff` 的"零差异骨架"，各用例只覆盖自己要的那一列 */
function diff(over: Partial<ReleaseDiff>): ReleaseDiff {
  return {
    from: '1.0.0',
    to: '1.1.0',
    added: [],
    removed: [],
    changed: [],
    missingThemes: [],
    specsAdded: [],
    specsRemoved: [],
    specsChanged: [],
    specsComparable: true,
    total: 0,
    isEmpty: false,
    ...over,
  }
}

/** 挂载 → 选 from/to → 点「对比」，返回 wrapper（提示只在 diff 落地后才渲染，所以必须真点一次） */
async function compareWith(d: ReleaseDiff): Promise<VueWrapper> {
  apiMock.diffReleases.mockResolvedValue(d)
  const wrapper = mount(ReleaseBoard)
  await flushPromises()
  const picks = wrapper.findAll('.rb__diff-pick select')
  expect(picks).toHaveLength(2)
  await picks[0]!.setValue(String(R1.id))
  await picks[1]!.setValue(String(R2.id))
  await wrapper.get('.rb__diff-pick button').trigger('click')
  await flushPromises()
  expect(apiMock.diffReleases).toHaveBeenCalledWith(1, R1.id, R2.id)
  return wrapper
}

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.setItem('forge_api_token', 'component-test-token')
  meta.value = {
    pluginId: 'design-system',
    capabilities: ['releases', 'releases.diff'],
    modelVersion: '3.1.0',
  } as unknown as MetaInfo
  currentProject.value = {
    id: 1,
    code: 'demo',
    name: 'Demo',
    kind: 'app',
    version: '1.0.0',
    status: 'active',
    parentProjectId: 0,
    defaultThemeId: 0,
    tokenCount: 250,
  } as unknown as Project
  apiMock.listReleases.mockResolvedValue([R2, R1])
  apiMock.releaseTokens.mockResolvedValue({ tokens: [], count: 0 })
  apiMock.releaseDtcg.mockResolvedValue({ content: '' })
})

describe('ReleaseBoard 跨 schema 的「无法比较」提示', () => {
  it('schema 2 ↔ 3：整节能比但 guideline 类没记过 → 点名这一类，且不说"没有变化"', async () => {
    const wrapper = await compareWith(diff({ notComparableKinds: ['guideline'] }))
    const hint = wrapper.find('[data-diff-not-comparable]')
    expect(hint.exists(), '后端报了 notComparableKinds，界面必须画出这行提示').toBe(true)
    expect(hint.text()).toContain('guideline')
    expect(hint.text()).toContain('无法比较')
    expect(wrapper.find('.rb__diff-body').text()).not.toContain('两版内容一致')
  })

  it('同类可比（notComparableKinds 为空）时不得出现该提示（否则它是常驻装饰）', async () => {
    const wrapper = await compareWith(diff({ notComparableKinds: [] }))
    expect(wrapper.find('[data-diff-not-comparable]').exists()).toBe(false)
  })

  it('schema 1 旧快照（specsComparable=false）走另一条文案，不与 M3 这条混用', async () => {
    const wrapper = await compareWith(diff({ specsComparable: false, notComparableKinds: ['guideline'] }))
    const body = wrapper.find('.rb__diff-body').text()
    expect(body).toContain('不可比')
    expect(body).toContain('schema 1')
    expect(wrapper.find('[data-diff-not-comparable]').exists(), '整节都不可比时不该再报"某一类不可比"').toBe(false)
  })
})
