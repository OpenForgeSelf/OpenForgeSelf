/**
 * 向导状态机单测（AC8）：推进/回退/必填/单飞/失败保留/陈旧丢弃。
 * 网络三个动作全部注入受控 promise，因此这里证明的是**行为**而不是"看起来连上了"。
 */
import { describe, expect, it, vi } from 'vitest'
import type { PresetMatch, QuickCreateResult, QuickCreateInput, StylePreset } from '../api'
import { ApiError } from '../http'
import { CODE_PATTERN, SCENES, Wizard, sceneOf, stageScenarioOf, type WizardDeps } from './wizard'

/** 受控 promise（测单飞/陈旧丢弃用） */
function deferred<T>() {
  let resolve!: (v: T) => void
  let reject!: (e: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

function match(id: string): PresetMatch {
  return {
    id,
    name: `风格 ${id}`,
    tagline: `${id} 一句话`,
    score: 1,
    reasons: [],
    request: { hue: 10, radiusBase: 4, motionScale: 0.9, density: 'compact', seedColor: '#112233' },
  }
}

function catalogItem(id: string): StylePreset {
  return { id, name: `目录 ${id}`, tagline: '目录一句话', tones: [], kinds: [], industries: [], keywords: [] }
}

const okResult = (code: string): QuickCreateResult => ({
  uiRoute: '/x',
  dryRun: false,
  project: { code, name: '我的设计系统', status: 'active' },
  warnings: null,
})

function makeDeps(over: Partial<WizardDeps> = {}): WizardDeps {
  return {
    recommend: vi.fn(async () => [match('admin-calm'), match('finance-trust'), match('media-bold')]),
    catalog: vi.fn(async () => [catalogItem('admin-calm')]),
    create: vi.fn(async (i: QuickCreateInput) => okResult(i.code ?? 'auto-code')),
    reload: vi.fn(async () => ({ tokenCount: 120 })),
    ...over,
  }
}

/** 走到第④步且名称已填的向导 */
async function readyWizard(deps: WizardDeps, name = '我的设计系统'): Promise<Wizard> {
  const w = new Wizard(deps)
  w.pickScene('admin')
  await w.next() // ①→② 并触发推荐
  w.pickPreset(w.choices[0]?.id ?? 'admin-calm')
  await w.next() // ②→③
  await w.next() // ③→④
  w.name = name
  return w
}

describe('场景映射（§W）', () => {
  it('六类场景 → kind / 默认舞台场景', () => {
    expect(SCENES.map((s) => [s.id, s.kind, s.stageScenario])).toEqual([
      ['admin', 'console', 'admin'],
      ['workbench', 'console', 'workbench'],
      ['board', 'console', 'board'],
      ['landing', 'marketing', 'landing'],
      ['mobile', 'product', 'mobile'],
      ['other', 'product', 'admin'],
    ])
  })

  it('sceneOf / stageScenarioOf 空值回落', () => {
    expect(sceneOf('landing')?.kind).toBe('marketing')
    expect(sceneOf(null)).toBe(null)
    expect(stageScenarioOf(null)).toBe('admin')
    expect(stageScenarioOf('mobile')).toBe('mobile')
  })

  it('code 正则与后端一致', () => {
    expect(CODE_PATTERN.source).toBe('^[a-z0-9][a-z0-9-]{0,39}$')
  })
})

describe('步骤门禁与推进/回退', () => {
  it('①未选场景时不可推进', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    expect(w.canAdvance()).toBe(false)
    await w.next()
    expect(w.step).toBe(1)
    expect(deps.recommend).not.toHaveBeenCalled()
  })

  it('①→②：选场景后可推进并触发推荐（limit=3）', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    expect(w.step).toBe(2)
    expect(deps.recommend).toHaveBeenCalledWith({ kind: 'console', brief: '', limit: 3 })
    expect(w.matches.map((c) => c.id)).toEqual(['admin-calm', 'finance-trust', 'media-bold'])
  })

  it('②未选预设时不可推进；选后可推进到③、④', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    expect(w.canAdvance()).toBe(false)
    await w.next()
    expect(w.step).toBe(2)
    w.pickPreset('finance-trust')
    await w.next()
    expect(w.step).toBe(3)
    await w.next()
    expect(w.step).toBe(4)
  })

  it('③→④ 恒可（微调可跳过）', async () => {
    const deps = makeDeps()
    const w = await readyWizard(deps)
    expect(w.step).toBe(4)
    expect(w.tune.radiusBase).toBe(4) // 取自预设，不拍默认
  })

  it('回退保留全部输入', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    w.pickScene('landing')
    w.setBrief('一句话描述')
    await w.next()
    w.pickPreset('admin-calm')
    await w.next()
    w.back()
    expect(w.step).toBe(2)
    expect(w.scene).toBe('landing')
    expect(w.brief).toBe('一句话描述')
    expect(w.presetId).toBe('admin-calm')
  })
})

describe('第④步必填校验', () => {
  it('名称为空不可提交，超过 60 字不可提交', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    w.pickPreset('admin-calm')
    await w.next()
    await w.next()
    expect(w.canSubmit()).toBe(false)
    expect(w.nameError()).toContain('名字')
    w.name = 'x'.repeat(61)
    expect(w.canSubmit()).toBe(false)
    w.name = 'x'.repeat(60)
    expect(w.canSubmit()).toBe(true)
  })

  it('填了非法 code 不可提交', async () => {
    const deps = makeDeps()
    const w = await readyWizard(deps)
    w.code = 'Bad Code'
    expect(w.codeError()).not.toBe('')
    expect(w.canSubmit()).toBe(false)
    w.code = 'e2e-m2-123'
    expect(w.canSubmit()).toBe(true)
  })

  it('非法品牌色不可提交', async () => {
    const deps = makeDeps()
    const w = await readyWizard(deps)
    w.setTune({ seedColor: 'red' })
    expect(w.brandError()).not.toBe('')
    expect(w.canSubmit()).toBe(false)
    w.setTune({ seedColor: '#ff8800' })
    expect(w.canSubmit()).toBe(true)
  })
})

describe('创建：单飞 / 入参 / 回读 / 失败保留', () => {
  it('提交入参：preset + 微调合并 request + kind + description', async () => {
    const deps = makeDeps()
    const w = await readyWizard(deps)
    w.setBrief('做个后台')
    w.code = 'e2e-m2-1'
    w.setTune({ radiusBase: 12 })
    await w.submit()
    expect(deps.create).toHaveBeenCalledTimes(1)
    const arg = (deps.create as unknown as { mock: { calls: [QuickCreateInput][] } }).mock.calls[0][0]
    expect(arg.name).toBe('我的设计系统')
    expect(arg.code).toBe('e2e-m2-1')
    expect(arg.kind).toBe('console')
    expect(arg.description).toBe('做个后台')
    expect(arg.preset).toBe('admin-calm')
    expect(arg.dryRun).toBe(false)
    expect(arg.request?.radiusBase).toBe(12)
    expect(arg.request?.hue).toBe(10) // 其余沿用预设
  })

  it('创建成功 → done，并按 code 回读核对令牌数', async () => {
    const deps = makeDeps()
    const w = await readyWizard(deps)
    await w.submit()
    expect(w.phase).toBe('done')
    expect(deps.reload).toHaveBeenCalledWith('auto-code')
    expect(w.created).toEqual({ code: 'auto-code', name: '我的设计系统', tokenCount: 120 })
  })

  it('单飞：creating 期间重复提交被忽略（只发一次）', async () => {
    const d = deferred<QuickCreateResult>()
    const create = vi.fn(() => d.promise)
    const deps = makeDeps({ create: create as unknown as WizardDeps['create'] })
    const w = await readyWizard(deps)
    const p1 = w.submit()
    const p2 = w.submit()
    expect(create).toHaveBeenCalledTimes(1)
    d.resolve(okResult('auto-code'))
    await p1
    await p2
    expect(w.phase).toBe('done')
  })

  it('失败保留输入、停在④、展示后端原文', async () => {
    const deps = makeDeps({
      create: vi.fn(async () => {
        throw new ApiError(500, '生成失败：磁盘满了')
      }),
    })
    const w = await readyWizard(deps)
    w.code = 'keep-me'
    await w.submit()
    expect(w.phase).toBe('idle')
    expect(w.step).toBe(4)
    expect(w.name).toBe('我的设计系统')
    expect(w.code).toBe('keep-me')
    expect(w.error).toContain('磁盘满了')
    expect(w.advancedOpen).toBe(false)
  })

  it('code 冲突 → 展开「高级」', async () => {
    const deps = makeDeps({
      create: vi.fn(async () => {
        throw new ApiError(400, '项目 code「dup」已存在，请换一个（或留空自动分配）')
      }),
    })
    const w = await readyWizard(deps)
    w.code = 'dup'
    await w.submit()
    expect(w.error).toContain('已存在')
    expect(w.advancedOpen).toBe(true)
  })

  it('名称未填时 submit 不调用后端，仅提示', async () => {
    const deps = makeDeps()
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    w.pickPreset('admin-calm')
    await w.next()
    await w.next()
    await w.submit()
    expect(deps.create).not.toHaveBeenCalled()
    expect(w.error).toContain('名字')
  })
})

describe('推荐：陈旧丢弃 / 失败回落', () => {
  it('旧的推荐响应后到，不覆盖新的', async () => {
    const d1 = deferred<PresetMatch[]>()
    const d2 = deferred<PresetMatch[]>()
    const queue = [d1, d2]
    let i = 0
    const deps = makeDeps({ recommend: () => queue[i++].promise })
    const w = new Wizard(deps)
    w.pickScene('admin')
    const p1 = w.recommend()
    const p2 = w.recommend()
    d2.resolve([match('new-one')])
    await p2
    d1.resolve([match('old-one')])
    await p1
    expect(w.matches.map((c) => c.id)).toEqual(['new-one'])
    expect(w.recommendError).toBe('')
  })

  it('推荐失败 → 展示后端原文并回落全部预设', async () => {
    const deps = makeDeps({
      recommend: vi.fn(async (input: { limit: number }) => {
        if (input.limit === 3) throw new ApiError(500, '推荐服务挂了')
        return [match('admin-calm'), match('finance-trust')]
      }),
    })
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    expect(w.recommendError).toContain('推荐服务挂了')
    expect(w.choices.map((c) => c.id)).toEqual(['admin-calm', 'finance-trust'])
  })

  it('全量也失败 → 回落 GET presets 目录（无 request）', async () => {
    const deps = makeDeps({
      recommend: vi.fn(async () => {
        throw new ApiError(500, '全挂')
      }),
    })
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    expect(w.choices.map((c) => c.id)).toEqual(['admin-calm'])
    expect(w.choices[0].request).toBe(null)
  })

  it('展开「全部风格」会惰性取全量', async () => {
    const deps = makeDeps({
      recommend: vi.fn(async (input: { limit: number }) =>
        input.limit === 3 ? [match('a')] : [match('a'), match('b'), match('c')],
      ),
    })
    const w = new Wizard(deps)
    w.pickScene('admin')
    await w.next()
    expect(w.choices.map((c) => c.id)).toEqual(['a'])
    await w.toggleShowAll(true)
    expect(w.choices.map((c) => c.id)).toEqual(['a', 'b', 'c'])
  })
})